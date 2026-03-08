using System;
using UnityEditor;
using UnityEngine;

namespace TerraDrive.Editor
{
    /// <summary>
    /// One-shot project configurator that can be invoked from the Unity CLI in batch
    /// mode via <c>-executeMethod TerraDrive.Editor.ProjectSetup.Configure</c>.
    ///
    /// What it does:
    ///   • Sets physics gravity to (0, -9.81, 0).
    ///   • Adds a "Terrain" user layer (first available slot ≥ 8).
    ///   • Adds a "Road"    user layer (first available slot ≥ 8).
    ///
    /// Example CLI invocations (adjust the Unity executable path to your version):
    ///
    ///   Windows:
    ///     "C:\Program Files\Unity\Hub\Editor\6000.3.x\Editor\Unity.exe" ^
    ///       -batchmode -quit ^
    ///       -executeMethod TerraDrive.Editor.ProjectSetup.Configure ^
    ///       -projectPath "C:\path\to\terradrive"
    ///
    ///   macOS / Linux:
    ///     /Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    ///       -batchmode -quit \
    ///       -executeMethod TerraDrive.Editor.ProjectSetup.Configure \
    ///       -projectPath "/path/to/terradrive"
    /// </summary>
    public static class ProjectSetup
    {
        // ── Names of the custom layers that must exist in the project ───────────

        private const string LayerTerrain = "Terrain";
        private const string LayerRoad = "Road";

        // ── Menu item (only visible in the Editor, not in batch mode) ──────────

        [MenuItem("TerraDrive/Configure Project")]
        public static void Configure()
        {
            try
            {
                ConfigurePhysics();
                ConfigureLayers();
                AssetDatabase.SaveAssets();

                Debug.Log("[ProjectSetup] Project configured successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectSetup] Configuration failed: {ex}");

                // In batch mode, a non-zero exit code signals failure to CI.
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        // ── Physics ─────────────────────────────────────────────────────────────

        private static void ConfigurePhysics()
        {
            // DynamicsManager.asset stores the Physics settings serialised on disk.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (assets.Length == 0)
            {
                Debug.LogWarning("[ProjectSetup] DynamicsManager.asset not found – skipping gravity setup.");
                return;
            }

            var physicsManager = new SerializedObject(assets[0]);
            var gravityProp = physicsManager.FindProperty("m_Gravity");
            if (gravityProp != null)
            {
                gravityProp.vector3Value = new Vector3(0f, -9.81f, 0f);
                physicsManager.ApplyModifiedProperties();
                Debug.Log("[ProjectSetup] Gravity set to (0, -9.81, 0).");
            }
            else
            {
                Debug.LogWarning("[ProjectSetup] m_Gravity property not found in DynamicsManager.asset.");
            }
        }

        // ── Layers ───────────────────────────────────────────────────────────────

        private static void ConfigureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                Debug.LogWarning("[ProjectSetup] TagManager.asset not found – skipping layer setup.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");

            EnsureLayer(layers, LayerTerrain);
            EnsureLayer(layers, LayerRoad);

            tagManager.ApplyModifiedProperties();
        }

        /// <summary>
        /// Adds <paramref name="layerName"/> to the first empty user-layer slot
        /// (index 8+) if it does not already exist.
        /// </summary>
        private static void EnsureLayer(SerializedProperty layers, string layerName)
        {
            // Check whether the layer already exists.
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                {
                    Debug.Log($"[ProjectSetup] Layer '{layerName}' already exists at index {i}.");
                    return;
                }
            }

            // User-defined layers start at index 8 (0–7 are reserved by Unity).
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    Debug.Log($"[ProjectSetup] Added layer '{layerName}' at index {i}.");
                    return;
                }
            }

            Debug.LogWarning($"[ProjectSetup] Could not add layer '{layerName}': all 32 layer slots are in use.");
        }
    }
}
