using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // 2026-11-09, reported in the Windows build: a build starts in Loading.unity
    // and the game scene loads additively. Objects the game made while starting
    // (Awake, Start, sceneLoaded) landed in the Loading scene and died with it:
    // every kill threw in EnemyDeathFx and the tutorial never heard of the kill.
    // The editor starts directly in the game scene, so it never happened there.
    public sealed class LoadingSceneAcceptanceTests
    {
        [UnityTest]
        public IEnumerator WhatTheGameCreatesWhileStartingOutlivesTheLoadingScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Loading.unity");
            yield return new EnterPlayMode();
            float giveUpAt = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < giveUpAt &&
                   (SceneManager.sceneCount > 1 || SceneManager.GetActiveScene().name != "EndlessRun"))
                yield return null;
            Assert.AreEqual(1, SceneManager.sceneCount, "The Loading scene was unloaded.");
            Assert.AreEqual("EndlessRun", SceneManager.GetActiveScene().name);
            yield return null;

            foreach (string name in new[] { "Enemy Death Fx", "Combat Particles", "Combat Feedback Pool" })
            {
                GameObject made = GameObject.Find(name);
                Assert.IsNotNull(made, name + " survived the Loading scene.");
                Assert.AreEqual("EndlessRun", made.scene.name, name);
            }

            // What the player reported: killing an enemy reaches every listener.
            Type playerType = Type.GetType("Deadline4Sec.PlayerController, Assembly-CSharp");
            Component player = (Component)UnityEngine.Object.FindFirstObjectByType(playerType);
            Delegate killed = (Delegate)playerType.GetField("EnemyKilled",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
            Assert.IsNotNull(killed, "Something listens for kills.");
            GameObject enemy = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Enemy.prefab"),
                player.transform.position + new Vector3(2.5f, 0f, 10f), Quaternion.identity);
            yield return null;
            Component enemyComponent = enemy.GetComponent(Type.GetType("Deadline4Sec.Enemy, Assembly-CSharp"));
            foreach (Delegate listener in killed.GetInvocationList())
                listener.DynamicInvoke(enemyComponent, "Lane Attack");
            yield return new ExitPlayMode();
        }
    }
}
