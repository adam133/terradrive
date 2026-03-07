using System.Collections.Generic;
using UnityEngine;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Generates a smooth Catmull-Rom spline through a sequence of world-space control points.
    ///
    /// Usage:
    /// <code>
    ///   List&lt;Vector3&gt; smoothPath = SplineGenerator.BuildCatmullRom(roadNodes, samplesPerSegment: 20);
    /// </code>
    /// </summary>
    public static class SplineGenerator
    {
        /// <summary>
        /// Samples a Catmull-Rom spline through <paramref name="controlPoints"/> and returns
        /// a dense list of interpolated world-space positions.
        /// </summary>
        /// <param name="controlPoints">
        /// Ordered control points.  At least two points are required; fewer returns the input as-is.
        /// </param>
        /// <param name="samplesPerSegment">
        /// Number of sample positions generated between each pair of control points.
        /// Higher values produce smoother geometry at the cost of more vertices.
        /// </param>
        /// <returns>List of smoothed world-space positions along the spline.</returns>
        public static List<Vector3> BuildCatmullRom(IList<Vector3> controlPoints, int samplesPerSegment = 20)
        {
            var result = new List<Vector3>();

            if (controlPoints == null || controlPoints.Count < 2)
            {
                if (controlPoints != null)
                    result.AddRange(controlPoints);
                return result;
            }

            int n = controlPoints.Count;

            for (int i = 0; i < n - 1; i++)
            {
                // Phantom points at the start and end mirror the curve inward.
                Vector3 p0 = i == 0 ? controlPoints[0] + (controlPoints[0] - controlPoints[1]) : controlPoints[i - 1];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = i + 2 >= n ? controlPoints[n - 1] + (controlPoints[n - 1] - controlPoints[n - 2]) : controlPoints[i + 2];

                int steps = (i == n - 2) ? samplesPerSegment + 1 : samplesPerSegment;
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / samplesPerSegment;
                    result.Add(CatmullRomPoint(p0, p1, p2, p3, t));
                }
            }

            return result;
        }

        // ── Math ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the Catmull-Rom spline equation at parameter <paramref name="t"/> ∈ [0, 1]
        /// between control points <paramref name="p1"/> and <paramref name="p2"/>.
        /// </summary>
        private static Vector3 CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }
    }
}
