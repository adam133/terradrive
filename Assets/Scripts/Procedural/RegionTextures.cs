using TerraDrive.DataInversion;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Provides region-appropriate texture identifiers for road and building meshes.
    ///
    /// The string IDs returned by each method correspond to named texture assets in the
    /// Unity project (e.g. the key used to look up a <c>Material</c> or a texture entry
    /// in a <c>ResourceManager</c>).  All IDs use lowercase with underscores so they are
    /// safe to use as <c>Resources.Load</c> paths.
    ///
    /// Usage:
    /// <code>
    ///   string roadTex = RegionTextures.GetRoadSurfaceTextureId(region, roadType);
    ///   string wallTex = RegionTextures.GetWallTextureId(region);
    ///   string roofTex = RegionTextures.GetRoofTextureId(region);
    /// </code>
    /// </summary>
    public static class RegionTextures
    {
        // ── Road surface ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the texture identifier for the road surface appropriate to the
        /// given climate region and road type.
        /// </summary>
        /// <param name="region">Climate zone of the map area.</param>
        /// <param name="roadType">Functional road classification.</param>
        /// <returns>
        /// A lowercase underscore-separated texture asset name, e.g.
        /// <c>"road_asphalt_temperate"</c> or <c>"road_dirt_tropical"</c>.
        /// </returns>
        public static string GetRoadSurfaceTextureId(RegionType region, RoadType roadType = RoadType.Unknown)
        {
            // Unpaved tracks use a surface-specific texture regardless of region.
            if (roadType == RoadType.Dirt || roadType == RoadType.Path)
                return GetUnpavedSurfaceTextureId(region);

            return region switch
            {
                RegionType.Temperate    => "road_asphalt_temperate",
                RegionType.Desert       => "road_asphalt_desert",
                RegionType.Tropical     => "road_asphalt_tropical",
                RegionType.Boreal       => "road_asphalt_boreal",
                RegionType.Arctic       => "road_asphalt_arctic",
                RegionType.Mediterranean => "road_asphalt_mediterranean",
                RegionType.Steppe       => "road_asphalt_steppe",
                _                       => "road_asphalt",
            };
        }

        // ── Kerb ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the texture identifier for kerb (curb) surfaces appropriate to
        /// the given climate region.
        /// </summary>
        /// <param name="region">Climate zone of the map area.</param>
        /// <returns>A lowercase underscore-separated texture asset name.</returns>
        public static string GetKerbTextureId(RegionType region)
        {
            return region switch
            {
                RegionType.Temperate    => "kerb_stone",
                RegionType.Desert       => "kerb_concrete",
                RegionType.Tropical     => "kerb_concrete",
                RegionType.Boreal       => "kerb_stone",
                RegionType.Arctic       => "kerb_concrete",
                RegionType.Mediterranean => "kerb_granite",
                RegionType.Steppe       => "kerb_concrete",
                _                       => "kerb_stone",
            };
        }

        // ── Building wall ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the texture identifier for building wall surfaces appropriate to
        /// the given climate region.
        /// </summary>
        /// <param name="region">Climate zone of the map area.</param>
        /// <returns>A lowercase underscore-separated texture asset name.</returns>
        public static string GetWallTextureId(RegionType region)
        {
            return region switch
            {
                RegionType.Temperate    => "building_wall_brick",
                RegionType.Desert       => "building_wall_sandstone",
                RegionType.Tropical     => "building_wall_stucco",
                RegionType.Boreal       => "building_wall_timber",
                RegionType.Arctic       => "building_wall_concrete",
                RegionType.Mediterranean => "building_wall_stucco",
                RegionType.Steppe       => "building_wall_concrete",
                _                       => "building_wall_brick",
            };
        }

        // ── Building roof ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the texture identifier for building roof surfaces appropriate to
        /// the given climate region.
        /// </summary>
        /// <param name="region">Climate zone of the map area.</param>
        /// <returns>A lowercase underscore-separated texture asset name.</returns>
        public static string GetRoofTextureId(RegionType region)
        {
            return region switch
            {
                RegionType.Temperate    => "building_roof_slate",
                RegionType.Desert       => "building_roof_terracotta",
                RegionType.Tropical     => "building_roof_terracotta",
                RegionType.Boreal       => "building_roof_metal",
                RegionType.Arctic       => "building_roof_metal",
                RegionType.Mediterranean => "building_roof_terracotta",
                RegionType.Steppe       => "building_roof_flat",
                _                       => "building_roof_slate",
            };
        }

        // ── Lane markings ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the texture identifier for the lane-marking overlay (UV channel 1
        /// on the road surface mesh).
        /// </summary>
        /// <param name="isOneWay">
        /// <c>true</c> for a one-way road (no opposing-traffic centre line);
        /// <c>false</c> for a bidirectional road (centre line separates directions).
        /// </param>
        /// <returns>
        /// <c>"lane_marking_oneway"</c> or <c>"lane_marking_twoway"</c>.
        /// </returns>
        public static string GetLaneMarkingTextureId(bool isOneWay) =>
            isOneWay ? "lane_marking_oneway" : "lane_marking_twoway";

        // ── Water surface ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the texture identifier for water surfaces appropriate to the given
        /// climate region.
        /// </summary>
        /// <param name="region">Climate zone of the map area.</param>
        /// <returns>A lowercase underscore-separated texture asset name.</returns>
        public static string GetWaterTextureId(RegionType region)
        {
            return region switch
            {
                RegionType.Arctic        => "water_arctic",
                RegionType.Tropical      => "water_tropical",
                _                        => "water",
            };
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Returns a surface texture ID for unpaved roads (dirt tracks and footpaths),
        /// varying the appearance by region.
        /// </summary>
        private static string GetUnpavedSurfaceTextureId(RegionType region)
        {
            return region switch
            {
                RegionType.Desert  => "road_sand",
                RegionType.Boreal  => "road_gravel_boreal",
                RegionType.Arctic  => "road_gravel_arctic",
                RegionType.Tropical => "road_mud",
                _                  => "road_dirt",
            };
        }
    }
}
