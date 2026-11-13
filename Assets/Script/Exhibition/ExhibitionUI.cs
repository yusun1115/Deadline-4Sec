using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Deadline4Sec
{
    // The exhibition screens, over the normal ones (ExhibitionRanking.md):
    //   WaitingForPlayer  the normal title (its tap starts the visitor's session)
    //   RevivePrompt      ONE MORE CHANCE: REVIVE / FINISH RUN
    //   NicknameEntry     the first result of a visitor
    //   Ranking           TOP 10 (+ the visitor's rank), RETRY / FINISH, and an
    //                     automatic FINISH after autoFinishIdleSeconds
    // The game's own GameState stays as it is; this only follows it.
    // Admin: Ctrl + Shift + Delete, at any time: skin currencies +50, souls
    // +10000, the ranking reset and the whole save reset (keys 1-4, Y / N).
    // Added to the GameFlowManager object only while the exhibition mode is on.
    public sealed class ExhibitionUI : MonoBehaviour
    {
        public enum Flow { WaitingForPlayer, Tutorial, Playing, RevivePrompt, Result, NicknameEntry, Ranking, SessionEnding }

        private const float ResultHoldSeconds = 1.6f;
        private static readonly Color Pink = new Color(1f, 0.32f, 0.62f);
        private static readonly Color LightPink = new Color(1f, 0.72f, 0.86f);
        private static readonly Color PanelColor = new Color(0.11f, 0.05f, 0.15f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.08f, 0.24f, 1f);
        private static readonly Color Gold = new Color(1f, 0.83f, 0.35f);

        private GameFlowManager flow;
        private RectTransform root;
        private GameObject revivePanel, nicknamePanel, rankingPanel, adminPanel, confirmPanel, noticePanel;
        private TMP_Text reviveCountdown, nicknameScore, nicknameError, autoFinishLabel, yourRankLabel, adminInfo, confirmQuestion;
        private System.Action confirmAction;
        private TMP_InputField nicknameInput;
        private readonly List<TMP_Text[]> rows = new List<TMP_Text[]>();
        private readonly List<Image> rowBacks = new List<Image>();
        private int pendingScore;
        private float autoFinishAt = float.PositiveInfinity;
        private bool resultShown;

        // Not waiting yet: the first frame on the title shows the start screen.
        public Flow Current { get; private set; } = Flow.SessionEnding;
        public float AutoFinishRemaining => float.IsPositiveInfinity(autoFinishAt) ? -1f : Mathf.Max(0f, autoFinishAt - Time.unscaledTime);
        public string NicknameError => nicknameError != null ? nicknameError.text : "";
        public bool AdminOpen => adminPanel != null && adminPanel.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(default, default);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            if (!ExhibitionMode.IsActive)
                return;
            GameFlowManager found = FindFirstObjectByType<GameFlowManager>();
            if (found != null && !found.TryGetComponent(out ExhibitionUI _))
                found.gameObject.AddComponent<ExhibitionUI>();
        }

        private void Awake()
        {
            flow = GetComponent<GameFlowManager>();
            Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
            if (canvas == null)
                return;
            root = (RectTransform)new GameObject("ExhibitionUI", typeof(RectTransform)).transform;
            root.SetParent(canvas.transform, false);
            Stretch(root);
            BuildRevive();
            BuildNickname();
            BuildRanking();
            BuildAdmin();
            HideAll();
            CloseAdmin();
        }

        private void LateUpdate()
        {
            if (root == null || flow == null)
                return;
            root.SetAsLastSibling();
            if (AdminShortcutPressed())
                OpenAdmin();
            else if (AdminOpen)
                HandleAdminKeys();
            switch (flow.State)
            {
                case GameFlowManager.GameState.Title:
                    if (Current != Flow.WaitingForPlayer)
                        ShowStart();
                    break;
                case GameFlowManager.GameState.Tutorial:
                    SetFlow(Flow.Tutorial);
                    break;
                case GameFlowManager.GameState.Ready:
                case GameFlowManager.GameState.Playing:
                case GameFlowManager.GameState.Dying:
                    SetFlow(Flow.Playing);
                    break;
            }
            if (Current == Flow.Ranking)
            {
                float left = AutoFinishRemaining;
                if (autoFinishLabel != null)
                    autoFinishLabel.text = $"다음 관람객을 위해 {Mathf.CeilToInt(left)}초 후 자동으로 종료됩니다";
                if (left <= 0f)
                    FinishSession();
            }
        }

        private void SetFlow(Flow next)
        {
            if (Current == next)
                return;
            Current = next;
            if (next == Flow.Tutorial || next == Flow.Playing)
                HideAll();
        }

        // ---------------- start ----------------

        // The normal title; its own tap (StartFromTitle) begins the session.
        private void ShowStart()
        {
            HideAll();
            Current = Flow.WaitingForPlayer;
            resultShown = false;
        }

        // The same as tapping the title (tests).
        public void TapStart()
        {
            if (Current == Flow.WaitingForPlayer && flow != null && !AdminOpen)
                flow.StartFromTitle();
        }

        // ---------------- revive ----------------

        public void ShowRevive(float seconds)
        {
            HideAll();
            Current = Flow.RevivePrompt;
            revivePanel.SetActive(true);
            SetReviveCountdown(seconds);
        }

        public void SetReviveCountdown(float seconds)
        {
            if (reviveCountdown != null)
                reviveCountdown.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();
        }

        public void HideRevive()
        {
            if (revivePanel != null)
                revivePanel.SetActive(false);
        }

        public void Revive() => flow?.UseExhibitionRevive();
        public void FinishRun() => flow?.GiveUpRevive();

        // ---------------- result → nickname / ranking ----------------

        public void ShowResult(int score)
        {
            if (resultShown)
                return;
            resultShown = true;
            pendingScore = score;
            Current = Flow.Result;
            StartCoroutine(AfterResult());
        }

        // The score counts up on the normal result first.
        private IEnumerator AfterResult()
        {
            yield return new WaitForSecondsRealtime(ResultHoldSeconds);
            if (ExhibitionSession.HasNickname)
            {
                // A retry: the same name, updated without asking again.
                ExhibitionLeaderboardService.Register(ExhibitionSession.CurrentNickname, pendingScore);
                ExhibitionSession.RecordScore(pendingScore);
                ShowRanking();
            }
            else
                ShowNickname();
        }

        private void ShowNickname()
        {
            HideAll();
            Current = Flow.NicknameEntry;
            nicknamePanel.SetActive(true);
            nicknameScore.text = pendingScore.ToString("N0", CultureInfo.InvariantCulture);
            nicknameError.text = "";
            nicknameInput.text = "";
            nicknameInput.characterLimit = ExhibitionMode.Settings.nicknameMaxLength;
            nicknameInput.ActivateInputField();
        }

        public bool SubmitNickname(string nickname)
        {
            if (Current != Flow.NicknameEntry)
                return false;
            string problem = ExhibitionLeaderboardService.Validate(nickname);
            if (problem.Length > 0)
            {
                nicknameError.text = problem;
                return false;
            }
            string name = ExhibitionLeaderboardService.Normalize(nickname);
            ExhibitionLeaderboardService.Register(name, pendingScore);
            ExhibitionSession.SetNickname(name, pendingScore);
            ShowRanking();
            return true;
        }

        private void ShowRanking()
        {
            HideAll();
            Current = Flow.Ranking;
            rankingPanel.SetActive(true);
            autoFinishAt = Time.unscaledTime + ExhibitionMode.Settings.autoFinishIdleSeconds;
            IReadOnlyList<ExhibitionScoreEntry> entries = ExhibitionLeaderboardService.Entries;
            string you = ExhibitionSession.CurrentNickname;
            for (int i = 0; i < rows.Count; i++)
            {
                bool filled = i < entries.Count;
                bool mine = filled && ExhibitionLeaderboardService.SameName(entries[i].nickname, you);
                TMP_Text[] row = rows[i];
                row[0].text = (i + 1).ToString("00");
                row[1].text = filled ? entries[i].nickname + (mine ? "  ← YOU" : "") : "-";
                row[2].text = filled ? entries[i].score.ToString("N0", CultureInfo.InvariantCulture) : "";
                Color text = mine ? Gold : i < 3 ? LightPink : Color.white;
                foreach (TMP_Text cell in row)
                    cell.color = filled ? text : new Color(1f, 1f, 1f, 0.35f);
                rowBacks[i].color = mine ? new Color(Pink.r, Pink.g, Pink.b, 0.45f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.05f : 0.02f);
            }
            int rank = ExhibitionLeaderboardService.RankOf(you);
            ExhibitionScoreEntry best = ExhibitionLeaderboardService.Find(you);
            yourRankLabel.text = rank > rows.Count && best != null
                ? $"YOUR RANK  {rank}위   {best.nickname}   {best.score.ToString("N0", CultureInfo.InvariantCulture)}"
                : best != null && pendingScore < best.score
                    ? $"이번 점수 {pendingScore.ToString("N0", CultureInfo.InvariantCulture)} · 최고 기록 유지"
                    : "";
        }

        public void RetryAsSameVisitor()
        {
            if (Current != Flow.Ranking || flow == null)
                return;
            HideAll();
            Current = Flow.SessionEnding;
            ExhibitionSession.NextRun();
            flow.Retry(); // gift boxes of the run open first, as in the normal game
        }

        // Manual FINISH and the automatic one: the same reset.
        public void FinishSession()
        {
            if ((Current != Flow.Ranking && Current != Flow.Result) || flow == null)
                return;
            HideAll();
            Current = Flow.SessionEnding;
            ExhibitionSession.ResetExhibitionSession();
            flow.MainMenu();
        }

        // ---------------- notice ----------------

        // A short message with one button (Classic is not ranked).
        public void ShowNotice(string message, string button, System.Action onOk)
        {
            if (noticePanel != null)
                Destroy(noticePanel);
            noticePanel = Layer("Notice", new Color(0f, 0f, 0f, 0.7f));
            RectTransform card = Card(noticePanel.transform, new Vector2(640f, 380f));
            TMP_Text text = Label(card, message, new Vector2(0f, 70f), new Vector2(580f, 140f), 34f, Color.white);
            text.textWrappingMode = TextWrappingModes.Normal;
            MakeButton(card, button, null, new Vector2(0f, -100f), new Vector2(420f, 100f), Pink, () =>
            {
                Destroy(noticePanel);
                noticePanel = null;
                onOk?.Invoke();
            });
        }

        public bool NoticeOpen => noticePanel != null;

        // ---------------- admin ----------------

        public void OpenAdmin()
        {
            adminPanel.SetActive(true);
            confirmPanel.SetActive(false);
            RefreshAdminInfo();
        }

        private void RefreshAdminInfo(string done = null)
        {
            CoinWallet wallet = GetComponent<CoinWallet>();
            adminInfo.text = (done != null ? done + "\n" : "") +
                $"Soul {(wallet != null ? wallet.TotalCoins : PlayerPrefs.GetInt(CoinWallet.TotalCoinsKey, 0)):N0} · " +
                $"전시 랭킹 {ExhibitionLeaderboardService.Entries.Count}명";
        }

        // Every skin currency (Frost Shard, Blood Shard, Cyber Core, Eclipse Fragment).
        public void GrantSkinCurrencies(int amount = 50)
        {
            foreach (SkinCurrency currency in System.Enum.GetValues(typeof(SkinCurrency)))
            {
                if (currency == SkinCurrency.None)
                    continue;
                string key = "Currency_" + currency;
                long total = (long)Mathf.Max(0, PlayerPrefs.GetInt(key, 0)) + amount;
                PlayerPrefs.SetInt(key, (int)System.Math.Min(int.MaxValue, total));
            }
            PlayerPrefs.Save();
            GetComponent<GameExtrasUI>()?.Refresh();
            RefreshAdminInfo($"스킨 재화 +{amount} 지급");
        }

        public void GrantSouls(int amount = 10000)
        {
            CoinWallet wallet = GetComponent<CoinWallet>();
            if (wallet != null)
                wallet.CreditTotalCoins(amount);
            RefreshAdminInfo($"Soul +{amount:N0} 지급");
        }

        // Everything this device saved: PlayerPrefs (souls, skins, upgrades,
        // items, records, achievements, quests, tutorial, settings) and the
        // exhibition ranking. The scene starts again from nothing.
        public void ResetAllData()
        {
            WipeSavedData();
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        public static void WipeSavedData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            ExhibitionLeaderboardService.ClearExhibitionLeaderboard();
            PlayerStats.ReloadFromSave();
            AchievementManager.ReloadFromSave();
            DailyQuestManager.ReloadFromSave();
            ExhibitionSession.ResetExhibitionSession();
            Debug.Log("All saved data cleared (exhibition admin).");
        }

        private void AskConfirm(string question, System.Action action)
        {
            confirmQuestion.text = question;
            confirmAction = action;
            confirmPanel.SetActive(true);
        }

        private void AnswerConfirm(bool yes)
        {
            confirmPanel.SetActive(false);
            System.Action action = confirmAction;
            confirmAction = null;
            if (yes)
                action?.Invoke();
        }

        private void AskRankingReset() => AskConfirm("정말 전시 랭킹을 삭제하시겠습니까?\n되돌릴 수 없습니다.", ConfirmClear);

        private void AskFullReset() => AskConfirm(
            "정말 전체 데이터를 초기화하시겠습니까?\nSoul, 스킨, 업그레이드, 아이템, 기록, 업적,\n퀘스트, 튜토리얼, 설정과 전시 랭킹이 모두 지워집니다.",
            ResetAllData);

        // 1-4 the buttons, Esc closes; while asked: Y yes, N / Esc no.
        private void HandleAdminKeys()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keys = Keyboard.current;
            if (keys == null)
                return;
            bool one = keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame;
            bool two = keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame;
            bool three = keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame;
            bool four = keys.digit4Key.wasPressedThisFrame || keys.numpad4Key.wasPressedThisFrame;
            bool yes = keys.yKey.wasPressedThisFrame;
            bool no = keys.nKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame;
#else
            bool one = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            bool two = Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            bool three = Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            bool four = Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            bool yes = Input.GetKeyDown(KeyCode.Y);
            bool no = Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape);
