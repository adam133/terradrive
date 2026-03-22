using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.DataInversion;
using VectorRoad.Procedural;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class RoadsidePropPlacerTests
    {
        // A 20 m straight road running along +Z.
        private static readonly IList<Vector3> TwoPoints = new[]
        {
            new Vector3(0f, 0f,  0f),
            new Vector3(0f, 0f, 20f),
        };

        // A 100 m straight road running along +Z.
        private static readonly IList<Vector3> LongRoad = new[]
        {
            new Vector3(0f, 0f,   0f),
            new Vector3(0f, 0f, 100f),
        };

        // ── Edge cases ────────────────────────────────────────────────────────

        [Test]
        public void Place_NullPoints_ReturnsEmpty()
        {
            var result = RoadsidePropPlacer.Place(null!, RoadType.Residential);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Place_SinglePoint_ReturnsEmpty()
        {
            var single = new[] { new Vector3(0f, 0f, 0f) };

            var result = RoadsidePropPlacer.Place(single, RoadType.Residential);

            Assert.That(result, Is.Empty);
        }

        // ── Basic placement ───────────────────────────────────────────────────

        [Test]
        public void Place_TwoPoints_ProducesEvenNumberOfPlacements()
        {
            // Each spacing interval emits two props: one per side.
            var result = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);

            Assert.That(result.Count % 2, Is.EqualTo(0),
                "Props should come in left/right pairs.");
        }

        [Test]
        public void Place_TwoPoints_EveryPairHasOneLeftAndOneRight()
        {
            var result = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);

            Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));

            // Consecutive items share the same DistanceAlong, differing only in IsLeftSide.
            for (int i = 0; i < result.Count - 1; i += 2)
            {
                Assert.That(result[i].DistanceAlong,
                    Is.EqualTo(result[i + 1].DistanceAlong).Within(1e-4f),
                    "Paired props must share the same arc-length position.");
                Assert.That(result[i].IsLeftSide,     Is.True,  "First in pair should be left.");
                Assert.That(result[i + 1].IsLeftSide, Is.False, "Second in pair should be right.");
            }
        }

        [Test]
        public void Place_LongRoad_PlacesMultipleSpacingIntervals()
        {
            // 100 m road with 15 m spacing → ~6 intervals → at least 4 placements.
            var result = RoadsidePropPlacer.Place(LongRoad, RoadType.Residential);

            Assert.That(result.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Place_PropsAreOffsetLaterally_NotOnCenterline()
        {
            // Road runs along Z → every prop must have a non-zero X offset.
            var result = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);

            Assert.That(result.Count, Is.GreaterThan(0));
            foreach (var p in result)
                Assert.That(Math.Abs(p.Position.x), Is.GreaterThan(0f),
                    "Props should be offset from the road centreline.");
        }

        [Test]
        public void Place_LeftSidePropHasNegativeX_ForZAlignedRoad()
        {
            // Road runs along +Z; right = (+1, 0, 0) → left side is −X.
            var result    = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);
            var leftProps = result.Where(p => p.IsLeftSide).ToList();

            Assert.That(leftProps.Count, Is.GreaterThan(0));
            foreach (var p in leftProps)
                Assert.That(p.Position.x, Is.LessThan(0f),
                    "Left-side props on a Z-aligned road should have negative X.");
        }

        [Test]
        public void Place_RightSidePropHasPositiveX_ForZAlignedRoad()
        {
            var result     = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);
            var rightProps = result.Where(p => !p.IsLeftSide).ToList();

            Assert.That(rightProps.Count, Is.GreaterThan(0));
            foreach (var p in rightProps)
                Assert.That(p.Position.x, Is.GreaterThan(0f),
                    "Right-side props on a Z-aligned road should have positive X.");
        }

        [Test]
        public void Place_ForwardDirectionIsAlongSpline()
        {
            // Road runs along +Z → forward.z should be ≈ 1, forward.x ≈ 0.
            var result = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);

            Assert.That(result.Count, Is.GreaterThan(0));
            foreach (var p in result)
            {
                Assert.That(p.Forward.z, Is.GreaterThan(0f),
                    "Forward should point along the road (+Z).");
                Assert.That(p.Forward.x, Is.EqualTo(0f).Within(1e-4f));
            }
        }

        [Test]
        public void Place_DistanceAlongIsStrictlyIncreasing()
        {
            var result = RoadsidePropPlacer.Place(LongRoad, RoadType.Residential);

            // Left-side props (even indices) should have strictly increasing DistanceAlong.
            float prev = -1f;
            for (int i = 0; i < result.Count; i += 2)
            {
                Assert.That(result[i].DistanceAlong, Is.GreaterThan(prev));
                prev = result[i].DistanceAlong;
            }
        }

        [Test]
        public void Place_PropsAreLaterallyBeyondRoadEdge()
        {
            float halfWidth = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential) * 0.5f;
            var   result    = RoadsidePropPlacer.Place(TwoPoints, RoadType.Residential);

            Assert.That(result.Count, Is.GreaterThan(0));
            foreach (var p in result)
            {
                if (p.IsLeftSide)
                    Assert.That(p.Position.x, Is.LessThan(-halfWidth));
                else
                    Assert.That(p.Position.x, Is.GreaterThan(halfWidth));
            }
        }

        [Test]
        public void Place_IsDeterministic_SameInputsSameOutput()
        {
            var a = RoadsidePropPlacer.Place(LongRoad, RoadType.Primary, RegionType.Temperate, 12345L);
            var b = RoadsidePropPlacer.Place(LongRoad, RoadType.Primary, RegionType.Temperate, 12345L);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
                Assert.That(a[i].Type, Is.EqualTo(b[i].Type));
        }

        // ── Spacing ───────────────────────────────────────────────────────────

        [Test]
        public void GetSpacingForRoadType_AllRoadTypesReturnPositiveSpacing()
        {
            foreach (RoadType rt in Enum.GetValues(typeof(RoadType)))
                Assert.That(RoadsidePropPlacer.GetSpacingForRoadType(rt), Is.GreaterThan(0f),
                    $"Spacing for {rt} must be positive.");
        }

        [Test]
        public void GetSpacingForRoadType_MotorwaySpacingWiderThanResidential()
        {
            float motorway    = RoadsidePropPlacer.GetSpacingForRoadType(RoadType.Motorway);
            float residential = RoadsidePropPlacer.GetSpacingForRoadType(RoadType.Residential);

            Assert.That(motorway, Is.GreaterThan(residential),
                "Motorway prop spacing should be wider than residential.");
        }

        // ── Prop-type selection ───────────────────────────────────────────────

        [Test]
        public void GetPropTypesForRoad_Motorway_ContainsLampPost_NotTree()
        {
            var types = RoadsidePropPlacer.GetPropTypesForRoad(RoadType.Motorway);

            Assert.That(types, Does.Contain(PropType.LampPost));
            Assert.That(types, Does.Not.Contain(PropType.Tree));
        }

        [Test]
        public void GetPropTypesForRoad_Residential_Temperate_ContainsTree()
        {
            var types = RoadsidePropPlacer.GetPropTypesForRoad(RoadType.Residential, RegionType.Temperate);

            Assert.That(types, Does.Contain(PropType.Tree));
        }

        [Test]
        public void GetPropTypesForRoad_Residential_Desert_DoesNotContainTree()
        {
            var types = RoadsidePropPlacer.GetPropTypesForRoad(RoadType.Residential, RegionType.Desert);

            Assert.That(types, Does.Not.Contain(PropType.Tree));
        }

        [Test]
        public void GetPropTypesForRoad_Residential_Arctic_DoesNotContainTree()
        {
            var types = RoadsidePropPlacer.GetPropTypesForRoad(RoadType.Residential, RegionType.Arctic);

            Assert.That(types, Does.Not.Contain(PropType.Tree));
        }

        [Test]
        public void GetPropTypesForRoad_Service_ContainsFence()
        {
            var types = RoadsidePropPlacer.GetPropTypesForRoad(RoadType.Service);

            Assert.That(types, Does.Contain(PropType.Fence));
        }

        [Test]
        public void GetPropTypesForRoad_AllRoadTypesReturnNonEmptyArray()
        {
            foreach (RoadType rt in Enum.GetValues(typeof(RoadType)))
                Assert.That(RoadsidePropPlacer.GetPropTypesForRoad(rt).Length, Is.GreaterThan(0),
                    $"Prop type list for {rt} must not be empty.");
        }

        [Test]
        public void GetPropTypesForRoad_Path_Temperate_ContainsTree()
        {
            var types = RoadsidePropPlacer.GetPropTypesForRoad(RoadType.Path, RegionType.Temperate);

            Assert.That(types, Does.Contain(PropType.Tree));
        }

        // ── Region variation ─────────────────────────────────────────────────

        [Test]
        public void Place_Desert_ResidentialRoad_ProducesNoTreeProps()
        {
            var result = RoadsidePropPlacer.Place(LongRoad, RoadType.Residential, RegionType.Desert);

            Assert.That(result.Any(p => p.Type == PropType.Tree), Is.False,
                "Desert region should produce no tree props.");
        }

        [Test]
        public void Place_Temperate_ResidentialRoad_ProducesTreeProps()
        {
            var result = RoadsidePropPlacer.Place(LongRoad, RoadType.Residential, RegionType.Temperate);

            Assert.That(result.Any(p => p.Type == PropType.Tree), Is.True,
                "Temperate region should include tree props on residential roads.");
        }
    }
}
