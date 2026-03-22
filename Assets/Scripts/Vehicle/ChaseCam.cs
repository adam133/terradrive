using UnityEngine;

namespace VectorRoad.Vehicle
{
    /// <summary>
    /// Smooth chase-camera controller that follows a target vehicle from behind and
    /// slightly above, with configurable lag for a cinematic feel.
    ///
    /// Setup:
    ///  1. Attach this script to the Camera GameObject (or a camera rig).
    ///  2. Assign the vehicle root <see cref="Transform"/> to <see cref="target"/>.
    ///  3. Tune <see cref="followDistance"/>, <see cref="height"/>,
    ///     <see cref="positionDamping"/>, and <see cref="rotationDamping"/> in the Inspector.
    /// </summary>
    public class ChaseCam : MonoBehaviour
    {
        // ── Inspector fields ───────────────────────────────────────────────────

        [Header("Target")]
        [Tooltip("The vehicle transform to follow.  Assign the car's root GameObject.")]
        public Transform target;

        [Header("Offset")]
        [Tooltip("Distance (world units) the camera maintains behind the target.")]
        public float followDistance = 8f;

        [Tooltip("Height (world units) of the camera above the target pivot.")]
        public float height = 3f;

        [Tooltip("How far ahead of the target the camera looks (world units).")]
        public float lookAheadDistance = 3f;

        [Header("Smoothing")]
        [Tooltip("Positional smoothing factor.  Higher values = snappier response.")]
        public float positionDamping = 5f;

        [Tooltip("Rotational smoothing factor.  Higher values = snappier response.")]
        public float rotationDamping = 5f;

        // ── Private state ──────────────────────────────────────────────────────

        private Vector3 _velocity;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (target == null) return;

            // Desired position: behind and above the target
            Vector3 desiredPosition = target.position
                - target.forward * followDistance
                + Vector3.up * height;

            // Smooth positional follow
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition,
                ref _velocity, 1f / positionDamping);

            // Look at a point slightly ahead of the target for more natural framing
            Vector3 lookAt = target.position + target.forward * lookAheadDistance;
            Vector3 lookDir = lookAt - transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, desiredRotation,
                    Time.deltaTime * rotationDamping);
            }
        }
    }
}
