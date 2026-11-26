using UnityEngine;

namespace GlowDust
{
    /// <summary>
    /// Owns the powder particle systems and simulates powder motion (wind, drag, obstacles, lifetime fade).
    /// Each frame the particles are splatted into <see cref="PowderVolume"/> so the stage becomes visible where powder is.
    /// Uses Unity ParticleSystem as a pooled renderer (no per-particle GameObjects, no Instantiate/Destroy).
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class PowderSystem : MonoBehaviour
    {
        public static PowderSystem Instance { get; private set; }

        [Header("References")]
        public PowderVolume volume;
        public WindField wind;
        public Transform viewer;
        public Material dustMaterial;
        public Material sparkMaterial;
        public Material moteMaterial;

        [Header("Capacity")]
        public int maxDustParticles = 12000;

        [Header("Burst (per impact)")]
        public int coreCount = 450;     // heavy, slow, stays around impact   -> strong reveal
        public int spreadCount = 650;   // medium, fast outward                -> mid reveal
        public int fineCount = 650;     // light, carried far by the wind      -> faint reveal
        public Vector2 coreLifetime = new Vector2(14f, 20f);
        public Vector2 spreadLifetime = new Vector2(11f, 17f);
        public Vector2 fineLifetime = new Vector2(16f, 24f);
        public Color[] palette =
        {
            new Color(0.62f, 0.35f, 1.0f),
            new Color(0.35f, 0.45f, 1.0f),
            new Color(0.25f, 0.85f, 1.0f),
            new Color(0.75f, 0.55f, 1.0f),
        };

        [Header("Reveal sources (impact deposit)")]
        public float impactRadius = 5f;
        public float impactStrength = 14f;
        public float impactLifetime = 20f;
        public float flashRadius = 7.5f;
        public float flashStrength = 9f;
        public float flashLifetime = 0.7f;

        [Header("Density weights")]
        public float heavyWeight = 1.4f;
        public float lightWeight = 0.9f;

        [Header("Motion")]
        public float heavySettle = 0.18f;   // downward drift of heavy grains (m/s)
        public float lightSettle = 0.02f;
        public float heavyDrag = 1.1f;      // how fast a grain adopts the wind velocity
        public float lightDrag = 2.6f;
        public float heavyWindFactor = 0.45f;
        public float lightWindFactor = 1.15f;
        [Range(0, 1)] public float wallFriction = 0.85f;

        [Header("Wind motes (ambient wind visualisation)")]
        public bool windMotes = true;
        public float moteRate = 35f;
        public float moteRadius = 11f;
        public int maxMotes = 700;

        const float MinSize = 0.015f, MaxSize = 0.09f;

        ParticleSystem dust, sparks, motes;
        ParticleSystem.Particle[] dustBuf, moteBuf;
        float moteAccumulator;

        public int ActiveDust => dust != null ? dust.particleCount : 0;

        void Awake()
        {
            Instance = this;
            if (volume == null) volume = GetComponent<PowderVolume>();
            if (wind == null) wind = GetComponent<WindField>();

            dust = CreateSystem("Dust", dustMaterial, maxDustParticles, ParticleSystemRenderMode.Billboard, 0f);
            sparks = CreateSystem("Sparks", sparkMaterial, 1500, ParticleSystemRenderMode.Stretch, 0.6f);
            motes = CreateSystem("WindMotes", moteMaterial, maxMotes, ParticleSystemRenderMode.Billboard, 0f);
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.velocityScale = 0.04f;
            sr.lengthScale = 1.5f;

            dustBuf = new ParticleSystem.Particle[maxDustParticles];
            moteBuf = new ParticleSystem.Particle[maxMotes];
        }

        void OnEnable() { if (volume != null) volume.Contribute += Simulate; }
        void OnDisable() { if (volume != null) volume.Contribute -= Simulate; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        ParticleSystem CreateSystem(string name, Material mat, int max, ParticleSystemRenderMode mode, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = true;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0;
            r.maxParticleSize = 2f;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Powder ball hit something: big burst of dust + sparks + reveal deposit.</summary>
        public void Burst(Vector3 point, Vector3 normal)
        {
            if (normal.sqrMagnitude < 1e-4f) normal = Vector3.up;
            normal.Normalize();
            Vector3 p = point + normal * 0.2f;

            EmitGroup(p, normal, coreCount, 0.3f, 2.2f, 0.05f, 0.09f, coreLifetime, 0.55f);
            EmitGroup(p, normal, spreadCount, 2.5f, 7.5f, 0.032f, 0.06f, spreadLifetime, 0.35f);
            EmitGroup(p, normal, fineCount, 1.0f, 4.5f, 0.015f, 0.032f, fineLifetime, 0.25f);

            // sparks
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 140; i++)
            {
                Vector3 dir = (Random.onUnitSphere + normal * 1.2f).normalized;
                ep.position = p;
                ep.velocity = dir * Random.Range(4f, 13f);
                ep.startLifetime = Random.Range(0.25f, 0.8f);
                ep.startSize = Random.Range(0.03f, 0.07f);
                ep.startColor = Color.Lerp(palette[Random.Range(0, palette.Length)], Color.white, 0.25f);
                sparks.Emit(ep, 1);
            }
            // flash blob
            ep = new ParticleSystem.EmitParams
            {
                position = p, velocity = Vector3.zero, startLifetime = 0.3f, startSize = 2.4f,
                startColor = new Color(0.6f, 0.5f, 1f, 0.45f)
            };
            sparks.Emit(ep, 1);

            if (volume != null)
            {
                volume.AddSource(p, impactRadius, impactStrength, impactLifetime, 0.3f);
                volume.AddSource(p, flashRadius, flashStrength, flashLifetime, 0f);
            }
        }

        void EmitGroup(Vector3 p, Vector3 normal, int count, float minSpeed, float maxSpeed,
                       float minSize, float maxSize, Vector2 lifetime, float spread)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 r = Random.onUnitSphere;
                if (Vector3.Dot(r, normal) < 0f) r = Vector3.Reflect(r, normal); // hemisphere
                Vector3 dir = Vector3.Slerp(r, normal, spread * Random.value).normalized;
                float speed = Mathf.Lerp(minSpeed, maxSpeed, Random.value * Random.value);
                ep.position = p + Random.insideUnitSphere * 0.25f;
                ep.velocity = dir * speed;
                ep.startLifetime = Random.Range(lifetime.x, lifetime.y);
                ep.startSize = Random.Range(minSize, maxSize);
                Color c = palette[Random.Range(0, palette.Length)];
                c.a = 0f; // faded in by Simulate
                ep.startColor = c;
                ep.randomSeed = (uint)Random.Range(1, int.MaxValue);
                dust.Emit(ep, 1);
            }
        }

