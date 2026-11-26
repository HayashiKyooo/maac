using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlowDust
{
    /// <summary>Left click: throw a powder ball toward the crosshair. Manages the powder count and a projectile pool.</summary>
    public class PowderThrower : MonoBehaviour
    {
        public Camera viewCamera;
        public PowderProjectile projectileTemplate;
        [Min(0)] public int startCount = 5;
        [Min(1)] public int maxCount = 5;
        public float throwSpeed = 15f;
        [Tooltip("Extra upward velocity as a fraction of throwSpeed (gives a nicer arc)")]
        public float upwardBias = 0.12f;
        public float cooldown = 0.35f;
        public int poolSize = 6;

        public int Count { get; private set; }
        public int MaxCount => maxCount;
        public event Action<int, int> CountChanged;  // (count, max)
        public event Action ThrowRejected;           // tried to throw with 0 powder

        readonly Stack<PowderProjectile> pool = new Stack<PowderProjectile>();
        CharacterController cc;
        Collider[] ownColliders;
        float nextThrow;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            Count = Mathf.Min(startCount, maxCount);
            if (projectileTemplate != null)
            {
                projectileTemplate.gameObject.SetActive(false);
                for (int i = 0; i < poolSize; i++) pool.Push(CreateProjectile());
            }
        }

        void Start() { CountChanged?.Invoke(Count, maxCount); }

        PowderProjectile CreateProjectile()
        {
            var p = Instantiate(projectileTemplate, projectileTemplate.transform.parent);
            p.name = projectileTemplate.name + "(Pooled)";
            p.gameObject.SetActive(false);
            return p;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            TryThrow();
        }

        public bool TryThrow()
        {
            if (Time.time < nextThrow || viewCamera == null || projectileTemplate == null) return false;
            if (Count <= 0) { ThrowRejected?.Invoke(); return false; }

            Count--;
            nextThrow = Time.time + cooldown;
            CountChanged?.Invoke(Count, maxCount);

            Transform cam = viewCamera.transform;
            Vector3 origin = cam.position + cam.forward * 0.45f + cam.right * 0.12f - cam.up * 0.08f;
            Vector3 vel = cam.forward * throwSpeed + Vector3.up * (throwSpeed * upwardBias);
            if (cc != null) vel += cc.velocity * 0.5f;

            var proj = pool.Count > 0 ? pool.Pop() : CreateProjectile();
            proj.Launch(this, origin, vel, ownColliders);
            return true;
        }

        public void Return(PowderProjectile p) { pool.Push(p); }

        public void AddPowder(int amount)
        {
            Count = Mathf.Clamp(Count + amount, 0, maxCount);
            CountChanged?.Invoke(Count, maxCount);
        }
    }
}
