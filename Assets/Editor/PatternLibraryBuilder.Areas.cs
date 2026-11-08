using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Areas (AreaBiome,Pacing.md, 2026-11-06): the transition tunnel, each area's
    // floor and obstacle models, and the data in Resources/Areas. Touches no
    // scene: PatternSpawner loads the catalog at run time. Safe to re-run; tuned
    // lengths, spacing and looks on existing area assets are kept.
    public static partial class PatternLibraryBuilder
    {
        private const string AreaFolder = "Assets/Resources/Areas/";
        private const string EnvironmentMaterialFolder = "Assets/Art/Materials/Environment/";

        private const float TunnelLength = 44f;
        private const float TunnelHalfWidth = 4.75f;
        private const float TunnelHeight = 7.6f;

        // Soul Railway keeps everything that exists today except the sections
        // that now belong to other areas.
        private static readonly string[] RailwayPatterns =
        {
            "A_LaneAttack", "B_JumpSlide", "C_AirCombo", "D_Stomp", "E_MixedRisk", "F_Crossfire", "G_Hurdles",
            "H_TrainHop", "I_ArchWalk", "J_ClockGate", "K_TrainRun", "L_AirLadder", "M_StompStairs", "N_Gauntlet",
            "O_TrainCombo", "P_ClockAirStorm", "Q_ObstacleRush", "R_TrainCorridor", "S_RampRun", "T_TrainWeave",
            "V1_Landing", "V2_SoulWalk",
        };

        // No trains in a cathedral; the same beats otherwise, in its own stone.
        private static readonly string[] CathedralPatterns =
        {
            "A_LaneAttack", "B_JumpSlide", "C_AirCombo", "D_Stomp", "E_MixedRisk", "F_Crossfire", "G_Hurdles",
            "I_ArchWalk", "L_AirLadder", "M_StompStairs", "N_Gauntlet", "P_ClockAirStorm", "Q_ObstacleRush",
            "V1_Landing", "V2_SoulWalk",
        };

        private static readonly string[] CathedralSpecials = { "W1_WallRun_Left", "W2_WallRun_Right" };

        // Machinery and air combat: more of the aerial patterns come up here.
        private static readonly string[] ClockworkPatterns =
        {
            "A_LaneAttack", "B_JumpSlide", "C_AirCombo", "D_Stomp", "E_MixedRisk", "F_Crossfire", "G_Hurdles",
            "J_ClockGate", "L_AirLadder", "M_StompStairs", "N_Gauntlet", "P_ClockAirStorm", "Q_ObstacleRush",
            "V1_Landing", "V2_SoulWalk",
        };

        private static readonly string[] ClockworkSpecials =
        {
            "U1_AirRouteEntry", "U5_AirRouteEntry_Weave", "U2_UpperRun_Combat", "U3_UpperRun_Hurdles", "U4_UpperReturn",
        };

        // Pattern Intensity: 1 rest, 2 light, 3 ordinary, 4 high focus, 5 strong risk.
        private static readonly Dictionary<string, int> PatternIntensities = new Dictionary<string, int>
        {
            { "A_LaneAttack", 2 }, { "B_JumpSlide", 2 }, { "C_AirCombo", 3 }, { "D_Stomp", 3 }, { "E_MixedRisk", 5 },
            { "F_Crossfire", 3 }, { "G_Hurdles", 2 }, { "H_TrainHop", 3 }, { "I_ArchWalk", 2 }, { "J_ClockGate", 3 },
            { "K_TrainRun", 3 }, { "L_AirLadder", 4 }, { "M_StompStairs", 4 }, { "N_Gauntlet", 5 }, { "O_TrainCombo", 4 },
            { "P_ClockAirStorm", 5 }, { "Q_ObstacleRush", 4 }, { "R_TrainCorridor", 3 }, { "S_RampRun", 3 }, { "T_TrainWeave", 4 },
            { "V1_Landing", 1 }, { "V2_SoulWalk", 1 }, { "W1_WallRun_Left", 4 }, { "W2_WallRun_Right", 4 },
            { "U1_AirRouteEntry", 3 }, { "U5_AirRouteEntry_Weave", 4 }, { "U2_UpperRun_Combat", 3 }, { "U3_UpperRun_Hurdles", 3 },
            { "U4_UpperReturn", 2 }, { "AreaTransition_Tunnel", 1 },
        };

        private static int IntensityOf(string patternName) =>
            PatternIntensities.TryGetValue(patternName.Replace("Pattern_", ""), out int intensity) ? intensity : 3;

        // Hand-made patterns don't pass through SavePattern.
        private static void ApplyPatternIntensities()
        {
            foreach (KeyValuePair<string, int> entry in PatternIntensities)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatternFolder + "Pattern_" + entry.Key + ".prefab");
                if (prefab == null)
                    continue;
                SerializedObject data = new SerializedObject(prefab.GetComponent<CoursePattern>());
                data.FindProperty("intensity").intValue = entry.Value;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
            }
        }

        private static PacingBlock[] PacingBlocks(PacingBlock signature) => new[]
        {
            signature,
            new PacingBlock("Recovery", 2, 3, 2, 1),
            new PacingBlock("BuildUp", 1, 1, 2, 3, 4),
            new PacingBlock("Flow", 1, 2, 3, 3, 2),
            new PacingBlock("Spike", 1, 2, 4, 5, 2),
        };

        [MenuItem("Deadline 4 Sec/Course/Build Areas")]
        public static void BuildAreas()
        {
            LoadPrefabs();
            System.IO.Directory.CreateDirectory(AreaFolder);

            CoursePattern transition = BuildTransitionPattern();
            BuildRecoveryPatterns();
            Material cathedralFloor = BuildCathedralFloor();
            AreaDefinition railway = LoadArea("Area_SoulRailway", out bool newRailway);
            railway.areaId = AreaType.SoulRailway;
            railway.displayName = "SOUL RAILWAY";
            railway.patternPool = Patterns(RailwayPatterns);
            railway.specialPatternPool = new CoursePattern[0];
            if (railway.pacingBlocks == null || railway.pacingBlocks.Length == 0)
                railway.pacingBlocks = PacingBlocks(new PacingBlock("Steady", 3, 2, 3, 2, 4));
            if (newRailway)
                railway.look = RailwayLook();
            EditorUtility.SetDirty(railway);

            AreaDefinition cathedral = LoadArea("Area_BrokenCathedral", out bool newCathedral);
            cathedral.areaId = AreaType.BrokenCathedral;
            cathedral.displayName = "BROKEN CATHEDRAL";
            cathedral.patternPool = Patterns(CathedralPatterns);
            cathedral.specialPatternPool = Patterns(CathedralSpecials);
            if (cathedral.pacingBlocks == null || cathedral.pacingBlocks.Length == 0)
                cathedral.pacingBlocks = PacingBlocks(new PacingBlock("Contrast", 3, 2, 4, 2, 4));
            cathedral.floorMaterial = cathedralFloor;
            cathedral.decorMaterial = Material("CathedralDecor", new Color(1f, 0.72f, 0.72f), new Color(0.8f, 0.2f, 0.16f), 4f);
            cathedral.obstacleSkins = CathedralSkins();
            if (newCathedral)
                cathedral.look = CathedralLook();
            EditorUtility.SetDirty(cathedral);

            AreaCatalog catalog = AssetDatabase.LoadAssetAtPath<AreaCatalog>(AreaFolder + "AreaCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AreaCatalog>();
                AssetDatabase.CreateAsset(catalog, AreaFolder + "AreaCatalog.asset");
            }
            AreaDefinition clockwork = LoadArea("Area_ClockworkAbyss", out bool newClockwork);
            clockwork.areaId = AreaType.ClockworkAbyss;
            clockwork.displayName = "CLOCKWORK ABYSS";
            clockwork.patternPool = Patterns(ClockworkPatterns);
            clockwork.specialPatternPool = Patterns(ClockworkSpecials);
            if (clockwork.pacingBlocks == null || clockwork.pacingBlocks.Length == 0)
                clockwork.pacingBlocks = PacingBlocks(new PacingBlock("Ascent", 3, 1, 3, 4, 3, 5));
            clockwork.floorMaterial = BuildClockworkFloor();
            clockwork.deckEdgeMaterial = BuildClockworkDeckEdge();
            clockwork.decorMaterial = Material("ClockworkDecor", new Color(1f, 0.78f, 0.42f), new Color(0.9f, 0.5f, 0.1f), 4.5f);
            clockwork.obstacleSkins = ClockworkSkins();
            if (newClockwork)
                clockwork.look = ClockworkLook();
            EditorUtility.SetDirty(clockwork);

            catalog.areas = new[] { railway, cathedral, clockwork };
            catalog.transitionPattern = transition;
            EditorUtility.SetDirty(catalog);
            ApplyPatternIntensities();
            AssetDatabase.SaveAssets();
            Debug.Log("Areas built: Soul Railway, Broken Cathedral, Clockwork Abyss, transition tunnel.");
        }

        private static AreaDefinition LoadArea(string name, out bool created)
        {
            string path = AreaFolder + name + ".asset";
            AreaDefinition area = AssetDatabase.LoadAssetAtPath<AreaDefinition>(path);
            created = area == null;
            if (created)
            {
                area = ScriptableObject.CreateInstance<AreaDefinition>();
                AssetDatabase.CreateAsset(area, path);
            }
            return area;
        }

        private static CoursePattern[] Patterns(string[] names)
        {
            var list = new List<CoursePattern>();
            foreach (string name in names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatternFolder + "Pattern_" + name + ".prefab");
                if (prefab == null)
                    throw new System.InvalidOperationException("Missing pattern " + name);
                list.Add(prefab.GetComponent<CoursePattern>());
            }
            return list.ToArray();
        }

        // ---------- recovery ----------

        // The way back down. Air combo patterns all end in the air and only an air
        // route used to lead back; an area without one would stay up there until
        // its transition (and never reach a wall run, which starts on the ground).
        // These accept any arrival and end on the ground: room to land first (a
        // stomp's bounce stays up for ~2 s, up to 28 m at full speed), then easy
        // enemies, so the Deadline timer is safe.
        private static void BuildRecoveryPatterns()
        {
            // Easy, so a rest is possible from the first seconds of a run.
            var easy = CoursePattern.DifficultyLevel.Easy;
            var ground = CoursePattern.PatternCategory.GroundCombat;
            SavePattern(P("Pattern_V1_Landing", 38f, easy, ground, CoursePattern.TravelState.Any, CoursePattern.TravelState.Ground)
                .Coins(0, 1f, 8f, 8, 1.6f).Enemy(-1, 28f).Enemy(1, 35f));
            SavePattern(P("Pattern_V2_SoulWalk", 40f, easy, ground, CoursePattern.TravelState.Any, CoursePattern.TravelState.Ground)
                .Coins(-1, 1f, 8f, 5, 1.4f).Coins(1, 1f, 17f, 5, 1.4f).Enemy(0, 29f).Enemy(-1, 37f));
        }

        // ---------- transition ----------

        private static CoursePattern BuildTransitionPattern()
        {
            GameObject tunnel = BuildTunnelPiece();
            // Enemies in the side lanes keep the Deadline resets coming through the
            // tunnel; Souls down the middle lead to the portal.
            Spec spec = P("Pattern_AreaTransition_Tunnel", TunnelLength, CoursePattern.DifficultyLevel.Easy,
                    CoursePattern.PatternCategory.AreaTransition, CoursePattern.TravelState.Any, CoursePattern.TravelState.Ground)
                .Add(tunnel, "SetPiece AreaTunnel", Vector3.zero)
                .Coins(0, 1f, 6f, 4, 1.4f).Enemy(-1, 13f)
                .Coins(0, 1f, 19f, 4, 1.4f).Enemy(1, 29f)
                .Coins(0, 1f, 34f, 5, 1.4f);
            return SavePattern(spec).GetComponent<CoursePattern>();
        }

        private static GameObject BuildTunnelPiece()
        {
            Material stone = Material("TunnelStone", new Color(0.55f, 0.5f, 0.6f), new Color(0.5f, 0.12f, 0.3f), 5f);
            string meshPath = ModelFolder + "Decor_AreaTunnel_Mesh.asset";
            string portalPath = ModelFolder + "Decor_AreaPortal_Mesh.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.DeleteAsset(portalPath);
            Mesh tunnelMesh = TunnelMesh();
            Mesh portalMesh = PortalMesh();
            AssetDatabase.CreateAsset(tunnelMesh, meshPath);
            AssetDatabase.CreateAsset(portalMesh, portalPath);

            string portalMaterialPath = ModelFolder + "AreaPortal.mat";
            Material portalMaterial = AssetDatabase.LoadAssetAtPath<Material>(portalMaterialPath);
            if (portalMaterial == null)
            {
                portalMaterial = new Material(Shader.Find("Deadline4Sec/AreaPortal"));
                AssetDatabase.CreateAsset(portalMaterial, portalMaterialPath);
            }
            portalMaterial.shader = Shader.Find("Deadline4Sec/AreaPortal");
            EditorUtility.SetDirty(portalMaterial);

            GameObject root = new GameObject("Decor_AreaTunnel");
            root.AddComponent<MeshFilter>().sharedMesh = tunnelMesh;
            MeshRenderer walls = root.AddComponent<MeshRenderer>();
            walls.sharedMaterial = stone;
            walls.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject portal = new GameObject("Portal");
            portal.transform.SetParent(root.transform, false);
            portal.transform.localPosition = new Vector3(0f, 0f, TunnelLength - 0.4f);
            portal.AddComponent<MeshFilter>().sharedMesh = portalMesh;
            MeshRenderer portalRenderer = portal.AddComponent<MeshRenderer>();
            portalRenderer.sharedMaterial = portalMaterial;
            portalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            portalRenderer.receiveShadows = false;

            AreaTransitionSection section = root.AddComponent<AreaTransitionSection>();
            SerializedObject data = new SerializedObject(section);
            SerializedProperty portals = data.FindProperty("portalRenderers");
            portals.arraySize = 1;
            portals.GetArrayElementAtIndex(0).objectReferenceValue = portalRenderer;
            // The camera is ~6.4 m behind the runner: fully open with the runner
            // ~19 m before the exit, about 1.3 s of warning at full speed.
            data.FindProperty("switchAfter").floatValue = 6f;
            data.FindProperty("revealFrom").floatValue = 8f;
            data.FindProperty("revealTo").floatValue = 18f;
            data.FindProperty("exitAt").floatValue = TunnelLength;
            data.ApplyModifiedPropertiesWithoutUndo();

            string path = DecorFolder + "Decor_AreaTunnel.prefab";
            AssetDatabase.DeleteAsset(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Walled and roofed from end to end, so nothing outside can be seen once
        // the camera is in; a great gate on the entrance side.
        private static Mesh TunnelMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float w = TunnelHalfWidth, h = TunnelHeight, l = TunnelLength;
            foreach (float s in new[] { -1f, 1f })
            {
                float inner = s * w, outer = s * (w + 1.2f);
                b.Box(new Vector3(Mathf.Min(inner, outer), -6f, 0f), new Vector3(Mathf.Max(inner, outer), h + 1f, l), Stone);
                // Glow along the foot of each wall, leading to the portal.
                float strip = s * (w - 0.04f);
                b.Box(new Vector3(Mathf.Min(strip, inner), 0.02f, 0f), new Vector3(Mathf.Max(strip, inner), 0.14f, l), Glow);
                // The gate's wings, seen from the track on the way in.
                float gateInner = s * (w + 1.2f), gateOuter = s * 22f;
                b.Box(new Vector3(Mathf.Min(gateInner, gateOuter), -8f, -0.6f), new Vector3(Mathf.Max(gateInner, gateOuter), 22f, 1.4f), Stone);
                b.Box(new Vector3(s * 10f - 2.4f, -8f, -1.6f), new Vector3(s * 10f + 2.4f, 34f, 1.6f), Stone);
                b.Spire(new Vector3(s * 10f, 34f, 0f), 2.2f, 12f, Stone);
                b.Box(new Vector3(s * 10f - 0.3f, 8f, -1.65f), new Vector3(s * 10f + 0.3f, 26f, -1.6f), Glow);
            }
            b.Box(new Vector3(-w - 1.2f, h, 0f), new Vector3(w + 1.2f, h + 1f, l), Stone);
            b.Box(new Vector3(-w - 1.2f, h, -0.6f), new Vector3(w + 1.2f, 22f, 1.4f), Stone);
            b.Spire(new Vector3(0f, 22f, 0.4f), 5f, 14f, Stone);
            b.Ring(new Vector3(0f, 15f, -0.65f), 2.6f, 3.4f, 32, StoneLight);
            b.Ring(new Vector3(0f, 15f, -0.66f), 0f, 2.6f, 32, Glow);

            // Ribs and hanging lamps every few metres give a sense of speed.
            for (float z = 2f; z < l - 2f; z += 4f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    float x0 = s * w, x1 = s * (w - 0.35f);
                    b.Box(new Vector3(Mathf.Min(x0, x1), 0f, z - 0.3f), new Vector3(Mathf.Max(x0, x1), h, z + 0.3f), StoneLight);
                    b.Box(new Vector3(s * (w - 0.42f) - 0.07f, 3.4f, z - 0.16f), new Vector3(s * (w - 0.42f) + 0.07f, 4.4f, z + 0.16f), Glow);
                }
                b.Box(new Vector3(-w, h - 0.35f, z - 0.3f), new Vector3(w, h, z + 0.3f), StoneLight);
            }
            return b.Build("Decor_AreaTunnel");
        }

        // The sheet of light that hides the next area: bright at the centre.
        // Vertex alpha: 0 at the centre, 1 at the rim (AreaPortal opens by it).
        private static Mesh PortalMesh()
        {
            float w = TunnelHalfWidth + 0.1f, top = TunnelHeight + 0.1f, bottom = -0.5f;
            Vector3 center = new Vector3(0f, (top + bottom) * 0.45f, 0f);
            Vector3[] corners =
            {
                new Vector3(-w, bottom, 0f), new Vector3(-w, top, 0f), new Vector3(w, top, 0f), new Vector3(w, bottom, 0f),
            };
            var vertices = new List<Vector3> { center };
            var colors = new List<Color> { new Color(1.3f, 1.3f, 1.3f, 0f) };
            var triangles = new List<int>();
            for (int i = 0; i < corners.Length; i++)
            {
                vertices.Add(corners[i]);
                colors.Add(new Color(0.16f, 0.16f, 0.16f, 1f));
            }
            for (int i = 0; i < corners.Length; i++)
            {
                triangles.Add(0);
                triangles.Add(1 + i);
                triangles.Add(1 + (i + 1) % corners.Length);
            }
            Mesh mesh = new Mesh { name = "Decor_AreaPortal" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- looks ----------

        private static AreaLook RailwayLook()
        {
            AreaLook look = new AreaLook();
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentMaterialFolder + "GothicSky.mat");
            if (sky != null)
            {
                look.skyTop = sky.GetColor("_TopColor");
                look.skyMid = sky.GetColor("_MidColor");
                look.skyHorizon = sky.GetColor("_HorizonColor");
                look.skyCloud = sky.GetColor("_CloudColor");
                look.moon = sky.GetColor("_MoonColor");
                look.moonGlow = sky.GetColor("_MoonGlow");
            }
            return look;
        }

        private static AreaLook CathedralLook() => new AreaLook
        {
            skyTop = new Color(0.07f, 0.02f, 0.06f),
            skyMid = new Color(0.25f, 0.06f, 0.14f),
            skyHorizon = new Color(0.72f, 0.26f, 0.3f),
            skyCloud = new Color(0.45f, 0.14f, 0.24f),
            moon = new Color(1f, 0.78f, 0.7f),
            moonGlow = new Color(0.95f, 0.3f, 0.32f),
            fog = new Color(0.25f, 0.09f, 0.15f),
            ambientSky = new Color(0.56f, 0.34f, 0.46f),
            ambientEquator = new Color(0.4f, 0.2f, 0.28f),
            ambientGround = new Color(0.14f, 0.06f, 0.1f),
            sun = new Color(1f, 0.78f, 0.8f),
            haze = new Color(0.25f, 0.09f, 0.15f),
            accent = new Color(1f, 0.26f, 0.3f),
        };

        private static AreaLook ClockworkLook() => new AreaLook
        {
            skyTop = new Color(0.03f, 0.025f, 0.05f),
            skyMid = new Color(0.17f, 0.09f, 0.05f),
            skyHorizon = new Color(0.85f, 0.5f, 0.17f),
            skyCloud = new Color(0.52f, 0.29f, 0.1f),
            moon = new Color(1f, 0.88f, 0.58f),
            moonGlow = new Color(1f, 0.58f, 0.18f),
            fog = new Color(0.2f, 0.12f, 0.06f),
            ambientSky = new Color(0.56f, 0.46f, 0.34f),
            ambientEquator = new Color(0.4f, 0.28f, 0.16f),
            ambientGround = new Color(0.13f, 0.09f, 0.05f),
            sun = new Color(1f, 0.86f, 0.62f),
            haze = new Color(0.1f, 0.055f, 0.03f),
            accent = new Color(1f, 0.64f, 0.14f),
        };

        private static Material BuildClockworkFloor()
        {
            string path = EnvironmentMaterialFolder + "Floor_ClockworkAbyss.mat";
            Material floor = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (floor == null)
            {
                floor = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FloorColor.mat"));
                AssetDatabase.CreateAsset(floor, path);
                // Riveted iron plates, brass rails, an amber edge.
                floor.SetColor("_BaseColor", new Color(0.11f, 0.085f, 0.06f));
                floor.SetColor("_StoneLight", new Color(0.24f, 0.17f, 0.09f));
                floor.SetColor("_SleeperColor", new Color(0.07f, 0.055f, 0.04f));
                floor.SetColor("_RailColor", new Color(0.78f, 0.6f, 0.28f));
                floor.SetColor("_GlowColor", new Color(1f, 0.6f, 0.1f));
                EditorUtility.SetDirty(floor);
            }
            return floor;
        }

        private static Material BuildClockworkDeckEdge()
        {
            string path = EnvironmentMaterialFolder + "DeckEdge_ClockworkAbyss.mat";
            Material edge = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (edge == null)
            {
                edge = new Material(DeckEdgeMaterial());
                AssetDatabase.CreateAsset(edge, path);
                Color amber = new Color(1f, 0.6f, 0.1f) * 2.4f;
                if (edge.HasProperty("_BaseColor"))
                    edge.SetColor("_BaseColor", amber);
                if (edge.HasProperty("_Tint"))
                    edge.SetColor("_Tint", amber);
                EditorUtility.SetDirty(edge);
            }
            return edge;
        }

        private static Material BuildCathedralFloor()
        {
            string path = EnvironmentMaterialFolder + "Floor_BrokenCathedral.mat";
            Material floor = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (floor == null)
            {
                floor = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FloorColor.mat"));
                AssetDatabase.CreateAsset(floor, path);
                // Flagstones instead of a railway: dull lines, a red edge.
                floor.SetColor("_BaseColor", new Color(0.15f, 0.09f, 0.12f));
                floor.SetColor("_StoneLight", new Color(0.3f, 0.18f, 0.22f));
                floor.SetColor("_SleeperColor", new Color(0.11f, 0.06f, 0.09f));
                floor.SetColor("_RailColor", new Color(0.4f, 0.28f, 0.3f));
                floor.SetColor("_GlowColor", new Color(1f, 0.2f, 0.18f));
                EditorUtility.SetDirty(floor);
            }
            return floor;
        }

        // ---------- clockwork obstacle models ----------

        private static readonly Color AmberGlow = new Color(1f, 0.58f, 0.1f) * 2.2f;
        private static readonly Color BrassLightModel = new Color(0.5f, 0.36f, 0.14f);
        private static readonly Color BrassDarkModel = new Color(0.17f, 0.12f, 0.07f);

        private static ObstacleSkinSet ClockworkSkins()
        {
            Material brass = Material("ClockworkBrass", Color.white, new Color(0.9f, 0.5f, 0.1f), 4.5f);
            return new ObstacleSkinSet
            {
                laneBlocker = new ObstacleSkin { model = SavePiece("Obstacle_Clockwork_Gear", GearBlockMesh(), brass, ModelFolder) },
                jumpObstacle = new ObstacleSkin { model = SavePiece("Obstacle_Clockwork_Axle", AxleMesh(), brass, ModelFolder) },
                slideObstacle = new ObstacleSkin { model = SavePiece("Obstacle_Clockwork_Frame", GearFrameMesh(), brass, ModelFolder) },
            };
        }

        // A toothed wheel facing the runner (-Z), on the box's front face.
        private static void GearFace(MeshBuilder b, Vector3 center, float radius, int teeth, Color body, Color hub)
        {
            b.Ring(center, radius * 0.3f, radius * 0.82f, teeth * 3, body);
            b.Ring(center + new Vector3(0f, 0f, -0.005f), 0f, radius * 0.3f, 16, hub);
            for (int i = 0; i < teeth; i++)
            {
                float a = i * Mathf.PI * 2f / teeth, w = Mathf.PI / teeth * 0.55f;
                Vector3 d0 = new Vector3(Mathf.Cos(a - w), Mathf.Sin(a - w), 0f), d1 = new Vector3(Mathf.Cos(a + w), Mathf.Sin(a + w), 0f);
                int v = b.V.Count;
                b.V.Add(center + d0 * radius * 0.8f); b.V.Add(center + d0 * radius);
                b.V.Add(center + d1 * radius); b.V.Add(center + d1 * radius * 0.8f);
                b.C.Add(body); b.C.Add(body); b.C.Add(body); b.C.Add(body);
                b.T.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2 });
            }
        }

        // Lane Blocker (1.5 x 2 x 1): a great gear standing in a frame.
        private static Mesh GearBlockMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.5f), new Vector3(0.75f, 0.2f, 0.5f), BrassDarkModel);
            b.Box(new Vector3(-0.75f, 1.86f, -0.5f), new Vector3(0.75f, 2f, 0.5f), BrassDarkModel);
            foreach (float s in new[] { -1f, 1f })
                b.Box(new Vector3(s * 0.67f - 0.08f, 0.2f, -0.5f), new Vector3(s * 0.67f + 0.08f, 1.86f, 0.5f), BrassDarkModel);
            b.Box(new Vector3(-0.59f, 0.2f, -0.2f), new Vector3(0.59f, 1.86f, 0.3f), BrassDarkModel);
            GearFace(b, new Vector3(0f, 1.03f, -0.22f), 0.74f, 12, BrassLightModel, AmberGlow);
            return b.Build("Obstacle_Clockwork_Gear");
        }

        // Jump Obstacle (1.5 x 0.8 x 1): a low shaft between two wheels.
        private static Mesh AxleMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.5f), new Vector3(0.75f, 0.14f, 0.5f), BrassDarkModel);
            b.Box(new Vector3(-0.75f, 0.28f, -0.22f), new Vector3(0.75f, 0.68f, 0.22f), BrassLightModel);
            foreach (float x in new[] { -0.45f, -0.15f, 0.15f, 0.45f })
                b.Box(new Vector3(x - 0.05f, 0.27f, -0.24f), new Vector3(x + 0.05f, 0.69f, 0.24f), BrassDarkModel);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 0.62f - 0.13f, 0.14f, -0.32f), new Vector3(s * 0.62f + 0.13f, 0.8f, 0.32f), BrassDarkModel);
                GearFace(b, new Vector3(s * 0.62f, 0.47f, -0.33f), 0.13f * 2f, 8, BrassLightModel, AmberGlow);
            }
            b.Box(new Vector3(-0.49f, 0.68f, -0.23f), new Vector3(0.49f, 0.72f, -0.2f), AmberGlow);
            return b.Build("Obstacle_Clockwork_Axle");
        }

        // Slide Obstacle (1.5 x 2.5 x thin): a gear frame hung over the track, its
        // lower edge marked.
        private static Mesh GearFrameMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.1f), new Vector3(0.75f, 2.5f, 0.1f), BrassDarkModel);
            b.Box(new Vector3(-0.75f, 0f, -0.12f), new Vector3(0.75f, 0.2f, 0.12f), BrassLightModel);
            b.Box(new Vector3(-0.75f, 0f, -0.13f), new Vector3(0.75f, 0.05f, -0.12f), AmberGlow);
            foreach (float s in new[] { -1f, 1f })
                GearFace(b, new Vector3(s * 0.36f, 1.25f + s * 0.4f, -0.11f), 0.38f, 9, BrassLightModel, AmberGlow);
            return b.Build("Obstacle_Clockwork_Frame");
        }

        // ---------- cathedral obstacle models ----------
        // Same hazard boxes: ObstacleVisual stretches each model to its collider.

        private static readonly Color RedGlow = new Color(1f, 0.2f, 0.16f) * 2.2f;
        private static readonly Color VioletGlass = new Color(0.6f, 0.2f, 1f) * 1.8f;
        private static readonly Color CathedralStoneLight = new Color(0.25f, 0.17f, 0.2f);
        private static readonly Color CathedralStoneDark = new Color(0.11f, 0.07f, 0.09f);

        private static ObstacleSkinSet CathedralSkins()
        {
            Material stone = Material("CathedralStone", Color.white, new Color(0.7f, 0.18f, 0.14f), 4.5f);
            return new ObstacleSkinSet
            {
                laneBlocker = new ObstacleSkin { model = SavePiece("Obstacle_Cathedral_Statue", StatueMesh(), stone, ModelFolder) },
                jumpObstacle = new ObstacleSkin { model = SavePiece("Obstacle_Cathedral_Rubble", RubbleMesh(), stone, ModelFolder) },
                slideObstacle = new ObstacleSkin { model = SavePiece("Obstacle_Cathedral_BrokenArch", BrokenArchMesh(), stone, ModelFolder) },
            };
        }

        // Lane Blocker (1.5 x 2 x 1): an angel statue on a plinth, facing the runner.
        private static Mesh StatueMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.5f), new Vector3(0.75f, 0.32f, 0.5f), CathedralStoneDark);
            b.Box(new Vector3(-0.42f, 0.32f, -0.3f), new Vector3(0.42f, 1.3f, 0.3f), CathedralStoneLight);
            foreach (float x in new[] { -0.26f, 0f, 0.26f })
                b.Box(new Vector3(x - 0.05f, 0.32f, -0.33f), new Vector3(x + 0.05f, 1.2f, -0.3f), CathedralStoneDark);
            b.Box(new Vector3(-0.48f, 1.3f, -0.32f), new Vector3(0.48f, 1.56f, 0.32f), CathedralStoneLight);
            b.Box(new Vector3(-0.18f, 1.56f, -0.19f), new Vector3(0.18f, 2f, 0.19f), CathedralStoneLight * 1.6f);
            b.Box(new Vector3(-0.11f, 1.76f, -0.205f), new Vector3(0.11f, 1.82f, -0.19f), RedGlow);
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                {
                    float y0 = 0.75f + i * 0.28f, reach = 0.5f + i * 0.09f;
                    float x0 = s * 0.42f, x1 = s * Mathf.Min(0.75f, reach + 0.05f);
                    b.Box(new Vector3(Mathf.Min(x0, x1), y0, 0.02f), new Vector3(Mathf.Max(x0, x1), y0 + 0.26f, 0.2f), i % 2 == 0 ? CathedralStoneLight : CathedralStoneDark);
                }
            return b.Build("Obstacle_Cathedral_Statue");
        }

        // Jump Obstacle (1.5 x 0.8 x 1): a toppled column in a heap of masonry.
        private static Mesh RubbleMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.36f), new Vector3(0.75f, 0.52f, 0.36f), CathedralStoneLight);
            b.Box(new Vector3(-0.7f, 0.52f, -0.2f), new Vector3(0.7f, 0.66f, 0.2f), CathedralStoneDark);
            foreach (float x in new[] { -0.45f, 0.1f, 0.5f })
                b.Box(new Vector3(x - 0.04f, 0f, -0.38f), new Vector3(x + 0.04f, 0.52f, -0.36f), CathedralStoneDark);
            b.Box(new Vector3(-0.75f, 0f, -0.5f), new Vector3(-0.28f, 0.34f, -0.16f), CathedralStoneDark);
            b.Box(new Vector3(0.28f, 0f, 0.14f), new Vector3(0.75f, 0.42f, 0.5f), CathedralStoneDark);
            b.Box(new Vector3(-0.14f, 0.5f, -0.1f), new Vector3(0.24f, 0.8f, 0.2f), CathedralStoneLight);
            b.Box(new Vector3(-0.75f, 0.5f, -0.37f), new Vector3(0.75f, 0.54f, -0.36f), RedGlow);
            return b.Build("Obstacle_Cathedral_Rubble");
        }

        // Slide Obstacle (1.5 x 2.5 x thin): the fallen crown of an arch, with a
        // lit window in it and its lower edge marked.
        private static Mesh BrokenArchMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.1f), new Vector3(0.75f, 2.5f, 0.1f), CathedralStoneDark);
            b.Box(new Vector3(-0.75f, 0f, -0.12f), new Vector3(0.75f, 0.22f, 0.12f), CathedralStoneLight);
            b.Box(new Vector3(-0.75f, 0f, -0.13f), new Vector3(0.75f, 0.05f, -0.12f), RedGlow);
            foreach (float x in new[] { -0.6f, 0.6f })
                b.Box(new Vector3(x - 0.15f, 0.22f, -0.12f), new Vector3(x + 0.15f, 2.5f, 0.12f), CathedralStoneLight);
            b.Box(new Vector3(-0.26f, 0.55f, -0.11f), new Vector3(0.26f, 1.7f, -0.1f), VioletGlass);
            for (int i = 0; i < 3; i++)
            {
                float half = 0.26f - i * 0.08f, y = 1.7f + i * 0.12f;
                b.Box(new Vector3(-half, y, -0.11f), new Vector3(half, y + 0.12f, -0.1f), VioletGlass);
            }
            return b.Build("Obstacle_Cathedral_BrokenArch");
        }
    }
}
