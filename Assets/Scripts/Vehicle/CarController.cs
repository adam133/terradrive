using UnityEngine;

namespace TerraDrive.Vehicle
{
    /// <summary>
    /// Semi-realistic car controller using Unity's <see cref="WheelCollider"/> API.
    ///
    /// Features:
    ///  - Configurable motor torque and braking
    ///  - Adjustable steering angle
    ///  - Drift friction model (reduced sideways stiffness when the handbrake is held)
    ///  - Anti-roll bar on both axles to prevent cornering flips
    ///
    /// Setup:
    ///  1. Create a vehicle root with a <see cref="Rigidbody"/> and four <see cref="WheelCollider"/> children.
    ///  2. Assign the colliders and their matching visual-mesh transforms in the inspector.
    ///  3. Attach this script to the vehicle root.
    /// </summary>
    public class CarController : MonoBehaviour
    {
        // ── Inspector fields ───────────────────────────────────────────────────

        [Header("Wheel Colliders")]
        public WheelCollider frontLeftCollider;
        public WheelCollider frontRightCollider;
        public WheelCollider rearLeftCollider;
        public WheelCollider rearRightCollider;

        [Header("Visual Wheel Transforms")]
        public Transform frontLeftMesh;
        public Transform frontRightMesh;
        public Transform rearLeftMesh;
        public Transform rearRightMesh;

        [Header("Drive")]
        [Tooltip("Peak motor torque applied to the rear wheels (Nm).")]
        public float motorTorque = 1500f;

        [Tooltip("Torque applied when the brake input is active (Nm).")]
        public float brakeTorque = 3000f;

        [Tooltip("Maximum front-wheel steering angle (degrees).")]
        public float maxSteerAngle = 30f;

        [Header("Friction")]
        [Tooltip("Sideways WheelFrictionCurve stiffness during normal driving.")]
        public float normalFriction = 1.2f;

        [Tooltip("Sideways WheelFrictionCurve stiffness when drifting (handbrake / Space).")]
        public float driftFriction = 0.4f;

        [Header("Stability")]
        [Tooltip("Anti-roll bar spring stiffness.  Higher values prevent cornering rollovers.")]
        public float antiRollStiffness = 5000f;

        // ── Private state ──────────────────────────────────────────────────────

        private Rigidbody _rb;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            // Lower centre of mass for stability.
            if (_rb != null)
                _rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        }

        private void FixedUpdate()
        {
            if (!HasWheelSetup())
                return;

            float throttle = Input.GetAxis("Vertical");
            float steer = Input.GetAxis("Horizontal");
            bool handbrake = Input.GetKey(KeyCode.Space);

            ApplySteering(steer);
            ApplyMotorAndBrake(throttle, handbrake);
            ApplyAntiRoll(frontLeftCollider, frontRightCollider);
            ApplyAntiRoll(rearLeftCollider, rearRightCollider);
            SetDriftFriction(rearLeftCollider, handbrake);
            SetDriftFriction(rearRightCollider, handbrake);
        }

        private void Update()
        {
            if (!HasWheelSetup())
                return;

            UpdateWheelMesh(frontLeftCollider, frontLeftMesh);
            UpdateWheelMesh(frontRightCollider, frontRightMesh);
            UpdateWheelMesh(rearLeftCollider, rearLeftMesh);
            UpdateWheelMesh(rearRightCollider, rearRightMesh);
        }

        private bool HasWheelSetup()
        {
            return _rb != null
                && frontLeftCollider != null
                && frontRightCollider != null
                && rearLeftCollider != null
                && rearRightCollider != null;
        }

        // ── Drive helpers ──────────────────────────────────────────────────────

        private void ApplySteering(float steer)
        {
            float angle = steer * maxSteerAngle;
            frontLeftCollider.steerAngle = angle;
            frontRightCollider.steerAngle = angle;
        }

        private void ApplyMotorAndBrake(float throttle, bool handbrake)
        {
            if (handbrake)
            {
                // Handbrake: release motor torque, lock rear wheels.
                rearLeftCollider.motorTorque = 0f;
                rearRightCollider.motorTorque = 0f;
                rearLeftCollider.brakeTorque = brakeTorque;
                rearRightCollider.brakeTorque = brakeTorque;
                frontLeftCollider.brakeTorque = 0f;
                frontRightCollider.brakeTorque = 0f;
            }
            else if (throttle < 0f)
            {
                // Braking
                float bt = Mathf.Abs(throttle) * brakeTorque;
                SetBrakeOnAll(bt);
                SetMotorOnRear(0f);
            }
            else
            {
                // Driving
                SetBrakeOnAll(0f);
                SetMotorOnRear(throttle * motorTorque);
            }
        }

        private void SetMotorOnRear(float torque)
        {
            rearLeftCollider.motorTorque = torque;
            rearRightCollider.motorTorque = torque;
        }

        private void SetBrakeOnAll(float torque)
        {
            frontLeftCollider.brakeTorque = torque;
            frontRightCollider.brakeTorque = torque;
            rearLeftCollider.brakeTorque = torque;
            rearRightCollider.brakeTorque = torque;
        }

        // ── Friction / drift ───────────────────────────────────────────────────

        private void SetDriftFriction(WheelCollider wheel, bool drifting)
        {
            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.stiffness = drifting ? driftFriction : normalFriction;
            wheel.sidewaysFriction = sideways;
        }

        // ── Anti-roll bar ──────────────────────────────────────────────────────

        /// <summary>
        /// Applies an anti-roll bar force between the two wheels of an axle to counteract
        /// body lean during cornering.
        /// </summary>
        private void ApplyAntiRoll(WheelCollider leftWheel, WheelCollider rightWheel)
        {
            leftWheel.GetGroundHit(out WheelHit leftHit);
            rightWheel.GetGroundHit(out WheelHit rightHit);

            bool leftGrounded = leftWheel.isGrounded;
            bool rightGrounded = rightWheel.isGrounded;

            float leftTravel = leftGrounded
                ? (-leftWheel.transform.InverseTransformPoint(leftHit.point).y - leftWheel.radius) / leftWheel.suspensionDistance
                : 1f;

            float rightTravel = rightGrounded
                ? (-rightWheel.transform.InverseTransformPoint(rightHit.point).y - rightWheel.radius) / rightWheel.suspensionDistance
                : 1f;

            float antiRollForce = (leftTravel - rightTravel) * antiRollStiffness;

            if (leftGrounded)
                _rb.AddForceAtPosition(leftWheel.transform.up * -antiRollForce, leftWheel.transform.position);
            if (rightGrounded)
                _rb.AddForceAtPosition(rightWheel.transform.up * antiRollForce, rightWheel.transform.position);
        }

        // ── Visual wheel sync ──────────────────────────────────────────────────

        private static void UpdateWheelMesh(WheelCollider collider, Transform meshTransform)
        {
            if (meshTransform == null) return;
            collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            meshTransform.SetPositionAndRotation(pos, rot);
        }
    }
}
