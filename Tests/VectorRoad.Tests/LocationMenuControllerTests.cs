using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VectorRoad.Core;
using VectorRoad.Terrain;
using VectorRoad.Tools;
using UnityEngine;

namespace VectorRoad.Tests
{
    /// <summary>
    /// Unit tests for <see cref="LocationMenuController"/> and
    /// <see cref="LocationLoadResult"/>.
    ///
    /// All network I/O is replaced by a <see cref="StubOsmDownloader"/> that returns
    /// canned OSM XML and a synthetic <see cref="ElevationGrid"/> — no real HTTP
    /// requests are made.
    /// </summary>
    [TestFixture]
    public class LocationMenuControllerTests
    {
        // ── Stub downloader ───────────────────────────────────────────────────

        /// <summary>
        /// Stub <see cref="IOsmDownloader"/> that returns pre-baked test data without
        /// making any real HTTP requests.
        /// </summary>
        private sealed class StubOsmDownloader : IOsmDownloader
        {
            // A minimal .osm file whose nodes lie within the elevation-grid bounds.
            private const string MinimalOsm = """
                <?xml version='1.0'?>
                <osm version='0.6'>
                  <node id='1' lat='51.5000' lon='-0.1000'/>
                  <node id='2' lat='51.5010' lon='-0.1010'/>
                  <way id='100'>
                    <nd ref='1'/><nd ref='2'/>
                    <tag k='highway' v='primary'/>
                  </way>
                </osm>
                """;

            // Track call counts for assertion purposes.
            public int DownloadOsmCallCount      { get; private set; }
            public int DownloadElevationCallCount { get; private set; }

            // Simulate a cancellation check.
            private readonly bool _throwOnCancelled;

            public StubOsmDownloader(bool throwOnCancelled = false)
            {
                _throwOnCancelled = throwOnCancelled;
            }

            public Task<string> DownloadOsmAsync(
                double lat, double lon, int radius,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DownloadOsmCallCount++;
                return Task.FromResult(MinimalOsm);
            }

