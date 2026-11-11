using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ConveyorSystem
{
    /// <summary>
    /// Small wrapper around mouse and keyboard input so the project works with either
    /// the old Input Manager or the new Input System package (Unity 6 projects often
    /// use the new one by default). All other scripts read input through this class.
    /// </summary>
    public static class InputHelper
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 MousePosition => Input.mousePosition;
        public static bool LeftDown => Input.GetMouseButtonDown(0);
        public static bool RightDown => Input.GetMouseButtonDown(1);
        public static bool LeftHeld => Input.GetMouseButton(0);
        public static bool MiddleHeld => Input.GetMouseButton(2);
        public static float Scroll => Input.mouseScrollDelta.y;
        public static bool KeyDown(KeyCode key) => Input.GetKeyDown(key);
        public static bool KeyHeld(KeyCode key) => Input.GetKey(key);
#elif ENABLE_INPUT_SYSTEM
        static Mouse M => Mouse.current;
        static Keyboard K => Keyboard.current;

        public static Vector2 MousePosition => M != null ? M.position.ReadValue() : Vector2.zero;
        public static bool LeftDown => M != null && M.leftButton.wasPressedThisFrame;
        public static bool RightDown => M != null && M.rightButton.wasPressedThisFrame;
        public static bool LeftHeld => M != null && M.leftButton.isPressed;
        public static bool MiddleHeld => M != null && M.middleButton.isPressed;

        public static float Scroll
        {
            get
            {
                if (M == null) return 0f;
                float y = M.scroll.ReadValue().y;
                if (Mathf.Abs(y) > 10f) y /= 120f; // Windows reports 120 per wheel notch
                return Mathf.Clamp(y, -3f, 3f);
            }
        }

        public static bool KeyDown(KeyCode code)
        {
            if (K == null) return false;
            Key key = ToKey(code);
            return key != Key.None && K[key].wasPressedThisFrame;
        }

        public static bool KeyHeld(KeyCode code)
        {
            if (K == null) return false;
            Key key = ToKey(code);
            return key != Key.None && K[key].isPressed;
        }

        static Key ToKey(KeyCode c)
        {
            if (c >= KeyCode.A && c <= KeyCode.Z) return (Key)((int)Key.A + (c - KeyCode.A));
            if (c >= KeyCode.Alpha1 && c <= KeyCode.Alpha9) return (Key)((int)Key.Digit1 + (c - KeyCode.Alpha1));
            switch (c)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                default: return Key.None;
            }
        }
#else
        public static Vector2 MousePosition => Vector2.zero;
        public static bool LeftDown => false;
        public static bool RightDown => false;
        public static bool LeftHeld => false;
        public static bool MiddleHeld => false;
        public static float Scroll => 0f;
        public static bool KeyDown(KeyCode key) => false;
        public static bool KeyHeld(KeyCode key) => false;
#endif

        public static bool Shift => KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift);
        public static bool Alt => KeyHeld(KeyCode.LeftAlt) || KeyHeld(KeyCode.RightAlt);
    }
}
