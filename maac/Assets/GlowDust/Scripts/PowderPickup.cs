using UnityEngine;

namespace GlowDust
{
    /// <summary>Faintly glowing powder pouch. Walk into it to refill powder.</summary>
    public class PowderPickup : MonoBehaviour
    {
        public int amount = 3;
        public PowderThrower thrower;
        public GlowDustHUD hud;
        public float radius = 1.3f;

        Vector3 basePos;

        void Start()
        {
            basePos = transform.position;
            if (thrower == null) thrower = FindAnyObjectByType<PowderThrower>();
        }

        void Update()
        {
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2f + basePos.x) * 0.1f);
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            if (thrower == null) return;
            Vector3 d = thrower.transform.position - basePos;
            d.y *= 0.5f;
            if (d.sqrMagnitude > radius * radius) return;
            if (thrower.Count >= thrower.MaxCount) return; // keep it for later
            int before = thrower.Count;
            thrower.AddPowder(amount);
            if (hud != null) hud.ShowMessage($"+{thrower.Count - before} glow powder", 1.8f);
            if (PowderSystem.Instance != null) PowderSystem.Instance.EmitDebris(basePos, 15);
            gameObject.SetActive(false);
        }
    }
}
