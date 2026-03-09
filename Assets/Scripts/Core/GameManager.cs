using UnityEngine;
using UnityEngine.Events;

namespace TerraDrive.Core
{
    /// <summary>
    /// High-level game state values.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        LoadingMap,
        GeneratingLevel,
        Racing,
        Paused,
        Results
    }

    /// <summary>
    /// Singleton entry-point for TerraDrive.  Owns the top-level game state machine
    /// and exposes events so other systems can react to state transitions without
    /// tight coupling.
    ///
    /// Usage:
    ///   GameManager.Instance.SetState(GameState.LoadingMap);
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────────────────────

        private static GameManager _instance;

        /// <summary>The single active <see cref="GameManager"/> instance.</summary>
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                    Debug.LogWarning("[GameManager] No instance found in scene.");
                return _instance;
            }
        }

        // ── State ──────────────────────────────────────────────────────────────

        [SerializeField]
        private GameState _initialState = GameState.MainMenu;

        /// <summary>The current game state.</summary>
        public GameState CurrentState { get; private set; }

        /// <summary>Fired whenever the game state changes. Passes the new state.</summary>
        public UnityEvent<GameState> OnStateChanged = new UnityEvent<GameState>();

        // ── Map settings ───────────────────────────────────────────────────────

        [Header("Map Origin")]
        [Tooltip("Latitude of the map origin (world 0,0,0).")]
        public double OriginLatitude = 51.5074;

        [Tooltip("Longitude of the map origin (world 0,0,0).")]
        public double OriginLongitude = -0.1278;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentState = _initialState;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions to <paramref name="newState"/> and fires <see cref="OnStateChanged"/>.
        /// No-ops if already in that state.
        /// </summary>
        public void SetState(GameState newState)
        {
            if (newState == CurrentState)
                return;

            Debug.Log($"[GameManager] {CurrentState} → {newState}");
            CurrentState = newState;
            OnStateChanged.Invoke(newState);
        }

        /// <summary>
        /// Updates the map origin to the given GPS coordinates.
        ///
        /// Call this after a successful
        /// <see cref="Core.LocationMenuController.LoadLocationAsync"/> to keep the
        /// <see cref="GameManager"/> in sync with the loaded map.
        /// </summary>
        /// <param name="latitude">New origin latitude in decimal degrees (WGS-84).</param>
        /// <param name="longitude">New origin longitude in decimal degrees (WGS-84).</param>
        public void SetLocation(double latitude, double longitude)
        {
            OriginLatitude  = latitude;
            OriginLongitude = longitude;
            Debug.Log($"[GameManager] Location updated to ({latitude:F6}, {longitude:F6}).");
        }
    }
}
