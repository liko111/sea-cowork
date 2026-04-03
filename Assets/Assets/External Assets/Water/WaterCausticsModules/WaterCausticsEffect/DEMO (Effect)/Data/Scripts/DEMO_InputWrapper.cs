// WaterCausticsModules
// Copyright (c) 2021 Masataka Hakozaki

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TouchPhase = UnityEngine.TouchPhase;
#endif

namespace MH.WaterCausticsModules {
    public static class DEMO_InputWrapper {
        public struct TouchInfo {
            public Vector2 position;
            public TouchPhase phase;
            public int fingerId;
            public Vector2 deltaPosition;
            public float deltaTime;
            public TouchInfo (Vector2 pos, TouchPhase touchPhase, int id = 0, Vector2 delta = default, float time = 0f) {
                position = pos;
                phase = touchPhase;
                fingerId = id;
                deltaPosition = delta;
                deltaTime = time;
            }
        }

        public static Vector2 mousePosition {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue () : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static int touchCount {
            get {
#if ENABLE_INPUT_SYSTEM
                if (Touchscreen.current == null) return 0;
                int count = 0;
                for (int i = 0; i < Touchscreen.current.touches.Count; i++) {
                    var phase = Touchscreen.current.touches [i].phase.ReadValue ();
                    if (phase != UnityEngine.InputSystem.TouchPhase.Ended &&
                        phase != UnityEngine.InputSystem.TouchPhase.Canceled) {
                        count++;
                    }
                }
                return count;
#else
                return Input.touchCount;
#endif
            }
        }

        public static bool GetMouseButton (int idx) {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            switch (idx) {
                case 0: return Mouse.current.leftButton.isPressed;
                case 1: return Mouse.current.rightButton.isPressed;
                case 2: return Mouse.current.middleButton.isPressed;
                default: return false;
            }
#else
            return Input.GetMouseButton (idx);
#endif
        }

        public static bool GetMouseButtonDown (int idx) {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            switch (idx) {
                case 0: return Mouse.current.leftButton.wasPressedThisFrame;
                case 1: return Mouse.current.rightButton.wasPressedThisFrame;
                case 2: return Mouse.current.middleButton.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetMouseButtonDown (idx);
#endif
        }

        public static bool GetMouseButtonUp (int idx) {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            switch (idx) {
                case 0: return Mouse.current.leftButton.wasReleasedThisFrame;
                case 1: return Mouse.current.rightButton.wasReleasedThisFrame;
                case 2: return Mouse.current.middleButton.wasReleasedThisFrame;
                default: return false;
            }
#else
            return Input.GetMouseButtonUp (idx);
#endif
        }

        public static bool GetKey (KeyCode keyCode) {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return false;
            var key = ConvertKeyCodeToKey (keyCode);
            return key != null && key.isPressed;
#else
            return Input.GetKey (keyCode);
#endif
        }

        public static float GetHorizontalAxis () {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return 0f;
            float value = 0f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) value += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) value -= 1f;
            return value;
#else
            return Input.GetAxis ("Horizontal");
#endif
        }

        public static float GetVerticalAxis () {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return 0f;
            float value = 0f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) value += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) value -= 1f;
            return value;
#else
            return Input.GetAxis ("Vertical");
#endif
        }

        public static float GetMouseScrollWheel () {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return 0f;
            return Mouse.current.scroll.ReadValue ().y / 30f;
#else
            return Input.GetAxis ("Mouse ScrollWheel");
#endif
        }

        public static TouchInfo GetTouch (int index) {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current == null || index >= Touchscreen.current.touches.Count)
                return new TouchInfo (Vector2.zero, TouchPhase.Ended);
            var touch = Touchscreen.current.touches [index];
            var touchControl = touch.ReadValue ();
            TouchPhase convertedPhase = ConvertTouchPhase (touchControl.phase);
            return new TouchInfo (
                touchControl.position,
                convertedPhase,
                touchControl.touchId,
                touchControl.delta,
                (float) touchControl.startTime
            );
#else
            if (index >= Input.touchCount)
                return new TouchInfo (Vector2.zero, TouchPhase.Ended);
            Touch touch = Input.GetTouch (index);
            return new TouchInfo (
                touch.position,
                touch.phase,
                touch.fingerId,
                touch.deltaPosition,
                touch.deltaTime
            );
#endif
        }

        public static bool IsTouchMoving (int index) {
            var touchInfo = GetTouch (index);
            return touchInfo.phase == TouchPhase.Moved;
        }

        public static bool IsTouchStationary (int index) {
            var touchInfo = GetTouch (index);
            return touchInfo.phase == TouchPhase.Stationary;
        }

        public static bool IsTouchBegan (int index) {
            var touchInfo = GetTouch (index);
            return touchInfo.phase == TouchPhase.Began;
        }

        public static bool IsTouchEnded (int index) {
            var touchInfo = GetTouch (index);
            return touchInfo.phase == TouchPhase.Ended || touchInfo.phase == TouchPhase.Canceled;
        }

#if ENABLE_INPUT_SYSTEM
        private static TouchPhase ConvertTouchPhase (UnityEngine.InputSystem.TouchPhase inputSystemPhase) {
            switch (inputSystemPhase) {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    return TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    return TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    return TouchPhase.Ended;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    return TouchPhase.Canceled;
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    return TouchPhase.Stationary;
                default:
                    return TouchPhase.Ended;
            }
        }

        private static KeyControl ConvertKeyCodeToKey (KeyCode keyCode) {
            if (Keyboard.current == null) return null;
            switch (keyCode) {
                case KeyCode.LeftAlt: return Keyboard.current.leftAltKey;
                case KeyCode.LeftCommand: return Keyboard.current.leftCommandKey;
                case KeyCode.LeftControl: return Keyboard.current.leftCtrlKey;
                case KeyCode.LeftShift: return Keyboard.current.leftShiftKey;
                case KeyCode.X: return Keyboard.current.xKey;
                case KeyCode.Z: return Keyboard.current.zKey;
                case KeyCode.A: return Keyboard.current.aKey;
                case KeyCode.D: return Keyboard.current.dKey;
                case KeyCode.S: return Keyboard.current.sKey;
                case KeyCode.W: return Keyboard.current.wKey;
                case KeyCode.LeftArrow: return Keyboard.current.leftArrowKey;
                case KeyCode.RightArrow: return Keyboard.current.rightArrowKey;
                case KeyCode.UpArrow: return Keyboard.current.upArrowKey;
                case KeyCode.DownArrow: return Keyboard.current.downArrowKey;
                default: return null;
            }
        }
#endif
    }
}
