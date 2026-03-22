using UnityEngine;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Creates solid-colour placeholder <c>Material</c> objects for every known texture
    /// identifier so that meshes render with a recognisable tint rather than the
    /// engine's default magenta "missing material" colour.
    ///
    /// <para>
    /// Placeholder colours are intentionally distinct per category:
    /// asphalt roads (dark grey), dirt/unpaved (brown), kerbs (light grey),
    /// building walls (warm tan), building roofs (dark brown-grey),
    /// terrain (grass green), water (blue).
    /// </para>
    ///
    /// <para>
    /// All placeholders use Unity's built-in Standard shader with zero gloss and
    /// zero metallic so they look flat but correctly lit.  They are created once
    /// and cached inside the <see cref="MaterialRegistry"/>'s own lookup table.
    /// </para>
    /// </summary>
    internal static class PlaceholderMaterialFactory
    {
        // ── All texture IDs that may be requested at runtime ───────────────────

        private static readonly string[] KnownIds =
        {
            // Road surfaces
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

            // Kerbs
            "kerb_stone",
            "kerb_concrete",
            "kerb_granite",

            // Building walls
            "building_wall_brick",
            "building_wall_sandstone",
            "building_wall_stucco",
            "building_wall_timber",
            "building_wall_concrete",

            // Building roofs
            "building_roof_slate",
            "building_roof_terracotta",
            "building_roof_metal",
            "building_roof_flat",

            // Terrain
            "terrain_grass",

            // Water
            "water",
            "water_arctic",
            "water_tropical",

            // Lane markings
            "lane_marking_oneway",
            "lane_marking_twoway",
        };

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a placeholder <c>Material</c> for every known texture ID that
        /// does not already have a non-null material in <paramref name="registry"/>.
        /// Existing (designer-assigned) materials are never overwritten.
        /// </summary>
        internal static void FillMissing(MaterialRegistry registry)
        {
            foreach (var id in KnownIds)
            {
                if (registry.GetMaterial(id) == null)
                    registry.Register(id, Create(id));
            }
        }

        /// <summary>
        /// Creates a new placeholder <c>Material</c> for the given
        /// <paramref name="textureId"/> using a category-appropriate solid colour.
        /// </summary>
        internal static Material Create(string textureId)
        {
            // Prefer the Standard shader; fall back to progressively simpler built-in
            // shaders so this works in the Built-in, URP, and HDRP render pipelines.
            var shader = Shader.Find("Standard")
                      ?? Shader.Find("Legacy Shaders/Diffuse")
                      ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[PlaceholderMaterialFactory] No suitable shader found. " +
                    "Placeholder colour will not be applied for: " + textureId);
                return null;
            }

            var mat    = new Material(shader) { name = textureId };
            mat.color  = GetPlaceholderColor(textureId);
            mat.SetFloat("_Glossiness", 0f);
            mat.SetFloat("_Metallic",   0f);
            return mat;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static Color GetPlaceholderColor(string id)
        {
            if (id.StartsWith("road_asphalt"))
                return new Color(0.29f, 0.29f, 0.29f);   // dark asphalt grey

            if (id == "road_dirt" || id == "road_mud")
                return new Color(0.54f, 0.40f, 0.20f);   // earthy brown

            if (id == "road_sand")
                return new Color(0.76f, 0.70f, 0.50f);   // sandy yellow

            if (id.StartsWith("road_gravel"))
                return new Color(0.58f, 0.55f, 0.45f);   // gravel grey-brown

            if (id.StartsWith("kerb"))
                return new Color(0.72f, 0.72f, 0.72f);   // light kerb grey

            if (id.StartsWith("building_wall"))
                return new Color(0.80f, 0.65f, 0.50f);   // warm sandstone tan

            if (id.StartsWith("building_roof"))
                return new Color(0.40f, 0.35f, 0.35f);   // dark roof grey-brown

            if (id.StartsWith("terrain"))
                return new Color(0.35f, 0.55f, 0.25f);   // grass green

            // Water variants: use exact matches first so that water_arctic and
            // water_tropical are not accidentally caught by the "water" prefix check.
            if (id == "water_arctic")
                return new Color(0.70f, 0.85f, 0.95f);   // icy pale blue

            if (id == "water_tropical")
                return new Color(0.00f, 0.65f, 0.75f);   // turquoise

            if (id == "water")
                return new Color(0.18f, 0.47f, 0.71f);   // mid water blue

            if (id.StartsWith("lane_marking"))
                return Color.white;

            return new Color(0.50f, 0.50f, 0.50f);        // neutral fallback
        }
    }
}
