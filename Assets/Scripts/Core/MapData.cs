using System.Collections.Generic;
using VectorRoad.DataInversion;
using VectorRoad.Terrain;

namespace VectorRoad.Core
{
    /// <summary>
    /// Holds all data produced by <see cref="MapLoader.LoadMapAsync"/>: the parsed OSM
    /// roads, buildings, and water bodies (with each node's Y coordinate lifted to the
    /// terrain elevation), the heightfield terrain mesh, and the underlying elevation grid.
    ///
    /// <para>
    /// Pass <see cref="Roads"/> and <see cref="Buildings"/> to the procedural mesh
    /// generators (<c>RoadMeshExtruder.ExtrudeWithDetails</c>,
    /// <c>BuildingGenerator.Extrude</c>) to produce Unity meshes whose geometry sits on
    /// the real-world terrain surface.  Pass <see cref="WaterBodies"/> to
    /// <c>WaterMeshGenerator.Generate</c> to produce flat water-surface meshes.
    /// Assign <see cref="TerrainMesh"/> directly to a Unity <c>Mesh</c> via its
    /// <c>Vertices</c>, <c>Triangles</c>, and <c>UVs</c> arrays.
    /// </para>
    /// </summary>
    public sealed class MapData
    {
        /// <summary>
        /// Parsed OSM road segments.  Each node's <c>Vector3.Y</c> is set to the
        /// terrain elevation sampled from <see cref="ElevationGrid"/>.
        /// </summary>
        public List<RoadSegment> Roads { get; }

        /// <summary>
        /// Parsed OSM building footprints.  Each corner's <c>Vector3.Y</c> is set to
        /// the terrain elevation sampled from <see cref="ElevationGrid"/>.
        /// </summary>
        public List<BuildingFootprint> Buildings { get; }

        /// <summary>
        /// Parsed OSM water bodies (lakes, ponds, riverbanks, reservoirs).  Each outline
        /// node's <c>Vector3.Y</c> is set to the terrain elevation sampled from
        /// <see cref="ElevationGrid"/>.
        /// </summary>
        public List<WaterBody> WaterBodies { get; }

        /// <summary>
        /// Geographic region detected from the OSM data (used for texture and prop
        /// selection by <c>RegionTextures</c> and <c>RoadsidePropPlacer</c>).
        /// </summary>
        public RegionType Region { get; }

        /// <summary>
        /// Heightfield terrain mesh produced by
        /// <see cref="TerrainMeshGenerator.Generate"/>.  Assign directly to a Unity
        /// <c>Mesh</c>:
        /// <code>
        ///   mesh.vertices  = mapData.TerrainMesh.Vertices;
        ///   mesh.triangles = mapData.TerrainMesh.Triangles;
        ///   mesh.uv        = mapData.TerrainMesh.UVs;
        ///   mesh.RecalculateNormals();
        /// </code>
        /// </summary>
        public TerrainMeshResult TerrainMesh { get; }

        /// <summary>
        /// The elevation grid used to lift OSM nodes and generate the terrain mesh.
        /// Can be passed to additional <c>OSMParser.ParseAsync</c> calls or used for
        /// elevation sampling at arbitrary geographic coordinates.
        /// </summary>
        public ElevationGrid ElevationGrid { get; }

        /// <summary>Initialises a new <see cref="MapData"/>.</summary>
        public MapData(
            List<RoadSegment> roads,
            List<BuildingFootprint> buildings,
            List<WaterBody> waterBodies,
            RegionType region,
            TerrainMeshResult terrainMesh,
            ElevationGrid elevationGrid)
        {
            Roads         = roads;
            Buildings     = buildings;
            WaterBodies   = waterBodies;
            Region        = region;
            TerrainMesh   = terrainMesh;
            ElevationGrid = elevationGrid;
        }
    }
}
