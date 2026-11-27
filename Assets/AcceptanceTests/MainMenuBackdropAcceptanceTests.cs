using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Main menu 3D background (2026-11-27, Main Menu 3D Background Visual
    // Polish.md): behind the Reaper on the title, the rail runs on into the fog
    // (no end in sight), the railway's pillars, mid pieces, clock tower and far
    // city stand behind it, the low fog and souls are there too; visual only (no
    // colliders), and gone once the run has taken the camera.
    public sealed class MainMenuBackdropAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        [UnityTest]
        public IEnumerator TheRailwayGoesOnBehindTheReaperOnlyInTheMenu()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            yield return null;
            Component flow = Get("GameFlowManager");
            Assert.AreEqual("Title", Read(flow, "State").ToString());
            Component scroller = Get("EnvironmentScroller");
            Transform root = (Transform)Read(scroller, "MainMenuBackdrop");
            Assert.IsNotNull(root, "MainMenu_Background_3D is built.");
            Assert.IsTrue((bool)Read(scroller, "MainMenuBackdropShown"));
            Assert.AreEqual(1, (int)Read(scroller, "BackdropCount"), "Not one of the scroller's backdrops.");
            foreach (string group in new[] { "Rail_Extension", "Midground", "ClockTower", "FarBackground" })
                Assert.IsNotNull(root.Find(group), group);

            // Visual only, and light.
            Assert.AreEqual(0, root.GetComponentsInChildren<Collider>(true).Length, "No colliders.");
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.LessOrEqual(renderers.Length, 60);
            int triangles = root.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            Assert.LessOrEqual(triangles, 20000);
            Assert.LessOrEqual(renderers.SelectMany(r => r.sharedMaterials).Distinct().Count(), 6, "Mostly the run's materials.");

            // The rail: Start Floor copies, end to end, on past the fog.
            Bounds start = GameObject.Find("Start Floor").GetComponent<Renderer>().bounds;
            List<Bounds> rail = root.Find("Rail_Extension").GetComponentsInChildren<Renderer>()
                .Select(r => r.bounds).OrderByDescending(b => b.max.z).ToList();
            Assert.Greater(rail.Count, 3);
            float edge = start.min.z;
            foreach (Bounds piece in rail)
            {
                Assert.AreEqual(edge, piece.max.z, 0.01f, "No gap in the rail.");
                Assert.AreEqual(start.size.x, piece.size.x, 0.01f, "The same width.");
                edge = piece.min.z;
            }
            Camera view = Camera.main;
            Assert.Less(Vector3.Dot(view.transform.forward, Vector3.forward), -0.9f, "The title camera looks back down the line.");
            Assert.Greater(view.transform.position.z - edge, RenderSettings.fogEndDistance, "The rail ends beyond the fog: no end in sight.");

            // The clock tower on the vanishing point, its dial above the Reaper's head.
            Transform tower = root.Find("ClockTower/ClockTower");
            Vector3 dial = tower.TransformPoint(new Vector3(0f, 66f, -9.1f));
            Vector3 onScreen = view.WorldToViewportPoint(dial);
            Assert.Greater(onScreen.z, 0f);
            Assert.AreEqual(0.5f, onScreen.x, 0.08f, "Centred on the line.");
            Component player = Get("PlayerController");
            // The hood's top: ~0.7 m over the capsule centre (the Reaper is ~1.7 m tall).
            Vector3 head = view.WorldToViewportPoint(player.transform.position + Vector3.up * 0.7f);
            Assert.Greater(onScreen.y, head.y, "Above the Reaper's head.");
            Assert.Less(onScreen.y, 0.85f, "Under the top of the screen.");
            Material clock = tower.GetComponent<Renderer>().sharedMaterial;
            Assert.Less(clock.GetColor("_Tint").maxColorComponent, 0.7f, "A dark silhouette behind the logo.");
            Assert.LessOrEqual(clock.GetFloat("_Glow"), 1.2f, "Hardly glowing.");
            Transform minute = tower.Find("ClockMinuteHand");
            float before = minute.localEulerAngles.z;
            yield return new WaitForSecondsRealtime(1f);
            float turned = Mathf.Abs(Mathf.DeltaAngle(before, minute.localEulerAngles.z));
            Assert.That(turned, Is.InRange(1f, 12f), "The minute hand creeps round.");

            // Low fog and souls behind.
            Component atmosphere = (Component)Read(scroller, "Atmosphere");
            Assert.IsTrue((bool)Read(atmosphere, "BackFogVisible"), "Low fog behind the start.");
            Assert.That((int)Read(atmosphere, "ParticleCount"), Is.InRange(1, 120), "A few souls rising.");

            // The run takes the camera: the menu scenery goes once the swing is over.
            flow.GetType().GetMethod("StartGame").Invoke(flow, null);
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.IsFalse((bool)Read(scroller, "MainMenuBackdropShown"), "Hidden in the run.");
            Assert.IsFalse((bool)Read(atmosphere, "BackFogVisible"));
            Assert.IsTrue((bool)Read(atmosphere, "LowFogVisible"), "The run's own fog ahead stays.");
            yield return new ExitPlayMode();
        }
    }
}
