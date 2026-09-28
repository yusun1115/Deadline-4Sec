using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class SceneFlowAcceptanceTests
    {
        private static Type Find(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        [UnityTest]
        public IEnumerator ConfirmedKillFeedbackHasABoundedPoolAndStopsWithTheRun()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            float previousDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                Component flow = Get("GameFlowManager");
                Component player = Get("PlayerController");
                Component spawner = Get("PatternSpawner");
                spawner.GetType().GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(spawner, new System.Random(12345));
                SetFloat(flow, "introDuration", 0.1f);
                flow.GetType().GetMethod("StartGame").Invoke(flow, null);
                int frames = 0;
                while (State(flow) != "Playing" && frames++ < 1000)
                    yield return null;
                Assert.Less(frames, 1000);
                Component firstEnemy = null;
                foreach (Component enemy in UnityEngine.Object.FindObjectsByType(Find("Enemy"), FindObjectsSortMode.None))
                    if (firstEnemy == null || enemy.transform.position.z < firstEnemy.transform.position.z)
                        firstEnemy = enemy;
                Assert.IsNotNull(firstEnemy);
                while (firstEnemy.transform.position.z - player.transform.position.z > 2.6f && frames++ < 1000)
                    yield return null;
                player.GetType().GetMethod("RequestMoveRight").Invoke(player, null);
                yield return null;
                Assert.AreEqual(100, Get("RunManager").GetType().GetProperty("CurrentScore").GetValue(Get("RunManager")),
                    "Feedback must be emitted by an actual public-input kill.");
                Component visuals = Get("CombatVisualFeedback");
                Assert.Greater((int)visuals.GetType().GetProperty("ActiveEffectCount").GetValue(visuals), 0);
                CaptureIntroFrame(Camera.main, "feedback-lane-kill");
                Component cameraFeedback = Get("CameraFeedbackController");
                cameraFeedback.GetType().GetMethod("StopAllFeedback").Invoke(cameraFeedback, null);
                // Pool expiry uses real unscaled time. Stop accelerated simulation
                // here so the unrelated four-second timer does not expire first.
                Time.captureDeltaTime = 0f;
                ((Behaviour)player).enabled = false;
                Vector3 point = player.GetComponent<CharacterController>().bounds.center + Vector3.forward * 2f;
                for (int i = 0; i < 50; i++)
                    visuals.GetType().GetMethod("PlayKill").Invoke(visuals, new object[] { point, false, false });
                int capacity = (int)visuals.GetType().GetProperty("Capacity").GetValue(visuals);
                Assert.AreEqual(capacity, (int)visuals.GetType().GetProperty("ActiveEffectCount").GetValue(visuals));
                GameObject pool = GameObject.Find("Combat Feedback Pool");
                Assert.AreEqual(capacity, pool.GetComponentsInChildren<LineRenderer>().Length);
                Assert.AreEqual(0, pool.GetComponentsInChildren<Collider>().Length,
                    "Visual effects must not change collision geometry.");
                visuals.GetType().GetMethod("Clear").Invoke(visuals, null);
                visuals.GetType().GetMethod("PlaySlam").Invoke(visuals, new object[] {
                    new Vector3(point.x, 0.14f, point.z), 4f });
                yield return AdvanceSeconds(0.1f);
                // Refresh the display sample after the first shader/capture stall.
                // Physics acceptance remains covered by the dedicated Slam tests.
                visuals.GetType().GetMethod("Clear").Invoke(visuals, null);
                visuals.GetType().GetMethod("PlaySlam").Invoke(visuals, new object[] {
                    new Vector3(point.x, 0.14f, point.z + 2f), 4f });
                CaptureIntroFrame(Camera.main, "feedback-slam-ring");
                float effectsExpireAt = Time.unscaledTime + 0.5f;
                int expiryFrames = 0;
                while (Time.unscaledTime < effectsExpireAt && expiryFrames++ < 10000)
                {
                    System.Threading.Thread.Sleep(1);
                    yield return null;
                }
                Assert.Less(expiryFrames, 10000);
                Assert.AreEqual(0, (int)visuals.GetType().GetProperty("ActiveEffectCount").GetValue(visuals),
                    "Completed effects must return to the existing pool.");
                Assert.IsTrue((bool)Get("GameTimer").GetType().GetProperty("IsRunning").GetValue(Get("GameTimer")));
                visuals.GetType().GetMethod("PlayHoming").Invoke(visuals, new object[] { point, point + Vector3.forward * 3f });
                Get("GameTimer").GetType().GetMethod("TriggerGameOver").Invoke(Get("GameTimer"), null);
                Assert.AreEqual(0, (int)visuals.GetType().GetProperty("ActiveEffectCount").GetValue(visuals));
                Assert.AreEqual(1f, Time.timeScale);
            }
            finally
            {
                Time.captureDeltaTime = previousDelta;
            }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator TitleStartWaitsForGoBeforeRunning()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();

            Component flow = (Component)UnityEngine.Object.FindFirstObjectByType(Find("GameFlowManager"));
            Component timer = (Component)UnityEngine.Object.FindFirstObjectByType(Find("GameTimer"));
            Component player = (Component)UnityEngine.Object.FindFirstObjectByType(Find("PlayerController"));
            Component spawner = (Component)UnityEngine.Object.FindFirstObjectByType(Find("PatternSpawner"));
            Assert.IsNotNull(flow);
            Assert.AreEqual("Title", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            Assert.IsFalse((bool)timer.GetType().GetProperty("IsRunning").GetValue(timer));
            Assert.IsFalse(((Behaviour)player).enabled);
            Assert.AreEqual(0, spawner.transform.childCount);
            AssertAllTextUsesGameFont();
            CaptureIntroFrame(Camera.main, "font-title");

            Type preferences = Find("GamePreferences");
            PropertyInfo sound = preferences.GetProperty("SoundEnabled");
            PropertyInfo vibration = preferences.GetProperty("VibrationEnabled");
            bool oldSound = (bool)sound.GetValue(null);
            bool oldVibration = (bool)vibration.GetValue(null);
            flow.GetType().GetMethod("OpenSettings").Invoke(flow, null);
            Assert.AreEqual("Settings", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            CaptureIntroFrame(Camera.main, "font-settings");
            try
            {
                GameObject.Find("Canvas/SettingsPanel/SoundButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                GameObject.Find("Canvas/SettingsPanel/VibrationButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(!oldSound, (bool)sound.GetValue(null));
                Assert.AreEqual(!oldVibration, (bool)vibration.GetValue(null));
            }
            finally
            {
                sound.SetValue(null, oldSound);
                vibration.SetValue(null, oldVibration);
            }
            flow.GetType().GetMethod("CloseSettings").Invoke(flow, null);
            Assert.AreEqual("Title", flow.GetType().GetProperty("State").GetValue(flow).ToString());

            Component run = Get("RunManager");
            Camera camera = Camera.main;
            Vector3 gameplayCameraPosition = camera.transform.localPosition;
            Quaternion gameplayCameraRotation = camera.transform.localRotation;
            float startZ = player.transform.position.z;
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            Assert.AreEqual("Ready", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            Assert.IsFalse((bool)timer.GetType().GetProperty("IsRunning").GetValue(timer));
            Assert.AreEqual("Ready?", IntroText(flow));
            Assert.Greater(spawner.transform.childCount, 0, "Course must exist before Go.");
            int preparedChildren = spawner.transform.childCount;
            Assert.Greater(Vector3.Distance(camera.transform.localPosition, gameplayCameraPosition), 2f);
            CaptureIntroFrame(camera, "intro-ready-side");
            yield return AdvanceSeconds(0.5f);
            Assert.AreEqual("Ready", State(flow));
            Assert.Greater(player.transform.position.z, startZ);
            Assert.IsFalse(((Behaviour)player).enabled, "Gameplay input remains disabled during cinematic travel.");
            Assert.AreEqual(0f, run.GetType().GetProperty("Distance").GetValue(run));
            Assert.AreEqual(0f, run.GetType().GetProperty("CurrentRunTime").GetValue(run));
            Assert.IsFalse(TitlePanel(flow).activeSelf);
            CaptureIntroFrame(camera, "intro-camera-transition");
            int frames = 0;
            bool capturedApproach = false;
            while (State(flow) == "Ready" && frames++ < 10000)
            {
                Assert.AreEqual("Ready?", IntroText(flow));
                Assert.IsFalse(TitlePanel(flow).activeSelf);
                Assert.AreEqual(preparedChildren, spawner.transform.childCount);
                Component feedback = Get("CameraFeedbackController");
                float cameraElapsed = (float)feedback.GetType().GetField("introElapsed",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(feedback);
                if (!capturedApproach && cameraElapsed >= 1.8f)
                {
                    CaptureIntroFrame(camera, "intro-ready-approach");
                    capturedApproach = true;
                }
                yield return null;
            }
            Assert.Less(frames, 10000);
            Assert.AreEqual("Playing", flow.GetType().GetProperty("State").GetValue(flow).ToString());
            Assert.AreEqual("Go!", IntroText(flow));
            Assert.IsTrue((bool)timer.GetType().GetProperty("IsRunning").GetValue(timer));
            Assert.IsTrue(((Behaviour)player).enabled);
            Assert.AreEqual(preparedChildren, spawner.transform.childCount);
            Assert.Less(Vector3.Distance(camera.transform.localPosition, gameplayCameraPosition), 0.001f);
            Assert.Less(Quaternion.Angle(camera.transform.localRotation, gameplayCameraRotation), 0.001f);
            Assert.Less((float)run.GetType().GetProperty("Distance").GetValue(run), 1f);
            Assert.IsFalse(TitlePanel(flow).activeSelf);
            CaptureIntroFrame(camera, "intro-go");
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.Text timerLabel = GameObject.Find("SurvivalTimer").GetComponent<UnityEngine.UI.Text>();
            Assert.Greater(timerLabel.cachedTextGenerator.vertexCount, 0,
                "The survival timer must fit and generate visible glyphs with the game font.");
            Component firstPattern = spawner.GetComponentInChildren(Find("CoursePattern"));
            float speed = (float)run.GetType().GetProperty("BaseForwardSpeed").GetValue(run);
            Assert.GreaterOrEqual(firstPattern.transform.position.z - player.transform.position.z, speed * 1.4f);
            Collider firstThreat = null;
            foreach (Collider body in firstPattern.GetComponentsInChildren<Collider>())
            {
                if (body.GetComponent(Find("Enemy")) == null && body.GetComponent(Find("Obstacle")) == null)
                    continue;
                if (firstThreat == null || body.bounds.min.z < firstThreat.bounds.min.z)
                    firstThreat = body;
            }
            Assert.IsNotNull(firstThreat);
            Assert.Less((firstThreat.bounds.max.z - player.transform.position.z) / speed, 4f);
            Assert.IsTrue(GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),
                firstThreat.bounds), "The first threat must be visible from the gameplay camera.");
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ResultRetryStartsFreshRunIntro()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            Component flow = Get("GameFlowManager");
            Component timer = Get("GameTimer");
            SetFloat(flow, "introDuration", 0.1f);
            SetFloat(flow, "resultDelay", 0.01f);
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            yield return AdvanceSeconds(0.2f);
            Assert.AreEqual("Playing", State(flow));
            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            yield return AdvanceSeconds(0.1f);
            Assert.AreEqual("Result", State(flow));
            AssertAllTextUsesGameFont();
            CaptureIntroFrame(Camera.main, "font-result");
            flow.GetType().GetMethod("Retry").Invoke(flow, null);
            yield return null;
            Component freshFlow = Get("GameFlowManager");
            Component freshTimer = Get("GameTimer");
            Assert.AreEqual("Ready", State(freshFlow));
            Assert.AreEqual("Ready?", IntroText(freshFlow));
            Assert.IsFalse(TitlePanel(freshFlow).activeSelf);
            Assert.IsFalse((bool)freshTimer.GetType().GetProperty("IsRunning").GetValue(freshTimer));
            yield return AdvanceRealtimeSeconds(2.3f);
            Assert.AreEqual("Playing", State(freshFlow));
            Assert.IsTrue((bool)freshTimer.GetType().GetProperty("IsRunning").GetValue(freshTimer));
            Assert.IsFalse((bool)freshTimer.GetType().GetProperty("IsGameOver").GetValue(freshTimer));
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ResultMainMenuReturnsToTitle()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            Component flow = Get("GameFlowManager");
            Component timer = Get("GameTimer");
            SetFloat(flow, "introDuration", 0.1f);
            SetFloat(flow, "resultDelay", 0.01f);
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            yield return AdvanceSeconds(0.2f);
            timer.GetType().GetMethod("TriggerGameOver").Invoke(timer, null);
            yield return AdvanceSeconds(0.1f);
            Assert.AreEqual("Result", State(flow));
            flow.GetType().GetMethod("MainMenu").Invoke(flow, null);
            yield return null;
            Component freshFlow = Get("GameFlowManager");
            Component freshTimer = Get("GameTimer");
            Assert.AreEqual("Title", State(freshFlow));
            Assert.IsFalse((bool)freshTimer.GetType().GetProperty("IsRunning").GetValue(freshTimer));
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Component Get(string name)
        {
            Component component = (Component)UnityEngine.Object.FindFirstObjectByType(Find(name));
            Assert.IsNotNull(component, name);
            return component;
        }

        private static string State(Component flow)
        {
            return flow.GetType().GetProperty("State").GetValue(flow).ToString();
        }

        internal static void AssertAllTextUsesGameFont()
        {
            Type fontType = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro");
            UnityEngine.Object font = Resources.Load("Fonts/GFCRedSpirit-Bold SDF", fontType);
            Assert.IsNotNull(font);
            Font source = (Font)fontType.GetProperty("sourceFontFile").GetValue(font);
            Assert.AreEqual("GFCRedSpirit-Bold", source.name);
            Type textType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            foreach (UnityEngine.Object label in UnityEngine.Object.FindObjectsByType(textType,
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.AreSame(font, textType.GetProperty("font").GetValue(label), label.name);
            foreach (UnityEngine.UI.Text label in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.AreSame(source, label.font, label.name);
        }

        private static GameObject TitlePanel(Component flow)
        {
            return (GameObject)flow.GetType().GetField("titlePanel",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow);
        }

        private static string IntroText(Component flow)
        {
            object label = flow.GetType().GetField("countdownText",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow);
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }

        internal static void CaptureIntroFrame(Camera camera, string name)
        {
            // Optional visual evidence during a graphics-enabled verification run.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            Type pipeline = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipeline, Unity.RenderPipelines.Universal.Runtime");
            Type requestType = pipeline.GetNestedType("SingleCameraRequest");
            object request = Activator.CreateInstance(requestType);
            RenderTexture target = new RenderTexture(480, 800, 24);
            target.Create();
            requestType.GetField("destination").SetValue(request, target);
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldPlane = canvas.planeDistance;
            float oldAspect = camera.aspect;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.5f;
                camera.aspect = 480f / 800f;
                Canvas.ForceUpdateCanvases();
                typeof(UnityEngine.Rendering.RenderPipeline).GetMethod("SubmitRenderRequest",
                    BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(requestType)
                    .Invoke(null, new object[] { camera, request });
                RenderTexture.active = target;
                Texture2D frame = new Texture2D(480, 800, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0f, 0f, 480f, 800f), 0, 0);
                frame.Apply();
                string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
                if (System.IO.Path.GetFileName(projectRoot) == "ValidationProject")
                    projectRoot = System.IO.Path.GetDirectoryName(projectRoot);
                string directory = System.IO.Path.Combine(projectRoot, "Verification");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), frame.EncodeToPNG());
                UnityEngine.Object.Destroy(frame);
            }
            finally
            {
                RenderTexture.active = oldActive;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlane;
                camera.aspect = oldAspect;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        private static void SetFloat(Component target, string name, float value)
        {
            target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        private static IEnumerator AdvanceSeconds(float seconds)
        {
            float end = Time.time + seconds;
            int frames = 0;
            while (Time.time < end && frames++ < 10000)
                yield return null;
            Assert.Less(frames, 10000);
        }

        private static IEnumerator AdvanceRealtimeSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            int frames = 0;
            while (Time.realtimeSinceStartup < end && frames++ < 10000)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.Less(frames, 10000);
        }
    }
}
