using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TerraDrive.Terrain;
using TerraDrive.Tools;

namespace TerraDrive.Core
{
    /// <summary>
    /// Encapsulates the game-menu logic for choosing a new real-world location by
    /// entering GPS coordinates.
    ///
    /// <para>
    /// When the player confirms new lat/lon coordinates this controller:
    /// <list type="number">
    ///   <item>
    ///     Downloads OSM road/building data for that location via the Overpass API.
    ///   </item>
    ///   <item>
    ///     Downloads the elevation grid (SRTM DEM) for that location.
    ///   </item>
    ///   <item>
    ///     Loads and generates the full <see cref="MapData"/> (roads, buildings, terrain
    ///     mesh) via <see cref="MapLoader.LoadMapAsync"/>.
    ///   </item>
    /// </list>
    /// After a successful load the player car should be moved to
    /// <see cref="LocationLoadResult.PlayerSpawnPosition"/> — always world origin (0, 0, 0) —
    /// and <c>GameManager.Instance.SetLocation</c> should be called with the new coordinates.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   var menu = new LocationMenuController();
    ///   menu.Latitude  = 48.8566;
    ///   menu.Longitude =  2.3522;
    ///
    ///   LocationLoadResult result = await menu.LoadLocationAsync();
    ///
    ///   // Move the player car:
    ///   playerTransform.position = result.PlayerSpawnPosition;   // (0,0,0)
    ///
    ///   // Keep GameManager in sync:
    ///   GameManager.Instance.SetLocation(result.OriginLatitude, result.OriginLongitude);
    /// </code>
    /// </summary>
    public class LocationMenuController
    {
        private readonly IOsmDownloader _downloader;
        private readonly string _dataDirectory;

        /// <summary>Default search radius in metres when <see cref="Radius"/> is not set.</summary>
        public const int DefaultRadius = 500;

        /// <summary>
        /// Latitude entered via the menu UI.  Must be in the range [-90, 90].
        /// Set this before calling <see cref="LoadLocationAsync"/>.
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude entered via the menu UI.  Must be in the range [-180, 180].
        /// Set this before calling <see cref="LoadLocationAsync"/>.
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// OSM/elevation search radius in metres.  Defaults to <see cref="DefaultRadius"/>
        /// when zero or negative.
        /// </summary>
        public int Radius { get; set; } = DefaultRadius;

        /// <summary>
        /// Initialises a new instance with an injectable <see cref="IOsmDownloader"/>
        /// and a directory where downloaded data files are cached.
        /// </summary>
        /// <param name="downloader">Downloader used for Overpass and DEM requests.</param>
        /// <param name="dataDirectory">Directory in which <c>current.osm</c> and
        /// <c>current.elevation.csv</c> are written.</param>
        public LocationMenuController(IOsmDownloader downloader, string dataDirectory)
        {
            _downloader    = downloader    ?? throw new ArgumentNullException(nameof(downloader));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        /// <summary>
        /// Initialises a new instance using the default <see cref="OsmDownloader"/> and
        /// a <c>terradrive</c> subdirectory inside the system temp folder.
        /// </summary>
        public LocationMenuController()
            : this(new OsmDownloader(), Path.Combine(Path.GetTempPath(), "terradrive")) { }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="latitude"/> and
        /// <paramref name="longitude"/> are within valid WGS-84 ranges.
        /// </summary>
        /// <param name="latitude">Latitude in decimal degrees.</param>
        /// <param name="longitude">Longitude in decimal degrees.</param>
        public static bool IsValidCoordinate(double latitude, double longitude) =>
            latitude  >= -90.0  && latitude  <= 90.0 &&
            longitude >= -180.0 && longitude <= 180.0;

        /// <summary>
        /// Downloads OSM and elevation data for the current <see cref="Latitude"/> /
        /// <see cref="Longitude"/>, generates the <see cref="MapData"/>, and returns a
        /// <see cref="LocationLoadResult"/> that contains all generated data and the
        /// player car spawn position.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A <see cref="LocationLoadResult"/> whose
        /// <see cref="LocationLoadResult.PlayerSpawnPosition"/> is always
        /// <c>Vector3.zero</c> — move the player car there after this call succeeds.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <see cref="Latitude"/> or <see cref="Longitude"/> is outside
        /// the valid WGS-84 range.
        /// </exception>
        public async Task<LocationLoadResult> LoadLocationAsync(
            CancellationToken cancellationToken = default)
        {
            if (Latitude < -90.0 || Latitude > 90.0)
                throw new ArgumentOutOfRangeException(nameof(Latitude), Latitude,
                    "Latitude must be in the range [-90, 90].");
            if (Longitude < -180.0 || Longitude > 180.0)
                throw new ArgumentOutOfRangeException(nameof(Longitude), Longitude,
                    "Longitude must be in the range [-180, 180].");

            int radius = Radius > 0 ? Radius : DefaultRadius;

            Directory.CreateDirectory(_dataDirectory);

            string osmPath  = Path.Combine(_dataDirectory, "current.osm");
            string elevPath = Path.Combine(_dataDirectory, "current.elevation.csv");

            // 1. Download OSM road/building data.
            string osmXml = await _downloader
                .DownloadOsmAsync(Latitude, Longitude, radius, cancellationToken)
                .ConfigureAwait(false);
            OsmDownloader.SaveOsm(osmXml, osmPath);

            // 2. Download DEM elevation grid.
            ElevationGrid elevGrid = await _downloader
                .DownloadElevationGridAsync(Latitude, Longitude, radius,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            OsmDownloader.SaveElevation(elevGrid, elevPath);

            // 3. Reset the coordinate system so the new coordinate maps to (0, 0, 0).
            CoordinateConverter.ResetWorldOrigin();

            // 4. Load and generate all map data (roads, buildings, terrain mesh).
            MapData mapData = await MapLoader
                .LoadMapAsync(osmPath, elevPath, Latitude, Longitude, cancellationToken)
                .ConfigureAwait(false);

            return new LocationLoadResult(mapData, Latitude, Longitude);
        }
    }
}
