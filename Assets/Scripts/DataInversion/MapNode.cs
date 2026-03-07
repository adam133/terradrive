using System;

namespace TerraDrive.DataInversion
{
    /// <summary>
    /// Represents a single OpenStreetMap node with geographic coordinates and elevation.
    /// </summary>
    public struct MapNode : IEquatable<MapNode>
    {
        /// <summary>OSM node identifier.</summary>
        public long Id { get; set; }

        /// <summary>WGS-84 latitude in decimal degrees.</summary>
        public double Lat { get; set; }

        /// <summary>WGS-84 longitude in decimal degrees.</summary>
        public double Lon { get; set; }

        /// <summary>Elevation above sea level in metres.</summary>
        public double Elevation { get; set; }

        /// <summary>
        /// Initialises a new <see cref="MapNode"/> with all four fields.
        /// </summary>
        public MapNode(long id, double lat, double lon, double elevation = 0.0)
        {
            Id = id;
            Lat = lat;
            Lon = lon;
            Elevation = elevation;
        }

        /// <inheritdoc/>
        public bool Equals(MapNode other) =>
            Id == other.Id &&
            Lat == other.Lat &&
            Lon == other.Lon &&
            Elevation == other.Elevation;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is MapNode n && Equals(n);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Id, Lat, Lon, Elevation);

        /// <inheritdoc/>
        public override string ToString() =>
            $"MapNode(Id={Id}, Lat={Lat}, Lon={Lon}, Elevation={Elevation})";
    }
}
