using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VectorRoad.Vehicle;

namespace VectorRoad.Tests.PlayMode
{
    /// <summary>
    /// Play-mode tests for <see cref="SpeedometerHud"/>.
    ///
    /// <see cref="SpeedometerHud"/> reads the attached <see cref="Rigidbody"/>'s
    /// velocity in its <c>Awake</c> method, which only runs in Play mode.  These
    /// tests verify the speed-reading logic under different Rigidbody states.
    /// </summary>
    public class SpeedometerHudPlayModeTests
    {
        private GameObject _gameObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                yield return null;
            }
        }

        // ── No Rigidbody ───────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WithoutRigidbody_RawSpeedMph_IsZero()
        {
            _gameObject = new GameObject("SpeedometerHud");
            var hud = _gameObject.AddComponent<SpeedometerHud>();

            yield return null; // Awake runs, finds no Rigidbody

            Assert.That(hud.RawSpeedMph, Is.EqualTo(0f),
                "RawSpeedMph should be 0 when no Rigidbody is attached.");
        }

        [UnityTest]
        public IEnumerator WithoutRigidbody_SpeedMph_IsZero()
        {
            _gameObject = new GameObject("SpeedometerHud");
            var hud = _gameObject.AddComponent<SpeedometerHud>();

            yield return null;

            Assert.That(hud.SpeedMph, Is.EqualTo(0));
        }

        // ── Stationary Rigidbody ───────────────────────────────────────────────

        [UnityTest]
        public IEnumerator StationaryRigidbody_RawSpeedMph_IsZero()
        {
            _gameObject = new GameObject("Vehicle");
            _gameObject.AddComponent<Rigidbody>();
            var hud = _gameObject.AddComponent<SpeedometerHud>();

            yield return null; // Awake sets _rb, velocity is zero

            Assert.That(hud.RawSpeedMph, Is.EqualTo(0f).Within(0.001f));
            Assert.That(hud.SpeedMph,    Is.EqualTo(0));
        }

        // ── SpeedMph rounding ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SpeedMph_IsRoundedToNearestInteger()
        {
            _gameObject = new GameObject("Vehicle");
            var rb  = _gameObject.AddComponent<Rigidbody>();
            var hud = _gameObject.AddComponent<SpeedometerHud>();

            yield return null; // Awake wires up _rb

            // 10 m/s ≈ 22.3694 mph → rounds to 22
            rb.linearVelocity = new Vector3(0f, 0f, 10f);

            yield return new WaitForFixedUpdate();

            int expected = Mathf.RoundToInt(hud.RawSpeedMph);
            Assert.That(hud.SpeedMph, Is.EqualTo(expected));
        }
    }
}
