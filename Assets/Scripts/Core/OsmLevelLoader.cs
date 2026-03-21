using System.Collections.Generic;
using System.Globalization;

namespace TerraDrive.Core
{
    /// <summary>
    /// Holds the GPS-coordinate settings for a
    /// <b>TerraDrive → Load OSM File / Generate Level</b> operation and validates them
    /// before the Editor downloads data and wires it into a
    /// <see cref="MapSceneBuilder"/> component.
    ///
    /// <para>
    /// This class is pure C# (no Unity Engine dependency) so it can be exercised by the
    /// .NET unit-test suite as well as by the Unity Editor script.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   var loader = new OsmLevelLoader
    ///   {
    ///       Latitude  = 51.5074,
    ///       Longitude = -0.1278,
    ///       Radius    = 500,
    ///   };
    ///
    ///   IReadOnlyList&lt;string&gt; errors = loader.Validate();
    ///   if (errors.Count == 0)
    ///   {
    ///       // Trigger download, configure MapSceneBuilder, and enter Play mode.
    ///   }
    /// </code>
    /// </summary>
    public sealed class OsmLevelLoader
    {
        // ── Constants ──────────────────────────────────────────────────────────

        /// <summary>Default OSM/elevation search radius in metres.</summary>
        public const int DefaultRadius = 500;

        // ── Settings ───────────────────────────────────────────────────────────

        /// <summary>
        /// Latitude of the map origin in decimal degrees (WGS-84).
        /// Must be in the range [−90, 90].
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude of the map origin in decimal degrees (WGS-84).
        /// Must be in the range [−180, 180].
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Download radius in metres around <see cref="Latitude"/> /
        /// <see cref="Longitude"/>.  Defaults to <see cref="DefaultRadius"/>.
        /// Must be greater than zero.
        /// </summary>
        public int Radius { get; set; } = DefaultRadius;

        // ── Validation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Validates the current settings and returns a list of human-readable error
        /// messages.  An empty list means the settings are valid and a download can
        /// be started.
        /// </summary>
        /// <returns>
        /// A read-only list of error strings.  Empty when <see cref="Latitude"/> is
        /// in [−90, 90], <see cref="Longitude"/> is in [−180, 180], and
        /// <see cref="Radius"/> is greater than zero.
        /// </returns>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            if (Latitude < -90.0 || Latitude > 90.0)
                errors.Add($"Latitude must be in the range [-90, 90] (got {Latitude}).");

            if (Longitude < -180.0 || Longitude > 180.0)
                errors.Add($"Longitude must be in the range [-180, 180] (got {Longitude}).");

            if (Radius <= 0)
                errors.Add($"Radius must be greater than 0 (got {Radius}).");

            return errors;
        }

        /// <summary>
        /// Returns <c>true</c> when <see cref="Validate"/> produces no errors.
        /// </summary>
        public bool IsValid() => Validate().Count == 0;

        // ── Coordinate parsing ─────────────────────────────────────────────────

        /// <summary>
        /// Tries to parse a coordinate string of the form <c>"lat, lon"</c> (whitespace
        /// around the comma is ignored) into separate latitude and longitude values.
        /// Both parts must be valid decimal numbers.
        /// </summary>
        /// <param name="input">Raw text entered by the user, e.g. <c>"51.5074, -0.1278"</c>.</param>
        /// <param name="lat">Parsed latitude on success; <c>0</c> otherwise.</param>
        /// <param name="lon">Parsed longitude on success; <c>0</c> otherwise.</param>
        /// <returns>
        /// <c>true</c> when <paramref name="input"/> contains exactly one comma that
        /// separates two parseable decimal numbers; <c>false</c> otherwise.
        /// </returns>
        public static bool TryParseCoordinates(string input, out double lat, out double lon)
        {
            lat = 0;
            lon = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            int commaIndex = input.IndexOf(',');
            if (commaIndex < 0)
                return false;

            string latPart = input.Substring(0, commaIndex).Trim();
            string lonPart = input.Substring(commaIndex + 1).Trim();

            return double.TryParse(latPart, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                && double.TryParse(lonPart, NumberStyles.Float, CultureInfo.InvariantCulture, out lon);
        }
    }
}
