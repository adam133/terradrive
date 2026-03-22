using UnityEngine;

namespace VectorRoad.Core
{
    /// <summary>
    /// Converts WGS-84 GPS coordinates (latitude / longitude) to Unity world-space
    /// metre coordinates using the Web Mercator (EPSG:3857) projection relative to a
    /// configurable map origin.
    ///
    /// A <see cref="WorldOrigin"/> variable is maintained so that the first coordinate
    /// processed (or the explicit origin) maps to world (0, 0, 0), keeping all
    /// offsets small and avoiding single-precision floating-point precision errors.
    ///
    /// Usage:
    ///   // Explicit origin (also sets WorldOrigin):
    ///   Vector3 worldPos = CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon);
    ///
    ///   // Auto-origin (WorldOrigin set on first call, returns Vector3.zero):
    ///   Vector3 first = CoordinateConverter.LatLonToUnity(lat, lon);  // → (0, 0, 0)
    ///   Vector3 next  = CoordinateConverter.LatLonToUnity(lat2, lon2);
    /// </summary>
    public static class CoordinateConverter
    {
        // Earth's equatorial radius in metres (EPSG:3857 / WGS-84).
        private const double EarthRadiusMetres = 6_378_137.0;

        // Backing fields for WorldOrigin.
        private static double _worldOriginX;
        private static double _worldOriginY;
        private static bool   _worldOriginSet;

        /// <summary>
        /// Web Mercator (EPSG:3857) X/Y coordinates (in metres) of the point that
        /// maps to Unity world (0, 0, 0).  Set automatically by the first call to
        /// <see cref="LatLonToUnity(double,double)"/> or by any call to
        /// <see cref="LatLonToUnity(double,double,double,double)"/>.
        /// </summary>
        public static (double X, double Y) WorldOrigin => (_worldOriginX, _worldOriginY);

        /// <summary>
        /// Clears the stored <see cref="WorldOrigin"/> so that the next call to
        /// <see cref="LatLonToUnity(double,double)"/> will re-initialise it.
        /// Useful for testing or when switching to a new map region.
        /// </summary>
        public static void ResetWorldOrigin()
        {
            _worldOriginX   = 0.0;
            _worldOriginY   = 0.0;
            _worldOriginSet = false;
        }

        /// <summary>
        /// Projects a WGS-84 GPS coordinate to a Unity world-space XZ position using
        /// the stored <see cref="WorldOrigin"/>.  The first call auto-initialises
        /// <see cref="WorldOrigin"/> from this coordinate, returning (0, 0, 0).
        /// The Y component is always 0; apply terrain elevation separately.
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees.</param>
        /// <param name="lon">Longitude in decimal degrees.</param>
        /// <returns>World-space position with X = east offset and Z = north offset in metres.</returns>
        public static Vector3 LatLonToUnity(double lat, double lon)
            => LatLonToUnity(lat, lon, 0.0);

        /// <summary>
        /// Projects a WGS-84 GPS coordinate and DEM elevation to a Unity world-space
        /// position using the stored <see cref="WorldOrigin"/>.  The first call
        /// auto-initialises <see cref="WorldOrigin"/> from this coordinate, returning
        /// (0, <paramref name="elevationMetres"/>, 0).
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees.</param>
        /// <param name="lon">Longitude in decimal degrees.</param>
        /// <param name="elevationMetres">
        /// Elevation above sea level in metres (e.g. from SRTM / Open-Elevation).
        /// Maps directly to the Unity Y axis.
        /// </param>
        /// <returns>
        /// World-space position with X = east offset, Y = elevation, Z = north offset,
        /// all in metres.
        /// </returns>
        public static Vector3 LatLonToUnity(double lat, double lon, double elevationMetres)
        {
            double mercX = LonToMercatorX(lon);
            double mercY = LatToMercatorY(lat);

            if (!_worldOriginSet)
            {
                _worldOriginX   = mercX;
                _worldOriginY   = mercY;
                _worldOriginSet = true;
            }

            return new Vector3(
                (float)(mercX - _worldOriginX),
                (float)elevationMetres,
                (float)(mercY - _worldOriginY));
        }

