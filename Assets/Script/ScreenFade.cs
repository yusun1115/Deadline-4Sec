using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Deadline4Sec
{
    // A black fade over everything that survives a scene load (2026-11-05):
    // fades out, runs the action (usually loading a scene), then fades the new
    // scene in and removes itself. Real-time, so it works with a paused game.
    public sealed class ScreenFade : MonoBehaviour
    {
        private Image cover;
        private bool loaded;

        public static bool Running { get; private set; }

        public static void FadeOutThen(float outSeconds, float inSeconds, Action action)
        {
            if (Running)
                return;
            GameObject go = new GameObject("ScreenFade", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(go);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            GameObject image = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(go.transform, false);
            RectTransform rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            ScreenFade fade = go.AddComponent<ScreenFade>();
            fade.cover = image.GetComponent<Image>();
            fade.cover.color = new Color(0f, 0f, 0f, 0f);
            fade.cover.raycastTarget = true; // no taps while it runs
            Running = true;
            fade.StartCoroutine(fade.Run(outSeconds, inSeconds, action));
        }

        private IEnumerator Run(float outSeconds, float inSeconds, Action action)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            yield return Animate(0f, 1f, outSeconds);
            action?.Invoke();
            // A scene load happens at the end of the frame; wait for it (or give up).
            float until = Time.realtimeSinceStartup + 3f;
            while (!loaded && Time.realtimeSinceStartup < until)
                yield return null;
            yield return null;
            yield return Animate(1f, 0f, inSeconds);
            Destroy(gameObject);
        }

        private IEnumerator Animate(float from, float to, float seconds)
        {
            float start = Time.realtimeSinceStartup;
            float t = 0f;
            while (t < 1f)
            {
                t = seconds > 0f ? Mathf.Clamp01((Time.realtimeSinceStartup - start) / seconds) : 1f;
                cover.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => loaded = true;

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Running = false;
        }
    }
}
