using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VectorRoad.Core;
using VectorRoad.DataInversion;
using VectorRoad.Terrain;

namespace VectorRoad.Tests
{
    /// <summary>
    /// Unit tests for <see cref="MapLoader"/>.
    ///
    /// Each test creates minimal temp files, calls <see cref="MapLoader.LoadMapAsync"/>,
    /// and asserts on the resulting <see cref="MapData"/>.
    /// </summary>
    [TestFixture]
    public class MapLoaderTests
    {
        // ── Minimal test fixtures ─────────────────────────────────────────────

        private const string MinimalOsm = """
            <?xml version='1.0'?>
            <osm version='0.6'>
              <node id='1' lat='51.5000' lon='-0.1000'/>
              <node id='2' lat='51.5010' lon='-0.1010'/>
              <node id='3' lat='51.5020' lon='-0.1020'/>
              <node id='4' lat='51.5000' lon='-0.1020'/>
              <way id='100'>
                <nd ref='1'/><nd ref='2'/>
                <tag k='highway' v='primary'/>
              </way>
              <way id='200'>
                <nd ref='3'/><nd ref='4'/><nd ref='3'/>
                <tag k='building' v='yes'/>
              </way>
            </osm>
            """;

        // OSM fixture that also contains a triangular water body (natural=water).
        private const string OsmWithWater = """
            <?xml version='1.0'?>
            <osm version='0.6'>
              <node id='1' lat='51.5000' lon='-0.1000'/>
              <node id='2' lat='51.5010' lon='-0.1010'/>
              <node id='3' lat='51.5000' lon='-0.1020'/>
              <way id='100'>
                <nd ref='1'/><nd ref='2'/>
                <tag k='highway' v='primary'/>
              </way>
              <way id='300'>
                <nd ref='1'/><nd ref='2'/><nd ref='3'/>
                <tag k='natural' v='water'/>
                <tag k='water' v='pond'/>
              </way>
            </osm>
            """;

        // A 2×2 elevation CSV whose bounds fully enclose the test nodes.
        // minLat=51.490, maxLat=51.510, minLon=-0.115, maxLon=-0.095, rows=2, cols=2
        // grid (south→north, west→east): [0,0]=5  [0,1]=10
        //                                 [1,0]=15 [1,1]=20
        private const string MinimalElevationCsv =
            "51.490,51.510,-0.115,-0.095,2,2\n" +
            "5,10\n" +
            "15,20\n";

        private const double OriginLat = 51.5000;
        private const double OriginLon = -0.1000;

        // ── helpers ───────────────────────────────────────────────────────────

        private static string WriteTempFile(string content, string extension)
        {
            string path = Path.GetTempFileName() + extension;
            File.WriteAllText(path, content);
            return path;
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        // ── LoadMapAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task LoadMapAsync_ReturnsNonNullMapData()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                Assert.That(data, Is.Not.Null);
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_PopulatesRoads()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                Assert.That(data.Roads.Count, Is.EqualTo(1));
                Assert.That(data.Roads[0].HighwayType, Is.EqualTo("primary"));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_PopulatesBuildings()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                Assert.That(data.Buildings.Count, Is.EqualTo(1));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_TerrainMesh_HasCorrectVertexCount()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                // 2×2 grid → 4 vertices
                Assert.That(data.TerrainMesh.Vertices.Length, Is.EqualTo(4));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_TerrainMesh_HasCorrectTriangleCount()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                // 2×2 grid → 1 quad → 2 triangles → 6 indices
                Assert.That(data.TerrainMesh.Triangles.Length, Is.EqualTo(6));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_ElevationGrid_MatchesLoadedCsv()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                Assert.That(data.ElevationGrid.Rows, Is.EqualTo(2));
                Assert.That(data.ElevationGrid.Cols, Is.EqualTo(2));
                Assert.That(data.ElevationGrid[0, 0], Is.EqualTo(5.0).Within(1e-9));
                Assert.That(data.ElevationGrid[1, 1], Is.EqualTo(20.0).Within(1e-9));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_RoadNodes_HaveElevationApplied()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                // All grid elevations are positive (5–20 m), so every road node's Y
                // should be lifted above zero.
                foreach (RoadSegment road in data.Roads)
                    foreach (var node in road.Nodes)
                        Assert.That(node.y, Is.GreaterThan(0.0f),
                            "Expected road node Y to be lifted to terrain elevation");
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_BuildingNodes_HaveElevationApplied()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                foreach (BuildingFootprint building in data.Buildings)
                    foreach (var corner in building.Footprint)
                        Assert.That(corner.y, Is.GreaterThan(0.0f),
                            "Expected building corner Y to be lifted to terrain elevation");
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_TerrainMesh_VertexY_MatchesElevationGrid()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                ElevationGrid grid = data.ElevationGrid;

                // Check each vertex Y against the grid value at that cell.
                for (int r = 0; r < grid.Rows; r++)
                {
                    for (int c = 0; c < grid.Cols; c++)
                    {
                        int idx = r * grid.Cols + c;
                        float expectedY = (float)grid[r, c];
                        Assert.That(data.TerrainMesh.Vertices[idx].y,
                            Is.EqualTo(expectedY).Within(1e-4f),
                            $"Terrain vertex [{r},{c}] Y mismatch");
                    }
                }
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public void LoadMapAsync_MissingElevationFile_ThrowsFileNotFoundException()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            // Use a non-existent filename inside the temp directory so the parent
            // directory exists but the file itself does not.
            string missingCsv = Path.Combine(Path.GetTempPath(),
                Path.GetRandomFileName() + ".elevation.csv");
            try
            {
                Assert.ThrowsAsync<FileNotFoundException>(
                    async () => await MapLoader.LoadMapAsync(
                        osm, missingCsv, OriginLat, OriginLon));
            }
            finally { DeleteFile(osm); }
        }

        [Test]
        public void LoadMapAsync_CancellationAlreadyCancelled_ThrowsOperationCanceledException()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                using var cts = new CancellationTokenSource();
                cts.Cancel();

                Assert.ThrowsAsync<OperationCanceledException>(
                    async () => await MapLoader.LoadMapAsync(
                        osm, csv, OriginLat, OriginLon, cts.Token));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        // ── Water bodies ──────────────────────────────────────────────────────

        [Test]
        public async Task LoadMapAsync_PopulatesWaterBodies()
        {
            string osm = WriteTempFile(OsmWithWater, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                Assert.That(data.WaterBodies.Count, Is.EqualTo(1));
                Assert.That(data.WaterBodies[0].WaterType, Is.EqualTo("pond"));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_WaterBodyNodes_HaveElevationApplied()
        {
            string osm = WriteTempFile(OsmWithWater, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                // All grid elevations are positive (5–20 m), so every outline node's Y
                // should be lifted above zero.
                foreach (var water in data.WaterBodies)
                    foreach (var node in water.Outline)
                        Assert.That(node.y, Is.GreaterThan(0.0f),
                            "Expected water body outline Y to be lifted to terrain elevation");
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }

        [Test]
        public async Task LoadMapAsync_OsmWithNoWater_WaterBodiesIsEmpty()
        {
            string osm = WriteTempFile(MinimalOsm, ".osm");
            string csv = WriteTempFile(MinimalElevationCsv, ".elevation.csv");
            try
            {
                MapData data = await MapLoader.LoadMapAsync(osm, csv, OriginLat, OriginLon);

                Assert.That(data.WaterBodies, Is.Not.Null);
                Assert.That(data.WaterBodies.Count, Is.EqualTo(0));
            }
            finally { DeleteFile(osm); DeleteFile(csv); }
        }
    }
}
