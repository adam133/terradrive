using System.Collections.Generic;
using UnityEngine;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Extrudes a UV-mapped road mesh along a sequence of spline positions.
    ///
    /// Usage:
    /// <code>
    ///   Mesh roadMesh = RoadMeshExtruder.Extrude(splinePoints, roadWidth: 7f);
    ///   GetComponent&lt;MeshFilter&gt;().sharedMesh = roadMesh;
    /// </code>
    /// </summary>
    public static class RoadMeshExtruder
    {
        /// <summary>
        /// Generates a flat road mesh extruded along <paramref name="splinePoints"/>.
        /// </summary>
        /// <param name="splinePoints">
        /// Ordered world-space centre-line positions along the road.
        /// Requires at least two points.
        /// </param>
        /// <param name="roadWidth">Total road width in metres.</param>
        /// <param name="uvTileLength">
        /// World-space length (in metres) after which the road texture repeats along the U axis.
        /// Default is 10 m — matching a standard asphalt texture tile.
        /// </param>
        /// <returns>A <see cref="Mesh"/> ready to assign to a <c>MeshFilter</c>.</returns>
        public static Mesh Extrude(IList<Vector3> splinePoints, float roadWidth = 7f, float uvTileLength = 10f)
        {
            if (splinePoints == null || splinePoints.Count < 2)
            {
                Debug.LogWarning("[RoadMeshExtruder] Need at least 2 spline points.");
                return new Mesh();
            }

            int n = splinePoints.Count;
            var vertices = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var triangles = new int[(n - 1) * 6];

            float halfWidth = roadWidth * 0.5f;
            float distanceAlongRoad = 0f;

            for (int i = 0; i < n; i++)
            {
                // Tangent direction along the spline.
                Vector3 tangent;
                if (i == 0)
                    tangent = (splinePoints[1] - splinePoints[0]).normalized;
                else if (i == n - 1)
                    tangent = (splinePoints[n - 1] - splinePoints[n - 2]).normalized;
                else
                    tangent = (splinePoints[i + 1] - splinePoints[i - 1]).normalized;

                // Road-perpendicular direction on the XZ plane.
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

                vertices[i * 2]     = splinePoints[i] - right * halfWidth;  // left edge
                vertices[i * 2 + 1] = splinePoints[i] + right * halfWidth;  // right edge

                // Accumulate road distance for V-coordinate tiling.
                if (i > 0)
                    distanceAlongRoad += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);

                float v = distanceAlongRoad / uvTileLength;
                uvs[i * 2]     = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            // Build quads from consecutive edge pairs.
            for (int i = 0; i < n - 1; i++)
            {
                int tri = i * 6;
                int v0 = i * 2;

                triangles[tri]     = v0;
                triangles[tri + 1] = v0 + 2;
                triangles[tri + 2] = v0 + 1;

                triangles[tri + 3] = v0 + 1;
                triangles[tri + 4] = v0 + 2;
                triangles[tri + 5] = v0 + 3;
            }

            var mesh = new Mesh { name = "RoadMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