#endif
            if (confirmPanel.activeSelf)
            {
                if (yes || no)
                    AnswerConfirm(yes);
                return;
            }
            if (one)
                GrantSkinCurrencies();
            else if (two)
                GrantSouls();
            else if (three)
                AskRankingReset();
            else if (four)
                AskFullReset();
            else if (no)
                CloseAdmin();
        }

        private static bool AdminShortcutPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keys = Keyboard.current;
            return keys != null && keys.deleteKey.wasPressedThisFrame &&
                keys.ctrlKey.isPressed && keys.shiftKey.isPressed;
#else
            return Input.GetKeyDown(KeyCode.Delete) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
#endif
        }

        public void CloseAdmin()
        {
            adminPanel.SetActive(false);
            confirmPanel.SetActive(false);
            confirmAction = null;
        }

        public void ConfirmClear()
        {
            ExhibitionLeaderboardService.ClearExhibitionLeaderboard();
            adminPanel.SetActive(true);
            RefreshAdminInfo("전시 랭킹 초기화 완료");
        }

        // ---------------- building ----------------

        private void HideAll()
        {
            foreach (GameObject panel in new[] { revivePanel, nicknamePanel, rankingPanel })
                if (panel != null)
                    panel.SetActive(false);
            autoFinishAt = float.PositiveInfinity;
        }

        private void BuildRevive()
        {
            revivePanel = Layer("RevivePrompt", new Color(0f, 0f, 0f, 0.6f));
            RectTransform card = Card(revivePanel.transform, new Vector2(620f, 560f));
            Label(card, "GAME OVER", new Vector2(0f, 200f), new Vector2(560f, 60f), 40f, LightPink);
            Label(card, "ONE MORE CHANCE", new Vector2(0f, 130f), new Vector2(560f, 80f), 56f, Color.white);
            reviveCountdown = Label(card, "10", new Vector2(0f, 45f), new Vector2(200f, 80f), 60f, Gold);
            MakeButton(card, "REVIVE", "무료 부활 1회", new Vector2(0f, -70f), new Vector2(460f, 110f), Pink, Revive);
            MakeButton(card, "FINISH RUN", null, new Vector2(0f, -195f), new Vector2(460f, 80f), ButtonColor, FinishRun);
        }

        private void BuildNickname()
        {
            nicknamePanel = Layer("NicknameEntry", new Color(0f, 0f, 0f, 0.7f));
            RectTransform card = Card(nicknamePanel.transform, new Vector2(660f, 640f));
            Label(card, "FINAL SCORE", new Vector2(0f, 245f), new Vector2(560f, 50f), 32f, LightPink);
            nicknameScore = Label(card, "0", new Vector2(0f, 175f), new Vector2(600f, 90f), 76f, Gold);
            nicknameScore.font = GameFont.NumberAsset;
            Label(card, "NICKNAME", new Vector2(0f, 90f), new Vector2(560f, 50f), 34f, Color.white);
            nicknameInput = InputField(card, new Vector2(0f, 15f), new Vector2(480f, 90f));
            Label(card, $"{ExhibitionMode.Settings.nicknameMinLength}~{ExhibitionMode.Settings.nicknameMaxLength}자, 공백 없이",
                new Vector2(0f, -50f), new Vector2(560f, 36f), 22f, new Color(1f, 1f, 1f, 0.6f));
            nicknameError = Label(card, "", new Vector2(0f, -90f), new Vector2(560f, 36f), 24f, new Color(1f, 0.45f, 0.45f));
            MakeButton(card, "REGISTER", "랭킹에 등록", new Vector2(0f, -195f), new Vector2(460f, 110f), Pink,
                () => SubmitNickname(nicknameInput.text));
            nicknameInput.onSubmit.AddListener(value => SubmitNickname(value));
        }

        private void BuildRanking()
        {
            rankingPanel = Layer("Ranking", new Color(0f, 0f, 0f, 0.8f));
            RectTransform card = Card(rankingPanel.transform, new Vector2(720f, 1160f));
            Label(card, "EXHIBITION RANKING", new Vector2(0f, 520f), new Vector2(680f, 70f), 48f, Pink);
            int shown = Mathf.Clamp(ExhibitionMode.Settings.shownEntries, 1, 10);
            for (int i = 0; i < shown; i++)
            {
                float y = 430f - i * 66f;
                RectTransform back = Box(card, "Row" + (i + 1), new Vector2(0f, y), new Vector2(660f, 60f), Color.clear);
                rowBacks.Add(back.GetComponent<Image>());
                TMP_Text rank = Label(back, "", new Vector2(-270f, 0f), new Vector2(90f, 50f), 32f, Color.white);
                TMP_Text name = Label(back, "", new Vector2(-30f, 0f), new Vector2(360f, 50f), 32f, Color.white);
                name.alignment = TextAlignmentOptions.Left;
                TMP_Text score = Label(back, "", new Vector2(215f, 0f), new Vector2(210f, 50f), 32f, Color.white);
                score.alignment = TextAlignmentOptions.Right;
                score.font = GameFont.NumberAsset;
                rows.Add(new[] { rank, name, score });
            }
            yourRankLabel = Label(card, "", new Vector2(0f, -250f), new Vector2(660f, 50f), 28f, Gold);
            MakeButton(card, "RETRY", "같은 이름으로 다시 도전 · 순위를 더 올려보세요!", new Vector2(0f, -350f), new Vector2(620f, 120f), Pink, RetryAsSameVisitor);
            MakeButton(card, "FINISH", "다음 관람객에게 넘기기", new Vector2(0f, -480f), new Vector2(620f, 100f), ButtonColor, FinishSession);
            autoFinishLabel = Label(rankingPanel.transform, "", new Vector2(0f, 0f), new Vector2(760f, 40f), 22f, new Color(1f, 1f, 1f, 0.7f));
            RectTransform auto = autoFinishLabel.rectTransform;
            auto.anchorMin = auto.anchorMax = new Vector2(0.5f, 0f);
            auto.anchoredPosition = new Vector2(0f, 40f);
        }

        private void BuildAdmin()
        {
            adminPanel = Layer("AdminPanel", new Color(0f, 0f, 0f, 0.85f));
            RectTransform card = Card(adminPanel.transform, new Vector2(660f, 860f));
            Label(card, "ADMIN", new Vector2(0f, 370f), new Vector2(560f, 60f), 44f, Gold);
            adminInfo = Label(card, "", new Vector2(0f, 290f), new Vector2(600f, 80f), 24f, Color.white);
            adminInfo.textWrappingMode = TextWrappingModes.Normal;
            Color grant = new Color(0.32f, 0.16f, 0.42f);
            Color danger = new Color(0.6f, 0.1f, 0.15f);
            MakeButton(card, "스킨 재화 50개 즉시 획득", "1", new Vector2(0f, 175f), new Vector2(500f, 96f), grant, () => GrantSkinCurrencies());
            MakeButton(card, "Soul 10,000 즉시 획득", "2", new Vector2(0f, 65f), new Vector2(500f, 96f), grant, () => GrantSouls());
            MakeButton(card, "전시 랭킹 초기화", "3", new Vector2(0f, -45f), new Vector2(500f, 96f), danger, AskRankingReset);
            MakeButton(card, "전체 데이터 초기화", "4", new Vector2(0f, -155f), new Vector2(500f, 96f), danger, AskFullReset);
            MakeButton(card, "닫기", "Esc", new Vector2(0f, -300f), new Vector2(500f, 90f), ButtonColor, CloseAdmin);

            confirmPanel = Layer("AdminConfirm", new Color(0f, 0f, 0f, 0.6f));
            confirmPanel.transform.SetParent(adminPanel.transform, false);
            RectTransform ask = Card(confirmPanel.transform, new Vector2(660f, 440f));
            confirmQuestion = Label(ask, "", new Vector2(0f, 90f), new Vector2(600f, 180f), 28f, Color.white);
            confirmQuestion.textWrappingMode = TextWrappingModes.Normal;
            MakeButton(ask, "삭제", "Y", new Vector2(-140f, -120f), new Vector2(240f, 100f), new Color(0.75f, 0.1f, 0.15f), () => AnswerConfirm(true));
            MakeButton(ask, "취소", "N / Esc", new Vector2(140f, -120f), new Vector2(240f, 100f), ButtonColor, () => AnswerConfirm(false));
        }

        private GameObject Layer(string name, Color dim)
        {
            RectTransform layer = Box(root, name, Vector2.zero, Vector2.zero, dim);
            Stretch(layer);
            return layer.gameObject;
        }

        private static RectTransform Card(Transform parent, Vector2 size)
        {
            RectTransform card = Box(parent, "Card", Vector2.zero, size, PanelColor);
            Image image = card.GetComponent<Image>();
            image.sprite = FxSprites.RoundedBox;
            image.type = Image.Type.Sliced;
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Pink.r, Pink.g, Pink.b, 0.75f);
            outline.effectDistance = new Vector2(3f, -3f);
            return card;
        }

        private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(Image)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return rect;
        }

        private static TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            TMP_Text label = GameFlowManager.CreateMenuText(parent, text, position, size, fontSize);
            label.color = color;
            return label;
        }

        private static void MakeButton(Transform parent, string title, string subtitle, Vector2 position, Vector2 size, Color color,
            UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = Box(parent, title + "Button", position, size, color);
            Image image = rect.GetComponent<Image>();
            image.sprite = FxSprites.RoundedBox;
            image.type = Image.Type.Sliced;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            bool two = !string.IsNullOrEmpty(subtitle);
            Label(rect, title, new Vector2(0f, two ? size.y * 0.14f : 0f), new Vector2(size.x - 30f, size.y * (two ? 0.5f : 0.7f)), 40f, Color.white);
            if (two)
                Label(rect, subtitle, new Vector2(0f, -size.y * 0.25f), new Vector2(size.x - 30f, size.y * 0.3f), 22f,
                    new Color(1f, 1f, 1f, 0.85f));
        }

        private static TMP_InputField InputField(Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform box = Box(parent, "NicknameInput", position, size, new Color(0.05f, 0.02f, 0.08f, 1f));
            Image image = box.GetComponent<Image>();
            image.sprite = FxSprites.RoundedBox;
            image.type = Image.Type.Sliced;
            Outline outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = LightPink;
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform area = (RectTransform)new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D)).transform;
            area.SetParent(box, false);
            Stretch(area);
            area.offsetMin = new Vector2(20f, 8f);
            area.offsetMax = new Vector2(-20f, -8f);

            TextMeshProUGUI placeholder = NewInputText(area, "Placeholder", "이름 입력", new Color(1f, 1f, 1f, 0.35f));
            TextMeshProUGUI text = NewInputText(area, "Text", "", Color.white);
            TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = GameFont.Asset;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = ExhibitionMode.Settings.nicknameMaxLength;
            input.caretColor = Pink;
            input.selectionColor = new Color(Pink.r, Pink.g, Pink.b, 0.4f);
            input.targetGraphic = image;
            return input;
        }

        private static TextMeshProUGUI NewInputText(Transform parent, string name, string value, Color color)
        {
            TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            Stretch(text.rectTransform);
            GameFont.Apply(text);
            text.text = value;
            text.fontSize = 44f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
