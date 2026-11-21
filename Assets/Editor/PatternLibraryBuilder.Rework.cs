using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Obstacle Pattern Rework (2026-11-21, Obstacle Pattern Rework.md), from the
    // first-time-player playtest:
    //  - Train Wagon patterns (H, K, O) leave every pool (see PatternLibraryBuilder.cs).
    //  - Nothing hides behind a Slide Obstacle: its beam (1.3–3.8 m) covers
    //    anything low behind it in the same lane until the runner is under it.
    //    So after a slide, the next threat in that lane is at least
    //    SlideClearance away (about a second at top speed), or in another lane.
    //    B_JumpSlide (jump, slide, jump 6 m apart) keeps jump → slide only,
    //    G_Hurdles splits its rows across lanes, Q_ObstacleRush spaces them.
    //  - Lane Blockers from the first seconds: three easy patterns (one blocker,
    //    a centre blocker, two blockers) and two mid-run combinations.
    //  - Trains: more of them, early, and ridden: ramp → roof → (the next car's
    //    ramp bridges the gap: falling short lands on it, no hidden drop) →
    //    roof → ground. Railway only.
    // Menu: Deadline 4 Sec/Course/Apply Obstacle Pattern Rework (touches no scene
    // except the open EndlessRun's fallback list, and only when it is saved).
    public static partial class PatternLibraryBuilder
    {
        // Distance kept clear behind a slide beam in its lane (m).
        public const float SlideClearance = 14f;

        private static Spec RoofJump(this Spec s, int lane, float z) =>
            s.Add(jump, $"Roof Jump / Near Miss {LaneName(lane)}", new Vector3(lane * Lane, TrainRoofY + 0.38f, z));

        // Row 1 splits the lanes (jump | free | slide), row 2 is the middle
        // alone: a choice, then one clear action, nothing behind a beam.
        private static Spec HurdlesSpec() =>
            P("Pattern_G_Hurdles", 34, CoursePattern.DifficultyLevel.Easy, CoursePattern.PatternCategory.Obstacle)
                .Jump(-1, 6).Slide(1, 6)
                .Jump(0, 19)
                .Enemy(-1, 31)
                .Coins(0, 1f, 2f, 5, 1.2f).Coins(0, 1.9f, 18.4f, 2).Power("SoulMagnet", 1, 14);

        // Jump, slide, then the lane blocker a full SlideClearance behind the
        // beam; a Soul to finish on, two lanes from where the coins lead.
        private static Spec ObstacleRushSpec() =>
            P("Pattern_Q_ObstacleRush", 44, CoursePattern.DifficultyLevel.Hard, CoursePattern.PatternCategory.Obstacle)
                .Jump(0, 4).Slide(0, 13).Block(0, 29).Enemy(-1, 40)
                .Coins(0, 1.9f, 3.4f, 2).Coins(0, 0.7f, 12, 3, 1f).Coins(1, 1f, 30, 4).Power("ReaperRush", -1, 20);

        // The jump used to stand 10 m behind the beam (hidden until under it).
        private static Spec GauntletSpec() =>
            P("Pattern_N_Gauntlet", 42, CoursePattern.DifficultyLevel.Hard, CoursePattern.PatternCategory.Mixed)
                .Set("Arch", 3).Set("Arch", 17).Set("Arch", 31)
                .Enemy(0, 4).Slide(0, 10).Enemy(1, 15).Jump(0, 24).Block(0, 36)
                .Coins(0, 0.7f, 9, 3, 1f).Coins(0, 1.9f, 23.4f, 2).Power("ComboSeal", -1, 28).Gift(1, 39);

        private static List<Spec> LaneBlockerSpecs()
        {
            var E = CoursePattern.DifficultyLevel.Easy;
            var M = CoursePattern.DifficultyLevel.Medium;
            var obstacle = CoursePattern.PatternCategory.Obstacle;
            var mixed = CoursePattern.PatternCategory.Mixed;
            return new List<Spec>
            {
                // First meeting: one blocker, two lanes wide open, Souls down the middle.
                P("Pattern_LaneBlocker_Single_Left", 26, E, obstacle)
                    .Block(-1, 10).Enemy(1, 20)
                    .Coins(0, 1f, 5, 5, 1.6f),
                // Blocker in the middle: left or right, both free.
                P("Pattern_LaneBlocker_Single_Center", 26, E, obstacle)
                    .Block(0, 10).Enemy(-1, 21)
                    .Coins(-1, 1f, 6, 3).Coins(1, 1f, 6, 3),
                // One way through, seen from far away (nothing else on screen).
                P("Pattern_LaneBlocker_Double", 28, E, obstacle)
                    .Block(-1, 11).Block(1, 11).Enemy(-1, 23)
                    .Coins(0, 1f, 4, 6, 1.4f).Gift(0, 17),
                // After the first seconds: blocker | jump | free, the jump a step
                // behind (dodging the blocker into the middle leaves time to jump).
                P("Pattern_LaneBlocker_Jump", 32, M, mixed)
                    .Block(-1, 10).Jump(0, 15).Enemy(-1, 27)
                    .Coins(1, 1f, 6, 5).Coins(0, 1.9f, 14.4f, 2),
                // Blocker in the middle with a Soul behind the right side.
                P("Pattern_LaneBlocker_Enemy", 30, M, mixed)
                    .Block(0, 9).Enemy(1, 14).Enemy(0, 25)
                    .Coins(-1, 1f, 5, 4).Power("FreezeClock", -1, 16),
            };
        }

        private static List<Spec> TrainRampSpecs()
        {
            var E = CoursePattern.DifficultyLevel.Easy;
            var M = CoursePattern.DifficultyLevel.Medium;
            var H = CoursePattern.DifficultyLevel.Hard;
            var train = CoursePattern.PatternCategory.Train;
            float roof = TrainRoofY;
            float roofCoin = roof + 1f;
            // Cars: front at z, roof to z + 14. The next car's front at
            // z + 14 + RampLength puts its ramp right where the roof ends.
            // A ground Soul only 12 m+ past the last roof: coming down (often
            // high after an air kill) is not landing next to it.
            const float Next = TrainLength + RampLength;
            return new List<Spec>
            {
                // Introduction: ramp → roof → ground. The side lanes stay open.
                P("Pattern_TrainRamp_Simple", 46, E, train)
                    .Enemy(-1, 7).RampTrain(0, 14).Air(0, roof + 2.4f, 21).Enemy(1, 40)
                    .RampCoins(0, 14).Coins(0, roofCoin, 15, 4, 1.6f).Gift(0, 25, roofCoin),
                // Two cars: the second ramp bridges the gap after the first roof.
                P("Pattern_TrainRamp_DoubleTrain", 66, M, train)
                    .Enemy(-1, 6).RampTrain(0, 14).Air(0, roof + 2.4f, 21)
                    .RampTrain(0, 14 + Next).Air(0, roof + 2.4f, 14 + Next + 7.5f).Enemy(1, 62)
                    .RampCoins(0, 14).Coins(0, roofCoin, 15, 3, 1.6f)
                    .RampCoins(0, 14 + Next).Coins(0, roofCoin, 14 + Next + 1f, 3, 1.6f),
                // One action on the roof: a hurdle on the first car (landing on
                // the next ramp), a Soul above the second.
                P("Pattern_TrainRoof_Jump", 66, M, train)
                    .Enemy(-1, 6).RampTrain(0, 14).RoofJump(0, 22)
                    .RampTrain(0, 14 + Next).Air(0, roof + 2.4f, 14 + Next + 7.5f).Enemy(1, 62)
                    // No Souls up this ramp: in line with the hurdle they hid it
                    // from the ramp (checked in the camera).
                    .Coins(0, roof + 1.9f, 21.4f, 2)
                    .RampCoins(0, 14 + Next).Coins(0, roofCoin, 14 + Next + 1f, 3, 1.6f),
                // Three cars for the second half of a run.
                P("Pattern_TrainRamp_Triple", 88, H, train)
                    .Enemy(-1, 6).RampTrain(0, 14).Air(0, roof + 2.4f, 21)
                    .RampTrain(0, 14 + Next).RoofJump(0, 14 + Next + 8f)
                    .RampTrain(0, 14 + Next * 2).Air(0, roof + 2.4f, 14 + Next * 2 + 7f).Enemy(0, 83)
                    .RampCoins(0, 14).RampCoins(0, 14 + Next * 2) // none before the roof hurdle
                    .Coins(0, roofCoin, 14 + Next * 2 + 1f, 3, 1.6f).Power("SoulAmplifier", 1, 50),
            };
        }

        [MenuItem("Deadline 4 Sec/Course/Apply Obstacle Pattern Rework")]
        public static void ApplyObstacleRework()
        {
            LoadPrefabs();
            train = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Train.prefab");
            rampTrain = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Train Ramp.prefab");
            if (train == null || rampTrain == null)
                throw new InvalidOperationException("Build Train Patterns first (Train / Train Ramp prefabs).");

            BuildSetPiecesDecorOnly();
            var specs = new List<Spec> { HurdlesSpec(), ObstacleRushSpec(), GauntletSpec() };
            specs.AddRange(LaneBlockerSpecs());
            specs.AddRange(TrainRampSpecs());
            foreach (Spec spec in specs)
                SavePattern(spec);
            ReworkJumpSlide();
            // A ground threat on the very end, then an air combo 3 m into the
            // next pattern: 2 m more room to read it.
            SetLength("E_MixedRisk", 32f);
            SetLength("I_ArchWalk", 28f);
            // The rests ended on a Soul 3 m before their end: the next pattern's
            // first one (another lane) came 8 m later, too close to reach or read.
            SetLength("V1_Landing", 42f);
            SetLength("V2_SoulWalk", 44f);
            foreach (string name in new[] { "R_TrainCorridor", "S_RampRun", "T_TrainWeave" })
                SetCategory(name, CoursePattern.PatternCategory.Train);

            SetPool("Area_SoulRailway", RailwayPatterns);
            SetPool("Area_BrokenCathedral", CathedralPatterns);
            SetPool("Area_ClockworkAbyss", ClockworkPatterns);
            ApplyPatternIntensities();
            AssetDatabase.SaveAssets();
            RemoveWagonPatternsFromOpenScene();
            Debug.Log("Obstacle pattern rework applied: " + specs.Count + " patterns built, B_JumpSlide reworked.");
        }

        // Jump → slide → jump 6 m apart, all in the centre lane: the beam hid the
        // second jump. Now jump, a recovery, the slide; nothing behind the beam.
        private static void ReworkJumpSlide()
        {
            const string path = PatternFolder + "Pattern_B_JumpSlide.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.transform)
                {
                    Vector3 p = child.localPosition;
                    switch (child.name)
                    {
                        case "01 Jump / Near Miss": p.z = 5f; break;
                        case "02 Slide / Near Miss": p.z = 17f; break;
                        case "Coin_01": p.z = 5f; break;
                        case "Coin_02": p.z = 6f; break;
                        case "Coin_03": p.z = 16f; break;
                        case "Coin_04": p.z = 18f; break;
                    }
                    child.localPosition = p;
                }
                // (Not Transform.Find: the '/' in the name reads as a path.)
                foreach (Transform child in root.transform)
                    if (child.name == "03 Jump / Near Miss")
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                        break;
                    }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The arch / clock gate set pieces, from their saved prefabs (no rebuild).
        private static void BuildSetPiecesDecorOnly()
        {
            Decor["ClockGate"] = AssetDatabase.LoadAssetAtPath<GameObject>(DecorFolder + "Decor_ClockGate.prefab");
            Decor["Arch"] = AssetDatabase.LoadAssetAtPath<GameObject>(DecorFolder + "Decor_GothicArch.prefab");
            if (Decor["Arch"] == null)
                throw new InvalidOperationException("Missing Decor_GothicArch (Build Extended Pattern Library first).");
        }

        private static void SetLength(string name, float length)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatternFolder + "Pattern_" + name + ".prefab");
            if (prefab == null)
                return;
            SerializedObject data = new SerializedObject(prefab.GetComponent<CoursePattern>());
            data.FindProperty("patternLength").floatValue = length;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
        }

        private static void SetCategory(string name, CoursePattern.PatternCategory category)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatternFolder + "Pattern_" + name + ".prefab");
            if (prefab == null)
                return;
            SerializedObject data = new SerializedObject(prefab.GetComponent<CoursePattern>());
            data.FindProperty("category").enumValueIndex = (int)category;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
        }

        private static void SetPool(string areaName, string[] names)
        {
            AreaDefinition area = AssetDatabase.LoadAssetAtPath<AreaDefinition>(AreaFolder + areaName + ".asset");
            if (area == null)
                throw new InvalidOperationException("Missing area " + areaName + " (Build Areas first).");
            area.patternPool = Patterns(names);
            EditorUtility.SetDirty(area);
        }

        // The scene's own list is the fallback library (no area catalog): the
        // wagon patterns leave it too, the new ones join. Only a saved, open
        // EndlessRun is touched (never someone's unsaved work).
        private static void RemoveWagonPatternsFromOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/EndlessRun.unity" || scene.isDirty)
            {
                Debug.LogWarning("Open the saved EndlessRun scene to update its fallback pattern list.");
                return;
            }
            PatternSpawner spawner = UnityEngine.Object.FindFirstObjectByType<PatternSpawner>();
            if (spawner == null)
                return;
            var removed = new HashSet<string> { "Pattern_H_TrainHop", "Pattern_K_TrainRun", "Pattern_O_TrainCombo" };
            SerializedObject data = new SerializedObject(spawner);
            SerializedProperty list = data.FindProperty("patternPrefabs");
            var keep = new List<UnityEngine.Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                UnityEngine.Object entry = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (entry != null && !removed.Contains(entry.name))
                    keep.Add(entry);
            }
            foreach (CoursePattern added in Patterns(RailwayPatterns))
                if (!keep.Contains(added))
                    keep.Add(added);
            list.arraySize = keep.Count;
            for (int i = 0; i < keep.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
