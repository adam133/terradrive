using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TerraDrive.DataInversion;
using TerraDrive.Procedural;

namespace TerraDrive.Tests
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
            // Spline is at Y = 0; kerb vertices should be at Y = kerbHeight > 0.
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                TwoPoints, 7f,
                kerbHeight: RoadMeshExtruder.DefaultKerbHeight);

            foreach (var v in result.KerbMesh.Vertices)
                Assert.That(v.y, Is.EqualTo(RoadMeshExtruder.DefaultKerbHeight).Within(1e-4f),
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
    }
}
