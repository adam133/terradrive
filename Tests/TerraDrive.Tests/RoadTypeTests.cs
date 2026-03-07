using System;
using NUnit.Framework;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    [TestFixture]
    public class RoadTypeTests
    {
        // ── Enum value existence ───────────────────────────────────────────────

        [Test]
        public void RoadType_HasUnknownValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Unknown), Is.True);
        }

        [Test]
        public void RoadType_HasMotorwayValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Motorway), Is.True);
        }

        [Test]
        public void RoadType_HasTrunkValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Trunk), Is.True);
        }

        [Test]
        public void RoadType_HasPrimaryValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Primary), Is.True);
        }

        [Test]
        public void RoadType_HasSecondaryValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Secondary), Is.True);
        }

        [Test]
        public void RoadType_HasTertiaryValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Tertiary), Is.True);
        }

        [Test]
        public void RoadType_HasResidentialValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Residential), Is.True);
        }

        [Test]
        public void RoadType_HasServiceValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Service), Is.True);
        }

        [Test]
        public void RoadType_HasDirtValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Dirt), Is.True);
        }

        [Test]
        public void RoadType_HasPathValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Path), Is.True);
        }

        [Test]
        public void RoadType_HasCyclewayValue()
        {
            Assert.That(Enum.IsDefined(typeof(RoadType), RoadType.Cycleway), Is.True);
        }

        // ── Value distinctness ─────────────────────────────────────────────────

        [Test]
        public void RoadType_AllValuesAreDistinct()
        {
            var values = (RoadType[])Enum.GetValues(typeof(RoadType));
            var distinct = new System.Collections.Generic.HashSet<int>();
            foreach (var v in values)
                Assert.That(distinct.Add((int)v), Is.True,
                    $"Duplicate numeric value found for RoadType.{v}");
        }

        // ── Default value ──────────────────────────────────────────────────────

        [Test]
        public void RoadType_DefaultValue_IsUnknown()
        {
            RoadType defaultType = default;
            Assert.That(defaultType, Is.EqualTo(RoadType.Unknown));
        }
    }
}
