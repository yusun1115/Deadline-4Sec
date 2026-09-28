using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class ObstaclePrefabAcceptanceTests
    {
        private static Type Find(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static object Read(Component component, string name) => component.GetType().GetProperty(name).GetValue(component);
        private static object Field(Component component, string name) => component.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
        private static void Set(Component component, string name, object value) => component.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
        private static void Call(Component component, string name, params object[] args) =>
            component.GetType().GetMethod(name).Invoke(component, args);

        [UnityTest]
        public IEnumerator AllThreeProductionObstaclesHaveFatalFacesAndSafeUnrewardedTops()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            float oldDelta = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = new Vector3(0f, -0.1f, 20f);
                floor.transform.localScale = new Vector3(20f, 0.2f, 100f);
                foreach (string prefabName in new[] { "Jump Obstacle", "Slide Obstacle", "Lane Blocker" })
                foreach (string contact in new[] { "Front", "SideDuringLaneAttack", "SlamOntoTop" })
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Prefab/Obstacle/" + prefabName + ".prefab");
                    GameObject block = UnityEngine.Object.Instantiate(prefab);
                    block.transform.position = new Vector3(0f, prefabName == "Jump Obstacle" ? 0.38f :
                        prefabName == "Slide Obstacle" ? 2.56f : 1.02f, 4f);
                    Physics.SyncTransforms();
                    float top = block.GetComponent<Collider>().bounds.max.y;
                    GameObject clock = new GameObject("Clock");
                    Component timer = clock.AddComponent(Find("GameTimer"));
                    Component run = clock.AddComponent(Find("RunManager"));
                    ((Behaviour)run).enabled = false;
                    GameObject actor = new GameObject("Player");
                    actor.transform.position = contact == "SlamOntoTop" ? new Vector3(0f, top + 8.5f, 4f) :
                        contact == "SideDuringLaneAttack" ? new Vector3(-2.5f, 1.08f, 3.35f) : new Vector3(0f, 1.08f, 0f);
                    CharacterController body = actor.AddComponent<CharacterController>();
                    body.height = 2f;
                    body.radius = 0.5f;
                    body.center = Vector3.zero;
                    Component player = actor.AddComponent(Find("PlayerController"));
                    Set(player, "groundSlamMinHeight", 6.5f);
                    Call(player, "SetRunForwardSpeed", contact == "SlamOntoTop" ? 0f : 12f);
                    Call(timer, "BeginRun");
                    yield return null;
                    // RunManager owns speed; hold Z only to isolate a vertical
                    // top collision, then resume production forward movement.
                    if (contact == "SideDuringLaneAttack") Call(player, "RequestMoveRight");
                    if (contact == "SlamOntoTop") Call(player, "RequestSlide");
                    int frames = 0;
                    while (!(bool)Read(timer, "IsGameOver") &&
                        (contact == "SlamOntoTop" ? !(bool)Read(player, "IsGrounded") : frames < 40) && frames++ < 120)
                        yield return null;
                    string context = prefabName + " / " + contact + " / " + actor.transform.position;
                    if (contact == "SlamOntoTop")
                    {
                        Assert.IsFalse((bool)Read(timer, "IsGameOver"), context);
                        Assert.IsTrue((bool)Read(player, "IsGrounded"), context);
                        Assert.IsTrue((bool)Field(block.GetComponent(Find("Obstacle")), "physicallyTouched"), context);
                        Assert.IsFalse((bool)Field(player, "hasGroundSlamImpacted"), context + " must not produce a floor shockwave");
                        Assert.IsFalse((bool)Field(player, "isGroundSlamming"), context);
                        Assert.GreaterOrEqual(body.bounds.min.y, top - 0.1f, context);
                        Call(player, "SetRunForwardSpeed", 12f);
                        for (int i = 0; i < 40; i++) yield return null;
                        Assert.IsFalse((bool)Read(timer, "IsGameOver"), context + " must allow forward exit");
                        Assert.Greater(body.bounds.min.z, block.GetComponent<Collider>().bounds.max.z, context);
                    }
                    else Assert.IsTrue((bool)Read(timer, "IsGameOver"), context);
                    Assert.AreEqual(0, Read(run, "CurrentScore"), context);
                    Assert.AreEqual(0, Read(run, "CurrentCombo"), context);
                    UnityEngine.Object.Destroy(actor);
                    UnityEngine.Object.Destroy(clock);
                    UnityEngine.Object.Destroy(block);
                    yield return null;
                }
            }
            finally { Time.captureDeltaTime = oldDelta; }
            yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
