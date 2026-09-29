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
        [Header("Consumable double tap")]
        [SerializeField, Range(0.15f, 0.5f)] private float doubleTapWindow = 0.32f;
        [SerializeField, Min(5f)] private float tapMaxMovement = 28f;
        [SerializeField, Min(5f)] private float tapPairDistance = 90f;

        private Vector2 swipeStartPosition;
        private bool trackingTouch;
        private bool swipeConsumed;
        private bool ignoredByUI;
        private int trackedTouchId = -1;
        private bool trackingMouse;
        private Vector2 lastPointerPosition;
        private Vector2 lastTapPosition;
        private float lastTapTime = float.NegativeInfinity;
        private RunInventory inventory;

        private void Awake()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (gameFlow == null)
                gameFlow = FindFirstObjectByType<GameFlowManager>();
            inventory = FindFirstObjectByType<RunInventory>();
        }

        private void Update()
        {
            if (gameFlow == null || !gameFlow.CanReceiveInput)
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
                    lastPointerPosition = tracked.position.ReadValue();
                    EvaluateSwipe(lastPointerPosition);
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
                        lastPointerPosition = touch.position;
                        EvaluateSwipe(lastPointerPosition);
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
            lastPointerPosition = mouse.position.ReadValue();
            EvaluateSwipe(lastPointerPosition);
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
            lastPointerPosition = Input.mousePosition;
            EvaluateSwipe(lastPointerPosition);
#endif
        }

        private void BeginTouch(int touchId, Vector2 position)
        {
            trackingTouch = true;
            trackedTouchId = touchId;
            swipeStartPosition = position;
            lastPointerPosition = position;
            swipeConsumed = false;
            ignoredByUI = IsPointerOverUI(position);
            if (ignoredByUI)
                Log("Swipe ignored: UI");
        }

        private void EndTouch()
        {
            EndPointer();
            trackingTouch = false;
            trackedTouchId = -1;
            swipeConsumed = false;
            ignoredByUI = false;
        }

        private void BeginMouse(Vector2 position)
        {
            trackingMouse = true;
            swipeStartPosition = position;
            lastPointerPosition = position;
            swipeConsumed = false;
            ignoredByUI = IsPointerOverUI(position);
            if (ignoredByUI)
                Log("Swipe ignored: UI");
        }

        private void EndMouse()
        {
            EndPointer();
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
            lastTapTime = float.NegativeInfinity;
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

        private void EndPointer()
        {
            if (swipeConsumed || ignoredByUI)
            {
                lastTapTime = float.NegativeInfinity;
                return;
            }
            float movement = Vector2.Distance(swipeStartPosition, lastPointerPosition);
            if (movement > tapMaxMovement)
            {
                lastTapTime = float.NegativeInfinity;
                Log("Input ignored: short drag");
                return;
            }
            float now = Time.unscaledTime;
            if (now - lastTapTime <= doubleTapWindow &&
                Vector2.Distance(swipeStartPosition, lastTapPosition) <= tapPairDistance)
            {
                lastTapTime = float.NegativeInfinity;
                if (inventory != null && inventory.TryUseEquipped())
                    Log("Double tap: consumable used");
            }
            else
            {
                lastTapTime = now;
                lastTapPosition = swipeStartPosition;
            }
        }

        private void ResetTracking()
        {
            trackingTouch = false;
            trackingMouse = false;
            trackedTouchId = -1;
            swipeConsumed = false;
            ignoredByUI = false;
            lastTapTime = float.NegativeInfinity;
        }

        private void Log(string message)
        {
            if (showInputDebugLogs)
                Debug.Log(message);
        }

        private void OnValidate()
        {
            minSwipeDistance = Mathf.Max(10f, minSwipeDistance);
            tapMaxMovement = Mathf.Min(tapMaxMovement, minSwipeDistance * 0.5f);
        }
    }
}
