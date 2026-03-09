using System.Collections.Generic;
using UnityEngine;

namespace TerraDrive.DataInversion
{
    /// <summary>
    /// Represents a water body parsed from an OSM closed-way polygon (e.g. lake, pond,
    /// riverbank, reservoir).
    ///
    /// <para>
    /// OSM ways are classified as water bodies when they carry one of the following tags:
    /// <list type="bullet">
    ///   <item><c>natural=water</c> — lakes, ponds, and other standing water.</item>
    ///   <item><c>waterway=riverbank</c> — the polygon outline of a river channel.</item>
    ///   <item><c>waterway=dock</c> — docks and basins.</item>
    ///   <item><c>landuse=reservoir</c> — artificial water storage.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class WaterBody
    {
        /// <summary>OSM way identifier.</summary>
        public long WayId { get; set; }

        /// <summary>Ordered world-space XZ corner positions of the water polygon outline.</summary>
        public List<Vector3> Outline { get; set; } = new List<Vector3>();

        /// <summary>All OSM tags on this way.</summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Water sub-type derived from OSM tags (e.g. <c>"lake"</c>, <c>"pond"</c>,
        /// <c>"reservoir"</c>, <c>"riverbank"</c>).
        /// Defaults to <c>"water"</c> when no specific sub-type tag is present.
        /// </summary>
        public string WaterType { get; set; } = "water";
    }
}
