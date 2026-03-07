using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using TerraDrive.Core;

namespace TerraDrive.DataInversion
{
    /// <summary>
    /// Represents a single OSM road way with its projected world-space nodes.
    /// </summary>
    public class RoadSegment
    {
        /// <summary>OSM way identifier.</summary>
        public long WayId { get; set; }

        /// <summary>Value of the OSM <c>highway</c> tag (e.g. "primary", "residential").</summary>
        public string HighwayType { get; set; }

        /// <summary>World-space XZ positions (Y = 0) of the way's nodes in order.</summary>
        public List<Vector3> Nodes { get; set; } = new List<Vector3>();

        /// <summary>All OSM tags on this way.</summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Represents the footprint polygon of a single OSM building way.
    /// </summary>
    public class BuildingFootprint
    {
        /// <summary>OSM way identifier.</summary>
        public long WayId { get; set; }

        /// <summary>Ordered world-space XZ corner positions of the building outline.</summary>
        public List<Vector3> Footprint { get; set; } = new List<Vector3>();

        /// <summary>All OSM tags on this way (e.g. <c>building:levels</c>).</summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Parses a standard <c>.osm</c> XML file and converts its road and building ways into
    /// strongly-typed C# objects with world-space coordinates.
    ///
    /// Usage:
    /// <code>
    ///   var (roads, buildings) = OSMParser.Parse("Assets/Data/london.osm", originLat, originLon);
    /// </code>
    /// </summary>
    public static class OSMParser
    {
        /// <summary>
        /// Parses an <c>.osm</c> file and returns all highway and building ways.
        /// </summary>
        /// <param name="filePath">Absolute or project-relative path to the <c>.osm</c> file.</param>
        /// <param name="originLat">Map origin latitude — maps to world (0, 0, 0).</param>
        /// <param name="originLon">Map origin longitude — maps to world (0, 0, 0).</param>
        /// <returns>
        /// A tuple containing a list of <see cref="RoadSegment"/>s and a list of
        /// <see cref="BuildingFootprint"/>s.
        /// </returns>
        public static (List<RoadSegment> roads, List<BuildingFootprint> buildings)
            Parse(string filePath, double originLat, double originLon)
        {
            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root ?? throw new InvalidOperationException("Empty OSM document.");

            // ── Build node id → world position lookup ──────────────────────────
            var nodePositions = new Dictionary<long, Vector3>();
            foreach (XElement node in root.Elements("node"))
            {
                long id = (long)node.Attribute("id");
                double lat = (double)node.Attribute("lat");
                double lon = (double)node.Attribute("lon");
                nodePositions[id] = CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon);
            }

            var roads = new List<RoadSegment>();
            var buildings = new List<BuildingFootprint>();

            // ── Process ways ───────────────────────────────────────────────────
            foreach (XElement way in root.Elements("way"))
            {
                long wayId = (long)way.Attribute("id");
                var tags = ParseTags(way);
                var nodeRefs = BuildNodeList(way, nodePositions);

                if (tags.TryGetValue("highway", out string highwayType))
                {
                    roads.Add(new RoadSegment
                    {
                        WayId = wayId,
                        HighwayType = highwayType,
                        Nodes = nodeRefs,
                        Tags = tags,
                    });
                }
                else if (tags.ContainsKey("building"))
                {
                    buildings.Add(new BuildingFootprint
                    {
                        WayId = wayId,
                        Footprint = nodeRefs,
                        Tags = tags,
                    });
                }
            }

            Debug.Log($"[OSMParser] Parsed {roads.Count} road segments and {buildings.Count} building footprints from '{filePath}'.");
            return (roads, buildings);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static Dictionary<string, string> ParseTags(XElement way)
        {
            var tags = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement tag in way.Elements("tag"))
            {
                string k = (string)tag.Attribute("k");
                string v = (string)tag.Attribute("v");
                if (!string.IsNullOrEmpty(k))
                    tags[k] = v ?? string.Empty;
            }
            return tags;
        }

        private static List<Vector3> BuildNodeList(XElement way, Dictionary<long, Vector3> nodePositions)
        {
            var positions = new List<Vector3>();
            foreach (XElement nd in way.Elements("nd"))
            {
                long nodeRef = (long)nd.Attribute("ref");
                if (nodePositions.TryGetValue(nodeRef, out Vector3 pos))
                    positions.Add(pos);
                else
                    Debug.LogWarning($"[OSMParser] Node {nodeRef} referenced by way but not found in file.");
            }
            return positions;
        }
    }
}
