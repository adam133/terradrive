using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.DataInversion;
using VectorRoad.Hud;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class MinimapRendererTests
    {
        private static readonly Vector3 Origin = new Vector3(0f, 0f, 0f);

        // Helper: build a simple RoadSegment with two nodes.
        private static RoadSegment MakeSegment(
            Vector3 a, Vector3 b, string highwayType = "residential") =>
            new RoadSegment
            {
                WayId       = 1,
                HighwayType = highwayType,
                Nodes       = new List<Vector3> { a, b },
            };

        // ── Null / empty inputs ───────────────────────────────────────────────

        [Test]
        public void BuildLines_NullRoads_ReturnsEmptyList()
        {
            var renderer = new MinimapRenderer();

            List<MinimapLine> lines = renderer.BuildLines(null!, Origin);

            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void BuildLines_EmptyRoads_ReturnsEmptyList()
        {
            var renderer = new MinimapRenderer();

            List<MinimapLine> lines = renderer.BuildLines(new List<RoadSegment>(), Origin);

            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void BuildLines_NullSegment_IsSkipped()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var roads = new List<RoadSegment> { null! };

            List<MinimapLine> lines = renderer.BuildLines(roads, Origin);

            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void BuildLines_SegmentWithOneNode_IsSkipped()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment = new RoadSegment
            {
                WayId       = 1,
                HighwayType = "primary",
                Nodes       = new List<Vector3> { new Vector3(10f, 0f, 10f) },
            };

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines, Is.Empty);
        }

        // ── Radius filtering ──────────────────────────────────────────────────

        [Test]
        public void BuildLines_BothNodesInsideRadius_LineIsIncluded()
        {
            var renderer = new MinimapRenderer { Radius = 150f };
            var segment  = MakeSegment(new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines, Has.Count.EqualTo(1));
        }

        [Test]
        public void BuildLines_BothNodesOutsideRadius_LineIsExcluded()
        {
            var renderer = new MinimapRenderer { Radius = 150f };
            var segment  = MakeSegment(new Vector3(200f, 0f, 0f), new Vector3(300f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void BuildLines_OneNodeInsideRadius_LineIsIncluded()
        {
            var renderer = new MinimapRenderer { Radius = 150f };
            // One node inside (100 m), one outside (200 m).
            var segment  = MakeSegment(new Vector3(100f, 0f, 0f), new Vector3(200f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines, Has.Count.EqualTo(1));
        }

        // ── Coordinate mapping ────────────────────────────────────────────────

        [Test]
        public void BuildLines_PlayerAtOrigin_NodeDueEast_StartIsRightOfCenter()
        {
            // A node at (50, 0, 0) should appear to the right of centre (x > 0.5).
            var renderer = new MinimapRenderer { Radius = 150f };
            var segment  = MakeSegment(new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].Start.x, Is.GreaterThan(0.5f));
        }

        [Test]
        public void BuildLines_PlayerAtOrigin_NodeDueNorth_StartIsAboveCenter()
        {
            // A node at (0, 0, 50) should appear above centre (y > 0.5).
            var renderer = new MinimapRenderer { Radius = 150f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 100f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].Start.y, Is.GreaterThan(0.5f));
        }

        [Test]
        public void BuildLines_NodeAtExactlyRadius_StartX_IsOne()
        {
            // A node exactly at Radius to the east maps to minimap x = 1.0.
            const float radius = 100f;
            var renderer = new MinimapRenderer { Radius = radius };
            var segment  = MakeSegment(new Vector3(radius, 0f, 0f), new Vector3(0f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].Start.x, Is.EqualTo(1.0f).Within(1e-4f));
        }

        [Test]
        public void BuildLines_NodeAtPlayerPosition_IsCentered()
        {
            // A node exactly at the player position maps to (0.5, 0.5).
            var renderer = new MinimapRenderer { Radius = 100f };
            var player   = new Vector3(10f, 0f, 20f);
            var segment  = MakeSegment(player, new Vector3(10f, 0f, 50f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, player);

            Assert.That(lines[0].Start.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(lines[0].Start.y, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void BuildLines_KnownCoordinates_MatchExpected()
        {
            // Player at origin, road from (50,0,0) → (100,0,0), radius=150.
            // diameter = 300.
            // Expected: Start=(0.5+50/300, 0.5) ≈ (0.667, 0.5)
            //           End  =(0.5+100/300, 0.5) ≈ (0.833, 0.5)
            var renderer = new MinimapRenderer { Radius = 150f };
            var segment  = MakeSegment(new Vector3(50f, 0f, 0f), new Vector3(100f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].Start.x, Is.EqualTo(0.5f + 50f / 300f).Within(1e-4f));
            Assert.That(lines[0].Start.y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(lines[0].End.x,   Is.EqualTo(0.5f + 100f / 300f).Within(1e-4f));
            Assert.That(lines[0].End.y,   Is.EqualTo(0.5f).Within(1e-4f));
        }

        // ── Yaw rotation ──────────────────────────────────────────────────────

        [Test]
        public void BuildLines_NoYaw_NorthNodeIsAboveCenter()
        {
            // Without yaw rotation a node due north (+Z) should appear above centre.
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 150f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin, 0f);

            Assert.That(lines[0].Start.y, Is.GreaterThan(0.5f));
        }

        [Test]
        public void BuildLines_Yaw90_EastFacing_NorthNodeAppearsLeftOfCenter()
        {
            // When the player faces east (yaw=90°) a node due north should appear to
            // the left of centre on the minimap (x < 0.5).
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 150f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin, 90f);

            Assert.That(lines[0].Start.x, Is.LessThan(0.5f));
        }

        [Test]
        public void BuildLines_Yaw90_EastFacing_EastNodeAppearsAboveCenter()
        {
            // When the player faces east (yaw=90°) a node due east should appear above
            // centre (minimap y > 0.5).
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(100f, 0f, 0f), new Vector3(150f, 0f, 0f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin, 90f);

            Assert.That(lines[0].Start.y, Is.GreaterThan(0.5f));
        }

        [Test]
        public void BuildLines_Yaw180_SouthFacing_NorthNodeIsBelowCenter()
        {
            // When the player faces south (yaw=180°) a node due north (+Z) should
            // appear below centre (y < 0.5).
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, 150f));

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin, 180f);

            Assert.That(lines[0].Start.y, Is.LessThan(0.5f));
        }

        // ── Road type mapping ─────────────────────────────────────────────────

        [Test]
        public void BuildLines_MotorwaySegment_LineHasMotorwayRoadType()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f), "motorway");

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].RoadType, Is.EqualTo(RoadType.Motorway));
        }

        [Test]
        public void BuildLines_ResidentialSegment_LineHasResidentialRoadType()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f), "residential");

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].RoadType, Is.EqualTo(RoadType.Residential));
        }

        [Test]
        public void BuildLines_UnknownHighwayType_LineHasUnknownRoadType()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment  = MakeSegment(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 100f), "unclassified");

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines[0].RoadType, Is.EqualTo(RoadType.Unknown));
        }

        // ── ParseRoadType ─────────────────────────────────────────────────────

        [Test]
        public void ParseRoadType_Motorway_ReturnsMotorway()
        {
            Assert.That(MinimapRenderer.ParseRoadType("motorway"), Is.EqualTo(RoadType.Motorway));
        }

        [Test]
        public void ParseRoadType_MotorwayLink_ReturnsMotorway()
        {
            Assert.That(MinimapRenderer.ParseRoadType("motorway_link"), Is.EqualTo(RoadType.Motorway));
        }

        [Test]
        public void ParseRoadType_Trunk_ReturnsTrunk()
        {
            Assert.That(MinimapRenderer.ParseRoadType("trunk"), Is.EqualTo(RoadType.Trunk));
        }

        [Test]
        public void ParseRoadType_Primary_ReturnsPrimary()
        {
            Assert.That(MinimapRenderer.ParseRoadType("primary"), Is.EqualTo(RoadType.Primary));
        }

        [Test]
        public void ParseRoadType_Secondary_ReturnsSecondary()
        {
            Assert.That(MinimapRenderer.ParseRoadType("secondary"), Is.EqualTo(RoadType.Secondary));
        }

        [Test]
        public void ParseRoadType_Tertiary_ReturnsTertiary()
        {
            Assert.That(MinimapRenderer.ParseRoadType("tertiary"), Is.EqualTo(RoadType.Tertiary));
        }

        [Test]
        public void ParseRoadType_Residential_ReturnsResidential()
        {
            Assert.That(MinimapRenderer.ParseRoadType("residential"), Is.EqualTo(RoadType.Residential));
        }

        [Test]
        public void ParseRoadType_LivingStreet_ReturnsResidential()
        {
            Assert.That(MinimapRenderer.ParseRoadType("living_street"), Is.EqualTo(RoadType.Residential));
        }

        [Test]
        public void ParseRoadType_Service_ReturnsService()
        {
            Assert.That(MinimapRenderer.ParseRoadType("service"), Is.EqualTo(RoadType.Service));
        }

        [Test]
        public void ParseRoadType_Track_ReturnsDirt()
        {
            Assert.That(MinimapRenderer.ParseRoadType("track"), Is.EqualTo(RoadType.Dirt));
        }

        [Test]
        public void ParseRoadType_Footway_ReturnsPath()
        {
            Assert.That(MinimapRenderer.ParseRoadType("footway"), Is.EqualTo(RoadType.Path));
        }

        [Test]
        public void ParseRoadType_Path_ReturnsPath()
        {
            Assert.That(MinimapRenderer.ParseRoadType("path"), Is.EqualTo(RoadType.Path));
        }

        [Test]
        public void ParseRoadType_Cycleway_ReturnsCycleway()
        {
            Assert.That(MinimapRenderer.ParseRoadType("cycleway"), Is.EqualTo(RoadType.Cycleway));
        }

        [Test]
        public void ParseRoadType_Null_ReturnsUnknown()
        {
            Assert.That(MinimapRenderer.ParseRoadType(null), Is.EqualTo(RoadType.Unknown));
        }

        [Test]
        public void ParseRoadType_UnrecognisedTag_ReturnsUnknown()
        {
            Assert.That(MinimapRenderer.ParseRoadType("unclassified"), Is.EqualTo(RoadType.Unknown));
        }

        // ── Multi-segment road ────────────────────────────────────────────────

        [Test]
        public void BuildLines_ThreeNodeSegment_ProducesTwoLines()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var segment = new RoadSegment
            {
                WayId       = 1,
                HighwayType = "primary",
                Nodes       = new List<Vector3>
                {
                    new Vector3(0f,   0f,  0f),
                    new Vector3(0f,   0f, 50f),
                    new Vector3(50f,  0f, 50f),
                },
            };

            List<MinimapLine> lines = renderer.BuildLines(new[] { segment }, Origin);

            Assert.That(lines, Has.Count.EqualTo(2));
        }

        [Test]
        public void BuildLines_MultipleSegments_AllLinesReturned()
        {
            var renderer = new MinimapRenderer { Radius = 200f };
            var seg1 = MakeSegment(new Vector3(0f,  0f, 0f), new Vector3(0f,  0f, 50f), "primary");
            var seg2 = MakeSegment(new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 50f), "residential");

            List<MinimapLine> lines = renderer.BuildLines(new[] { seg1, seg2 }, Origin);

            Assert.That(lines, Has.Count.EqualTo(2));
        }

        // ── Radius property ───────────────────────────────────────────────────

        [Test]
        public void Radius_DefaultValue_Is150()
        {
            var renderer = new MinimapRenderer();

            Assert.That(renderer.Radius, Is.EqualTo(150f));
        }

        [Test]
        public void Radius_CanBeChanged()
        {
            var renderer = new MinimapRenderer { Radius = 300f };

            Assert.That(renderer.Radius, Is.EqualTo(300f));
        }

        [Test]
        public void BuildLines_LargerRadius_IncludesMoreDistantRoads()
        {
            var smallRenderer = new MinimapRenderer { Radius = 50f };
            var largeRenderer = new MinimapRenderer { Radius = 300f };

            // Segment at 100 m away — inside large radius but outside small radius.
            var segment = MakeSegment(new Vector3(100f, 0f, 0f), new Vector3(150f, 0f, 0f));
            var roads   = new[] { segment };

            List<MinimapLine> smallLines = smallRenderer.BuildLines(roads, Origin);
            List<MinimapLine> largeLines = largeRenderer.BuildLines(roads, Origin);

            Assert.That(smallLines, Is.Empty,         "Segment should be excluded with small radius.");
            Assert.That(largeLines, Has.Count.EqualTo(1), "Segment should be included with large radius.");
        }
    }
}
