using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using VectorRoad.Core;
using VectorRoad.Terrain;

namespace VectorRoad.DataInversion
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

        /// <summary>
        /// <c>true</c> when the OSM way carries a <c>bridge</c> tag with a value other than
        /// <c>"no"</c> (e.g. <c>"yes"</c>, <c>"viaduct"</c>), indicating the road is elevated
        /// on a bridge structure and should be raised above the surface mesh.
        /// </summary>
        public bool IsBridge { get; set; }

        /// <summary>
        /// Number of lanes from the OSM <c>lanes</c> tag.  Zero means the tag was absent
        /// or could not be parsed; callers should fall back to road-type defaults.
        /// </summary>
        public int Lanes { get; set; }

        /// <summary>
        /// <c>true</c> when the OSM <c>oneway</c> tag is set to <c>"yes"</c>, <c>"1"</c>,
        /// or <c>"true"</c>, indicating traffic flows in one direction only.
        /// </summary>
        public bool IsOneWay { get; set; }
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
    ///   var (roads, buildings, region) = OSMParser.Parse("Assets/Data/london.osm", originLat, originLon);
    /// </code>
    /// </summary>
    public static class OSMParser
    {
        /// <summary>
        /// Parses an <c>.osm</c> file and returns all highway and building ways together
        /// with the detected <see cref="RegionType"/> inferred from <c>country</c> or
        /// <c>addr:country</c> tags on OSM nodes.
        /// </summary>
        /// <param name="filePath">Absolute or project-relative path to the <c>.osm</c> file.</param>
        /// <param name="originLat">Map origin latitude — maps to world (0, 0, 0).</param>
        /// <param name="originLon">Map origin longitude — maps to world (0, 0, 0).</param>
        /// <returns>
        /// A tuple containing a list of <see cref="RoadSegment"/>s, a list of
        /// <see cref="BuildingFootprint"/>s, a list of <see cref="WaterBody"/>s, and a
        /// <see cref="RegionType"/> derived from the most common country code found on
        /// nodes in the file.
        /// </returns>
        public static (List<RoadSegment> roads, List<BuildingFootprint> buildings, List<WaterBody> waterBodies, RegionType region)
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

            var roads       = new List<RoadSegment>();
            var buildings   = new List<BuildingFootprint>();
            var waterBodies = new List<WaterBody>();

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
                        WayId       = wayId,
                        HighwayType = highwayType,
                        Nodes       = nodeRefs,
                        Tags        = tags,
                        IsBridge    = IsBridgeWay(tags),
                        Lanes       = ParseLanes(tags),
                        IsOneWay    = IsOneWayRoad(tags),
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
                else if (nodeRefs.Count >= 3 && TryGetWaterType(tags, out string waterType))
                {
                    waterBodies.Add(new WaterBody
                    {
                        WayId     = wayId,
                        Outline   = nodeRefs,
                        Tags      = tags,
                        WaterType = waterType,
                    });
                }
            }

            // ── Detect region from node country tags ───────────────────────────
            RegionType region = DetectRegion(root);

            Debug.Log($"[OSMParser] Parsed {roads.Count} road segments, {buildings.Count} building footprints, " +
                      $"{waterBodies.Count} water bodies, and region '{region}' from '{filePath}'.");
            return (roads, buildings, waterBodies, region);
        }

        /// <summary>
        /// Parses an <c>.osm</c> file, samples terrain elevation for every node via
        /// <paramref name="elevationSource"/>, and returns all highway and building ways
        /// together with the detected <see cref="RegionType"/>.
        ///
        /// Each <see cref="RoadSegment"/> node and each <see cref="BuildingFootprint"/>
        /// corner will have its Unity Y coordinate set to the elevation (in metres above
        /// sea level) returned by the elevation source for that node's latitude/longitude.
        /// </summary>
        /// <param name="filePath">Absolute or project-relative path to the <c>.osm</c> file.</param>
        /// <param name="originLat">Map origin latitude — maps to world (0, 0, 0).</param>
        /// <param name="originLon">Map origin longitude — maps to world (0, 0, 0).</param>
        /// <param name="elevationSource">
        /// DEM data source used to fetch terrain elevation values for each OSM node.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A tuple containing a list of <see cref="RoadSegment"/>s, a list of
        /// <see cref="BuildingFootprint"/>s, a list of <see cref="WaterBody"/>s, and a
        /// <see cref="RegionType"/> derived from the most common country code found on
        /// nodes in the file.
        /// </returns>
        public static async Task<(List<RoadSegment> roads, List<BuildingFootprint> buildings, List<WaterBody> waterBodies, RegionType region)>
            ParseAsync(
                string filePath,
                double originLat,
                double originLon,
                IElevationSource elevationSource,
                CancellationToken cancellationToken = default)
        {
            if (elevationSource == null) throw new ArgumentNullException(nameof(elevationSource));

            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root ?? throw new InvalidOperationException("Empty OSM document.");

            // ── Collect all node lat/lon values (preserving insertion order) ──────
            var nodeLatLons  = new Dictionary<long, (double lat, double lon)>();
            foreach (XElement node in root.Elements("node"))
            {
                long   id  = (long)node.Attribute("id");
                double lat = (double)node.Attribute("lat");
                double lon = (double)node.Attribute("lon");
                nodeLatLons[id] = (lat, lon);
            }

            // ── Batch-fetch elevations for all nodes ──────────────────────────────
            var nodeIds  = new List<long>(nodeLatLons.Count);
            var locations = new List<(double lat, double lon)>(nodeLatLons.Count);
            foreach (var kv in nodeLatLons)
            {
                nodeIds.Add(kv.Key);
                locations.Add(kv.Value);
            }

            IReadOnlyList<double> elevations =
                await elevationSource.FetchElevationsAsync(locations, cancellationToken)
                                     .ConfigureAwait(false);

            // ── Build node id → world position lookup (with elevation) ─────────────
            var nodePositions = new Dictionary<long, Vector3>(nodeIds.Count);
            for (int i = 0; i < nodeIds.Count; i++)
            {
                (double lat, double lon) = locations[i];
                double elev = elevations[i];
                nodePositions[nodeIds[i]] =
                    CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon, elev);
            }

            var roads       = new List<RoadSegment>();
            var buildings   = new List<BuildingFootprint>();
            var waterBodies = new List<WaterBody>();

            // ── Process ways ───────────────────────────────────────────────────────
            foreach (XElement way in root.Elements("way"))
            {
                long wayId    = (long)way.Attribute("id");
                var  tags     = ParseTags(way);
                var  nodeRefs = BuildNodeList(way, nodePositions);

                if (tags.TryGetValue("highway", out string highwayType))
                {
                    roads.Add(new RoadSegment
                    {
                        WayId       = wayId,
                        HighwayType = highwayType,
                        Nodes       = nodeRefs,
                        Tags        = tags,
                        IsBridge    = IsBridgeWay(tags),
                        Lanes       = ParseLanes(tags),
                        IsOneWay    = IsOneWayRoad(tags),
                    });
                }
                else if (tags.ContainsKey("building"))
                {
                    buildings.Add(new BuildingFootprint
                    {
                        WayId     = wayId,
                        Footprint = nodeRefs,
                        Tags      = tags,
                    });
                }
                else if (nodeRefs.Count >= 3 && TryGetWaterType(tags, out string waterType))
                {
                    waterBodies.Add(new WaterBody
                    {
                        WayId     = wayId,
                        Outline   = nodeRefs,
                        Tags      = tags,
                        WaterType = waterType,
                    });
                }
            }

            // ── Detect region from node country tags ───────────────────────────────
            RegionType region = DetectRegion(root);

            Debug.Log($"[OSMParser] Parsed {roads.Count} road segments, {buildings.Count} building footprints, " +
                      $"{waterBodies.Count} water bodies, and region '{region}' from '{filePath}' (with elevation).");
            return (roads, buildings, waterBodies, region);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Scans all <c>&lt;node&gt;</c> elements in the document for <c>country</c> or
        /// <c>addr:country</c> tags, tallies the votes, and maps the most common ISO
        /// country code to a <see cref="RegionType"/>.
        /// </summary>
        private static RegionType DetectRegion(XElement root)
        {
            var votes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement node in root.Elements("node"))
            {
                foreach (XElement tag in node.Elements("tag"))
                {
                    string k = (string)tag.Attribute("k") ?? string.Empty;
                    if (k != "country" && k != "addr:country")
                        continue;

                    string v = ((string)tag.Attribute("v") ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(v))
                        continue;

                    votes.TryGetValue(v, out int count);
                    votes[v] = count + 1;
                }
            }

            if (votes.Count == 0)
                return RegionType.Unknown;

            string winner = string.Empty;
            int max = 0;
            foreach (var kv in votes)
            {
                if (kv.Value > max)
                {
                    max = kv.Value;
                    winner = kv.Key;
                }
            }

            return CountryCodeToRegion(winner);
        }

        /// <summary>
        /// Maps an ISO 3166-1 alpha-2 country code to a <see cref="RegionType"/>.
        /// Unknown or unmapped codes return <see cref="RegionType.Unknown"/>.
        /// </summary>
        private static RegionType CountryCodeToRegion(string countryCode)
        {
            return countryCode.ToUpperInvariant() switch
            {
                // ── Temperate ──────────────────────────────────────────────────
                "GB" or "IE" or "DE" or "FR" or "NL" or "BE" or "LU" or
                "AT" or "CH" or "PL" or "CZ" or "SK" or "HU" or "RO" or
                "BG" or "SI" or "RS" or "BA" or "ME" or "MK" or "AL" or
                "LT" or "LV" or "EE" or "JP" or "KR" or
                "NZ" or "CN" or "AR" or "CL"
                    => RegionType.Temperate,

                // ── Temperate North America ────────────────────────────────────
                "US" or "CA"
                    => RegionType.TemperateNorthAmerica,

                // ── Desert ─────────────────────────────────────────────────────
                "SA" or "AE" or "QA" or "KW" or "OM" or "BH" or "YE" or
                "IQ" or "IR" or "EG" or "LY" or "DZ" or "MA" or "MR" or
                "ML" or "NE" or "TD" or "SD" or "ER" or "DJ" or "SO" or
                "AF" or "PK" or "AU" or "NA" or "BW" or "PS"
                    => RegionType.Desert,

                // ── Tropical ───────────────────────────────────────────────────
                "BR" or "CO" or "VE" or "GY" or "SR" or "EC" or "PE" or
                "BO" or "NG" or "CI" or "GH" or "CM" or "CF" or "CG" or
                "CD" or "GA" or "GQ" or "SS" or "UG" or "RW" or "BI" or
                "TZ" or "KE" or "ET" or "TH" or "MY" or "ID" or "PH" or
                "SG" or "MM" or "KH" or "LA" or "VN" or "BD" or "LK" or
                "HN" or "GT" or "NI" or "CR" or "PA" or "MX" or "CU" or
                "HT" or "DO" or "JM" or "TT" or "FJ" or "PG" or "SB"
                    => RegionType.Tropical,

                // ── Boreal ─────────────────────────────────────────────────────
                "NO" or "SE" or "FI" or "RU"
                    => RegionType.Boreal,

                // ── Arctic ─────────────────────────────────────────────────────
                "GL" or "SJ" or "IS"
                    => RegionType.Arctic,

                // ── Mediterranean ──────────────────────────────────────────────
                "ES" or "PT" or "IT" or "GR" or "CY" or "MT" or "IL" or "LB"
                    => RegionType.Mediterranean,

                // ── Steppe ─────────────────────────────────────────────────────
                "KZ" or "MN" or "UA" or "KG" or "TJ" or "TM" or "UZ" or
                "AZ" or "GE"
                    => RegionType.Steppe,

                // ── Fallback ───────────────────────────────────────────────────
                _ => RegionType.Unknown,
            };
        }

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

        /// <summary>
        /// Returns <c>true</c> when <paramref name="tags"/> contains a <c>bridge</c> key with a
        /// value other than <c>"no"</c> (e.g. <c>"yes"</c>, <c>"viaduct"</c>).
        /// </summary>
        private static bool IsBridgeWay(Dictionary<string, string> tags) =>
            tags.TryGetValue("bridge", out string bridgeValue) &&
            !string.Equals(bridgeValue, "no", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Parses the OSM <c>lanes</c> tag into a positive integer.
        /// Returns 0 when the tag is absent or cannot be parsed.
        /// </summary>
        private static int ParseLanes(Dictionary<string, string> tags) =>
            tags.TryGetValue("lanes", out string lanesStr) &&
            int.TryParse(lanesStr, out int lanes) && lanes > 0
                ? lanes
                : 0;

        /// <summary>
        /// Returns <c>true</c> when the OSM <c>oneway</c> tag explicitly indicates a
        /// one-way road (<c>"yes"</c>, <c>"1"</c>, or <c>"true"</c>).
        /// </summary>
        private static bool IsOneWayRoad(Dictionary<string, string> tags) =>
            tags.TryGetValue("oneway", out string oneway) &&
            (string.Equals(oneway, "yes",  StringComparison.OrdinalIgnoreCase) ||
             string.Equals(oneway, "1",    StringComparison.Ordinal) ||
             string.Equals(oneway, "true", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns <c>true</c> when <paramref name="tags"/> describe a closed-polygon
        /// water feature, and sets <paramref name="waterType"/> to the detected sub-type.
        ///
        /// <para>Recognised tag combinations:</para>
        /// <list type="bullet">
        ///   <item><c>natural=water</c> — type taken from the <c>water</c> sub-tag (e.g.
        ///     <c>"lake"</c>, <c>"pond"</c>); falls back to <c>"water"</c>.</item>
        ///   <item><c>waterway=riverbank</c> or <c>waterway=dock</c> — polygon waterways.</item>
        ///   <item><c>landuse=reservoir</c> — artificial water storage.</item>
        /// </list>
        /// </summary>
        private static bool TryGetWaterType(Dictionary<string, string> tags, out string waterType)
        {
            if (tags.TryGetValue("natural", out string naturalVal) &&
                string.Equals(naturalVal, "water", StringComparison.OrdinalIgnoreCase))
            {
                // Use the optional water=* sub-tag for a more specific type.
                if (!tags.TryGetValue("water", out waterType) || string.IsNullOrEmpty(waterType))
                    waterType = "water";
                return true;
            }

            if (tags.TryGetValue("waterway", out string waterwayVal) &&
                (string.Equals(waterwayVal, "riverbank", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(waterwayVal, "dock",      StringComparison.OrdinalIgnoreCase)))
            {
                waterType = string.Equals(waterwayVal, "riverbank", StringComparison.OrdinalIgnoreCase)
                    ? "riverbank" : "dock";
                return true;
            }

            if (tags.TryGetValue("landuse", out string landuseVal) &&
                string.Equals(landuseVal, "reservoir", StringComparison.OrdinalIgnoreCase))
            {
                waterType = "reservoir";
                return true;
            }

            waterType = string.Empty;
            return false;
        }
    }
}
