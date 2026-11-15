using UnityEngine;

namespace Deadline4Sec
{
    // Visual upgrade (2026-11-15, VisualUpgrade.md B): the mid and far layers of
    // each area's backdrop. Mid: a few kinds of structure between the track-side
    // pillars and the far towers, recycled ahead of the camera, in their own
    // dimmer material copy. Far: one skyline mesh riding with the camera, mostly
    // sunk in the fog (own material: _FogAmount, _Tint from the area profile).
    // Same pieces on both sides (mirrored), no left/right variants.
    public sealed partial class EnvironmentScroller
    {
        private void BuildLayers(AreaType area, float startZ)
        {
            Mesh[] mids;
            switch (area)
            {
                case AreaType.BrokenCathedral:
                    mids = new[] { BuildArcadeMesh(), BuildLeaningSpireMesh(), BuildChainPostsMesh() };
                    break;
                case AreaType.ClockworkAbyss:
                    mids = new[] { BuildTrussMesh(), BuildAxlePostMesh(), BuildPipeRunMesh() };
                    break;
                default:
                    mids = new[] { BuildGothicBlockMesh(), BuildViaductMesh(), BuildRailArchMesh() };
                    break;
            }
            Mesh axleGear = area == AreaType.ClockworkAbyss ? BuildGearMesh(4.2f, 12, Brass, BrassDark) : null;
            float step = midSpan / Mathf.Max(1, midCount);
            for (int i = 0; i < midCount; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                int kind = (i / 2 + i) % mids.Length;
                Transform m = Spawn("Mid", mids[kind], building.MidMaterial);
                m.position = new Vector3(trackX + side * Random.Range(midDistanceX.x, midDistanceX.y), 0f,
                    startZ + 20f + i * step + Random.Range(-step * 0.3f, step * 0.3f));
                m.localScale = new Vector3(-side, 1f, 1f); // meshes face +X (the track) on the left
                if (area == AreaType.BrokenCathedral && kind == 1)
                    m.rotation = Quaternion.Euler(0f, 0f, -side * Random.Range(4f, 8f)); // leaning away from the track
                if (axleGear != null && kind == 1)
                    AddGear(m, axleGear, new Vector3(1.2f, 13f, 0f), 40f * side, building.MidMaterial);
                building.Mids.Add(m);
            }

            Transform far = Spawn("FarLayer", area == AreaType.BrokenCathedral ? BuildFarCathedralMesh()
                : area == AreaType.ClockworkAbyss ? BuildFarMachineMesh() : BuildFarCityMesh(), building.FarMaterial);
            building.Far = far;
            if (area == AreaType.ClockworkAbyss)
            {
                Mesh giant = BuildGearMesh(30f, 24, Brass, BrassDark);
                AddGear(far, giant, new Vector3(-150f, 52f, 14f), 5f, building.FarMaterial);
                AddGear(far, giant, new Vector3(170f, 38f, 14f), -4f, building.FarMaterial);
                AddGear(far, BuildGearMesh(18f, 16, Brass, BrassDark), new Vector3(-95f, 66f, 13f), -8f, building.FarMaterial);
            }
        }

        // A beam between two points: two crossed quads (Cull Off), for braces.
        private static void Beam(MeshBuilder b, Vector3 from, Vector3 to, float thickness, Color col)
        {
            Vector3 dir = (to - from).normalized;
            Vector3 n1 = Vector3.Cross(dir, Vector3.forward);
            if (n1.sqrMagnitude < 0.01f)
                n1 = Vector3.Cross(dir, Vector3.right);
            n1 = n1.normalized * thickness * 0.5f;
            Vector3 n2 = Vector3.Cross(dir, n1).normalized * thickness * 0.5f;
            b.Quad(from - n1, to - n1, to + n1, from + n1, col);
            b.Quad(from - n2, to - n2, to + n2, from + n2, col);
        }

