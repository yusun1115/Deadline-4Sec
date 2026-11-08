using UnityEngine;

namespace Deadline4Sec
{
    // Area backdrops. Same recipe as the railway (pillars by the track, far
    // towers, one landmark on the horizon, the cloud sea), other shapes: all of
    // it one material and vertex colours, alpha > 0.5 meaning emissive.
    public sealed partial class EnvironmentScroller
    {
        // ---------- Broken Cathedral: ruined nave, stained glass, red dusk ----------

        private static readonly Color CathedralStone = new Color(0.1f, 0.06f, 0.09f, 0f);
        private static readonly Color CathedralLight = new Color(0.2f, 0.12f, 0.16f, 0f);
        private static readonly Color[] StainedGlass =
        {
            // Dark on purpose: the emissive boost and bloom bring them up to full colour.
            new Color(0.42f, 0.03f, 0.06f, 1f),
            new Color(0.2f, 0.04f, 0.36f, 1f),
            new Color(0.46f, 0.2f, 0.03f, 1f),
            new Color(0.05f, 0.09f, 0.4f, 1f),
        };

        private void BuildCathedral(float startZ)
        {
            Mesh[] pillarMeshes = { BuildBrokenColumnMesh(9f, true), BuildBrokenColumnMesh(5.5f, false), BuildBrokenColumnMesh(7.5f, true) };
            for (int i = 0; i < pillarCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform p = Spawn("Column", pillarMeshes[i % pillarMeshes.Length]);
                p.position = new Vector3(trackX + side * pillarOffsetX, 0f,
                    startZ - 10f + (i / 2) * pillarSpacing + (side > 0 ? pillarSpacing * 0.5f : 0f));
                building.Pillars.Add(p);
            }

            Mesh[] towerMeshes = new Mesh[6];
            for (int i = 0; i < towerMeshes.Length; i++)
                towerMeshes[i] = i % 3 == 2
                    ? BuildBrokenBridgeMesh(Random.Range(5, 8))
                    : BuildRuinedTowerMesh(Random.Range(5f, 9f), Random.Range(18f, 46f), i % 3 == 1);
            for (int i = 0; i < spireCount; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                Transform s = Spawn("Ruin", towerMeshes[i % towerMeshes.Length]);
                s.position = new Vector3(trackX + side * Random.Range(spireDistanceX.x, spireDistanceX.y),
                    Random.Range(-10f, -4f), startZ + Random.Range(-20f, spireSpan));
                s.rotation = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
                float k = Random.Range(0.8f, 1.3f);
                s.localScale = new Vector3(k, k, k);
                building.Spires.Add(s);
            }

            building.Landmark = Spawn("CathedralFacade", BuildCathedralFacadeMesh());
            building.LandmarkOffset = new Vector3(-14f, -10f, 240f);
            building.CloudSea = Spawn("CloudSea", BuildCloudSeaMesh(new Color(0.27f, 0.1f, 0.17f, 0f)));
        }

        // A lancet window on a -Z facing wall: panes of stained glass and a point.
        private static void Lancet(MeshBuilder b, float x, float y, float z, float width, float height, int offset)
        {
            int rows = Mathf.Max(2, Mathf.RoundToInt(height / width));
            float pane = height / rows;
            for (int r = 0; r < rows; r++)
                b.Quad(new Vector3(x - width * 0.5f, y + r * pane, z), new Vector3(x - width * 0.5f, y + (r + 1) * pane - 0.04f, z),
                    new Vector3(x + width * 0.5f, y + (r + 1) * pane - 0.04f, z), new Vector3(x + width * 0.5f, y + r * pane, z),
                    StainedGlass[(r + offset) % StainedGlass.Length]);
            b.Tri(new Vector3(x - width * 0.5f, y + height, z), new Vector3(x, y + height + width, z),
                new Vector3(x + width * 0.5f, y + height, z), StainedGlass[offset % StainedGlass.Length]);
        }

