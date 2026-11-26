using UnityEngine;

namespace GlowDust
{
    /// <summary>Cracked floor tile: shakes when stepped on, falls into the abyss, and comes back after a while.</summary>
    public class CrumbleTile : DynamicStageElement
    {
        public float shakeTime = 0.9f;
        public float shakeAmount = 0.05f;
        public float fallTime = 1.6f;
        public float respawnTime = 6f;

        enum State { Idle, Shaking, Falling, Gone }
        State state;
        float timer, fallSpeed;
        Vector3 home;
        Renderer rend;

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            rend = GetComponent<Renderer>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Idle:
                    if (PlayerStandingOn()) { state = State.Shaking; timer = shakeTime; }
                    break;
                case State.Shaking:
                    timer -= dt;
                    transform.position = home + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * shakeAmount;
                    if (timer <= 0f)
                    {
                        state = State.Falling; timer = fallTime; fallSpeed = 0f;
                        transform.position = home;
                        if (PowderSystem.Instance != null) PowderSystem.Instance.EmitDebris(home, 25);
                    }
                    break;
                case State.Falling:
                    timer -= dt;
                    fallSpeed += 14f * dt;
                    transform.position += Vector3.down * fallSpeed * dt;
                    transform.Rotate(20f * dt, 0f, 12f * dt);
                    if (col.enabled && transform.position.y < home.y - 0.6f) col.enabled = false;
                    if (timer <= 0f) { state = State.Gone; timer = respawnTime; rend.enabled = false; }
                    break;
                case State.Gone:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        transform.SetPositionAndRotation(home, Quaternion.identity);
                        rend.enabled = true;
                        col.enabled = true;
                        state = State.Idle;
                    }
                    break;
            }
        }
    }
}
