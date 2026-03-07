using NUnit.Framework;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    [TestFixture]
    public class MapNodeTests
    {
        // ── Constructor / property tests ───────────────────────────────────────

        [Test]
        public void Constructor_AllParameters_SetsPropertiesCorrectly()
        {
            var node = new MapNode(42L, 51.5074, -0.1278, 12.5);

            Assert.That(node.Id, Is.EqualTo(42L));
            Assert.That(node.Lat, Is.EqualTo(51.5074));
            Assert.That(node.Lon, Is.EqualTo(-0.1278));
            Assert.That(node.Elevation, Is.EqualTo(12.5));
        }

        [Test]
        public void Constructor_WithoutElevation_DefaultsToZero()
        {
            var node = new MapNode(1L, 51.5, -0.1);

            Assert.That(node.Elevation, Is.EqualTo(0.0));
        }

        [Test]
        public void PropertySetters_UpdateValues()
        {
            var node = new MapNode();
            node.Id = 99L;
            node.Lat = 48.8566;
            node.Lon = 2.3522;
            node.Elevation = 35.0;

            Assert.That(node.Id, Is.EqualTo(99L));
            Assert.That(node.Lat, Is.EqualTo(48.8566));
            Assert.That(node.Lon, Is.EqualTo(2.3522));
            Assert.That(node.Elevation, Is.EqualTo(35.0));
        }

        [Test]
        public void NegativeElevation_IsAllowed()
        {
            var node = new MapNode(1L, 0.0, 0.0, -420.0);

            Assert.That(node.Elevation, Is.EqualTo(-420.0));
        }

        // ── Equality tests ─────────────────────────────────────────────────────

        [Test]
        public void Equals_SameValues_ReturnsTrue()
        {
            var a = new MapNode(1L, 51.5, -0.1, 10.0);
            var b = new MapNode(1L, 51.5, -0.1, 10.0);

            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void Equals_DifferentId_ReturnsFalse()
        {
            var a = new MapNode(1L, 51.5, -0.1, 0.0);
            var b = new MapNode(2L, 51.5, -0.1, 0.0);

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_DifferentElevation_ReturnsFalse()
        {
            var a = new MapNode(1L, 51.5, -0.1, 0.0);
            var b = new MapNode(1L, 51.5, -0.1, 5.0);

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void GetHashCode_EqualNodes_ReturnSameHash()
        {
            var a = new MapNode(7L, 40.7128, -74.0060, 10.0);
            var b = new MapNode(7L, 40.7128, -74.0060, 10.0);

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        // ── ToString ───────────────────────────────────────────────────────────

        [Test]
        public void ToString_ContainsAllFields()
        {
            var node = new MapNode(5L, 51.5, -0.1, 7.3);
            string result = node.ToString();

            Assert.That(result, Does.Contain("5"));
            Assert.That(result, Does.Contain("51.5"));
            Assert.That(result, Does.Contain("-0.1"));
            Assert.That(result, Does.Contain("7.3"));
        }
    }
}
