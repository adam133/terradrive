using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TerraDrive.Terrain
{
    /// <summary>
    /// Fetches terrain elevation data from the Open-Elevation REST API, which is backed
    /// by SRTM (Shuttle Radar Topography Mission) 30 m resolution data.
    ///
    /// <para><strong>Why SRTM via Open-Elevation?</strong></para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <strong>Free with no API key</strong> — unlike Cesium ion terrain or many
    ///       commercial services, Open-Elevation requires no registration or billing.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <strong>Global coverage</strong> — SRTM covers land between ±60° latitude,
    ///       which encompasses the vast majority of populated driving areas.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <strong>Sufficient resolution</strong> — SRTM's 30 m horizontal posting is
    ///       adequate for road and terrain mesh generation in a driving simulator.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <strong>Batch API</strong> — a single POST can resolve hundreds of points,
    ///       minimising round-trips when loading OSM node sets.
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// The Open-Elevation project (<see href="https://open-elevation.com"/>) is
    /// self-hostable, so operators can replace the public endpoint with a private
    /// instance for offline or production use.
    /// </para>
    ///
    /// <para><strong>Usage</strong></para>
    /// <code>
    ///   var source = new OpenElevationSource();
    ///   IReadOnlyList&lt;double&gt; elevations = await source.FetchElevationsAsync(
    ///       new[] { (51.5074, -0.1278), (48.8566, 2.3522) });
    /// </code>
    /// </summary>
    public class OpenElevationSource : IElevationSource
    {
        /// <summary>Public Open-Elevation API endpoint.</summary>
        public const string DefaultBaseUrl = "https://api.open-elevation.com/api/v1/lookup";

        /// <summary>Maximum number of retry attempts on HTTP 429 responses.</summary>
        internal const int MaxRetries = 60;

        /// <summary>Fixed delay in seconds between retry attempts when no <c>Retry-After</c> header is present.</summary>
        internal const double RetryDelaySeconds = 2.0;

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        /// <summary>
        /// Initialises a new instance using the supplied <paramref name="httpClient"/>
        /// and an optional custom endpoint URL.
        /// </summary>
        /// <param name="httpClient">
        /// The HTTP client used to send requests.  Pass a pre-configured instance
        /// (e.g. with a base address or retry policy) or inject a mock for testing.
        /// </param>
        /// <param name="baseUrl">
        /// Override the default API endpoint; useful for self-hosted instances.
        /// Defaults to <see cref="DefaultBaseUrl"/>.
        /// </param>
        public OpenElevationSource(HttpClient httpClient, string baseUrl = DefaultBaseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUrl    = baseUrl    ?? throw new ArgumentNullException(nameof(baseUrl));
        }

        /// <summary>
        /// Initialises a new instance with a fresh <see cref="HttpClient"/> and the
        /// default public endpoint.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This constructor is provided for simple, short-lived use (e.g. editor tools,
        /// one-off scripts).  In long-running applications — especially Unity scenes that
        /// may create many instances — use the
        /// <see cref="OpenElevationSource(HttpClient, string)"/> constructor with a
        /// shared, injected <see cref="HttpClient"/> to avoid socket exhaustion caused
        /// by creating a new TCP connection per instance.
        /// </para>
        /// </remarks>
        public OpenElevationSource() : this(new HttpClient()) { }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<double>> FetchElevationsAsync(
            IReadOnlyList<(double lat, double lon)> locations,
            CancellationToken cancellationToken = default)
        {
            if (locations == null) throw new ArgumentNullException(nameof(locations));
            if (locations.Count == 0) return Array.Empty<double>();

            string requestJson = BuildRequestJson(locations);

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                HttpResponseMessage response =
                    await _httpClient.PostAsync(_baseUrl, content, cancellationToken)
                                     .ConfigureAwait(false);

                int statusCode = (int)response.StatusCode;
                if (statusCode == 429 || statusCode == 504)
                {
                    if (attempt == MaxRetries)
                        response.EnsureSuccessStatusCode(); // always throws for 4xx/5xx

                    double delay = RetryDelaySeconds;

                    if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
                    {
                        string raw = string.Join(",", values);
                        if (double.TryParse(raw, NumberStyles.Any,
                                            CultureInfo.InvariantCulture, out double parsed))
                            delay = parsed;
                    }

                    Console.WriteLine(
                        $"HTTP {statusCode} received. Retrying in {delay:F0}s " +
                        $"(attempt {attempt + 1}/{MaxRetries})...");

                    await WaitAsync(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                string responseBody =
                    await response.Content.ReadAsStringAsync(cancellationToken)
                                  .ConfigureAwait(false);

                return ParseResponseJson(responseBody, locations.Count);
            }

            // Unreachable: the loop always returns or throws before exhausting retries.
            throw new InvalidOperationException("Retry loop exhausted without returning or throwing.");
        }

        /// <summary>
        /// Delays for the given number of seconds before the next retry attempt.
        /// Override in tests to skip actual waiting.
        /// </summary>
        protected virtual Task WaitAsync(double seconds, CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);

        // ── Internal helpers (internal so unit tests can exercise them directly) ──

        /// <summary>
        /// Builds the Open-Elevation JSON POST body for the given set of locations.
        /// </summary>
        internal static string BuildRequestJson(IReadOnlyList<(double lat, double lon)> locations)
        {
            var sb = new StringBuilder("{\"locations\":[", 64 + locations.Count * 32);
            for (int i = 0; i < locations.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"latitude\":");
                sb.Append(locations[i].lat.ToString("R", CultureInfo.InvariantCulture));
                sb.Append(",\"longitude\":");
                sb.Append(locations[i].lon.ToString("R", CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Parses the Open-Elevation JSON response body and extracts elevation values.
        /// </summary>
        /// <param name="responseBody">Raw JSON string returned by the API.</param>
        /// <param name="expectedCount">Number of elevation values expected.</param>
        /// <returns>Elevation values in the same order as the original request.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the response is missing the <c>results</c> array or its length
        /// does not match <paramref name="expectedCount"/>.
        /// </exception>
        internal static IReadOnlyList<double> ParseResponseJson(string responseBody, int expectedCount)
        {
            using JsonDocument doc  = JsonDocument.Parse(responseBody);
            JsonElement        root = doc.RootElement;

            if (!root.TryGetProperty("results", out JsonElement results))
                throw new InvalidOperationException(
                    "Open-Elevation response is missing the 'results' field.");

            int resultCount = results.GetArrayLength();
            if (resultCount != expectedCount)
                throw new InvalidOperationException(
                    $"Open-Elevation returned {resultCount} result(s) but {expectedCount} were expected.");

            var elevations = new double[expectedCount];
            int idx = 0;
            foreach (JsonElement result in results.EnumerateArray())
                elevations[idx++] = result.GetProperty("elevation").GetDouble();

            return elevations;
        }
    }
}
