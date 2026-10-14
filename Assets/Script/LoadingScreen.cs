using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // Boot loading screen (2026-10-09 request). The bar follows real work:
    //   0–15%   building the synthesized sounds, effect sprites and fonts the
    //           game would otherwise create on its first frame,
    //   15–75%  reading the gameplay scene in the background,
    //   75–95%  activating it (every Awake) underneath this screen,
    //   95–100% its first frame (every Start); then this screen fades out and
    //           unloads itself.
    // The bar only eases between those values and never runs ahead of the work.
    // Tips are edited in the Inspector; one is picked at random each launch.
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private string sceneToLoad = "EndlessRun";
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField, TextArea(1, 3)] private string[] tips =
        {
            "장애물을 아슬아슬하게 피해보세요!",
        };
        [Tooltip("Fastest the bar may move (fill per second); it only eases toward real progress.")]
        [SerializeField, Min(0.1f)] private float barSpeed = 1.8f;

        private const float WarmUpShare = 0.15f;
        private const float LoadedShare = 0.75f;
        private const float ActivatedShare = 0.95f;
        private const float FadeTime = 0.25f;
        private float target;
        private float shown;

        private void Awake()
        {
            GameFont.Apply(loadingText);
            GameFont.Apply(tipText);
            if (tipText != null)
                tipText.text = PickTip();
            if (fill != null)
                fill.fillAmount = 0f;
        }

        private string PickTip()
        {
            var usable = new List<string>();
            if (tips != null)
                foreach (string tip in tips)
                    if (!string.IsNullOrWhiteSpace(tip))
                        usable.Add(tip.Trim());
            return usable.Count > 0 ? usable[UnityEngine.Random.Range(0, usable.Count)] : "";
        }

        private IEnumerator Start()
        {
            yield return null; // show the screen before any work starts

            // Warm-up: one item per frame so the bar moves with the work.
            var steps = new List<Action>
            {
                () => _ = GameFont.Asset,
                () => _ = GameFont.NumberAsset,
                () => _ = SoundLibrary.Instance,
            };
            foreach (string name in SfxSynth.All)
                steps.Add(() => _ = SfxSynth.Get(name));
            foreach (Func<Sprite> sprite in FxSprites.All)
                steps.Add(() => _ = sprite());
            for (int i = 0; i < steps.Count; i++)
            {
                steps[i]();
                target = WarmUpShare * (i + 1) / steps.Count;
                yield return null;
            }

            // Scene load: Unity reports 0–0.9 while reading the scene and holds at
            // 0.9 until activation is allowed.
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
            if (load == null)
                yield break;
            load.allowSceneActivation = false;
            while (load.progress < 0.9f)
            {
                target = Mathf.Lerp(WarmUpShare, LoadedShare, load.progress / 0.9f);
                yield return null;
            }
            target = LoadedShare;

            // Activation (every Awake in the game scene) is the heaviest part, so
            // the screen stays up over it: the game loads underneath, becomes the
            // active scene, runs its first frame (Start), and only then is revealed.
            load.allowSceneActivation = true;
            while (!load.isDone)
                yield return null;
            Scene game = sceneToLoad.Contains("/") ? SceneManager.GetSceneByPath(sceneToLoad)
                : SceneManager.GetSceneByName(sceneToLoad);
            if (game.IsValid())
                SceneManager.SetActiveScene(game);
            target = ActivatedShare;
            yield return null; // the game scene's Start runs here
            target = 1f;
            while (shown < 0.999f)
                yield return null;

            CanvasGroup group = GetComponent<CanvasGroup>();
            for (float t = 0f; group != null && t < FadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / FadeTime;
                yield return null;
            }
            // Unloading this scene ends this coroutine; free the key art afterwards.
            SceneManager.UnloadSceneAsync(gameObject.scene).completed += _ => Resources.UnloadUnusedAssets();
        }

        private void Update()
        {
            shown = Mathf.MoveTowards(shown, target, barSpeed * Time.unscaledDeltaTime);
            if (fill != null)
                fill.fillAmount = shown;
            if (loadingText != null)
            {
                int dots = 1 + (int)(Time.unscaledTime * 2.5f) % 3;
                loadingText.text = "LOADING" + new string('.', dots) + new string(' ', 3 - dots);
            }
        }
    }
}
