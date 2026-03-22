using UnityEngine;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Holds the set of meshes and region-appropriate texture identifiers produced by
    /// <see cref="RoadMeshExtruder.ExtrudeWithDetails"/>.
    /// </summary>
    public readonly struct RoadMeshResult
    {
        /// <summary>
        /// Road surface mesh.
        /// UV channel 0 = asphalt texture tiling (U 0→1 across road, V tiles every
        /// <c>uvTileLength</c> metres).
        /// UV channel 1 = lane-marking tiling (same U, V tiles every
        /// <c>laneMarkingTileLength</c> metres, allowing a separate lane-marking
        /// texture or shader to render centre lines and edge markings).
        /// </summary>
        public readonly Mesh RoadMesh;

        /// <summary>
        /// Kerb (curb) strips on both sides of the carriageway, combined into one
        /// mesh.  The kerb quads sit just outside and slightly above the road surface
        /// so that a separate kerb material can be applied.
        /// UV channel 0 = kerb texture tiling (U 0→1 outer→inner, V tiles by
        /// distance along road).
        /// </summary>
        public readonly Mesh KerbMesh;

        /// <summary>
        /// Region-appropriate texture asset name for the road surface (e.g.
        /// <c>"road_asphalt_temperate"</c>).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string RoadTextureId;

        /// <summary>
        /// Region-appropriate texture asset name for the kerb surface (e.g.
        /// <c>"kerb_stone"</c>).
        /// Corresponds to a key in the project's texture/material registry.
        /// </summary>
        public readonly string KerbTextureId;

        /// <summary>
        /// Texture asset name for the lane-marking overlay applied via UV channel 1.
        /// Encodes whether the road is one-way or two-way (e.g.
        /// <c>"lane_marking_oneway"</c> vs <c>"lane_marking_twoway"</c>).
        /// </summary>
        public readonly string LaneMarkingTextureId;

        /// <summary>
        /// Mesh for the lane-marking overlay, positioned a few millimetres above the road
        /// surface to prevent z-fighting.  UV channel 0 carries the same tiling used by
        /// UV channel 1 on <see cref="RoadMesh"/>, so a dedicated lane-marking material
        /// can be applied directly.  <c>null</c> when no lane markings are appropriate
        /// (e.g. dirt tracks or paths).
        /// </summary>
        public readonly Mesh? LaneMarkingMesh;

        /// <summary>
        /// Roadside ditch mesh for rural roads — a V-profile trench on both sides of the
        /// carriageway.  <c>null</c> for urban road types where a kerb is used instead.
        /// </summary>
        public readonly Mesh? DitchMesh;

        /// <summary>
        /// Region-appropriate texture asset name for the ditch surface (e.g.
        /// <c>"terrain_grass"</c>).  Empty string when <see cref="DitchMesh"/> is <c>null</c>.
        /// </summary>
        public readonly string DitchTextureId;

        /// <summary>
        /// Creates a new <see cref="RoadMeshResult"/>.
        /// </summary>
        /// <param name="roadMesh">The extruded road surface mesh.</param>
        /// <param name="kerbMesh">The combined kerb mesh.</param>
        /// <param name="roadTextureId">Texture asset name for the road surface.</param>
        /// <param name="kerbTextureId">Texture asset name for the kerb surface.</param>
        /// <param name="laneMarkingTextureId">Texture asset name for the lane-marking overlay.</param>
        /// <param name="laneMarkingMesh">Lane-marking overlay mesh (slightly above road surface).</param>
        /// <param name="ditchMesh">Roadside ditch mesh for rural roads; <c>null</c> for urban.</param>
        /// <param name="ditchTextureId">Texture asset name for the ditch surface.</param>
        public RoadMeshResult(Mesh roadMesh, Mesh kerbMesh, string roadTextureId, string kerbTextureId,
                              string laneMarkingTextureId = "",
                              Mesh? laneMarkingMesh = null,
                              Mesh? ditchMesh = null,
                              string ditchTextureId = "")
        {
            RoadMesh             = roadMesh;
            KerbMesh             = kerbMesh;
            RoadTextureId        = roadTextureId;
            KerbTextureId        = kerbTextureId;
            LaneMarkingTextureId = laneMarkingTextureId;
            LaneMarkingMesh      = laneMarkingMesh;
            DitchMesh            = ditchMesh;
            DitchTextureId       = ditchTextureId;
        }
    }
}
