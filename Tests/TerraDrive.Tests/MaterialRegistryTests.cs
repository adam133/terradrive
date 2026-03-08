using NUnit.Framework;
using UnityEngine;
using TerraDrive.Procedural;

namespace TerraDrive.Tests
{
    [TestFixture]
    public class MaterialRegistryTests
    {
        // ── GetMaterial — null / empty / missing IDs ──────────────────────────

        [Test]
        public void GetMaterial_NullId_ReturnsNull()
        {
            var registry = new MaterialRegistry();

            Assert.That(registry.GetMaterial(null), Is.Null);
        }

        [Test]
        public void GetMaterial_EmptyId_ReturnsNull()
        {
            var registry = new MaterialRegistry();

            Assert.That(registry.GetMaterial(string.Empty), Is.Null);
        }

        [Test]
        public void GetMaterial_UnregisteredId_ReturnsNull()
        {
            var registry = new MaterialRegistry();

            Assert.That(registry.GetMaterial("road_asphalt_temperate"), Is.Null);
        }

        // ── Register + GetMaterial ────────────────────────────────────────────

        [Test]
        public void GetMaterial_RegisteredId_ReturnsCorrectMaterial()
        {
            var registry = new MaterialRegistry();
            var mat = new Material("road_asphalt_temperate");
            registry.Register("road_asphalt_temperate", mat);

            Assert.That(registry.GetMaterial("road_asphalt_temperate"), Is.SameAs(mat));
        }

        [Test]
        public void Register_NullMaterial_GetMaterialReturnsNull()
        {
            var registry = new MaterialRegistry();
            registry.Register("road_asphalt_temperate", null);

            Assert.That(registry.GetMaterial("road_asphalt_temperate"), Is.Null);
        }

        [Test]
        public void Register_NullId_IsIgnored()
        {
            var registry = new MaterialRegistry();
            var mat = new Material("mat");

            // Must not throw
            Assert.DoesNotThrow(() => registry.Register(null, mat));
            Assert.That(registry.GetMaterial(null), Is.Null);
        }

        [Test]
        public void Register_EmptyId_IsIgnored()
        {
            var registry = new MaterialRegistry();
            var mat = new Material("mat");

            Assert.DoesNotThrow(() => registry.Register(string.Empty, mat));
            Assert.That(registry.GetMaterial(string.Empty), Is.Null);
        }

        [Test]
        public void Register_DuplicateId_LastMaterialWins()
        {
            var registry = new MaterialRegistry();
            var mat1 = new Material("first");
            var mat2 = new Material("second");
            registry.Register("road_asphalt", mat1);
            registry.Register("road_asphalt", mat2);

            Assert.That(registry.GetMaterial("road_asphalt"), Is.SameAs(mat2));
        }

        [Test]
        public void Register_MultipleIds_EachReturnsOwnMaterial()
        {
            var registry = new MaterialRegistry();
            var matRoad = new Material("road");
            var matKerb = new Material("kerb");
            registry.Register("road_asphalt_temperate", matRoad);
            registry.Register("kerb_stone", matKerb);

            Assert.That(registry.GetMaterial("road_asphalt_temperate"), Is.SameAs(matRoad));
            Assert.That(registry.GetMaterial("kerb_stone"), Is.SameAs(matKerb));
        }

        // ── BuildLookup ───────────────────────────────────────────────────────

        [Test]
        public void BuildLookup_AfterRegister_MaterialIsStillRetrievable()
        {
            var registry = new MaterialRegistry();
            var mat = new Material("road_asphalt");
            registry.Register("road_asphalt", mat);

            // Re-building the dictionary from the stored entries should
            // produce the same result.
            registry.BuildLookup();

            Assert.That(registry.GetMaterial("road_asphalt"), Is.SameAs(mat));
        }

        [Test]
        public void BuildLookup_EmptyRegistry_GetMaterialReturnsNull()
        {
            var registry = new MaterialRegistry();
            registry.BuildLookup();

            Assert.That(registry.GetMaterial("road_asphalt"), Is.Null);
        }

        // ── ApplyTo ───────────────────────────────────────────────────────────

        [Test]
        public void ApplyTo_NullRenderer_DoesNotThrow()
        {
            var registry = new MaterialRegistry();
            var mat = new Material("road_asphalt");
            registry.Register("road_asphalt", mat);

            Assert.DoesNotThrow(() => registry.ApplyTo(null, "road_asphalt"));
        }

        [Test]
        public void ApplyTo_RegisteredId_SetsMaterialOnRenderer()
        {
            var registry = new MaterialRegistry();
            var mat = new Material("road_asphalt");
            registry.Register("road_asphalt", mat);
            var renderer = new MeshRenderer();

            registry.ApplyTo(renderer, "road_asphalt");

            Assert.That(renderer.sharedMaterial, Is.SameAs(mat));
        }

        [Test]
        public void ApplyTo_UnregisteredId_LeavesRendererMaterialUnchanged()
        {
            var registry = new MaterialRegistry();
            var original = new Material("existing");
            var renderer = new MeshRenderer { sharedMaterial = original };

            registry.ApplyTo(renderer, "road_asphalt_temperate");

            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
        }

        [Test]
        public void ApplyTo_NullId_LeavesRendererMaterialUnchanged()
        {
            var registry = new MaterialRegistry();
            var original = new Material("existing");
            var renderer = new MeshRenderer { sharedMaterial = original };

            registry.ApplyTo(renderer, null);

            Assert.That(renderer.sharedMaterial, Is.SameAs(original));
        }

        // ── Integration with RegionTextures IDs ───────────────────────────────

        [Test]
        public void Register_AllKnownRoadTextureIds_CanAllBeRetrieved()
        {
            // Verify that the registry correctly stores every ID returned by
            // RegionTextures so that a fully configured registry in the scene
            // would cover all possible lookup calls.
            var ids = new[]
            {
                "road_asphalt",
                "road_asphalt_temperate",
                "road_asphalt_desert",
                "road_asphalt_tropical",
                "road_asphalt_boreal",
                "road_asphalt_arctic",
                "road_asphalt_mediterranean",
                "road_asphalt_steppe",
                "road_dirt",
                "road_sand",
                "road_mud",
                "road_gravel_boreal",
                "road_gravel_arctic",
                "kerb_stone",
                "kerb_concrete",
                "kerb_granite",
            };

            var registry = new MaterialRegistry();
            foreach (var id in ids)
                registry.Register(id, new Material(id));

            foreach (var id in ids)
                Assert.That(registry.GetMaterial(id), Is.Not.Null,
                    $"Material for '{id}' should be retrievable after registration.");
        }

        [Test]
        public void Register_AllKnownBuildingTextureIds_CanAllBeRetrieved()
        {
            var ids = new[]
            {
                "building_wall_brick",
                "building_wall_sandstone",
                "building_wall_stucco",
                "building_wall_timber",
                "building_wall_concrete",
                "building_roof_slate",
                "building_roof_terracotta",
                "building_roof_metal",
                "building_roof_flat",
            };

            var registry = new MaterialRegistry();
            foreach (var id in ids)
                registry.Register(id, new Material(id));

            foreach (var id in ids)
                Assert.That(registry.GetMaterial(id), Is.Not.Null,
                    $"Material for '{id}' should be retrievable after registration.");
        }
    }
}
