using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace ColorStackRush
{
    [DefaultExecutionOrder(-50)]
    public class SwipeInput : MonoBehaviour
    {
        public static SwipeInput Instance { get; private set; }
        [SerializeField] float dragToWorldUnits = 8.5f;
        [SerializeField] float keyboardSpeed = 9f;
        float accumulatedDelta;
        Vector2 lastPosition;
        int finger = -1;
        bool captured, blocked, waitForRelease;
        PointerEventData pointer;
        EventSystem pointerSystem;
        readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        void Awake() => Instance = this;
        void OnEnable() { GameEvents.RunStarted += ResetInput; GameEvents.StateChanged += StateChanged; }
        void OnDisable() { GameEvents.RunStarted -= ResetInput; GameEvents.StateChanged -= StateChanged; ResetInput(); if (Instance == this) Instance = null; }
        void StateChanged(GameState state) => ResetInput();
        public void ResetInput() { accumulatedDelta = 0; finger = -1; captured = false; blocked = false; waitForRelease = true; }
        void OnApplicationFocus(bool focused) { if (!focused) ResetInput(); }
        void OnApplicationPause(bool paused) { if (paused) ResetInput(); }
        bool OverUI(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            if (pointer == null || pointerSystem != EventSystem.current) { pointerSystem = EventSystem.current; pointer = new PointerEventData(pointerSystem); }
            pointer.position = pos;
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0;
        }
        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) { accumulatedDelta = 0; return; }
            var touch = Touchscreen.current;
            bool anyTouch = false;
            if (touch != null) foreach (var t in touch.touches) if (t.press.isPressed) { anyTouch = true; break; }
            bool mouseDown = !anyTouch && Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (waitForRelease)
            {
                if (!anyTouch && !mouseDown) waitForRelease = false;
                return;
            }
            Vector2 pos = lastPosition;
            bool pressed = false;
            if (anyTouch)
            {
                foreach (var t in touch.touches)
                {
                    if (!captured && t.press.wasPressedThisFrame) { finger = t.touchId.ReadValue(); pos = t.position.ReadValue(); pressed = true; break; }
                    if (captured && t.touchId.ReadValue() == finger && t.press.isPressed) { pos = t.position.ReadValue(); pressed = true; break; }
                }
            }
            else if (mouseDown && (!captured || finger == -2)) { finger = -2; pos = Mouse.current.position.ReadValue(); pressed = true; }
            if (pressed)
            {
                if (!captured) { captured = true; blocked = OverUI(pos); }
                else if (!blocked) accumulatedDelta += (pos.x - lastPosition.x) / Mathf.Max(1, Screen.width) * dragToWorldUnits;
                lastPosition = pos;
            }
            else if (captured)
            {
                captured = false; finger = -1; blocked = false;
                // All fingers must lift before another can acquire the gesture.
                if (anyTouch) waitForRelease = true;
            }
            var kb = Keyboard.current;
            if (kb != null) accumulatedDelta += ((kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0)) * keyboardSpeed * Time.deltaTime;
        }
        public float ConsumeHorizontalDelta() { float value = accumulatedDelta; accumulatedDelta = 0; return value; }
    }
}
