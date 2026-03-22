using NUnit.Framework;
using UnityEngine;
using VectorRoad.Vehicle;

namespace VectorRoad.Tests.EditMode
{
    /// <summary>
    /// Edit-mode tests for <see cref="ChaseCam"/>.
    ///
    /// Verifies that the public inspector-configurable fields have the expected
    /// default values. These are pure synchronous tests that do not require
    /// entering Play mode.
    /// </summary>
    public class ChaseCamEditModeTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ChaseCam_DefaultFollowDistance_IsEightUnits()
        {
            _gameObject = new GameObject("Camera");
            var cam = _gameObject.AddComponent<ChaseCam>();

            Assert.That(cam.followDistance, Is.EqualTo(8f).Within(1e-6f));
        }

        [Test]
        public void ChaseCam_DefaultHeight_IsThreeUnits()
        {
            _gameObject = new GameObject("Camera");
            var cam = _gameObject.AddComponent<ChaseCam>();

            Assert.That(cam.height, Is.EqualTo(3f).Within(1e-6f));
        }

        [Test]
        public void ChaseCam_DefaultLookAheadDistance_IsThreeUnits()
        {
            _gameObject = new GameObject("Camera");
            var cam = _gameObject.AddComponent<ChaseCam>();

            Assert.That(cam.lookAheadDistance, Is.EqualTo(3f).Within(1e-6f));
        }

        [Test]
        public void ChaseCam_DefaultPositionDamping_IsFive()
        {
            _gameObject = new GameObject("Camera");
            var cam = _gameObject.AddComponent<ChaseCam>();

            Assert.That(cam.positionDamping, Is.EqualTo(5f).Within(1e-6f));
        }

        [Test]
        public void ChaseCam_DefaultRotationDamping_IsFive()
        {
            _gameObject = new GameObject("Camera");
            var cam = _gameObject.AddComponent<ChaseCam>();

            Assert.That(cam.rotationDamping, Is.EqualTo(5f).Within(1e-6f));
        }

        [Test]
        public void ChaseCam_DefaultTarget_IsNull()
        {
            _gameObject = new GameObject("Camera");
            var cam = _gameObject.AddComponent<ChaseCam>();

            Assert.That(cam.target, Is.Null);
        }
    }
}
