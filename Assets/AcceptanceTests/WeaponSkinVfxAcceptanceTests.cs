using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Weapon Skin Unique VFX (2026-11-21): every skin has its own idle, swing,
    // trail and hit effects from its catalog entry; changing skins leaves no
    // trace of the previous one; the NEON REAPER pink swing never shows with
    // another skin (it used to: the swing colour was hard-coded); a reloaded
    // scene (Retry) brings the equipped skin's effects back.
    public sealed class WeaponSkinVfxAcceptanceTests
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] Styles = { "NeonReaper", "Starlight", "FrostBite", "BloodMoon", "CyberDeath", "Eclipse" };
        private static readonly Color NeonPink = new Color(1f, 0.22f, 0.55f);

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static Component Get(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(T(name));
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static object Read(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static Array Skins() => (Array)T("GameExtrasCatalog").GetProperty("Skins")
            .GetValue(Resources.Load("GameExtras/GameExtrasCatalog", T("GameExtrasCatalog")));

        [Test]
        public void EachSkinHasItsOwnEffectStyleAndColours()
        {
            Array skins = Skins();
            var colours = new HashSet<Color>();
            for (int i = 0; i < skins.Length; i++)
            {
                object vfx = Field(skins.GetValue(i), "vfx");
                Assert.IsNotNull(vfx);
                Assert.AreEqual(Styles[i], Field(vfx, "style").ToString(), "Style of skin " + i);
                colours.Add((Color)Field(vfx, "primaryColor"));
                Assert.That((float)Field(vfx, "attackDuration"), Is.InRange(0.1f, 0.6f));
            }
            Assert.AreEqual(skins.Length, colours.Count, "Every skin its own colour.");
            // Skin models carry the anchors the effects use.
            for (int i = 1; i < skins.Length; i++)
            {
                var prefab = (GameObject)Field(skins.GetValue(i), "weaponPrefab");
                Assert.IsNotNull(prefab.transform.Find("VFX_BladeEnd"), prefab.name + " blade tip anchor");
                Assert.IsNotNull(prefab.transform.Find("VFX_IdleAnchor"), prefab.name + " idle anchor");
            }
        }

        [UnityTest]
        public IEnumerator EffectsFollowTheEquippedSkinAndLeaveNothingBehind()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            // Read in play mode: entering it reloads the domain and drops earlier locals.
            int saved = PlayerPrefs.GetInt("EquippedWeaponSkin", 0);
            int tutorial = PlayerPrefs.GetInt("Deadline4Sec.TutorialCompleted", -1);
            yield return null;
            try
            {
                Component inventory = Get("RunInventory");
                MethodInfo apply = inventory.GetType().GetMethod("ApplySkinVisual", Hidden);
                Component player = Get("PlayerController");
                Component vfx = Get("WeaponVfxController");
                Assert.IsNotNull(vfx, "The weapon effects controller is on the weapon object.");

                // The order of the request: Neon → Cyber → Frost → Eclipse → Starlight → Blood → Neon.
                foreach (int index in new[] { 0, 4, 2, 5, 1, 3, 0 })
                {
                    string style = Styles[index];
                    PlayerPrefs.SetInt("EquippedWeaponSkin", index);
                    apply.Invoke(inventory, null);
                    yield return null;
                    yield return null;
                    Assert.AreEqual(style, Read(vfx, "Style").ToString());
                    Assert.AreEqual(style, Read(vfx, "IdleStyle").ToString());

                    // Exactly one idle effect, the skin's own, and nothing else glowing on the weapon.
                    var idle = player.GetComponentsInChildren(T("WeaponSkinFx"), false);
                    Assert.AreEqual(1, idle.Length, style + ": one idle effect");
                    Assert.AreEqual(style, Read(idle[0], "Kind").ToString(), style + ": its own idle effect");
                    foreach (ParticleSystem system in player.GetComponentsInChildren<ParticleSystem>(false))
                        if (system.isEmitting)
                            Assert.IsTrue(system.transform.IsChildOf(idle[0].transform), style + ": stray effect " + system.name);
                    Assert.AreEqual(index == 0 ? 1 : 0, player.GetComponentsInChildren<Transform>(false).Count(t => t.name == "Skin Idle VFX"),
                        style + ": the built-in scythe's idle object only with the default");

                    // One trail, on the hand, with the skin's own look.
                    TrailRenderer[] trails = player.GetComponentsInChildren<TrailRenderer>(true).Where(t => t.name == "Weapon Trail").ToArray();
                    Assert.AreEqual(1, trails.Length, style + ": one weapon trail");
                    Assert.AreSame(Read(vfx, "Trail"), trails[0]);

                    // A swing and a kill: the skin's own, never the NEON pink by itself.
                    vfx.GetType().GetMethod("PlaySwing").Invoke(vfx, new object[] { player.transform.position, Quaternion.identity, false });
                    vfx.GetType().GetMethod("PlayHit").Invoke(vfx, new object[] { player.transform.position + Vector3.forward * 3f, Quaternion.identity, false });
                    Assert.AreEqual(style, Read(vfx, "LastSwingStyle").ToString());
                    Assert.AreEqual(style, Read(vfx, "LastHitStyle").ToString());
                    Color swing = (Color)Read(vfx, "LastSwingColor");
                    if (index != 0)
                        Assert.AreNotEqual(NeonPink, swing, style + ": not the default pink");
                    else
                        Assert.AreEqual(NeonPink, swing);
                }

                // The real lane attack and kills go through the skin (the bug: the pink default).
                PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
                PlayerPrefs.SetInt("EquippedWeaponSkin", 4);
                apply.Invoke(inventory, null);
                Component flow = Get("GameFlowManager");
                flow.GetType().GetMethod("StartGame").Invoke(flow, null);
                float giveUp = Time.realtimeSinceStartup + 10f;
                while (Read(flow, "State").ToString() != "Playing" && Time.realtimeSinceStartup < giveUp)
                    yield return null;
                Component particles = Get("CombatParticles");
                int swings = (int)Read(vfx, "SwingCount");
                Type action = T("PlayerController").GetNestedType("PlayerAction");
                particles.GetType().GetMethod("OnPlayerAction", Hidden).Invoke(particles, new[] { Enum.Parse(action, "Left") });
                Assert.AreEqual(swings + 1, (int)Read(vfx, "SwingCount"), "The lane attack plays the skin's swing.");
                Assert.AreEqual("CyberDeath", Read(vfx, "LastSwingStyle").ToString());
                foreach (string attack in new[] { "Side Lane Attack", "Homing Dash Attack" })
                {
                    vfx.GetType().GetProperty("LastHitStyle").SetValue(vfx, Enum.Parse(T("WeaponSkinFx").GetNestedType("Effect"), "None"));
                    particles.GetType().GetMethod("PlayKill").Invoke(particles, new object[] { player.transform.position + Vector3.forward * 4f, attack });
                    Assert.AreEqual("CyberDeath", Read(vfx, "LastHitStyle").ToString(), attack + " uses the skin's hit effect.");
                }
                // The trail stays lit 0.22 s after a swing; a batch frame can take longer
                // than that, so keep swinging until a frame shows it.
                bool lit = false;
                float until = Time.realtimeSinceStartup + 2f;
                while (!lit && Time.realtimeSinceStartup < until)
                {
                    particles.GetType().GetMethod("OnPlayerAction", Hidden).Invoke(particles, new[] { Enum.Parse(action, "Right") });
                    yield return null;
                    lit = (bool)Read(vfx, "TrailEmitting");
                }
                Assert.IsTrue(lit, "The swing lights the skin's trail.");

                // Retry: a reloaded scene shows the equipped skin's effects again.
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                yield return null;
                yield return null;
                yield return null;
                Component again = Get("WeaponVfxController");
                Assert.IsNotNull(again);
                Assert.AreNotSame(vfx, again);
                Assert.AreEqual("CyberDeath", Read(again, "Style").ToString(), "After a reload: the equipped skin.");
                Assert.AreEqual("CyberDeath", Read(again, "IdleStyle").ToString());
            }
            finally
            {
                PlayerPrefs.SetInt("EquippedWeaponSkin", saved);
                if (tutorial < 0)
                    PlayerPrefs.DeleteKey("Deadline4Sec.TutorialCompleted");
                else
                    PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", tutorial);
                PlayerPrefs.Save();
            }
            yield return new ExitPlayMode();
        }
    }
}
