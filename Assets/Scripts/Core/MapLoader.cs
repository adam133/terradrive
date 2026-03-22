using System.Threading;
using System.Threading.Tasks;
using VectorRoad.DataInversion;
using VectorRoad.Terrain;

namespace VectorRoad.Core
{
    /// <summary>
    /// Loads a full map scene from a pre-downloaded <c>.osm</c> and
    /// <c>.elevation.csv</c> file pair (produced by the <c>OsmDownloader</c> CLI tool)
    /// and generates all data required for Unity scene construction in one call.
    ///
    /// <para>
    /// The pipeline is:
    /// <list type="number">
    ///   <item>
    ///     Load the elevation grid from the CSV file via
    ///     <see cref="ElevationGrid.Load"/>.
    ///   </item>
    ///   <item>
    ///     Parse the OSM file via <see cref="OSMParser.ParseAsync"/>, passing the
    ///     <see cref="ElevationGrid"/> as the <see cref="IElevationSource"/>.  Every
    ///     OSM node's Y coordinate is lifted to the sampled terrain elevation — no
    ///     additional HTTP requests are made.
    ///   </item>
    ///   <item>
    ///     Generate a heightfield terrain mesh via
    ///     <see cref="TerrainMeshGenerator.Generate"/> using the same grid.
    ///   </item>
    /// </list>
    /// All results are returned together in a <see cref="MapData"/> object.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   MapData map = await MapLoader.LoadMapAsync(
    ///       "Assets/Data/london.osm",
    ///       "Assets/Data/london.elevation.csv",
    ///       originLat: 51.5074, originLon: -0.1278);
    ///
    ///   // Roads and buildings have their Y coordinates set to terrain elevation
    ///   foreach (RoadSegment road in map.Roads)  { /* extrude mesh */ }
    ///   foreach (BuildingFootprint b in map.Buildings) { /* extrude mesh */ }
    ///
    ///   // Terrain mesh is ready for direct Unity Mesh assignment
    ///   mesh.vertices  = map.TerrainMesh.Vertices;
    ///   mesh.triangles = map.TerrainMesh.Triangles;
    ///   mesh.uv        = map.TerrainMesh.UVs;
    ///   mesh.RecalculateNormals();
    /// </code>
    /// </summary>
    public static class MapLoader
    {
        /// <summary>
        /// Loads map data from a pre-downloaded OSM + elevation CSV file pair and
        /// generates road/building geometry and a terrain mesh, all elevated to match
        /// the real-world terrain.
        /// </summary>
        /// <param name="osmPath">Path to the <c>.osm</c> XML file.</param>
        /// <param name="elevationCsvPath">
        /// Path to the companion <c>.elevation.csv</c> file written by
        /// <c>OsmDownloader.SaveElevation</c> (or produced by the CLI tool).
        /// </param>
        /// <param name="originLat">
        /// Map origin latitude — the point that maps to world position (0, *, 0).
        /// </param>
        /// <param name="originLon">
        /// Map origin longitude — the point that maps to world position (0, *, 0).
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="MapData"/> containing roads, buildings, region type, terrain
        /// mesh, and the elevation grid used to produce them.
        /// </returns>
        public static async Task<MapData> LoadMapAsync(
            string osmPath,
            string elevationCsvPath,
            double originLat,
            double originLon,
            CancellationToken cancellationToken = default)
        {
            // Load the pre-fetched elevation grid from disk.
            ElevationGrid elevationGrid = ElevationGrid.Load(elevationCsvPath);

            // Parse OSM data, using the elevation grid as the IElevationSource so that
            // every road, building, and water body node's Y coordinate is lifted to match
            // the terrain surface without issuing any additional network requests.
            var (roads, buildings, waterBodies, region) = await OSMParser.ParseAsync(
                    osmPath, originLat, originLon, elevationGrid, cancellationToken)
                .ConfigureAwait(false);

            // Generate a heightfield terrain mesh from the same elevation grid.
            TerrainMeshResult terrainMesh =
                TerrainMeshGenerator.Generate(elevationGrid, originLat, originLon);

            return new MapData(roads, buildings, waterBodies, region, terrainMesh, elevationGrid);
        }
    }
}
