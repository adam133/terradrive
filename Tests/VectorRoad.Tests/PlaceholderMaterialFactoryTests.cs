using NUnit.Framework;
using UnityEngine;
using VectorRoad.Procedural;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class PlaceholderMaterialFactoryTests
    {
        // ── Create ────────────────────────────────────────────────────────────

        [Test]
        public void Create_WithValidTextureId_ReturnsNonNullMaterial()
        {
            var mat = PlaceholderMaterialFactory.Create("road_asphalt");
            Assert.That(mat, Is.Not.Null);
        }

        [Test]
        public void Create_SetsNameToTextureId()
        {
            var mat = PlaceholderMaterialFactory.Create("road_asphalt_temperate");
            Assert.That(mat.name, Is.EqualTo("road_asphalt_temperate"));
        }

        [TestCase("road_asphalt")]
        [TestCase("road_asphalt_temperate")]
        [TestCase("road_asphalt_desert")]
        [TestCase("road_dirt")]
        [TestCase("road_sand")]
        [TestCase("road_mud")]
        [TestCase("road_gravel_boreal")]
        [TestCase("kerb_stone")]
        [TestCase("kerb_concrete")]
        [TestCase("kerb_granite")]
        [TestCase("building_wall_brick")]
        [TestCase("building_wall_sandstone")]
        [TestCase("building_roof_slate")]
        [TestCase("building_roof_terracotta")]
        [TestCase("terrain_grass")]
        [TestCase("water")]
        [TestCase("water_arctic")]
        [TestCase("water_tropical")]
        [TestCase("lane_marking_oneway")]
        [TestCase("lane_marking_twoway")]
        [TestCase("prop_lamppost")]
        [TestCase("prop_signpost")]
        [TestCase("prop_tree")]
        [TestCase("prop_fence")]
        public void Create_AllKnownIds_ReturnMaterialWithDistinctColor(string textureId)
        {
            // Magenta (r=1, g=0, b=1) is Unity's "missing material" colour.
            // Any placeholder must use a different colour.
            const float MagentaR = 1f, MagentaG = 0f, MagentaB = 1f;

            var mat = PlaceholderMaterialFactory.Create(textureId);

            Assert.That(mat, Is.Not.Null, $"Material for '{textureId}' should not be null.");
            Assert.That(
                mat.color.r == MagentaR && mat.color.g == MagentaG && mat.color.b == MagentaB,
                Is.False,
                $"'{textureId}' placeholder must not be magenta (missing-material colour).");
        }

        [Test]
        public void Create_AsphaltRoads_HaveDarkGreyColor()
        {
            foreach (var id in new[] { "road_asphalt", "road_asphalt_temperate", "road_asphalt_desert" })
            {
                var mat = PlaceholderMaterialFactory.Create(id);
                // Dark grey: all channels roughly equal and below 0.5
                Assert.That(mat.color.r, Is.LessThan(0.5f), $"{id}: red channel should be dark");
                Assert.That(mat.color.g, Is.LessThan(0.5f), $"{id}: green channel should be dark");
                Assert.That(mat.color.b, Is.LessThan(0.5f), $"{id}: blue channel should be dark");
            }
        }

        [Test]
        public void Create_TerrainGrass_HasGreenDominance()
        {
            var mat = PlaceholderMaterialFactory.Create("terrain_grass");
            Assert.That(mat.color.g, Is.GreaterThan(mat.color.r), "Terrain grass: green > red");
            Assert.That(mat.color.g, Is.GreaterThan(mat.color.b), "Terrain grass: green > blue");
        }

        [Test]
        public void Create_Water_HasBlueDominance()
        {
            var mat = PlaceholderMaterialFactory.Create("water");
            Assert.That(mat.color.b, Is.GreaterThan(mat.color.r), "Water: blue > red");
        }

        [Test]
        public void Create_LaneMarkings_AreWhite()
        {
            foreach (var id in new[] { "lane_marking_oneway", "lane_marking_twoway" })
            {
                var mat = PlaceholderMaterialFactory.Create(id);
                Assert.That(mat.color.r, Is.EqualTo(1f).Within(0.001f), $"{id}: white red");
                Assert.That(mat.color.g, Is.EqualTo(1f).Within(0.001f), $"{id}: white green");
                Assert.That(mat.color.b, Is.EqualTo(1f).Within(0.001f), $"{id}: white blue");
            }
        }

        [Test]
        public void Create_UnknownId_ReturnsNeutralGreyMaterial()
        {
            var mat = PlaceholderMaterialFactory.Create("some_unknown_texture");
            Assert.That(mat, Is.Not.Null);
            Assert.That(mat.color.r, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(mat.color.g, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(mat.color.b, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Create_PropPostIds_HaveMidGreyColor()
        {
            foreach (var id in new[] { "prop_lamppost", "prop_signpost" })
            {
                var mat = PlaceholderMaterialFactory.Create(id);
                Assert.That(mat.color.r, Is.EqualTo(0.60f).Within(0.001f), $"{id}: red channel");
                Assert.That(mat.color.g, Is.EqualTo(0.60f).Within(0.001f), $"{id}: green channel");
                Assert.That(mat.color.b, Is.EqualTo(0.60f).Within(0.001f), $"{id}: blue channel");
            }
        }

        [Test]
        public void Create_PropTree_HasGreenDominance()
        {
            var mat = PlaceholderMaterialFactory.Create("prop_tree");
            Assert.That(mat.color.g, Is.GreaterThan(mat.color.r), "prop_tree: green > red");
            Assert.That(mat.color.g, Is.GreaterThan(mat.color.b), "prop_tree: green > blue");
        }

        [Test]
        public void Create_PropFence_HasWarmBrownTone()
        {
            var mat = PlaceholderMaterialFactory.Create("prop_fence");
            // Weathered wood: red > blue, both > 0.4
            Assert.That(mat.color.r, Is.GreaterThan(mat.color.b), "prop_fence: red > blue");
            Assert.That(mat.color.r, Is.GreaterThan(0.4f), "prop_fence: visible red component");
            Assert.That(mat.color.b, Is.GreaterThan(0.4f), "prop_fence: visible blue component");
        }

        // ── FillMissing ───────────────────────────────────────────────────────

        [Test]
        public void FillMissing_PopulatesAllKnownTextureIds()
        {
            var registry = new MaterialRegistry();
            // Simulate Awake by calling BuildLookup first (empty — all null in scene)
            registry.BuildLookup();

            PlaceholderMaterialFactory.FillMissing(registry);

            foreach (var id in new[]
            {
                "road_asphalt", "road_asphalt_temperate", "road_asphalt_desert",
                "road_asphalt_tropical", "road_asphalt_boreal", "road_asphalt_arctic",
                "road_asphalt_mediterranean", "road_asphalt_steppe",
                "road_dirt", "road_sand", "road_mud", "road_gravel_boreal", "road_gravel_arctic",
                "kerb_stone", "kerb_concrete", "kerb_granite",
                "building_wall_brick", "building_wall_sandstone", "building_wall_stucco",
                "building_wall_timber", "building_wall_concrete",
                "building_roof_slate", "building_roof_terracotta", "building_roof_metal", "building_roof_flat",
                "terrain_grass",
                "water", "water_arctic", "water_tropical",
                "lane_marking_oneway", "lane_marking_twoway",
                "prop_lamppost", "prop_signpost", "prop_tree", "prop_fence",
            })
            {
                Assert.That(registry.GetMaterial(id), Is.Not.Null,
                    $"FillMissing should have created a placeholder for '{id}'.");
            }
        }

        [Test]
        public void FillMissing_DoesNotOverwriteExistingMaterial()
        {
            var registry = new MaterialRegistry();
            var existing = new Material("my_road");
            registry.Register("road_asphalt", existing);

            PlaceholderMaterialFactory.FillMissing(registry);

            Assert.That(registry.GetMaterial("road_asphalt"), Is.SameAs(existing),
                "FillMissing should not replace a material that is already registered.");
        }
    }
}
