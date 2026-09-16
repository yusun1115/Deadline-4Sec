using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deadline4Sec
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Forward movement")]
        [SerializeField, Min(0f)] private float forwardSpeed = 8f;

        [Header("Lanes")]
        [SerializeField, Min(0.1f)] private float laneWidth = 2.5f;
        [SerializeField, Min(0.1f)] private float laneChangeSpeed = 12f;

        [Header("Jump")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.6f;
        [SerializeField] private float gravity = -25f;

        [Header("Jump attack")]
        [SerializeField, Min(0.01f)] private float jumpAttackDuration = 0.45f;
        [SerializeField] private Vector3 jumpAttackBoxSize = new Vector3(1.2f, 1.4f, 2.2f);
        [SerializeField, Min(0f)] private float jumpAttackForwardOffset = 1.2f;
        [SerializeField] private float jumpAttackHeightOffset = 0.2f;

        [Header("Air homing dash")]
        [SerializeField, Min(0f)] private float airJumpMinInterval = 0.08f;
        [SerializeField, Min(0.1f)] private float airTargetSearchRange = 35f;
        [SerializeField, Range(0.05f, 0.4f)] private float airHomingDuration = 0.12f;
        [SerializeField] private Vector3 airHomingTargetOffset = new Vector3(0f, 0f, -0.5f);
        [SerializeField, Min(0.1f)] private float airHomingHitRadius = 1.2f;
        [SerializeField, Min(0f)] private float airTargetSideBias = 3f;
        [SerializeField, Min(0f)] private float airTargetHeightWeight = 0.5f;
        [SerializeField, Min(0.1f)] private float airComboBouncePower = 5f;
        [SerializeField, Min(0f)] private float airEnemyKillBoost = 2.5f;

        [Header("Lane attack")]
        [SerializeField] private GameTimer gameTimer;
        [SerializeField, Min(0.01f)] private float attackDuration = 0.3f;
        [SerializeField] private Vector3 attackBoxSize = new Vector3(1.5f, 1.8f, 2.4f);
        [SerializeField, Min(0f)] private float attackSideOffset = 1.5f;
        [SerializeField, Min(0f)] private float attackForwardOffset = 1.2f;
        [SerializeField] private float attackHeightOffset = 1f;

        [Header("Slide")]
        [SerializeField, Min(0.01f)] private float slideDuration = 0.6f;
        [SerializeField, Range(0.1f, 1f)] private float slideHeightRatio = 0.5f;
        [SerializeField] private Vector3 slideAttackBoxSize = new Vector3(1.2f, 1f, 2.2f);
        [SerializeField, Min(0f)] private float slideAttackForwardOffset = 1.2f;
        [SerializeField, Min(0.1f)] private float fastFallSpeed = 18f;
        [SerializeField, Min(0.1f)] private float stompBouncePower = 12f;
        [SerializeField] private Transform visualTransform;

        private CharacterController characterController;
        private float centerX;
        private float verticalSpeed;
        private bool isGrounded;
        private bool jumpInputHeld;
        private int lastJumpActionFrame = -1;
        private int laneIndex; // -1: left, 0: center, 1: right
        private float attackTimeRemaining;
        private int attackDirection;
        private float jumpAttackTimeRemaining;
        private Enemy airDashTarget;
        private bool isHomingDash;
        private float airHomingElapsed;
        private float lastAirJumpTime = float.NegativeInfinity;
        private int preferredAirDashDirection;
        private float lastLaneInputTime = float.NegativeInfinity;
        private float slideTimeRemaining;
        private bool isSliding;
        private bool isFastFalling;
        private bool pendingGroundSlide;
        private float standingHeight;
        private Vector3 standingCenter;
        private BoxCollider boxCollider;
        private Vector3 standingBoxSize;
        private Vector3 standingBoxCenter;
        private CapsuleCollider capsuleCollider;
        private float standingCapsuleHeight;
        private Vector3 standingCapsuleCenter;
        private Quaternion standingVisualRotation;
        private Vector3 standingVisualPosition;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            standingHeight = characterController.height;
            standingCenter = characterController.center;
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                standingBoxSize = boxCollider.size;
                standingBoxCenter = boxCollider.center;
            }
            capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                standingCapsuleHeight = capsuleCollider.height;
                standingCapsuleCenter = capsuleCollider.center;
            }
            SetupVisual();
            centerX = transform.position.x;
            if (gameTimer == null)
                gameTimer = FindFirstObjectByType<GameTimer>();
        }

        private void Start()
        {
            // The test scene spawns the player slightly above the plane. Establish
            // contact before the first Jump input can be classified as an air jump.
            Bounds bounds = characterController.bounds;
            RaycastHit[] hits = Physics.RaycastAll(bounds.center, Vector3.down,
                bounds.extents.y + 0.6f, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<Enemy>() != null)
                    continue;

                float gap = bounds.min.y - hit.point.y;
                if (gap < 0f || gap > 0.5f)
                    continue;

                CollisionFlags flags = characterController.Move(Vector3.down * (gap + 0.05f));
                isGrounded = (flags & CollisionFlags.Below) != 0;
                break;
            }
        }

        private void Update()
        {
            ReadKeyboardInput();

            // Homing owns the only movement call for this frame. Normal forward,
            // lane movement, gravity and jump velocity resume after it ends.
            if (isHomingDash)
            {
                UpdateHomingDash();
                return;
            }

            if (isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            verticalSpeed += gravity * Time.deltaTime;
            if (isFastFalling)
                verticalSpeed = -fastFallSpeed;

            float targetX = centerX + laneIndex * laneWidth;
            bool isChangingLane = !Mathf.Approximately(transform.position.x, targetX);
            float nextX = Mathf.MoveTowards(transform.position.x, targetX, laneChangeSpeed * Time.deltaTime);
            Bounds contactBounds = characterController.bounds;
            Vector3 movement = new Vector3(nextX - transform.position.x,
                verticalSpeed * Time.deltaTime, forwardSpeed * Time.deltaTime);

            CollisionFlags collisions = characterController.Move(movement);
            isGrounded = (collisions & CollisionFlags.Below) != 0;
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;

            // A head-first fast fall takes priority over all other attacks and contact.
            if (isFastFalling && movement.y < 0f &&
                TryStompEnemy(contactBounds, characterController.bounds))
                return;

            if (isSliding && !isGrounded)
                EndSlide();

            bool landedIntoSlide = pendingGroundSlide && isGrounded;
            if (landedIntoSlide)
            {
                pendingGroundSlide = false;
                isFastFalling = false;
                StartGroundSlide();
            }
            else if (isGrounded)
            {
                isFastFalling = false;
            }

            bool laneAttackActive = attackTimeRemaining > 0f && isChangingLane;
            bool jumpAttackActive = jumpAttackTimeRemaining > 0f;
            bool slideAttackActive = isSliding && isGrounded;

            if (laneAttackActive)
                CheckLaneAttack();
            if (jumpAttackActive)
                CheckJumpAttack("Jump Attack");
            if (slideAttackActive)
                CheckSlideAttack();

            // Attack boxes resolve first. Contact then handles enemies that are still alive.
            // The ground slide begins only at landing; its contact sweep must not
            // retroactively attack enemies along the airborne descent.
            CheckEnemyContact(landedIntoSlide ? characterController.bounds : contactBounds,
                landedIntoSlide ? Vector3.zero : movement,
                laneAttackActive, jumpAttackActive, slideAttackActive);

            if (attackTimeRemaining > 0f)
            {
                attackTimeRemaining = Mathf.Max(0f, attackTimeRemaining - Time.deltaTime);
            }

            if (jumpAttackTimeRemaining > 0f)
                jumpAttackTimeRemaining = Mathf.Max(0f, jumpAttackTimeRemaining - Time.deltaTime);

            if (isSliding)
            {
                slideTimeRemaining -= Time.deltaTime;
                if (slideTimeRemaining <= 0f)
                    EndSlide();
            }
        }

        private void OnDisable()
        {
            attackTimeRemaining = 0f;
            attackDirection = 0;
            jumpAttackTimeRemaining = 0f;
            StopAirDash();
            lastAirJumpTime = float.NegativeInfinity;
            isFastFalling = false;
            pendingGroundSlide = false;
            EndSlide();
        }

        private void ReadKeyboardInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                ChangeLane(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                ChangeLane(1);
            bool jumpPressed = keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed || keyboard.spaceKey.isPressed;
            if (jumpPressed && !jumpInputHeld)
                Jump();
            jumpInputHeld = jumpPressed;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                Slide();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                ChangeLane(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                ChangeLane(1);
            bool jumpPressed = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space);
            if (jumpPressed && !jumpInputHeld)
                Jump();
            jumpInputHeld = jumpPressed;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                Slide();
#endif
        }

        public void ChangeLane(int direction)
        {
            if (!isActiveAndEnabled || (gameTimer != null && gameTimer.IsGameOver))
                return;

            if (direction == 0)
                return;

            preferredAirDashDirection = direction > 0 ? 1 : -1;
            lastLaneInputTime = Time.time;

            int nextLane = Mathf.Clamp(laneIndex + direction, -1, 1);
            if (nextLane == laneIndex)
                return;

            laneIndex = nextLane;
            attackDirection = direction > 0 ? 1 : -1;
            attackTimeRemaining = attackDuration;
        }

        private void CheckLaneAttack()
        {
            if (gameTimer == null || gameTimer.IsGameOver)
                return;

            KillEnemiesInBox(GetAttackBoxCenter(attackDirection), attackBoxSize, "Lane Attack");
        }

        private void CheckSlideAttack()
        {
            if (gameTimer == null || gameTimer.IsGameOver)
                return;

            KillEnemiesInBox(GetSlideAttackBoxCenter(), slideAttackBoxSize, "Slide Attack");
        }

        private void CheckJumpAttack(string attackName)
        {
            if (gameTimer == null || gameTimer.IsGameOver)
                return;

            KillEnemiesInBox(GetJumpAttackBoxCenter(), jumpAttackBoxSize, attackName);
        }

        private void KillEnemiesInBox(Vector3 center, Vector3 size, string attackName)
        {
            Collider[] hits = Physics.OverlapBox(
                center, size * 0.5f, Quaternion.identity, ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                Enemy enemy = hit.GetComponentInParent<Enemy>();
                // The selected target is reserved for the dash hit and combo bounce.
                if (isHomingDash && enemy == airDashTarget)
                    continue;
                TryKillEnemy(enemy, attackName);
            }
        }

        private bool TryKillEnemy(Enemy enemy, string attackName)
        {
            if (enemy == null || !enemy.TryKill(attackName))
                return false;

            gameTimer.ResetTimer();
            Debug.Log("Timer Reset by Kill");
            if (attackName == "Homing Dash Attack")
            {
                StopAirDash();
                verticalSpeed = airComboBouncePower;
                isGrounded = false;
                lastAirJumpTime = float.NegativeInfinity;
            }
            else if (enemy.Type == Enemy.EnemyType.Air && attackName == "Jump Attack")
            {
                isFastFalling = false;
                pendingGroundSlide = false;
                isGrounded = false;
                verticalSpeed = Mathf.Max(0f, verticalSpeed) + airEnemyKillBoost;
            }
            return true;
        }

        private bool TryStompEnemy(Bounds beforeMove, Bounds afterMove)
        {
            if (gameTimer == null || gameTimer.IsGameOver)
                return false;

            Bounds sweptBounds = beforeMove;
            sweptBounds.Encapsulate(afterMove.min);
            sweptBounds.Encapsulate(afterMove.max);
            Collider[] hits = Physics.OverlapBox(sweptBounds.center,
                sweptBounds.extents + new Vector3(2f, 1f, 2f),
                Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                Enemy enemy = hit.GetComponentInParent<Enemy>();
                if (enemy == null || !enemy.IsValidStomp(beforeMove, afterMove) ||
                    !TryKillEnemy(enemy, "Stomp Attack"))
                    continue;

                isFastFalling = false;
                pendingGroundSlide = false;
                isGrounded = false;
                verticalSpeed = stompBouncePower;
                jumpAttackTimeRemaining = 0f;
                StopAirDash();
                lastAirJumpTime = float.NegativeInfinity;
                return true;
            }
            return false;
        }

        private Enemy FindAirTarget()
        {
            Vector3 origin = characterController.bounds.center;
            Enemy[] candidates = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            Enemy best = null;
            float bestScore = float.PositiveInfinity;
            int side = Time.time - lastLaneInputTime <= 0.25f ? preferredAirDashDirection : 0;
            int airCount = 0;
            int eligibleCount = 0;

            foreach (Enemy enemy in candidates)
            {
                if (enemy == null || enemy.IsKilled || enemy.Type != Enemy.EnemyType.Air)
                    continue;
                airCount++;

                Vector3 delta = GetAirTargetPoint(enemy) - origin;
                if (delta.z < -0.5f || delta.sqrMagnitude > airTargetSearchRange * airTargetSearchRange)
                    continue;
                eligibleCount++;

                float score = delta.magnitude + Mathf.Abs(delta.y) * airTargetHeightWeight;
                if (delta.z < 0f)
                    score += 2f;
                if (side != 0)
                    score += Mathf.Sign(delta.x) == side ? -airTargetSideBias : airTargetSideBias;

                if (score >= bestScore)
                    continue;

                best = enemy;
                bestScore = score;
            }
            Debug.Log($"Air Target Scan: {airCount} Air Enemy, {eligibleCount} in front/range");
            return best;
        }

        private Vector3 GetAirTargetPoint(Enemy enemy)
        {
            Collider body = enemy.GetComponent<Collider>();
            return (body != null ? body.bounds.center : enemy.transform.position) + airHomingTargetOffset;
        }

        private void UpdateHomingDash()
        {
            if (airDashTarget == null || airDashTarget.IsKilled)
            {
                StopAirDash();
                return;
            }

            Vector3 before = characterController.bounds.center;
            Vector3 targetPoint = GetAirTargetPoint(airDashTarget);
            float remaining = Mathf.Max(0.001f, airHomingDuration - airHomingElapsed);
            Vector3 requestedMove = (targetPoint - before) * Mathf.Clamp01(Time.deltaTime / remaining);

            // This is the only CharacterController.Move call while homing.
            characterController.Move(requestedMove);
            Vector3 after = characterController.bounds.center;
            isGrounded = false;
            verticalSpeed = 0f;

            if (TryHitDashTarget(before, after))
            {
                Debug.Log("Homing Dash Reached Target");
                TryKillEnemy(airDashTarget, "Homing Dash Attack");
                return;
            }

            airHomingElapsed += Time.deltaTime;
            if (airHomingElapsed >= airHomingDuration)
            {
                Debug.LogWarning("Homing Dash Timed Out: " + airDashTarget.name);
                StopAirDash();
            }
        }

        private bool TryHitDashTarget(Vector3 before, Vector3 after)
        {
            if (airDashTarget == null || airDashTarget.IsKilled)
                return false;

            Collider body = airDashTarget.GetComponent<Collider>();
            if (Vector3.Distance(after, GetAirTargetPoint(airDashTarget)) <= airHomingHitRadius ||
                (body != null && Vector3.Distance(body.ClosestPoint(after), after) <= airHomingHitRadius))
                return true;

            Vector3 travelled = after - before;
            if (travelled.sqrMagnitude > 0f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(before, airHomingHitRadius,
                    travelled.normalized, travelled.magnitude,
                    ~0, QueryTriggerInteraction.Collide);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.GetComponentInParent<Enemy>() == airDashTarget)
                        return true;
                }
            }
            return false;
        }

        private void StopAirDash()
        {
            if (isHomingDash)
                Debug.Log("Homing Dash Ended");
            isHomingDash = false;
            airDashTarget = null;
            airHomingElapsed = 0f;
        }

        private Vector3 GetAttackBoxCenter(int direction)
        {
            return transform.position + new Vector3(
                direction * attackSideOffset,
                attackHeightOffset,
                attackForwardOffset);
        }

        private Vector3 GetSlideAttackBoxCenter()
        {
            return characterController.bounds.center + Vector3.forward * slideAttackForwardOffset;
        }

        private Vector3 GetJumpAttackBoxCenter()
        {
            return characterController.bounds.center + new Vector3(
                0f, jumpAttackHeightOffset, jumpAttackForwardOffset);
        }

        private void CheckEnemyContact(Bounds beforeMove, Vector3 movement,
            bool laneAttackActive, bool jumpAttackActive, bool slideAttackActive)
        {
            if (gameTimer == null || gameTimer.IsGameOver)
                return;

            Vector3 halfExtents = beforeMove.extents * 0.9f;
            if (movement.sqrMagnitude > 0f)
            {
                RaycastHit[] sweptHits = Physics.BoxCastAll(
                    beforeMove.center, halfExtents, movement.normalized,
                    Quaternion.identity, movement.magnitude, ~0,
                    QueryTriggerInteraction.Collide);

                foreach (RaycastHit hit in sweptHits)
                {
                    if (ResolveEnemyContact(hit.collider,
                        laneAttackActive, jumpAttackActive, slideAttackActive))
                        return;
                }
            }

            Bounds afterMove = characterController.bounds;
            Collider[] overlaps = Physics.OverlapBox(
                afterMove.center, afterMove.extents * 0.9f,
                Quaternion.identity, ~0, QueryTriggerInteraction.Collide);

            foreach (Collider hit in overlaps)
            {
                if (ResolveEnemyContact(hit,
                    laneAttackActive, jumpAttackActive, slideAttackActive))
                    return;
            }
        }

        private bool ResolveEnemyContact(Collider collider,
            bool laneAttackActive, bool jumpAttackActive, bool slideAttackActive)
        {
            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsKilled)
                return false;

            if (laneAttackActive || jumpAttackActive || slideAttackActive)
            {
                string attackName = laneAttackActive ? "Lane Attack" :
                    jumpAttackActive ? "Jump Attack" : "Slide Attack";
                TryKillEnemy(enemy, attackName);
                return false;
            }

            Debug.Log("Game Over: Hit Enemy Without Attack");
            gameTimer.TriggerGameOver();
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = attackTimeRemaining > 0f ? Color.red : Color.yellow;
            if (attackDirection != 0 && attackTimeRemaining > 0f)
            {
                Gizmos.DrawWireCube(GetAttackBoxCenter(attackDirection), attackBoxSize);
            }
            else
            {
                Gizmos.DrawWireCube(GetAttackBoxCenter(-1), attackBoxSize);
                Gizmos.DrawWireCube(GetAttackBoxCenter(1), attackBoxSize);
            }
            DrawSlideGizmo();
            Gizmos.color = jumpAttackTimeRemaining > 0f
                ? Color.green : new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireCube(GetJumpAttackBoxCenter(), jumpAttackBoxSize);
        }

        private void DrawSlideGizmo()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (characterController == null)
                return;

            Gizmos.color = isSliding ? Color.cyan : new Color(0f, 1f, 1f, 0.5f);
            Vector3 center = GetSlideAttackBoxCenter();
            if (!isSliding)
            {
                float slideHeight = Mathf.Max(characterController.radius * 2f,
                    characterController.height * slideHeightRatio);
                center.y -= (characterController.height - slideHeight) * 0.5f;
            }
            Gizmos.DrawWireCube(center, slideAttackBoxSize);
        }

        public void Jump()
        {
            if (!isActiveAndEnabled || (gameTimer != null && gameTimer.IsGameOver))
                return;
            if (lastJumpActionFrame == Time.frameCount)
                return;

            if (isGrounded)
            {
                EndSlide();
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpAttackTimeRemaining = jumpAttackDuration;
                StopAirDash();
                Debug.Log("Normal Jump");
            }
            else
            {
                if (Time.time - lastAirJumpTime < airJumpMinInterval)
                    return;

                Enemy target = FindAirTarget();
                if (target == null)
                {
                    Debug.Log("No Air Target Found");
                    return;
                }

                Debug.Log("Air Target Found: " + target.name);
                lastAirJumpTime = Time.time;
                isFastFalling = false;
                pendingGroundSlide = false;
                verticalSpeed = 0f;
                jumpAttackTimeRemaining = 0f;
                airDashTarget = target;
                airHomingElapsed = 0f;
                isHomingDash = true;
                Debug.Log("Homing Dash Started");
            }
            lastJumpActionFrame = Time.frameCount;
            isGrounded = false;
        }

        public void Slide()
        {
            if (!isActiveAndEnabled || (gameTimer != null && gameTimer.IsGameOver))
                return;

            if (isGrounded)
            {
                StartGroundSlide();
            }
            else
            {
                StopAirDash();
                pendingGroundSlide = true;
                isFastFalling = true;
                verticalSpeed = Mathf.Min(verticalSpeed, -fastFallSpeed);
            }
        }

        private void StartGroundSlide()
        {
            isSliding = true;
            slideTimeRemaining = slideDuration;
            SetSlideColliders(true);
            SetSlideVisual(true);
        }

        private void EndSlide()
        {
            if (!isSliding)
                return;

            isSliding = false;
            slideTimeRemaining = 0f;
            SetSlideColliders(false);
            SetSlideVisual(false);
        }

        private void SetupVisual()
        {
            // The current prototype cube has its renderer on the controller root.
            // Copy only its appearance to a child so a visual tilt cannot rotate physics.
            if (visualTransform == transform)
                visualTransform = null;
            if (visualTransform == null)
            {
                MeshFilter sourceMesh = GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
                if (sourceMesh != null && sourceRenderer != null)
                {
                    GameObject visual = new GameObject("PlayerVisual");
                    visual.transform.SetParent(transform, false);
                    visual.AddComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                    visual.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                    sourceRenderer.enabled = false;
                    visualTransform = visual.transform;
                }
            }

            if (visualTransform != null)
            {
                standingVisualRotation = visualTransform.localRotation;
                standingVisualPosition = visualTransform.localPosition;
            }
        }

        private void SetSlideVisual(bool sliding)
        {
            if (visualTransform == null)
                return;

            visualTransform.localRotation = sliding
                ? standingVisualRotation * Quaternion.Euler(-90f, 0f, 0f)
                : standingVisualRotation;
            float slideHeight = Mathf.Max(characterController.radius * 2f,
                standingHeight * slideHeightRatio);
            visualTransform.localPosition = sliding
                ? standingVisualPosition - Vector3.up * ((standingHeight - slideHeight) * 0.5f)
                : standingVisualPosition;
        }

        private void SetSlideColliders(bool sliding)
        {
            float height = sliding
                ? Mathf.Max(characterController.radius * 2f, standingHeight * slideHeightRatio)
                : standingHeight;
            characterController.height = height;
            characterController.center = standingCenter - Vector3.up * ((standingHeight - height) * 0.5f);

            if (boxCollider != null)
            {
                Vector3 size = standingBoxSize;
                size.y = sliding ? standingBoxSize.y * slideHeightRatio : standingBoxSize.y;
                boxCollider.size = size;
                boxCollider.center = standingBoxCenter - Vector3.up * ((standingBoxSize.y - size.y) * 0.5f);
            }

            if (capsuleCollider != null)
            {
                float capsuleHeight = sliding
                    ? Mathf.Max(capsuleCollider.radius * 2f, standingCapsuleHeight * slideHeightRatio)
                    : standingCapsuleHeight;
                capsuleCollider.height = capsuleHeight;
                capsuleCollider.center = standingCapsuleCenter - Vector3.up * ((standingCapsuleHeight - capsuleHeight) * 0.5f);
            }
        }

        private void OnValidate()
        {
            if (gravity >= 0f)
                gravity = -0.1f;
        }
    }
}
