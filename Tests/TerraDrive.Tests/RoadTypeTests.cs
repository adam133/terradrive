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

        // ── RoadTypeParser.Parse ───────────────────────────────────────────────

        [TestCase("motorway",        RoadType.Motorway)]
        [TestCase("motorway_link",   RoadType.Motorway)]
        [TestCase("trunk",           RoadType.Trunk)]
        [TestCase("trunk_link",      RoadType.Trunk)]
        [TestCase("primary",         RoadType.Primary)]
        [TestCase("primary_link",    RoadType.Primary)]
        [TestCase("secondary",       RoadType.Secondary)]
        [TestCase("secondary_link",  RoadType.Secondary)]
        [TestCase("tertiary",        RoadType.Tertiary)]
        [TestCase("tertiary_link",   RoadType.Tertiary)]
        [TestCase("residential",     RoadType.Residential)]
        [TestCase("living_street",   RoadType.Residential)]
        [TestCase("service",         RoadType.Service)]
        [TestCase("track",           RoadType.Dirt)]
        [TestCase("dirt_road",       RoadType.Dirt)]
        [TestCase("path",            RoadType.Path)]
        [TestCase("footway",         RoadType.Path)]
        [TestCase("steps",           RoadType.Path)]
        [TestCase("cycleway",        RoadType.Cycleway)]
        public void RoadTypeParser_Parse_KnownTag_ReturnsExpected(
            string tag, RoadType expected)
        {
            Assert.That(RoadTypeParser.Parse(tag), Is.EqualTo(expected));
        }

        [Test]
        public void RoadTypeParser_Parse_UnknownTag_ReturnsResidential()
        {
            Assert.That(RoadTypeParser.Parse("pedestrian"), Is.EqualTo(RoadType.Residential));
        }

        [Test]
        public void RoadTypeParser_Parse_NullTag_ReturnsResidential()
        {
            Assert.That(RoadTypeParser.Parse(null), Is.EqualTo(RoadType.Residential));
        }

        [Test]
        public void RoadTypeParser_Parse_EmptyTag_ReturnsResidential()
        {
            Assert.That(RoadTypeParser.Parse(string.Empty), Is.EqualTo(RoadType.Residential));
        }

        [Test]
        public void RoadTypeParser_Parse_IsCaseInsensitive()
        {
            Assert.That(RoadTypeParser.Parse("MOTORWAY"),  Is.EqualTo(RoadType.Motorway));
            Assert.That(RoadTypeParser.Parse("Primary"),   Is.EqualTo(RoadType.Primary));
            Assert.That(RoadTypeParser.Parse("SECONDARY"), Is.EqualTo(RoadType.Secondary));
        }
    }
}

