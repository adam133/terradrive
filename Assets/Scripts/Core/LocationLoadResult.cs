using UnityEngine;

namespace TerraDrive.Core
{
    /// <summary>
    /// Result returned by <see cref="LocationMenuController.LoadLocationAsync"/>.
    ///
    /// Contains the fully-loaded <see cref="MapData"/> for the chosen coordinates together
    /// with the player spawn position that the calling code should teleport the car to.
    /// </summary>
    public sealed class LocationLoadResult
    {
        /// <summary>
        /// All map data generated for the chosen location: roads, buildings, region type,
        /// terrain mesh, and the underlying elevation grid.
        /// </summary>
        public MapData MapData { get; }

        /// <summary>Origin latitude used to build this map (decimal degrees, WGS-84).</summary>
        public double OriginLatitude { get; }

        /// <summary>Origin longitude used to build this map (decimal degrees, WGS-84).</summary>
        public double OriginLongitude { get; }

        /// <summary>
        /// World-space position where the player car should be moved after the location
        /// change.  Always <see cref="Vector3.zero"/> because the chosen coordinate
        /// becomes the new world origin.
        /// </summary>
        public Vector3 PlayerSpawnPosition => Vector3.zero;

        /// <summary>Initialises a new <see cref="LocationLoadResult"/>.</summary>
        public LocationLoadResult(MapData mapData, double originLatitude, double originLongitude)
        {
            MapData         = mapData;
            OriginLatitude  = originLatitude;
            OriginLongitude = originLongitude;
        }
    }
}
