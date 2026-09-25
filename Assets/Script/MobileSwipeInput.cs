using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Deadline4Sec
{
    public sealed class MobileSwipeInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private GameFlowManager gameFlow;

        [Header("Swipe")]
        [SerializeField, Min(10f)] private float minSwipeDistance = 80f;
        [SerializeField] private bool enableMouseSwipeTesting = true;
        [SerializeField] private bool showInputDebugLogs;

        private Vector2 swipeStartPosition;
        private bool trackingTouch;
        private bool swipeConsumed;
        private bool ignoredByUI;
        private int trackedTouchId = -1;
        private bool trackingMouse;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (gameFlow == null)
                gameFlow = FindFirstObjectByType<GameFlowManager>();
        }

        private void Update()
        {
            if (gameFlow == null || !gameFlow.IsPlaying)
            {
                ResetTracking();
                return;
            }

            ReadTouch();
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!trackingTouch && enableMouseSwipeTesting)
                ReadMouse();
#endif
        }

        private void ReadTouch()
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return;

            if (trackingTouch)
            {
                TouchControl tracked = null;
                for (int i = 0; i < touchscreen.touches.Count; i++)
                {
                    TouchControl touch = touchscreen.touches[i];
                    if (touch.touchId.ReadValue() == trackedTouchId)
                    {
                        tracked = touch;
                        break;
                    }
                }
                if (tracked != null && tracked.press.isPressed)
                {
                    EvaluateSwipe(tracked.position.ReadValue());
                    return;
                }
                EndTouch();
            }

            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                TouchControl touch = touchscreen.touches[i];
                if (!touch.press.wasPressedThisFrame)
                    continue;
                BeginTouch(touch.touchId.ReadValue(), touch.position.ReadValue());
                return;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (trackingTouch)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != trackedTouchId)
                        continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        EndTouch();
                        break;
                    }
                    else
                    {
                        EvaluateSwipe(touch.position);
                        return;
                    }
                }
                if (trackingTouch)
                    EndTouch();
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;
                BeginTouch(touch.fingerId, touch.position);
                return;
            }
#endif
        }

        private void ReadMouse()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            if (mouse.leftButton.wasPressedThisFrame)
                BeginMouse(mouse.position.ReadValue());
            if (!trackingMouse)
                return;
            if (!mouse.leftButton.isPressed)
            {
                EndMouse();
                return;
            }
            EvaluateSwipe(mouse.position.ReadValue());
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
                BeginMouse(Input.mousePosition);
            if (!trackingMouse)
                return;
            if (!Input.GetMouseButton(0))
            {
                EndMouse();
                return;
            }
            EvaluateSwipe(Input.mousePosition);
#endif
        }

        private void BeginTouch(int touchId, Vector2 position)
        {
            trackingTouch = true;
            trackedTouchId = touchId;
            swipeStartPosition = position;
            swipeConsumed = false;
            ignoredByUI = IsPointerOverUI(position);
            if (ignoredByUI)
                Log("Swipe ignored: UI");
        }

        private void EndTouch()
        {
            if (!swipeConsumed && !ignoredByUI &&
                Vector2.Distance(swipeStartPosition, GetLastPointerPosition()) < minSwipeDistance)
                Log("Swipe ignored: too short");
            trackingTouch = false;
            trackedTouchId = -1;
            swipeConsumed = false;
            ignoredByUI = false;
        }

        private void BeginMouse(Vector2 position)
        {
            trackingMouse = true;
            swipeStartPosition = position;
            swipeConsumed = false;
            ignoredByUI = IsPointerOverUI(position);
            if (ignoredByUI)
                Log("Swipe ignored: UI");
        }

        private void EndMouse()
        {
            if (!swipeConsumed && !ignoredByUI &&
                Vector2.Distance(swipeStartPosition, GetLastPointerPosition()) < minSwipeDistance)
                Log("Swipe ignored: too short");
            trackingMouse = false;
            swipeConsumed = false;
            ignoredByUI = false;
        }

        private void EvaluateSwipe(Vector2 currentPosition)
        {
            if (swipeConsumed || ignoredByUI || playerController == null)
                return;

            Vector2 delta = currentPosition - swipeStartPosition;
            if (delta.magnitude < minSwipeDistance)
                return;

            swipeConsumed = true;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                if (delta.x < 0f)
                {
                    Log("Swipe: LEFT");
                    playerController.RequestMoveLeft();
                }
                else
                {
                    Log("Swipe: RIGHT");
                    playerController.RequestMoveRight();
                }
            }
            else if (delta.y > 0f)
            {
                Log("Swipe: UP");
                playerController.RequestJump();
            }
            else
            {
                Log("Swipe: DOWN");
                playerController.RequestSlide();
            }
        }

        private static bool IsPointerOverUI(Vector2 position)
        {
            if (EventSystem.current == null)
                return false;

            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                position = position
            };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            foreach (RaycastResult result in results)
            {
                Selectable control = result.gameObject.GetComponentInParent<Selectable>();
                if (control != null && control.IsActive() && control.IsInteractable())
                    return true;
            }
            return false;
        }

        private Vector2 GetLastPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (trackingMouse && Mouse.current != null)
                return Mouse.current.position.ReadValue();
            if (trackingTouch && Touchscreen.current != null)
            {
                foreach (TouchControl touch in Touchscreen.current.touches)
                    if (touch.touchId.ReadValue() == trackedTouchId)
                        return touch.position.ReadValue();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (trackingMouse)
                return Input.mousePosition;
            for (int i = 0; trackingTouch && i < Input.touchCount; i++)
                if (Input.GetTouch(i).fingerId == trackedTouchId)
                    return Input.GetTouch(i).position;
#endif
            return swipeStartPosition;
        }

        private void ResetTracking()
        {
            trackingTouch = false;
            trackingMouse = false;
            trackedTouchId = -1;
            swipeConsumed = false;
            ignoredByUI = false;
        }

        private void Log(string message)
        {
            if (showInputDebugLogs)
                Debug.Log(message);
        }

        private void OnValidate()
        {
            minSwipeDistance = Mathf.Max(10f, minSwipeDistance);
        }
    }
}
