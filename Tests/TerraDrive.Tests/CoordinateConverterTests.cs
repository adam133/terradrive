using System;
using NUnit.Framework;
using UnityEngine;
using TerraDrive.Core;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Unit tests for <see cref="CoordinateConverter"/>.
    /// </summary>
    [TestFixture]
    public class CoordinateConverterTests
    {
        private const double EarthRadius = 6_378_137.0;

        [SetUp]
        public void SetUp()
        {
            // Ensure each test starts with a clean WorldOrigin state.
            CoordinateConverter.ResetWorldOrigin();
        }

        // ── LatLonToUnity (explicit origin) ───────────────────────────────────

        [Test]
        public void LatLonToUnity_AtOrigin_ReturnsZeroVector()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(51.5, -0.12, 51.5, -0.12);

            Assert.That(result.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void LatLonToUnity_YComponentIsAlwaysZero()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(52.0, 0.0, 51.5, -0.12);

            Assert.That(result.y, Is.EqualTo(0f));
        }

        [Test]
        public void LatLonToUnity_NorthOfOrigin_HasPositiveZ()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(51.6, -0.12, 51.5, -0.12);

            Assert.That(result.z, Is.GreaterThan(0f), "Northward offset should be positive Z");
        }

        [Test]
        public void LatLonToUnity_SouthOfOrigin_HasNegativeZ()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(51.4, -0.12, 51.5, -0.12);

            Assert.That(result.z, Is.LessThan(0f), "Southward offset should be negative Z");
        }

        [Test]
        public void LatLonToUnity_EastOfOrigin_HasPositiveX()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(51.5, 0.0, 51.5, -0.12);

            Assert.That(result.x, Is.GreaterThan(0f), "Eastward offset should be positive X");
        }

        [Test]
        public void LatLonToUnity_WestOfOrigin_HasNegativeX()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(51.5, -0.2, 51.5, -0.12);

            Assert.That(result.x, Is.LessThan(0f), "Westward offset should be negative X");
        }

        [Test]
        public void LatLonToUnity_OneDegreeNorth_MatchesWebMercatorMetres()
        {
            // Web Mercator Y offset for 1° of latitude at ~52° ≈ 180 823 m
            // Y = R * (ln(tan(π/4 + 52.5°/2)) − ln(tan(π/4 + 51.5°/2)))
            double y1 = EarthRadius * Math.Log(Math.Tan(Math.PI / 4.0 + 51.5 * Math.PI / 360.0));
            double y2 = EarthRadius * Math.Log(Math.Tan(Math.PI / 4.0 + 52.5 * Math.PI / 360.0));
            double expected = y2 - y1;

            Vector3 result = CoordinateConverter.LatLonToUnity(52.5, 0.0, 51.5, 0.0);

            Assert.That(result.z, Is.EqualTo((float)expected).Within(1.0f),
                "1° north at ~52° should match Web Mercator Y offset");
        }

        [Test]
        public void LatLonToUnity_OneDegreeEastAtEquator_MatchesExpectedMetres()
        {
            // At the equator Web Mercator X = R * λ, same as equirectangular.
            double expected = EarthRadius * (Math.PI / 180.0);
            Vector3 result = CoordinateConverter.LatLonToUnity(0.0, 1.0, 0.0, 0.0);

            Assert.That(result.x, Is.EqualTo((float)expected).Within(1.0f),
                "1° east at equator should be ~111 319 m in X");
        }

        // ── UnityToLatLon (explicit origin) ───────────────────────────────────

        [Test]
        public void UnityToLatLon_AtWorldZero_ReturnsOrigin()
        {
            double originLat = 51.5;
            double originLon = -0.12;

            var (lat, lon) = CoordinateConverter.UnityToLatLon(Vector3.zero, originLat, originLon);

            Assert.That(lat, Is.EqualTo(originLat).Within(1e-6));
            Assert.That(lon, Is.EqualTo(originLon).Within(1e-6));
        }

        [Test]
        public void UnityToLatLon_RoundTrip_PreservesCoordinates()
        {
            double originLat = 51.5074;
            double originLon = -0.1278;
            double inputLat = 51.52;
            double inputLon = -0.10;

            Vector3 worldPos = CoordinateConverter.LatLonToUnity(inputLat, inputLon, originLat, originLon);
            var (outLat, outLon) = CoordinateConverter.UnityToLatLon(worldPos, originLat, originLon);

            Assert.That(outLat, Is.EqualTo(inputLat).Within(1e-4),
                "Round-trip latitude should match input within 10 m");
            Assert.That(outLon, Is.EqualTo(inputLon).Within(1e-4),
                "Round-trip longitude should match input within 10 m");
        }

        // ── WorldOrigin auto-initialisation ───────────────────────────────────

        [Test]
        public void WorldOrigin_AutoInit_FirstCallReturnsZeroVector()
        {
            // With no explicit origin, first call should return (0, 0, 0).
            Vector3 result = CoordinateConverter.LatLonToUnity(51.5, -0.12);

            Assert.That(result.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(result.y, Is.EqualTo(0f));
            Assert.That(result.z, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void WorldOrigin_AutoInit_SecondCallIsRelativeToFirst()
        {
            CoordinateConverter.LatLonToUnity(51.5, -0.12);   // sets origin
            Vector3 second = CoordinateConverter.LatLonToUnity(51.6, -0.12);

            Assert.That(second.x, Is.EqualTo(0f).Within(1e-3f),
                "Same longitude → X offset should be zero");
            Assert.That(second.z, Is.GreaterThan(0f),
                "More northerly point should have positive Z");
        }

        [Test]
        public void WorldOrigin_ExplicitOrigin_SetsWorldOriginProperty()
        {
            double originLat = 51.5;
            double originLon = -0.12;
            CoordinateConverter.LatLonToUnity(51.5, -0.12, originLat, originLon);

            // X = R * originLon (in radians)
            double expectedX = EarthRadius * originLon * (Math.PI / 180.0);
            // Y = R * ln(tan(π/4 + originLat/2))
            double expectedY = EarthRadius * Math.Log(Math.Tan(Math.PI / 4.0 + originLat * Math.PI / 360.0));

            Assert.That(CoordinateConverter.WorldOrigin.X, Is.EqualTo(expectedX).Within(1e-3),
                "WorldOrigin.X should equal Mercator X of origin longitude");
            Assert.That(CoordinateConverter.WorldOrigin.Y, Is.EqualTo(expectedY).Within(1e-3),
                "WorldOrigin.Y should equal Mercator Y of origin latitude");
        }

        [Test]
        public void WorldOrigin_Reset_AllowsReInitialisation()
        {
            CoordinateConverter.LatLonToUnity(51.5, -0.12);   // sets origin
            CoordinateConverter.ResetWorldOrigin();
            Vector3 afterReset = CoordinateConverter.LatLonToUnity(52.0, 0.0);

            Assert.That(afterReset.x, Is.EqualTo(0f).Within(1e-3f),
                "After reset the new first call should return zero X");
            Assert.That(afterReset.z, Is.EqualTo(0f).Within(1e-3f),
                "After reset the new first call should return zero Z");
        }

        [Test]
        public void UnityToLatLon_AutoOrigin_RoundTrip()
        {
            double inputLat = 51.52;
            double inputLon = -0.10;

            // First call sets origin automatically.
            CoordinateConverter.LatLonToUnity(51.5, -0.12);
            Vector3 worldPos = CoordinateConverter.LatLonToUnity(inputLat, inputLon);
            var (outLat, outLon) = CoordinateConverter.UnityToLatLon(worldPos);

            Assert.That(outLat, Is.EqualTo(inputLat).Within(1e-4),
                "Round-trip latitude should match input");
            Assert.That(outLon, Is.EqualTo(inputLon).Within(1e-4),
                "Round-trip longitude should match input");
        }

        // ── Elevation-aware overloads ──────────────────────────────────────────

        [Test]
        public void LatLonToUnity_WithElevation_AutoOrigin_SetsYComponent()
        {
            const double elevation = 42.5;
            Vector3 result = CoordinateConverter.LatLonToUnity(51.5, -0.12, elevation);

            Assert.That(result.y, Is.EqualTo((float)elevation).Within(1e-3f),
                "Y should equal the supplied elevation");
        }

        [Test]
        public void LatLonToUnity_WithZeroElevation_AutoOrigin_YIsZero()
        {
            Vector3 result = CoordinateConverter.LatLonToUnity(51.5, -0.12, 0.0);

            Assert.That(result.y, Is.EqualTo(0f));
        }

        [Test]
        public void LatLonToUnity_WithNegativeElevation_AutoOrigin_YIsNegative()
        {
            const double elevation = -100.0;
            Vector3 result = CoordinateConverter.LatLonToUnity(0.0, 0.0, elevation);

            Assert.That(result.y, Is.EqualTo((float)elevation).Within(1e-3f),
                "Negative elevation (e.g. Dead Sea) should produce negative Y");
        }

        [Test]
        public void LatLonToUnity_WithElevation_ExplicitOrigin_SetsYComponent()
        {
            const double elevation = 300.0;
            Vector3 result = CoordinateConverter.LatLonToUnity(51.6, -0.12, 51.5, -0.12, elevation);

            Assert.That(result.y, Is.EqualTo((float)elevation).Within(1e-3f),
                "Y should equal the supplied elevation");
        }

        [Test]
        public void LatLonToUnity_WithElevation_ExplicitOrigin_XAndZUnchanged()
        {
            // Reference: same call without elevation should yield identical X/Z.
            // We reset between the two calls because the explicit-origin overload
            // updates the static WorldOrigin; the [SetUp] only runs between tests,
            // so we reset manually here to keep the two conversions independent.
            Vector3 noElev  = CoordinateConverter.LatLonToUnity(51.6, 0.0, 51.5, -0.12);
            CoordinateConverter.ResetWorldOrigin();
            Vector3 withElev = CoordinateConverter.LatLonToUnity(51.6, 0.0, 51.5, -0.12, 50.0);

            Assert.That(withElev.x, Is.EqualTo(noElev.x).Within(1e-3f),
                "Elevation should not affect the X offset");
            Assert.That(withElev.z, Is.EqualTo(noElev.z).Within(1e-3f),
                "Elevation should not affect the Z offset");
        }

        [Test]
        public void LatLonToUnity_NoElevationOverload_YRemainsZero()
        {
            // The zero-elevation convenience overloads must still return Y=0
            Vector3 autoOrigin    = CoordinateConverter.LatLonToUnity(51.5, -0.12);
            Vector3 explicitOrigin = CoordinateConverter.LatLonToUnity(51.5, -0.12, 51.5, -0.12);

            Assert.That(autoOrigin.y,    Is.EqualTo(0f), "Auto-origin overload: Y must be 0");
            Assert.That(explicitOrigin.y, Is.EqualTo(0f), "Explicit-origin overload: Y must be 0");
        }
    }
}
