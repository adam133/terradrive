using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TerraDrive.Terrain;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OpenElevationSource"/>, covering JSON building,
    /// JSON parsing, HTTP integration, and edge-case error handling.
    /// </summary>
    [TestFixture]
    public class OpenElevationSourceTests
    {
        // ── BuildRequestJson ──────────────────────────────────────────────────

        [Test]
        public void BuildRequestJson_SingleLocation_ProducesValidJson()
        {
            var locations = new[] { (51.5074, -0.1278) };
            string json = OpenElevationSource.BuildRequestJson(locations);

            Assert.That(json, Does.Contain("\"locations\""));
            Assert.That(json, Does.Contain("\"latitude\""));
            Assert.That(json, Does.Contain("\"longitude\""));
            Assert.That(json, Does.Contain("51.5074"));
            Assert.That(json, Does.Contain("-0.1278"));
        }

        [Test]
        public void BuildRequestJson_MultipleLocations_ContainsAllPoints()
        {
            var locations = new[]
            {
                (10.0, 20.0),
                (30.0, 40.0),
                (-5.5, 100.25),
            };
            string json = OpenElevationSource.BuildRequestJson(locations);

            Assert.That(json, Does.Contain("10"));
            Assert.That(json, Does.Contain("20"));
            Assert.That(json, Does.Contain("30"));
            Assert.That(json, Does.Contain("40"));
            Assert.That(json, Does.Contain("-5.5"));
            Assert.That(json, Does.Contain("100.25"));
        }

        [Test]
        public void BuildRequestJson_UsesInvariantCulture_ForDecimalSeparator()
        {
            // Regardless of system locale, decimal separator must be a '.' not ','
            var locations = new[] { (1.5, 2.5) };
            string json = OpenElevationSource.BuildRequestJson(locations);

            Assert.That(json, Does.Contain("1.5"));
            Assert.That(json, Does.Contain("2.5"));
            Assert.That(json, Does.Not.Contain("1,5"));
        }

        // ── ParseResponseJson ─────────────────────────────────────────────────

        [Test]
        public void ParseResponseJson_ValidResponse_ReturnsElevations()
        {
            const string json = """
                {
                    "results": [
                        { "latitude": 10.0, "longitude": 20.0, "elevation": 412.0 },
                        { "latitude": 30.0, "longitude": 40.0, "elevation": 0.0   },
                        { "latitude": -5.5, "longitude": 100.25, "elevation": -50.5 }
                    ]
                }
                """;

            IReadOnlyList<double> elevations = OpenElevationSource.ParseResponseJson(json, 3);

            Assert.That(elevations.Count, Is.EqualTo(3));
            Assert.That(elevations[0], Is.EqualTo(412.0));
            Assert.That(elevations[1], Is.EqualTo(0.0));
            Assert.That(elevations[2], Is.EqualTo(-50.5));
        }

        [Test]
        public void ParseResponseJson_MissingResultsField_ThrowsInvalidOperationException()
        {
            const string json = "{ \"something_else\": [] }";

            Assert.Throws<InvalidOperationException>(
                () => OpenElevationSource.ParseResponseJson(json, 1),
                "Missing 'results' field should throw.");
        }

        [Test]
        public void ParseResponseJson_CountMismatch_ThrowsInvalidOperationException()
        {
            const string json = """
                { "results": [{ "latitude": 1.0, "longitude": 2.0, "elevation": 100.0 }] }
                """;

            Assert.Throws<InvalidOperationException>(
                () => OpenElevationSource.ParseResponseJson(json, 3),
                "Mismatched result count should throw.");
        }

        [Test]
        public void ParseResponseJson_NegativeElevation_IsReturnedCorrectly()
        {
            const string json = """
                { "results": [{ "latitude": 0.0, "longitude": 0.0, "elevation": -420.0 }] }
                """;

            IReadOnlyList<double> elevations = OpenElevationSource.ParseResponseJson(json, 1);

            Assert.That(elevations[0], Is.EqualTo(-420.0));
        }

        [Test]
        public void ParseResponseJson_ZeroElevation_IsReturnedCorrectly()
        {
            const string json = """
                { "results": [{ "latitude": 0.0, "longitude": 0.0, "elevation": 0 }] }
                """;

            IReadOnlyList<double> elevations = OpenElevationSource.ParseResponseJson(json, 1);

            Assert.That(elevations[0], Is.EqualTo(0.0));
        }

        // ── FetchElevationsAsync (via stub HTTP handler) ──────────────────────

        [Test]
        public async Task FetchElevationsAsync_SuccessfulResponse_ReturnsElevations()
        {
            const string responseBody = """
                {
                    "results": [
                        { "latitude": 51.5074, "longitude": -0.1278, "elevation": 11.0 },
                        { "latitude": 48.8566, "longitude":  2.3522, "elevation": 35.0 }
                    ]
                }
                """;

            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
                });

            var source = new OpenElevationSource(new HttpClient(handler));
            var locations = new[] { (51.5074, -0.1278), (48.8566, 2.3522) };

            IReadOnlyList<double> elevations = await source.FetchElevationsAsync(locations);

            Assert.That(elevations.Count, Is.EqualTo(2));
            Assert.That(elevations[0], Is.EqualTo(11.0));
            Assert.That(elevations[1], Is.EqualTo(35.0));
        }

        [Test]
        public async Task FetchElevationsAsync_EmptyLocations_ReturnsEmptyList()
        {
            var handler = new StubHttpMessageHandler(_ =>
                throw new InvalidOperationException("Should not make an HTTP request for empty input."));

            var source = new OpenElevationSource(new HttpClient(handler));

            IReadOnlyList<double> elevations =
                await source.FetchElevationsAsync(Array.Empty<(double, double)>());

            Assert.That(elevations, Is.Empty);
        }

        [Test]
        public void FetchElevationsAsync_NullLocations_ThrowsArgumentNullException()
        {
            var source = new OpenElevationSource(new HttpClient(new StubHttpMessageHandler(
                _ => throw new InvalidOperationException("Should not be called."))));

            Assert.ThrowsAsync<ArgumentNullException>(
                () => source.FetchElevationsAsync(null!));
        }

        [Test]
        public void FetchElevationsAsync_HttpError_ThrowsHttpRequestException()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var source = new OpenElevationSource(new HttpClient(handler));
            var locations = new[] { (0.0, 0.0) };

            Assert.ThrowsAsync<HttpRequestException>(
                () => source.FetchElevationsAsync(locations));
        }

        [Test]
        public void FetchElevationsAsync_CancellationRequested_ThrowsTaskCanceled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = new StubHttpMessageHandler(req =>
            {
                req.Options.TryGetValue(
                    new System.Net.Http.HttpRequestOptionsKey<bool>("__test__"), out _);
                throw new TaskCanceledException();
            });

            var source = new OpenElevationSource(new HttpClient(handler));
            var locations = new[] { (1.0, 2.0) };

            Assert.ThrowsAsync<TaskCanceledException>(
                () => source.FetchElevationsAsync(locations, cts.Token));
        }

        [Test]
        public void Constructor_NullHttpClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenElevationSource(null!, OpenElevationSource.DefaultBaseUrl));
        }

        [Test]
        public void Constructor_NullBaseUrl_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenElevationSource(new HttpClient(), null!));
        }

        [Test]
        public void DefaultBaseUrl_IsOpenElevationEndpoint()
        {
            Assert.That(OpenElevationSource.DefaultBaseUrl,
                Does.StartWith("https://api.open-elevation.com"),
                "Default endpoint should point to the Open-Elevation API.");
        }

        // ── Stub HTTP message handler ─────────────────────────────────────────

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
    }
}
