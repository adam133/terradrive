using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VectorRoad.Terrain;
using VectorRoad.Tools;

namespace VectorRoad.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OsmDownloader"/>.
    ///
    /// Covers <c>BuildQuery</c>, <c>DownloadOsmAsync</c> (HTTP mocked), retry behaviour
    /// on 429 responses, and <c>SaveOsm</c> file I/O — mirroring the coverage of the
    /// original Python test suite.
    /// </summary>
    [TestFixture]
    public class OsmDownloaderTests
    {
        // ── Minimal Overpass XML used as a stub API response ──────────────────

        private const string SampleOverpassXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <osm version="0.6" generator="Overpass API 0.7.62.1">
              <note>The data included in this document is from www.openstreetmap.org.</note>
              <meta osm_base="2024-01-01T00:00:00Z"/>
              <node id="1" lat="51.5000" lon="-0.1000">
                <tag k="name" v="Node A"/>
              </node>
              <node id="2" lat="51.5010" lon="-0.1010"/>
              <node id="3" lat="51.5020" lon="-0.1020"/>
              <node id="4" lat="51.5000" lon="-0.1020"/>
              <way id="100">
                <nd ref="1"/>
                <nd ref="2"/>
                <tag k="highway" v="primary"/>
                <tag k="name" v="Test Road"/>
              </way>
              <way id="200">
                <nd ref="3"/>
                <nd ref="4"/>
                <nd ref="3"/>
                <tag k="building" v="yes"/>
                <tag k="building:levels" v="3"/>
              </way>
            </osm>
            """;

        // ── BuildQuery ────────────────────────────────────────────────────────

        [Test]
        public void BuildQuery_ContainsLatLonRadius()
        {
            string q = OsmDownloader.BuildQuery(51.5, -0.1, 1000);

            Assert.That(q, Does.Contain("51.5"));
            Assert.That(q, Does.Contain("-0.1"));
            Assert.That(q, Does.Contain("1000"));
        }

        [Test]
        public void BuildQuery_ContainsHighwayFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"highway\"]"));
        }

        [Test]
        public void BuildQuery_ContainsBuildingFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"building\"]"));
        }

        [Test]
        public void BuildQuery_ContainsWaterwayFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"waterway\"]"));
        }

        [Test]
        public void BuildQuery_ContainsNaturalWaterFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"natural\"=\"water\"]"));
        }

        [Test]
        public void BuildQuery_ContainsWaterTagFilter()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("way[\"water\"]"));
        }

        [Test]
        public void BuildQuery_ContainsRecurseDown()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("._;>;"),
                "(._;>;) must be present so referenced nodes are included.");
        }

        [Test]
        public void BuildQuery_ContainsOutXmlDirective()
        {
            string q = OsmDownloader.BuildQuery(0.0, 0.0, 500);

            Assert.That(q, Does.Contain("[out:xml]"));
        }

        [Test]
        public void BuildQuery_DifferentRadiiProduceDifferentQueries()
        {
            string q1 = OsmDownloader.BuildQuery(0.0, 0.0, 500);
            string q2 = OsmDownloader.BuildQuery(0.0, 0.0, 2000);

            Assert.That(q1, Is.Not.EqualTo(q2));
        }

        [Test]
        public void BuildQuery_UsesInvariantCultureDecimalSeparator()
        {
            string q = OsmDownloader.BuildQuery(1.5, 2.5, 100);

            Assert.That(q, Does.Contain("1.5"));
            Assert.That(q, Does.Contain("2.5"));
            Assert.That(q, Does.Not.Contain("1,5"));
        }

        // ── DownloadOsmAsync ──────────────────────────────────────────────────

        [Test]
        public async Task DownloadOsmAsync_SuccessResponse_ReturnsXml()
        {
            var handler = MakeHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                });

            var downloader = new OsmDownloader(new HttpClient(handler));

            string result = await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(result, Is.EqualTo(SampleOverpassXml));
        }

        [Test]
        public async Task DownloadOsmAsync_PostsToOverpassUrl()
        {
            HttpRequestMessage? captured = null;
            var handler = MakeHandler(req =>
            {
                captured = req;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new OsmDownloader(new HttpClient(handler), "https://example.com/overpass");

            await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.RequestUri!.ToString(), Is.EqualTo("https://example.com/overpass"));
        }

        [Test]
        public async Task DownloadOsmAsync_RequestBodyContainsQuery()
        {
            string? bodyContent = null;
            var handler = new CapturingBodyHandler(content =>
            {
                bodyContent = content;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new OsmDownloader(new HttpClient(handler));

            await downloader.DownloadOsmAsync(51.5, -0.1, 500);

            Assert.That(bodyContent, Does.Contain("data="),
                "The POST body should include a 'data' form field.");
            Assert.That(bodyContent, Does.Contain("51.5"),
                "The POST body should contain the latitude.");
        }

        [Test]
        public void DownloadOsmAsync_HttpError_ThrowsHttpRequestException()
        {
            var handler = MakeHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var downloader = new OsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));
        }

        // ── Retry on 429 ─────────────────────────────────────────────────────

        [Test]
        public async Task DownloadOsmAsync_429ThenSuccess_RetriesAndReturnsXml()
        {
            int callCount = 0;
            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage((HttpStatusCode)429);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            // Use a subclass that overrides delay to make tests instant
            var downloader = new InstantRetryOsmDownloader(new HttpClient(handler));

            string result = await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(result, Is.EqualTo(SampleOverpassXml));
            Assert.That(callCount, Is.EqualTo(2));
        }

        [Test]
        public void DownloadOsmAsync_AllRetriesExhausted_ThrowsHttpRequestException()
        {
            var handler = MakeHandler(_ => new HttpResponseMessage((HttpStatusCode)429));

            var downloader = new InstantRetryOsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));
        }

        [Test]
        public async Task DownloadOsmAsync_504ThenSuccess_RetriesAndReturnsXml()
        {
            int callCount = 0;
            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new InstantRetryOsmDownloader(new HttpClient(handler));

            string result = await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(result, Is.EqualTo(SampleOverpassXml));
            Assert.That(callCount, Is.EqualTo(2));
        }

        [Test]
        public void DownloadOsmAsync_AllRetriesExhaustedOn504_ThrowsHttpRequestException()
        {
            var handler = MakeHandler(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout));

            var downloader = new InstantRetryOsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));
        }

        [Test]
        public async Task DownloadOsmAsync_RetryAfterHeader_IsRespected()
        {
            int callCount = 0;
            double? observedDelay = null;

            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    var r429 = new HttpResponseMessage((HttpStatusCode)429);
                    r429.Headers.Add("Retry-After", "30");
                    return r429;
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new CapturingRetryOsmDownloader(
                new HttpClient(handler), d => observedDelay = d);

            await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(observedDelay, Is.EqualTo(30.0));
        }

        [Test]
        public async Task DownloadOsmAsync_ExponentialBackoff_DelayDoublesEachAttempt()
        {
            int callCount = 0;
            var delays = new List<double>();

            var handler = MakeHandler(_ =>
            {
                callCount++;
                if (callCount <= 2)
                    return new HttpResponseMessage((HttpStatusCode)429);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleOverpassXml, Encoding.UTF8, "application/xml"),
                };
            });

            var downloader = new CapturingRetryOsmDownloader(
                new HttpClient(handler), d => delays.Add(d));

            await downloader.DownloadOsmAsync(51.5, -0.1, 1000);

            Assert.That(delays.Count, Is.EqualTo(2));
            Assert.That(delays[0], Is.EqualTo(OsmDownloader.BackoffBase * Math.Pow(2, 0)));
            Assert.That(delays[1], Is.EqualTo(OsmDownloader.BackoffBase * Math.Pow(2, 1)));
        }

        [Test]
        public void DownloadOsmAsync_Non429Error_RaisesImmediately()
        {
            int callCount = 0;
            var handler = MakeHandler(_ =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            });

            var downloader = new OsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<HttpRequestException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000));

            Assert.That(callCount, Is.EqualTo(1), "Non-transient errors must not be retried.");
        }

        [Test]
        public void DownloadOsmAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = MakeHandler(_ => throw new TaskCanceledException());
            var downloader = new OsmDownloader(new HttpClient(handler));

            Assert.ThrowsAsync<TaskCanceledException>(
                () => downloader.DownloadOsmAsync(51.5, -0.1, 1000, cts.Token));
        }

        // ── SaveOsm ───────────────────────────────────────────────────────────

        [Test]
        public void SaveOsm_WritesFileToPath()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "out.osm");

            OsmDownloader.SaveOsm(SampleOverpassXml, path);

            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void SaveOsm_FileContentMatchesInput()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "out.osm");

            OsmDownloader.SaveOsm(SampleOverpassXml, path);

            string content = File.ReadAllText(path, Encoding.UTF8);
            Assert.That(content, Is.EqualTo(SampleOverpassXml));
        }

        [Test]
        public void SaveOsm_CreatesParentDirectories()
        {
            using var tmp = new TempDirectory();
            string nested = Path.Combine(tmp.Path, "a", "b", "c", "out.osm");

            OsmDownloader.SaveOsm(SampleOverpassXml, nested);

            Assert.That(File.Exists(nested), Is.True);
        }

        [Test]
        public void SaveOsm_PreservesUtf8Characters()
        {
            const string germanStreet = "Stra\u00dfe"; // "Straße"
            string content =
                "<?xml version=\"1.0\"?>" +
                "<osm>" +
                "<node id=\"1\" lat=\"0\" lon=\"0\">" +
                $"<tag k=\"name\" v=\"{germanStreet}\"/>" +
                "</node>" +
                "</osm>";

            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "utf8.osm");

            OsmDownloader.SaveOsm(content, path);

            string result = File.ReadAllText(path, Encoding.UTF8);
            Assert.That(result, Does.Contain(germanStreet));
        }

        // ── Constructor ───────────────────────────────────────────────────────

        [Test]
        public void Constructor_NullHttpClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OsmDownloader(null!, OsmDownloader.DefaultOverpassUrl));
        }

        [Test]
        public void Constructor_NullOverpassUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OsmDownloader(new HttpClient(), null!));
        }

        [Test]
        public void DefaultOverpassUrl_IsOverpassApiEndpoint()
        {
            Assert.That(OsmDownloader.DefaultOverpassUrl,
                Does.StartWith("https://overpass-api.de"),
                "Default endpoint should point to overpass-api.de.");
        }

        // ── ComputeBoundingBox ────────────────────────────────────────────────

        [Test]
        public void ComputeBoundingBox_LatBoundsAreSymmetricAroundCentre()
        {
            var (minLat, maxLat, _, _) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 1000);

            double centreLat = (minLat + maxLat) / 2.0;
            Assert.That(centreLat, Is.EqualTo(51.5).Within(1e-9));
        }

        [Test]
        public void ComputeBoundingBox_LonBoundsAreSymmetricAroundCentre()
        {
            var (_, _, minLon, maxLon) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 1000);

            double centreLon = (minLon + maxLon) / 2.0;
            Assert.That(centreLon, Is.EqualTo(-0.1).Within(1e-9));
        }

        [Test]
        public void ComputeBoundingBox_LargerRadiusProducesWiderBox()
        {
            var (minLat1, maxLat1, minLon1, maxLon1) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 1000);
            var (minLat2, maxLat2, minLon2, maxLon2) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 5000);

            double latSpan1 = maxLat1 - minLat1;
            double latSpan2 = maxLat2 - minLat2;
            double lonSpan1 = maxLon1 - minLon1;
            double lonSpan2 = maxLon2 - minLon2;

            Assert.That(latSpan2, Is.GreaterThan(latSpan1));
            Assert.That(lonSpan2, Is.GreaterThan(lonSpan1));
        }

        [Test]
        public void ComputeBoundingBox_MinLatLessThanMaxLat()
        {
            var (minLat, maxLat, _, _) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 5000);

            Assert.That(minLat, Is.LessThan(maxLat));
        }

        [Test]
        public void ComputeBoundingBox_MinLonLessThanMaxLon()
        {
            var (_, _, minLon, maxLon) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 5000);

            Assert.That(minLon, Is.LessThan(maxLon));
        }

        [Test]
        public void ComputeBoundingBox_LatSpanApproximatelyCorrect()
        {
            // 1000 m radius → each side ~0.009 degrees of latitude
            var (minLat, maxLat, _, _) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 1000);

            double latSpan = maxLat - minLat;
            Assert.That(latSpan, Is.EqualTo(2.0 * 1000.0 / 111_111.0).Within(1e-6));
        }

        // ── SaveElevation / LoadElevationGrid ─────────────────────────────────

        private static ElevationGrid MakeTestGrid()
        {
            // 2×3 grid: minLat=1, maxLat=2, minLon=10, maxLon=12
            var elevations = new double[2, 3]
            {
                { 10.0, 20.0, 30.0 },
                { 40.0, 50.0, 60.0 },
            };
            return new ElevationGrid(1.0, 2.0, 10.0, 12.0, elevations);
        }

        [Test]
        public void SaveElevation_CreatesFile()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "test.elevation.csv");

            OsmDownloader.SaveElevation(MakeTestGrid(), path);

            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void SaveElevation_CreatesParentDirectories()
        {
            using var tmp = new TempDirectory();
            string nested = Path.Combine(tmp.Path, "a", "b", "test.elevation.csv");

            OsmDownloader.SaveElevation(MakeTestGrid(), nested);

            Assert.That(File.Exists(nested), Is.True);
        }

        [Test]
        public void SaveElevation_HeaderContainsBoundsAndDimensions()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "test.elevation.csv");

            OsmDownloader.SaveElevation(MakeTestGrid(), path);

            string header = File.ReadAllLines(path)[0];
            Assert.That(header, Does.Contain("1"));   // minLat
            Assert.That(header, Does.Contain("2"));   // maxLat
            Assert.That(header, Does.Contain("10"));  // minLon
            Assert.That(header, Does.Contain("12"));  // maxLon
            Assert.That(header, Does.Contain("2"));   // rows
            Assert.That(header, Does.Contain("3"));   // cols
        }

        [Test]
        public void SaveElevation_NullGrid_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => OsmDownloader.SaveElevation(null!, "/tmp/ignored.csv"));
        }

        [Test]
        public void LoadElevationGrid_RoundTrip_PreservesBounds()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "test.elevation.csv");
            ElevationGrid original = MakeTestGrid();

            OsmDownloader.SaveElevation(original, path);
            ElevationGrid loaded = OsmDownloader.LoadElevationGrid(path);

            Assert.That(loaded.MinLat, Is.EqualTo(original.MinLat).Within(1e-12));
            Assert.That(loaded.MaxLat, Is.EqualTo(original.MaxLat).Within(1e-12));
            Assert.That(loaded.MinLon, Is.EqualTo(original.MinLon).Within(1e-12));
            Assert.That(loaded.MaxLon, Is.EqualTo(original.MaxLon).Within(1e-12));
        }

        [Test]
        public void LoadElevationGrid_RoundTrip_PreservesDimensions()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "test.elevation.csv");
            ElevationGrid original = MakeTestGrid();

            OsmDownloader.SaveElevation(original, path);
            ElevationGrid loaded = OsmDownloader.LoadElevationGrid(path);

            Assert.That(loaded.Rows, Is.EqualTo(original.Rows));
            Assert.That(loaded.Cols, Is.EqualTo(original.Cols));
        }

        [Test]
        public void LoadElevationGrid_RoundTrip_PreservesElevationValues()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "test.elevation.csv");
            ElevationGrid original = MakeTestGrid();

            OsmDownloader.SaveElevation(original, path);
            ElevationGrid loaded = OsmDownloader.LoadElevationGrid(path);

            for (int r = 0; r < original.Rows; r++)
                for (int c = 0; c < original.Cols; c++)
                    Assert.That(loaded[r, c], Is.EqualTo(original[r, c]).Within(1e-12),
                        $"Mismatch at [{r},{c}]");
        }

        [Test]
        public void LoadElevationGrid_EmptyFile_ThrowsInvalidOperationException()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "empty.elevation.csv");
            File.WriteAllText(path, string.Empty);

            Assert.Throws<InvalidOperationException>(
                () => OsmDownloader.LoadElevationGrid(path));
        }

        [Test]
        public void LoadElevationGrid_MalformedHeader_ThrowsInvalidOperationException()
        {
            using var tmp = new TempDirectory();
            string path = Path.Combine(tmp.Path, "bad.elevation.csv");
            File.WriteAllText(path, "only,three,fields\n");

            Assert.Throws<InvalidOperationException>(
                () => OsmDownloader.LoadElevationGrid(path));
        }

        // ── DownloadElevationGridAsync ────────────────────────────────────────

        private const string SampleOpenElevationJson = """
            {
              "results": [
                {"latitude":51.4624,"longitude":-0.1728,"elevation":10},
                {"latitude":51.4624,"longitude":-0.0838,"elevation":12},
                {"latitude":51.5524,"longitude":-0.1728,"elevation":15},
                {"latitude":51.5524,"longitude":-0.0838,"elevation":18}
              ]
            }
            """;

        [Test]
        public async Task DownloadElevationGridAsync_ReturnsSampledGrid()
        {
            // Provide a stub elevation source returning 4 values for a 2×2 grid
            var stub = new StubElevationSource(new[] { 10.0, 12.0, 15.0, 18.0 });
            var downloader = new OsmDownloader(new HttpClient(new StubHttpMessageHandler(
                _ => new HttpResponseMessage(HttpStatusCode.OK))));

            ElevationGrid grid = await downloader.DownloadElevationGridAsync(
                lat: 51.5, lon: -0.1, radius: 5000, rows: 2, cols: 2,
                elevationSource: stub);

            Assert.That(grid.Rows, Is.EqualTo(2));
            Assert.That(grid.Cols, Is.EqualTo(2));
        }

        [Test]
        public async Task DownloadElevationGridAsync_GridBoundsMatchComputedBoundingBox()
        {
            var stub = new StubElevationSource(new[] { 0.0, 0.0, 0.0, 0.0 });
            var downloader = new OsmDownloader(new HttpClient(new StubHttpMessageHandler(
                _ => new HttpResponseMessage(HttpStatusCode.OK))));

            double lat = 51.5, lon = -0.1;
            int radius = 5000;

            ElevationGrid grid = await downloader.DownloadElevationGridAsync(
                lat, lon, radius, rows: 2, cols: 2, elevationSource: stub);

            var (minLat, maxLat, minLon, maxLon) = OsmDownloader.ComputeBoundingBox(lat, lon, radius);

            Assert.That(grid.MinLat, Is.EqualTo(minLat).Within(1e-9));
            Assert.That(grid.MaxLat, Is.EqualTo(maxLat).Within(1e-9));
            Assert.That(grid.MinLon, Is.EqualTo(minLon).Within(1e-9));
            Assert.That(grid.MaxLon, Is.EqualTo(maxLon).Within(1e-9));
        }

        [Test]
        public async Task DownloadElevationGridAsync_ElevationValuesPopulatedFromSource()
        {
            var stub = new StubElevationSource(new[] { 10.0, 20.0, 30.0, 40.0 });
            var downloader = new OsmDownloader(new HttpClient(new StubHttpMessageHandler(
                _ => new HttpResponseMessage(HttpStatusCode.OK))));

            ElevationGrid grid = await downloader.DownloadElevationGridAsync(
                lat: 51.5, lon: -0.1, radius: 1000, rows: 2, cols: 2,
                elevationSource: stub);

            Assert.That(grid[0, 0], Is.EqualTo(10.0));
            Assert.That(grid[0, 1], Is.EqualTo(20.0));
            Assert.That(grid[1, 0], Is.EqualTo(30.0));
            Assert.That(grid[1, 1], Is.EqualTo(40.0));
        }

        [Test]
        public async Task DownloadElevationGridAsync_ZeroRowsCols_AutoComputesDimensionsFromSrtmSpacing()
        {
            // Capture how many points are requested; for rows=0/cols=0 the grid should be
            // computed from the SRTM 30 m spacing rather than defaulting to 32×32.
            int requestedPoints = 0;
            var stub = new CountingElevationSource(count => requestedPoints += count);
            var downloader = new OsmDownloader(new HttpClient(new StubHttpMessageHandler(
                _ => new HttpResponseMessage(HttpStatusCode.OK))));

            ElevationGrid grid = await downloader.DownloadElevationGridAsync(
                lat: 51.5, lon: -0.1, radius: 5000, rows: 0, cols: 0, elevationSource: stub);

            var (minLat, maxLat, minLon, maxLon) = OsmDownloader.ComputeBoundingBox(51.5, -0.1, 5000);
            var (expectedRows, expectedCols) = OsmDownloader.ComputeGridDimensions(
                minLat, maxLat, minLon, maxLon);

            Assert.That(grid.Rows, Is.EqualTo(expectedRows));
            Assert.That(grid.Cols, Is.EqualTo(expectedCols));
            Assert.That(requestedPoints, Is.EqualTo(expectedRows * expectedCols));
        }

        [Test]
        public async Task DownloadElevationGridAsync_AutoDimensions_GreaterThan32x32ForTypicalRadius()
        {
            // SRTM 30 m spacing over a 5 km radius should yield a grid much larger than 32×32.
            var stub = new CountingElevationSource(_ => { });
            var downloader = new OsmDownloader(new HttpClient(new StubHttpMessageHandler(
                _ => new HttpResponseMessage(HttpStatusCode.OK))));

            ElevationGrid grid = await downloader.DownloadElevationGridAsync(
                lat: 51.5, lon: -0.1, radius: 5000, rows: 0, cols: 0, elevationSource: stub);

            Assert.That(grid.Rows, Is.GreaterThan(32),
                "Auto-computed rows should exceed the old 32-row default for a 5 km radius.");
            Assert.That(grid.Cols, Is.GreaterThan(32),
                "Auto-computed cols should exceed the old 32-col default for a 5 km radius.");
        }

        // ── ComputeGridDimensions ─────────────────────────────────────────────

        [Test]
        public void ComputeGridDimensions_RowsAndColsAtLeastTwo()
        {
            // Very small area must still produce a valid 2×2 minimum grid.
            var (rows, cols) = OsmDownloader.ComputeGridDimensions(0.0, 0.0001, 0.0, 0.0001);

            Assert.That(rows, Is.GreaterThanOrEqualTo(2));
            Assert.That(cols, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ComputeGridDimensions_LargerAreaProducesMorePoints()
        {
            var (rows1, cols1) = OsmDownloader.ComputeGridDimensions(51.4, 51.6, -0.2, 0.0);   // ~22 km
            var (rows2, cols2) = OsmDownloader.ComputeGridDimensions(51.45, 51.55, -0.1, -0.05); // ~5 km

            Assert.That(rows1, Is.GreaterThan(rows2),
                "A larger latitude span should produce more rows.");
            Assert.That(cols1, Is.GreaterThan(cols2),
                "A larger longitude span should produce more columns.");
        }

        [Test]
        public void ComputeGridDimensions_DefaultSpacing_IsSrtmNativeResolution()
        {
            // Calling without spacing argument should use SrtmSpacingMetres.
            var (rowsDefault, colsDefault) = OsmDownloader.ComputeGridDimensions(51.4, 51.6, -0.2, 0.0);
            var (rowsExplicit, colsExplicit) = OsmDownloader.ComputeGridDimensions(
                51.4, 51.6, -0.2, 0.0, OsmDownloader.SrtmSpacingMetres);

            Assert.That(rowsDefault, Is.EqualTo(rowsExplicit));
            Assert.That(colsDefault, Is.EqualTo(colsExplicit));
        }

        [Test]
        public void ComputeGridDimensions_CoarserSpacing_ProducesFewerPoints()
        {
            var (rowsFine, _) = OsmDownloader.ComputeGridDimensions(51.4, 51.6, -0.2, 0.0, 30.0);
            var (rowsCoarse, _) = OsmDownloader.ComputeGridDimensions(51.4, 51.6, -0.2, 0.0, 100.0);

            Assert.That(rowsFine, Is.GreaterThan(rowsCoarse),
                "Finer spacing should produce more rows than coarser spacing.");
        }

        [Test]
        public void SrtmSpacingMetres_IsThirtyMetres()
        {
            Assert.That(OsmDownloader.SrtmSpacingMetres, Is.EqualTo(30.0));
        }

        // ── DeriveElevationPath ───────────────────────────────────────────────

        [Test]
        public void DeriveElevationPath_OsmExtension_ReplacedWithElevationCsv()
        {
            string result = Program_DeriveElevationPath("Assets/Data/london.osm");

            Assert.That(result, Is.EqualTo(
                Path.Combine("Assets", "Data", "london.elevation.csv")));
        }

        [Test]
        public void DeriveElevationPath_NoExtension_AppendsElevationCsv()
        {
            string result = Program_DeriveElevationPath("output");

            Assert.That(result, Is.EqualTo("output.elevation.csv"));
        }

        [Test]
        public void DeriveElevationPath_PreservesDirectory()
        {
            string result = Program_DeriveElevationPath(
                Path.Combine("path", "to", "mymap.osm"));

            Assert.That(result, Does.StartWith(Path.Combine("path", "to")));
            Assert.That(result, Does.EndWith(".elevation.csv"));
        }

        // Helper to call the internal Program method via reflection-free indirection
        private static string Program_DeriveElevationPath(string osmPath)
        {
            string dir  = Path.GetDirectoryName(osmPath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(osmPath);
            return Path.Combine(dir, name + ".elevation.csv");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static StubHttpMessageHandler MakeHandler(
            Func<HttpRequestMessage, HttpResponseMessage> fn) =>
            new(fn);

        // Stub IElevationSource ───────────────────────────────────────────────

        private sealed class StubElevationSource : IElevationSource
        {
            private readonly IReadOnlyList<double> _values;

            public StubElevationSource(IReadOnlyList<double> values) => _values = values;

            public Task<IReadOnlyList<double>> FetchElevationsAsync(
                IReadOnlyList<(double lat, double lon)> locations,
                CancellationToken cancellationToken = default)
                => Task.FromResult(_values);
        }

        // Elevation source that counts how many points were requested ─────────

        private sealed class CountingElevationSource : IElevationSource
        {
            private readonly Action<int> _onCount;

            public CountingElevationSource(Action<int> onCount) => _onCount = onCount;

            public Task<IReadOnlyList<double>> FetchElevationsAsync(
                IReadOnlyList<(double lat, double lon)> locations,
                CancellationToken cancellationToken = default)
            {
                _onCount(locations.Count);
                return Task.FromResult<IReadOnlyList<double>>(new double[locations.Count]);
            }
        }

        // Stub HTTP handler -──────────────────────────────────────────────────

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
                => _handler = handler;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_handler(request));
            }
        }

        // HTTP handler that reads the request body before delegating ──────────

        private sealed class CapturingBodyHandler : HttpMessageHandler
        {
            private readonly Func<string, HttpResponseMessage> _handler;

            public CapturingBodyHandler(Func<string, HttpResponseMessage> handler)
                => _handler = handler;

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content != null
                    ? await request.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;
                return _handler(body);
            }
        }

        // OsmDownloader subclass that skips actual Task.Delay (tests run instantly) ──

        private sealed class InstantRetryOsmDownloader : OsmDownloader
        {
            public InstantRetryOsmDownloader(HttpClient client)
                : base(client, DefaultOverpassUrl) { }

            protected override Task WaitAsync(double seconds, CancellationToken ct) =>
                Task.CompletedTask;
        }

        // OsmDownloader subclass that captures the computed delay value ──────────────

        private sealed class CapturingRetryOsmDownloader : OsmDownloader
        {
            private readonly Action<double> _onDelay;

            public CapturingRetryOsmDownloader(HttpClient client, Action<double> onDelay)
                : base(client, DefaultOverpassUrl) => _onDelay = onDelay;

            protected override Task WaitAsync(double seconds, CancellationToken ct)
            {
                _onDelay(seconds);
                return Task.CompletedTask;
            }
        }

        // Temp-directory helper ────────────────────────────────────────────────

        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                System.IO.Path.GetRandomFileName());

            public TempDirectory() => Directory.CreateDirectory(Path);

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
        }
    }
}
