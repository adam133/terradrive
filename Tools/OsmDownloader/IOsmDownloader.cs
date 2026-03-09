using System.Threading;
using System.Threading.Tasks;
using TerraDrive.Terrain;

namespace TerraDrive.Tools
{
    /// <summary>
    /// Abstraction over <see cref="OsmDownloader"/> that allows dependency injection
    /// and unit testing of consumers such as <c>LocationMenuController</c>.
    /// </summary>
    public interface IOsmDownloader
    {
        /// <summary>
        /// Queries the Overpass API and returns the raw OSM XML response.
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Raw OSM XML string.</returns>
        Task<string> DownloadOsmAsync(
            double lat,
            double lon,
            int radius,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads a regular elevation grid for the bounding box that encloses the
        /// given centre coordinate and radius.
        /// </summary>
        /// <param name="lat">Centre latitude in decimal degrees (WGS-84).</param>
        /// <param name="lon">Centre longitude in decimal degrees (WGS-84).</param>
        /// <param name="radius">Search radius in metres.</param>
        /// <param name="rows">Number of latitude samples in the grid (default: 32).</param>
        /// <param name="cols">Number of longitude samples in the grid (default: 32).</param>
        /// <param name="elevationSource">
        /// Optional elevation data source. When <c>null</c> (the default), a new
        /// <see cref="OpenElevationSource"/> is used.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>An <see cref="ElevationGrid"/> populated with elevation values.</returns>
        Task<ElevationGrid> DownloadElevationGridAsync(
            double lat,
            double lon,
            int radius,
            int rows = 32,
            int cols = 32,
            IElevationSource? elevationSource = null,
            CancellationToken cancellationToken = default);
    }
}
