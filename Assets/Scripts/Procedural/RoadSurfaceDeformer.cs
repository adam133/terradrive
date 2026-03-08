using System;
using System.Collections.Generic;
using UnityEngine;
using TerraDrive.DataInversion;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Applies procedural Y-axis imperfections to a road spline to simulate
    /// real-world surface roughness: dips, bumps, potholes, and slight elevation
    /// differences.
    ///
    /// Roughness is scaled by road classification — major roads (motorways, trunk
    /// roads) are well-maintained and nearly flat, while minor roads (residential
    /// streets, dirt tracks) exhibit significantly more surface variation.
    ///
    /// Usage:
    /// <code>
    ///   List&lt;Vector3&gt; deformed = RoadSurfaceDeformer.Deform(splinePoints, RoadType.Residential, seed: wayId);
    ///   RoadMeshResult result   = RoadMeshExtruder.ExtrudeWithDetails(deformed, RoadType.Residential);
    /// </code>
    /// </summary>
    public static class RoadSurfaceDeformer
    {
        /// <summary>
        /// Maximum Y deviation (in metres) applied to road surface vertices,
        /// keyed by road classification.
        /// </summary>
        private static readonly Dictionary<RoadType, float> RoughnessAmplitudes =
            new Dictionary<RoadType, float>
            {
                { RoadType.Motorway,     0.005f },   // near-perfect motorway surface
                { RoadType.Trunk,        0.010f },   // well-maintained dual carriageway
                { RoadType.Primary,      0.020f },   // occasional minor irregularities
                { RoadType.Secondary,    0.030f },   // light surface variation
                { RoadType.Tertiary,     0.050f },   // noticeable undulations
                { RoadType.Residential,  0.080f },   // bumpy residential street
                { RoadType.Service,      0.100f },   // rough access lane
                { RoadType.Cycleway,     0.060f },   // cycle lane, moderate quality
                { RoadType.Dirt,         0.150f },   // unpaved track — largest variation
                { RoadType.Path,         0.120f },   // footpath, uneven surface
                { RoadType.Unknown,      0.050f },   // sensible fallback
            };

        // Noise frequency parameters (cycles per metre of road distance).
        // Two octaves are blended: broad undulations (low) + sharp bumps (high).
        private const float LowFrequency  = 0.05f;  // ~1 cycle per 20 m
        private const float HighFrequency = 0.40f;  // ~1 cycle per 2.5 m

        // Contribution weights of the two octaves (must sum to 1).
        private const float LowOctaveWeight  = 0.70f;
        private const float HighOctaveWeight = 0.30f;

        /// <summary>
        /// Returns the maximum Y deviation (in metres) used for surface deformation
        /// of the given <paramref name="roadType"/>.
        /// </summary>
        public static float GetRoughnessAmplitude(RoadType roadType) =>
            RoughnessAmplitudes.TryGetValue(roadType, out float amp) ? amp : 0.05f;

        /// <summary>
        /// Returns a new list of spline points with Y perturbations applied to
        /// simulate road surface imperfections.  X and Z coordinates are unchanged;
        /// only Y (height) varies.
        /// </summary>
        /// <param name="splinePoints">
        /// Input centre-line spline positions.  At least two points are required;
        /// fewer returns an empty list.
        /// </param>
        /// <param name="roadType">
        /// Road classification used to look up the roughness amplitude.
        /// </param>
        /// <param name="seed">
        /// Integer seed that makes the deformation deterministic and unique per road
        /// segment (e.g. the OSM way ID cast to int is a good choice).
        /// </param>
        /// <returns>
        /// New <see cref="List{Vector3}"/> of the same length as
        /// <paramref name="splinePoints"/> with Y modified by noise.
        /// </returns>
        public static List<Vector3> Deform(IList<Vector3> splinePoints, RoadType roadType, int seed)
        {
            if (splinePoints == null || splinePoints.Count < 2)
                return new List<Vector3>();

            float amplitude = GetRoughnessAmplitude(roadType);
            var result = new List<Vector3>(splinePoints.Count);

            float distAlongRoad = 0f;
            for (int i = 0; i < splinePoints.Count; i++)
            {
                if (i > 0)
                    distAlongRoad += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);

                float lowNoise  = ValueNoise(distAlongRoad * LowFrequency,  seed);
                float highNoise = ValueNoise(distAlongRoad * HighFrequency, seed + 1);
                float dy = amplitude * (lowNoise * LowOctaveWeight + highNoise * HighOctaveWeight);

                Vector3 p = splinePoints[i];
                result.Add(new Vector3(p.x, p.y + dy, p.z));
            }

            return result;
        }

        // ── Private noise helpers ─────────────────────────────────────────────

        /// <summary>
        /// Smoothly interpolated 1-D value noise.  Returns a value in approximately [-1, 1].
        /// </summary>
        private static float ValueNoise(float t, int seed)
        {
            int   i0   = (int)Math.Floor(t);
            float frac = t - i0;

            // Cubic Hermite smoothstep: 3t² − 2t³
            float smooth = frac * frac * (3f - 2f * frac);

            float v0 = HashToFloat(i0,     seed);
            float v1 = HashToFloat(i0 + 1, seed);

            return v0 + (v1 - v0) * smooth;
        }

        /// <summary>
        /// Maps an integer index and seed to a reproducible float in approximately
        /// [-1, 1] using a fast integer hash.
        /// </summary>
        private static float HashToFloat(int n, int seed)
        {
            unchecked
            {
                uint h = (uint)(n * 1664525 + seed * 22695477 + 1013904223);
                h ^= h >> 16;
                h *= 0x45d9f3bu;
                h ^= h >> 16;
                return (h / (float)uint.MaxValue) * 2f - 1f;
            }
        }
    }
}
