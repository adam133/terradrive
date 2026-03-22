using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VectorRoad.Core;
using VectorRoad.Terrain;
using VectorRoad.Tools;

namespace VectorRoad.Hud
{
    /// <summary>
    /// Programmatic uGUI startup menu shown when the game is in the
    /// <see cref="GameState.MainMenu"/> state.  Gives the player two choices:
    /// load the bundled default map immediately, or download a new location
    /// before building the level.
    ///
    /// <para>
    /// Auto-created by <see cref="VectorRoad.Core.MapSceneBuilder"/> when
    /// <see cref="GameState.MainMenu"/> is detected at startup — no prefab or
    /// manual scene placement required.
    /// </para>
    /// </summary>
    public class StartupMenuUi : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Download Defaults")]
        [Tooltip("Latitude pre-filled in the download form.")]
        [SerializeField] private double _defaultLatitude = 51.5074;

        [Tooltip("Longitude pre-filled in the download form.")]
        [SerializeField] private double _defaultLongitude = -0.1278;

        [Tooltip("Radius (metres) pre-filled in the download form.")]
        [SerializeField] private int _defaultRadius = OsmLevelLoader.DefaultRadius;

        // ── UI references ──────────────────────────────────────────────────────

        private Canvas         _canvas;
        private GameObject     _splashPanel;
        private GameObject     _downloadPanel;
        private GameObject     _loadingPanel;

        private TMP_InputField _coordsField;
        private TMP_InputField _radField;
        private TMP_Text       _downloadStatus;
        private Button         _downloadBtn;
        private Button         _backBtn;
        private TMP_Text       _loadingStatus;

        // ── Private state ──────────────────────────────────────────────────────

