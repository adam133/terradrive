using NUnit.Framework;
using UnityEngine;
using VectorRoad.Core;

namespace VectorRoad.Tests.EditMode
{
    /// <summary>
    /// Edit-mode tests for <see cref="GameManager"/>.
    ///
    /// These tests run entirely inside the Unity Editor without entering Play mode,
    /// so MonoBehaviour lifecycle methods (Awake / Start) are not invoked
    /// automatically. They focus on pure logic that can be exercised synchronously:
    /// inspector-facing setter methods and the <see cref="GameState"/> enum.
    /// </summary>
    public class GameManagerEditModeTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void SetLocation_UpdatesOriginLatitudeAndLongitude()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            gm.SetLocation(51.5074, -0.1278);

            Assert.That(gm.OriginLatitude,  Is.EqualTo(51.5074).Within(1e-6));
            Assert.That(gm.OriginLongitude, Is.EqualTo(-0.1278).Within(1e-6));
        }

        [Test]
        public void SetLocation_OverwritesPreviousCoordinates()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            gm.SetLocation(40.7128, -74.0060);
            gm.SetLocation(48.8566,   2.3522);

            Assert.That(gm.OriginLatitude,  Is.EqualTo(48.8566).Within(1e-6));
            Assert.That(gm.OriginLongitude, Is.EqualTo(2.3522).Within(1e-6));
        }

        [Test]
        public void GameState_EnumValues_AreAllDistinct()
        {
            var values = (GameState[])System.Enum.GetValues(typeof(GameState));
            var unique = new System.Collections.Generic.HashSet<int>();
            foreach (var state in values)
                unique.Add((int)state);

            Assert.That(unique.Count, Is.EqualTo(values.Length),
                "Every GameState value should map to a unique integer.");
        }

        [Test]
        public void GameState_EnumContains_ExpectedStates()
        {
            Assert.That(System.Enum.IsDefined(typeof(GameState), GameState.MainMenu),     Is.True);
            Assert.That(System.Enum.IsDefined(typeof(GameState), GameState.LoadingMap),   Is.True);
            Assert.That(System.Enum.IsDefined(typeof(GameState), GameState.GeneratingLevel), Is.True);
            Assert.That(System.Enum.IsDefined(typeof(GameState), GameState.Racing),       Is.True);
            Assert.That(System.Enum.IsDefined(typeof(GameState), GameState.Paused),       Is.True);
            Assert.That(System.Enum.IsDefined(typeof(GameState), GameState.Results),      Is.True);
        }
    }
}
