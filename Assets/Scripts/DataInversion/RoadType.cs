namespace TerraDrive.DataInversion
{
    /// <summary>
    /// Classifies an OSM way by its road surface or functional category.
    /// </summary>
    public enum RoadType
    {
        /// <summary>Road type is not recognised or not set.</summary>
        Unknown,

        /// <summary>Motorway or controlled-access expressway (OSM: <c>motorway</c>).</summary>
        Motorway,

        /// <summary>Trunk road — major road that is not a motorway (OSM: <c>trunk</c>).</summary>
        Trunk,

        /// <summary>Primary road (OSM: <c>primary</c>).</summary>
        Primary,

        /// <summary>Secondary road (OSM: <c>secondary</c>).</summary>
        Secondary,

        /// <summary>Tertiary road (OSM: <c>tertiary</c>).</summary>
        Tertiary,

        /// <summary>Residential street (OSM: <c>residential</c>).</summary>
        Residential,

        /// <summary>Service road or access track (OSM: <c>service</c>).</summary>
        Service,

        /// <summary>Unpaved dirt track or forest road (OSM: <c>track</c>).</summary>
        Dirt,

        /// <summary>Designated footpath or walking trail (OSM: <c>footway</c> / <c>path</c>).</summary>
        Path,

        /// <summary>Dedicated cycle lane (OSM: <c>cycleway</c>).</summary>
        Cycleway,
    }

    /// <summary>
    /// Maps OSM <c>highway</c> tag values to <see cref="RoadType"/> enum members.
    ///
    /// Used by <c>MapSceneBuilder</c> to convert the raw tag string stored on a
    /// <c>RoadSegment</c> into a typed enum before passing it to the mesh generators.
    /// </summary>
    public static class RoadTypeParser
    {
        /// <summary>
        /// Returns the <see cref="RoadType"/> corresponding to the OSM <c>highway</c>
        /// tag value <paramref name="highwayTag"/>.
        ///
        /// <para>
        /// Link variants (e.g. <c>motorway_link</c>, <c>primary_link</c>) map to the
        /// same type as their parent class.  Unrecognised values fall back to
        /// <see cref="RoadType.Residential"/>.
        /// </para>
        /// </summary>
        /// <param name="highwayTag">
        /// OSM <c>highway</c> tag value (e.g. <c>"primary"</c>, <c>"motorway_link"</c>).
        /// <c>null</c> is treated as unrecognised.
        /// </param>
        /// <returns>
        /// The matching <see cref="RoadType"/>, or <see cref="RoadType.Residential"/>
        /// when the value is not recognised.
        /// </returns>
        public static RoadType Parse(string? highwayTag) =>
            highwayTag?.ToLowerInvariant() switch
            {
                "motorway"   or "motorway_link"    => RoadType.Motorway,
                "trunk"      or "trunk_link"       => RoadType.Trunk,
                "primary"    or "primary_link"     => RoadType.Primary,
                "secondary"  or "secondary_link"   => RoadType.Secondary,
                "tertiary"   or "tertiary_link"    => RoadType.Tertiary,
                "residential" or "living_street"   => RoadType.Residential,
                "service"                          => RoadType.Service,
                "track"      or "dirt_road"        => RoadType.Dirt,
                "path"       or "footway" or "steps" => RoadType.Path,
                "cycleway"                         => RoadType.Cycleway,
                _                                  => RoadType.Residential,
            };
    }
}
