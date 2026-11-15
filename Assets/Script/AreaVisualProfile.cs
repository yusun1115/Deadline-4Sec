using System;
using UnityEngine;

namespace Deadline4Sec
{
    // Which drifting bits fill the air of an area (AreaAtmosphere).
    public enum AmbientParticleStyle { NeonDust, Ash, SteamAndSparks }

    // Visual upgrade (2026-11-15, VisualUpgrade.md): everything about an area's
    // atmosphere beyond the sky colours of AreaLook — the low fog that hides the
    // floating bottoms of the side structures, distance fog, the mid/far
    // background layers, the landmark's strength, light particles and the light
    // on the background. Blended by AreaDirector together with AreaLook, inside
    // the transition tunnel. Gameplay never reads any of it.
    [Serializable]
    public sealed class AreaVisualProfile
    {
        [Header("A. Low fog (under the track, hides structure bottoms)")]
        public Color lowFogColor = new Color(0.32f, 0.16f, 0.4f);
        [Tooltip("Second fog colour, mixed in by the noise (neon glow, red haze, warm steam).")]
        public Color lowFogGlowColor = new Color(0.85f, 0.25f, 0.65f);
        [Range(0f, 1f)] public float lowFogAlpha = 0.7f;
        [Tooltip("World height of the lowest fog sheet.")]
        public float lowFogBottom = -8.5f;
        [Tooltip("World height of the highest fog sheet (the track floor is at 0).")]
        public float lowFogTop = 0.6f;
        [Tooltip("Fog drift, world units per second (x across, y along the track).")]
        public Vector2 lowFogDrift = new Vector2(0.6f, -1.2f);
        [Tooltip("Size of the fog clumps: larger is broader.")]
        [Min(1f)] public float lowFogScale = 26f;

        [Header("B. Distance fog")]
        [Min(0f)] public float fogStart = 28f;
        [Min(1f)] public float fogEnd = 270f;
        [Tooltip("How high the background sinks into the haze colour (Silhouette _HazeHeight).")]
        public float hazeHeight = 9f;

        [Header("B. Mid / far background layers")]
        [Tooltip("Mid layer colour multiplier: below 1 is dimmer than the play layer.")]
        public Color midLayerTint = new Color(0.8f, 0.78f, 0.85f);
        [Tooltip("Far layer colour multiplier.")]
        public Color farLayerTint = new Color(0.75f, 0.7f, 0.85f);
        [Tooltip("How much of the far layer sinks into the fog (1 = all of it).")]
        [Range(0f, 1f)] public float farFogAmount = 0.82f;

        [Header("D. Landmark")]
        [Tooltip("Fog on the landmark: lower keeps it readable on the horizon.")]
        [Range(0f, 1f)] public float landmarkFogAmount = 0.45f;
        [Tooltip("Emissive boost of the landmark's lit parts.")]
        [Min(0f)] public float landmarkGlow = 3f;

        [Header("E. Light particles")]
        public AmbientParticleStyle particleStyle = AmbientParticleStyle.NeonDust;
        public Color particleColor = new Color(1f, 0.45f, 0.85f, 0.8f);
        public Color particleColorAlt = new Color(0.6f, 0.45f, 1f, 0.7f);
        [Tooltip("Particles per second around the camera.")]
        [Min(0f)] public float particleRate = 30f;

        [Header("E. Light on the background")]
        [Tooltip("Side light on the background structures (direction it comes from, world space).")]
        public Color sideLight = new Color(0.55f, 0.18f, 0.6f);
        public Vector3 sideLightFrom = new Vector3(1f, 0.6f, -0.4f);
        [Tooltip("Back light: tops and edges against the sky.")]
        public Color backLight = new Color(0.4f, 0.16f, 0.45f);

        public static AreaVisualProfile Lerp(AreaVisualProfile a, AreaVisualProfile b, float t, AreaVisualProfile into)
        {
            into.lowFogColor = Color.Lerp(a.lowFogColor, b.lowFogColor, t);
            into.lowFogGlowColor = Color.Lerp(a.lowFogGlowColor, b.lowFogGlowColor, t);
            into.lowFogAlpha = Mathf.Lerp(a.lowFogAlpha, b.lowFogAlpha, t);
            into.lowFogBottom = Mathf.Lerp(a.lowFogBottom, b.lowFogBottom, t);
            into.lowFogTop = Mathf.Lerp(a.lowFogTop, b.lowFogTop, t);
            into.lowFogDrift = Vector2.Lerp(a.lowFogDrift, b.lowFogDrift, t);
            into.lowFogScale = Mathf.Lerp(a.lowFogScale, b.lowFogScale, t);
            into.fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t);
            into.fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t);
            into.hazeHeight = Mathf.Lerp(a.hazeHeight, b.hazeHeight, t);
            into.midLayerTint = Color.Lerp(a.midLayerTint, b.midLayerTint, t);
            into.farLayerTint = Color.Lerp(a.farLayerTint, b.farLayerTint, t);
            into.farFogAmount = Mathf.Lerp(a.farFogAmount, b.farFogAmount, t);
            into.landmarkFogAmount = Mathf.Lerp(a.landmarkFogAmount, b.landmarkFogAmount, t);
            into.landmarkGlow = Mathf.Lerp(a.landmarkGlow, b.landmarkGlow, t);
            into.particleStyle = t < 0.5f ? a.particleStyle : b.particleStyle;
            into.particleColor = Color.Lerp(a.particleColor, b.particleColor, t);
            into.particleColorAlt = Color.Lerp(a.particleColorAlt, b.particleColorAlt, t);
            into.particleRate = Mathf.Lerp(a.particleRate, b.particleRate, t);
            into.sideLight = Color.Lerp(a.sideLight, b.sideLight, t);
            into.sideLightFrom = Vector3.Slerp(a.sideLightFrom.normalized, b.sideLightFrom.normalized, t);
            into.backLight = Color.Lerp(a.backLight, b.backLight, t);
            return into;
        }
    }
}
