using System;
using System.Collections.Generic;
using UnityEngine;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Applies smooth vertical elevation to a spline to represent a bridge or overpass.
    ///
    /// Roads tagged as bridges in OSM should be visually elevated above the surrounding
    /// terrain.  This class takes the flat (or terrain-sampled) spline centre-line and
    /// returns a new list of points whose Y coordinates ramp smoothly up at the approach,
    /// stay at the full bridge height through the span, and ramp back down at the
    /// departure — using a smooth-step curve to avoid sharp kinks.
    ///
    /// Usage:
    /// <code>
    ///   if (road.IsBridge)
    ///   {
    ///       splinePoints = BridgeElevator.ApplyElevation(splinePoints);
    ///   }
    ///   RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, road.RoadType);
    /// </code>
    /// </summary>
    public static class BridgeElevator
    {
        /// <summary>
        /// Default height (in metres) by which the bridge centreline is raised above the
        /// surrounding surface.  Approximately matches a standard road-over-road clearance.
        /// </summary>
        public const float DefaultBridgeHeight = 4.5f;

        /// <summary>
        /// Fraction of the total spline length used for the approach ramp (and symmetrically
        /// for the departure ramp).  A value of <c>0.2</c> means the first 20 % of points
        /// ascend, the middle 60 % are at full height, and the last 20 % descend.
        /// </summary>
        public const float DefaultRampFraction = 0.2f;

        /// <summary>
        /// Maximum allowed grade for a bridge approach or departure ramp, expressed as a
        /// fraction (0.15 = 15 %).  When the requested <see cref="DefaultRampFraction"/>
        /// would produce a steeper ramp the fraction is automatically widened until the
        /// grade constraint is satisfied.
        /// </summary>
        public const float MaxRampGrade = 0.15f;

        /// <summary>
        /// Returns a new list of spline points with Y coordinates smoothly elevated to
        /// represent a bridge or overpass.
        ///
        /// <list type="bullet">
        ///   <item>Points in the first <paramref name="rampFraction"/> of the spline ramp
        ///   up from their original Y using a smooth-step (cubic Hermite) curve.</item>
        ///   <item>Points in the middle section sit at full
        ///   <c>original Y + <paramref name="bridgeHeight"/></c>.</item>
        ///   <item>Points in the last <paramref name="rampFraction"/> descend
        ///   symmetrically.</item>
        /// </list>
        ///
        /// The input points are not modified; a new <see cref="List{T}"/> is returned.
        /// </summary>
        /// <param name="splinePoints">
        /// Ordered world-space centre-line positions.  A <c>null</c> or empty list returns
        /// an empty list without throwing.
        /// </param>
        /// <param name="bridgeHeight">
        /// Height in metres to add at the peak of the bridge.  Defaults to
        /// <see cref="DefaultBridgeHeight"/>.
        /// </param>
        /// <param name="rampFraction">
        /// Fraction (0..0.5) of the spline used for each approach/departure ramp.
        /// Defaults to <see cref="DefaultRampFraction"/>.
        /// </param>
        /// <returns>A new <see cref="List{Vector3}"/> with elevated Y coordinates.</returns>
        public static List<Vector3> ApplyElevation(
            IList<Vector3> splinePoints,
            float bridgeHeight  = DefaultBridgeHeight,
            float rampFraction  = DefaultRampFraction)
        {
            if (splinePoints == null || splinePoints.Count == 0)
                return new List<Vector3>();

            // Clamp rampFraction so neither ramp exceeds half the spline.
            rampFraction = Math.Clamp(rampFraction, 0f, 0.5f);

            // Enforce the maximum ramp grade: rise / horizontal-run ≤ MaxRampGrade.
            // Extend rampFraction if necessary so the ramp is long enough.
            if (bridgeHeight > 0f)
            {
                float totalLength = ComputeXZLength(splinePoints);
                if (totalLength > 0f)
                {
                    float minRampFraction = (bridgeHeight / MaxRampGrade) / totalLength;
                    rampFraction = Math.Max(rampFraction, Math.Clamp(minRampFraction, 0f, 0.5f));
                }
            }

            int n = splinePoints.Count;
            var result = new List<Vector3>(n);

            for (int i = 0; i < n; i++)
            {
                // t = normalised position along the spline, 0 at start, 1 at end.
                float t = (n == 1) ? 0f : (float)i / (n - 1);
                float elevFactor = ComputeElevationFactor(t, rampFraction);

                Vector3 p = splinePoints[i];
                result.Add(new Vector3(p.x, p.y + bridgeHeight * elevFactor, p.z));
            }

            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Returns a 0..1 blending factor for the bridge elevation at normalised spline
        /// position <paramref name="t"/> (0 = start, 1 = end).
        /// </summary>
        internal static float ComputeElevationFactor(float t, float rampFraction)
        {
            if (rampFraction <= 0f)
                return 1f;

            if (t <= rampFraction)
            {
                // Ascending ramp: 0 at start → 1 at end of ramp.
                float localT = t / rampFraction;
                return SmoothStep(localT);
            }

            if (t >= 1f - rampFraction)
            {
                // Descending ramp: 1 at start of ramp → 0 at end.
                float localT = (1f - t) / rampFraction;
                return SmoothStep(localT);
            }

            // Middle span — fully elevated.
            return 1f;
        }

        /// <summary>
        /// Returns the total arc length of <paramref name="points"/> projected onto the
        /// XZ plane, ignoring the Y (elevation) component.  Used for grade calculations.
        /// </summary>
        private static float ComputeXZLength(IList<Vector3> points)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float dx = points[i].x - points[i - 1].x;
                float dz = points[i].z - points[i - 1].z;
                total += (float)Math.Sqrt(dx * dx + dz * dz);
            }
            return total;
        }

        /// <summary>
        /// Cubic smooth-step: maps [0,1] → [0,1] with zero first-derivatives at both
        /// ends, producing a smooth S-shaped transition.
        /// </summary>
        private static float SmoothStep(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
