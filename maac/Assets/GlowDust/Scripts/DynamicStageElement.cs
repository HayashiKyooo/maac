using UnityEngine;

namespace GlowDust
{
    /// <summary>
    /// Base for moving / changing stage pieces (moving platforms, crumbling floors).
    /// They are excluded from the static powder occupancy grid and can detect the player standing on them.
    /// </summary>
    public abstract class DynamicStageElement : MonoBehaviour
    {
        public CharacterController player;
        protected Collider col;

        protected virtual void Awake()
        {
            col = GetComponent<Collider>();
            if (player == null) player = FindAnyObjectByType<CharacterController>();
        }

        /// <summary>True if the player's feet are on top of this element.</summary>
        protected bool PlayerStandingOn()
        {
            if (player == null || col == null || !col.enabled) return false;
            Bounds b = col.bounds;
            Vector3 p = player.transform.position;
            float m = player.radius * 0.6f;
            if (p.x < b.min.x - m || p.x > b.max.x + m || p.z < b.min.z - m || p.z > b.max.z + m) return false;
            float dy = p.y - b.max.y;
            return dy > -0.25f && dy < 0.3f;
        }
    }
}
