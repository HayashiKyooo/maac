using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ChibiCat
{
    /// <summary>Reads keyboard / mouse / gamepad through whichever input backend the project has enabled.</summary>
    public static class ChibiInput
    {
        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            }
            var g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static Vector2 Look()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 v = Vector2.zero;
            var m = Mouse.current;
            if (m != null) v += m.delta.ReadValue() * 0.1f;
            var g = Gamepad.current;
            if (g != null) v += g.rightStick.ReadValue() * 3f;
            return v;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
            return Vector2.zero;
#endif
        }

        public static float Zoom()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current; return m != null ? m.scroll.ReadValue().y / 120f : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        public static bool Run()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.leftShiftKey.isPressed) || (g != null && g.leftStickButton.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift);
#else
            return false;
#endif
        }

        public static bool JumpDown()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        public static bool ClickDown()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current; return m != null && m.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        public static bool EscapeDown()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current; return k != null && k.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
