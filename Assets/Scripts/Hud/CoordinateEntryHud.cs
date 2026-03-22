using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VectorRoad.Core;
using VectorRoad.Tools;

namespace VectorRoad.Hud
{
    /// <summary>
    /// In-game HUD component that shows a coordinate-entry dialog so the player can
    /// download a new location and reload the scene without leaving Play mode.
    ///
    /// <para>
    /// Attach this MonoBehaviour to any persistent GameObject in the scene (e.g. the
    /// same object that carries <see cref="GameManager"/>).  Press <see cref="ToggleKey"/>
    /// (default: <c>Escape</c>) while in Play mode to open or close the dialog.
    /// </para>
    ///
    /// <para>
    /// When the player confirms new coordinates the component:
    /// <list type="number">
    ///   <item>Validates the input via <see cref="OsmLevelLoader"/>.</item>
    ///   <item>Downloads OSM and elevation data using <see cref="OsmDownloader"/>.</item>
    ///   <item>
    ///     Stores the downloaded file paths in <see cref="GameManager.OsmFilePathOverride"/>
    ///     and <see cref="GameManager.ElevationFilePathOverride"/> so that
    ///     <see cref="MapSceneBuilder"/> picks them up on the next scene load.
    ///   </item>
    ///   <item>Reloads the active scene, triggering a full level rebuild.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class CoordinateEntryHud : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Input Binding")]
        [Tooltip("Key that opens and closes the coordinate-entry dialog.")]
        public KeyCode ToggleKey = KeyCode.Escape;

        [Header("Dialog Defaults")]
        [Tooltip("Latitude pre-filled in the dialog (updated each time the dialog is opened).")]
        public double DefaultLatitude = 51.5074;

        [Tooltip("Longitude pre-filled in the dialog (updated each time the dialog is opened).")]
        public double DefaultLongitude = -0.1278;

        [Tooltip("Search radius (metres) pre-filled in the dialog.")]
        public int DefaultRadius = OsmLevelLoader.DefaultRadius;

        // ── Private state ──────────────────────────────────────────────────────

        private bool   _isVisible;
        private bool   _isLoading;

        private string _coordsStr = string.Empty;
        private string _radStr    = string.Empty;

        private string _statusMessage = string.Empty;
        private bool   _statusIsError;

        private CancellationTokenSource _cts;

        // GUI window rect (computed once on first show)
        private static readonly Vector2 DialogSize = new Vector2(420f, 230f);

        // Cached overlay texture to avoid per-frame allocations in OnGUI.
        private Texture2D _overlayTexture;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                Toggle();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            if (_overlayTexture != null)
                Destroy(_overlayTexture);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Opens the dialog if it is closed, or closes it if it is open.
        /// While loading, the dialog can be closed only by cancelling.
        /// </summary>
        public void Toggle()
        {
            if (_isVisible)
            {
                if (!_isLoading)
                    Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>Opens the coordinate-entry dialog.</summary>
        public void Show()
        {
            // Pre-fill fields from the current GameManager origin (or inspector defaults).
            double lat = GameManager.Instance != null
                ? GameManager.Instance.OriginLatitude
                : DefaultLatitude;
            double lon = GameManager.Instance != null
                ? GameManager.Instance.OriginLongitude
                : DefaultLongitude;

            _coordsStr = $"{lat.ToString("F6", CultureInfo.InvariantCulture)}, {lon.ToString("F6", CultureInfo.InvariantCulture)}";
            _radStr = DefaultRadius.ToString(CultureInfo.InvariantCulture);

            _statusMessage = string.Empty;
            _isVisible     = true;

            GameManager.Instance?.SetState(GameState.Paused);
        }

        /// <summary>Closes the coordinate-entry dialog without loading.</summary>
        public void Hide()
        {
            _isVisible = false;
            GameManager.Instance?.SetState(GameState.Racing);
        }

        // ── IMGUI rendering ────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_isVisible)
                return;

            // Semi-transparent full-screen overlay.
            if (_overlayTexture == null)
                _overlayTexture = MakePixel(new Color(0f, 0f, 0f, 0.5f));

            var overlayStyle = new GUIStyle();
            overlayStyle.normal.background = _overlayTexture;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, overlayStyle);

            float x = (Screen.width  - DialogSize.x) * 0.5f;
            float y = (Screen.height - DialogSize.y) * 0.5f;
            GUI.Window(0, new Rect(x, y, DialogSize.x, DialogSize.y),
                DrawDialogContents, "Enter Coordinates");
        }

        private void DrawDialogContents(int _windowId)
        {
            GUILayout.Space(6);

            // ── Coordinates (lat, lon) ──
            GUILayout.BeginHorizontal();
            GUILayout.Label("Coordinates:", GUILayout.Width(110));
            GUI.enabled = !_isLoading;
            _coordsStr = GUILayout.TextField(_coordsStr);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("e.g. 51.5074, -0.1278", new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });

            // ── Radius ──
            GUILayout.BeginHorizontal();
            GUILayout.Label("Radius (m):", GUILayout.Width(110));
            GUI.enabled = !_isLoading;
            _radStr = GUILayout.TextField(_radStr);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ── Buttons ──
            GUILayout.BeginHorizontal();

