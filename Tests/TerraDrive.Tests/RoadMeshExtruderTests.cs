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
    }
}