        /// <summary>
        /// Projects a WGS-84 GPS coordinate to a Unity world-space XZ position using
        /// an explicit map origin.  Also updates <see cref="WorldOrigin"/> to the
        /// Mercator coordinates of the supplied origin.
        /// The Y component is always 0; apply terrain elevation separately.
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees.</param>
        /// <param name="lon">Longitude in decimal degrees.</param>
        /// <param name="originLat">Origin latitude — maps to world (0, 0, 0).</param>
        /// <param name="originLon">Origin longitude — maps to world (0, 0, 0).</param>
        /// <returns>World-space position with X = east offset and Z = north offset in metres.</returns>
        public static Vector3 LatLonToUnity(double lat, double lon, double originLat, double originLon)
            => LatLonToUnity(lat, lon, originLat, originLon, 0.0);

        /// <summary>
        /// Projects a WGS-84 GPS coordinate and DEM elevation to a Unity world-space
        /// position using an explicit map origin.  Also updates <see cref="WorldOrigin"/>
        /// to the Mercator coordinates of the supplied origin.
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees.</param>
        /// <param name="lon">Longitude in decimal degrees.</param>
        /// <param name="originLat">Origin latitude — maps to world (0, 0, 0).</param>
        /// <param name="originLon">Origin longitude — maps to world (0, 0, 0).</param>
        /// <param name="elevationMetres">
        /// Elevation above sea level in metres (e.g. from SRTM / Open-Elevation).
        /// Maps directly to the Unity Y axis.
        /// </param>
        /// <returns>
        /// World-space position with X = east offset, Y = elevation, Z = north offset,
        /// all in metres.
        /// </returns>
        public static Vector3 LatLonToUnity(
            double lat, double lon, double originLat, double originLon, double elevationMetres)
        {
            double originMercX = LonToMercatorX(originLon);
            double originMercY = LatToMercatorY(originLat);

            _worldOriginX   = originMercX;
            _worldOriginY   = originMercY;
            _worldOriginSet = true;

            double x = LonToMercatorX(lon) - originMercX;
            double z = LatToMercatorY(lat)  - originMercY;

            return new Vector3((float)x, (float)elevationMetres, (float)z);
        }

        /// <summary>
        /// Converts a Unity world-space XZ position back to WGS-84 GPS coordinates
        /// using the stored <see cref="WorldOrigin"/>.
        /// </summary>
        /// <param name="worldPos">World-space position (Y is ignored).</param>
        /// <returns>GPS coordinate as (latitude, longitude) in decimal degrees.</returns>
        public static (double lat, double lon) UnityToLatLon(Vector3 worldPos)
        {
            double mercX = worldPos.x + _worldOriginX;
            double mercY = worldPos.z + _worldOriginY;

            return (MercatorYToLat(mercY), MercatorXToLon(mercX));
        }

        /// <summary>
        /// Converts a Unity world-space XZ position back to WGS-84 GPS coordinates
        /// using an explicit map origin.
        /// </summary>
        /// <param name="worldPos">World-space position (Y is ignored).</param>
        /// <param name="originLat">Origin latitude used when building the map.</param>
        /// <param name="originLon">Origin longitude used when building the map.</param>
        /// <returns>GPS coordinate as (latitude, longitude) in decimal degrees.</returns>
        public static (double lat, double lon) UnityToLatLon(Vector3 worldPos, double originLat, double originLon)
        {
            double mercX = worldPos.x + LonToMercatorX(originLon);
            double mercY = worldPos.z + LatToMercatorY(originLat);

            return (MercatorYToLat(mercY), MercatorXToLon(mercX));
        }

        // ── Web Mercator helpers ───────────────────────────────────────────────

        // EPSG:3857 forward: longitude (°) → X (m)
        private static double LonToMercatorX(double lonDeg) =>
            EarthRadiusMetres * lonDeg * (System.Math.PI / 180.0);

        // EPSG:3857 forward: latitude (°) → Y (m)
        private static double LatToMercatorY(double latDeg)
        {
            double latRad = latDeg * (System.Math.PI / 180.0);
            return EarthRadiusMetres * System.Math.Log(System.Math.Tan(System.Math.PI / 4.0 + latRad / 2.0));
        }

        // EPSG:3857 inverse: X (m) → longitude (°)
        private static double MercatorXToLon(double x) =>
            x / EarthRadiusMetres * (180.0 / System.Math.PI);

        // EPSG:3857 inverse: Y (m) → latitude (°)
        private static double MercatorYToLat(double y) =>
            (2.0 * System.Math.Atan(System.Math.Exp(y / EarthRadiusMetres)) - System.Math.PI / 2.0)
            * (180.0 / System.Math.PI);
    }
}
