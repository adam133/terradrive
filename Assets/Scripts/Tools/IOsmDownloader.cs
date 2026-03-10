using System.Threading;
using System.Threading.Tasks;
using TerraDrive.Terrain;

namespace TerraDrive.Tools
{
    public interface IOsmDownloader
    {
        Task<string> DownloadOsmAsync(
            double lat,
            double lon,
            int radius,
            CancellationToken cancellationToken = default);

        Task<ElevationGrid> DownloadElevationGridAsync(
            double lat,
            double lon,
            int radius,
            int rows = 0,
            int cols = 0,
            double targetSpacingMetres = OsmDownloader.SrtmSpacingMetres,
            IElevationSource? elevationSource = null,
            CancellationToken cancellationToken = default);
    }
}