        /// <summary>Small puff of sparks + a tiny reveal (crumbling floor, pickups).</summary>
        public void EmitDebris(Vector3 p, int count)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = p + Random.insideUnitSphere * 0.4f;
                ep.velocity = Random.onUnitSphere * Random.Range(1f, 3f) + Vector3.up;
                ep.startLifetime = Random.Range(0.3f, 0.9f);
                ep.startSize = Random.Range(0.02f, 0.05f);
                ep.startColor = palette[Random.Range(0, palette.Length)];
                sparks.Emit(ep, 1);
            }
            if (volume != null) volume.AddSource(p, 1.8f, 2.5f, 1.2f, 0.2f);
        }

        /// <summary>Few grains falling off the flying powder ball.</summary>
        public void EmitTrail(Vector3 p, Vector3 velocity)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 3; i++)
            {
                ep.position = p + Random.insideUnitSphere * 0.08f;
                ep.velocity = velocity * 0.05f + Random.insideUnitSphere * 0.4f;
                ep.startLifetime = Random.Range(4f, 8f);
                ep.startSize = Random.Range(0.015f, 0.03f);
                Color c = palette[Random.Range(0, palette.Length)]; c.a = 0f;
                ep.startColor = c;
                dust.Emit(ep, 1);
            }
        }

        // ------------------------------------------------------------------ simulation

        void Simulate(PowderVolume vol)
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            int n = dust.GetParticles(dustBuf);
            for (int i = 0; i < n; i++)
            {
                ParticleSystem.Particle pt = dustBuf[i];
                Vector3 pos = pt.position;
                Vector3 vel = pt.velocity;
                float light = Mathf.InverseLerp(MaxSize, MinSize, pt.startSize); // 0 heavy .. 1 light

                if (dt > 0f)
                {
                    Vector3 w = vol.GetWind(pos, t + (pt.randomSeed & 1023) * 0.01f);
                    Vector3 target = w * Mathf.Lerp(heavyWindFactor, lightWindFactor, light);
                    target.y -= Mathf.Lerp(heavySettle, lightSettle, light);
                    float k = Mathf.Lerp(heavyDrag, lightDrag, light);
                    vel += (target - vel) * (1f - Mathf.Exp(-k * dt));

                    // obstacle collision (predict next step, per axis)
                    float h = dt * 1.5f;
                    if (vol.IsBlocked(pos + vel * h))
                    {
                        if (vol.IsBlocked(new Vector3(pos.x + vel.x * h, pos.y, pos.z))) vel.x *= -0.15f;
                        if (vol.IsBlocked(new Vector3(pos.x, pos.y + vel.y * h, pos.z))) vel.y *= -0.1f;
                        if (vol.IsBlocked(new Vector3(pos.x, pos.y, pos.z + vel.z * h))) vel.z *= -0.15f;
                        vel *= wallFriction;
                    }
                    if (vol.IsBlocked(pos))
                    {
                        // stuck inside geometry (spawned too close) -> fade quickly
                        pt.remainingLifetime = Mathf.Min(pt.remainingLifetime, 0.5f);
                        vel = Vector3.zero;
                    }
                    pt.velocity = vel;
                }

                // lifetime fade (gradual) + fade-in + twinkle
                float life01 = pt.remainingLifetime / pt.startLifetime;
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(life01 / 0.45f));
                float fadeIn = Mathf.Clamp01((1f - life01) * pt.startLifetime * 4f);
                float fade = fadeOut * fadeIn;
                float twinkle = 0.7f + 0.3f * Mathf.Sin(t * 5f + (pt.randomSeed & 1023) * 0.37f);
                Color32 c = pt.startColor;
                c.a = (byte)(255f * fade * twinkle);
                pt.startColor = c;
                dustBuf[i] = pt;

                vol.Splat(pos, fade * Mathf.Lerp(heavyWeight, lightWeight, light));
            }
            dust.SetParticles(dustBuf, n);

            if (windMotes) SimulateMotes(vol, dt, t);
        }

        void SimulateMotes(PowderVolume vol, float dt, float t)
        {
            if (viewer != null && dt > 0f)
            {
                moteAccumulator += moteRate * dt;
                var ep = new ParticleSystem.EmitParams();
                int guard = 0;
                while (moteAccumulator >= 1f && guard++ < 20)
                {
                    moteAccumulator -= 1f;
                    Vector3 p = viewer.position + Vector3.Scale(Random.insideUnitSphere, new Vector3(moteRadius, 3f, moteRadius));
                    if (vol.IsBlocked(p)) continue;
                    ep.position = p;
                    ep.velocity = Vector3.zero;
                    ep.startLifetime = Random.Range(3f, 6f);
                    ep.startSize = Random.Range(0.012f, 0.028f);
                    ep.startColor = new Color(0.45f, 0.8f, 1f, 0f);
                    motes.Emit(ep, 1);
                }
            }

            int n = motes.GetParticles(moteBuf);
            for (int i = 0; i < n; i++)
            {
                ParticleSystem.Particle pt = moteBuf[i];
                Vector3 pos = pt.position;
                if (dt > 0f)
                {
                    Vector3 w = vol.GetWind(pos, t);
                    pt.velocity = Vector3.Lerp(pt.velocity, w * 1.2f, 1f - Mathf.Exp(-3f * dt));
                    if (vol.IsBlocked(pos)) pt.remainingLifetime = Mathf.Min(pt.remainingLifetime, 0.2f);
                }
                float life01 = pt.remainingLifetime / pt.startLifetime;
                float a = Mathf.Sin(life01 * Mathf.PI);
                Color32 c = pt.startColor;
                c.a = (byte)(255f * a * 0.55f);
                pt.startColor = c;
                moteBuf[i] = pt;
            }
            motes.SetParticles(moteBuf, n);
        }
    }
}
