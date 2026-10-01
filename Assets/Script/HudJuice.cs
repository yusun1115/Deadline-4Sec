using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Presentation-only HUD motion: floating "+score" popups at kills, a combo
    // punch tinted by multiplier tier, a timer pop on every 4-second reset and a
    // critical-time pulse. Reads game state; never writes it.
    public sealed class HudJuice : MonoBehaviour
    {
        private const int PopupCount = 10;

        private static readonly Color[] TierColors =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 0.45f, 0.7f),
            new Color(0.75f, 0.45f, 1f),
            new Color(1f, 0.82f, 0.3f),
        };

        private sealed class Popup
        {
            public TextMeshProUGUI Text;
            public RectTransform Rect;
            public Vector3 World;
            public float Start;
            public bool Active;
        }

        private RunManager run;
        private GameTimer timer;
        private PlayerController player;
        private GameFlowManager flow;
        private Camera view;
        private RectTransform hud;
        private Canvas canvas;
        private Text timerText;
        private Text comboText;
        private RectTransform timerFrame;
        private readonly Popup[] popups = new Popup[PopupCount];
        private int nextPopup;
        private int lastScore;
        private int lastCombo;
        private Vector3 lastKillPoint;
        private float lastKillTime = -10f;
        private float comboPunchStart = -10f;
        private float timerPopStart = -10f;
        private Vector3 timerBaseScale = Vector3.one;
        private Vector3 comboBaseScale = Vector3.one;
        private TMP_Text countdownText;
        private string lastCountdown;
        private float countdownPopStart = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            RunManager run = FindFirstObjectByType<RunManager>();
            if (run != null && run.GetComponent<HudJuice>() == null)
                run.gameObject.AddComponent<HudJuice>();
        }

        private void Start()
        {
            run = GetComponent<RunManager>();
            timer = FindFirstObjectByType<GameTimer>();
            flow = FindFirstObjectByType<GameFlowManager>();
            player = FindFirstObjectByType<PlayerController>();
            view = Camera.main;
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject == null)
                return;
            canvas = canvasObject.GetComponent<Canvas>();
            Transform gameplay = canvasObject.transform.Find("GameplayUI");
            if (gameplay == null)
                return;
            hud = (RectTransform)gameplay;
            timerText = gameplay.Find("SurvivalTimer")?.GetComponent<Text>();
            comboText = gameplay.Find("ComboText")?.GetComponent<Text>();
            timerFrame = gameplay.Find("TimerFrame") as RectTransform;
            countdownText = canvasObject.transform.Find("CountdownUI/CountdownText")?.GetComponent<TMP_Text>();

            if (timerText != null)
                timerBaseScale = timerText.rectTransform.localScale;
            if (comboText != null)
                comboBaseScale = comboText.rectTransform.localScale;

            for (int i = 0; i < PopupCount; i++)
            {
                GameObject go = new GameObject("ScorePopup", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(hud, false);
                TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
                GameFont.Apply(text);
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = 40f;
                text.raycastTarget = false;
                RectTransform rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(300f, 70f);
                go.SetActive(false);
                popups[i] = new Popup { Text = text, Rect = rect };
            }

            if (player != null)
                player.EnemyKilled += OnEnemyKilled;
            if (timer != null)
                timer.TimerReset += OnTimerReset;
            lastScore = run != null ? run.CurrentScore : 0;
        }

        private void OnDestroy()
        {
            if (player != null)
                player.EnemyKilled -= OnEnemyKilled;
            if (timer != null)
                timer.TimerReset -= OnTimerReset;
        }

        private void OnEnemyKilled(Enemy enemy, string attack)
        {
            if (enemy == null)
                return;
            lastKillPoint = enemy.transform.position + Vector3.up * 1.2f;
            lastKillTime = Time.unscaledTime;
        }

        private void OnTimerReset(float previous) => timerPopStart = Time.unscaledTime;

        private void LateUpdate()
        {
            if (run == null || hud == null)
                return;
            float now = Time.unscaledTime;

            int score = run.CurrentScore;
            if (score < lastScore)
                lastScore = score; // new run
            if (score > lastScore)
            {
                Vector3 at = now - lastKillTime < 0.3f ? lastKillPoint
                    : player != null ? player.transform.position + new Vector3(0f, 2f, 1.5f) : Vector3.zero;
                SpawnPopup("+" + (score - lastScore), at, TierColors[TierIndex()]);
                lastScore = score;
            }

            int combo = run.CurrentCombo;
            if (combo > lastCombo)
                comboPunchStart = now;
            lastCombo = combo;

            UpdatePopups(now);
            UpdateCombo(now);
            UpdateTimer(now);
            UpdateCountdown(now);
        }

        // "Ready?" slams in, "Go!" bursts out in hot pink.
        private void UpdateCountdown(float now)
        {
            if (countdownText == null || !countdownText.isActiveAndEnabled)
                return;
            if (countdownText.text != lastCountdown)
            {
                lastCountdown = countdownText.text;
                countdownPopStart = now;
                countdownText.color = lastCountdown == "Go!" ? new Color(1f, 0.3f, 0.6f) : Color.white;
            }
            float t = Mathf.Clamp01((now - countdownPopStart) / 0.28f);
            float scale = t < 1f ? Mathf.Lerp(1.7f, 1f, 1f - (1f - t) * (1f - t) * (1f - t)) : 1f;
            countdownText.rectTransform.localScale = Vector3.one * scale;
        }

        private int TierIndex()
        {
            int m = run.ScoreMultiplier;
            return Mathf.Clamp(m - 1, 0, TierColors.Length - 1);
        }

        private void SpawnPopup(string label, Vector3 world, Color color)
        {
            Popup p = popups[nextPopup];
            nextPopup = (nextPopup + 1) % PopupCount;
            p.Text.text = label;
            p.Text.color = color;
            p.World = world;
            p.Start = Time.unscaledTime;
            p.Active = true;
            p.Text.gameObject.SetActive(true);
        }

        private void UpdatePopups(float now)
        {
            foreach (Popup p in popups)
            {
                if (p == null || !p.Active)
                    continue;
                float t = (now - p.Start) / 0.7f;
                if (t >= 1f || view == null)
                {
                    p.Active = false;
                    p.Text.gameObject.SetActive(false);
                    continue;
                }
                Vector3 screen = view.WorldToScreenPoint(p.World);
                if (screen.z < 0f)
                {
                    p.Text.gameObject.SetActive(false);
                    continue;
                }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, screen,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local);
                float rise = 90f * (1f - (1f - t) * (1f - t));
                p.Rect.anchoredPosition = local + new Vector2(0f, rise) - hud.rect.center;
                float pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.25f, t / 0.15f) : Mathf.Lerp(1.25f, 1f, (t - 0.15f) / 0.2f);
                p.Rect.localScale = Vector3.one * Mathf.Max(pop, 1f - Mathf.Max(0f, t - 0.7f));
                Color c = p.Text.color;
                c.a = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                p.Text.color = c;
            }
        }

        private void UpdateCombo(float now)
        {
            if (comboText == null)
                return;
            float t = (now - comboPunchStart) / 0.22f;
            float punch = t < 1f ? 1f + Mathf.Sin(t * Mathf.PI) * (0.35f + 0.05f * Mathf.Min(lastCombo, 6)) : 1f;
            comboText.rectTransform.localScale = comboBaseScale * punch;
            comboText.color = lastCombo > 0 ? TierColors[TierIndex()] : new Color(1f, 1f, 1f, 0.55f);
        }

        private void UpdateTimer(float now)
        {
            if (timerText == null || timer == null)
                return;
            float pop = 1f;
            float t = (now - timerPopStart) / 0.25f;
            if (t < 1f)
                pop += Mathf.Sin(t * Mathf.PI) * 0.28f;
            bool critical = timer.IsRunning && timer.RemainingTime <= 1f;
            if (critical)
                pop += (Mathf.Sin(now * 18f) * 0.5f + 0.5f) * 0.12f;
            timerText.rectTransform.localScale = timerBaseScale * pop;
            if (timerFrame != null)
                timerFrame.localScale = Vector3.one * (1f + (pop - 1f) * 0.5f);
        }
    }
}
