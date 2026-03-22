using UnityEngine;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Describes the world-space position, orientation, and type of a single roadside prop.
    /// </summary>
    public struct PropPlacement
    {
        /// <summary>World-space base position of the prop.</summary>
        public Vector3 Position;

        /// <summary>
        /// Road tangent direction at the placement point.
        /// The prop faces along the road (i.e. towards <c>Position + Forward</c>).
        /// </summary>
        public Vector3 Forward;

        /// <summary>Kind of prop to place at this location.</summary>
        public PropType Type;

        /// <summary>Cumulative arc-length distance from the road start (metres).</summary>
        public float DistanceAlong;

        /// <summary>
        /// <c>true</c> if the prop is on the left side of the road (looking in the
        /// <see cref="Forward"/> direction); <c>false</c> if it is on the right side.
        /// </summary>
        public bool IsLeftSide;
    }
}
