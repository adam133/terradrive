using UnityEngine;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Holds the set of meshes and region-appropriate texture identifiers produced by
    /// <see cref="BuildingGenerator.Extrude"/>.
    /// </summary>
    public readonly struct BuildingMeshResult
    {
        /// <summary>
        /// Wall mesh.
        /// UV channel 0 = wall texture tiling (U tiles by wall width, V tiles by height).
        /// </summary>
        public readonly Mesh WallMesh;

        /// <summary>
        /// Flat-roof mesh.
        /// UV channel 0 = roof texture tiling.
        /// </summary>
        public readonly Mesh RoofMesh;

        /// <summary>
        /// Region-appropriate texture asset name for the wall surface (e.g.
        /// <c>"building_wall_brick"</c> for temperate regions).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string WallTextureId;

        /// <summary>
        /// Region-appropriate texture asset name for the roof surface (e.g.
        /// <c>"building_roof_slate"</c> for temperate regions).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string RoofTextureId;

        /// <summary>
        /// Creates a new <see cref="BuildingMeshResult"/>.
        /// </summary>
        /// <param name="wallMesh">The extruded wall mesh.</param>
        /// <param name="roofMesh">The flat roof mesh.</param>
        /// <param name="wallTextureId">Texture asset name for the walls.</param>
        /// <param name="roofTextureId">Texture asset name for the roof.</param>
        public BuildingMeshResult(Mesh wallMesh, Mesh roofMesh, string wallTextureId, string roofTextureId)
        {
            WallMesh      = wallMesh;
            RoofMesh      = roofMesh;
            WallTextureId = wallTextureId;
            RoofTextureId = roofTextureId;
        }
    }
}
