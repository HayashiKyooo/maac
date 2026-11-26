using UnityEngine;

namespace GlowDust
{
    /// <summary>
    /// Local wind override (box). Place several of these to give each area its own wind direction/strength.
    /// Edges are blended with <see cref="edgeSoftness"/>. Later volumes in the list win when overlapping.
    /// </summary>
    public class WindVolume : MonoBehaviour
    {
        public Vector3 size = new Vector3(6, 6, 6);
        [Tooltip("Wind direction (world space unless localDirection is on)")]
        public Vector3 direction = Vector3.forward;
        public bool localDirection;
        [Min(0)] public float strength = 1.5f;
        [Min(0)] public float turbulence = 0.3f;
        [Min(0.01f)] public float edgeSoftness = 1.5f;

        Vector3 cachedPos;
        Quaternion cachedInvRot;
        Vector3 cachedWind;

        public Vector3 Wind => cachedWind;
        public float Turbulence => turbulence;

        void OnEnable() { Cache(); WindField.Register(this); }
        void OnDisable() { WindField.Unregister(this); }
        void OnValidate() { Cache(); }

        /// <summary>Call if the volume is moved/changed at runtime.</summary>
        public void Cache()
        {
            cachedPos = transform.position;
            cachedInvRot = Quaternion.Inverse(transform.rotation);
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
            if (localDirection) dir = transform.rotation * dir;
            cachedWind = dir * strength;
            WindField.MarkDirty();
        }

        /// <summary>0 outside, 1 well inside.</summary>
        public float Weight(Vector3 p)
        {
            Vector3 l = cachedInvRot * (p - cachedPos);
            float dx = Mathf.Abs(l.x) - size.x * 0.5f;
            float dy = Mathf.Abs(l.y) - size.y * 0.5f;
            float dz = Mathf.Abs(l.z) - size.z * 0.5f;
            float m = Mathf.Max(dx, Mathf.Max(dy, dz));
            if (m >= 0f) return 0f;
            return Mathf.Clamp01(-m / edgeSoftness);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.25f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 dir = direction.normalized;
            if (localDirection) dir = transform.rotation * dir;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Vector3 a = transform.position;
            Vector3 b = a + dir * (1f + strength);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(b, 0.15f);
        }
    }
}