        private CancellationTokenSource _cts;
        private bool _inDownloadPanel;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            BuildCanvas();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged.AddListener(HandleStateChanged);
            ShowSplash();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged.RemoveListener(HandleStateChanged);
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── State handling ─────────────────────────────────────────────────────

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                    if (_inDownloadPanel) ShowDownload(); else ShowSplash();
                    break;
                case GameState.LoadingMap:
                    ShowLoading("Loading map data\u2026");
                    break;
                case GameState.GeneratingLevel:
                    if (_loadingPanel != null && _loadingPanel.activeSelf)
                        _loadingStatus.text = "Generating level\u2026";
                    break;
                case GameState.Racing:
                    if (_canvas != null)
                        _canvas.gameObject.SetActive(false);
                    break;
            }
        }

        private void ShowSplash()
        {
            _inDownloadPanel = false;
            _splashPanel?.SetActive(true);
            _downloadPanel?.SetActive(false);
            _loadingPanel?.SetActive(false);
        }

        private void ShowDownload()
        {
            _inDownloadPanel = true;
            _splashPanel?.SetActive(false);
            _downloadPanel?.SetActive(true);
            _loadingPanel?.SetActive(false);
        }

        private void ShowLoading(string message)
        {
            _splashPanel?.SetActive(false);
            _downloadPanel?.SetActive(false);
            _loadingPanel?.SetActive(true);
            if (_loadingStatus != null)
                _loadingStatus.text = message;
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void OnLoadDefault()
        {
            GameManager.Instance?.SetState(GameState.LoadingMap);
        }

        private void OnShowDownload()
        {
            _coordsField.text     = $"{_defaultLatitude.ToString("F6", CultureInfo.InvariantCulture)}, {_defaultLongitude.ToString("F6", CultureInfo.InvariantCulture)}";
            _radField.text        = _defaultRadius.ToString(CultureInfo.InvariantCulture);
            _downloadStatus.text  = string.Empty;
            ShowDownload();
        }

        private void OnBack()
        {
            _cts?.Cancel();
            ShowSplash();
        }

        private void OnDownloadAndLoad()
        {
            if (!OsmLevelLoader.TryParseCoordinates(_coordsField.text, out double lat, out double lon))
            {
                SetDownloadStatus("Invalid coordinates \u2014 enter as \u201clat, lon\u201d, e.g. 51.5074, -0.1278", isError: true);
                return;
            }

            if (!int.TryParse(_radField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rad))
            {
                SetDownloadStatus("Invalid radius \u2014 use a whole number, e.g. 500", isError: true);
                return;
            }

            var loader = new OsmLevelLoader { Latitude = lat, Longitude = lon, Radius = rad };
            var errors = loader.Validate();
            if (errors.Count > 0)
            {
                SetDownloadStatus(string.Join("\n", errors), isError: true);
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            // Fire-and-forget: updates status labels on the Unity main thread
            // and ultimately calls SetState(LoadingMap) on success, so there
            // is no meaningful state to recover after the method returns.
            _ = DownloadAndLoadAsync(lat, lon, rad, _cts.Token);
        }

        private async Task DownloadAndLoadAsync(double lat, double lon, int rad, CancellationToken ct)
        {
            SetDownloadInteractable(false);
            SetDownloadStatus("Connecting to Overpass API\u2026");

            try
            {
                string dataDir = Path.Combine(Path.GetTempPath(), "vectorroad");
                Directory.CreateDirectory(dataDir);

                string osmPath  = Path.Combine(dataDir, "current.osm");
                string elevPath = Path.Combine(dataDir, "current.elevation.csv");

                // 1. Download OSM road/building data.
                SetDownloadStatus("Downloading OSM road data\u2026");
                var downloader = new OsmDownloader();
                string osmXml = await downloader
                    .DownloadOsmAsync(lat, lon, rad, ct)
                    .ConfigureAwait(true);   // resume on Unity main thread

                ct.ThrowIfCancellationRequested();
                OsmDownloader.SaveOsm(osmXml, osmPath);

                // 2. Download DEM elevation grid.
                SetDownloadStatus("Downloading elevation data\u2026");
                ElevationGrid elevGrid = await downloader
                    .DownloadElevationGridAsync(lat, lon, rad, cancellationToken: ct)
                    .ConfigureAwait(true);

                ct.ThrowIfCancellationRequested();
                OsmDownloader.SaveElevation(elevGrid, elevPath);

                // 3. Update GameManager so MapSceneBuilder uses the new files.
                CoordinateConverter.ResetWorldOrigin();
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetLocation(lat, lon);
                    GameManager.Instance.OsmFilePathOverride       = osmPath;
                    GameManager.Instance.ElevationFilePathOverride = elevPath;
                }

                // 4. Advance state — WaitForMenuThenBuild will unblock and start building.
                GameManager.Instance?.SetState(GameState.LoadingMap);
            }
            catch (OperationCanceledException)
            {
                SetDownloadStatus("Download cancelled.", isError: true);
                SetDownloadInteractable(true);
            }
            catch (Exception ex)
            {
                SetDownloadStatus($"Download failed: {ex.Message}", isError: true);
                Debug.LogError($"[StartupMenuUi] Download failed: {ex}");
                SetDownloadInteractable(true);
            }
        }

        private void SetDownloadStatus(string message, bool isError = false)
        {
            if (_downloadStatus == null) return;
            _downloadStatus.text  = message;
            _downloadStatus.color = isError
                ? new Color(1f, 0.35f, 0.35f)
                : new Color(0.75f, 0.95f, 0.75f);
        }

        private void SetDownloadInteractable(bool interactable)
        {
            if (_downloadBtn != null) _downloadBtn.interactable = interactable;
            if (_backBtn     != null) _backBtn.interactable     = interactable;
        }

        // ── Canvas builder ─────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            // Root canvas — sits on top of everything at sort order 100.
            var canvasGo = new GameObject("StartupMenuCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas              = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Make sure an EventSystem is present so buttons receive clicks.
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            // Full-screen dark overlay behind all panels.
            var bgGo  = CreateUiObject("Background", canvasGo.transform);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.07f, 0.93f);
            StretchFull(bgGo.GetComponent<RectTransform>());

            // Build the three panels; only splash is shown initially.
            _splashPanel   = BuildSplashPanel(canvasGo.transform);
            _downloadPanel = BuildDownloadPanel(canvasGo.transform);
            _loadingPanel  = BuildLoadingPanel(canvasGo.transform);

            _downloadPanel.SetActive(false);
            _loadingPanel.SetActive(false);
        }

        // ── Splash panel ───────────────────────────────────────────────────────

        private GameObject BuildSplashPanel(Transform canvasRoot)
        {
            var panel = CreateCenteredPanel("SplashPanel", canvasRoot, 500f, 310f);
            AddPanelBackground(panel);

            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding                = new RectOffset(36, 36, 36, 36);
            vl.spacing                = 14f;
            vl.childAlignment         = TextAnchor.UpperCenter;
            vl.childControlWidth      = true;
            vl.childControlHeight     = true;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;

            AddLabel(panel.transform, "VectorRoad", 54f, Color.white,
                     FontStyles.Bold, preferredHeight: 72f);

            AddLabel(panel.transform, "Choose how to start", 19f,
                     new Color(0.68f, 0.68f, 0.78f), preferredHeight: 28f);

            AddSpacer(panel.transform, 6f);

            AddButton(panel.transform, "Load Default Map",
                      new Color(0.16f, 0.50f, 0.24f), OnLoadDefault, preferredHeight: 56f);

            AddButton(panel.transform, "Download New Location",
                      new Color(0.14f, 0.36f, 0.68f), OnShowDownload, preferredHeight: 56f);

            return panel;
        }

        // ── Download panel ─────────────────────────────────────────────────────

        private GameObject BuildDownloadPanel(Transform canvasRoot)
        {
            var panel = CreateCenteredPanel("DownloadPanel", canvasRoot, 500f, 350f);
            AddPanelBackground(panel);

            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding                = new RectOffset(36, 36, 28, 28);
            vl.spacing                = 12f;
            vl.childAlignment         = TextAnchor.UpperCenter;
            vl.childControlWidth      = true;
            vl.childControlHeight     = true;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;

            AddLabel(panel.transform, "Download New Location", 28f, Color.white,
                     FontStyles.Bold, preferredHeight: 42f);

            _coordsField = AddInputRow(panel.transform, "Coordinates", "e.g. 51.5074, -0.1278");
            _coordsField.contentType = TMP_InputField.ContentType.Standard;
            _radField = AddInputRow(panel.transform, "Radius (m)", "e.g. 500");
            _radField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Button row.
            var btnRow = CreateUiObject("ButtonRow", panel.transform);
            AddLayoutElement(btnRow, preferredHeight: 52f);
            var hl = btnRow.AddComponent<HorizontalLayoutGroup>();
            hl.spacing               = 14f;
            hl.childAlignment        = TextAnchor.MiddleCenter;
            hl.childControlWidth     = true;
            hl.childControlHeight    = true;
            hl.childForceExpandWidth  = true;
            hl.childForceExpandHeight = false;

            _downloadBtn = AddButton(btnRow.transform, "Download & Load",
                                     new Color(0.14f, 0.36f, 0.68f), OnDownloadAndLoad,
                                     preferredHeight: 52f);
            _backBtn     = AddButton(btnRow.transform, "\u2190 Back",
                                     new Color(0.28f, 0.28f, 0.33f), OnBack,
                                     preferredHeight: 52f);

            // Status label (shows progress and errors).
            var statusGo = CreateUiObject("Status", panel.transform);
            AddLayoutElement(statusGo, preferredHeight: 52f);
            _downloadStatus                     = statusGo.AddComponent<TextMeshProUGUI>();
            _downloadStatus.fontSize            = 15f;
            _downloadStatus.color               = Color.white;
            _downloadStatus.alignment           = TextAlignmentOptions.Center;
            _downloadStatus.enableWordWrapping  = true;

            return panel;
        }

        // ── Loading panel ──────────────────────────────────────────────────────

        private GameObject BuildLoadingPanel(Transform canvasRoot)
        {
            var panel = CreateCenteredPanel("LoadingPanel", canvasRoot, 460f, 160f);
            AddPanelBackground(panel);

            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding                = new RectOffset(36, 36, 36, 36);
            vl.spacing                = 16f;
            vl.childAlignment         = TextAnchor.MiddleCenter;
            vl.childControlWidth      = true;
            vl.childControlHeight     = true;
            vl.childForceExpandWidth  = true;
            vl.childForceExpandHeight = false;

            AddLabel(panel.transform, "Please wait\u2026", 24f,
                     new Color(0.68f, 0.68f, 0.78f), preferredHeight: 36f);

            var statusGo = CreateUiObject("Status", panel.transform);
            AddLayoutElement(statusGo, preferredHeight: 34f);
            _loadingStatus           = statusGo.AddComponent<TextMeshProUGUI>();
            _loadingStatus.fontSize  = 20f;
            _loadingStatus.color     = Color.white;
            _loadingStatus.alignment = TextAlignmentOptions.Center;

            return panel;
        }

        // ── UI factory helpers ─────────────────────────────────────────────────

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateCenteredPanel(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static void AddPanelBackground(GameObject panel)
        {
            var img = panel.AddComponent<Image>();
            img.color = new Color(0.09f, 0.09f, 0.12f, 0.97f);
        }

        private static void AddLayoutElement(GameObject go, float preferredHeight)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleHeight  = 0f;
        }

        private static void AddLabel(
            Transform   parent,
            string      text,
            float       fontSize,
            Color       color,
            FontStyles  fontStyle     = FontStyles.Normal,
            float       preferredHeight = 30f)
        {
            var go = CreateUiObject(text, parent);
            AddLayoutElement(go, preferredHeight);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text               = text;
            tmp.fontSize           = fontSize;
            tmp.color              = color;
            tmp.fontStyle          = fontStyle;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
        }

        private static void AddSpacer(Transform parent, float height)
        {
            var go = CreateUiObject("Spacer", parent);
            AddLayoutElement(go, height);
        }

        private Button AddButton(
            Transform                        parent,
            string                           label,
            Color                            bgColor,
            UnityEngine.Events.UnityAction   onClick,
            float                            preferredHeight = 52f)
        {
            var go = CreateUiObject(label, parent);
            AddLayoutElement(go, preferredHeight);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.normalColor      = bgColor;
            cs.highlightedColor = new Color(
                Mathf.Min(bgColor.r + 0.13f, 1f),
                Mathf.Min(bgColor.g + 0.13f, 1f),
                Mathf.Min(bgColor.b + 0.13f, 1f));
            cs.pressedColor  = new Color(
                Mathf.Max(bgColor.r - 0.10f, 0f),
                Mathf.Max(bgColor.g - 0.10f, 0f),
                Mathf.Max(bgColor.b - 0.10f, 0f));
            cs.disabledColor = new Color(0.22f, 0.22f, 0.24f, 0.75f);
            btn.colors       = cs;
            btn.onClick.AddListener(onClick);

            // Label inside button.
            var textGo = CreateUiObject("Label", go.transform);
            var rt     = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text               = label;
            tmp.fontSize           = 21f;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.color              = Color.white;
            tmp.enableWordWrapping = false;

            return btn;
        }

        private static TMP_InputField AddInputRow(
            Transform parent,
            string    labelText,
            string    placeholderText,
            float     preferredHeight = 44f)
        {
            // Horizontal row: fixed-width label + expanding input field.
            var row = CreateUiObject(labelText + "Row", parent);
            AddLayoutElement(row, preferredHeight);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing               = 10f;
            hl.childAlignment        = TextAnchor.MiddleLeft;
            hl.childControlWidth     = true;
            hl.childControlHeight    = true;
            hl.childForceExpandWidth  = false;
            hl.childForceExpandHeight = true;

            // Label.
            var labelGo = CreateUiObject(labelText + "Label", row.transform);
            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 120f;
            labelLe.flexibleWidth  = 0f;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text      = labelText;
            labelTmp.fontSize  = 17f;
            labelTmp.color     = new Color(0.82f, 0.82f, 0.88f);
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Input field container.
            var fieldGo = CreateUiObject(labelText + "Field", row.transform);
            var fieldLe = fieldGo.AddComponent<LayoutElement>();
            fieldLe.flexibleWidth = 1f;
            var bg = fieldGo.AddComponent<Image>();
            bg.color = new Color(0.17f, 0.17f, 0.21f);

            var inputField = fieldGo.AddComponent<TMP_InputField>();

            // Text area (clips overflow text).
            var textAreaGo = CreateUiObject("Text Area", fieldGo.transform);
            var textAreaRt = textAreaGo.GetComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(10f, 4f);
            textAreaRt.offsetMax = new Vector2(-10f, -4f);
            textAreaGo.AddComponent<RectMask2D>();

            // Placeholder text.
            var placeholderGo = CreateUiObject("Placeholder", textAreaGo.transform);
            var placeholderRt = placeholderGo.GetComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = Vector2.zero;
            placeholderRt.offsetMax = Vector2.zero;
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text               = placeholderText;
            placeholder.fontSize           = 16f;
            placeholder.fontStyle          = FontStyles.Italic;
            placeholder.color              = new Color(0.42f, 0.42f, 0.47f);
            placeholder.enableWordWrapping = false;
            placeholder.alignment          = TextAlignmentOptions.MidlineLeft;

            // Input text.
            var textGo = CreateUiObject("Text", textAreaGo.transform);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var inputText = textGo.AddComponent<TextMeshProUGUI>();
            inputText.fontSize           = 16f;
            inputText.color              = Color.white;
            inputText.enableWordWrapping = false;
            inputText.alignment          = TextAlignmentOptions.MidlineLeft;

            // Wire references into TMP_InputField.
            inputField.textComponent = inputText;
            inputField.placeholder   = placeholder;
            inputField.textViewport  = textAreaRt;
            inputField.contentType   = TMP_InputField.ContentType.DecimalNumber;

            return inputField;
        }
    }
}
