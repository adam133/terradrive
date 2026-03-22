using TMPro;
using UnityEngine;

namespace VectorRoad.Vehicle
{
    /// <summary>
    /// HUD component that reads the vehicle <see cref="Rigidbody"/> speed and drives a
    /// <see cref="TMP_Text"/> label with the current speed in mph.
    /// Attach to the same <c>GameObject</c> as <see cref="CarController"/>, then
    /// assign <c>SpeedLabel</c> in the Inspector.
    /// </summary>
    public class SpeedometerHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text _speedLabel;

        private Rigidbody _rb;

        /// <summary>Assigns the speed label at runtime (called by <see cref="VectorRoad.Core.MapSceneBuilder"/>).</summary>
        public void Init(TMP_Text label) => _speedLabel = label;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_speedLabel != null)
                _speedLabel.text = $"{SpeedMph} mph";
        }

        /// <summary>Current vehicle speed in miles per hour (unrounded).</summary>
        public float RawSpeedMph =>
            _rb != null ? Speedometer.ToMph(_rb.linearVelocity.magnitude) : 0f;

        /// <summary>Current vehicle speed in MPH rounded to the nearest integer.</summary>
        public int SpeedMph => Mathf.RoundToInt(RawSpeedMph);
    }
}
