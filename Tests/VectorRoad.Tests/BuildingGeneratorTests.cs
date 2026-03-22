using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.DataInversion;
using VectorRoad.Procedural;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class BuildingGeneratorTests
    {
        // A minimal valid square footprint (4 corners, no repeated closing point).
        private static readonly IList<Vector3> SquareFootprint = new[]
        {
            new Vector3( 0f, 0f,  0f),
            new Vector3(10f, 0f,  0f),
            new Vector3(10f, 0f, 10f),
            new Vector3( 0f, 0f, 10f),
        };

        // ── Edge cases ────────────────────────────────────────────────────────

        [Test]
        public void Extrude_NullFootprint_ReturnsEmptyMeshes()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(null!);

            Assert.That(result.WallMesh,  Is.Not.Null);
            Assert.That(result.RoofMesh,  Is.Not.Null);
            Assert.That(result.WallMesh.Vertices.Length, Is.EqualTo(0));
            Assert.That(result.RoofMesh.Vertices.Length, Is.EqualTo(0));
        }

        [Test]
        public void Extrude_TwoPointFootprint_ReturnsEmptyMeshes()
        {
            var twoPoints = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f) };

            BuildingMeshResult result = BuildingGenerator.Extrude(twoPoints);

            Assert.That(result.WallMesh.Vertices.Length, Is.EqualTo(0));
            Assert.That(result.RoofMesh.Vertices.Length, Is.EqualTo(0));
        }

        // ── Mesh geometry ─────────────────────────────────────────────────────

        [Test]
        public void Extrude_WallMesh_HasCorrectName()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(SquareFootprint);

            Assert.That(result.WallMesh.name, Is.EqualTo("BuildingWalls"));
        }

        [Test]
        public void Extrude_RoofMesh_HasCorrectName()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(SquareFootprint);

            Assert.That(result.RoofMesh.name, Is.EqualTo("BuildingRoof"));
        }

        [Test]
        public void Extrude_WallMesh_CorrectVertexCount()
        {
            // Each wall face uses 4 unique vertices (quad) for clean UV mapping.
            // Square footprint has 4 edges → 4 × 4 = 16 vertices.
            BuildingMeshResult result = BuildingGenerator.Extrude(SquareFootprint);

            Assert.That(result.WallMesh.Vertices.Length, Is.EqualTo(SquareFootprint.Count * 4));
        }

        [Test]
        public void Extrude_WallMesh_CorrectTriangleCount()
        {
            // Each wall quad → 2 triangles × 3 indices = 6.
            // 4 walls → 24 triangle indices.
            BuildingMeshResult result = BuildingGenerator.Extrude(SquareFootprint);

            Assert.That(result.WallMesh.Triangles.Length, Is.EqualTo(SquareFootprint.Count * 6));
        }

        [Test]
        public void Extrude_RoofMesh_CorrectVertexCount()
        {
            // Fan triangulation: centroid + one vertex per footprint corner.
            BuildingMeshResult result = BuildingGenerator.Extrude(SquareFootprint);

            Assert.That(result.RoofMesh.Vertices.Length, Is.EqualTo(SquareFootprint.Count + 1));
        }

        [Test]
        public void Extrude_WallVertices_TopRowAtExpectedHeight()
        {
            const float minH = 8f;
            const float maxH = 8f;   // pin to a known height by setting min == max
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, minHeight: minH, maxHeight: maxH, wayId: 0);

            // Top vertices (indices 2 and 3 per quad) should be at Y = 8.
            foreach (var v in result.WallMesh.Vertices)
                Assert.That(v.y, Is.EqualTo(0f).Or.EqualTo(minH).Within(1e-4f),
                    "Wall vertices must be either at Y=0 (base) or Y=height (top).");
        }

        [Test]
        public void Extrude_RoofVertices_AllAtExpectedHeight()
        {
            const float minH = 6f;
            const float maxH = 6f;
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, minHeight: minH, maxHeight: maxH, wayId: 0);

            foreach (var v in result.RoofMesh.Vertices)
                Assert.That(v.y, Is.EqualTo(minH).Within(1e-4f),
                    "All roof vertices must be elevated to the building height.");
        }

        [Test]
        public void Extrude_IsDeterministic_SameWayIdSameHeight()
        {
            // The same WayId must produce identical wall vertex positions.
            BuildingMeshResult a = BuildingGenerator.Extrude(SquareFootprint, wayId: 99999L);
            BuildingMeshResult b = BuildingGenerator.Extrude(SquareFootprint, wayId: 99999L);

            Assert.That(a.WallMesh.Vertices.Length, Is.EqualTo(b.WallMesh.Vertices.Length));
            for (int i = 0; i < a.WallMesh.Vertices.Length; i++)
                Assert.That(a.WallMesh.Vertices[i].y,
                    Is.EqualTo(b.WallMesh.Vertices[i].y).Within(1e-4f));
        }

        // ── Texture identifiers ───────────────────────────────────────────────

        [Test]
        public void Extrude_DefaultRegion_ReturnsNonEmptyTextureIds()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(SquareFootprint);

            Assert.That(result.WallTextureId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RoofTextureId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Extrude_InvalidFootprint_ReturnsEmptyTextureIds()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(null!);

            Assert.That(result.WallTextureId, Is.Empty);
            Assert.That(result.RoofTextureId, Is.Empty);
        }

        [Test]
        public void Extrude_Temperate_HasBrickWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Temperate);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_brick"));
        }

        [Test]
        public void Extrude_Temperate_HasSlateRoofTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Temperate);

            Assert.That(result.RoofTextureId, Is.EqualTo("building_roof_slate"));
        }

        [Test]
        public void Extrude_Desert_HasSandstoneWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Desert);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_sandstone"));
        }

        [Test]
        public void Extrude_Desert_HasTerracottaRoofTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Desert);

            Assert.That(result.RoofTextureId, Is.EqualTo("building_roof_terracotta"));
        }

        [Test]
        public void Extrude_Tropical_HasStuccoWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Tropical);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_stucco"));
        }

        [Test]
        public void Extrude_Boreal_HasTimberWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Boreal);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_timber"));
        }

        [Test]
        public void Extrude_Boreal_HasMetalRoofTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Boreal);

            Assert.That(result.RoofTextureId, Is.EqualTo("building_roof_metal"));
        }

        [Test]
        public void Extrude_Arctic_HasConcreteWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Arctic);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_concrete"));
        }

        [Test]
        public void Extrude_Arctic_HasMetalRoofTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Arctic);

            Assert.That(result.RoofTextureId, Is.EqualTo("building_roof_metal"));
        }

        [Test]
        public void Extrude_Mediterranean_HasStuccoWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Mediterranean);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_stucco"));
        }

        [Test]
        public void Extrude_Mediterranean_HasTerracottaRoofTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Mediterranean);

            Assert.That(result.RoofTextureId, Is.EqualTo("building_roof_terracotta"));
        }

        [Test]
        public void Extrude_Steppe_HasConcreteWallTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Steppe);

            Assert.That(result.WallTextureId, Is.EqualTo("building_wall_concrete"));
        }

        [Test]
        public void Extrude_Steppe_HasFlatRoofTexture()
        {
            BuildingMeshResult result = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Steppe);

            Assert.That(result.RoofTextureId, Is.EqualTo("building_roof_flat"));
        }

        [Test]
        public void Extrude_AllRegions_ReturnNonEmptyWallTextureId()
        {
            foreach (RegionType region in System.Enum.GetValues(typeof(RegionType)))
            {
                BuildingMeshResult result = BuildingGenerator.Extrude(
                    SquareFootprint, region: region);

                Assert.That(result.WallTextureId, Is.Not.Null.And.Not.Empty,
                    $"WallTextureId must not be empty for region '{region}'.");
            }
        }

        [Test]
        public void Extrude_AllRegions_ReturnNonEmptyRoofTextureId()
        {
            foreach (RegionType region in System.Enum.GetValues(typeof(RegionType)))
            {
                BuildingMeshResult result = BuildingGenerator.Extrude(
                    SquareFootprint, region: region);

                Assert.That(result.RoofTextureId, Is.Not.Null.And.Not.Empty,
                    $"RoofTextureId must not be empty for region '{region}'.");
            }
        }

        [Test]
        public void Extrude_DifferentRegions_ProduceDifferentWallTextures()
        {
            BuildingMeshResult temperate = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Temperate);
            BuildingMeshResult desert = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Desert);

            Assert.That(temperate.WallTextureId, Is.Not.EqualTo(desert.WallTextureId),
                "Temperate and Desert regions should use different wall textures.");
        }

        [Test]
        public void Extrude_DifferentRegions_ProduceDifferentRoofTextures()
        {
            BuildingMeshResult temperate = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Temperate);
            BuildingMeshResult arctic = BuildingGenerator.Extrude(
                SquareFootprint, region: RegionType.Arctic);

            Assert.That(temperate.RoofTextureId, Is.Not.EqualTo(arctic.RoofTextureId),
                "Temperate and Arctic regions should use different roof textures.");
        }
    }
}
