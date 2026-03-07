using UnityEngine;

namespace TerraDrive.Terrain
{
    /// <summary>
    /// Holds the mesh data produced by <see cref="TerrainMeshGenerator.Generate"/>.
    ///
    /// <para>
    /// The arrays are sized for direct upload to a Unity <see cref="Mesh"/>:
    /// </para>
    /// <code>
    ///   TerrainMeshResult result = TerrainMeshGenerator.Generate(grid, originLat, originLon);
    ///   var mesh = new Mesh();
    ///   mesh.vertices  = result.Vertices;
    ///   mesh.triangles = result.Triangles;
    ///   mesh.uv        = result.UVs;
    ///   mesh.RecalculateNormals();
    /// </code>
    /// </summary>
    public sealed class TerrainMeshResult
    {
        /// <summary>World-space vertex positions (one per grid cell).</summary>
        public Vector3[] Vertices { get; }

        /// <summary>
        /// Triangle indices in groups of three.
        /// Each quad in the grid produces two triangles (six indices).
        /// </summary>
        public int[] Triangles { get; }

        /// <summary>
        /// UV coordinates in the [0, 1] range.  U increases west → east;
        /// V increases south → north.
        /// </summary>
        public Vector2[] UVs { get; }

        /// <summary>Initialises a new <see cref="TerrainMeshResult"/>.</summary>
        public TerrainMeshResult(Vector3[] vertices, int[] triangles, Vector2[] uvs)
        {
            Vertices  = vertices;
            Triangles = triangles;
            UVs       = uvs;
        }
    }
}
