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
    public class OsmDownloader : IOsmDownloader
    {
        public const string DefaultOverpassUrl = "https://overpass-api.de/api/interpreter";
        internal const int MaxRetries = 5;
        internal const double BackoffBase = 2.0;
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

        public OsmDownloader(HttpClient httpClient, string overpassUrl = DefaultOverpassUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _overpassUrl = overpassUrl ?? throw new ArgumentNullException(nameof(overpassUrl));
        }

        public OsmDownloader() : this(new HttpClient()) { }

        public static string BuildQuery(double lat, double lon, int radius)
        {
            return QueryTemplate
                .Replace("{lat}", lat.ToString(CultureInfo.InvariantCulture))
                .Replace("{lon}", lon.ToString(CultureInfo.InvariantCulture))
                .Replace("{radius}", radius.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<string> DownloadOsmAsync(
            double lat,
            double lon,
            int radius,
            CancellationToken cancellationToken = default)
        {
            string query = BuildQuery(lat, lon, radius);

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                using var formData = new FormUrlEncodedContent(
                    new[] { new KeyValuePair<string, string>("data", query) });

                HttpResponseMessage response =
                    await _httpClient.PostAsync(_overpassUrl, formData, cancellationToken)
                                     .ConfigureAwait(false);

                if ((int)response.StatusCode == 429)
                {
                    if (attempt == MaxRetries)
                        response.EnsureSuccessStatusCode();

                    double delay = BackoffBase * Math.Pow(2, attempt);

                    if (response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values))
                    {
                        string raw = string.Join(",", values);
                        if (double.TryParse(raw, NumberStyles.Any,
                                            CultureInfo.InvariantCulture, out double parsed))
                            delay = parsed;
                    }

                    await WaitAsync(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync()
                                             .ConfigureAwait(false);
            }

            throw new InvalidOperationException("Retry loop exhausted without returning or throwing.");
        }

        protected virtual Task WaitAsync(double seconds, CancellationToken cancellationToken) =>
            Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);

        public static void SaveOsm(string content, string outputPath)
        {
            string? parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (parent != null)
                Directory.CreateDirectory(parent);

            File.WriteAllText(outputPath, content, Encoding.UTF8);
        }

        public static (double minLat, double maxLat, double minLon, double maxLon)
            ComputeBoundingBox(double lat, double lon, int radius)
        {
            const double MetresPerDegree = 111_111.0;
            double deltaLat = radius / MetresPerDegree;
            double deltaLon = radius / (MetresPerDegree * Math.Cos(lat * Math.PI / 180.0));
            return (lat - deltaLat, lat + deltaLat, lon - deltaLon, lon + deltaLon);
        }

        public static (int rows, int cols) ComputeGridDimensions(
            double minLat, double maxLat,
            double minLon, double maxLon,
            double spacingMetres = SrtmSpacingMetres)
        {
            const double MetresPerDegree = 111_111.0;
            double midLat = (minLat + maxLat) / 2.0;
            double latSpanM = (maxLat - minLat) * MetresPerDegree;
            double lonSpanM = (maxLon - minLon) * MetresPerDegree
                              * Math.Cos(midLat * Math.PI / 180.0);
            int rows = Math.Max(2, (int)Math.Round(latSpanM / spacingMetres) + 1);
            int cols = Math.Max(2, (int)Math.Round(lonSpanM / spacingMetres) + 1);
            return (rows, cols);
        }

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

            return await ElevationGrid.SampleAsync(
                minLat, maxLat, minLon, maxLon, rows, cols, source,
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public static void SaveElevation(ElevationGrid grid, string outputPath)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            string? parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (parent != null)
                Directory.CreateDirectory(parent);

            var sb = new StringBuilder();

            sb.AppendLine(string.Join(",",
                grid.MinLat.ToString("R", CultureInfo.InvariantCulture),
                grid.MaxLat.ToString("R", CultureInfo.InvariantCulture),
                grid.MinLon.ToString("R", CultureInfo.InvariantCulture),
                grid.MaxLon.ToString("R", CultureInfo.InvariantCulture),
                grid.Rows.ToString(CultureInfo.InvariantCulture),
                grid.Cols.ToString(CultureInfo.InvariantCulture)));

            for (int r = 0; r < grid.Rows; r++)
            {
                var values = new string[grid.Cols];
                for (int c = 0; c < grid.Cols; c++)
                    values[c] = grid[r, c].ToString("R", CultureInfo.InvariantCulture);
                sb.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        public static ElevationGrid LoadElevationGrid(string path)
            => ElevationGrid.Load(path);
    }
}