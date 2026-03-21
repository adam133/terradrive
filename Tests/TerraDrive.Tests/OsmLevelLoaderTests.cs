using NUnit.Framework;
using TerraDrive.Core;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OsmLevelLoader"/> — the pure-C# helper that validates
    /// GPS-coordinate settings for the
    /// <b>TerraDrive → Load OSM File / Generate Level</b> editor menu item.
    /// </summary>
    [TestFixture]
    public class OsmLevelLoaderTests
    {
        // ── Default values ────────────────────────────────────────────────────

        [Test]
        public void DefaultRadius_IsExpected()
        {
            Assert.That(OsmLevelLoader.DefaultRadius, Is.EqualTo(500));
        }

        [Test]
        public void DefaultConstruction_HasExpectedDefaults()
        {
            var loader = new OsmLevelLoader();

            Assert.That(loader.Latitude,  Is.EqualTo(0.0));
            Assert.That(loader.Longitude, Is.EqualTo(0.0));
            Assert.That(loader.Radius,    Is.EqualTo(OsmLevelLoader.DefaultRadius));
        }

        // ── IsValid — valid coordinates ───────────────────────────────────────

        [Test]
        public void IsValid_ValidCoordinates_ReturnsTrue()
        {
            var loader = new OsmLevelLoader
            {
                Latitude  = 51.5074,
                Longitude = -0.1278,
                Radius    = 500,
            };

            Assert.That(loader.IsValid(), Is.True);
        }

        [Test]
        public void IsValid_ZeroCoordinates_ReturnsTrue()
        {
            // (0, 0) is a valid coordinate (Atlantic Ocean off Gabon).
            var loader = new OsmLevelLoader
            {
                Latitude  = 0.0,
                Longitude = 0.0,
                Radius    = 1000,
            };

            Assert.That(loader.IsValid(), Is.True);
        }

        [Test]
        public void IsValid_ExactBoundaryLatitude_ReturnsTrue()
        {
            var loader = new OsmLevelLoader { Latitude = 90.0,  Longitude = 0.0, Radius = 100 };
            Assert.That(loader.IsValid(), Is.True);

            loader.Latitude = -90.0;
            Assert.That(loader.IsValid(), Is.True);
        }

        [Test]
        public void IsValid_ExactBoundaryLongitude_ReturnsTrue()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 180.0,  Radius = 100 };
            Assert.That(loader.IsValid(), Is.True);

            loader.Longitude = -180.0;
            Assert.That(loader.IsValid(), Is.True);
        }

        [Test]
        public void Validate_ValidCoordinates_ReturnsEmptyList()
        {
            var loader = new OsmLevelLoader
            {
                Latitude  = 48.8566,
                Longitude =  2.3522,
                Radius    = 1000,
            };

            Assert.That(loader.Validate(), Is.Empty);
        }

        // ── Latitude validation ───────────────────────────────────────────────

        [Test]
        public void IsValid_LatitudeTooHigh_ReturnsFalse()
        {
            var loader = new OsmLevelLoader { Latitude = 90.001, Longitude = 0.0, Radius = 500 };

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void IsValid_LatitudeTooLow_ReturnsFalse()
        {
            var loader = new OsmLevelLoader { Latitude = -90.001, Longitude = 0.0, Radius = 500 };

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void Validate_LatitudeOutOfRange_ContainsLatitudeError()
        {
            var loader = new OsmLevelLoader { Latitude = 91.0, Longitude = 0.0, Radius = 500 };

            var errors = loader.Validate();

            Assert.That(errors, Has.Some.Contains("Latitude"));
        }

        [Test]
        public void Validate_LatitudeOutOfRange_ContainsExactValue()
        {
            var loader = new OsmLevelLoader { Latitude = 95.0, Longitude = 0.0, Radius = 500 };

            var errors = loader.Validate();

            Assert.That(errors, Has.Some.Contains("95"));
        }

        // ── Longitude validation ──────────────────────────────────────────────

        [Test]
        public void IsValid_LongitudeTooHigh_ReturnsFalse()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 180.001, Radius = 500 };

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void IsValid_LongitudeTooLow_ReturnsFalse()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = -180.001, Radius = 500 };

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void Validate_LongitudeOutOfRange_ContainsLongitudeError()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 200.0, Radius = 500 };

            var errors = loader.Validate();

            Assert.That(errors, Has.Some.Contains("Longitude"));
        }

        // ── Radius validation ─────────────────────────────────────────────────

        [Test]
        public void IsValid_RadiusZero_ReturnsFalse()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 0.0, Radius = 0 };

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void IsValid_RadiusNegative_ReturnsFalse()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 0.0, Radius = -1 };

            Assert.That(loader.IsValid(), Is.False);
        }

        [Test]
        public void Validate_RadiusZero_ContainsRadiusError()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 0.0, Radius = 0 };

            var errors = loader.Validate();

            Assert.That(errors, Has.Some.Contains("Radius"));
        }

        [Test]
        public void IsValid_RadiusOne_ReturnsTrue()
        {
            var loader = new OsmLevelLoader { Latitude = 0.0, Longitude = 0.0, Radius = 1 };

            Assert.That(loader.IsValid(), Is.True);
        }

        // ── Multiple errors ───────────────────────────────────────────────────

        [Test]
        public void Validate_AllInvalid_ReturnsThreeErrors()
        {
            var loader = new OsmLevelLoader
            {
                Latitude  = 100.0,
                Longitude = 200.0,
                Radius    = -5,
            };

            var errors = loader.Validate();

            Assert.That(errors.Count, Is.EqualTo(3));
        }

        // ── Property round-trip ───────────────────────────────────────────────

        [Test]
        public void Latitude_SetValue_RoundTrips()
        {
            var loader = new OsmLevelLoader { Latitude = 41.8957 };
            Assert.That(loader.Latitude, Is.EqualTo(41.8957).Within(1e-9));
        }

        [Test]
        public void Longitude_SetValue_RoundTrips()
        {
            var loader = new OsmLevelLoader { Longitude = -93.5888 };
            Assert.That(loader.Longitude, Is.EqualTo(-93.5888).Within(1e-9));
        }

        [Test]
        public void Radius_SetValue_RoundTrips()
        {
            var loader = new OsmLevelLoader { Radius = 1234 };
            Assert.That(loader.Radius, Is.EqualTo(1234));
        }

        // ── TryParseCoordinates ───────────────────────────────────────────────

        [Test]
        public void TryParseCoordinates_ValidInput_ReturnsTrueAndCorrectValues()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("51.5074, -0.1278", out double lat, out double lon);

            Assert.That(result, Is.True);
            Assert.That(lat, Is.EqualTo(51.5074).Within(1e-9));
            Assert.That(lon, Is.EqualTo(-0.1278).Within(1e-9));
        }

        [Test]
        public void TryParseCoordinates_NoSpaces_ReturnsTrueAndCorrectValues()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("48.8566,2.3522", out double lat, out double lon);

            Assert.That(result, Is.True);
            Assert.That(lat, Is.EqualTo(48.8566).Within(1e-9));
            Assert.That(lon, Is.EqualTo(2.3522).Within(1e-9));
        }

        [Test]
        public void TryParseCoordinates_NegativeLatAndLon_ReturnsTrueAndCorrectValues()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("-33.8688, -70.6693", out double lat, out double lon);

            Assert.That(result, Is.True);
            Assert.That(lat, Is.EqualTo(-33.8688).Within(1e-9));
            Assert.That(lon, Is.EqualTo(-70.6693).Within(1e-9));
        }

        [Test]
        public void TryParseCoordinates_ExtraWhitespace_ReturnsTrueAndCorrectValues()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("  51.5074  ,  -0.1278  ", out double lat, out double lon);

            Assert.That(result, Is.True);
            Assert.That(lat, Is.EqualTo(51.5074).Within(1e-9));
            Assert.That(lon, Is.EqualTo(-0.1278).Within(1e-9));
        }

        [Test]
        public void TryParseCoordinates_NullInput_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates(null, out double lat, out double lon);

            Assert.That(result, Is.False);
            Assert.That(lat, Is.EqualTo(0.0));
            Assert.That(lon, Is.EqualTo(0.0));
        }

        [Test]
        public void TryParseCoordinates_EmptyInput_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates(string.Empty, out double lat, out double lon);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseCoordinates_NoComma_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("51.5074 -0.1278", out double lat, out double lon);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseCoordinates_SingleNumberOnly_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("51.5074", out double lat, out double lon);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseCoordinates_NonNumericParts_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("abc, def", out double lat, out double lon);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseCoordinates_OnlyComma_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates(",", out double lat, out double lon);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseCoordinates_WhiteSpaceOnly_ReturnsFalse()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("   ", out double lat, out double lon);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryParseCoordinates_IntegerCoordinates_ReturnsTrueAndCorrectValues()
        {
            bool result = OsmLevelLoader.TryParseCoordinates("51, 0", out double lat, out double lon);

            Assert.That(result, Is.True);
            Assert.That(lat, Is.EqualTo(51.0).Within(1e-9));
            Assert.That(lon, Is.EqualTo(0.0).Within(1e-9));
        }
    }
}
