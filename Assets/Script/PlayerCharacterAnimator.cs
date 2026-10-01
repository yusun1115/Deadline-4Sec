using UnityEngine;

namespace Deadline4Sec
{
    // Drives the Reaper model's Animator from PlayerController state. Purely visual:
    // it never moves the player or changes gameplay timing, so animation cannot
    // delay input (Spec §60). States are cross-faded by name instead of wired
    // transitions so every action reacts on the same frame as its input.
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerCharacterAnimator : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string RunState = "Run";
        public const string JumpState = "Jump";
        public const string FallState = "Fall";
        public const string FastFallState = "FastFall";
        public const string SlideState = "Slide";
        public const string AttackLeftState = "AttackLeft";
        public const string AttackRightState = "AttackRight";
        public const string HomingState = "Homing";
        public const string DieState = "Die";

        [SerializeField] private PlayerController player;
        [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.5f;
        [SerializeField, Min(0f)] private float fade = 0.08f;
        [SerializeField, Min(0.05f)] private float attackHold = 0.28f;
        [SerializeField, Min(0.05f)] private float jumpHold = 0.35f;
        [SerializeField] private float homingLeanAngle = 10f;
        [SerializeField] private float homingSpinSpeed = 0f;
        [SerializeField] private Transform weaponPivot;

        private Animator animator;
        private GameFlowManager flow;
        private string current;
        private float lockedUntil;
        private Vector3 lastPosition;
        private float planarSpeed;
        private Quaternion baseRotation;
        private Quaternion weaponBaseRotation;
        private float homingSpin;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            if (player == null)
                player = GetComponentInParent<PlayerController>();
            flow = FindFirstObjectByType<GameFlowManager>();
            baseRotation = transform.localRotation;
            if (weaponPivot != null)
                weaponBaseRotation = weaponPivot.localRotation;
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (player != null)
                player.ActionPerformed += OnAction;
            current = null;
            lockedUntil = 0f;
        }

        private void OnDisable()
        {
            if (player != null)
                player.ActionPerformed -= OnAction;
        }

        private void OnAction(PlayerController.PlayerAction action)
        {
            switch (action)
            {
                case PlayerController.PlayerAction.Left:
                    Play(AttackLeftState, attackHold, true);
                    break;
                case PlayerController.PlayerAction.Right:
                    Play(AttackRightState, attackHold, true);
                    break;
                case PlayerController.PlayerAction.Jump:
                    Play(JumpState, jumpHold, true);
                    break;
                case PlayerController.PlayerAction.Homing:
                    homingSpin = 0f;
                    Play(HomingState, 0f, true);
                    break;
                case PlayerController.PlayerAction.FastFall:
                case PlayerController.PlayerAction.SlamStart:
                    Play(FastFallState, 0f, true);
                    break;
                case PlayerController.PlayerAction.Slide:
                    Play(SlideState, 0f, true);
                    break;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 delta = transform.position - lastPosition;
                delta.y = 0f;
                planarSpeed = delta.magnitude / dt;
            }
            lastPosition = transform.position;
            if (player == null)
                return;

            if (player.DeathPoseProgress > 0f)
            {
                Play(DieState, 0f, false);
                ApplyHomingPose(false);
                return;
            }

            ApplyHomingPose(player.IsHoming);
            if (Time.time < lockedUntil)
                return;

            Play(ResolveState(), 0f, false);
        }

        private string ResolveState()
        {
            // The Ready? intro moves the body without ground contact; it is a sprint, not a fall.
            if (flow != null && flow.State == GameFlowManager.GameState.Ready)
                return RunState;
            // Menus show the Reaper standing in its idle pose (the controller is
            // disabled there, so ground contact is never refreshed).
            if (flow != null && (flow.State == GameFlowManager.GameState.Title ||
                                 flow.State == GameFlowManager.GameState.Settings ||
                                 flow.State == GameFlowManager.GameState.Upgrades ||
                                 flow.State == GameFlowManager.GameState.Skins))
                return IdleState;
            if (player.IsHoming)
                return HomingState;
            if (player.IsSliding && player.IsGrounded)
                return SlideState;
            if (!player.IsGrounded)
            {
                if (player.IsFastFalling)
                    return FastFallState;
                // Keep the attack pose through a lane move started in the air.
                if (current == AttackLeftState || current == AttackRightState)
                    return FallState;
                return player.VerticalSpeed > 2f && current == JumpState ? JumpState : FallState;
            }
            return planarSpeed > idleSpeedThreshold ? RunState : IdleState;
        }

        private void Play(string state, float hold, bool restart)
        {
            if (state == current && !restart)
                return;
            current = state;
            lockedUntil = hold > 0f ? Time.time + hold : 0f;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        // There is no dedicated homing clip, so lean the body into the dash and
        // spin the scythe. Rotations are local and reset as soon as homing ends.
        private void ApplyHomingPose(bool homing)
        {
            if (homing)
            {
                homingSpin += homingSpinSpeed * Time.deltaTime;
                transform.localRotation = baseRotation * Quaternion.Euler(homingLeanAngle, 0f, 0f);
                if (weaponPivot != null)
                    weaponPivot.localRotation = weaponBaseRotation * Quaternion.Euler(homingSpin, 0f, 0f);
                return;
            }
            transform.localRotation = baseRotation;
            if (weaponPivot != null)
                weaponPivot.localRotation = weaponBaseRotation;
        }
    }
}
