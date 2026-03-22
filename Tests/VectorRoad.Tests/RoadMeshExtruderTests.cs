using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.DataInversion;
using VectorRoad.Procedural;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class RoadMeshExtruderTests
    {
        // Two simple spline points used throughout most tests.
        private static readonly IList<Vector3> TwoPoints = new[]
        {
            new Vector3(0f, 0f,  0f),
            new Vector3(0f, 0f, 10f),
        };

        // ── GetWidthForRoadType ────────────────────────────────────────────────

        [Test]
        public void GetWidthForRoadType_Motorway_IsWidestClass()
        {
            float motorway = RoadMeshExtruder.GetWidthForRoadType(RoadType.Motorway);
            float trunk    = RoadMeshExtruder.GetWidthForRoadType(RoadType.Trunk);

            Assert.That(motorway, Is.GreaterThan(trunk));
        }

        [Test]
        public void GetWidthForRoadType_Trunk_WiderThanPrimary()
        {
            float trunk   = RoadMeshExtruder.GetWidthForRoadType(RoadType.Trunk);
            float primary = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary);

            Assert.That(trunk, Is.GreaterThan(primary));
        }

        [Test]
        public void GetWidthForRoadType_Primary_WiderThanSecondary()
        {
            float primary   = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary);
            float secondary = RoadMeshExtruder.GetWidthForRoadType(RoadType.Secondary);

            Assert.That(primary, Is.GreaterThan(secondary));
        }

        [Test]
        public void GetWidthForRoadType_Secondary_WiderThanTertiary()
        {
            float secondary = RoadMeshExtruder.GetWidthForRoadType(RoadType.Secondary);
            float tertiary  = RoadMeshExtruder.GetWidthForRoadType(RoadType.Tertiary);

            Assert.That(secondary, Is.GreaterThan(tertiary));
        }

        [Test]
        public void GetWidthForRoadType_Tertiary_WiderThanResidential()
        {
            float tertiary    = RoadMeshExtruder.GetWidthForRoadType(RoadType.Tertiary);
            float residential = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential);

            Assert.That(tertiary, Is.GreaterThan(residential));
        }

        [Test]
        public void GetWidthForRoadType_Residential_WiderThanPath()
        {
            float residential = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential);
            float path        = RoadMeshExtruder.GetWidthForRoadType(RoadType.Path);

            Assert.That(residential, Is.GreaterThan(path));
        }

        [Test]
        public void GetWidthForRoadType_MotorwayWiderThanResidential()
        {
            float motorway    = RoadMeshExtruder.GetWidthForRoadType(RoadType.Motorway);
            float residential = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential);

            Assert.That(motorway, Is.GreaterThan(residential),
                "Motorways must be wider than residential streets.");
        }

        [Test]
        public void GetWidthForRoadType_AllRoadTypesReturnPositiveWidth()
        {
            foreach (RoadType rt in System.Enum.GetValues(typeof(RoadType)))
                Assert.That(RoadMeshExtruder.GetWidthForRoadType(rt), Is.GreaterThan(0f),
                    $"Width for {rt} must be positive.");
        }

        [Test]
        public void GetWidthForRoadType_Unknown_ReturnsFallbackWidth()
        {
            float width = RoadMeshExtruder.GetWidthForRoadType(RoadType.Unknown);

            Assert.That(width, Is.GreaterThan(0f));
        }

        // ── Extrude(splinePoints, RoadType) overload ──────────────────────────

        [Test]
        public void Extrude_RoadType_ReturnsNonNullMesh()
        {
            Mesh mesh = RoadMeshExtruder.Extrude(TwoPoints, RoadType.Residential);

            Assert.That(mesh, Is.Not.Null);
        }

        [Test]
        public void Extrude_RoadType_MeshHasCorrectName()
        {
            Mesh mesh = RoadMeshExtruder.Extrude(TwoPoints, RoadType.Primary);

            Assert.That(mesh.name, Is.EqualTo("RoadMesh"));
        }

        [Test]
        public void Extrude_RoadType_GeneratesCorrectVertexCount()
        {
            // 2 spline points × 2 edge vertices = 4 vertices
            Mesh mesh = RoadMeshExtruder.Extrude(TwoPoints, RoadType.Motorway);

            Assert.That(mesh.Vertices.Length, Is.EqualTo(4));
        }

        [Test]
        public void Extrude_RoadType_GeneratesCorrectTriangleCount()
        {
            // (2-1) quads × 6 indices = 6
            Mesh mesh = RoadMeshExtruder.Extrude(TwoPoints, RoadType.Motorway);

            Assert.That(mesh.Triangles.Length, Is.EqualTo(6));
        }

        [Test]
        public void Extrude_MotorwayProducesWiderMeshThanResidential()
        {
            Mesh motorway    = RoadMeshExtruder.Extrude(TwoPoints, RoadType.Motorway);
            Mesh residential = RoadMeshExtruder.Extrude(TwoPoints, RoadType.Residential);

            // The road runs along Z; edge vertices differ only in X.
            // left-edge X is negative, right-edge X is positive.
            float motorwayWidth    = motorway.Vertices[1].x    - motorway.Vertices[0].x;
            float residentialWidth = residential.Vertices[1].x - residential.Vertices[0].x;

            Assert.That(motorwayWidth, Is.GreaterThan(residentialWidth),
                "Motorway mesh must be wider than residential mesh.");
        }

        [Test]
        public void Extrude_RoadType_WidthMatchesGetWidthForRoadType()
        {
            const RoadType roadType = RoadType.Primary;
            float expectedWidth = RoadMeshExtruder.GetWidthForRoadType(roadType);

            Mesh mesh = RoadMeshExtruder.Extrude(TwoPoints, roadType);

            // right-edge X minus left-edge X should equal the full road width
            float actualWidth = mesh.Vertices[1].x - mesh.Vertices[0].x;
            Assert.That(actualWidth, Is.EqualTo(expectedWidth).Within(1e-4f));
        }

        // ── Original Extrude(splinePoints, float) overload still works ─────────

        [Test]
        public void Extrude_ExplicitWidth_StillProducesCorrectMesh()
        {
            const float width = 8f;
            Mesh mesh = RoadMeshExtruder.Extrude(TwoPoints, width);

            float actualWidth = mesh.Vertices[1].x - mesh.Vertices[0].x;
            Assert.That(actualWidth, Is.EqualTo(width).Within(1e-4f));
        }

        [Test]
        public void Extrude_NullPoints_ReturnsEmptyMesh()
        {
            Mesh mesh = RoadMeshExtruder.Extrude(null!, RoadType.Residential);

            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Vertices.Length, Is.EqualTo(0));
        }

        [Test]
        public void Extrude_NullPoints_ExplicitWidth_ReturnsEmptyMesh()
        {
            Mesh mesh = RoadMeshExtruder.Extrude(null!, 8f);

            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Vertices.Length, Is.EqualTo(0));
        }

        [Test]
        public void Extrude_SinglePoint_ReturnsEmptyMesh()
        {
            var single = new[] { new Vector3(0f, 0f, 0f) };
            Mesh mesh = RoadMeshExtruder.Extrude(single, RoadType.Primary);

            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.Vertices.Length, Is.EqualTo(0));
        }

        // ── ExtrudeWithDetails ────────────────────────────────────────────────

        [Test]
        public void ExtrudeWithDetails_ReturnsNonNullRoadMesh()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.RoadMesh, Is.Not.Null);
        }

        [Test]
        public void ExtrudeWithDetails_ReturnsNonNullKerbMesh()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.KerbMesh, Is.Not.Null);
        }

        [Test]
        public void ExtrudeWithDetails_RoadMesh_HasCorrectName()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Residential);

            Assert.That(result.RoadMesh.name, Is.EqualTo("RoadMesh"));
        }

        [Test]
        public void ExtrudeWithDetails_KerbMesh_HasCorrectName()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Residential);

            Assert.That(result.KerbMesh.name, Is.EqualTo("KerbMesh"));
        }

        [Test]
        public void ExtrudeWithDetails_RoadMesh_CorrectVertexCount()
        {
            // Same as Extrude: 2 spline points × 2 edge vertices = 4
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.RoadMesh.Vertices.Length, Is.EqualTo(4));
        }

        [Test]
        public void ExtrudeWithDetails_RoadMesh_WidthMatchesRoadType()
        {
            const RoadType roadType = RoadType.Secondary;
            float expected = RoadMeshExtruder.GetWidthForRoadType(roadType);

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, roadType);

            float actual = result.RoadMesh.Vertices[1].x - result.RoadMesh.Vertices[0].x;
            Assert.That(actual, Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void ExtrudeWithDetails_RoadMesh_HasUV0Channel()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.RoadMesh.GetUVs(0).Length, Is.EqualTo(result.RoadMesh.Vertices.Length));
        }

        [Test]
        public void ExtrudeWithDetails_RoadMesh_HasUV1Channel()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            // UV1 (lane-marking channel) must have the same vertex count as UV0
            Assert.That(result.RoadMesh.GetUVs(1).Length, Is.EqualTo(result.RoadMesh.Vertices.Length));
        }

        [Test]
        public void ExtrudeWithDetails_RoadMesh_UV1_UsesLaneMarkingTileLength()
        {
            // Road is 10 m long; default uvTileLength=10, laneMarkingTileLength=6.
            // At the last point: UV0.v = 10/10 = 1.0, UV1.v = 10/6 ≈ 1.667.
            const float uvTile    = 10f;
            const float lmTile    = 6f;
            const float roadLen   = 10f;   // distance between TwoPoints

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, 7f, uvTile, lmTile);

            float uv0V = result.RoadMesh.GetUVs(0)[2].y;   // left-edge vertex at index 2 (point 1)
            float uv1V = result.RoadMesh.GetUVs(1)[2].y;

            Assert.That(uv0V, Is.EqualTo(roadLen / uvTile).Within(1e-4f));
            Assert.That(uv1V, Is.EqualTo(roadLen / lmTile).Within(1e-4f));
        }

        [Test]
        public void ExtrudeWithDetails_KerbMesh_CorrectVertexCount()
        {
            // 4 kerb vertices per spline point: left-outer, left-inner, right-inner, right-outer
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.KerbMesh.Vertices.Length, Is.EqualTo(TwoPoints.Count * 4));
        }

        [Test]
        public void ExtrudeWithDetails_KerbMesh_CorrectTriangleCount()
        {
            // (n-1) segments × 2 kerb strips × 2 triangles × 3 indices = (n-1) × 12
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.KerbMesh.Triangles.Length, Is.EqualTo((TwoPoints.Count - 1) * 12));
        }

        [Test]
        public void ExtrudeWithDetails_KerbMesh_IsElevatedAboveRoadSurface()
        {
            // Spline is at Y = 0; kerb vertices should be at Y = TerrainClearance + kerbHeight.
            // TerrainClearance is added by ExtrudeWithDetails to prevent z-fighting with terrain.
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, 7f,
                kerbHeight: RoadMeshExtruder.DefaultKerbHeight);

            float expectedY = RoadMeshExtruder.TerrainClearance + RoadMeshExtruder.DefaultKerbHeight;
            foreach (var v in result.KerbMesh.Vertices)
                Assert.That(v.y, Is.EqualTo(expectedY).Within(1e-4f),
                    "Every kerb vertex should be elevated above the road plane.");
        }

        [Test]
        public void ExtrudeWithDetails_KerbMesh_OuterEdgeIsBeyondRoadEdge()
        {
            // Road runs along Z with width = 7 m → half-width = 3.5 m.
            // Left-outer kerb vertex should have X < −3.5 m.
            // Right-outer kerb vertex should have X > +3.5 m.
            const float roadWidth = 7f;
            float halfWidth = roadWidth * 0.5f;

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, roadWidth);

            // Vertex layout per spline point: [leftOuter, leftInner, rightInner, rightOuter]
            float leftOuter  = result.KerbMesh.Vertices[0].x;   // index 0 of first point
            float rightOuter = result.KerbMesh.Vertices[3].x;   // index 3 of first point

            Assert.That(leftOuter,  Is.LessThan(-halfWidth),
                "Left kerb outer edge must extend beyond the left road edge.");
            Assert.That(rightOuter, Is.GreaterThan(halfWidth),
                "Right kerb outer edge must extend beyond the right road edge.");
        }

        [Test]
        public void ExtrudeWithDetails_NullPoints_ReturnsBothEmptyMeshes()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(null!, RoadType.Primary);

            Assert.That(result.RoadMesh.Vertices.Length, Is.EqualTo(0));
            Assert.That(result.KerbMesh.Vertices.Length, Is.EqualTo(0));
        }

        [Test]
        public void ExtrudeWithDetails_ExplicitWidth_KerbOutsideRoadEdge()
        {
            const float roadWidth = 10f;
            float halfWidth = roadWidth * 0.5f;

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, roadWidth);

            float leftOuter  = result.KerbMesh.Vertices[0].x;
            float rightOuter = result.KerbMesh.Vertices[3].x;

            Assert.That(leftOuter,  Is.LessThan(-halfWidth));
            Assert.That(rightOuter, Is.GreaterThan(halfWidth));
        }

        // ── Region texture identifiers ────────────────────────────────────────

        [Test]
        public void ExtrudeWithDetails_DefaultRegion_ReturnsNonEmptyTextureIds()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary);

            Assert.That(result.RoadTextureId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.KerbTextureId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ExtrudeWithDetails_NullPoints_ReturnsEmptyTextureIds()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(null!, RoadType.Primary);

            Assert.That(result.RoadTextureId, Is.Empty);
            Assert.That(result.KerbTextureId, Is.Empty);
        }

        [Test]
        public void ExtrudeWithDetails_Temperate_HasTemperateRoadTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.Temperate);

            Assert.That(result.RoadTextureId, Is.EqualTo("road_asphalt_temperate"));
        }

        [Test]
        public void ExtrudeWithDetails_Desert_HasDesertRoadTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.Desert);

            Assert.That(result.RoadTextureId, Is.EqualTo("road_asphalt_desert"));
        }

        [Test]
        public void ExtrudeWithDetails_Temperate_HasStoneKerbTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Residential, region: RegionType.Temperate);

            Assert.That(result.KerbTextureId, Is.EqualTo("kerb_stone"));
        }

        [Test]
        public void ExtrudeWithDetails_Mediterranean_HasGraniteKerbTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Secondary, region: RegionType.Mediterranean);

            Assert.That(result.KerbTextureId, Is.EqualTo("kerb_granite"));
        }

        [Test]
        public void ExtrudeWithDetails_Dirt_DirtRoad_HasDirtSurfaceTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Dirt, region: RegionType.Temperate);

            Assert.That(result.RoadTextureId, Is.EqualTo("road_dirt"));
        }

        [Test]
        public void ExtrudeWithDetails_Dirt_DesertRegion_HasSandSurfaceTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Dirt, region: RegionType.Desert);

            Assert.That(result.RoadTextureId, Is.EqualTo("road_sand"));
        }

        [Test]
        public void ExtrudeWithDetails_AllRegions_ReturnNonEmptyRoadTextureId()
        {
            foreach (RegionType region in System.Enum.GetValues(typeof(RegionType)))
            {
                RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                    TwoPoints, RoadType.Residential, region: region);

                Assert.That(result.RoadTextureId, Is.Not.Null.And.Not.Empty,
                    $"RoadTextureId must not be empty for region '{region}'.");
            }
        }

        [Test]
        public void ExtrudeWithDetails_AllRegions_ReturnNonEmptyKerbTextureId()
        {
            foreach (RegionType region in System.Enum.GetValues(typeof(RegionType)))
            {
                RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                    TwoPoints, RoadType.Residential, region: region);

                Assert.That(result.KerbTextureId, Is.Not.Null.And.Not.Empty,
                    $"KerbTextureId must not be empty for region '{region}'.");
            }
        }

        [Test]
        public void ExtrudeWithDetails_DifferentRegions_ProduceDifferentRoadTextures()
        {
            RoadMeshResult temperate = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.Temperate);
            RoadMeshResult desert = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.Desert);

            Assert.That(temperate.RoadTextureId, Is.Not.EqualTo(desert.RoadTextureId),
                "Temperate and Desert regions should use different road textures.");
        }

        // ── Lane count and one-way ────────────────────────────────────────────

        [Test]
        public void GetWidthForRoadType_WithLanes_UsesLaneCountOverTable()
        {
            // 4 lanes × 3.5 m = 14 m — wider than the Primary table value (12 m)
            float width = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary, lanes: 4);

            Assert.That(width, Is.EqualTo(4 * RoadMeshExtruder.DefaultLaneWidth).Within(1e-4f));
        }

        [Test]
        public void GetWidthForRoadType_ZeroLanes_FallsBackToTable()
        {
            float withZero  = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential, lanes: 0);
            float tableWidth = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential);

            Assert.That(withZero, Is.EqualTo(tableWidth).Within(1e-4f));
        }

        [Test]
        public void GetWidthForRoadType_TwoLanes_NarrowerThanFourLanes()
        {
            float two  = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary, lanes: 2);
            float four = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary, lanes: 4);

            Assert.That(two, Is.LessThan(four));
        }

        [Test]
        public void ExtrudeWithDetails_WithLanes_MeshWidthReflectsLaneCountAndShoulder()
        {
            // Formula: (lanes × DefaultLaneWidth + shoulderWidth) × regionFactor
            // Primary shoulder = 3.0 m; Unknown region factor = 1.0
            const int lanes = 3;
            const RoadType roadType = RoadType.Primary;
            float expectedWidth = lanes * RoadMeshExtruder.DefaultLaneWidth
                                  + RegionWidthFactors.GetShoulderWidth(roadType);

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, roadType, lanes: lanes);

            float actual = result.RoadMesh.Vertices[1].x - result.RoadMesh.Vertices[0].x;
            Assert.That(actual, Is.EqualTo(expectedWidth).Within(1e-4f));
        }

        [Test]
        public void ExtrudeWithDetails_MoreLanesProducesWiderMesh()
        {
            RoadMeshResult two  = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary, lanes: 2);
            RoadMeshResult four = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary, lanes: 4);

            float twoWidth  = two.RoadMesh.Vertices[1].x  - two.RoadMesh.Vertices[0].x;
            float fourWidth = four.RoadMesh.Vertices[1].x - four.RoadMesh.Vertices[0].x;

            Assert.That(fourWidth, Is.GreaterThan(twoWidth));
        }

        [Test]
        public void ExtrudeWithDetails_TwoWay_HasTwoWayLaneMarkingTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, isOneWay: false);

            Assert.That(result.LaneMarkingTextureId, Is.EqualTo("lane_marking_twoway"));
        }

        [Test]
        public void ExtrudeWithDetails_OneWay_HasOneWayLaneMarkingTexture()
        {
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, isOneWay: true);

            Assert.That(result.LaneMarkingTextureId, Is.EqualTo("lane_marking_oneway"));
        }

        [Test]
        public void ExtrudeWithDetails_DefaultIsNotOneWay_HasTwoWayLaneMarkingTexture()
        {
            // Default call (no isOneWay argument) should produce two-way markings.
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Secondary);

            Assert.That(result.LaneMarkingTextureId, Is.EqualTo("lane_marking_twoway"));
        }

        [Test]
        public void ExtrudeWithDetails_OneWayVsTwoWay_ProduceDifferentLaneMarkingTextures()
        {
            RoadMeshResult oneway  = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary, isOneWay: true);
            RoadMeshResult twoway  = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, RoadType.Primary, isOneWay: false);

            Assert.That(oneway.LaneMarkingTextureId, Is.Not.EqualTo(twoway.LaneMarkingTextureId));
        }

        [Test]
        public void ExtrudeWithDetails_LaneMarkingTextureId_IsNonEmptyForAllRoadTypes()
        {
            foreach (RoadType rt in System.Enum.GetValues(typeof(RoadType)))
            {
                RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(TwoPoints, rt);

                Assert.That(result.LaneMarkingTextureId, Is.Not.Null.And.Not.Empty,
                    $"LaneMarkingTextureId must not be empty for road type '{rt}'.");
            }
        }

        // ── Region-based width factor ─────────────────────────────────────────

        [Test]
        public void GetWidthForRoadType_TemperateNorthAmerica_WiderThanTemperate()
        {
            // USA/Canada roads are wider than European roads of the same type.
            float na     = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary, 0, RegionType.TemperateNorthAmerica);
            float europe = RoadMeshExtruder.GetWidthForRoadType(RoadType.Primary, 0, RegionType.Temperate);

            Assert.That(na, Is.GreaterThan(europe),
                "North American roads should be wider than their European counterparts.");
        }

        [Test]
        public void GetWidthForRoadType_WithRegion_ZeroLanes_AppliesRegionFactorToTableWidth()
        {
            // Zero lanes falls back to the table value, then multiplies by the region factor.
            float baseWidth = RoadMeshExtruder.GetWidthForRoadType(RoadType.Secondary);
            float naFactor  = RegionWidthFactors.GetWidthFactor(RegionType.TemperateNorthAmerica);
            float expected  = baseWidth * naFactor;

            float actual = RoadMeshExtruder.GetWidthForRoadType(
                RoadType.Secondary, 0, RegionType.TemperateNorthAmerica);

            Assert.That(actual, Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void GetWidthForRoadType_WithRegion_Lanes_AppliesShoulderAndRegionFactor()
        {
            const int lanes          = 2;
            const RoadType roadType  = RoadType.Motorway;
            const RegionType region  = RegionType.TemperateNorthAmerica;

            float shoulder = RegionWidthFactors.GetShoulderWidth(roadType);
            float factor   = RegionWidthFactors.GetWidthFactor(region);
            float expected = (lanes * RoadMeshExtruder.DefaultLaneWidth + shoulder) * factor;

            float actual = RoadMeshExtruder.GetWidthForRoadType(roadType, lanes, region);

            Assert.That(actual, Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void GetWidthForRoadType_UnknownRegion_SameAsBaseline()
        {
            // RegionType.Unknown factor is 1.0 — identical to Temperate.
            float unknown   = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential, 0, RegionType.Unknown);
            float temperate = RoadMeshExtruder.GetWidthForRoadType(RoadType.Residential, 0, RegionType.Temperate);

            Assert.That(unknown, Is.EqualTo(temperate).Within(1e-4f));
        }

        [Test]
        public void ExtrudeWithDetails_NorthAmerica_ProducesWiderMeshThanTemperate()
        {
            RoadMeshResult na     = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.TemperateNorthAmerica);
            RoadMeshResult europe = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.Temperate);

            float naWidth     = na.RoadMesh.Vertices[1].x     - na.RoadMesh.Vertices[0].x;
            float europeWidth = europe.RoadMesh.Vertices[1].x - europe.RoadMesh.Vertices[0].x;

            Assert.That(naWidth, Is.GreaterThan(europeWidth),
                "North American road mesh must be wider than European mesh of the same type.");
        }

        [Test]
        public void GetWidthFactor_AllRegions_ReturnPositiveValue()
        {
            foreach (RegionType region in System.Enum.GetValues(typeof(RegionType)))
                Assert.That(RegionWidthFactors.GetWidthFactor(region), Is.GreaterThan(0f),
                    $"Width factor for region '{region}' must be positive.");
        }

        [Test]
        public void GetShoulderWidth_AllRoadTypes_ReturnNonNegativeValue()
        {
            foreach (RoadType rt in System.Enum.GetValues(typeof(RoadType)))
                Assert.That(RegionWidthFactors.GetShoulderWidth(rt), Is.GreaterThanOrEqualTo(0f),
                    $"Shoulder width for road type '{rt}' must be non-negative.");
        }

        [Test]
        public void GetShoulderWidth_Motorway_WidestShoulder()
        {
            float motorway    = RegionWidthFactors.GetShoulderWidth(RoadType.Motorway);
            float residential = RegionWidthFactors.GetShoulderWidth(RoadType.Residential);

            Assert.That(motorway, Is.GreaterThan(residential),
                "Motorway shoulders must be wider than residential shoulders.");
        }

        [Test]
        public void ExtrudeWithDetails_WithLanes_NorthAmerica_WiderThanEurope()
        {
            RoadMeshResult na     = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.TemperateNorthAmerica, lanes: 4);
            RoadMeshResult europe = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, RoadType.Primary, region: RegionType.Temperate, lanes: 4);

            float naWidth     = na.RoadMesh.Vertices[1].x     - na.RoadMesh.Vertices[0].x;
            float europeWidth = europe.RoadMesh.Vertices[1].x - europe.RoadMesh.Vertices[0].x;

            Assert.That(naWidth, Is.GreaterThan(europeWidth),
                "4-lane North American road must be wider than 4-lane European road.");
        }
    }
}
