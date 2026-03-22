using System.Collections.Generic;
using UnityEngine;
using VectorRoad.DataInversion;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Scatters roadside props (lamp posts, trees, sign posts, fences) along a road spline
    /// at regular intervals, varying placement by road type and climate region.
    ///
    /// Usage:
    /// <code>
    ///   List&lt;Vector3&gt; spline = SplineGenerator.BuildCatmullRom(road.Nodes);
    ///   List&lt;PropPlacement&gt; props = RoadsidePropPlacer.Place(spline, RoadType.Residential, region, road.WayId);
    /// </code>
    /// </summary>
    public static class RoadsidePropPlacer
    {
        // ── Public constants ──────────────────────────────────────────────────

        /// <summary>
        /// Default lateral clearance (metres) beyond the road-edge kerb at which props are placed.
        /// </summary>
        public const float DefaultSideOffset = 1.5f;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Walks <paramref name="splinePoints"/> and returns one <see cref="PropPlacement"/>
        /// per prop position on each side of the road.
        /// </summary>
        /// <param name="splinePoints">
        /// Dense world-space centreline path (e.g. output of
        /// <see cref="SplineGenerator.BuildCatmullRom"/>). Needs at least two points.
        /// </param>
        /// <param name="roadType">Road classification; controls spacing and prop selection.</param>
        /// <param name="region">Climate zone used to vary which props appear.</param>
        /// <param name="wayId">OSM way ID used to seed deterministic prop-type variation.</param>
        /// <returns>
        /// A flat list of placements ordered by ascending <see cref="PropPlacement.DistanceAlong"/>,
        /// alternating left-side and right-side entries at each spacing interval.
        /// </returns>
        public static List<PropPlacement> Place(
            IList<Vector3> splinePoints,
            RoadType roadType,
            RegionType region = RegionType.Unknown,
            long wayId = 0)
        {
            var result = new List<PropPlacement>();

            if (splinePoints == null || splinePoints.Count < 2)
                return result;

            float spacing       = GetSpacingForRoadType(roadType);
            float halfWidth     = RoadMeshExtruder.GetWidthForRoadType(roadType) * 0.5f;
            float lateralOffset = halfWidth + RoadMeshExtruder.DefaultKerbWidth + DefaultSideOffset;

            PropType[] propCycle = GetPropTypesForRoad(roadType, region);
            if (propCycle.Length == 0)
                return result;

            // Start the first prop half-a-spacing in from the road tip so props
            // don't appear right at the junction.
            float nextTarget  = spacing * 0.5f;
            float accumulated = 0f;
            int   cycleIndex  = 0;

            for (int i = 1; i < splinePoints.Count; i++)
            {
                Vector3 from   = splinePoints[i - 1];
                Vector3 to     = splinePoints[i];
                float   segLen = Vector3.Distance(from, to);

                if (segLen < 1e-6f)
                    continue;

                // Unit tangent and right-hand perpendicular in the XZ plane.
                Vector3 tangent = (to - from) * (1f / segLen);
                var     right   = new Vector3(tangent.z, 0f, -tangent.x);

                float segEnd = accumulated + segLen;

                while (nextTarget <= segEnd)
                {
                    float   localT  = nextTarget - accumulated;
                    Vector3 centre  = from + tangent * localT;
                    PropType pt     = propCycle[cycleIndex % propCycle.Length];
                    cycleIndex++;

                    result.Add(new PropPlacement
                    {
                        Position      = centre - right * lateralOffset,
                        Forward       = tangent,
                        Type          = pt,
                        DistanceAlong = nextTarget,
                        IsLeftSide    = true,
                    });

                    result.Add(new PropPlacement
                    {
                        Position      = centre + right * lateralOffset,
                        Forward       = tangent,
                        Type          = pt,
                        DistanceAlong = nextTarget,
                        IsLeftSide    = false,
                    });

                    nextTarget += spacing;
                }

                accumulated = segEnd;
            }

            return result;
        }

        /// <summary>
        /// Returns the cycling prop-type sequence for a given road class and region.
        /// The <see cref="Place"/> method cycles through this array using modulo indexing.
        /// </summary>
        public static PropType[] GetPropTypesForRoad(RoadType roadType, RegionType region = RegionType.Unknown)
        {
            bool hasTrees = HasVegetation(region);

            switch (roadType)
            {
                case RoadType.Motorway:
                case RoadType.Trunk:
                    return new[] { PropType.LampPost };

                case RoadType.Primary:
                    return new[] { PropType.LampPost, PropType.SignPost };

                case RoadType.Secondary:
                    return hasTrees
                        ? new[] { PropType.LampPost, PropType.Tree, PropType.SignPost }
                        : new[] { PropType.LampPost, PropType.SignPost };

                case RoadType.Tertiary:
                case RoadType.Residential:
                    return hasTrees
                        ? new[] { PropType.Tree, PropType.LampPost, PropType.Tree, PropType.SignPost }
                        : new[] { PropType.LampPost, PropType.SignPost };

                case RoadType.Service:
                    return new[] { PropType.Fence, PropType.LampPost };

                case RoadType.Path:
                case RoadType.Cycleway:
                    return hasTrees
                        ? new[] { PropType.Tree, PropType.Fence }
                        : new[] { PropType.Fence };

                case RoadType.Dirt:
                    return hasTrees
                        ? new[] { PropType.Tree }
                        : new[] { PropType.Fence };

                default:
                    return new[] { PropType.LampPost };
            }
        }

        /// <summary>
        /// Returns the inter-prop spacing (metres) for a given road class.
        /// Busier, wider roads use wider spacing; minor roads receive denser props.
        /// </summary>
        public static float GetSpacingForRoadType(RoadType roadType)
        {
            switch (roadType)
            {
                case RoadType.Motorway:
                case RoadType.Trunk:     return 40f;
                case RoadType.Primary:   return 25f;
                case RoadType.Secondary: return 20f;
                case RoadType.Tertiary:
                case RoadType.Residential: return 15f;
                case RoadType.Service:   return 10f;
                case RoadType.Path:
                case RoadType.Cycleway:  return 10f;
                case RoadType.Dirt:      return 20f;
                default:                 return 20f;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the climate region supports roadside trees / vegetation.
        /// Desert and Arctic regions skip tree props in favour of signs or fences.
        /// </summary>
        private static bool HasVegetation(RegionType region) =>
            region != RegionType.Desert && region != RegionType.Arctic;
    }
}
