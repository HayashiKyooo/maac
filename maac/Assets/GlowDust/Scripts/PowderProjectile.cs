using UnityEngine;

namespace GlowDust
{
    /// <summary>Pooled powder ball. Flies on a parabola (Rigidbody gravity) and bursts on first contact.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PowderProjectile : MonoBehaviour
    {
        public float maxFlightTime = 6f;
        public float trailInterval = 0.05f;

        Rigidbody rb;
        Collider col;
        TrailRenderer trail;
        PowderThrower owner;
        float launchTime, nextTrail;
        bool live;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            trail = GetComponentInChildren<TrailRenderer>();
        }

        public void Launch(PowderThrower thrower, Vector3 position, Vector3 velocity, Collider[] ignore)
        {
            owner = thrower;
            transform.position = position;
            gameObject.SetActive(true);
            rb.position = position;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            if (ignore != null && col != null)
                foreach (var c in ignore) if (c != null) Physics.IgnoreCollision(col, c, true);
            if (trail != null) trail.Clear();
            launchTime = Time.time;
            nextTrail = 0f;
            live = true;
        }

        void Update()
        {
            if (!live) return;
            Vector3 p = transform.position;
            if (Time.time >= nextTrail)
            {
                nextTrail = Time.time + trailInterval;
                if (PowderVolume.Instance != null) PowderVolume.Instance.AddSource(p, 1.8f, 3.5f, 0.6f, 0f);
                if (PowderSystem.Instance != null) PowderSystem.Instance.EmitTrail(p, rb.linearVelocity);
            }
            if (Time.time - launchTime > maxFlightTime) Explode(p, -rb.linearVelocity.normalized);
        }

        void OnCollisionEnter(Collision c)
        {
            if (!live) return;
            ContactPoint cp = c.GetContact(0);
            Explode(cp.point, cp.normal);
        }

        void Explode(Vector3 point, Vector3 normal)
        {
            live = false;
            if (PowderSystem.Instance != null) PowderSystem.Instance.Burst(point, normal);
            rb.linearVelocity = Vector3.zero;
            gameObject.SetActive(false);
            if (owner != null) owner.Return(this);
        }
    }
}
