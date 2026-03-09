using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerraDrive.Terrain;

namespace TerraDrive.Tools
{
    /// <summary>
    /// Downloads OpenStreetMap road and building data via the Overpass API for a given
    /// GPS coordinate and radius, then saves the result as a standard <c>.osm</c> file.
    ///
    /// <para><strong>Usage (programmatic)</strong></para>
    /// <code>
    ///   var downloader = new OsmDownloader();
    ///   string xml = await downloader.DownloadOsmAsync(lat: 51.5074, lon: -0.1278, radius: 5000);
    ///   OsmDownloader.SaveOsm(xml, "Assets/Data/london.osm");
    /// </code>
    /// </summary>
    public class OsmDownloader : IOsmDownloader
    {
        /// <summary>Default Overpass API endpoint.</summary>
        public const string DefaultOverpassUrl = "https://overpass-api.de/api/interpreter";

        /// <summary>Maximum number of retry attempts on HTTP 429 responses.</summary>
        internal const int MaxRetries = 5;

        /// <summary>Base delay in seconds for exponential backoff: delay = BackoffBase * 2^attempt.</summary>
        internal const double BackoffBase = 2.0;

        /// <summary>
        /// Horizontal posting interval of SRTM (Shuttle Radar Topography Mission) data in metres.
        /// Used as the default target sample spacing when auto-computing elevation grid dimensions.
        /// </summary>
        public const double SrtmSpacingMetres = 30.0;

        private static readonly string QueryTemplate = string.Join("\n",
            "[out:xml][timeout:90];",
            "(",
            "  way[\"highway\"](around:{radius},{lat},{lon});",
            "  way[\"building\"](around:{radius},{lat},{lon});",
            ");",
            "(._;>;);",
            "out body;");

        private readonly HttpClient _httpClient;
        private readonly string _overpassUrl;

        /// <summary>
        /// Initialises a new instance using the supplied <paramref name="httpClient"/>
        /// and an optional custom endpoint URL.
        /// </summary>
        /// <param name="httpClient">
        /// The HTTP client used to send requests. Pass a pre-configured instance or a
        /// mock for testing.
        /// </param>
        /// <param name="overpassUrl">
        /// Override the default Overpass API endpoint. Defaults to
        /// <see cref="DefaultOverpassUrl"/>.
        /// </param>
        public OsmDownloader(HttpClient httpClient, string overpassUrl = DefaultOverpassUrl)
        {
            _httpClient  = httpClient   ?? throw new ArgumentNullException(nameof(httpClient));
            _overpassUrl = overpassUrl  ?? throw new ArgumentNullException(nameof(overpassUrl));
        }

        /// <summary>
        /// Initialises a new instance with a fresh <see cref="HttpClient"/> and the
        /// default Overpass API endpoint.
        /// </summary>
        public OsmDownloader() : this(new HttpClient()) { }

