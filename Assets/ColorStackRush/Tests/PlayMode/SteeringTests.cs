using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
namespace ColorStackRush.Tests
{
    // Real Input System devices; no implementation-private fields are modified.
    public class SteeringTests : InputTestFixture
    {
        GameObject managers;
        Mouse mouse;
        string folder;
        public override void Setup()
        {
            base.Setup(); folder = Path.Combine(Path.GetTempPath(), "csr-input-" + Guid.NewGuid()); SaveManager.SetStorageDirectoryForTests(folder);
            managers = new GameObject("InputTest"); managers.AddComponent<GameManager>(); managers.AddComponent<SwipeInput>(); mouse = InputSystem.AddDevice<Mouse>();
            GameManager.Instance.StartRun(RunConfig.Level(1)); Tick();
        }
        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(managers); SaveManager.SetStorageDirectoryForTests(null);
            if (Directory.Exists(folder)) Directory.Delete(folder, true); base.TearDown();
        }
        void Tick() => managers.GetComponent<SwipeInput>().SendMessage("Update");
        void MouseAt(float x, bool down)
        {
            var state = new MouseState { position = new Vector2(x, 200) }; if (down) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update(); Tick();
        }
        [Test] public void PauseAndMenuNeverAccumulatePointerMovement()
        {
            MouseAt(100, true); MouseAt(200, true); Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.GreaterThan(0));
            GameManager.Instance.PauseGame(); MouseAt(300, true); MouseAt(600, true); Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.Zero);
            GameManager.Instance.ResumeGame(); MouseAt(700, true); Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.Zero);
            MouseAt(700, false); MouseAt(700, true); MouseAt(750, true); Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.GreaterThan(0));
            GameManager.Instance.GoToMenu(); MouseAt(900, true); Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.Zero);
        }
        [Test] public void SecondFingerCannotTakeOverAfterFirstLifts()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(100, 200), screen: screen); Tick();
            MoveTouch(1, new Vector2(150, 200), screen: screen); Tick();
            Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.GreaterThan(0));
            BeginTouch(2, new Vector2(500, 200), screen: screen); Tick();
            EndTouch(1, new Vector2(150, 200), screen: screen); Tick();
            MoveTouch(2, new Vector2(700, 200), screen: screen); Tick();
            Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.Zero);
            EndTouch(2, new Vector2(700, 200), screen: screen); Tick();
            BeginTouch(3, new Vector2(100, 200), screen: screen); Tick();
            MoveTouch(3, new Vector2(200, 200), screen: screen); Tick();
            Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.GreaterThan(0));
        }
        [UnityTest] public IEnumerator GestureStartingOnUiStaysBlockedWhenDraggedAway()
        {
            var events = new GameObject("Events", typeof(EventSystem));
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var button = new GameObject("Button", typeof(RectTransform), typeof(Image));
            var rect = button.GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(50, 150); rect.sizeDelta = new Vector2(100, 100);
            Canvas.ForceUpdateCanvases();
            try
            {
                yield return null; // GraphicRaycaster needs the first canvas render to establish graphic depth.
                Canvas.ForceUpdateCanvases();
                MouseAt(100, false);
                MouseAt(100, true); MouseAt(300, true);
                Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.Zero);
                MouseAt(300, false); MouseAt(300, true); MouseAt(400, true);
                Assert.That(SwipeInput.Instance.ConsumeHorizontalDelta(), Is.GreaterThan(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); UnityEngine.Object.DestroyImmediate(events); }
        }
    }
}
