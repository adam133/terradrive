using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TerraDrive.Core;

namespace TerraDrive.Tests.PlayMode
{
    /// <summary>
    /// Play-mode tests for <see cref="GameManager"/>.
    ///
    /// These tests enter Play mode so that Unity's MonoBehaviour lifecycle
    /// (Awake, etc.) fires normally, allowing the singleton setup, state machine,
    /// and UnityEvent callbacks to be exercised end-to-end.
    /// </summary>
    public class GameManagerPlayModeTests
    {
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            // Clear any stale singleton reference left by a previous test so
            // the next AddComponent<GameManager>() can initialise cleanly.
            ClearSingleton();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_gameObject != null)
            {
                Object.Destroy(_gameObject);
                yield return null; // allow Destroy to complete
            }

            ClearSingleton();
        }

        // ── Singleton ──────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Awake_SetsSingletonInstance()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null; // let Awake run

            Assert.That(GameManager.Instance, Is.SameAs(gm));
        }

        [UnityTest]
        public IEnumerator Awake_SetsInitialStateToMainMenu()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null;

            Assert.That(gm.CurrentState, Is.EqualTo(GameState.MainMenu));
        }

        [UnityTest]
        public IEnumerator Awake_DuplicateInstance_IsDestroyed()
        {
            _gameObject = new GameObject("GameManager");
            _gameObject.AddComponent<GameManager>();
            yield return null; // first instance registered

            var duplicate = new GameObject("GameManager_Duplicate");
            duplicate.AddComponent<GameManager>();
            yield return null; // Awake on duplicate should self-destruct it

            // The original singleton should still be alive
            Assert.That(GameManager.Instance, Is.Not.Null);
            Assert.That(duplicate == null, Is.True,
                "The duplicate GameManager GameObject should have been destroyed.");
        }

        // ── State machine ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SetState_ChangesCurrentState()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null;

            gm.SetState(GameState.LoadingMap);

            Assert.That(gm.CurrentState, Is.EqualTo(GameState.LoadingMap));
        }

        [UnityTest]
        public IEnumerator SetState_FiresOnStateChangedEvent()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null;

            GameState receivedState = GameState.MainMenu;
            gm.OnStateChanged.AddListener(s => receivedState = s);

            gm.SetState(GameState.Racing);

            Assert.That(receivedState, Is.EqualTo(GameState.Racing));
        }

        [UnityTest]
        public IEnumerator SetState_NoOpWhenAlreadyInSameState()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null;

            int callCount = 0;
            gm.OnStateChanged.AddListener(_ => callCount++);

            gm.SetState(GameState.MainMenu); // already in MainMenu

            Assert.That(callCount, Is.EqualTo(0),
                "OnStateChanged must not fire when the state does not change.");
        }

        [UnityTest]
        public IEnumerator SetState_SequentialTransitions_FollowCorrectOrder()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null;

            gm.SetState(GameState.LoadingMap);
            Assert.That(gm.CurrentState, Is.EqualTo(GameState.LoadingMap));

            gm.SetState(GameState.GeneratingLevel);
            Assert.That(gm.CurrentState, Is.EqualTo(GameState.GeneratingLevel));

            gm.SetState(GameState.Racing);
            Assert.That(gm.CurrentState, Is.EqualTo(GameState.Racing));
        }

        // ── Location ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SetLocation_UpdatesOriginCoordinates()
        {
            _gameObject = new GameObject("GameManager");
            var gm = _gameObject.AddComponent<GameManager>();

            yield return null;

            gm.SetLocation(41.8957, -93.5888);

            Assert.That(gm.OriginLatitude,  Is.EqualTo(41.8957).Within(1e-6));
            Assert.That(gm.OriginLongitude, Is.EqualTo(-93.5888).Within(1e-6));
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears the private static <c>_instance</c> field via reflection so
        /// each test starts from a clean singleton state.
        /// </summary>
        private static void ClearSingleton()
        {
            var field = typeof(GameManager).GetField(
                "_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            field?.SetValue(null, null);
        }
    }
}