        // Windows on the +X face (towards the track) of a box, a grid of a few lit.
        private static void WindowGrid(MeshBuilder b, float x, float z0, float z1, float y0, float y1, float size, float chance, Color[] lit)
        {
            for (float y = y0; y + size * 1.6f < y1; y += size * 2.6f)
                for (float z = z0 + size; z + size < z1; z += size * 2.2f)
                    if (Random.value < chance)
                        b.Quad(new Vector3(x, y, z), new Vector3(x, y + size * 1.6f, z),
                            new Vector3(x, y + size * 1.6f, z + size), new Vector3(x, y, z + size), lit[Random.Range(0, lit.Length)]);
        }

        // ---------- Soul Railway: gothic blocks, rail viaducts, rail arches; the city beyond ----------

        private static readonly Color[] RailwayWindows =
        {
            new Color(0.36f, 0.05f, 0.21f, 1f),
            new Color(0.32f, 0.15f, 0.05f, 1f),
            new Color(0.2f, 0.05f, 0.3f, 1f),
        };

        private static Mesh BuildGothicBlockMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float hw = 3.2f, h = 15f, d = 5f;
            b.Box(new Vector3(-hw, -14f, -d), new Vector3(hw, h, d), Stone);
            // Pitched roof, ridge along Z.
            Vector3 r0 = new Vector3(0f, h + 4.5f, -d), r1 = new Vector3(0f, h + 4.5f, d);
            b.Quad(new Vector3(-hw, h, -d), r0, r1, new Vector3(-hw, h, d), StoneLight);
            b.Quad(new Vector3(hw, h, d), r1, r0, new Vector3(hw, h, -d), StoneLight);
            b.Tri(new Vector3(-hw, h, -d), new Vector3(hw, h, -d), r0, Stone);
            b.Tri(new Vector3(hw, h, d), new Vector3(-hw, h, d), r1, Stone);
            // A corner turret.
            b.Box(new Vector3(hw - 1.2f, h, -d), new Vector3(hw + 0.4f, h + 6f, -d + 1.6f), Stone);
            b.Pyramid(new Vector3(hw - 0.4f, h + 6f, -d + 0.8f), 1f, 5f, Stone);
            WindowGrid(b, hw + 0.02f, -d, d, 2f, h - 1f, 0.7f, 0.28f, RailwayWindows);
            return b.Build("GothicBlock");
        }

