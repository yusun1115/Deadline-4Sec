using UnityEngine;

namespace Deadline4Sec
{
    // Menu framing: while the title, settings, upgrade or skin screens are open,
    // the camera looks at the Reaper from the front in a close-up (idle pose).
    // Gameplay framing is untouched; when a run starts the intro camera takes
    // over from the side view as before. Runs after CameraFeedbackController.
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Camera))]
    public sealed class TitleShowcaseCamera : MonoBehaviour
    {
        // Offsets are from the PlayerController origin (capsule center, ~1m above the feet).
        [SerializeField] private Vector3 titleOffset = new Vector3(0f, 0.9f, 3.9f);
        [SerializeField] private Vector3 titleLookOffset = new Vector3(0f, 0.72f, 0f);
        // The camera faces -Z, so world -X is screen right: shifting it that way puts
        // the Reaper on the left, clear of the skin card column.
        [SerializeField] private Vector3 skinOffset = new Vector3(-0.3f, 0.7f, 4.4f);
        [SerializeField] private Vector3 skinLookOffset = new Vector3(-0.3f, 0.45f, 0f);
        [SerializeField, Range(20f, 70f)] private float fieldOfView = 42f;
        [SerializeField, Min(0.01f)] private float blendSpeed = 7f;
        // Leaving the title, the camera swings from the front close-up around the
        // Reaper onto the run camera instead of cutting to it.
        [SerializeField, Min(0.05f)] private float handoffDuration = 1.15f;
        [SerializeField] private float handoffPivotHeight = 0.9f;

        private GameFlowManager flow;
        private PlayerController player;
        private Camera view;
        private bool wasShowcasing;
        private Vector3 currentOffset;
        private Vector3 currentLook;
        private Vector3 gameplayLocalPosition;
        private Quaternion gameplayLocalRotation;
        private float gameplayFov;
        private bool handingOff;
        private float handoffElapsed;
        private Vector3 shownOffset;
        private Quaternion shownRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            Camera main = Camera.main;
            if (main != null && main.GetComponent<TitleShowcaseCamera>() == null &&
                FindFirstObjectByType<GameFlowManager>() != null)
                main.gameObject.AddComponent<TitleShowcaseCamera>();
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            flow = FindFirstObjectByType<GameFlowManager>();
            player = FindFirstObjectByType<PlayerController>();
            currentOffset = titleOffset;
            currentLook = titleLookOffset;
            gameplayLocalPosition = transform.localPosition;
            gameplayLocalRotation = transform.localRotation;
            gameplayFov = view.fieldOfView;
        }

        private bool Showcasing
        {
            get
            {
                if (flow == null)
                    return false;
                GameFlowManager.GameState s = flow.State;
                return s == GameFlowManager.GameState.Title || s == GameFlowManager.GameState.Settings ||
                       s == GameFlowManager.GameState.Upgrades || s == GameFlowManager.GameState.Skins;
            }
        }

        private void LateUpdate()
        {
            if (player == null || view == null)
                return;
            bool showcasing = Showcasing;
            if (!showcasing)
            {
                if (wasShowcasing)
                {
                    // Hand the camera back as the gameplay rig left it. The Ready?
                    // intro already placed the side view this frame, so leave it.
                    GameFlowManager.GameState s = flow.State;
                    if (s != GameFlowManager.GameState.Ready)
                    {
                        transform.localPosition = gameplayLocalPosition;
                        transform.localRotation = gameplayLocalRotation;
                    }
                    view.fieldOfView = gameplayFov;
                    handingOff = s == GameFlowManager.GameState.Ready || s == GameFlowManager.GameState.Tutorial;
                    handoffElapsed = 0f;
                }
                wasShowcasing = false;
                if (handingOff)
                    BlendFromShowcase();
                return;
            }
            handingOff = false;

            bool skins = flow.State == GameFlowManager.GameState.Skins;
            Vector3 targetOffset = skins ? skinOffset : titleOffset;
            Vector3 targetLook = skins ? skinLookOffset : titleLookOffset;
            if (!wasShowcasing)
            {
                currentOffset = targetOffset;
                currentLook = targetLook;
            }
            float k = 1f - Mathf.Exp(-blendSpeed * Time.unscaledDeltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, k);
            currentLook = Vector3.Lerp(currentLook, targetLook, k);

            // The Reaper faces +Z down the track; the camera stands in front of it.
            Vector3 origin = player.transform.position;
            transform.position = origin + currentOffset;
            transform.rotation = Quaternion.LookRotation(origin + currentLook - transform.position, Vector3.up);
            view.fieldOfView = fieldOfView;
            wasShowcasing = true;
            shownOffset = currentOffset;
            shownRotation = transform.rotation;
        }

        // The run rig has already posed the camera this frame (side intro or the
        // gameplay view); orbit from the last showcase pose onto it around the
        // Reaper, so the swing never cuts through the character.
        private void BlendFromShowcase()
        {
            // Clamped like the Ready? intro so the hitch of spawning the opening
            // course does not skip the start of the swing.
            handoffElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.Clamp01(handoffElapsed / handoffDuration);
            if (t >= 1f || flow.State == GameFlowManager.GameState.Playing)
            {
                handingOff = false;
                if (flow.State != GameFlowManager.GameState.Ready)
                {
                    transform.localPosition = gameplayLocalPosition;
                    transform.localRotation = gameplayLocalRotation;
                }
                view.fieldOfView = gameplayFov;
                return;
            }
            float e = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; // InOutCubic
            // The Ready? intro re-poses the camera every frame; other states (the
            // tutorial) only reset the position, so aim for the saved gameplay pose.
            Vector3 targetPosition = transform.position;
            Quaternion targetRotation = transform.rotation;
            if (flow.State != GameFlowManager.GameState.Ready)
            {
                Transform parent = transform.parent;
                targetPosition = parent != null ? parent.TransformPoint(gameplayLocalPosition) : gameplayLocalPosition;
                targetRotation = parent != null ? parent.rotation * gameplayLocalRotation : gameplayLocalRotation;
            }
            Vector3 pivot = player.transform.position + Vector3.up * handoffPivotHeight;
            Vector3 from = player.transform.position + shownOffset - pivot;
            Vector3 to = targetPosition - pivot;
            Vector3 direction = Vector3.Slerp(from.normalized, to.normalized, e);
            transform.position = pivot + direction * Mathf.Lerp(from.magnitude, to.magnitude, e);
            transform.rotation = Quaternion.Slerp(shownRotation, targetRotation, e);
            view.fieldOfView = Mathf.Lerp(fieldOfView, gameplayFov, e);
        }
    }
}
