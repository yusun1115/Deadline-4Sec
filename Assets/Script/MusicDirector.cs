using UnityEngine;

namespace Deadline4Sec
{
    // Cross-fades title and run music from the game state and ducks it on
    // death. Silent until clips are assigned in the SoundLibrary.
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.6f;
        [SerializeField, Range(0f, 1f)] private float gameOverDuck = 0.35f;

        private GameFlowManager flow;
        private AudioSource[] sources;
        private int active;
        private AudioClip wanted;

        // Attach to whichever scene holds the game flow, without editing scenes.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToGameFlow()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            GameFlowManager flow = FindFirstObjectByType<GameFlowManager>();
            if (flow != null && flow.GetComponent<MusicDirector>() == null)
                flow.gameObject.AddComponent<MusicDirector>();
        }

        private void Awake()
        {
            flow = TryGetComponent(out GameFlowManager own) ? own : FindFirstObjectByType<GameFlowManager>();
            sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].loop = true;
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].volume = 0f;
                sources[i].priority = 0;
            }
        }

        private void Update()
        {
            SoundLibrary library = SoundLibrary.Instance;
            if (library == null || flow == null)
                return;

            GameFlowManager.GameState state = flow.State;
            bool inRun = state == GameFlowManager.GameState.Ready || state == GameFlowManager.GameState.Playing ||
                         state == GameFlowManager.GameState.Tutorial || state == GameFlowManager.GameState.GameOver ||
                         state == GameFlowManager.GameState.Result;
            AudioClip next = inRun ? library.runMusic : library.titleMusic;
            if (next != wanted)
            {
                wanted = next;
                active = 1 - active;
                sources[active].clip = next;
                if (next != null)
                    sources[active].Play();
            }

            float target = GamePreferences.SoundEnabled ? library.musicVolume : 0f;
            if (state == GameFlowManager.GameState.GameOver || state == GameFlowManager.GameState.Result)
                target *= gameOverDuck;
            float step = Time.unscaledDeltaTime / fadeSeconds;
            for (int i = 0; i < sources.Length; i++)
            {
                float goal = i == active && sources[i].clip != null ? target : 0f;
                sources[i].volume = Mathf.MoveTowards(sources[i].volume, goal, step * Mathf.Max(target, 0.1f));
                if (i != active && sources[i].isPlaying && sources[i].volume <= 0.001f)
                    sources[i].Stop();
            }
        }
    }
}
