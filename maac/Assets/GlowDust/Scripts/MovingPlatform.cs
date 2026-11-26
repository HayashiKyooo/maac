using UnityEngine;

namespace GlowDust
{
    /// <summary>Platform that ping-pongs between two points and carries the player standing on it.</summary>
    [DefaultExecutionOrder(-50)] // move before the FirstPersonController updates
    public class MovingPlatform : DynamicStageElement
    {
        public Vector3 pointA;
        public Vector3 pointB;
        [Min(0.1f)] public float speed = 1.8f;
        [Min(0)] public float waitTime = 1.0f;
        [Range(0, 1)] public float startPhase;

        float travel;     // 0..1 along A->B
        int direction = 1;
        float wait;

        void Start()
        {
            travel = startPhase;
            transform.position = Vector3.Lerp(pointA, pointB, Smooth(travel));
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool riding = PlayerStandingOn();
            Vector3 before = transform.position;

            if (wait > 0f) wait -= dt;
            else
            {
                float len = Mathf.Max(0.01f, Vector3.Distance(pointA, pointB));
                travel += direction * dt * speed / len;
                if (travel >= 1f) { travel = 1f; direction = -1; wait = waitTime; }
                else if (travel <= 0f) { travel = 0f; direction = 1; wait = waitTime; }
            }
            Vector3 after = Vector3.Lerp(pointA, pointB, Smooth(travel));
            transform.position = after;
            Physics.SyncTransforms();

            if (riding && player.enabled) player.Move(after - before);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Gizmos.DrawLine(pointA, pointB);
            Gizmos.DrawWireSphere(pointA, 0.2f);
            Gizmos.DrawWireSphere(pointB, 0.2f);
        }
    }
}
