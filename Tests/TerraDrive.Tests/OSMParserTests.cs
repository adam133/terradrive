using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OSMParser"/>.
    ///
    /// Each test builds a minimal in-memory .osm XML string, writes it to a
    /// temporary file, calls OSMParser.Parse, and asserts on the resulting
    /// RoadSegment / BuildingFootprint lists.
    /// </summary>
    [TestFixture]
    public class OSMParserTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        private static string WriteTempOsm(string osmXml)
        {
            string path = Path.GetTempFileName() + ".osm";
            File.WriteAllText(path, osmXml);
            return path;
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        // ── Road parsing ───────────────────────────────────────────────────────

        [Test]
        public void Parse_SingleRoadWay_ReturnsOneRoadSegment()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5000' lon='-0.1000'/>
  <node id='2' lat='51.5010' lon='-0.1010'/>
  <way id='100'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='primary'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, buildings) = OSMParser.Parse(path, 51.5000, -0.1000);

                Assert.That(roads.Count, Is.EqualTo(1), "Expected one road segment");
                Assert.That(buildings.Count, Is.EqualTo(0));

                RoadSegment road = roads[0];
                Assert.That(road.WayId, Is.EqualTo(100));
                Assert.That(road.HighwayType, Is.EqualTo("primary"));
                Assert.That(road.Nodes.Count, Is.EqualTo(2));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_RoadNodeAtOrigin_MapsToWorldZero()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5000' lon='-0.1278'/>
  <node id='2' lat='51.5010' lon='-0.1278'/>
  <way id='10'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='residential'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, _) = OSMParser.Parse(path, 51.5000, -0.1278);

                // The first node is exactly at the origin, so it should project to (0,0,0)
                Vector3 origin = roads[0].Nodes[0];
                Assert.That(origin.x, Is.EqualTo(0f).Within(0.01f), "Origin node X should be 0");
                Assert.That(origin.y, Is.EqualTo(0f), "Y should always be 0");
                Assert.That(origin.z, Is.EqualTo(0f).Within(0.01f), "Origin node Z should be 0");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_SecondNodeNorthOfOrigin_HasPositiveZ()
        {
            // Node 2 is north of node 1 (higher latitude)
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5000' lon='-0.1278'/>
  <node id='2' lat='51.5100' lon='-0.1278'/>
  <way id='10'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='service'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, _) = OSMParser.Parse(path, 51.5000, -0.1278);

                Vector3 northNode = roads[0].Nodes[1];
                Assert.That(northNode.z, Is.GreaterThan(0f), "Node north of origin should have positive Z");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeEastOfOrigin_HasPositiveX()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5000' lon='-0.1278'/>
  <node id='2' lat='51.5000' lon='-0.1000'/>
  <way id='10'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='motorway'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, _) = OSMParser.Parse(path, 51.5000, -0.1278);

                Vector3 eastNode = roads[0].Nodes[1];
                Assert.That(eastNode.x, Is.GreaterThan(0f), "Node east of origin should have positive X");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_RoadTagsAreCopied()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.0' lon='-0.1'/>
  <node id='2' lat='51.1' lon='-0.1'/>
  <way id='42'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='trunk'/>
    <tag k='name' v='Test Road'/>
    <tag k='maxspeed' v='70'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, _) = OSMParser.Parse(path, 51.0, -0.1);

                Assert.That(roads[0].Tags["highway"], Is.EqualTo("trunk"));
                Assert.That(roads[0].Tags["name"], Is.EqualTo("Test Road"));
                Assert.That(roads[0].Tags["maxspeed"], Is.EqualTo("70"));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_MultipleRoads_AllReturned()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <node id='2' lat='51.6' lon='-0.1'/>
  <node id='3' lat='51.5' lon='-0.2'/>
  <way id='1'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='primary'/>
  </way>
  <way id='2'>
    <nd ref='2'/><nd ref='3'/>
    <tag k='highway' v='secondary'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, _) = OSMParser.Parse(path, 51.5, -0.1);

                Assert.That(roads.Count, Is.EqualTo(2));
            }
            finally { DeleteFile(path); }
        }

        // ── Building parsing ───────────────────────────────────────────────────

        [Test]
        public void Parse_SingleBuildingWay_ReturnsOneBuildingFootprint()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.50' lon='-0.10'/>
  <node id='2' lat='51.50' lon='-0.09'/>
  <node id='3' lat='51.51' lon='-0.09'/>
  <node id='4' lat='51.51' lon='-0.10'/>
  <way id='200'>
    <nd ref='1'/><nd ref='2'/><nd ref='3'/><nd ref='4'/><nd ref='1'/>
    <tag k='building' v='yes'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, buildings) = OSMParser.Parse(path, 51.50, -0.10);

                Assert.That(roads.Count, Is.EqualTo(0));
                Assert.That(buildings.Count, Is.EqualTo(1));

                BuildingFootprint building = buildings[0];
                Assert.That(building.WayId, Is.EqualTo(200));
                Assert.That(building.Footprint.Count, Is.EqualTo(5), "Closed ring has 5 nodes");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_BuildingTagsAreCopied()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.0' lon='-0.1'/>
  <node id='2' lat='51.0' lon='-0.0'/>
  <node id='3' lat='51.1' lon='-0.1'/>
  <way id='99'>
    <nd ref='1'/><nd ref='2'/><nd ref='3'/>
    <tag k='building' v='residential'/>
    <tag k='building:levels' v='3'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, buildings) = OSMParser.Parse(path, 51.0, -0.1);

                Assert.That(buildings[0].Tags["building"], Is.EqualTo("residential"));
                Assert.That(buildings[0].Tags["building:levels"], Is.EqualTo("3"));
            }
            finally { DeleteFile(path); }
        }

        // ── Mixed content ──────────────────────────────────────────────────────

        [Test]
        public void Parse_MixedRoadsAndBuildings_CorrectlySegregated()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <node id='2' lat='51.6' lon='-0.1'/>
  <node id='3' lat='51.5' lon='-0.2'/>
  <way id='10'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='residential'/>
  </way>
  <way id='20'>
    <nd ref='1'/><nd ref='2'/><nd ref='3'/>
    <tag k='building' v='yes'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, buildings) = OSMParser.Parse(path, 51.5, -0.1);

                Assert.That(roads.Count, Is.EqualTo(1));
                Assert.That(buildings.Count, Is.EqualTo(1));
                Assert.That(roads[0].WayId, Is.EqualTo(10));
                Assert.That(buildings[0].WayId, Is.EqualTo(20));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_WayWithUnknownTags_IsIgnored()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <node id='2' lat='51.6' lon='-0.1'/>
  <way id='30'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='landuse' v='forest'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, buildings) = OSMParser.Parse(path, 51.5, -0.1);

                Assert.That(roads.Count, Is.EqualTo(0));
                Assert.That(buildings.Count, Is.EqualTo(0));
            }
            finally { DeleteFile(path); }
        }

        // ── Edge cases ─────────────────────────────────────────────────────────

        [Test]
        public void Parse_EmptyOsmFile_ReturnsEmptyLists()
        {
            string osm = @"<?xml version='1.0'?><osm version='0.6'/>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, buildings) = OSMParser.Parse(path, 0, 0);

                Assert.That(roads.Count, Is.EqualTo(0));
                Assert.That(buildings.Count, Is.EqualTo(0));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_WayReferencingMissingNode_NodeIsSkipped()
        {
            // Node 99 is referenced by the way but not declared in the file
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <way id='10'>
    <nd ref='1'/><nd ref='99'/>
    <tag k='highway' v='primary'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (roads, _) = OSMParser.Parse(path, 51.5, -0.1);

                // The way is still returned, but only the valid node is included
                Assert.That(roads.Count, Is.EqualTo(1));
                Assert.That(roads[0].Nodes.Count, Is.EqualTo(1),
                    "Only the declared node should appear; missing node is skipped");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_FileNotFound_ThrowsException()
        {
            // DirectoryNotFoundException / FileNotFoundException both inherit IOException
            Assert.Catch<System.IO.IOException>(() =>
                OSMParser.Parse("/non/existent/path.osm", 0, 0));
        }
    }
}
