using UnityEngine;
using UnityEngine.InputSystem;

namespace ChibiCat
{
    /// <summary>
    /// Third-person movement for ChibiCat. Camera-relative WASD / left stick, Shift (or left stick press) to run,
    /// Space / South button to jump. Drives the Animator "Speed" parameter and scales playback so the feet match
    /// the ground speed. Also blinks at random using the "Blink" blend shape.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ChibiCatController : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraTransform;
        public Animator animator;
        public SkinnedMeshRenderer faceRenderer;

        [Header("Movement")]
        public float walkSpeed = 1.3f;
        public float runSpeed = 2.6f;
        public float acceleration = 10f;
        public float turnSpeed = 720f;
        public float jumpHeight = 0.6f;
        public float gravity = -20f;

        [Header("Animation")]
        [Tooltip("Ground speed (m/s) at which the Walk clip plays at 1x.")]
        public float walkClipSpeed = 1.0f;
        public float maxAnimSpeed = 2.2f;

        [Header("Blink")]
        public Vector2 blinkInterval = new Vector2(2f, 5f);
        public float blinkDuration = 0.12f;

        CharacterController _cc;
        Vector3 _planarVel;
        float _vy;
        float _blinkTimer;
        float _blinkT = -1f;
        int _blinkIndex = -1;
        static readonly int SpeedId = Animator.StringToHash("Speed");

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (!animator) animator = GetComponent<Animator>();
            if (animator) animator.applyRootMotion = false;
            if (!faceRenderer) faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (faceRenderer && faceRenderer.sharedMesh)
                _blinkIndex = faceRenderer.sharedMesh.GetBlendShapeIndex("Blink");
            if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
            _blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        }

        void Update()
        {
            Vector2 move = ReadMove();
            bool run = ReadRun();
            bool jump = ReadJump();

            // camera-relative direction on the ground plane
            Vector3 fwd = cameraTransform ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform ? cameraTransform.right : Vector3.right;
            fwd.y = 0; right.y = 0; fwd.Normalize(); right.Normalize();
            Vector3 wish = fwd * move.y + right * move.x;
            float mag = Mathf.Clamp01(wish.magnitude);
            if (mag > 0.001f) wish /= wish.magnitude;

            float target = mag * (run ? runSpeed : walkSpeed);
            _planarVel = Vector3.MoveTowards(_planarVel, wish * target, acceleration * Time.deltaTime);

            if (mag > 0.05f)
            {
                Quaternion look = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }

            if (_cc.isGrounded)
            {
                if (_vy < 0) _vy = -2f;
                if (jump) _vy = Mathf.Sqrt(-2f * gravity * jumpHeight);
            }
            _vy += gravity * Time.deltaTime;

            _cc.Move((_planarVel + Vector3.up * _vy) * Time.deltaTime);

            UpdateAnimator();
            UpdateBlink();
        }

        void UpdateAnimator()
        {
            if (!animator) return;
            float speed = new Vector3(_planarVel.x, 0, _planarVel.z).magnitude;
            animator.SetFloat(SpeedId, speed);
            var st = animator.GetCurrentAnimatorStateInfo(0);
            animator.speed = st.IsName("Walk") ? Mathf.Clamp(speed / walkClipSpeed, 0.6f, maxAnimSpeed) : 1f;
        }

        void UpdateBlink()
        {
            if (_blinkIndex < 0) return;
            if (_blinkT < 0)
            {
                _blinkTimer -= Time.deltaTime;
                if (_blinkTimer <= 0) _blinkT = 0;
                return;
            }
            _blinkT += Time.deltaTime;
            float p = _blinkT / blinkDuration;            // 0..2 : close then open
            float w = p < 1 ? p : 2 - p;
            faceRenderer.SetBlendShapeWeight(_blinkIndex, Mathf.Clamp01(w) * 100f);
            if (p >= 2)
            {
                faceRenderer.SetBlendShapeWeight(_blinkIndex, 0);
                _blinkT = -1;
                _blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
            }
        }

        static Vector2 ReadMove()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
            }
            var gp = Gamepad.current;
            if (gp != null && gp.leftStick.ReadValue().sqrMagnitude > v.sqrMagnitude) v = gp.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        static bool ReadRun()
        {
            var kb = Keyboard.current; var gp = Gamepad.current;
            return (kb != null && kb.leftShiftKey.isPressed) || (gp != null && (gp.leftStickButton.isPressed || gp.rightTrigger.isPressed));
        }

        static bool ReadJump()
        {
            var kb = Keyboard.current; var gp = Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
        }
    }
}
