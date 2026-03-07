using UnityEngine;

namespace TerraDrive.Core
{
    /// <summary>
    /// Converts WGS-84 GPS coordinates (latitude / longitude) to Unity world-space
    /// metre coordinates using an Equirectangular (Plate Carrée) projection relative
    /// to a configurable map origin.
    ///
    /// Usage:
    ///   Vector3 worldPos = CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon);
    /// </summary>
    public static class CoordinateConverter
    {
        // Earth's mean radius in metres (WGS-84 approximation).
        private const double EarthRadiusMetres = 6_378_137.0;

        /// <summary>
        /// Projects a WGS-84 GPS coordinate to a Unity world-space XZ position.
        /// The Y component is set to 0; terrain elevation should be applied separately.
        /// </summary>
        /// <param name="lat">Latitude in decimal degrees.</param>
        /// <param name="lon">Longitude in decimal degrees.</param>
        /// <param name="originLat">Origin latitude — maps to world (0, 0, 0).</param>
        /// <param name="originLon">Origin longitude — maps to world (0, 0, 0).</param>
        /// <returns>World-space position with X = east offset and Z = north offset in metres.</returns>
        public static Vector3 LatLonToUnity(double lat, double lon, double originLat, double originLon)
        {
            double originLatRad = originLat * Mathf.Deg2Rad;

            // Equirectangular projection
            double x = (lon - originLon) * Mathf.Deg2Rad * EarthRadiusMetres * System.Math.Cos(originLatRad);
            double z = (lat - originLat) * Mathf.Deg2Rad * EarthRadiusMetres;

            return new Vector3((float)x, 0f, (float)z);
        }

        /// <summary>
        /// Converts a Unity world-space XZ position back to WGS-84 GPS coordinates.
        /// </summary>
        /// <param name="worldPos">World-space position (Y is ignored).</param>
        /// <param name="originLat">Origin latitude used when building the map.</param>
        /// <param name="originLon">Origin longitude used when building the map.</param>
        /// <returns>GPS coordinate as (latitude, longitude) in decimal degrees.</returns>
        public static (double lat, double lon) UnityToLatLon(Vector3 worldPos, double originLat, double originLon)
        {
            double originLatRad = originLat * Mathf.Deg2Rad;

            double lat = originLat + (worldPos.z / EarthRadiusMetres) * Mathf.Rad2Deg;
            double lon = originLon + (worldPos.x / (EarthRadiusMetres * System.Math.Cos(originLatRad))) * Mathf.Rad2Deg;

            return (lat, lon);
        }
    }
}