            public Task<ElevationGrid> DownloadElevationGridAsync(
                double lat, double lon, int radius,
                int rows = 0, int cols = 0,
                double targetSpacingMetres = OsmDownloader.SrtmSpacingMetres,
                IElevationSource? elevationSource = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DownloadElevationCallCount++;

                // Build a 2×2 grid that fully encloses the stub OSM nodes.
                var elevs = new double[2, 2] { { 5.0, 10.0 }, { 15.0, 20.0 } };
                var grid  = new ElevationGrid(
                    minLat: 51.490, maxLat: 51.510,
                    minLon: -0.115, maxLon: -0.095,
                    elevations: elevs);
                return Task.FromResult(grid);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string _tempDir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            CoordinateConverter.ResetWorldOrigin();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private LocationMenuController MakeController(StubOsmDownloader? stub = null)
        {
            stub ??= new StubOsmDownloader();
            return new LocationMenuController(stub, _tempDir)
            {
                Latitude  = 51.5000,
                Longitude = -0.1000,
                Radius    = 500,
            };
        }

        // ── IsValidCoordinate ─────────────────────────────────────────────────

        [Test]
        public void IsValidCoordinate_ValidValues_ReturnsTrue()
        {
            Assert.That(LocationMenuController.IsValidCoordinate(51.5, -0.1), Is.True);
            Assert.That(LocationMenuController.IsValidCoordinate(0.0,   0.0), Is.True);
            Assert.That(LocationMenuController.IsValidCoordinate(90.0, 180.0), Is.True);
            Assert.That(LocationMenuController.IsValidCoordinate(-90.0, -180.0), Is.True);
        }

        [Test]
        public void IsValidCoordinate_LatOutOfRange_ReturnsFalse()
        {
            Assert.That(LocationMenuController.IsValidCoordinate(91.0,  0.0), Is.False);
            Assert.That(LocationMenuController.IsValidCoordinate(-91.0, 0.0), Is.False);
        }

        [Test]
        public void IsValidCoordinate_LonOutOfRange_ReturnsFalse()
        {
            Assert.That(LocationMenuController.IsValidCoordinate(0.0, 181.0),  Is.False);
            Assert.That(LocationMenuController.IsValidCoordinate(0.0, -181.0), Is.False);
        }

        // ── LoadLocationAsync – basic success ─────────────────────────────────

        [Test]
        public async Task LoadLocationAsync_ReturnsNonNullResult()
        {
            LocationMenuController controller = MakeController();

            LocationLoadResult result = await controller.LoadLocationAsync();

            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task LoadLocationAsync_ResultOriginMatchesInputCoordinates()
        {
            LocationMenuController controller = MakeController();

            LocationLoadResult result = await controller.LoadLocationAsync();

            Assert.That(result.OriginLatitude,  Is.EqualTo(51.5000).Within(1e-9));
            Assert.That(result.OriginLongitude, Is.EqualTo(-0.1000).Within(1e-9));
        }

        [Test]
        public async Task LoadLocationAsync_PlayerSpawnPosition_IsWorldOrigin()
        {
            LocationMenuController controller = MakeController();

            LocationLoadResult result = await controller.LoadLocationAsync();

            Assert.That(result.PlayerSpawnPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public async Task LoadLocationAsync_MapData_IsNotNull()
        {
            LocationMenuController controller = MakeController();

            LocationLoadResult result = await controller.LoadLocationAsync();

            Assert.That(result.MapData, Is.Not.Null);
        }

        [Test]
        public async Task LoadLocationAsync_MapData_HasRoads()
        {
            LocationMenuController controller = MakeController();

            LocationLoadResult result = await controller.LoadLocationAsync();

            Assert.That(result.MapData.Roads.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task LoadLocationAsync_MapData_TerrainMesh_HasVertices()
        {
            LocationMenuController controller = MakeController();

            LocationLoadResult result = await controller.LoadLocationAsync();

            Assert.That(result.MapData.TerrainMesh.Vertices.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task LoadLocationAsync_CallsDownloaderOnce()
        {
            var stub = new StubOsmDownloader();
            LocationMenuController controller = MakeController(stub);

            await controller.LoadLocationAsync();

            Assert.That(stub.DownloadOsmCallCount,       Is.EqualTo(1));
            Assert.That(stub.DownloadElevationCallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task LoadLocationAsync_WritesOsmFileToDisk()
        {
            LocationMenuController controller = MakeController();

            await controller.LoadLocationAsync();

            string osmPath = Path.Combine(_tempDir, "current.osm");
            Assert.That(File.Exists(osmPath), Is.True, "current.osm should be written to the data directory.");
        }

        [Test]
        public async Task LoadLocationAsync_WritesElevationFileToDisk()
        {
            LocationMenuController controller = MakeController();

            await controller.LoadLocationAsync();

            string elevPath = Path.Combine(_tempDir, "current.elevation.csv");
            Assert.That(File.Exists(elevPath), Is.True, "current.elevation.csv should be written to the data directory.");
        }

        // ── LoadLocationAsync – coordinate validation ─────────────────────────

        [Test]
        public void LoadLocationAsync_InvalidLatitude_ThrowsArgumentOutOfRangeException()
        {
            var stub = new StubOsmDownloader();
            var controller = new LocationMenuController(stub, _tempDir)
            {
                Latitude  = 91.0,
                Longitude = 0.0,
            };

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => controller.LoadLocationAsync());
        }

        [Test]
        public void LoadLocationAsync_InvalidLongitude_ThrowsArgumentOutOfRangeException()
        {
            var stub = new StubOsmDownloader();
            var controller = new LocationMenuController(stub, _tempDir)
            {
                Latitude  = 0.0,
                Longitude = 200.0,
            };

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => controller.LoadLocationAsync());
        }

        // ── LoadLocationAsync – cancellation ──────────────────────────────────

        [Test]
        public void LoadLocationAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            LocationMenuController controller = MakeController();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                () => controller.LoadLocationAsync(cts.Token));
        }

        // ── LoadLocationAsync – repeated calls update the origin ──────────────

        [Test]
        public async Task LoadLocationAsync_SecondCallWithDifferentCoords_UpdatesOriginLatLon()
        {
            LocationMenuController controller = MakeController();
            await controller.LoadLocationAsync();

            // Switch to a second location.
            controller.Latitude  = 48.8566;
            controller.Longitude =  2.3522;
            LocationLoadResult result2 = await controller.LoadLocationAsync();

            Assert.That(result2.OriginLatitude,  Is.EqualTo(48.8566).Within(1e-9));
            Assert.That(result2.OriginLongitude, Is.EqualTo( 2.3522).Within(1e-9));
        }

        // ── LocationLoadResult ────────────────────────────────────────────────

        [Test]
        public void LocationLoadResult_PlayerSpawnPosition_IsAlwaysZero()
        {
            // PlayerSpawnPosition must be Vector3.zero regardless of the origin used.
            var fakeMap = new MapData(
                new System.Collections.Generic.List<VectorRoad.DataInversion.RoadSegment>(),
                new System.Collections.Generic.List<VectorRoad.DataInversion.BuildingFootprint>(),
                new System.Collections.Generic.List<VectorRoad.DataInversion.WaterBody>(),
                VectorRoad.DataInversion.RegionType.Unknown,
                new VectorRoad.Terrain.TerrainMeshResult(
                    new Vector3[0], new int[0], new UnityEngine.Vector2[0]),
                new ElevationGrid(0, 1, 0, 1, new double[2, 2]));

            var result = new LocationLoadResult(fakeMap, originLatitude: 51.5, originLongitude: -0.1);

            Assert.That(result.PlayerSpawnPosition, Is.EqualTo(Vector3.zero));
        }
    }
}
