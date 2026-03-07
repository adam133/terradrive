using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using TerraDrive.DataInversion;
using TerraDrive.Terrain;

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
                var (roads, buildings, _) = OSMParser.Parse(path, 51.5000, -0.1000);

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
                var (roads, _, _) = OSMParser.Parse(path, 51.5000, -0.1278);

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
                var (roads, _, _) = OSMParser.Parse(path, 51.5000, -0.1278);

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
                var (roads, _, _) = OSMParser.Parse(path, 51.5000, -0.1278);

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
                var (roads, _, _) = OSMParser.Parse(path, 51.0, -0.1);

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
                var (roads, _, _) = OSMParser.Parse(path, 51.5, -0.1);

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
                var (roads, buildings, _) = OSMParser.Parse(path, 51.50, -0.10);

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
                var (_, buildings, _) = OSMParser.Parse(path, 51.0, -0.1);

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
                var (roads, buildings, _) = OSMParser.Parse(path, 51.5, -0.1);

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
                var (roads, buildings, _) = OSMParser.Parse(path, 51.5, -0.1);

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
                var (roads, buildings, _) = OSMParser.Parse(path, 0, 0);

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
                var (roads, _, _) = OSMParser.Parse(path, 51.5, -0.1);

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

        // ── Region detection ───────────────────────────────────────────────────

        [Test]
        public void Parse_NodeWithAddrCountryGB_ReturnsTemperateRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'>
    <tag k='addr:country' v='GB'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 51.5, -0.1);
                Assert.That(region, Is.EqualTo(RegionType.Temperate));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeWithCountryAE_ReturnsDesertRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='24.4' lon='54.4'>
    <tag k='country' v='AE'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 24.4, 54.4);
                Assert.That(region, Is.EqualTo(RegionType.Desert));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeWithAddrCountryBR_ReturnsTropicalRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='-3.1' lon='-60.0'>
    <tag k='addr:country' v='BR'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, -3.1, -60.0);
                Assert.That(region, Is.EqualTo(RegionType.Tropical));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeWithCountryFI_ReturnsBorealRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='64.9' lon='25.7'>
    <tag k='country' v='FI'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 64.9, 25.7);
                Assert.That(region, Is.EqualTo(RegionType.Boreal));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeWithCountryGL_ReturnsArcticRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='72.0' lon='-40.0'>
    <tag k='country' v='GL'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 72.0, -40.0);
                Assert.That(region, Is.EqualTo(RegionType.Arctic));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeWithAddrCountryIT_ReturnsMediterraneanRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='41.9' lon='12.5'>
    <tag k='addr:country' v='IT'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 41.9, 12.5);
                Assert.That(region, Is.EqualTo(RegionType.Mediterranean));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NodeWithCountryKZ_ReturnsSteppeRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.2' lon='71.4'>
    <tag k='country' v='KZ'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 51.2, 71.4);
                Assert.That(region, Is.EqualTo(RegionType.Steppe));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_NoCountryTags_ReturnsUnknownRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <node id='2' lat='51.6' lon='-0.1'/>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 51.5, -0.1);
                Assert.That(region, Is.EqualTo(RegionType.Unknown));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_UnrecognisedCountryCode_ReturnsUnknownRegion()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='0.0' lon='0.0'>
    <tag k='addr:country' v='XX'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 0.0, 0.0);
                Assert.That(region, Is.EqualTo(RegionType.Unknown));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_MajorityCountryCodeWins()
        {
            // Three DE nodes (Temperate) and one SA node (Desert) — Temperate should win
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='52.5' lon='13.4'>
    <tag k='addr:country' v='DE'/>
  </node>
  <node id='2' lat='52.5' lon='13.5'>
    <tag k='addr:country' v='DE'/>
  </node>
  <node id='3' lat='52.5' lon='13.6'>
    <tag k='addr:country' v='DE'/>
  </node>
  <node id='4' lat='24.7' lon='46.7'>
    <tag k='addr:country' v='SA'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 52.5, 13.4);
                Assert.That(region, Is.EqualTo(RegionType.Temperate));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public void Parse_CountryTagCaseInsensitive_ReturnsCorrectRegion()
        {
            // Tags with lowercase country code should still map correctly
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'>
    <tag k='addr:country' v='gb'/>
  </node>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var (_, _, region) = OSMParser.Parse(path, 51.5, -0.1);
                Assert.That(region, Is.EqualTo(RegionType.Temperate));
            }
            finally { DeleteFile(path); }
        }
        // ── ParseAsync (with elevation) ────────────────────────────────────────

        /// <summary>
        /// Stub elevation source that returns a fixed elevation value for every location.
        /// </summary>
        private sealed class FixedElevationSource : IElevationSource
        {
            private readonly double _elevation;
            public FixedElevationSource(double elevation) => _elevation = elevation;

            public Task<IReadOnlyList<double>> FetchElevationsAsync(
                IReadOnlyList<(double lat, double lon)> locations,
                CancellationToken cancellationToken = default)
            {
                var result = new double[locations.Count];
                for (int i = 0; i < result.Length; i++)
                    result[i] = _elevation;
                return Task.FromResult<IReadOnlyList<double>>(result);
            }
        }

        /// <summary>
        /// Stub elevation source that returns a per-index elevation value.
        /// </summary>
        private sealed class IndexedElevationSource : IElevationSource
        {
            private readonly double[] _elevations;
            public IndexedElevationSource(params double[] elevations) => _elevations = elevations;

            public Task<IReadOnlyList<double>> FetchElevationsAsync(
                IReadOnlyList<(double lat, double lon)> locations,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<double>>(_elevations);
        }

        [Test]
        public async Task ParseAsync_RoadNodes_HaveElevationAppliedToY()
        {
            const double elev = 42.0;
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
                var source = new FixedElevationSource(elev);
                var (roads, _, _) = await OSMParser.ParseAsync(path, 51.5000, -0.1000, source);

                Assert.That(roads.Count, Is.EqualTo(1));
                foreach (Vector3 node in roads[0].Nodes)
                    Assert.That(node.y, Is.EqualTo((float)elev).Within(0.001f),
                        "Road node Y should equal the sampled elevation.");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public async Task ParseAsync_BuildingFootprint_HaveElevationAppliedToY()
        {
            const double elev = 15.5;
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
                var source = new FixedElevationSource(elev);
                var (_, buildings, _) = await OSMParser.ParseAsync(path, 51.50, -0.10, source);

                Assert.That(buildings.Count, Is.EqualTo(1));
                foreach (Vector3 corner in buildings[0].Footprint)
                    Assert.That(corner.y, Is.EqualTo((float)elev).Within(0.001f),
                        "Building corner Y should equal the sampled elevation.");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public async Task ParseAsync_OriginNode_HasZeroXZ_AndCorrectElevation()
        {
            const double elev = 100.0;
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
                var source = new FixedElevationSource(elev);
                var (roads, _, _) = await OSMParser.ParseAsync(path, 51.5000, -0.1278, source);

                Vector3 origin = roads[0].Nodes[0];
                Assert.That(origin.x, Is.EqualTo(0f).Within(0.01f), "Origin node X should be 0");
                Assert.That(origin.y, Is.EqualTo((float)elev).Within(0.001f), "Origin node Y should equal elevation");
                Assert.That(origin.z, Is.EqualTo(0f).Within(0.01f), "Origin node Z should be 0");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public async Task ParseAsync_NullElevationSource_ThrowsArgumentNullException()
        {
            string osm = @"<?xml version='1.0'?><osm version='0.6'/>";
            string path = WriteTempOsm(osm);
            try
            {
                Assert.ThrowsAsync<ArgumentNullException>(
                    () => OSMParser.ParseAsync(path, 0, 0, null!));
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public async Task ParseAsync_ElevationFetchedOncePerUniqueNode()
        {
            // Two nodes, both distinct — verify batch size matches node count
            int fetchCallCount = 0;
            int lastBatchSize  = 0;

            var source = new TrackingElevationSource(locations =>
            {
                fetchCallCount++;
                lastBatchSize = locations.Count;
                var result = new double[locations.Count];
                return Task.FromResult<IReadOnlyList<double>>(result);
            });

            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <node id='2' lat='51.6' lon='-0.1'/>
  <way id='1'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='primary'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                await OSMParser.ParseAsync(path, 51.5, -0.1, source);

                Assert.That(fetchCallCount, Is.EqualTo(1), "FetchElevationsAsync should be called exactly once.");
                Assert.That(lastBatchSize, Is.EqualTo(2), "Both OSM nodes should be included in the batch.");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public async Task ParseAsync_ZeroElevation_NodesHaveYEqualZero()
        {
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5' lon='-0.1'/>
  <node id='2' lat='51.6' lon='-0.1'/>
  <way id='1'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='secondary'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                var source = new FixedElevationSource(0.0);
                var (roads, _, _) = await OSMParser.ParseAsync(path, 51.5, -0.1, source);

                foreach (Vector3 node in roads[0].Nodes)
                    Assert.That(node.y, Is.EqualTo(0f), "Zero elevation should give Y = 0.");
            }
            finally { DeleteFile(path); }
        }

        [Test]
        public async Task ParseAsync_DifferentElevationsPerNode_EachNodeHasCorrectY()
        {
            // Node 1 (origin) at elev 10m, node 2 at elev 50m
            string osm = @"<?xml version='1.0'?>
<osm version='0.6'>
  <node id='1' lat='51.5000' lon='-0.1278'/>
  <node id='2' lat='51.5010' lon='-0.1278'/>
  <way id='10'>
    <nd ref='1'/><nd ref='2'/>
    <tag k='highway' v='primary'/>
  </way>
</osm>";
            string path = WriteTempOsm(osm);
            try
            {
                // Nodes are enumerated in document order: id=1 first, id=2 second
                var source = new IndexedElevationSource(10.0, 50.0);
                var (roads, _, _) = await OSMParser.ParseAsync(path, 51.5000, -0.1278, source);

                Assert.That(roads[0].Nodes[0].y, Is.EqualTo(10f).Within(0.001f),
                    "First node Y should be 10m.");
                Assert.That(roads[0].Nodes[1].y, Is.EqualTo(50f).Within(0.001f),
                    "Second node Y should be 50m.");
            }
            finally { DeleteFile(path); }
        }

        private sealed class TrackingElevationSource : IElevationSource
        {
            private readonly Func<IReadOnlyList<(double lat, double lon)>, Task<IReadOnlyList<double>>> _handler;

            public TrackingElevationSource(
                Func<IReadOnlyList<(double lat, double lon)>, Task<IReadOnlyList<double>>> handler)
                => _handler = handler;

            public Task<IReadOnlyList<double>> FetchElevationsAsync(
                IReadOnlyList<(double lat, double lon)> locations,
                CancellationToken cancellationToken = default)
                => _handler(locations);
        }
    }
}
