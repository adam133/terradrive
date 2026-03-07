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

        // ── LatLonToUnity ──────────────────────────────────────────────────────

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
        public void LatLonToUnity_OneDegreeNorth_MatchesExpectedMetres()
        {
            // 1° of latitude ≈ EarthRadius * π/180 metres ≈ 111 319 m
            double expected = EarthRadius * (Math.PI / 180.0);
            Vector3 result = CoordinateConverter.LatLonToUnity(52.5, 0.0, 51.5, 0.0);

            Assert.That(result.z, Is.EqualTo((float)expected).Within(1.0f),
                "1° north should be ~111 319 m in Z");
        }

        [Test]
        public void LatLonToUnity_OneDegreeEastAtEquator_MatchesExpectedMetres()
        {
            // At the equator 1° longitude ≈ EarthRadius * π/180 metres
            double expected = EarthRadius * (Math.PI / 180.0);
            Vector3 result = CoordinateConverter.LatLonToUnity(0.0, 1.0, 0.0, 0.0);

            Assert.That(result.x, Is.EqualTo((float)expected).Within(1.0f),
                "1° east at equator should be ~111 319 m in X");
        }

        // ── UnityToLatLon ──────────────────────────────────────────────────────

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
    }
}
