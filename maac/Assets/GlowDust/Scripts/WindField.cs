using System.Collections.Generic;
using UnityEngine;

namespace GlowDust
{
    /// <summary>
    /// Wind sampling: global wind + local <see cref="WindVolume"/> overrides + cheap procedural turbulence.
    /// Obstacle deflection is applied by the caller through PowderVolume.DeflectByObstacles.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class WindField : MonoBehaviour
    {
        public static WindField Instance { get; private set; }
        static readonly List<WindVolume> volumes = new List<WindVolume>();

        public Vector3 globalDirection = new Vector3(0.3f, 0f, 1f);
        [Min(0)] public float globalStrength = 0.35f;
        [Min(0)] public float turbulence = 0.3f;
        [Min(0.01f)] public float turbulenceScale = 0.45f;
        [Min(0)] public float turbulenceSpeed = 0.5f;

        Vector3 globalWind;

        public static void Register(WindVolume v) { if (!volumes.Contains(v)) volumes.Add(v); Version++; }
        public static void Unregister(WindVolume v) { volumes.Remove(v); Version++; }

        void Awake() { Instance = this; Cache(); }
        void OnValidate() { Cache(); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Cache()
        {
            Version++;
            globalWind = (globalDirection.sqrMagnitude > 1e-6f ? globalDirection.normalized : Vector3.zero) * globalStrength;
        }

        /// <summary>Incremented whenever wind settings / volumes change (consumers re-bake their caches).</summary>
        public static int Version { get; private set; }
        public static void MarkDirty() { Version++; }

        /// <summary>Full wind sample (base + turbulence). Prefer the baked grid in PowderVolume for many queries.</summary>
        public Vector3 Sample(Vector3 p, float time)
        {
            Vector3 w = SampleBase(p, out float turb);
            return w + Turbulence(p, time) * turb;
        }

        /// <summary>Static part of the wind (global + volumes) and the local turbulence amplitude.</summary>
        public Vector3 SampleBase(Vector3 p, out float turb)
        {
            Vector3 w = globalWind;
            turb = turbulence;
            for (int i = 0; i < volumes.Count; i++)
            {
                float k = volumes[i].Weight(p);
                if (k <= 0f) continue;
                w = Vector3.LerpUnclamped(w, volumes[i].Wind, k);
                turb = Mathf.LerpUnclamped(turb, volumes[i].Turbulence, k);
            }
            return w;
        }

        /// <summary>Unit-amplitude procedural turbulence (cheap sum of sines, time varying).</summary>
        public Vector3 Turbulence(Vector3 p, float time)
        {
            float s = turbulenceScale, t = time * turbulenceSpeed;
            float x = p.x * s, y = p.y * s, z = p.z * s;
            return new Vector3(
                Mathf.Sin(z * 1.3f + t * 1.7f) + Mathf.Sin(y * 2.1f - t * 1.1f) * 0.5f,
                (Mathf.Sin(x * 1.7f + t * 1.3f) + Mathf.Cos(z * 1.1f - t * 0.9f)) * 0.35f,
                Mathf.Cos(x * 1.2f - t * 1.5f) + Mathf.Sin(y * 1.9f + t * 0.8f) * 0.5f);
        }
    }
}
