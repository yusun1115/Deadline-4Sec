using System;
using UnityEngine;

namespace Deadline4Sec
{
    // Area / Biome (2026-11-06, AreaBiome,Pacing.md): the run travels through
    // regions in one gameplay scene. An area only changes what the course is
    // built from and how it looks; player, combat, timer and scoring are shared.
    public enum AreaType { SoulRailway, BrokenCathedral, ClockworkAbyss }

    // How an area looks: applied when the runner is inside a transition tunnel.
    [Serializable]
    public sealed class AreaLook
    {
        [Header("Sky (GothicSky)")]
        public Color skyTop = new Color(0.05f, 0.03f, 0.09f);
        public Color skyMid = new Color(0.16f, 0.09f, 0.22f);
        public Color skyHorizon = new Color(0.55f, 0.42f, 0.62f);
        public Color skyCloud = new Color(0.42f, 0.3f, 0.5f);
        public Color moon = new Color(1f, 0.93f, 0.92f);
        public Color moonGlow = new Color(0.95f, 0.55f, 0.75f);
        [Header("Fog and light")]
        public Color fog = new Color(0.2f, 0.14f, 0.27f);
        public Color ambientSky = new Color(0.5f, 0.4f, 0.66f);
        public Color ambientEquator = new Color(0.36f, 0.26f, 0.44f);
        public Color ambientGround = new Color(0.12f, 0.08f, 0.15f);
        public Color sun = new Color(0.92f, 0.82f, 1f);
        [Tooltip("Colour the background sinks into near its base (Silhouette _HazeColor).")]
        public Color haze = new Color(0.2f, 0.14f, 0.27f);
        [Tooltip("The portal at the end of the tunnel leading into this area.")]
        public Color accent = new Color(1f, 0.2f, 0.55f);
    }

    // Same gameplay box, another model: ObstacleVisual stretches it to the hazard.
    [Serializable]
    public sealed class ObstacleSkin
    {
        public GameObject model;
        public Vector3 euler;
    }

    [Serializable]
    public sealed class ObstacleSkinSet
    {
        public ObstacleSkin laneBlocker = new ObstacleSkin();
        public ObstacleSkin jumpObstacle = new ObstacleSkin();
        public ObstacleSkin slideObstacle = new ObstacleSkin();

        public ObstacleSkin For(Obstacle.ObstacleType type)
        {
            switch (type)
            {
                case Obstacle.ObstacleType.LaneBlocker: return laneBlocker;
                case Obstacle.ObstacleType.JumpObstacle: return jumpObstacle;
                case Obstacle.ObstacleType.SlideObstacle: return slideObstacle;
                default: return null;
            }
        }
    }

    [CreateAssetMenu(menuName = "Deadline 4 Sec/Area Definition", fileName = "Area")]
    public sealed class AreaDefinition : ScriptableObject
    {
        public AreaType areaId;
        public string displayName = "AREA";

        [Header("Course")]
        [Tooltip("Ordinary patterns of this area (difficulty and connection rules still apply).")]
        public CoursePattern[] patternPool;
        [Tooltip("Its signature section: wall runs, or an air route (entry, upper runs, return).")]
        public CoursePattern[] specialPatternPool;

        [Header("Length: patterns spawned before the next transition")]
        [Min(1)] public int minPatterns = 8;
        [Min(1)] public int maxPatterns = 12;

        [Header("Signature section spacing (ordinary patterns)")]
        [Min(0)] public int firstSpecialAfterPatterns = 2;
        [Min(1)] public int patternsBetweenSpecialMin = 3;
        [Min(1)] public int patternsBetweenSpecialMax = 5;

        [Header("Pacing")]
        [Tooltip("Intensity curves used here, by weight. The first one opens the area.")]
        public PacingBlock[] pacingBlocks;

        [Header("Visuals")]
        public AreaLook look = new AreaLook();
        [Tooltip("Floor, upper deck and wall panels. Empty: the spawner's own floor material.")]
        public Material floorMaterial;
        [Tooltip("Lit edges of the upper deck (air route). Empty: the patterns' own pink.")]
        public Material deckEdgeMaterial;
        [Tooltip("Set pieces in the patterns (arches, clock gates). Empty: their own stone.")]
        public Material decorMaterial;
        [Tooltip("Empty entries keep the obstacle prefab's own model.")]
        public ObstacleSkinSet obstacleSkins = new ObstacleSkinSet();

        private void OnValidate()
        {
            maxPatterns = Mathf.Max(minPatterns, maxPatterns);
            patternsBetweenSpecialMax = Mathf.Max(patternsBetweenSpecialMin, patternsBetweenSpecialMax);
        }
    }
}
