using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TerraDrive.Core;
using TerraDrive.Tools;

namespace TerraDrive.Editor
{
    /// <summary>
    /// Editor window opened by <b>TerraDrive → Load OSM File / Generate Level</b>.
    ///
    /// <para>
    /// The user enters GPS coordinates and a search radius, then clicks
    /// <b>Download &amp; Generate Level</b>.  The window:
    /// <list type="number">
    ///   <item>
    ///     Validates the coordinates via <see cref="OsmLevelLoader"/>.
    ///   </item>
    ///   <item>
    ///     Downloads OSM road/building data from the Overpass API and a DEM elevation
    ///     grid from the Open-Elevation API using <see cref="OsmDownloader"/>.
    ///   </item>
    ///   <item>
    ///     Saves the downloaded data as <c>downloaded.osm</c> and
    ///     <c>downloaded.elevation.csv</c> inside the chosen output directory
    ///     (default: <c>Assets/Data/</c>).
    ///   </item>
    ///   <item>
    ///     Finds the first <see cref="MapSceneBuilder"/> in the active scene (or creates
    ///     a new one) and updates its <see cref="MapSceneBuilder.OsmFilePath"/>,
    ///     <see cref="MapSceneBuilder.ElevationCsvPath"/>,
    ///     <see cref="MapSceneBuilder.OriginLatitude"/>, and
    ///     <see cref="MapSceneBuilder.OriginLongitude"/> fields.
    ///   </item>
    ///   <item>
    ///     Also syncs the active scene's <see cref="GameManager"/> origin so the
    ///     coordinate system is consistent from the first frame.
    ///   </item>
    ///   <item>
    ///     Marks the scene dirty and offers to enter Play mode immediately so
    ///     <see cref="MapSceneBuilder"/> runs the full generation pipeline.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class LoadOsmMenuEditor : EditorWindow
    {
        // ── State ──────────────────────────────────────────────────────────────

        private double _latitude  = 51.5074;
        private double _longitude = -0.1278;
        private int    _radius    = OsmLevelLoader.DefaultRadius;
        private string _outputDir = string.Empty;

        private bool   _isDownloading;
        private string _statusMessage = string.Empty;
        private bool   _statusIsError;

        private CancellationTokenSource _cts;

        // ── Menu item ──────────────────────────────────────────────────────────

        [MenuItem("TerraDrive/Load OSM File / Generate Level")]
        public static void Open()
        {
            var window = GetWindow<LoadOsmMenuEditor>("Load OSM / Generate Level");
            window.minSize = new Vector2(400, 220);
            window.Show();
        }

        // ── Unity EditorWindow lifecycle ───────────────────────────────────────

        private void OnEnable()
        {
            // Default output directory: <project root>/Assets/Data/
            _outputDir = Path.Combine(Application.dataPath, "Data");
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Location Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            using (new EditorGUI.DisabledGroupScope(_isDownloading))
            {
                _latitude  = EditorGUILayout.DoubleField(
                    new GUIContent("Latitude",  "Map origin latitude in decimal degrees (WGS-84). Range: [-90, 90]."),
                    _latitude);
                _longitude = EditorGUILayout.DoubleField(
                    new GUIContent("Longitude", "Map origin longitude in decimal degrees (WGS-84). Range: [-180, 180]."),
                    _longitude);
                _radius    = EditorGUILayout.IntField(
                    new GUIContent("Radius (m)", "Download radius around the origin in metres."),
                    _radius);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _outputDir = EditorGUILayout.TextField(
                        new GUIContent("Output Directory", "Directory where downloaded .osm and .elevation.csv files are saved."),
                        _outputDir);

                    if (GUILayout.Button("…", GUILayout.Width(28)))
                    {
                        string chosen = EditorUtility.OpenFolderPanel(
                            "Select Output Directory", _outputDir, string.Empty);
                        if (!string.IsNullOrEmpty(chosen))
                            _outputDir = chosen;
                    }
                }

                EditorGUILayout.Space(8);

                if (GUILayout.Button("Download & Generate Level", GUILayout.Height(30)))
                    StartDownload();
            }

            if (_isDownloading)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Cancel"))
                {
                    _cts?.Cancel();
                    SetStatus("Download cancelled.", isError: true);
                }
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(4);
                var type = _statusIsError ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(_statusMessage, type);
            }
        }

        // ── Download pipeline ──────────────────────────────────────────────────

        private void StartDownload()
        {
            // Validate coordinates.
            var settings = new OsmLevelLoader
            {
                Latitude  = _latitude,
                Longitude = _longitude,
                Radius    = _radius,
            };

            var errors = settings.Validate();
            if (errors.Count > 0)
            {
                SetStatus(string.Join("\n", errors), isError: true);
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _ = DownloadAndConfigureAsync(_latitude, _longitude, _radius, _outputDir, _cts.Token);
        }

        private async Task DownloadAndConfigureAsync(
            double latitude, double longitude, int radius,
            string outputDir, CancellationToken ct)
        {
            _isDownloading = true;
            SetStatus("Downloading OSM data…");
            Repaint();

            try
            {
                Directory.CreateDirectory(outputDir);
                string osmPath = Path.Combine(outputDir, "downloaded.osm");
                string csvPath = Path.Combine(outputDir, "downloaded.elevation.csv");

                // 1. Download OSM road/building XML.
                EditorUtility.DisplayProgressBar(
                    "TerraDrive — Downloading", "Fetching OSM road data…", 0.15f);

                var downloader = new OsmDownloader();
                string osmXml = await downloader
                    .DownloadOsmAsync(latitude, longitude, radius, ct)
                    .ConfigureAwait(true);   // resume on Unity main thread

                ct.ThrowIfCancellationRequested();
                OsmDownloader.SaveOsm(osmXml, osmPath);

                // 2. Download DEM elevation grid.
                EditorUtility.DisplayProgressBar(
                    "TerraDrive — Downloading", "Fetching elevation data…", 0.55f);

                TerraDrive.Terrain.ElevationGrid elevGrid = await downloader
                    .DownloadElevationGridAsync(latitude, longitude, radius,
                        cancellationToken: ct)
                    .ConfigureAwait(true);

                ct.ThrowIfCancellationRequested();
                OsmDownloader.SaveElevation(elevGrid, csvPath);

                // 3. Wire the downloaded files into the scene.
                EditorUtility.DisplayProgressBar(
                    "TerraDrive — Configuring", "Configuring scene…", 0.90f);

                ConfigureScene(osmPath, csvPath, latitude, longitude);

                EditorUtility.ClearProgressBar();
                SetStatus($"Download complete. OSM: {osmPath}\nCSV: {csvPath}");

                // 4. Offer to enter Play mode.
                bool enterPlay = EditorUtility.DisplayDialog(
                    "TerraDrive — Generate Level",
                    $"Files downloaded and scene configured.\n\n" +
                    $"• Lat: {latitude:F6}  Lon: {longitude:F6}  Radius: {radius} m\n\n" +
                    "Enter Play mode now to build the terrain, roads, and buildings?",
                    "Enter Play Mode",
                    "Later");

                if (enterPlay)
                    EditorApplication.isPlaying = true;
            }
            catch (OperationCanceledException)
            {
                EditorUtility.ClearProgressBar();
                SetStatus("Download cancelled.", isError: true);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                SetStatus($"Download failed: {ex.Message}", isError: true);
                Debug.LogError($"[LoadOsmMenu] Download failed: {ex}");
            }
            finally
            {
                _isDownloading = false;
                Repaint();
            }
        }

        // ── Scene configuration ────────────────────────────────────────────────

        private static void ConfigureScene(
            string osmPath, string csvPath,
            double latitude, double longitude)
        {
            // Find or create a MapSceneBuilder.
            var builders = UnityEngine.Object.FindObjectsByType<MapSceneBuilder>(FindObjectsSortMode.None);
            if (builders.Length > 1)
                Debug.LogWarning("[LoadOsmMenu] Multiple MapSceneBuilder instances found in scene; configuring the first one.");

            MapSceneBuilder builder;
            if (builders.Length > 0)
            {
                builder = builders[0];
            }
            else
            {
                var go = new GameObject("MapSceneBuilder");
                builder = go.AddComponent<MapSceneBuilder>();
                Debug.Log("[LoadOsmMenu] Created a new MapSceneBuilder GameObject.");
            }

            builder.OsmFilePath      = osmPath;
            builder.ElevationCsvPath = csvPath;
            builder.OriginLatitude   = latitude;
            builder.OriginLongitude  = longitude;

            // Sync the GameManager origin if one exists in the scene.
            var gameManagers = UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
            if (gameManagers.Length > 1)
                Debug.LogWarning("[LoadOsmMenu] Multiple GameManager instances found in scene; syncing the first one.");

            if (gameManagers.Length > 0)
            {
                gameManagers[0].OriginLatitude  = latitude;
                gameManagers[0].OriginLongitude = longitude;
            }

            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

            Debug.Log(
                $"[LoadOsmMenu] Scene configured.\n" +
                $"  OSM: {osmPath}\n" +
                $"  CSV: {csvPath}\n" +
                $"  Origin: ({latitude:F6}, {longitude:F6})");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void SetStatus(string message, bool isError = false)
        {
            _statusMessage = message;
            _statusIsError = isError;
            Repaint();
        }
    }
}
