using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlowDust
{
    /// <summary>
    /// World-space 3D density grid of glow powder.
    /// Every frame: clear -> stamp reveal sources (impact deposits, flashes) -> contributors splat particles -> upload Texture3D.
    /// Stage shaders (GlowDust/RevealStone) sample this texture to decide how visible each surface is.
    /// Also stores a static occupancy grid (walls/obstacles) used for powder collision and wind deflection.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PowderVolume : MonoBehaviour
    {
        public static PowderVolume Instance { get; private set; }

        [Header("Volume bounds (world)")]
        public Vector3 origin = new Vector3(-0.6f, -0.6f, -0.6f);
        public Vector3 size = new Vector3(55.2f, 7.8f, 49.2f);
        [Min(0.2f)] public float cellSize = 0.6f;

        [Header("Obstacles")]
        [Tooltip("Colliders on these layers block powder and deflect wind.")]
        public LayerMask obstacleMask = ~0;

        [Header("Viewer")]
        public Transform viewer;

        [Header("Debug")]
        public bool drawOccupancyGizmos;

        /// <summary>Called during LateUpdate after reveal sources were stamped; splat particles here.</summary>
        public event Action<PowderVolume> Contribute;

        public int SizeX => nx;
        public int SizeY => ny;
        public int SizeZ => nz;

        struct RevealSource
        {
            public Vector3 position;
            public float radius;
            public float strength;
            public float age;
            public float lifetime;
            public float fadeStart; // normalized age where fading starts
        }

        readonly List<RevealSource> sources = new List<RevealSource>(64);
        float[] density;
        bool[] blocked;          // collision grid at half the density cell size (finer, for powder collision)
        int cx, cy, cz;          // collision grid dims
        float invColCell;
        Vector3[] bakedWind;     // per density cell: base wind already deflected by obstacles
        float[] bakedTurb;       // per density cell: turbulence amplitude
        int bakedWindVersion = -1;
        float[] kx = new float[64], ky = new float[64], kz = new float[64];
        Texture3D tex;
        int nx, ny, nz;
        float invCell;

        static readonly int IdTex = Shader.PropertyToID("_GD_DensityTex");
        static readonly int IdOrigin = Shader.PropertyToID("_GD_VolumeOrigin");
        static readonly int IdInvSize = Shader.PropertyToID("_GD_VolumeInvSize");
        static readonly int IdCell = Shader.PropertyToID("_GD_CellSize");
        static readonly int IdPlayer = Shader.PropertyToID("_GD_PlayerPos");
        static readonly int IdEnabled = Shader.PropertyToID("_GD_Enabled");

        void Awake()
        {
            Instance = this;
            invCell = 1f / cellSize;
            nx = Mathf.Max(2, Mathf.CeilToInt(size.x * invCell - 0.001f));
            ny = Mathf.Max(2, Mathf.CeilToInt(size.y * invCell - 0.001f));
            nz = Mathf.Max(2, Mathf.CeilToInt(size.z * invCell - 0.001f));
            size = new Vector3(nx, ny, nz) * cellSize;

            density = new float[nx * ny * nz];
            bakedWind = new Vector3[nx * ny * nz];
            bakedTurb = new float[nx * ny * nz];
            cx = nx * 2; cy = ny * 2; cz = nz * 2;
            invColCell = invCell * 2f;
            blocked = new bool[cx * cy * cz];
            tex = new Texture3D(nx, ny, nz, TextureFormat.RFloat, false)
            {
                name = "GlowDustDensity",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixelData(density, 0);
            tex.Apply(false, false);

            Shader.SetGlobalTexture(IdTex, tex);
            Shader.SetGlobalVector(IdOrigin, origin);
            Shader.SetGlobalVector(IdInvSize, new Vector3(1f / size.x, 1f / size.y, 1f / size.z));
            Shader.SetGlobalVector(IdCell, new Vector4(cellSize, 0, 0, 0));
            Shader.SetGlobalFloat(IdEnabled, 1f);
        }

        void Start()
        {
            RebuildOccupancy();
            BakeWind();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat(IdEnabled, 0f);
            if (tex != null) Destroy(tex);
        }

        // ------------------------------------------------------------------ occupancy

        /// <summary>Rasterize all solid colliders into the occupancy grid. Call again if the stage changes.</summary>
        public void RebuildOccupancy()
        {
            Array.Clear(blocked, 0, blocked.Length);
            var cols = FindObjectsByType<Collider>();
            int count = 0;
            float h = cellSize * 0.5f;
            foreach (var col in cols)
            {
                if (col.isTrigger || !col.enabled || col is CharacterController) continue;
                if ((obstacleMask.value & (1 << col.gameObject.layer)) == 0) continue;
                if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue;
                if (col.GetComponentInParent<DynamicStageElement>() != null) continue; // moving / crumbling pieces
                count++;
                Bounds b = col.bounds;
                Vector3 a = (b.min - origin) * invColCell, e = (b.max - origin) * invColCell;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(a.x), 0, cx - 1), x1 = Mathf.Clamp(Mathf.FloorToInt(e.x), 0, cx - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(a.y), 0, cy - 1), y1 = Mathf.Clamp(Mathf.FloorToInt(e.y), 0, cy - 1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt(a.z), 0, cz - 1), z1 = Mathf.Clamp(Mathf.FloorToInt(e.z), 0, cz - 1);
                bool axisAlignedBox = col is BoxCollider && IsAxisAligned(col.transform.rotation);
                for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector3 c = origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * h;
                    bool inside = axisAlignedBox ? b.Contains(c) : (col.ClosestPoint(c) - c).sqrMagnitude < 1e-6f;
                    if (inside) blocked[x + cx * (y + cy * z)] = true;
                }
            }
            Debug.Log($"[GlowDust] Occupancy built from {count} colliders (density {nx}x{ny}x{nz}, collision {cx}x{cy}x{cz}).");
        }

        static bool IsAxisAligned(Quaternion q)
        {
            Vector3 e = q.eulerAngles;
            return Mathf.Abs(Mathf.DeltaAngle(e.x, Mathf.Round(e.x / 90f) * 90f)) < 0.5f &&
                   Mathf.Abs(Mathf.DeltaAngle(e.y, Mathf.Round(e.y / 90f) * 90f)) < 0.5f &&
                   Mathf.Abs(Mathf.DeltaAngle(e.z, Mathf.Round(e.z / 90f) * 90f)) < 0.5f;
        }

        void CellRange(Vector3 min, Vector3 max, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1)
        {
            Vector3 a = (min - origin) * invCell;
            Vector3 b = (max - origin) * invCell;
            x0 = Mathf.Clamp(Mathf.FloorToInt(a.x), 0, nx - 1); x1 = Mathf.Clamp(Mathf.FloorToInt(b.x), 0, nx - 1);
            y0 = Mathf.Clamp(Mathf.FloorToInt(a.y), 0, ny - 1); y1 = Mathf.Clamp(Mathf.FloorToInt(b.y), 0, ny - 1);
            z0 = Mathf.Clamp(Mathf.FloorToInt(a.z), 0, nz - 1); z1 = Mathf.Clamp(Mathf.FloorToInt(b.z), 0, nz - 1);
        }

        int Index(int x, int y, int z) => x + nx * (y + ny * z);
        Vector3 CellCenter(int x, int y, int z) => origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSize;

        /// <summary>True if the point is inside an obstacle or outside the volume.</summary>
        public bool IsBlocked(Vector3 p)
        {
            float fx = (p.x - origin.x) * invColCell, fy = (p.y - origin.y) * invColCell, fz = (p.z - origin.z) * invColCell;
            if (fx < 0 || fy < 0 || fz < 0) return true;
            int x = (int)fx, y = (int)fy, z = (int)fz;
            if (x >= cx || y >= cy || z >= cz) return true;
            return blocked[x + cx * (y + cy * z)];
        }

        /// <summary>Remove wind components that push into obstacles and redirect them along the surface.</summary>
        public Vector3 DeflectByObstacles(Vector3 p, Vector3 v)
        {
            float look = cellSize;
            float removed = 0f;
            if (v.x != 0f && IsBlocked(new Vector3(p.x + Mathf.Sign(v.x) * look, p.y, p.z))) { removed += Mathf.Abs(v.x); v.x = 0f; }
            if (v.z != 0f && IsBlocked(new Vector3(p.x, p.y, p.z + Mathf.Sign(v.z) * look))) { removed += Mathf.Abs(v.z); v.z = 0f; }
            if (v.y != 0f && IsBlocked(new Vector3(p.x, p.y + Mathf.Sign(v.y) * look, p.z))) { removed += Mathf.Abs(v.y) * 0.5f; v.y = 0f; }
            if (removed > 0f)
            {
                float m = v.magnitude;
                if (m > 1e-4f) v *= (m + removed * 0.6f) / m;
                else v.y += removed * 0.15f; // dead-end pocket: let it slowly rise/swirl
            }
            return v;
        }

        // ------------------------------------------------------------------ wind cache

        /// <summary>Bake static wind (global + WindVolumes + obstacle deflection) into the grid. Auto re-bakes when WindField.Version changes.</summary>
        public void BakeWind()
        {
            var wf = WindField.Instance;
            bakedWindVersion = WindField.Version;
            if (wf == null) { Array.Clear(bakedWind, 0, bakedWind.Length); Array.Clear(bakedTurb, 0, bakedTurb.Length); return; }
            for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                int i = Index(x, y, z);
                Vector3 c = CellCenter(x, y, z);
                Vector3 w = wf.SampleBase(c, out float turb);
                bakedWind[i] = DeflectByObstacles(c, w);
                bakedTurb[i] = turb;
            }
        }

        /// <summary>Fast wind lookup for particles: baked base wind of the cell + procedural turbulence.</summary>
        public Vector3 GetWind(Vector3 p, float time)
        {
            float fx = (p.x - origin.x) * invCell, fy = (p.y - origin.y) * invCell, fz = (p.z - origin.z) * invCell;
            if (fx < 0 || fy < 0 || fz < 0) return Vector3.zero;
            int x = (int)fx, y = (int)fy, z = (int)fz;
            if (x >= nx || y >= ny || z >= nz) return Vector3.zero;
            int i = x + nx * (y + ny * z);
            float amp = bakedTurb[i];
            Vector3 w = bakedWind[i];
            if (amp > 0f && WindField.Instance != null) w += WindField.Instance.Turbulence(p, time) * amp;
            return w;
        }

        // ------------------------------------------------------------------ density

        /// <summary>Trilinear splat of a point amount of powder.</summary>
        public void Splat(Vector3 p, float amount)
        {
            float fx = (p.x - origin.x) * invCell - 0.5f;
            float fy = (p.y - origin.y) * invCell - 0.5f;
            float fz = (p.z - origin.z) * invCell - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy), z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0, ty = fy - y0, tz = fz - z0;
            if (x0 < 0 || y0 < 0 || z0 < 0 || x0 >= nx - 1 || y0 >= ny - 1 || z0 >= nz - 1) return;
            int i = x0 + nx * (y0 + ny * z0);
            int sy = nx, sz = nx * ny;
            float ax = 1 - tx, ay = 1 - ty, az = 1 - tz;
            density[i]                += amount * ax * ay * az;
            density[i + 1]            += amount * tx * ay * az;
            density[i + sy]           += amount * ax * ty * az;
            density[i + 1 + sy]       += amount * tx * ty * az;
            density[i + sz]           += amount * ax * ay * tz;
            density[i + 1 + sz]       += amount * tx * ay * tz;
            density[i + sy + sz]      += amount * ax * ty * tz;
            density[i + 1 + sy + sz]  += amount * tx * ty * tz;
        }

        /// <summary>Spherical reveal source (impact deposit / flash / trail). Fades out over its lifetime.</summary>
        public void AddSource(Vector3 position, float radius, float strength, float lifetime, float fadeStart = 0.35f)
        {
            sources.Add(new RevealSource
            {
                position = position, radius = radius, strength = strength,
                lifetime = Mathf.Max(0.01f, lifetime), fadeStart = Mathf.Clamp01(fadeStart)
            });
        }

        public float SampleDensity(Vector3 p)
        {
            float fx = (p.x - origin.x) * invCell, fy = (p.y - origin.y) * invCell, fz = (p.z - origin.z) * invCell;
            int x = (int)fx, y = (int)fy, z = (int)fz;
            if (fx < 0 || fy < 0 || fz < 0 || x >= nx || y >= ny || z >= nz) return 0f;
            return density[Index(x, y, z)];
        }

        // Gaussian falloff (separable -> no exp per cell): radius = core radius, influence reaches 2x radius.
        void StampSource(in RevealSource s, float amount)
        {
            float reach = s.radius * 2f;
            CellRange(s.position - Vector3.one * reach, s.position + Vector3.one * reach,
                out int x0, out int y0, out int z0, out int x1, out int y1, out int z1);
            int sx = x1 - x0 + 1, sy = y1 - y0 + 1, sz = z1 - z0 + 1;
            if (sx > kx.Length || sy > ky.Length || sz > kz.Length)
            {
                int m = Mathf.Max(sx, Mathf.Max(sy, sz));
                kx = new float[m]; ky = new float[m]; kz = new float[m];
            }
            float k = 2.5f / (s.radius * s.radius);
            for (int x = 0; x < sx; x++) { float d = origin.x + (x0 + x + 0.5f) * cellSize - s.position.x; kx[x] = Mathf.Exp(-k * d * d); }
            for (int y = 0; y < sy; y++) { float d = origin.y + (y0 + y + 0.5f) * cellSize - s.position.y; ky[y] = Mathf.Exp(-k * d * d) * amount; }
            for (int z = 0; z < sz; z++) { float d = origin.z + (z0 + z + 0.5f) * cellSize - s.position.z; kz[z] = Mathf.Exp(-k * d * d); }
            const float cutoff = 0.002f;
            for (int z = 0; z < sz; z++)
            {
                float wz = kz[z];
                for (int y = 0; y < sy; y++)
                {
                    float wyz = wz * ky[y];
                    if (wyz < cutoff * amount) continue;
                    int row = nx * ((y0 + y) + ny * (z0 + z)) + x0;
                    for (int x = 0; x < sx; x++) density[row + x] += wyz * kx[x];
                }
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (bakedWindVersion != WindField.Version) BakeWind();
            Array.Clear(density, 0, density.Length);

            for (int i = sources.Count - 1; i >= 0; i--)
            {
                RevealSource s = sources[i];
                s.age += dt;
                if (s.age >= s.lifetime) { sources.RemoveAt(i); continue; }
                sources[i] = s;
                float t = s.age / s.lifetime;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.fadeStart, 1f, t));
                StampSource(s, s.strength * fade);
            }

            Contribute?.Invoke(this);

            tex.SetPixelData(density, 0);
            tex.Apply(false, false);

            if (viewer != null) Shader.SetGlobalVector(IdPlayer, viewer.position);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireCube(origin + size * 0.5f, size);
            if (!drawOccupancyGizmos || blocked == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.15f);
            float h = cellSize * 0.5f;
            for (int z = 0; z < cz; z += 3)
            for (int y = 0; y < cy; y += 3)
            for (int x = 0; x < cx; x += 3)
                if (blocked[x + cx * (y + cy * z)]) Gizmos.DrawCube(origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * h, Vector3.one * h);
        }
    }
}
