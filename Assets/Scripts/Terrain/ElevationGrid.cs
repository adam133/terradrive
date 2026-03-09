using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TerraDrive.Terrain
{
    /// <summary>
    /// A regular lat/lon grid of terrain elevation samples.
    ///
    /// Row 0 corresponds to <see cref="MinLat"/>; row <see cref="Rows"/>-1 corresponds to
    /// <see cref="MaxLat"/>.  Column 0 corresponds to <see cref="MinLon"/>; column
    /// <see cref="Cols"/>-1 corresponds to <see cref="MaxLon"/>.
    ///
    /// The grid can be constructed directly (e.g. from a cached DEM file) or produced via
    /// <see cref="SampleAsync"/> which batch-fetches elevations from an
    /// <see cref="IElevationSource"/>.
    /// <para>
    /// <see cref="ElevationGrid"/> also implements <see cref="IElevationSource"/> via
    /// <see cref="SampleElevation"/>, so a pre-built grid can be passed directly to
    /// <see cref="ElevationGrid.SampleAsync"/> or to any API that accepts an
    /// <see cref="IElevationSource"/> (e.g. <c>OSMParser.ParseAsync</c>) to lift road
    /// splines and building footprints to match the terrain surface without issuing
    /// additional network requests.
    /// </para>
    /// </summary>
    public sealed class ElevationGrid : IElevationSource
    {
        private readonly double[,] _elevations;

        /// <summary>Southern boundary of the grid in decimal degrees.</summary>
        public double MinLat { get; }

        /// <summary>Northern boundary of the grid in decimal degrees.</summary>
        public double MaxLat { get; }

        /// <summary>Western boundary of the grid in decimal degrees.</summary>
        public double MinLon { get; }

        /// <summary>Eastern boundary of the grid in decimal degrees.</summary>
        public double MaxLon { get; }

        /// <summary>Number of latitude samples (rows south → north).</summary>
        public int Rows { get; }

        /// <summary>Number of longitude samples (columns west → east).</summary>
        public int Cols { get; }

        /// <summary>
        /// Initialises a new <see cref="ElevationGrid"/> from a pre-populated 2-D array.
        /// </summary>
        /// <param name="minLat">Southern boundary (decimal degrees).</param>
        /// <param name="maxLat">Northern boundary (decimal degrees); must be &gt; <paramref name="minLat"/>.</param>
        /// <param name="minLon">Western boundary (decimal degrees).</param>
        /// <param name="maxLon">Eastern boundary (decimal degrees); must be &gt; <paramref name="minLon"/>.</param>
        /// <param name="elevations">
        /// 2-D array of elevation values in metres indexed as [row, col] where row 0 = south
        /// and col 0 = west.  Must have at least 2 rows and 2 columns.
        /// </param>
        public ElevationGrid(
            double minLat, double maxLat,
            double minLon, double maxLon,
            double[,] elevations)
        {
            if (elevations == null)        throw new ArgumentNullException(nameof(elevations));
            if (minLat >= maxLat)          throw new ArgumentException("minLat must be less than maxLat.",  nameof(minLat));
            if (minLon >= maxLon)          throw new ArgumentException("minLon must be less than maxLon.",  nameof(minLon));
            if (elevations.GetLength(0) < 2) throw new ArgumentException("Grid must have at least 2 rows.",    nameof(elevations));
            if (elevations.GetLength(1) < 2) throw new ArgumentException("Grid must have at least 2 columns.", nameof(elevations));

            MinLat      = minLat;
            MaxLat      = maxLat;
            MinLon      = minLon;
            MaxLon      = maxLon;
            _elevations = elevations;
            Rows        = elevations.GetLength(0);
            Cols        = elevations.GetLength(1);
        }

        /// <summary>Gets the elevation (metres) at the specified grid cell.</summary>
        public double this[int row, int col] => _elevations[row, col];

        /// <summary>Returns the latitude in decimal degrees for the given row index.</summary>
        public double LatAtRow(int row)
            => MinLat + (MaxLat - MinLat) * row / (Rows - 1);

        /// <summary>Returns the longitude in decimal degrees for the given column index.</summary>
        public double LonAtCol(int col)
            => MinLon + (MaxLon - MinLon) * col / (Cols - 1);

        /// <summary>
        /// Returns the terrain elevation in metres at the given geographic coordinate using
        /// bilinear interpolation between the four nearest grid cells.
        ///
        /// <para>
        /// Coordinates outside the grid extent are clamped to the nearest boundary so that
        /// callers do not need to guard against minor floating-point overshoots.
        /// </para>
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees.</param>
        /// <param name="lon">Longitude in decimal degrees.</param>
        /// <returns>Interpolated elevation in metres above sea level.</returns>
        public double SampleElevation(double lat, double lon)
        {
            // Clamp to grid bounds so out-of-range values get the nearest edge value.
            lat = Math.Clamp(lat, MinLat, MaxLat);
            lon = Math.Clamp(lon, MinLon, MaxLon);

            // Continuous row/column index (0-based, floating point).
            double rowF = (lat - MinLat) / (MaxLat - MinLat) * (Rows - 1);
            double colF = (lon - MinLon) / (MaxLon - MinLon) * (Cols - 1);

            // Integer cell indices for the south-west corner of the enclosing quad.
            int r0 = Math.Clamp((int)Math.Floor(rowF), 0, Rows - 2);
            int c0 = Math.Clamp((int)Math.Floor(colF), 0, Cols - 2);
            int r1 = r0 + 1;
            int c1 = c0 + 1;

            // Fractional offsets within the cell.
            double tr = rowF - r0;
            double tc = colF - c0;

            // Bilinear interpolation across the four cell corners.
            return _elevations[r0, c0] * (1.0 - tr) * (1.0 - tc)
                 + _elevations[r0, c1] * (1.0 - tr) * tc
                 + _elevations[r1, c0] * tr           * (1.0 - tc)
                 + _elevations[r1, c1] * tr           * tc;
        }

        /// <summary>
        /// Implements <see cref="IElevationSource"/> by sampling this grid via
        /// <see cref="SampleElevation"/> for each supplied location.
        ///
        /// <para>
        /// This allows a pre-built <see cref="ElevationGrid"/> to be used wherever an
        /// <see cref="IElevationSource"/> is expected — for example, passing a terrain grid
        /// directly to <c>OSMParser.ParseAsync</c> to raise road splines and building
        /// footprints to match the terrain surface without issuing additional network
        /// requests (Unity scene wiring).
        /// </para>
        /// </summary>
        public Task<IReadOnlyList<double>> FetchElevationsAsync(
            IReadOnlyList<(double lat, double lon)> locations,
            CancellationToken cancellationToken = default)
        {
            if (locations == null) throw new ArgumentNullException(nameof(locations));

            var results = new double[locations.Count];
            for (int i = 0; i < locations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[i] = SampleElevation(locations[i].lat, locations[i].lon);
            }

            return Task.FromResult<IReadOnlyList<double>>(results);
        }

        /// <summary>
        /// Samples terrain elevation for a regular grid of lat/lon points using the supplied
        /// <paramref name="elevationSource"/> and returns the results as an
        /// <see cref="ElevationGrid"/>.
        ///
        /// <para>
        /// Points are sampled in row-major order (all columns of row 0 first, then row 1,
        /// etc.).  When the total number of grid points exceeds <paramref name="batchSize"/>,
        /// requests are split into multiple calls of at most <paramref name="batchSize"/> points
        /// each, allowing large high-resolution grids to be fetched without exceeding the
        /// Open-Elevation API's practical per-request limit.
        /// </para>
        /// </summary>
        /// <param name="minLat">Southern boundary (decimal degrees).</param>
        /// <param name="maxLat">Northern boundary; must be &gt; <paramref name="minLat"/>.</param>
        /// <param name="minLon">Western boundary (decimal degrees).</param>
        /// <param name="maxLon">Eastern boundary; must be &gt; <paramref name="minLon"/>.</param>
        /// <param name="rows">Number of latitude samples (≥ 2).</param>
        /// <param name="cols">Number of longitude samples (≥ 2).</param>
        /// <param name="elevationSource">DEM data source used to fetch elevation values.</param>
        /// <param name="batchSize">
        /// Maximum number of locations sent to the elevation source in a single call.
        /// When the grid contains more points than this limit, multiple calls are made
        /// and their results concatenated.  Defaults to 512, which fits comfortably within
        /// the Open-Elevation public API's recommended request size.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// An <see cref="ElevationGrid"/> whose values are populated from the elevation source.
        /// </returns>
        public static async Task<ElevationGrid> SampleAsync(
            double minLat, double maxLat,
            double minLon, double maxLon,
            int rows, int cols,
            IElevationSource elevationSource,
            int batchSize = 512,
            CancellationToken cancellationToken = default)
        {
            if (elevationSource == null)
                throw new ArgumentNullException(nameof(elevationSource));
            if (rows < 2)
                throw new ArgumentOutOfRangeException(nameof(rows), "Grid must have at least 2 rows.");
            if (cols < 2)
                throw new ArgumentOutOfRangeException(nameof(cols), "Grid must have at least 2 columns.");
            if (batchSize < 1)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be at least 1.");

            // Build ordered flat list of locations (row-major: all cols of row 0, then row 1…)
            var locations = new List<(double lat, double lon)>(rows * cols);
            for (int r = 0; r < rows; r++)
            {
                double lat = minLat + (maxLat - minLat) * r / (rows - 1);
                for (int c = 0; c < cols; c++)
                {
                    double lon = minLon + (maxLon - minLon) * c / (cols - 1);
                    locations.Add((lat, lon));
                }
            }

            // Fetch elevations in batches to stay within the API's practical request size.
            var allElevations = new double[locations.Count];
            int fetched = 0;
            while (fetched < locations.Count)
            {
                int count = Math.Min(batchSize, locations.Count - fetched);
                var batch = locations.GetRange(fetched, count);
                IReadOnlyList<double> batchResult =
                    await elevationSource.FetchElevationsAsync(batch, cancellationToken)
                                         .ConfigureAwait(false);
                for (int i = 0; i < count; i++)
                    allElevations[fetched + i] = batchResult[i];
                fetched += count;
            }

            var grid = new double[rows, cols];
            int idx = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = allElevations[idx++];

            return new ElevationGrid(minLat, maxLat, minLon, maxLon, grid);
        }
        /// <summary>
        /// Loads an <see cref="ElevationGrid"/> from a CSV file previously written by
        /// <c>OsmDownloader.SaveElevation</c>.
        ///
        /// <para>
        /// CSV format:
        /// <list type="bullet">
        ///   <item>Line 0 (header): <c>minLat,maxLat,minLon,maxLon,rows,cols</c></item>
        ///   <item>Lines 1…rows: comma-separated elevation values, one row per line,
        ///         south edge first.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="path">Path to the <c>.elevation.csv</c> file.</param>
        /// <returns>An <see cref="ElevationGrid"/> reconstructed from the saved data.</returns>
        /// <exception cref="FileNotFoundException">Thrown when <paramref name="path"/> does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file format is invalid.</exception>
        public static ElevationGrid Load(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);

            if (lines.Length < 1)
                throw new InvalidOperationException("Elevation CSV is empty.");

            string[] header = lines[0].Split(',');
            if (header.Length < 6)
                throw new InvalidOperationException(
                    $"Elevation CSV header has {header.Length} field(s); expected 6 " +
                    "(minLat,maxLat,minLon,maxLon,rows,cols).");

            double minLat = double.Parse(header[0], CultureInfo.InvariantCulture);
            double maxLat = double.Parse(header[1], CultureInfo.InvariantCulture);
            double minLon = double.Parse(header[2], CultureInfo.InvariantCulture);
            double maxLon = double.Parse(header[3], CultureInfo.InvariantCulture);
            int    rows   = int.Parse(header[4], CultureInfo.InvariantCulture);
            int    cols   = int.Parse(header[5], CultureInfo.InvariantCulture);

            if (lines.Length < 1 + rows)
                throw new InvalidOperationException(
                    $"Elevation CSV has {lines.Length - 1} data row(s) but the header " +
                    $"declares {rows}.");

            var elevations = new double[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                string[] values = lines[1 + r].Split(',');
                if (values.Length < cols)
                    throw new InvalidOperationException(
                        $"Elevation CSV data row {r} has {values.Length} value(s) but " +
                        $"the header declares {cols} columns.");

                for (int c = 0; c < cols; c++)
                    elevations[r, c] = double.Parse(values[c], CultureInfo.InvariantCulture);
            }

            return new ElevationGrid(minLat, maxLat, minLon, maxLon, elevations);
        }
    }
}
