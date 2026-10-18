using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Air Route + Upper Platform patterns (Upper Platform.md, 2026-10-16).
    //
    // Two levels in one pattern: the spawner's ordinary floor stays the lower
    // route, and an "Upper Deck" (a plain floor collider, not an Obstacle) runs
    // UpperDeckHeight (8m) above it (OneWayPlatform: solid from above only).
    // Entry (Ground → Upper): air enemies climb
    // in alternating lanes, 5m apart (Pattern L/P spacing), the last one 1m
    // above the deck and 1.5m before it, so that kill's bounce lands on the
    // deck. A missed chain leaves the runner in the lane of the last enemy it
    // killed — never under the next one — and low enough to pass under the
    // deck's front edge (bottom 7.5m) onto the lower route. Upper runs (Upper →
    // Upper) put ordinary enemies / obstacles / Souls on both levels so the
    // Deadline timer can be refreshed on either. The return (Upper → Ground)
    // ends the deck: the runner drops ~0.8s onto a clear stretch below.
    // Entries accept Any (like Air Ladder): a runner arriving from an air combo
    // homes straight into the first chain enemy; the route then ends on the
    // ground, which also breaks the long air-pattern loops.
    public static partial class PatternLibraryBuilder
    {
        private const float Deck = CoursePattern.UpperDeckHeight;
        private const float DeckThickness = 0.5f;
        private const float DeckWidth = 8.6f;

        [MenuItem("Deadline 4 Sec/Course/Build Air Route Patterns")]
        public static void BuildAirRoutes()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the open scene before building patterns.");
            LoadPrefabs();
            var M = CoursePattern.DifficultyLevel.Medium;
            var H = CoursePattern.DifficultyLevel.Hard;
            var G = CoursePattern.TravelState.Ground;
            var Any = CoursePattern.TravelState.Any;
            var U = CoursePattern.TravelState.Upper;

            var specs = new List<Spec>
            {
                // First air route: chain only, no obstacles (readable).
                P("Pattern_U1_AirRouteEntry", 40, M, CoursePattern.PatternCategory.AirRoute, Any, U)
                    .Air(0, 2.6f, 8).Air(1, 4.6f, 13).Air(0, 6.6f, 18).Air(-1, Deck + 1f, 23)
                    .Coins(1, 3.6f, 10.2f, 1).Coins(0, 5.6f, 15.2f, 1).Coins(-1, 7.6f, 20.2f, 1)
                    .UpperDeck(24.5f, 40f)
                    .Coins(-1, Deck + 1f, 27, 7, 1.6f).Gift(0, 35, Deck + 1f)
                    // Lower route: a Soul trail away from the chain, one enemy later.
                    .Coins(-1, 1f, 9, 6, 1.6f).Enemy(1, 33),
                // Variant: five enemies with side steps and Soul lines between.
                P("Pattern_U5_AirRouteEntry_Weave", 44, H, CoursePattern.PatternCategory.AirRoute, Any, U)
                    .Air(1, 2.6f, 7).Air(-1, 4.4f, 12).Air(0, 6.2f, 17).Air(1, 7.4f, 21).Air(0, Deck + 1f, 26)
                    .Coins(-1, 3.6f, 9.3f, 2, 1.4f).Coins(0, 5.4f, 14.3f, 2, 1.4f).Coins(1, 6.9f, 18.8f, 1).Coins(0, 8.6f, 23.6f, 1)
                    .UpperDeck(27.5f, 44f)
                    .Coins(0, Deck + 1f, 30, 6, 1.6f).Gift(1, 40, Deck + 1f)
                    // Lower route: obstacle, then enemy, after the landing zone of a missed chain.
                    .Jump(-1, 35).Enemy(1, 40),
                // Upper runs: ordinary ground beats on the deck, a lower route under it.
                P("Pattern_U2_UpperRun_Combat", 32, M, CoursePattern.PatternCategory.GroundCombat, U, U)
                    .UpperDeck(0f, 32f)
                    .Enemy(1, 6, Deck).Enemy(-1, 12, Deck).Enemy(0, 18, Deck).Enemy(1, 26, Deck)
                    .Coins(0, Deck + 1f, 7, 3).Coins(-1, Deck + 1f, 21, 3).Gift(-1, 28, Deck + 1f)
                    .DeckLower().JumpLow(0, 8).Enemy(-1, 16).SlideLow(1, 24).Coins(-1, 1f, 25, 3),
                P("Pattern_U3_UpperRun_Hurdles", 32, M, CoursePattern.PatternCategory.Obstacle, U, U)
                    .UpperDeck(0f, 32f)
                    .DeckJump(0, 6).DeckSlide(0, 14).Enemy(1, 21, Deck).DeckJump(-1, 28)
                    .Coins(0, Deck + 1.9f, 5.4f, 2).Coins(0, Deck + 0.7f, 13, 3, 1f).Coins(-1, Deck + 1f, 22, 3)
                    .DeckLower().Enemy(1, 6).JumpLow(-1, 14).Enemy(0, 24).Coins(1, 1f, 15, 3),
                // Return: the deck ends at 14m; the drop lands ~23m on a clear stretch.
                P("Pattern_U4_UpperReturn", 40, M, CoursePattern.PatternCategory.Mixed, U, G)
                    .UpperDeck(0f, 14f).DeckEdge(14f)
                    .Enemy(0, 6, Deck).Coins(0, Deck + 1f, 9, 3)
                    .Coins(0, 6.2f, 16.5f, 1).Coins(0, 4f, 18.5f, 1).Coins(0, 2f, 20.5f, 1)
                    .DeckLower().Enemy(-1, 6).Coins(1, 1f, 4, 3)
                    // No jump obstacle near the end: the next pattern may open with a train.
                    .Enemy(1, 31).Coins(0, 1f, 26, 4),
            };

            var built = new List<GameObject>();
            foreach (Spec spec in specs)
                built.Add(SavePattern(spec));
            RegisterInScenes(built);
            AssetDatabase.SaveAssets();
            Debug.Log("Air route patterns: " + built.Count);
        }

        // The deck: a plain floor box (FloorColor rail track on top) from z0 to
        // z1, top at UpperDeckHeight, with pink edge strips and two side girders
        // (visual only, outside the lanes).
        private static Spec UpperDeck(this Spec s, float z0, float z1)
        {
            s.Items.Add(parent =>
            {
                Material floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FloorColor.mat");
                GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = "Upper Deck";
                deck.transform.SetParent(parent, false);
                deck.transform.localPosition = new Vector3(0f, Deck - DeckThickness * 0.5f, (z0 + z1) * 0.5f);
                deck.transform.localScale = new Vector3(DeckWidth, DeckThickness, z1 - z0);
                deck.GetComponent<Renderer>().sharedMaterial = floor;
                // Solid from above only: no ceiling / front edge for the lower route.
                deck.AddComponent<OneWayPlatform>();

                Material edge = DeckEdgeMaterial();
                foreach (float x in new[] { -DeckWidth * 0.5f, DeckWidth * 0.5f })
                {
                    Visual(parent, "Deck Rail", edge, new Vector3(x, Deck + 0.08f, (z0 + z1) * 0.5f), new Vector3(0.18f, 0.16f, z1 - z0));
                    Visual(parent, "Deck Girder", floor, new Vector3(x * 1.08f, Deck - 1.1f, (z0 + z1) * 0.5f), new Vector3(0.35f, 1.6f, z1 - z0));
                }
                // A bright lip on a deck's first edge shows where the route goes up.
                if (z0 > 0f)
                    Visual(parent, "Deck Front Edge", edge, new Vector3(0f, Deck - 0.1f, z0 + 0.05f), new Vector3(DeckWidth, 0.3f, 0.12f));
            });
            return s;
        }

        // Bright lip where the deck ends (return pattern).
        private static Spec DeckEdge(this Spec s, float z)
        {
            s.Items.Add(parent => Visual(parent, "Deck End Edge", DeckEdgeMaterial(),
                new Vector3(0f, Deck - 0.1f, z - 0.05f), new Vector3(DeckWidth, 0.3f, 0.12f)));
            return s;
        }

        // Marker only (keeps the builder lines readable: what follows is the lower route).
        private static Spec DeckLower(this Spec s) => s;

        private static Spec DeckJump(this Spec s, int lane, float z) =>
            s.Add(jump, $"Deck Jump / Near Miss {LaneName(lane)}", new Vector3(lane * Lane, Deck + 0.38f, z));
        private static Spec DeckSlide(this Spec s, int lane, float z) =>
            s.Add(slide, $"Deck Slide / Near Miss {LaneName(lane)}", new Vector3(lane * Lane, Deck + 2.56f, z));
        private static Spec JumpLow(this Spec s, int lane, float z) => s.Jump(lane, z);
        private static Spec SlideLow(this Spec s, int lane, float z) => s.Slide(lane, z);

        private static void Visual(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material DeckEdgeMaterial()
        {
            const string path = ModelFolder + "DeckEdgeGlow.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
                return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", new Color(1f, 0.3f, 0.7f) * 2.2f);
            System.IO.Directory.CreateDirectory(ModelFolder);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
