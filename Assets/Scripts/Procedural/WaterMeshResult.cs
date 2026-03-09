using UnityEngine;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Holds the mesh and texture identifier produced by
    /// <see cref="WaterMeshGenerator.Generate"/>.
    /// </summary>
    public readonly struct WaterMeshResult
    {
        /// <summary>
        /// Flat water-surface mesh.
        /// UV channel 0 = water texture tiling (U and V tile by world-space X and Z,
        /// scaled by <c>WaterMeshGenerator.UvScale</c>).
        /// </summary>
        public readonly Mesh Mesh;

        /// <summary>
        /// Texture asset name for the water surface (e.g. <c>"water"</c>).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string TextureId;

        /// <summary>Creates a new <see cref="WaterMeshResult"/>.</summary>
        /// <param name="mesh">The generated water-surface mesh.</param>
        /// <param name="textureId">Texture asset name for the surface.</param>
        public WaterMeshResult(Mesh mesh, string textureId)
        {
            Mesh      = mesh;
            TextureId = textureId;
        }
    }
}
