using UnityEngine;

namespace TerraDrive.Vehicle
{
    /// <summary>
    /// HUD component that reads the vehicle <see cref="Rigidbody"/> speed and exposes it
    /// in miles per hour.  Attach to the same <c>GameObject</c> as
    /// <see cref="CarController"/>.
    ///
    /// <para>
    /// Connect <see cref="SpeedMph"/> or <see cref="RawSpeedMph"/> to a UI Text element
    /// (e.g. via a <c>TextMeshPro</c> component) in a separate HUD script or via
    /// Unity's <c>OnGUI</c> callback.
    /// </para>
    /// </summary>
    public class SpeedometerHud : MonoBehaviour
    {
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>Current vehicle speed in miles per hour (unrounded).</summary>
        public float RawSpeedMph =>
            _rb != null ? Speedometer.ToMph(_rb.linearVelocity.magnitude) : 0f;

        /// <summary>Current vehicle speed in MPH rounded to the nearest integer.</summary>
        public int SpeedMph => Mathf.RoundToInt(RawSpeedMph);
    }
}
