using System.Collections.Generic;
using VectorRoad.DataInversion;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Provides region-based width multipliers and road-type shoulder widths used to
    /// calculate realistic road carriageway widths.
    ///
    /// Road width formula (when a lane count is known):
    /// <code>
    ///   width = (lanes × laneWidth + ShoulderWidth(roadType)) × GetWidthFactor(region)
    /// </code>
    ///
    /// When no lane count is available the caller falls back to the canonical
    /// <see cref="RoadMeshExtruder.GetWidthForRoadType(RoadType)"/> table value,
    /// scaled by <see cref="GetWidthFactor(RegionType)"/>.
    /// </summary>
    public static class RegionWidthFactors
    {
        /// <summary>
        /// Width multipliers keyed by <see cref="RegionType"/>.
        /// Values above 1.0 produce wider roads; values below 1.0 produce narrower roads
        /// relative to the European/temperate baseline.
        ///
        /// Reference baselines (lane widths, metres):
        ///   USA/Canada      3.6 – 3.7 m  → factor 1.1
        ///   Western Europe  3.25 – 3.5 m → factor 1.0
        ///   Developing regions often narrower → factors 0.85 – 0.95
        /// </summary>
        private static readonly Dictionary<RegionType, float> WidthFactors =
            new Dictionary<RegionType, float>
            {
                { RegionType.TemperateNorthAmerica, 1.10f }, // USA / Canada: wider lanes and shoulders
                { RegionType.Temperate,             1.00f }, // Western/Central Europe, East Asia: baseline
                { RegionType.Mediterranean,         1.00f }, // Southern Europe: similar to Temperate
                { RegionType.Boreal,                1.00f }, // Scandinavia / Russia: same infrastructure standard
                { RegionType.Desert,                0.95f }, // Middle East / North Africa: slightly narrower
                { RegionType.Arctic,                0.90f }, // Greenland / Iceland: limited infrastructure
                { RegionType.Tropical,              0.90f }, // Equatorial Africa / SE Asia / Central America
                { RegionType.Steppe,                0.85f }, // Central Asia: developing-region standard
                { RegionType.Unknown,               1.00f }, // No region data: use baseline
            };

        /// <summary>
        /// Total shoulder width in metres (both sides combined) added to the lane
        /// carriageway for each <see cref="RoadType"/>.
        ///
        /// A shoulder provides a safety buffer and emergency stopping area.  Higher-
        /// class roads have wider hard shoulders; local and path roads have none.
        /// </summary>
        private static readonly Dictionary<RoadType, float> ShoulderWidths =
            new Dictionary<RoadType, float>
            {
                { RoadType.Motorway,     6.0f }, // 3 m hard shoulder each side
                { RoadType.Trunk,        5.0f }, // 2.5 m each side
                { RoadType.Primary,      3.0f }, // 1.5 m each side
                { RoadType.Secondary,    1.5f }, // 0.75 m each side
                { RoadType.Tertiary,     1.0f }, // 0.5 m each side
                { RoadType.Residential,  0.5f }, // narrow verge / parking strip
                { RoadType.Service,      0.0f }, // access lane — no shoulder
                { RoadType.Dirt,         0.0f }, // unsurfaced track
                { RoadType.Path,         0.0f }, // footpath
                { RoadType.Cycleway,     0.0f }, // cycle lane
                { RoadType.Unknown,      0.0f }, // no shoulder data — safe fallback
            };

        /// <summary>
        /// Returns the width multiplier for the given <paramref name="region"/>.
        /// Values greater than 1.0 indicate roads wider than the European baseline;
        /// values less than 1.0 indicate narrower roads.
        /// Returns 1.0 for any region not explicitly mapped.
        /// </summary>
        /// <param name="region">The region type to look up.</param>
        /// <returns>A positive floating-point multiplier.</returns>
        public static float GetWidthFactor(RegionType region) =>
            WidthFactors.TryGetValue(region, out float f) ? f : 1.0f;

        /// <summary>
        /// Returns the total shoulder width in metres (both sides combined) appropriate
        /// for the given <paramref name="roadType"/>.
        /// Returns 0 for any road type not explicitly mapped.
        /// </summary>
        /// <param name="roadType">The functional road classification.</param>
        /// <returns>Total shoulder width in metres (≥ 0).</returns>
        public static float GetShoulderWidth(RoadType roadType) =>
            ShoulderWidths.TryGetValue(roadType, out float s) ? s : 0.0f;
    }
}