        /// <summary>
        /// Builds an Overpass QL query string that fetches all <c>highway</c> and
        /// <c>building</c> ways within <paramref name="radius"/> metres of the given
        /// coordinate, including all referenced nodes.
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres.</param>
        /// <returns>Overpass QL query string.</returns>
        public static string BuildQuery(double lat, double lon, int radius)
        {
            return QueryTemplate
                .Replace("{lat}",    lat.ToString(CultureInfo.InvariantCulture))
                .Replace("{lon}",    lon.ToString(CultureInfo.InvariantCulture))
                .Replace("{radius}", radius.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Queries the Overpass API and returns the raw OSM XML response.
        ///
        /// <para>
        /// Retries up to <see cref="MaxRetries"/> times on HTTP 429 (Too Many Requests),
        /// honouring the <c>Retry-After</c> response header when present and falling back
        /// to exponential backoff (<c>BackoffBase * 2^attempt</c> seconds).
        /// </para>
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Raw OSM XML string.</returns>
        /// <exception cref="HttpRequestException">
        /// Thrown if the Overpass API returns a non-2xx status code after all retries are
        /// exhausted (or immediately for non-429 errors).
        /// </exception>
        public async Task<string> DownloadOsmAsync(
            double lat,
            double lon,
            int radius,
            CancellationToken cancellationToken = default)
        {
            string query = BuildQuery(lat, lon, radius);
            Console.WriteLine($"Querying Overpass API (lat={lat}, lon={lon}, radius={radius}m)...");

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                var formData = new FormUrlEncodedContent(
                    new[] { new KeyValuePair<string, string>("data", query) });

                HttpResponseMessage response =
                    await _httpClient.PostAsync(_overpassUrl, formData, cancellationToken)
                                     .ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    if (attempt == MaxRetries)
                        response.EnsureSuccessStatusCode(); // always throws for 4xx/5xx

                    double delay = BackoffBase * Math.Pow(2, attempt);

                    if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
                    {
                        string raw = string.Join(",", values);
                        if (double.TryParse(raw, NumberStyles.Any,
                                            CultureInfo.InvariantCulture, out double parsed))
                            delay = parsed;
                    }

                    Console.WriteLine(
                        $"Rate limited (429). Retrying in {delay:F0}s " +
                        $"(attempt {attempt + 1}/{MaxRetries})...");

                    await WaitAsync(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                string content =
                    await response.Content.ReadAsStringAsync(cancellationToken)
                                  .ConfigureAwait(false);

                Console.WriteLine($"Received {content.Length:N0} bytes from Overpass API.");
                return content;
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

        /// <summary>
        /// Writes <paramref name="content"/> to <paramref name="outputPath"/>, creating
        /// any missing parent directories automatically.
        /// </summary>
        /// <param name="content">OSM XML string to write.</param>
        /// <param name="outputPath">Destination file path.</param>
        public static void SaveOsm(string content, string outputPath)
        {
            string? parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (parent != null)
                Directory.CreateDirectory(parent);

            File.WriteAllText(outputPath, content, Encoding.UTF8);
            Console.WriteLine($"Saved OSM data to: {outputPath}");
        }

        // ── Elevation / DEM ───────────────────────────────────────────────────

        /// <summary>
        /// Computes the geographic bounding box of a circle with the given centre
        /// coordinate and radius, suitable for passing to
        /// <see cref="DownloadElevationGridAsync"/>.
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Radius in metres.</param>
        /// <returns>
        /// A tuple <c>(minLat, maxLat, minLon, maxLon)</c> that encloses the circle.
        /// </returns>
        public static (double minLat, double maxLat, double minLon, double maxLon)
            ComputeBoundingBox(double lat, double lon, int radius)
        {
            const double MetresPerDegree = 111_111.0;
            double deltaLat = radius / MetresPerDegree;
            double deltaLon = radius / (MetresPerDegree * Math.Cos(lat * Math.PI / 180.0));
            return (lat - deltaLat, lat + deltaLat, lon - deltaLon, lon + deltaLon);
        }

        /// <summary>
        /// Computes the number of rows and columns needed to sample a bounding box at
        /// approximately <paramref name="spacingMetres"/> metres between adjacent samples.
        ///
        /// <para>
        /// The returned dimensions are the minimum values ≥ 2 that produce a sample spacing
        /// no coarser than <paramref name="spacingMetres"/> in both the latitude and longitude
        /// directions.  Pass <see cref="SrtmSpacingMetres"/> (30 m) to match the native
        /// resolution of SRTM data served by the Open-Elevation API.
        /// </para>
        /// </summary>
        /// <param name="minLat">Southern boundary (decimal degrees).</param>
        /// <param name="maxLat">Northern boundary (decimal degrees).</param>
        /// <param name="minLon">Western boundary (decimal degrees).</param>
        /// <param name="maxLon">Eastern boundary (decimal degrees).</param>
        /// <param name="spacingMetres">
        /// Desired sample spacing in metres.  Defaults to <see cref="SrtmSpacingMetres"/>.
        /// </param>
        /// <returns>
        /// A <c>(rows, cols)</c> tuple where each value is at least 2.
        /// </returns>
        public static (int rows, int cols) ComputeGridDimensions(
            double minLat, double maxLat,
            double minLon, double maxLon,
            double spacingMetres = SrtmSpacingMetres)
        {
            const double MetresPerDegree = 111_111.0;
            double midLat       = (minLat + maxLat) / 2.0;
            double latSpanM     = (maxLat - minLat) * MetresPerDegree;
            double lonSpanM     = (maxLon - minLon) * MetresPerDegree
                                  * Math.Cos(midLat * Math.PI / 180.0);
            int rows = Math.Max(2, (int)Math.Round(latSpanM / spacingMetres) + 1);
            int cols = Math.Max(2, (int)Math.Round(lonSpanM / spacingMetres) + 1);
            return (rows, cols);
        }

        /// <summary>
        /// Downloads a regular elevation grid for the bounding box that encloses the
        /// given centre coordinate and radius, using the Open-Elevation API (SRTM 30 m
        /// data) by default.
        ///
        /// <para>
        /// When <paramref name="rows"/> or <paramref name="cols"/> is zero (the default),
        /// the grid dimensions are computed automatically via
        /// <see cref="ComputeGridDimensions"/> so that adjacent samples are spaced
        /// approximately <paramref name="targetSpacingMetres"/> metres apart — matching
        /// the native 30 m posting of SRTM data and delivering the highest practical
        /// elevation resolution available from the Open-Elevation API.
        /// </para>
        ///
        /// <para>
        /// Large grids are fetched in batches (see
        /// <see cref="ElevationGrid.SampleAsync"/>) to avoid exceeding the API's
        /// practical per-request limit.
        /// </para>
        ///
        /// <para><strong>Usage (programmatic)</strong></para>
        /// <code>
        ///   var downloader = new OsmDownloader();
        ///   ElevationGrid grid = await downloader.DownloadElevationGridAsync(
        ///       lat: 51.5074, lon: -0.1278, radius: 5000);
        ///   OsmDownloader.SaveElevation(grid, "Assets/Data/london.elevation.csv");
        /// </code>
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres (must match the OSM download radius).</param>
        /// <param name="rows">
        /// Number of latitude samples in the grid.  Pass 0 (the default) to auto-compute
        /// from <paramref name="targetSpacingMetres"/>.
        /// </param>
        /// <param name="cols">
        /// Number of longitude samples in the grid.  Pass 0 (the default) to auto-compute
        /// from <paramref name="targetSpacingMetres"/>.
        /// </param>
        /// <param name="targetSpacingMetres">
        /// Desired sample spacing used when auto-computing grid dimensions.  Defaults to
        /// <see cref="SrtmSpacingMetres"/> (30 m) to match the native SRTM resolution.
        /// Ignored when <paramref name="rows"/> and <paramref name="cols"/> are both
        /// explicitly provided (non-zero).
        /// </param>
        /// <param name="elevationSource">
        /// Optional elevation data source. When <c>null</c> (the default), a new
        /// <see cref="OpenElevationSource"/> backed by the downloader's
        /// <see cref="HttpClient"/> is used.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// An <see cref="ElevationGrid"/> populated with SRTM elevation values for the
        /// bounding box that encloses the given coordinate and radius.
        /// </returns>
        public async Task<ElevationGrid> DownloadElevationGridAsync(
            double lat,
            double lon,
            int radius,
            int rows = 0,
            int cols = 0,
            double targetSpacingMetres = SrtmSpacingMetres,
            IElevationSource? elevationSource = null,
            CancellationToken cancellationToken = default)
        {
            var source = elevationSource ?? new OpenElevationSource(_httpClient);
            var (minLat, maxLat, minLon, maxLon) = ComputeBoundingBox(lat, lon, radius);

            if (rows <= 0 || cols <= 0)
                (rows, cols) = ComputeGridDimensions(minLat, maxLat, minLon, maxLon, targetSpacingMetres);

            Console.WriteLine(
                $"Downloading elevation grid ({rows}×{cols}) for bounding box " +
                $"[{minLat:F4},{maxLat:F4}] × [{minLon:F4},{maxLon:F4}]...");

            ElevationGrid grid = await ElevationGrid.SampleAsync(
                minLat, maxLat, minLon, maxLon, rows, cols, source,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            Console.WriteLine($"Downloaded {rows * cols} elevation samples.");
            return grid;
        }

        /// <summary>
        /// Saves an <see cref="ElevationGrid"/> to a CSV file alongside an
        /// <c>.osm</c> download.
        ///
        /// <para>
        /// Format — line 1 is a header containing six comma-separated fields:
        /// <c>minLat,maxLat,minLon,maxLon,rows,cols</c>.  The following
        /// <c>rows</c> lines each contain <c>cols</c> comma-separated elevation
        /// values in metres (row 0 = southern edge, last row = northern edge).
        /// </para>
        /// </summary>
        /// <param name="grid">Elevation grid to serialise.</param>
        /// <param name="outputPath">Destination file path (directories are created automatically).</param>
        public static void SaveElevation(ElevationGrid grid, string outputPath)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            string? parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (parent != null)
                Directory.CreateDirectory(parent);

            var sb = new StringBuilder();

            // Header: bounds and grid dimensions
            sb.AppendLine(string.Join(",",
                grid.MinLat.ToString("R", CultureInfo.InvariantCulture),
                grid.MaxLat.ToString("R", CultureInfo.InvariantCulture),
                grid.MinLon.ToString("R", CultureInfo.InvariantCulture),
                grid.MaxLon.ToString("R", CultureInfo.InvariantCulture),
                grid.Rows.ToString(CultureInfo.InvariantCulture),
                grid.Cols.ToString(CultureInfo.InvariantCulture)));

            // Elevation rows (south → north)
            for (int r = 0; r < grid.Rows; r++)
            {
                var values = new string[grid.Cols];
                for (int c = 0; c < grid.Cols; c++)
                    values[c] = grid[r, c].ToString("R", CultureInfo.InvariantCulture);
                sb.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"Saved elevation data to: {outputPath}");
        }

        /// <summary>
        /// Loads an <see cref="ElevationGrid"/> from a CSV file previously written by
        /// <see cref="SaveElevation"/>.
        /// </summary>
        /// <param name="path">Path to the <c>.elevation.csv</c> file.</param>
        /// <returns>An <see cref="ElevationGrid"/> reconstructed from the saved data.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the file format is invalid or the row/column counts are inconsistent.
        /// </exception>
        public static ElevationGrid LoadElevationGrid(string path)
            => ElevationGrid.Load(path);
    }
}
