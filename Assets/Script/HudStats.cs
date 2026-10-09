using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Motion for the top-right stat pills (2026-10-05 request):
    //  - The gift pill is absent at 0 boxes and slides in from the right with
    //    the first one.
    //  - A collected box flies from where it was picked up into the pill's icon
    //    on a curve; the count ticks up when it lands.
    //  - The coin icon punches when coins come in.
    // Presentation only: the inventory is already updated on pickup; this only
    // delays what the label shows until the box lands.
    public sealed class HudStats : MonoBehaviour
    {
        private const float SlideDistance = 300f;
        private const float FlightTime = 0.55f;

        private sealed class Flight
        {
            public RectTransform Rect;
            public Vector2 From;
            public float Start;
        }

        private RunInventory inventory;
        private CoinWallet wallet;
        private RectTransform hud;
        private RectTransform[] giftParts;
        private Vector2[] giftBase;
        private RectTransform giftIcon;
        private TMP_Text giftLabel;
        private RectTransform coinIcon;
        private SoundLibrary library;
        private AudioSource audioSource;

        private readonly List<Flight> flights = new List<Flight>();
        private readonly Stack<RectTransform> idle = new Stack<RectTransform>();
        private bool giftShown;
        private float giftShowStart = -10f;
        private float giftPunchStart = -10f;
        private float coinPunchStart = -10f;
        private int lastCoins;

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
            if (run != null && !run.TryGetComponent(out HudStats _))
                run.gameObject.AddComponent<HudStats>();
        }

        private void Start()
        {
            inventory = FindFirstObjectByType<RunInventory>();
            wallet = FindFirstObjectByType<CoinWallet>();
            hud = GameObject.Find("Canvas")?.transform.Find("GameplayUI") as RectTransform;
            library = SoundLibrary.Instance;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            GiftBoxPickup.Collected += OnGiftCollected;
            if (hud == null)
                return;

            var parts = new List<RectTransform>();
            foreach (string name in new[] { "GiftPill", "GiftPillIcon", "GiftBoxCountText" })
                if (hud.Find(name) is RectTransform rect)
                    parts.Add(rect);
            giftParts = parts.ToArray();
            giftBase = new Vector2[giftParts.Length];
            for (int i = 0; i < giftParts.Length; i++)
                giftBase[i] = giftParts[i].anchoredPosition;
            giftIcon = hud.Find("GiftPillIcon") as RectTransform;
            giftLabel = hud.Find("GiftBoxCountText")?.GetComponent<TMP_Text>();
            coinIcon = hud.Find("CoinPillIcon") as RectTransform;
            lastCoins = wallet != null ? wallet.CurrentRunCoins : 0;
        }

        private void OnDestroy() => GiftBoxPickup.Collected -= OnGiftCollected;

        private void OnGiftCollected(Vector3 world)
        {
            Play(library != null ? library.magnetZip : null, "MagnetZip", 0.6f, 0.8f);
            if (hud == null || giftIcon == null)
                return;
            Camera view = Camera.main;
            Vector2 from = Vector2.zero;
            if (view != null)
                RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, view.WorldToScreenPoint(world), null, out from);
            RectTransform rect = idle.Count > 0 ? idle.Pop() : NewFlyer();
            rect.gameObject.SetActive(true);
            rect.SetAsLastSibling();
            flights.Add(new Flight { Rect = rect, From = from, Start = Time.unscaledTime });
        }

        private RectTransform NewFlyer()
        {
            Image image = new GameObject("GiftFlyer", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(hud, false);
            image.sprite = giftIcon.GetComponent<Image>()?.sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = giftIcon.sizeDelta;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            return image.rectTransform;
        }

        private void LateUpdate()
        {
            if (hud == null || inventory == null)
                return;
            float now = Time.unscaledTime;

            // Flights land on the icon wherever it currently is (it may still be sliding in).
            Vector2 target = giftIcon != null ? (Vector2)hud.InverseTransformPoint(giftIcon.TransformPoint(giftIcon.rect.center)) : Vector2.zero;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight f = flights[i];
                float t = (now - f.Start) / FlightTime;
                if (t >= 1f || !hud.gameObject.activeInHierarchy)
                {
                    f.Rect.gameObject.SetActive(false);
                    idle.Push(f.Rect);
                    flights.RemoveAt(i);
                    giftPunchStart = now;
                    Play(library != null ? library.nearMissChime : null, "NearChime", 0.45f, 1.25f);
                    continue;
                }
                float e = t * t * (3f - 2f * t);
                Vector2 control = Vector2.Lerp(f.From, target, 0.3f) + new Vector2(-140f, 220f);
                Vector2 a = Vector2.Lerp(f.From, control, e), b = Vector2.Lerp(control, target, e);
                f.Rect.anchoredPosition = Vector2.Lerp(a, b, e);
                f.Rect.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, e) * (t < 0.12f ? t / 0.12f : 1f);
                f.Rect.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * 200f);
            }

            // The pill appears with the first box and leaves when a new run resets to 0.
            int owned = inventory.CurrentRunGiftBoxes;
            bool show = owned > 0;
            if (show && !giftShown)
                giftShowStart = now;
            giftShown = show;
            float slide = show ? OutBack(Mathf.Clamp01((now - giftShowStart) / 0.38f)) : 0f;
            if (giftParts != null)
                for (int i = 0; i < giftParts.Length; i++)
                {
                    giftParts[i].gameObject.SetActive(show);
                    giftParts[i].anchoredPosition = giftBase[i] + new Vector2((1f - slide) * SlideDistance, 0f);
                }

            // The count only includes boxes that have landed.
            if (giftLabel != null && show)
            {
                giftLabel.text = Mathf.Max(0, owned - flights.Count).ToString();
                float pt = (now - giftPunchStart) / 0.25f;
                float punch = pt < 1f ? 1f + Mathf.Sin(pt * Mathf.PI) * 0.35f : 1f;
                giftLabel.rectTransform.localScale = Vector3.one * punch;
                if (giftIcon != null)
                    giftIcon.localScale = Vector3.one * punch;
            }

            if (wallet != null && coinIcon != null)
            {
                int coins = wallet.CurrentRunCoins;
                if (coins > lastCoins)
                    coinPunchStart = now;
                lastCoins = coins;
                float ct = (now - coinPunchStart) / 0.18f;
                coinIcon.localScale = Vector3.one * (ct < 1f ? 1f + Mathf.Sin(ct * Mathf.PI) * 0.25f : 1f);
            }
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            float u = t - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }

        private void Play(AudioClip authored, string synth, float volume, float pitch)
        {
            if (!GamePreferences.SoundEnabled || audioSource == null)
                return;
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(authored != null ? authored : SfxSynth.Get(synth),
                volume * (library != null ? library.sfxVolume : 0.8f));
        }
    }
}
