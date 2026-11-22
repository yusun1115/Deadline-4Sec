using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Area obstacle models that read like the railway's (2026-11-22, playtest):
    // one silhouette per action, whatever the area.
    //   Lane Blocker = a tall thing standing on the floor (railway: tombstone),
    //   Jump         = low and wide (railway: barricade),
    //   Slide        = a beam hanging overhead, spikes down, chains / rods up
    //                  (railway: spiked beam) — open underneath.
    // Before, the Cathedral slide was a framed panel and the Clockwork blocker
    // and slide were both square gear panels: a first-time player couldn't
    // tell whether to jump, dodge or slide. Same hazard boxes (ObstacleVisual
    // stretches each model to its collider), so gameplay is unchanged.
    // Menu: Deadline 4 Sec/Course/Rebuild Area Obstacle Models.
    public static partial class PatternLibraryBuilder
    {
        [MenuItem("Deadline 4 Sec/Course/Rebuild Area Obstacle Models")]
        public static void RebuildAreaObstacleModels()
        {
            AreaDefinition cathedral = AssetDatabase.LoadAssetAtPath<AreaDefinition>(AreaFolder + "Area_BrokenCathedral.asset");
            AreaDefinition clockwork = AssetDatabase.LoadAssetAtPath<AreaDefinition>(AreaFolder + "Area_ClockworkAbyss.asset");
            if (cathedral == null || clockwork == null)
                throw new InvalidOperationException("Build Areas first.");
            var old = new List<string>();
            foreach (ObstacleSkinSet set in new[] { cathedral.obstacleSkins, clockwork.obstacleSkins })
                foreach (ObstacleSkin skin in new[] { set.laneBlocker, set.jumpObstacle, set.slideObstacle })
                    if (skin != null && skin.model != null)
                        old.Add(AssetDatabase.GetAssetPath(skin.model));
            cathedral.obstacleSkins = CathedralSkins();
            clockwork.obstacleSkins = ClockworkSkins();
            EditorUtility.SetDirty(cathedral);
            EditorUtility.SetDirty(clockwork);
            AssetDatabase.SaveAssets();
            // The replaced models (and their meshes) are no longer used anywhere.
            var kept = new HashSet<string>();
            foreach (ObstacleSkinSet set in new[] { cathedral.obstacleSkins, clockwork.obstacleSkins })
                foreach (ObstacleSkin skin in new[] { set.laneBlocker, set.jumpObstacle, set.slideObstacle })
                    kept.Add(AssetDatabase.GetAssetPath(skin.model));
            foreach (string path in old)
                if (!string.IsNullOrEmpty(path) && !kept.Contains(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.DeleteAsset(path.Replace(".prefab", "_Mesh.asset"));
                }
            Debug.Log("Area obstacle models rebuilt.");
        }

        // A point hanging down (stalactite / tooth): apex `length` below the base.
        private static void SpikeDown(MeshBuilder b, Vector3 baseCenter, float half, float length, Color col)
        {
            Vector3 tip = baseCenter + Vector3.down * length;
            Vector3 a = baseCenter + new Vector3(-half, 0, -half), c = baseCenter + new Vector3(half, 0, -half);
            Vector3 d = baseCenter + new Vector3(half, 0, half), e = baseCenter + new Vector3(-half, 0, half);
            foreach (var (p, q) in new[] { (a, c), (c, d), (d, e), (e, a) })
            {
                int i = b.V.Count;
                b.V.Add(p); b.V.Add(q); b.V.Add(tip);
                b.C.Add(col); b.C.Add(col); b.C.Add(col);
                b.T.AddRange(new[] { i, i + 1, i + 2 });
            }
        }

        // Links from y0 up to y1 at x, alternating wide / narrow.
        private static void Chain(MeshBuilder b, float x, float y0, float y1, Color col)
        {
            int k = 0;
            for (float y = y0; y < y1; y += 0.16f, k++)
            {
                float w = k % 2 == 0 ? 0.06f : 0.025f, d = k % 2 == 0 ? 0.025f : 0.06f;
                b.Box(new Vector3(x - w, y, -d), new Vector3(x + w, Mathf.Min(y1, y + 0.14f), d), col);
            }
        }

        // Cathedral slide (1.5 x 2.5 x thin): a fallen stone lintel on two
        // chains, stalactites under it, a red glow along its lower edge.
        private static Mesh HangingLintelMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0.42f, -0.1f), new Vector3(0.75f, 0.92f, 0.1f), CathedralStoneLight);
            b.Box(new Vector3(-0.75f, 0.92f, -0.11f), new Vector3(0.75f, 1.02f, 0.11f), CathedralStoneDark);
            b.Box(new Vector3(-0.7f, 0.6f, -0.105f), new Vector3(0.7f, 0.66f, -0.1f), CathedralStoneDark);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.48f + i * 0.32f;
                b.Box(new Vector3(x - 0.05f, 0.7f, -0.11f), new Vector3(x + 0.05f, 0.84f, -0.105f), RedGlow);
            }
            b.Box(new Vector3(-0.75f, 0.9f, -0.12f), new Vector3(0.75f, 0.93f, -0.11f), RedGlow * 0.7f);
            b.Box(new Vector3(-0.75f, 0.4f, -0.12f), new Vector3(0.75f, 0.44f, -0.1f), RedGlow);
            for (int i = 0; i < 6; i++)
            {
                float x = -0.625f + i * 0.25f;
                SpikeDown(b, new Vector3(x, 0.42f, 0f), 0.09f, i % 2 == 0 ? 0.42f : 0.3f, CathedralStoneLight);
            }
            foreach (float x in new[] { -0.55f, 0.55f })
                Chain(b, x, 1.02f, 2.5f, CathedralStoneDark);
            return b.Build("Obstacle_Cathedral_HangingLintel");
        }

        // Clockwork lane blocker (1.5 x 2 x 1): a tall piston column on a
        // base plate — a standing pillar, like the railway's tombstone.
        private static Mesh PistonColumnMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0f, -0.5f), new Vector3(0.75f, 0.22f, 0.5f), BrassDarkModel);
            b.Box(new Vector3(-0.42f, 0.22f, -0.36f), new Vector3(0.42f, 1.78f, 0.36f), BrassLightModel);
            foreach (float y in new[] { 0.55f, 1.15f, 1.6f })
                b.Box(new Vector3(-0.5f, y, -0.42f), new Vector3(0.5f, y + 0.09f, 0.42f), y > 1f && y < 1.3f ? AmberGlow : BrassDarkModel);
            b.Box(new Vector3(-0.58f, 1.76f, -0.47f), new Vector3(0.58f, 1.8f, -0.46f), AmberGlow);
            b.Box(new Vector3(-0.75f, 0.2f, -0.52f), new Vector3(0.75f, 0.24f, -0.5f), AmberGlow * 0.8f);
            b.Box(new Vector3(-0.58f, 1.78f, -0.46f), new Vector3(0.58f, 2f, 0.46f), BrassDarkModel);
            // A glowing gauge and a vertical warning strip on the face.
            GearFace(b, new Vector3(0f, 0.9f, -0.37f), 0.24f, 10, BrassDarkModel, AmberGlow);
            b.Box(new Vector3(-0.04f, 1.25f, -0.375f), new Vector3(0.04f, 1.72f, -0.365f), AmberGlow);
            return b.Build("Obstacle_Clockwork_Piston");
        }

        // Clockwork slide (1.5 x 2.5 x thin): a toothed rack hanging from two
        // rods and a turning gear, teeth down, an amber glow along its edge.
        private static Mesh GearRackMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.75f, 0.42f, -0.1f), new Vector3(0.75f, 0.9f, 0.1f), BrassLightModel);
            b.Box(new Vector3(-0.75f, 0.9f, -0.11f), new Vector3(0.75f, 0.98f, 0.11f), BrassDarkModel);
            b.Box(new Vector3(-0.75f, 0.9f, -0.12f), new Vector3(0.75f, 0.93f, -0.11f), AmberGlow * 0.7f);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.45f + i * 0.45f;
                b.Box(new Vector3(x - 0.06f, 0.6f, -0.11f), new Vector3(x + 0.06f, 0.75f, -0.105f), AmberGlow);
            }
            b.Box(new Vector3(-0.75f, 0.4f, -0.12f), new Vector3(0.75f, 0.44f, -0.1f), AmberGlow);
            for (int i = 0; i < 7; i++)
                SpikeDown(b, new Vector3(-0.64f + i * 0.213f, 0.42f, 0f), 0.08f, 0.36f, BrassDarkModel);
            foreach (float x in new[] { -0.55f, 0.55f })
                b.Box(new Vector3(x - 0.04f, 0.98f, -0.04f), new Vector3(x + 0.04f, 2.5f, 0.04f), BrassDarkModel);
            GearFace(b, new Vector3(0f, 1.85f, -0.05f), 0.4f, 10, BrassLightModel, AmberGlow);
            return b.Build("Obstacle_Clockwork_GearRack");
        }
    }
}