        // An elevated rail line beside the track: piers, stepped arches, a deck
        // with rails and a pink neon edge.
        private static Mesh BuildViaductMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float deck = 8f, half = 15f;
            for (float z = -half; z <= half + 0.1f; z += 10f)
                b.Box(new Vector3(-1.1f, -14f, z - 0.9f), new Vector3(1.1f, deck, z + 0.9f), Stone);
            for (float z = -half; z < half - 0.1f; z += 10f)
                for (int s = 0; s < 5; s++)
                {
                    float t = (s + 0.5f) / 5f;
                    float y = deck - 3f + Mathf.Sin(t * Mathf.PI) * 2.2f;
                    b.Box(new Vector3(-1f, y, z + 0.9f + t * 8.2f - 0.82f), new Vector3(1f, deck, z + 0.9f + t * 8.2f + 0.82f), StoneLight);
                }
            b.Box(new Vector3(-1.8f, deck, -half - 1f), new Vector3(1.8f, deck + 0.7f, half + 1f), StoneLight);
            foreach (float x in new[] { -0.6f, 0.6f })
                b.Box(new Vector3(x - 0.07f, deck + 0.7f, -half - 1f), new Vector3(x + 0.07f, deck + 0.85f, half + 1f), Iron);
            b.Box(new Vector3(1.8f, deck + 0.55f, -half - 1f), new Vector3(1.9f, deck + 0.7f, half + 1f), Glow);
            return b.Build("Viaduct");
        }

        // A free-standing gothic gate along the line, a lantern hanging in it.
        private static Mesh BuildRailArchMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float half = 5f, h = 11f;
            foreach (float z in new[] { -half, half })
            {
                b.Box(new Vector3(-0.8f, -14f, z - 0.8f), new Vector3(0.8f, h, z + 0.8f), Stone);
                b.Pyramid(new Vector3(0f, h + 3.6f, z), 0.6f, 3f, Stone);
            }
            for (int s = 0; s < 8; s++)
            {
                float t = (s + 0.5f) / 8f;
                float y = h + 3.2f * (1f - Mathf.Abs(t * 2f - 1f));
                float z = -half + t * half * 2f;
                b.Box(new Vector3(-0.6f, y - 0.5f, z - 0.7f), new Vector3(0.6f, y + 0.5f, z + 0.7f), StoneLight);
            }
            b.Box(new Vector3(-0.04f, h - 0.5f, -0.04f), new Vector3(0.04f, h + 2.5f, 0.04f), Iron);
            b.Box(new Vector3(-0.35f, h - 1.3f, -0.35f), new Vector3(0.35f, h - 0.5f, 0.35f), Glow);
            return b.Build("RailArch");
        }

        // The far skyline: a gothic city with clock towers and rail bridges.
        private static Mesh BuildFarCityMesh()
        {
            MeshBuilder b = new MeshBuilder();
            Color dim = new Color(0.32f, 0.05f, 0.2f, 1f);
            float x = -330f;
            int n = 0;
            while (x < 330f)
            {
                float w = Random.Range(9f, 22f);
                bool clock = n % 7 == 3;
                float h = clock ? Random.Range(58f, 72f) : Random.Range(12f, 44f);
                float z = Random.Range(0f, 14f);
                b.Box(new Vector3(x, -40f, z), new Vector3(x + w, h, z + 12f), Stone);
                if (clock)
                {
                    b.Pyramid(new Vector3(x + w * 0.5f, h, z + 6f), w * 0.45f, w * 1.3f, Stone);
                    b.Ring(new Vector3(x + w * 0.5f, h - w * 0.55f, z - 0.05f), 0f, w * 0.32f, 20, dim);
                }
                else if (Random.value < 0.5f)
                    b.Pyramid(new Vector3(x + w * 0.5f, h, z + 6f), w * 0.5f, w * 0.6f, Stone);
                else if (Random.value < 0.5f)
                {
                    b.Box(new Vector3(x + w * 0.4f, h, z + 4f), new Vector3(x + w * 0.6f, h + 9f, z + 8f), Stone);
                    b.Pyramid(new Vector3(x + w * 0.5f, h + 9f, z + 6f), w * 0.1f + 0.4f, 6f, Stone);
                }
                for (int i = 0; i < 3; i++)
                    if (Random.value < 0.5f)
                    {
                        float wx = x + Random.Range(1f, w - 2f), wy = Random.Range(4f, h - 4f);
                        b.Quad(new Vector3(wx, wy, z - 0.05f), new Vector3(wx, wy + 1.6f, z - 0.05f),
                            new Vector3(wx + 1f, wy + 1.6f, z - 0.05f), new Vector3(wx + 1f, wy, z - 0.05f), dim);
                    }
                x += w + Random.Range(-2f, 3f);
                n++;
            }
            // Two long rail bridges across the city, on arches.
            foreach (float y in new[] { 16f, 26f })
            {
                float z = y < 20f ? -6f : -3f;
                b.Box(new Vector3(-330f, y, z), new Vector3(330f, y + 2f, z + 3f), StoneLight);
                for (float px = -320f; px < 330f; px += 24f)
                    b.Box(new Vector3(px, -40f, z + 0.5f), new Vector3(px + 2.5f, y, z + 2.5f), Stone);
            }
            return b.Build("FarCity");
        }

        // ---------- Broken Cathedral: arcades, leaning spires, chains; the great walls beyond ----------

        private static Mesh BuildArcadeMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float pier = 10f;
            float[] zs = { -12f, -4f, 4f, 12f };
            foreach (float z in zs)
                b.Box(new Vector3(-0.8f, -14f, z - 0.7f), new Vector3(0.8f, pier, z + 0.7f), CathedralStone);
            int broken = Random.Range(0, zs.Length - 1);
            for (int a = 0; a < zs.Length - 1; a++)
                for (int s = 0; s < 7; s++)
                {
                    if (a == broken && s > 3)
                        break; // the arch fell in
                    float t = (s + 0.5f) / 7f;
                    float y = pier + 3.4f * (1f - Mathf.Abs(t * 2f - 1f));
                    float z = zs[a] + 0.7f + t * 6.6f;
                    b.Box(new Vector3(-0.7f, y - 0.6f, z - 0.5f), new Vector3(0.7f, y + 0.4f, z + 0.5f), CathedralLight);
                }
            b.Box(new Vector3(-0.9f, pier + 3.6f, -12.7f), new Vector3(0.9f, pier + 4.4f, zs[broken] + 1f), CathedralStone);
            return b.Build("Arcade");
        }

        private static Mesh BuildLeaningSpireMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float hw = 1.5f, h = 19f;
            b.Box(new Vector3(-hw, -14f, -hw), new Vector3(hw, h, hw), CathedralStone);
            b.Box(new Vector3(-hw - 0.3f, h * 0.6f, -hw - 0.3f), new Vector3(hw + 0.3f, h * 0.6f + 0.6f, hw + 0.3f), CathedralLight);
            // Broken point: a truncated pyramid and a jagged stub.
            b.Pyramid(new Vector3(0f, h, 0f), hw, 5f, CathedralStone);
            b.Box(new Vector3(-0.3f, h + 4f, -0.3f), new Vector3(0.1f, h + 6.5f, 0.2f), CathedralStone);
            // A red slit of light down the side facing the track.
            b.Quad(new Vector3(hw + 0.02f, 4f, -0.25f), new Vector3(hw + 0.02f, 12f, -0.25f),
                new Vector3(hw + 0.02f, 12f, 0.25f), new Vector3(hw + 0.02f, 4f, 0.25f), new Color(0.6f, 0.04f, 0.05f, 1f));
            return b.Build("LeaningSpire");
        }

        private static Mesh BuildChainPostsMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float h = 14f;
            foreach (float z in new[] { -9f, 9f })
            {
                b.Box(new Vector3(-0.6f, -14f, z - 0.6f), new Vector3(0.6f, h, z + 0.6f), CathedralStone);
                b.Box(new Vector3(-0.9f, h, z - 0.9f), new Vector3(0.9f, h + 0.6f, z + 0.9f), CathedralLight);
            }
            int links = 26;
            for (int i = 0; i < links; i++)
            {
                float t = (i + 0.5f) / links;
                float z = -9f + t * 18f;
                float y = h - 0.4f - Mathf.Sin(t * Mathf.PI) * 4f;
                float s = i % 2 == 0 ? 0.15f : 0.05f;
                b.Box(new Vector3(-s, y - 0.06f, z - 0.36f), new Vector3(s, y + 0.06f, z + 0.36f), Iron);
            }
            // Chains hanging straight down, one with a caged red lamp.
            foreach (float z in new[] { -4f, 3f })
            {
                float top = h - 0.4f - Mathf.Sin((z + 9f) / 18f * Mathf.PI) * 4f;
                for (float y = top - 0.5f; y > top - 6f; y -= 0.6f)
                    b.Box(new Vector3(-0.05f, y - 0.28f, z - 0.12f), new Vector3(0.05f, y + 0.28f, z + 0.12f), Iron);
            }
            b.Box(new Vector3(-0.3f, h - 13f, 2.7f), new Vector3(0.3f, h - 12.2f, 3.3f), new Color(0.7f, 0.06f, 0.05f, 1f));
            return b.Build("ChainPosts");
        }

        // The far side of the nave: endless walls with buttresses and tall windows.
        private static Mesh BuildFarCathedralMesh()
        {
            MeshBuilder b = new MeshBuilder();
            Color glassA = new Color(0.4f, 0.03f, 0.05f, 1f), glassB = new Color(0.16f, 0.03f, 0.3f, 1f);
            float x = -330f;
            int n = 0;
            while (x < 330f)
            {
                float w = Random.Range(26f, 40f);
                float h = Random.Range(34f, 52f);
                b.Box(new Vector3(x, -40f, 6f), new Vector3(x + w, h, 18f), CathedralStone);
                // Gabled top or a broken edge.
                if (Random.value < 0.6f)
                    b.Tri(new Vector3(x, h, 6f), new Vector3(x + w * 0.5f, h + w * 0.35f, 6f), new Vector3(x + w, h, 6f), CathedralStone);
                else
                    for (int i = 0; i < 4; i++)
                        b.Box(new Vector3(x + i * w / 4f, h, 6f), new Vector3(x + (i + 1) * w / 4f, h + Random.Range(1f, 7f), 9f), CathedralStone);
                // Buttress: a pier and its flying arm.
                b.Box(new Vector3(x - 2f, -40f, -4f), new Vector3(x + 2f, h * 0.75f, 2f), CathedralStone);
                b.Pyramid(new Vector3(x, h * 0.75f, -1f), 2f, 8f, CathedralStone);
                Beam(b, new Vector3(x, h * 0.62f, -1f), new Vector3(x + 0.1f, h * 0.88f, 6f), 2f, CathedralLight);
                // Two tall lancets.
                for (int i = 0; i < 2; i++)
                {
                    float wx = x + w * (0.3f + i * 0.4f);
                    b.Quad(new Vector3(wx - 1.4f, h * 0.25f, 5.95f), new Vector3(wx - 1.4f, h * 0.7f, 5.95f),
                        new Vector3(wx + 1.4f, h * 0.7f, 5.95f), new Vector3(wx + 1.4f, h * 0.25f, 5.95f), (n + i) % 2 == 0 ? glassA : glassB);
                    b.Tri(new Vector3(wx - 1.4f, h * 0.7f, 5.95f), new Vector3(wx, h * 0.7f + 3f, 5.95f), new Vector3(wx + 1.4f, h * 0.7f, 5.95f),
                        (n + i) % 2 == 0 ? glassA : glassB);
                }
                // Now and then a tower above the walls.
                if (n % 4 == 1)
                {
                    float th = h + Random.Range(28f, 46f);
                    b.Box(new Vector3(x + w * 0.3f, -40f, 8f), new Vector3(x + w * 0.7f, th, 16f), CathedralStone);
                    if (Random.value < 0.5f)
                        b.Pyramid(new Vector3(x + w * 0.5f, th, 12f), w * 0.22f, w * 0.9f, CathedralStone);
                }
                x += w;
                n++;
            }
            return b.Build("FarCathedral");
        }

        // ---------- Clockwork Abyss: frames, gear axles, pipes; giant machines beyond ----------

        private static Mesh BuildTrussMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float h = 16f;
            foreach (float z in new[] { -6f, 6f })
                b.Box(new Vector3(-0.4f, -14f, z - 0.4f), new Vector3(0.4f, h, z + 0.4f), BrassDark);
            for (float y = 2f; y <= h; y += 4.5f)
            {
                b.Box(new Vector3(-0.3f, y - 0.2f, -6f), new Vector3(0.3f, y + 0.2f, 6f), Brass);
                if (y + 4.5f <= h)
                    Beam(b, new Vector3(0f, y, -6f), new Vector3(0f, y + 4.5f, 6f), 0.35f, Brass);
            }
            b.Box(new Vector3(-0.8f, h, -7f), new Vector3(0.8f, h + 0.6f, 7f), BrassLight);
            b.Box(new Vector3(0.42f, h - 3f, -0.25f), new Vector3(0.5f, h - 2.4f, 0.25f), Amber);
            return b.Build("Truss");
        }

        // A machine post carrying a turning gear (added as a spinner) on an axle.
        private static Mesh BuildAxlePostMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-1.2f, -14f, -1.2f), new Vector3(1.2f, 11f, 1.2f), BrassDark);
            b.Box(new Vector3(-1.5f, 11f, -1.5f), new Vector3(1.5f, 12f, 1.5f), Brass);
            b.Box(new Vector3(-0.3f, 12f, -0.3f), new Vector3(1.3f, 14f, 0.3f), BrassLight); // the axle
            for (float y = 2f; y < 10f; y += 3f)
                b.Quad(new Vector3(1.22f, y, -0.3f), new Vector3(1.22f, y + 1f, -0.3f),
                    new Vector3(1.22f, y + 1f, 0.3f), new Vector3(1.22f, y, 0.3f), Amber);
            return b.Build("AxlePost");
        }

        private static Mesh BuildPipeRunMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float half = 16f;
            float[] ys = { 2.5f, 4.2f, 6.4f };
            float[] xs = { 0.6f, -0.4f, 0.2f };
            for (int i = 0; i < ys.Length; i++)
            {
                float r = 0.35f + i * 0.08f;
                b.Box(new Vector3(xs[i] - r, ys[i] - r, -half), new Vector3(xs[i] + r, ys[i] + r, half), i == 1 ? BrassDark : Brass);
                // Flanges and a drop into the abyss.
                for (float z = -half + 4f; z < half; z += 8f)
                    b.Box(new Vector3(xs[i] - r - 0.1f, ys[i] - r - 0.1f, z - 0.15f), new Vector3(xs[i] + r + 0.1f, ys[i] + r + 0.1f, z + 0.15f), BrassLight);
                b.Box(new Vector3(xs[i] - r, -14f, -half + 2f + i * 9f), new Vector3(xs[i] + r, ys[i], -half + 2f + i * 9f + r * 2f), BrassDark);
            }
            // Valves glowing amber.
            b.Box(new Vector3(0.85f, 2.2f, -3f), new Vector3(1.05f, 2.8f, -2.4f), Amber);
            b.Box(new Vector3(0.45f, 6.2f, 7f), new Vector3(0.75f, 6.7f, 7.6f), Amber);
            // Supports.
            for (float z = -half + 2f; z < half; z += 10f)
                b.Box(new Vector3(-0.2f, -14f, z - 0.2f), new Vector3(0.2f, 7f, z + 0.2f), BrassDark);
            return b.Build("PipeRun");
        }

        // Giant machines on the horizon: stepped blocks, stacks with a glow.
        private static Mesh BuildFarMachineMesh()
        {
            MeshBuilder b = new MeshBuilder();
            Color furnace = new Color(0.5f, 0.22f, 0.02f, 1f);
            float x = -330f;
            int n = 0;
            while (x < 330f)
            {
                float w = Random.Range(18f, 34f);
                float h = Random.Range(18f, 48f);
                float z = Random.Range(0f, 8f);
                b.Box(new Vector3(x, -40f, z), new Vector3(x + w, h, z + 14f), BrassDark);
                b.Box(new Vector3(x + w * 0.15f, h, z + 2f), new Vector3(x + w * 0.85f, h + Random.Range(4f, 10f), z + 12f), BrassDark);
                b.Box(new Vector3(x - 0.5f, h * 0.5f, z - 0.5f), new Vector3(x + w + 0.5f, h * 0.5f + 1.5f, z + 0.5f), Brass);
                if (n % 2 == 0)
                {
                    float sx = x + w * Random.Range(0.2f, 0.7f), sh = h + Random.Range(16f, 34f);
                    b.Box(new Vector3(sx, h, z + 5f), new Vector3(sx + 3.2f, sh, z + 8f), BrassDark);
                    b.Box(new Vector3(sx - 0.4f, sh - 1.5f, z + 4.6f), new Vector3(sx + 3.6f, sh, z + 8.4f), Brass);
                }
                if (Random.value < 0.6f)
                    b.Quad(new Vector3(x + w * 0.3f, h * 0.2f, z - 0.05f), new Vector3(x + w * 0.3f, h * 0.2f + 3f, z - 0.05f),
                        new Vector3(x + w * 0.7f, h * 0.2f + 3f, z - 0.05f), new Vector3(x + w * 0.7f, h * 0.2f, z - 0.05f), furnace);
                x += w + Random.Range(0f, 6f);
                n++;
            }
            return b.Build("FarMachines");
        }
    }
}
