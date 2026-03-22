using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VectorRoad.Vehicle;

namespace VectorRoad.Tests.PlayMode
{
    /// <summary>
    /// Play-mode tests for <see cref="CarController"/> MonoBehaviour lifecycle.
    /// Input reading is exercised via the legacy Input system (active in Both mode).
    /// </summary>
    public class CarControllerPlayModeTests
    {
        private GameObject _go;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null)
            {
                Object.Destroy(_go);
                yield return null;
            }
        }

        // ── Component lifecycle ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Awake_WithRigidbody_InitializesWithoutException()
        {
            _go = new GameObject("Car");
            _go.AddComponent<Rigidbody>();
            Assert.DoesNotThrow(() => _go.AddComponent<CarController>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedUpdate_WithoutWheelSetup_DoesNotThrow()
        {
            _go = new GameObject("Car");
            _go.AddComponent<Rigidbody>();
            _go.AddComponent<CarController>();

            // Several fixed-update cycles with no wheel colliders wired up should be silent.
            for (int i = 0; i < 5; i++)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Awake_WithoutRigidbody_DoesNotThrow()
        {
            _go = new GameObject("Car");
            Assert.DoesNotThrow(() => _go.AddComponent<CarController>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Update_WithoutWheelSetup_DoesNotThrow()
        {
            _go = new GameObject("Car");
            _go.AddComponent<Rigidbody>();
            _go.AddComponent<CarController>();

            for (int i = 0; i < 5; i++)
                yield return null;
        }

        // ── Inspector field defaults ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator DefaultMotorTorque_IsGreaterThanZero()
        {
            _go = new GameObject("Car");
            _go.AddComponent<Rigidbody>();
            var ctrl = _go.AddComponent<CarController>();
            yield return null;

            Assert.That(ctrl.motorTorque, Is.GreaterThan(0f),
                "Default motorTorque should be positive.");
        }

        [UnityTest]
        public IEnumerator DefaultBrakeTorque_IsGreaterThanZero()
        {
            _go = new GameObject("Car");
            _go.AddComponent<Rigidbody>();
            var ctrl = _go.AddComponent<CarController>();
            yield return null;

            Assert.That(ctrl.brakeTorque, Is.GreaterThan(0f),
                "Default brakeTorque should be positive.");
        }

        [UnityTest]
        public IEnumerator DefaultMaxSteerAngle_IsGreaterThanZero()
        {
            _go = new GameObject("Car");
            _go.AddComponent<Rigidbody>();
            var ctrl = _go.AddComponent<CarController>();
            yield return null;

            Assert.That(ctrl.maxSteerAngle, Is.GreaterThan(0f),
                "Default maxSteerAngle should be positive.");
        }
    }
}
