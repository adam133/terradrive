using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TerraDrive.Vehicle;

namespace TerraDrive.Tests.PlayMode
{
    /// <summary>
    /// Play-mode tests for <see cref="ChaseCam"/>.
    ///
    /// <see cref="ChaseCam.LateUpdate"/> uses <see cref="Vector3.SmoothDamp"/> and
    /// <see cref="Quaternion.Slerp"/> which depend on <see cref="Time.deltaTime"/>
    /// and the full Transform hierarchy — functionality that requires the engine
    /// to be in Play mode.
    /// </summary>
    public class ChaseCamPlayModeTests
    {
        private GameObject _cameraGo;
        private GameObject _targetGo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_cameraGo != null) Object.Destroy(_cameraGo);
            if (_targetGo != null) Object.Destroy(_targetGo);
            yield return null;
        }

        // ── No target ──────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WithNoTarget_CameraPositionDoesNotChange()
        {
            _cameraGo = new GameObject("Camera");
            _cameraGo.AddComponent<ChaseCam>(); // target is null by default

            Vector3 initialPosition = _cameraGo.transform.position;

            yield return null; // LateUpdate runs

            Assert.That(_cameraGo.transform.position, Is.EqualTo(initialPosition),
                "Camera must not move when no target is assigned.");
        }

        // ── Camera follows target ──────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WithTarget_CameraMovesTowardsDesiredPosition()
        {
            _targetGo = new GameObject("Target");
            _targetGo.transform.position = new Vector3(0f, 0f, 20f);

            _cameraGo = new GameObject("Camera");
            var cam = _cameraGo.AddComponent<ChaseCam>();
            cam.target           = _targetGo.transform;
            cam.followDistance   = 8f;
            cam.height           = 3f;
            cam.positionDamping  = 50f; // high value for fast convergence in tests

            // Desired camera position = behind and above the target
            Vector3 desired = _targetGo.transform.position
                - _targetGo.transform.forward * cam.followDistance
                + Vector3.up * cam.height;

            // Advance several frames to allow SmoothDamp to converge
            for (int i = 0; i < 30; i++)
                yield return null;

            float distanceToDesired = Vector3.Distance(
                _cameraGo.transform.position, desired);

            Assert.That(distanceToDesired, Is.LessThan(0.5f),
                $"Camera should be within 0.5 units of the desired follow position; " +
                $"actual distance = {distanceToDesired:F3}");
        }

        [UnityTest]
        public IEnumerator WithTarget_CameraLooksAtPointAheadOfTarget()
        {
            _targetGo = new GameObject("Target");
            _targetGo.transform.position = new Vector3(0f, 0f, 20f);

            _cameraGo = new GameObject("Camera");
            _cameraGo.transform.position = new Vector3(0f, 3f, 12f);
            var cam = _cameraGo.AddComponent<ChaseCam>();
            cam.target            = _targetGo.transform;
            cam.lookAheadDistance = 3f;
            cam.rotationDamping   = 100f; // fast convergence

            // The look-at point is ahead of the target on the +Z axis
            Vector3 lookAt = _targetGo.transform.position
                + _targetGo.transform.forward * cam.lookAheadDistance;

            for (int i = 0; i < 30; i++)
                yield return null;

            // After convergence the camera forward direction should point roughly
            // towards the look-at point
            Vector3 toTarget = (lookAt - _cameraGo.transform.position).normalized;
            float dot = Vector3.Dot(_cameraGo.transform.forward, toTarget);

            Assert.That(dot, Is.GreaterThan(0.9f),
                "Camera forward should align closely with the look-at direction " +
                $"(dot product = {dot:F3}).");
        }
    }
}
