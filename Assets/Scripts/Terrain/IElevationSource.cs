using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VectorRoad.Terrain
{
    /// <summary>
    /// Abstraction over a DEM (Digital Elevation Model) data source.
    ///
    /// Implementations must return elevations in the same order as the
    /// supplied <c>locations</c> list so that callers can correlate results
    /// by index without additional look-ups.
    /// </summary>
    public interface IElevationSource
    {
        /// <summary>
        /// Fetches terrain elevation values for a batch of geographic coordinates.
        /// </summary>
        /// <param name="locations">
        /// Ordered list of (latitude, longitude) pairs in decimal degrees.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// Elevation in metres above sea level for each input location,
        /// in the same order as <paramref name="locations"/>.
        /// Returns an empty list when <paramref name="locations"/> is empty.
        /// </returns>
        Task<IReadOnlyList<double>> FetchElevationsAsync(
            IReadOnlyList<(double lat, double lon)> locations,
            CancellationToken cancellationToken = default);
    }
}
