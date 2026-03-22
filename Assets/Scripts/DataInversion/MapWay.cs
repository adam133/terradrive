using System.Collections.Generic;

namespace VectorRoad.DataInversion
{
    /// <summary>
    /// Represents a single OpenStreetMap way, holding raw geographic nodes and all OSM metadata.
    /// </summary>
    public class MapWay
    {
        /// <summary>OSM way identifier.</summary>
        public long Id { get; set; }

        /// <summary>
        /// Ordered list of <see cref="MapNode"/>s that make up this way, preserving the
        /// original OSM node sequence.
        /// </summary>
        public List<MapNode> Nodes { get; set; } = new List<MapNode>();

        /// <summary>All OSM tags on this way (key → value).</summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Functional road classification derived from the OSM <c>highway</c> tag.
        /// Defaults to <see cref="RoadType.Unknown"/> when the tag is absent or unrecognised.
        /// </summary>
        public RoadType RoadType { get; set; } = RoadType.Unknown;
    }
}