        // A rose window: rings of coloured petals inside a stone rim, facing -Z.
        private static void RoseWindow(MeshBuilder b, Vector3 center, float radius, Color rim)
        {
            b.Ring(center + new Vector3(0f, 0f, 0.02f), radius, radius * 1.14f, 32, rim);
            int petals = 12;
            for (int ring = 0; ring < 3; ring++)
            {
                float inner = radius * ring / 3f + radius * 0.03f;
                float outer = radius * (ring + 1) / 3f - radius * 0.03f;
                for (int i = 0; i < petals; i++)
                {
                    float a0 = (i + 0.06f) * Mathf.PI * 2f / petals, a1 = (i + 0.94f) * Mathf.PI * 2f / petals;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    b.Quad(center + d0 * inner, center + d0 * outer, center + d1 * outer, center + d1 * inner,
                        StainedGlass[(i + ring) % StainedGlass.Length]);
                }
            }
        }

        private static Mesh BuildBrokenColumnMesh(float height, bool glass)
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-1f, -6f, -1f), new Vector3(1f, 0.8f, 1f), CathedralLight);
            b.Box(new Vector3(-0.72f, 0.8f, -0.72f), new Vector3(0.72f, height, 0.72f), CathedralStone);
            // The break: uneven stumps on top, a fallen block leaning at the base.
            b.Box(new Vector3(-0.72f, height, -0.72f), new Vector3(0.05f, height + 1.3f, 0.3f), CathedralStone);
            b.Box(new Vector3(0.05f, height, -0.72f), new Vector3(0.72f, height + 0.55f, 0.72f), CathedralLight);
            b.Box(new Vector3(-0.3f, height + 0.4f, 0.3f), new Vector3(0.3f, height + 0.9f, 0.72f), CathedralStone);
            b.Box(new Vector3(0.75f, 0.8f, -1.3f), new Vector3(1.5f, 1.5f, -0.5f), CathedralLight);
            if (glass)
            {
                Lancet(b, 0f, 3.2f, -0.74f, 0.42f, Mathf.Max(1.2f, height - 6.2f), 0);
            }
            return b.Build("BrokenColumn");
        }

        private static Mesh BuildRuinedTowerMesh(float width, float height, bool intact)
        {
            MeshBuilder b = new MeshBuilder();
            float hw = width * 0.5f;
            b.Box(new Vector3(-hw, -20f, -hw), new Vector3(hw, height, hw), CathedralStone);
            b.Box(new Vector3(-hw - 0.5f, height * 0.55f, -hw - 0.5f), new Vector3(hw + 0.5f, height * 0.55f + 0.7f, hw + 0.5f), CathedralLight);
            if (intact)
            {
                b.Box(new Vector3(-hw - 0.4f, height, -hw - 0.4f), new Vector3(hw + 0.4f, height + 0.8f, hw + 0.4f), CathedralLight);
                b.Pyramid(new Vector3(0f, height + 0.8f, 0f), hw * 0.9f, width * 2.4f, CathedralStone);
            }
            else
            {
                // Open to the sky: jagged wall stumps of different heights.
                int teeth = 5;
                for (int i = 0; i < teeth; i++)
                {
                    float x0 = -hw + width * i / teeth, x1 = x0 + width / teeth;
                    float h = Random.Range(0.6f, width * 0.9f);
                    b.Box(new Vector3(x0, height, -hw), new Vector3(x1, height + h, -hw + 0.8f), CathedralStone);
                    float h2 = Random.Range(0.4f, width * 0.6f);
                    b.Box(new Vector3(x0, height, hw - 0.8f), new Vector3(x1, height + h2, hw), CathedralLight);
                }
            }
            RoseWindow(b, new Vector3(0f, height * 0.72f, -hw - 0.03f), Mathf.Min(hw * 0.72f, 4f), CathedralLight);
            for (int i = -1; i <= 1; i += 2)
                Lancet(b, i * hw * 0.45f, height * 0.18f, -hw - 0.03f, width * 0.16f, height * 0.26f, i + 1);
            return b.Build("RuinedTower");
        }

        // A viaduct that stops in mid air: the broken bridges of the abbey.
        private static Mesh BuildBrokenBridgeMesh(int arches)
        {
            MeshBuilder b = new MeshBuilder();
            float span = 9f, deck = 20f;
            int missing = arches / 2;
            for (int i = 0; i < arches; i++)
            {
                if (i == missing)
                    continue;
                float z = (i - arches * 0.5f) * span;
                b.Box(new Vector3(-1.4f, -20f, z - 1.2f), new Vector3(1.4f, deck, z + 1.2f), CathedralStone);
                // The stepped arch toward the next pier.
                if (i + 1 < arches && i + 1 != missing)
                    for (int s = 0; s < 6; s++)
                    {
                        float t = (s + 0.5f) / 6f;
                        float y = deck - 4.5f + Mathf.Sin(t * Mathf.PI) * 3.5f;
                        float zs = z + t * span;
                        b.Box(new Vector3(-1.2f, y, zs - span / 12f), new Vector3(1.2f, deck, zs + span / 12f), CathedralLight);
                    }
            }
            return b.Build("BrokenBridge");
        }

        // The cathedral on the horizon: two towers (one broken), a gabled nave and
        // a huge rose window, always looming ahead like the railway's clock tower.
        private static Mesh BuildCathedralFacadeMesh()
        {
            MeshBuilder b = new MeshBuilder();
            float front = -7f;
            b.Box(new Vector3(-26f, -30f, -7f), new Vector3(26f, 62f, 12f), CathedralStone);
            b.Tri(new Vector3(-26f, 62f, front), new Vector3(0f, 90f, front), new Vector3(26f, 62f, front), CathedralStone);
            b.Tri(new Vector3(26f, 62f, 12f), new Vector3(0f, 90f, 12f), new Vector3(-26f, 62f, 12f), CathedralStone);
            b.Box(new Vector3(-27f, 60f, front - 0.8f), new Vector3(27f, 62f, front), CathedralLight);

            // Left tower with its spire, right tower broken off.
            b.Box(new Vector3(-40f, -30f, -9f), new Vector3(-24f, 112f, 9f), CathedralStone);
            b.Pyramid(new Vector3(-32f, 112f, 0f), 8.5f, 44f, CathedralStone);
            b.Box(new Vector3(24f, -30f, -9f), new Vector3(40f, 78f, 9f), CathedralStone);
            for (int i = 0; i < 6; i++)
            {
                float x0 = 24f + i * 16f / 6f;
                b.Box(new Vector3(x0, 78f, -9f), new Vector3(x0 + 16f / 6f, 78f + Random.Range(2f, 15f), -6f), CathedralStone);
            }

            RoseWindow(b, new Vector3(0f, 42f, front - 0.05f), 15f, CathedralLight);
            for (int i = 0; i < 5; i++)
                Lancet(b, -14f + i * 7f, 5f, front - 0.05f, 3f, 13f, i);
            foreach (float x in new[] { -32f, 32f })
            {
                Lancet(b, x - 3f, 30f, -9.05f, 2.4f, 22f, 0);
                Lancet(b, x + 3f, 30f, -9.05f, 2.4f, 22f, 2);
            }
            return b.Build("CathedralFacade");
        }

        // ---------- Clockwork Abyss: gears over a golden void ----------

        private static readonly Color BrassDark = new Color(0.09f, 0.06f, 0.035f, 0f);
        private static readonly Color Brass = new Color(0.2f, 0.13f, 0.06f, 0f);
        private static readonly Color BrassLight = new Color(0.34f, 0.23f, 0.09f, 0f);
        private static readonly Color Amber = new Color(0.55f, 0.28f, 0.03f, 1f);

        private void BuildClockwork(float startZ)
        {
            Mesh girder = BuildGirderMesh();
            Mesh trackGear = BuildGearMesh(1.5f, 10, Brass, BrassDark);
            for (int i = 0; i < pillarCount; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform p = Spawn("Girder", girder);
                p.position = new Vector3(trackX + side * pillarOffsetX, 0f,
                    startZ - 10f + (i / 2) * pillarSpacing + (side > 0 ? pillarSpacing * 0.5f : 0f));
                building.Pillars.Add(p);
                // Every other one carries a turning gear, in reach of the eye.
                if (i % 4 < 2)
                    AddGear(p, trackGear, new Vector3(0f, 8.6f, -0.5f), side * 50f);
            }

            Mesh tower = BuildMachineTowerMesh(6f, 34f);
            Mesh platform = BuildFloatingPlatformMesh();
            Mesh farGear = BuildGearMesh(9f, 16, Brass, BrassDark);
            Mesh smallGear = BuildGearMesh(4f, 11, BrassLight, BrassDark);
            for (int i = 0; i < spireCount; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                int kind = i % 3;
                Transform s = Spawn(kind == 0 ? "MachineTower" : kind == 1 ? "FloatingPlatform" : "Gear",
                    kind == 0 ? tower : kind == 1 ? platform : farGear);
                float y = kind == 0 ? Random.Range(-10f, -4f) : Random.Range(6f, 34f);
                s.position = new Vector3(trackX + side * Random.Range(spireDistanceX.x, spireDistanceX.y),
                    y, startZ + Random.Range(-20f, spireSpan));
                float k = Random.Range(0.8f, 1.3f);
                s.localScale = new Vector3(k, k, k);
                building.Spires.Add(s);
                if (kind == 0)
                    AddGear(s, smallGear, new Vector3(0f, 24f, -3.1f), side * 30f);
                else if (kind == 1)
                    AddGear(s, smallGear, new Vector3(0f, -2.5f, -0.2f), -side * 36f);
                else
                    building.Spinners.Add(new Spinner { Part = s, Speed = side * 14f });
            }

            // The great clock on the horizon: meshing wheels in a frame, one dial.
            Transform core = Spawn("ClockworkCore", BuildClockworkFrameMesh());
            building.Landmark = core;
            building.LandmarkOffset = new Vector3(6f, 22f, 250f);
            AddGear(core, BuildGearMesh(36f, 28, Brass, BrassDark), new Vector3(0f, 34f, -2f), 8f);
            AddGear(core, BuildGearMesh(20f, 16, BrassLight, BrassDark), new Vector3(50f, 58f, -1f), -14f);
            AddGear(core, BuildGearMesh(16f, 13, BrassLight, BrassDark), new Vector3(-46f, 14f, -1f), -17.2f);
            Transform dial = Spawn("ClockworkDial", BuildDialMesh());
            dial.SetParent(core, false);
            dial.localPosition = new Vector3(0f, 34f, -3f);
            building.CloudSea = Spawn("CloudSea", BuildCloudSeaMesh(new Color(0.1f, 0.055f, 0.03f, 0f)));
        }

        private void AddGear(Transform parent, Mesh mesh, Vector3 localPosition, float speed)
        {
            Transform gear = Spawn("Gear", mesh);
            gear.SetParent(parent, false);
            gear.localPosition = localPosition;
            building.Spinners.Add(new Spinner { Part = gear, Speed = speed });
        }

        // A flat wheel facing -Z: rim with teeth, spokes, hub, amber centre.
        private static Mesh BuildGearMesh(float radius, int teeth, Color body, Color spokeColor)
        {
            MeshBuilder b = new MeshBuilder();
            float rimIn = radius * 0.72f, rimOut = radius * 0.9f, hub = radius * 0.22f;
            b.Ring(Vector3.zero, rimIn, rimOut, teeth * 4, body);
            b.Ring(Vector3.zero, hub, hub * 1.35f, 16, body);
            b.Ring(new Vector3(0f, 0f, -0.02f), 0f, hub, 16, Amber);
            for (int i = 0; i < teeth; i++)
            {
                float a = i * Mathf.PI * 2f / teeth, w = Mathf.PI / teeth * 0.55f;
                Vector3 d0 = new Vector3(Mathf.Cos(a - w), Mathf.Sin(a - w), 0f), d1 = new Vector3(Mathf.Cos(a + w), Mathf.Sin(a + w), 0f);
                b.Quad(d0 * rimOut, d0 * radius, d1 * radius, d1 * rimOut, body);
            }
            int spokes = Mathf.Max(4, teeth / 3);
            for (int i = 0; i < spokes; i++)
            {
                float a = i * Mathf.PI * 2f / spokes;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), n = new Vector3(-d.y, d.x, 0f) * radius * 0.05f;
                b.Quad(d * hub - n, d * rimIn - n, d * rimIn + n, d * hub + n, spokeColor);
            }
            return b.Build("Gear");
        }

        private static Mesh BuildGirderMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-0.9f, -6f, -0.9f), new Vector3(0.9f, 0.5f, 0.9f), BrassDark);
            foreach (float x in new[] { -0.45f, 0.45f })
                b.Box(new Vector3(x - 0.14f, 0.5f, -0.14f), new Vector3(x + 0.14f, 10f, 0.14f), Brass);
            for (float y = 1.2f; y < 9.6f; y += 1.5f)
                b.Box(new Vector3(-0.45f, y, -0.1f), new Vector3(0.45f, y + 0.16f, 0.1f), BrassLight);
            b.Box(new Vector3(-0.8f, 10f, -0.5f), new Vector3(0.8f, 10.4f, 0.5f), BrassLight);
            b.Box(new Vector3(-0.18f, 5.2f, -0.2f), new Vector3(0.18f, 6f, -0.14f), Amber);
            return b.Build("Girder");
        }

        private static Mesh BuildMachineTowerMesh(float width, float height)
        {
            MeshBuilder b = new MeshBuilder();
            float hw = width * 0.5f;
            b.Box(new Vector3(-hw, -20f, -hw), new Vector3(hw, height, hw), BrassDark);
            for (float y = 4f; y < height; y += 8f)
                b.Box(new Vector3(-hw - 0.4f, y, -hw - 0.4f), new Vector3(hw + 0.4f, y + 0.8f, hw + 0.4f), Brass);
            b.Box(new Vector3(-hw + 1f, height, -hw + 1f), new Vector3(hw - 1f, height + 5f, hw - 1f), Brass);
            for (float y = 8f; y < height - 14f; y += 8f)
                foreach (float x in new[] { -hw * 0.5f, hw * 0.5f })
                    b.Quad(new Vector3(x - 0.4f, y, -hw - 0.03f), new Vector3(x - 0.4f, y + 3f, -hw - 0.03f),
                        new Vector3(x + 0.4f, y + 3f, -hw - 0.03f), new Vector3(x + 0.4f, y, -hw - 0.03f), Amber);
            return b.Build("MachineTower");
        }

        // Hanging in the void, with pipes and a lit underside.
        private static Mesh BuildFloatingPlatformMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-7f, -0.8f, -5f), new Vector3(7f, 0.6f, 5f), Brass);
            b.Box(new Vector3(-4f, -2.2f, -3f), new Vector3(4f, -0.8f, 3f), BrassDark);
            b.Box(new Vector3(-7.2f, 0.6f, -5.2f), new Vector3(7.2f, 0.9f, -4.9f), BrassLight);
            b.Box(new Vector3(-2f, 0.6f, -2f), new Vector3(2f, 6f, 2f), BrassDark);
            b.Pyramid(new Vector3(0f, 6f, 0f), 2.2f, 4f, Brass);
            b.Quad(new Vector3(-3.6f, -2.21f, -2.6f), new Vector3(-3.6f, -2.21f, 2.6f),
                new Vector3(3.6f, -2.21f, 2.6f), new Vector3(3.6f, -2.21f, -2.6f), Amber);
            return b.Build("FloatingPlatform");
        }

        private static Mesh BuildClockworkFrameMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Box(new Vector3(-8f, -60f, 2f), new Vector3(8f, 34f, 8f), BrassDark);
            foreach (float x in new[] { -60f, 60f })
                b.Box(new Vector3(x - 4f, -60f, 2f), new Vector3(x + 4f, 96f, 7f), BrassDark);
            b.Box(new Vector3(-64f, 88f, 2f), new Vector3(64f, 94f, 7f), Brass);
            b.Box(new Vector3(-64f, -24f, 2f), new Vector3(64f, -20f, 7f), Brass);
            return b.Build("ClockworkFrame");
        }

        private static Mesh BuildDialMesh()
        {
            MeshBuilder b = new MeshBuilder();
            b.Ring(Vector3.zero, 0f, 15f, 40, BrassDark);
            b.Ring(new Vector3(0f, 0f, -0.05f), 14f, 15f, 40, Amber);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), n = new Vector3(-d.y, d.x, 0f) * 0.5f;
                Vector3 z = new Vector3(0f, 0f, -0.08f);
                b.Quad(d * 11f - n + z, d * 13.4f - n + z, d * 13.4f + n + z, d * 11f + n + z, Amber);
            }
            return b.Build("ClockworkDial");
        }

        private static Mesh BuildCloudSeaMesh(Color haze)
        {
            MeshBuilder b = new MeshBuilder();
            b.Quad(new Vector3(-400f, 0f, -250f), new Vector3(-400f, 0f, 250f),
                new Vector3(400f, 0f, 250f), new Vector3(400f, 0f, -250f), haze);
            return b.Build("CloudSea");
        }
    }
}
