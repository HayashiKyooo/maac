using UnityEngine;

namespace ChibiCat
{
    /// <summary>
    /// Simple orbit camera. Click the Game view to capture the mouse (Esc releases it).
    /// Mouse / right stick orbits, wheel zooms, and the camera pulls in when something blocks the view.
    /// </summary>
    public class ChibiThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 0.6f, 0f);
        public float distance = 3.2f;
        public float minDistance = 1.5f;
        public float maxDistance = 7f;
        public float yaw = 0f;
        public float pitch = 15f;
        public float minPitch = -10f;
        public float maxPitch = 65f;
        public float lookSensitivity = 2.5f;
        public float followSmooth = 12f;
        public LayerMask collisionMask = ~0;
        public float collisionRadius = 0.2f;

        Vector3 focus;

        void Start()
        {
            if (target) focus = target.position + targetOffset;
            Lock(true);
        }

        static void Lock(bool on)
        {
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        void LateUpdate()
        {
            if (!target) return;
            if (ChibiInput.EscapeDown()) Lock(false);
            else if (ChibiInput.ClickDown()) Lock(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = ChibiInput.Look();
                yaw += look.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, minPitch, maxPitch);
            }
            distance = Mathf.Clamp(distance - ChibiInput.Zoom() * 0.5f, minDistance, maxDistance);

            Vector3 goal = target.position + targetOffset;
            focus = Vector3.Lerp(focus, goal, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.back;
            float d = distance;
            if (Physics.SphereCast(focus, collisionRadius, dir, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
                d = Mathf.Max(0.3f, hit.distance);
            transform.SetPositionAndRotation(focus + dir * d, rot);
        }
    }
}
