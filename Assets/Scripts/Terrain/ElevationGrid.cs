using System;
using System.Collections.Generic;
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
    /// </summary>
    public sealed class ElevationGrid
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
        /// Samples terrain elevation for a regular grid of lat/lon points using the supplied
        /// <paramref name="elevationSource"/> and returns the results as an
        /// <see cref="ElevationGrid"/>.
        ///
        /// Points are sampled in row-major order (all columns of row 0 first, then row 1,
        /// etc.) so they can be passed directly to
        /// <see cref="IElevationSource.FetchElevationsAsync"/> as a single batch.
        /// </summary>
        /// <param name="minLat">Southern boundary (decimal degrees).</param>
        /// <param name="maxLat">Northern boundary; must be &gt; <paramref name="minLat"/>.</param>
        /// <param name="minLon">Western boundary (decimal degrees).</param>
        /// <param name="maxLon">Eastern boundary; must be &gt; <paramref name="minLon"/>.</param>
        /// <param name="rows">Number of latitude samples (≥ 2).</param>
        /// <param name="cols">Number of longitude samples (≥ 2).</param>
        /// <param name="elevationSource">DEM data source used to fetch elevation values.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// An <see cref="ElevationGrid"/> whose values are populated from the elevation source.
        /// </returns>
        public static async Task<ElevationGrid> SampleAsync(
            double minLat, double maxLat,
            double minLon, double maxLon,
            int rows, int cols,
            IElevationSource elevationSource,
            CancellationToken cancellationToken = default)
        {
            if (elevationSource == null)
                throw new ArgumentNullException(nameof(elevationSource));
            if (rows < 2)
                throw new ArgumentOutOfRangeException(nameof(rows), "Grid must have at least 2 rows.");
            if (cols < 2)
                throw new ArgumentOutOfRangeException(nameof(cols), "Grid must have at least 2 columns.");

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

            IReadOnlyList<double> elevations =
                await elevationSource.FetchElevationsAsync(locations, cancellationToken)
                                     .ConfigureAwait(false);

            var grid = new double[rows, cols];
            int idx = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = elevations[idx++];

            return new ElevationGrid(minLat, maxLat, minLon, maxLon, grid);
        }
    }
}
