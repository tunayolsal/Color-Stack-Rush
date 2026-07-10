using UnityEngine;
using UnityEngine.InputSystem;

namespace ColorStackRush
{
    /// <summary>
    /// Reads horizontal drag input using the new Input System.
    /// Touch drag (or mouse drag in the editor) is converted into world-space
    /// horizontal movement; A/D and arrow keys work as an editor fallback.
    /// The PlayerController consumes the accumulated delta once per frame.
    /// </summary>
    public class SwipeInput : MonoBehaviour
    {
        public static SwipeInput Instance { get; private set; }

        [Header("Sensitivity")]
        [Tooltip("World units the player moves when dragging across the full screen width.")]
        [SerializeField] float dragToWorldUnits = 8.5f;
        [Tooltip("World units per second when steering with the keyboard (editor testing).")]
        [SerializeField] float keyboardSpeed = 9f;

        float accumulatedDelta; // world units, consumed by the player each frame
        Vector2? lastPointerPos;

        void Awake() => Instance = this;

        void Update()
        {
            float pixels = 0f;

            // --- Touch (primary) ---
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                Vector2 pos = touch.primaryTouch.position.ReadValue();
                if (lastPointerPos.HasValue) pixels = pos.x - lastPointerPos.Value.x;
                lastPointerPos = pos;
            }
            // --- Mouse (editor / desktop) ---
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                if (lastPointerPos.HasValue) pixels = pos.x - lastPointerPos.Value.x;
                lastPointerPos = pos;
            }
            else
            {
                lastPointerPos = null;
            }

            accumulatedDelta += pixels / Screen.width * dragToWorldUnits;

            // --- Keyboard fallback ---
            var kb = Keyboard.current;
            if (kb != null)
            {
                float axis = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis += 1f;
                accumulatedDelta += axis * keyboardSpeed * Time.deltaTime;
            }
        }

        /// <summary>Returns and clears the horizontal input gathered since the last call (world units).</summary>
        public float ConsumeHorizontalDelta()
        {
            float d = accumulatedDelta;
            accumulatedDelta = 0f;
            return d;
        }
    }
}