            GUI.enabled = !_isLoading;
            if (GUILayout.Button(_isLoading ? "Downloading…" : "Load", GUILayout.Height(26)))
                StartLoad();
            GUI.enabled = true;

            if (GUILayout.Button(_isLoading ? "Cancel Download" : "Cancel", GUILayout.Height(26)))
            {
                _cts?.Cancel();
                if (!_isLoading)
                    Hide();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── Extra actions ──
            GUILayout.BeginHorizontal();

            GUI.enabled = !_isLoading;
            if (GUILayout.Button("Reset Car", GUILayout.Height(26)))
            {
                ResetCar();
                Hide();
            }
            if (GUILayout.Button("Exit Game", GUILayout.Height(26)))
                QuitGame();
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            // ── Status message ──
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Space(4);
                var style     = new GUIStyle(GUI.skin.label) { wordWrap = true };
                style.normal.textColor = _statusIsError ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.3f);
                GUILayout.Label(_statusMessage, style);
            }
        }

        // ── Load pipeline ──────────────────────────────────────────────────────

        private void StartLoad()
        {
            if (!OsmLevelLoader.TryParseCoordinates(_coordsStr, out double lat, out double lon))
            {
                SetStatus("Invalid coordinates — enter as \"lat, lon\", e.g. 51.5074, -0.1278", isError: true);
                return;
            }

            if (!int.TryParse(_radStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rad))
            {
                SetStatus("Invalid radius — use a whole number, e.g. 500", isError: true);
                return;
            }

            var loader = new OsmLevelLoader { Latitude = lat, Longitude = lon, Radius = rad };
            var errors = loader.Validate();
            if (errors.Count > 0)
            {
                SetStatus(string.Join("\n", errors), isError: true);
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            // Fire-and-forget is intentional: LoadAsync updates _isLoading / _statusMessage
            // on the Unity main thread and ultimately triggers a full scene reload, so there
            // is no meaningful state to recover once the method returns.
            _ = LoadAsync(lat, lon, rad, _cts.Token);
        }

        private async Task LoadAsync(
            double lat, double lon, int rad, CancellationToken ct)
        {
            _isLoading = true;
            SetStatus("Connecting to Overpass API…");

            try
            {
                string dataDir = Path.Combine(Path.GetTempPath(), "vectorroad");
                Directory.CreateDirectory(dataDir);

                string osmPath  = Path.Combine(dataDir, "current.osm");
                string elevPath = Path.Combine(dataDir, "current.elevation.csv");

                // 1. Download OSM road/building data.
                SetStatus("Downloading OSM road data…");
                var downloader = new OsmDownloader();
                string osmXml = await downloader
                    .DownloadOsmAsync(lat, lon, rad, ct)
                    .ConfigureAwait(true);   // resume on Unity main thread

                ct.ThrowIfCancellationRequested();
                OsmDownloader.SaveOsm(osmXml, osmPath);

                // 2. Download DEM elevation grid.
                SetStatus("Downloading elevation data…");
                VectorRoad.Terrain.ElevationGrid elevGrid = await downloader
                    .DownloadElevationGridAsync(lat, lon, rad, cancellationToken: ct)
                    .ConfigureAwait(true);

                ct.ThrowIfCancellationRequested();
                OsmDownloader.SaveElevation(elevGrid, elevPath);

                // 3. Update GameManager so the reloaded scene uses the new data.
                SetStatus("Reloading scene…");
                // Clear the cached Mercator world origin so the new coordinate
                // maps cleanly to (0, 0, 0) on reload — mirrors LocationMenuController.
                CoordinateConverter.ResetWorldOrigin();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetLocation(lat, lon);
                    GameManager.Instance.OsmFilePathOverride       = osmPath;
                    GameManager.Instance.ElevationFilePathOverride = elevPath;
                }

                // 4. Reload the active scene — MapSceneBuilder will pick up the
                //    new file paths from GameManager and rebuild everything.
                // Hide the dialog before reloading so it doesn't persist on the
                // GameManager object into the freshly loaded scene.
                _isVisible = false;
                _isLoading = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Download cancelled.", isError: true);
                _isLoading = false;
            }
            catch (Exception ex)
            {
                SetStatus($"Download failed: {ex.Message}", isError: true);
                Debug.LogError($"[CoordinateEntryHud] Download failed: {ex}");
                _isLoading = false;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetStatus(string message, bool isError = false)
        {
            _statusMessage = message;
            _statusIsError = isError;
        }

        private static Texture2D MakePixel(Color colour)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, colour);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Teleports the vehicle to the nearest drivable road by delegating to
        /// <see cref="MapSceneBuilder.ResetVehicle"/>.
        /// </summary>
        private static void ResetCar()
        {
            var builder = FindFirstObjectByType<MapSceneBuilder>();
            if (builder != null)
                builder.ResetVehicle();
            else
                Debug.LogWarning("[CoordinateEntryHud] ResetCar: no MapSceneBuilder found in scene.");
        }

        /// <summary>
        /// Exits the game.  In the Unity Editor this stops Play mode; in a
        /// standalone build it calls <see cref="Application.Quit"/>.
        /// </summary>
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
