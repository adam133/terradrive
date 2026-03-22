using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.DataInversion;
using VectorRoad.Procedural;

namespace VectorRoad.Tests
{
    /// <summary>
    /// Unit tests for <see cref="WaterMeshGenerator"/>.
    ///
    /// Each test constructs a minimal <see cref="WaterBody"/>, calls
    /// <see cref="WaterMeshGenerator.Generate"/>, and asserts on the resulting mesh
    /// topology and texture identifier.
    /// </summary>
    [TestFixture]
    public class WaterMeshGeneratorTests
    {
        // ── Generate — basic mesh topology ────────────────────────────────────

        [Test]
        public void Generate_TriangleOutline_ReturnsMeshWithCorrectVertexCount()
        {
            // 3 outline points → centroid + 3 outline = 4 vertices
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.Vertices.Length, Is.EqualTo(4),
                "Triangle outline (3 points) + centroid = 4 vertices.");
        }

        [Test]
        public void Generate_TriangleOutline_ReturnsMeshWithCorrectTriangleCount()
        {
            // 3 outline points → 3 fan triangles → 9 triangle indices
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.Triangles.Length, Is.EqualTo(9),
                "3 fan triangles → 9 triangle indices.");
        }

        [Test]
        public void Generate_QuadOutline_ReturnsMeshWithCorrectVertexCount()
        {
            // 4 outline points → centroid + 4 = 5 vertices
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
                new Vector3(0f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.Vertices.Length, Is.EqualTo(5),
                "Quad outline (4 points) + centroid = 5 vertices.");
        }

        [Test]
        public void Generate_QuadOutline_ReturnsMeshWithCorrectTriangleCount()
        {
            // 4 outline points → 4 fan triangles → 12 triangle indices
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
                new Vector3(0f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.Triangles.Length, Is.EqualTo(12),
                "4 fan triangles → 12 triangle indices.");
        }

        // ── Generate — Y elevation ────────────────────────────────────────────

        [Test]
        public void Generate_AllVerticesAreAtAverageElevation()
        {
            // Outline nodes at varying elevations; the mesh should be flat at the average.
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 10f, 0f),
                new Vector3(10f, 20f, 0f),
                new Vector3(5f, 30f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            float expectedY = (10f + 20f + 30f) / 3f;
            foreach (Vector3 v in result.Mesh.Vertices)
                Assert.That(v.y, Is.EqualTo(expectedY).Within(1e-4f),
                    "Every mesh vertex should sit at the average outline elevation.");
        }

        [Test]
        public void Generate_FlatOutlineAtZero_AllVerticesAtYZero()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(5f, 0f, 0f),
                new Vector3(5f, 0f, 5f),
                new Vector3(0f, 0f, 5f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            foreach (Vector3 v in result.Mesh.Vertices)
                Assert.That(v.y, Is.EqualTo(0f),
                    "Flat outline at Y=0 → all vertices at Y=0.");
        }

        // ── Generate — texture ID ──────────────────────────────────────────────

        [Test]
        public void Generate_UnknownRegion_ReturnsDefaultWaterTextureId()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water, RegionType.Unknown);

            Assert.That(result.TextureId, Is.EqualTo("water"));
        }

        [Test]
        public void Generate_ArcticRegion_ReturnsArcticWaterTextureId()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water, RegionType.Arctic);

            Assert.That(result.TextureId, Is.EqualTo("water_arctic"));
        }

        [Test]
        public void Generate_TropicalRegion_ReturnsTropicalWaterTextureId()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water, RegionType.Tropical);

            Assert.That(result.TextureId, Is.EqualTo("water_tropical"));
        }

        [Test]
        public void Generate_TemperateRegion_ReturnsDefaultWaterTextureId()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water, RegionType.Temperate);

            Assert.That(result.TextureId, Is.EqualTo("water"));
        }

        // ── Generate — guard clauses ──────────────────────────────────────────

        [Test]
        public void Generate_NullWaterBody_ReturnsEmptyMesh()
        {
            WaterMeshResult result = WaterMeshGenerator.Generate(null!);

            Assert.That(result.Mesh, Is.Not.Null);
            Assert.That(result.Mesh.Vertices.Length, Is.EqualTo(0));
            Assert.That(result.TextureId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Generate_TwoPointOutline_ReturnsEmptyMesh()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.Vertices.Length, Is.EqualTo(0),
                "Fewer than 3 outline points should produce an empty mesh.");
        }

        [Test]
        public void Generate_EmptyOutline_ReturnsEmptyMesh()
        {
            var water = new WaterBody { Outline = new List<Vector3>() };

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.Vertices.Length, Is.EqualTo(0));
        }

        // ── Generate — mesh name ──────────────────────────────────────────────

        [Test]
        public void Generate_ValidOutline_MeshIsNamedWaterSurface()
        {
            var water = MakeWaterBody(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

            WaterMeshResult result = WaterMeshGenerator.Generate(water);

            Assert.That(result.Mesh.name, Is.EqualTo("WaterSurface"));
        }

        // ── RegionTextures.GetWaterTextureId ──────────────────────────────────

        [Test]
        public void GetWaterTextureId_Unknown_ReturnsWater()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Unknown), Is.EqualTo("water"));

        [Test]
        public void GetWaterTextureId_Temperate_ReturnsWater()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Temperate), Is.EqualTo("water"));

        [Test]
        public void GetWaterTextureId_Desert_ReturnsWater()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Desert), Is.EqualTo("water"));

        [Test]
        public void GetWaterTextureId_Boreal_ReturnsWater()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Boreal), Is.EqualTo("water"));

        [Test]
        public void GetWaterTextureId_Steppe_ReturnsWater()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Steppe), Is.EqualTo("water"));

        [Test]
        public void GetWaterTextureId_Mediterranean_ReturnsWater()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Mediterranean), Is.EqualTo("water"));

        [Test]
        public void GetWaterTextureId_Arctic_ReturnsWaterArctic()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Arctic), Is.EqualTo("water_arctic"));

        [Test]
        public void GetWaterTextureId_Tropical_ReturnsWaterTropical()
            => Assert.That(RegionTextures.GetWaterTextureId(RegionType.Tropical), Is.EqualTo("water_tropical"));

        // ── Helpers ───────────────────────────────────────────────────────────

        private static WaterBody MakeWaterBody(IEnumerable<Vector3> outline)
        {
            return new WaterBody
            {
                WayId   = 1L,
                Outline = new List<Vector3>(outline),
            };
        }
    }
}
