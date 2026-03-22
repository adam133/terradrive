using System;
using NUnit.Framework;
using VectorRoad.DataInversion;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class RegionTypeTests
    {
        // ── Enum value existence ───────────────────────────────────────────────

        [Test]
        public void RegionType_HasUnknownValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Unknown), Is.True);
        }

        [Test]
        public void RegionType_HasTemperateValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Temperate), Is.True);
        }

        [Test]
        public void RegionType_HasTemperateNorthAmericaValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.TemperateNorthAmerica), Is.True);
        }

        [Test]
        public void RegionType_HasDesertValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Desert), Is.True);
        }

        [Test]
        public void RegionType_HasTropicalValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Tropical), Is.True);
        }

        [Test]
        public void RegionType_HasBorealValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Boreal), Is.True);
        }

        [Test]
        public void RegionType_HasArcticValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Arctic), Is.True);
        }

        [Test]
        public void RegionType_HasMediterraneanValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Mediterranean), Is.True);
        }

        [Test]
        public void RegionType_HasSteppeValue()
        {
            Assert.That(Enum.IsDefined(typeof(RegionType), RegionType.Steppe), Is.True);
        }

        // ── Value distinctness ─────────────────────────────────────────────────

        [Test]
        public void RegionType_AllValuesAreDistinct()
        {
            var values = (RegionType[])Enum.GetValues(typeof(RegionType));
            var distinct = new System.Collections.Generic.HashSet<int>();
            foreach (var v in values)
                Assert.That(distinct.Add((int)v), Is.True,
                    $"Duplicate numeric value found for RegionType.{v}");
        }

        // ── Default value ──────────────────────────────────────────────────────

        [Test]
        public void RegionType_DefaultValue_IsUnknown()
        {
            RegionType defaultType = default;
            Assert.That(defaultType, Is.EqualTo(RegionType.Unknown));
        }
    }
}
