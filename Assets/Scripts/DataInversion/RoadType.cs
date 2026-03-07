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
}
