using System;
using System.Collections.Generic;
using UnityEngine;
using VectorRoad.DataInversion;

namespace VectorRoad.Hud
{
    /// <summary>
    /// Renders a top-down minimap of nearby road segments relative to the player's
    /// current world position.
    ///
    /// <para>
    /// The output is a list of <see cref="MinimapLine"/> objects in normalised [0, 1]
    /// minimap coordinates, ready to be drawn as 2-D line primitives on a HUD canvas.
    /// </para>
    ///
    /// <para>
    /// Coordinate convention: (0.5, 0.5) is always the player's position.
    /// The X axis increases to the right (east) and the Y axis increases upward
    /// (north).  When <c>playerYawDegrees</c> is non-zero the entire map is rotated so
    /// that the player's forward direction always points toward the top of the minimap.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   var renderer = new MinimapRenderer { Radius = 200f };
    ///   List&lt;MinimapLine&gt; lines = renderer.BuildLines(mapData.Roads, carTransform.position, carYaw);
    /// </code>
    /// </summary>
    public sealed class MinimapRenderer
    {
        /// <summary>
        /// World-space radius (metres) around the player that is shown on the minimap.
        /// Roads whose both endpoints lie outside this radius are excluded.
        /// Default: 150 m.
        /// </summary>
        public float Radius { get; set; } = 150f;

        /// <summary>
        /// Projects road segments onto the minimap and returns a list of line segments
        /// in normalised [0, 1] minimap space.
        /// </summary>
        /// <param name="roads">All road segments in the loaded map.</param>
        /// <param name="playerPosition">Player's current world-space position.</param>
        /// <param name="playerYawDegrees">
        /// Player's yaw angle in degrees (clockwise from north / +Z).  When non-zero
        /// the minimap is rotated so the player always faces the top of the display.
        /// </param>
        /// <returns>
        /// A list of <see cref="MinimapLine"/> objects.  Lines whose both endpoints
        /// are outside the visible radius are omitted.
        /// </returns>
        public List<MinimapLine> BuildLines(
            IEnumerable<RoadSegment> roads,
            Vector3 playerPosition,
            float playerYawDegrees = 0f)
        {
            if (roads == null) return new List<MinimapLine>();

            double rad     = playerYawDegrees * Math.PI / 180.0;
            float  cosYaw  = (float)Math.Cos(rad);
            float  sinYaw  = (float)Math.Sin(rad);
            float  diameter = 2f * Radius;

            var lines = new List<MinimapLine>();

            foreach (RoadSegment segment in roads)
            {
                if (segment?.Nodes == null || segment.Nodes.Count < 2) continue;

                for (int i = 0; i < segment.Nodes.Count - 1; i++)
                {
                    Vector3 a = segment.Nodes[i];
                    Vector3 b = segment.Nodes[i + 1];

                    float dxa = a.x - playerPosition.x;
                    float dza = a.z - playerPosition.z;
                    float dxb = b.x - playerPosition.x;
                    float dzb = b.z - playerPosition.z;

                    // Skip lines that are entirely outside the visible radius.
                    if (!IsWithinRadius(dxa, dza, Radius) &&
                        !IsWithinRadius(dxb, dzb, Radius))
                        continue;

                    // Rotate so the player's forward direction maps to minimap +Y (up).
                    float rax = cosYaw * dxa - sinYaw * dza;
                    float raz = sinYaw * dxa + cosYaw * dza;
                    float rbx = cosYaw * dxb - sinYaw * dzb;
                    float rbz = sinYaw * dxb + cosYaw * dzb;

                    // Map [-Radius, +Radius] → [0, 1].
                    lines.Add(new MinimapLine
                    {
                        Start    = new Vector2(0.5f + rax / diameter, 0.5f + raz / diameter),
                        End      = new Vector2(0.5f + rbx / diameter, 0.5f + rbz / diameter),
                        RoadType = ParseRoadType(segment.HighwayType),
                    });
                }
            }

            return lines;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool IsWithinRadius(float dx, float dz, float radius) =>
            dx * dx + dz * dz <= radius * radius;

        /// <summary>
        /// Converts an OSM <c>highway</c> tag value to the strongly-typed
        /// <see cref="RoadType"/> enum used for minimap colour selection.
        /// </summary>
        internal static RoadType ParseRoadType(string? highwayType) =>
            highwayType switch
            {
                "motorway"       => RoadType.Motorway,
                "motorway_link"  => RoadType.Motorway,
                "trunk"          => RoadType.Trunk,
                "trunk_link"     => RoadType.Trunk,
                "primary"        => RoadType.Primary,
                "primary_link"   => RoadType.Primary,
                "secondary"      => RoadType.Secondary,
                "secondary_link" => RoadType.Secondary,
                "tertiary"       => RoadType.Tertiary,
                "tertiary_link"  => RoadType.Tertiary,
                "residential"    => RoadType.Residential,
                "living_street"  => RoadType.Residential,
                "service"        => RoadType.Service,
                "track"          => RoadType.Dirt,
                "footway"        => RoadType.Path,
                "path"           => RoadType.Path,
                "cycleway"       => RoadType.Cycleway,
                _                => RoadType.Unknown,
            };
    }

    /// <summary>A single projected road line on the minimap in normalised [0, 1] coordinates.</summary>
    public sealed class MinimapLine
    {
        /// <summary>Start point in normalised minimap space [0, 1].</summary>
        public Vector2 Start { get; set; }

        /// <summary>End point in normalised minimap space [0, 1].</summary>
        public Vector2 End { get; set; }

        /// <summary>Functional road type — used to select the line colour when rendering.</summary>
        public RoadType RoadType { get; set; }
    }
}
