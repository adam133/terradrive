using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VectorRoad.Core;

namespace VectorRoad.Tests.PlayMode
{
    /// <summary>
    /// Play-mode test that loads the default <c>ProofOfConcept</c> scene, waits
    /// for the map-build pipeline to reach the <see cref="GameState.Racing"/>
    /// state, then renders a PNG screenshot to <c>Screenshots/pr-preview.png</c>
    /// at the project root.
    ///
    /// <para>
    /// Designed to run in GitHub Actions via the <c>pr-preview.yml</c> workflow.
    /// The screenshot artifact is uploaded and linked in a PR comment so
    /// reviewers can see the rendered result at a glance.
    /// </para>
    ///
    /// <para>
    /// The startup menu is bypassed automatically by advancing the
    /// <see cref="GameManager"/> state to <see cref="GameState.LoadingMap"/>
    /// immediately after the scene loads.
    /// </para>
    /// </summary>
    public class SceneScreenshotTests
    {
        private const string SceneName = "ProofOfConcept";
        private const int ScreenshotWidth = 1920;
        private const int ScreenshotHeight = 1080;

        /// <summary>
        /// Loads the default location, waits for level generation to complete,
        /// and saves a screenshot to <c>Screenshots/pr-preview.png</c>.
        /// </summary>
        [UnityTest]
        [Timeout(300000)] // 5 minutes – map build can take a while in CI
        public IEnumerator DefaultLocation_RendersScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName);

            // Allow Awake/Start to run on all objects in the loaded scene.
            yield return null;

            // The MapSceneBuilder waits for the GameManager to leave MainMenu
            // before it starts loading map data.  Advance the state here to
            // skip the interactive startup menu in automated runs.
            var gm = GameManager.Instance;
            if (gm != null && gm.CurrentState == GameState.MainMenu)
                gm.SetState(GameState.LoadingMap);

            // Wait until the map build pipeline signals that the level is ready.
            float elapsed = 0f;
            const float mapLoadTimeout = 240f; // seconds
            while (elapsed < mapLoadTimeout)
            {
                var instance = GameManager.Instance;
                if (instance == null)
                    Assert.Fail("GameManager.Instance became null while waiting for map load.");
                if (instance.CurrentState == GameState.Racing)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Find any active camera to render from.  Camera.main returns the
            // camera tagged "MainCamera", which is the expected render camera in
            // the ProofOfConcept scene.  FindFirstObjectByType is a safe fallback
            // for scenes where the main camera tag has not been set.
            var camera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(camera, "No Camera was found in the scene.");

            // Render the scene to a RenderTexture so the capture works reliably
            // in headless / batch mode (no display required).
            var rt = new RenderTexture(ScreenshotWidth, ScreenshotHeight, 24);
            var prevTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();

            var tex = new Texture2D(ScreenshotWidth, ScreenshotHeight,
                TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, ScreenshotWidth, ScreenshotHeight), 0, 0);
            tex.Apply();

            // Save to <project root>/Screenshots/pr-preview.png so the workflow
            // can locate and upload the file as an artifact.
            string screenshotDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(screenshotDir);
            string screenshotPath = Path.Combine(screenshotDir, "pr-preview.png");
            File.WriteAllBytes(screenshotPath, tex.EncodeToPNG());

            // Restore state and release GPU resources.
            camera.targetTexture = prevTarget;
            RenderTexture.active = null;
            Object.Destroy(rt);
            Object.Destroy(tex);

            Assert.IsTrue(File.Exists(screenshotPath),
                $"Screenshot was not saved to {screenshotPath}");
        }
    }
}
