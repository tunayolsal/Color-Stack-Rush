using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ColorStackRush.Tests
{
    // Exercise real pointer input, PlayerController.FixedUpdate and kinematic physics together.
    public class SteeringMotionTests : InputTestFixture
    {
        GameObject managers, player, cameraRig;
        Rigidbody body;
        PlayerController controller;
        CameraFollow follow;
        Camera camera;
        Mouse mouse;
        Vector2 pointerPosition;
        string folder;
        float previousTimeScale, previousFixedDeltaTime;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        InputSettings.BackgroundBehavior previousBackgroundInput;

        public override void Setup()
        {
            base.Setup();
            previousTimeScale = Time.timeScale;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundInput = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            folder = Path.Combine(Path.GetTempPath(), "csr-motion-" + Guid.NewGuid());
            SaveManager.SetStorageDirectoryForTests(folder);
            SaveManager.Data.reducedMotion = true;
            managers = new GameObject("SteeringMotionManagers");
            managers.AddComponent<GameManager>();
            managers.AddComponent<SwipeInput>();
            mouse = InputSystem.AddDevice<Mouse>();

            player = new GameObject("SteeringMotionPlayer");
            body = player.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            controller = player.AddComponent<PlayerController>();

            cameraRig = new GameObject("SteeringMotionCameraRig");
            follow = cameraRig.AddComponent<CameraFollow>();
            var cameraObject = new GameObject("SteeringMotionCamera");
            cameraObject.transform.SetParent(cameraRig.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 45;
            follow.SetTarget(player.transform);

            GameManager.Instance.StartRun(RunConfig.Level(1));
            pointerPosition = new Vector2(Screen.width * .25f, Screen.height * .45f);
            SendMouse(false); // A fresh gesture may start after the run's release guard.
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(cameraRig);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(managers);
            SaveManager.SetStorageDirectoryForTests(null);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            InputSystem.settings.backgroundBehavior = previousBackgroundInput;
            base.TearDown();
        }

        void SendMouse(bool down)
        {
            var state = new MouseState { position = pointerPosition };
            if (down) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            SwipeInput.Instance.SendMessage("Update");
        }

        IEnumerator Drag(float worldDelta)
        {
            pointerPosition.x += worldDelta * Mathf.Max(1, Screen.width) / 8.5f;
            SendMouse(true);
            yield return new WaitForFixedUpdate();
        }

        [UnityTest] public IEnumerator SmallReverseDragMovesInTheNewDirectionImmediately()
        {
            SendMouse(true);
            yield return Drag(2f);
            float rightX = body.position.x;
            Assert.That(rightX, Is.EqualTo(2f).Within(.0001f), "A drag must reach its requested position without a hidden target lag");

            yield return Drag(-.3f);
            Assert.That(body.position.x, Is.LessThan(rightX), "Left input must not continue the previous rightward motion");
            Assert.That(body.position.x, Is.EqualTo(1.7f).Within(.0001f));
        }

        [UnityTest] public IEnumerator ReleasingMouseHoldsLateralPositionWhileForwardMotionContinues()
        {
            SendMouse(true);
            yield return Drag(1.4f);
            SendMouse(false);
            float releasedX = body.position.x;
            float releasedZ = body.position.z;
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(body.position.x, Is.EqualTo(releasedX).Within(.0001f), "Releasing the mouse must not leave lateral movement queued");
            }
            Assert.That(body.position.z, Is.GreaterThan(releasedZ));
        }

        [UnityTest] public IEnumerator ReverseDragLeavesRoadBoundaryWithoutStoredOvershoot()
        {
            SendMouse(true);
            yield return Drag(4f);
            Assert.That(body.position.x, Is.EqualTo(controller.LaneHalfWidth).Within(.0001f));
            yield return Drag(-.25f);
            Assert.That(body.position.x, Is.EqualTo(controller.LaneHalfWidth - .25f).Within(.0001f), "Reversing at the boundary must move inward on the next physics step");
        }

        [UnityTest] public IEnumerator ResumingHeldDragKeepsPositionUntilAFreshGestureStarts()
        {
            SendMouse(true);
            yield return Drag(1.4f);
            float pausedX = body.position.x;
            GameManager.Instance.PauseGame();
            pointerPosition.x += .8f * Mathf.Max(1, Screen.width) / 8.5f;
            SendMouse(true);
            yield return null;
            Assert.That(body.position.x, Is.EqualTo(pausedX).Within(.0001f));

            GameManager.Instance.ResumeGame();
            SendMouse(true);
            yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.EqualTo(pausedX).Within(.0001f), "A held drag must not resume steering or apply movement made while paused");

            SendMouse(false);
            SendMouse(true);
            yield return Drag(-.25f);
            Assert.That(body.position.x, Is.EqualTo(pausedX - .25f).Within(.0001f), "A fresh gesture should steer normally after releasing the paused drag");
        }

        [UnityTest] public IEnumerator CameraDoesNotSlidePawnSidewaysAfterMouseRelease()
        {
            SendMouse(true);
            yield return Drag(1.3f);
            SendMouse(false);
            // Flush the Rigidbody's interpolated pose before measuring screen position.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            follow.SendMessage("LateUpdate");
            float viewportX = camera.WorldToViewportPoint(player.transform.position + Vector3.up * .5f).x;
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
                // Measure after camera alignment; this also keeps the check independent of coroutine/LateUpdate order.
                follow.SendMessage("LateUpdate");
                Assert.That(camera.WorldToViewportPoint(player.transform.position + Vector3.up * .5f).x,
                    Is.EqualTo(viewportX).Within(.0001f), "A released pawn must keep its screen position as the course moves forward");
            }
        }
    }
}
