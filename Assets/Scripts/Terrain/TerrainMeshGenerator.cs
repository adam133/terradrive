using System;
using UnityEngine;
using TerraDrive.Core;

namespace TerraDrive.Terrain
{
    /// <summary>
    /// Generates a Unity-compatible heightfield mesh from an <see cref="ElevationGrid"/>.
    ///
    /// <para>
    /// Each grid cell becomes a quad split into two triangles.  Vertex positions are
    /// computed with
    /// <see cref="CoordinateConverter.LatLonToUnity(double,double,double,double,double)"/>
    /// so the X and Z axes carry the horizontal offset in metres from the map origin and the
    /// Y axis carries the terrain elevation above sea level in metres.
    /// </para>
    ///
    /// <para>
    /// UV coordinates run from (0, 0) at the south-west corner to (1, 1) at the north-east
    /// corner so that a single tiled texture can cover the whole terrain patch.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   ElevationGrid grid = await ElevationGrid.SampleAsync(
    ///       minLat, maxLat, minLon, maxLon, rows: 64, cols: 64, elevationSource);
    ///
    ///   TerrainMeshResult result = TerrainMeshGenerator.Generate(grid, originLat, originLon);
    ///
    ///   var mesh = new Mesh();
    ///   mesh.vertices  = result.Vertices;
    ///   mesh.triangles = result.Triangles;
    ///   mesh.uv        = result.UVs;
    ///   mesh.RecalculateNormals();
    ///   GetComponent&lt;MeshFilter&gt;().sharedMesh = mesh;
    /// </code>
    /// </summary>
    public static class TerrainMeshGenerator
    {
        /// <summary>
        /// Generates a terrain mesh from the supplied elevation grid.
        /// </summary>
        /// <param name="grid">
        /// Regular lat/lon elevation grid produced by <see cref="ElevationGrid.SampleAsync"/>
        /// or constructed manually.
        /// </param>
        /// <param name="originLat">Map origin latitude — maps to world (0, *, 0).</param>
        /// <param name="originLon">Map origin longitude — maps to world (0, *, 0).</param>
        /// <returns>
        /// A <see cref="TerrainMeshResult"/> whose arrays can be assigned directly to a
        /// Unity <see cref="Mesh"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="grid"/> is <c>null</c>.
        /// </exception>
        public static TerrainMeshResult Generate(
            ElevationGrid grid,
            double originLat,
            double originLon)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            int rows      = grid.Rows;
            int cols      = grid.Cols;
            int vertCount = rows * cols;

            var vertices  = new Vector3[vertCount];
            var uvs       = new Vector2[vertCount];

            for (int r = 0; r < rows; r++)
            {
                double lat = grid.LatAtRow(r);
                float  v   = (float)r / (rows - 1);

                for (int c = 0; c < cols; c++)
                {
                    double lon  = grid.LonAtCol(c);
                    double elev = grid[r, c];
                    float  u    = (float)c / (cols - 1);

                    int idx = r * cols + c;
                    vertices[idx] = CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon, elev);
                    uvs[idx]      = new Vector2(u, v);
                }
            }

            // (rows-1) × (cols-1) quads, each split into 2 triangles → 6 indices per quad.
            int quadCount = (rows - 1) * (cols - 1);
            var triangles = new int[quadCount * 6];
            int ti = 0;

            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int bl = r * cols + c;      // bottom-left  (south-west)
                    int br = bl + 1;            // bottom-right (south-east)
                    int tl = bl + cols;         // top-left     (north-west)
                    int tr = tl + 1;            // top-right    (north-east)

                    // Triangle 1: bl → tl → tr
                    triangles[ti++] = bl;
                    triangles[ti++] = tl;
                    triangles[ti++] = tr;

                    // Triangle 2: bl → tr → br
                    triangles[ti++] = bl;
                    triangles[ti++] = tr;
                    triangles[ti++] = br;
                }
            }

            return new TerrainMeshResult(vertices, triangles, uvs);
        }
    }
}
