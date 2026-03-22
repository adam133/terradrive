using System.Collections.Generic;
using NUnit.Framework;
using VectorRoad.DataInversion;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class MapWayTests
    {
        // ── Default state ──────────────────────────────────────────────────────

        [Test]
        public void DefaultConstructor_NodesIsEmptyList()
        {
            var way = new MapWay();

            Assert.That(way.Nodes, Is.Not.Null);
            Assert.That(way.Nodes.Count, Is.EqualTo(0));
        }

        [Test]
        public void DefaultConstructor_TagsIsEmptyDictionary()
        {
            var way = new MapWay();

            Assert.That(way.Tags, Is.Not.Null);
            Assert.That(way.Tags.Count, Is.EqualTo(0));
        }

        [Test]
        public void DefaultConstructor_RoadTypeIsUnknown()
        {
            var way = new MapWay();

            Assert.That(way.RoadType, Is.EqualTo(RoadType.Unknown));
        }

        // ── Property assignment ────────────────────────────────────────────────

        [Test]
        public void Id_CanBeSet()
        {
            var way = new MapWay { Id = 12345L };

            Assert.That(way.Id, Is.EqualTo(12345L));
        }

        [Test]
        public void RoadType_CanBeAssigned()
        {
            var way = new MapWay { RoadType = RoadType.Residential };

            Assert.That(way.RoadType, Is.EqualTo(RoadType.Residential));
        }

        // ── Nodes ──────────────────────────────────────────────────────────────

        [Test]
        public void Nodes_CanBePopulated()
        {
            var way = new MapWay();
            way.Nodes.Add(new MapNode(1L, 51.5000, -0.1000, 0.0));
            way.Nodes.Add(new MapNode(2L, 51.5010, -0.1010, 5.0));

            Assert.That(way.Nodes.Count, Is.EqualTo(2));
        }

        [Test]
        public void Nodes_PreservesOrder()
        {
            var nodeA = new MapNode(10L, 51.5000, -0.1000);
            var nodeB = new MapNode(20L, 51.5010, -0.1010);
            var nodeC = new MapNode(30L, 51.5020, -0.1020);

            var way = new MapWay();
            way.Nodes.Add(nodeA);
            way.Nodes.Add(nodeB);
            way.Nodes.Add(nodeC);

            Assert.That(way.Nodes[0].Id, Is.EqualTo(10L));
            Assert.That(way.Nodes[1].Id, Is.EqualTo(20L));
            Assert.That(way.Nodes[2].Id, Is.EqualTo(30L));
        }

        [Test]
        public void Nodes_CanBeReplacedWithNewList()
        {
            var way = new MapWay();
            var newNodes = new List<MapNode>
            {
                new MapNode(5L, 48.8566, 2.3522),
            };
            way.Nodes = newNodes;

            Assert.That(way.Nodes, Is.SameAs(newNodes));
        }

        // ── Tags ───────────────────────────────────────────────────────────────

        [Test]
        public void Tags_CanBePopulated()
        {
            var way = new MapWay();
            way.Tags["highway"] = "residential";
            way.Tags["name"] = "Baker Street";

            Assert.That(way.Tags.Count, Is.EqualTo(2));
            Assert.That(way.Tags["highway"], Is.EqualTo("residential"));
        }

        [Test]
        public void Tags_CanBeReplacedWithNewDictionary()
        {
            var way = new MapWay();
            var newTags = new Dictionary<string, string> { ["surface"] = "gravel" };
            way.Tags = newTags;

            Assert.That(way.Tags, Is.SameAs(newTags));
            Assert.That(way.Tags["surface"], Is.EqualTo("gravel"));
        }

        // ── Integration: fully populated MapWay ───────────────────────────────

        [Test]
        public void FullyPopulatedMapWay_AllPropertiesReturnExpectedValues()
        {
            var way = new MapWay
            {
                Id = 999L,
                RoadType = RoadType.Dirt,
                Nodes = new List<MapNode>
                {
                    new MapNode(1L, 51.0, -0.5, 100.0),
                    new MapNode(2L, 51.1, -0.6, 110.0),
                },
                Tags = new Dictionary<string, string>
                {
                    ["highway"] = "track",
                    ["surface"] = "dirt",
                },
            };

            Assert.That(way.Id, Is.EqualTo(999L));
            Assert.That(way.RoadType, Is.EqualTo(RoadType.Dirt));
            Assert.That(way.Nodes.Count, Is.EqualTo(2));
            Assert.That(way.Nodes[0].Elevation, Is.EqualTo(100.0));
            Assert.That(way.Tags["surface"], Is.EqualTo("dirt"));
        }
    }
}
