using System;
using System.IO;
using NUnit.Framework;
using TerraDrive.Core;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Integration tests that render a perspective chase-cam screenshot from a
    /// static position on the sample map and save it so the CI workflow can upload
    /// it as a pull-request artifact.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class ChaseCamIntegrationTests
    {
        // Downtown Des Moines, IA — used as the world origin for all integration tests.
        private const double OriginLat =  41.587881;
        private const double OriginLon = -93.620142;

        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the path to the OSM map file.
        /// In CI the <c>OSM_MAP_PATH</c> environment variable is set to the file
        /// downloaded by <c>osm_downloader.py</c> so that the integration test uses
        /// live API data rather than the static repo file.
        /// When running locally the method falls back to locating
        /// <c>Assets/Data/map.osm.xml</c> in the repository tree.
        /// </summary>
        private static string FindOsmMapFile()
        {
            string? envPath = Environment.GetEnvironmentVariable("OSM_MAP_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                if (!File.Exists(envPath))
                    throw new FileNotFoundException(
                        $"OSM_MAP_PATH is set but the file does not exist: {envPath}");
                return envPath;
            }

            string dir = Path.GetDirectoryName(
                typeof(ChaseCamIntegrationTests).Assembly.Location)
                ?? Directory.GetCurrentDirectory();

            for (int depth = 0; depth < 8; depth++)
            {
                string candidate = Path.Combine(dir, "Assets", "Data", "map.osm.xml");
                if (File.Exists(candidate))
                    return candidate;

                string? parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }

            throw new FileNotFoundException(
                "Could not locate the OSM map file. Set the OSM_MAP_PATH environment " +
                "variable or ensure Assets/Data/map.osm.xml exists in the repository.");
        }

        // ── tests ──────────────────────────────────────────────────────────────

        [Test]
        [Description("Parses the sample OSM file, renders a perspective chase-cam PNG from " +
                     "a static position at the map centre, and saves it for the CI artifact upload.")]
        public void Render_ChaseCamView_ProducesPngFile()
        {
            // ── Arrange ────────────────────────────────────────────────────────
            string osmPath = FindOsmMapFile();

            // Centre of the map at downtown Des Moines, IA
            const double originLat = OriginLat;
            const double originLon = OriginLon;

            CoordinateConverter.ResetWorldOrigin();
            var (roads, buildings, _, _) = OSMParser.Parse(osmPath, originLat, originLon);

            // Static camera position: the car is at world origin (0, 0) — the
            // centre of the test map — facing forward (+Z direction).
            const float carX = 0f;
            const float carZ = 0f;

            // ── Act ────────────────────────────────────────────────────────────
            using var bitmap = ChaseCamRenderer.Render(roads, buildings, carX, carZ);

            string outputDir = Environment.GetEnvironmentVariable("CHASE_CAM_PREVIEW_DIR")
                ?? TestContext.CurrentContext.WorkDirectory;
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "chase-cam-preview.png");

            ChaseCamRenderer.SaveAsPng(bitmap, outputPath);

            // ── Assert ─────────────────────────────────────────────────────────
            Assert.That(File.Exists(outputPath), Is.True,
                $"Expected chase-cam-preview.png at: {outputPath}");

            long fileSize = new FileInfo(outputPath).Length;
            Assert.That(fileSize, Is.GreaterThan(10_000L),
                "Rendered PNG should be larger than 10 KB");

            Assert.That(bitmap.Width,  Is.EqualTo(1600));
            Assert.That(bitmap.Height, Is.EqualTo(900));

            TestContext.Out.WriteLine($"Chase-cam preview written to: {outputPath}");
            TestContext.Out.WriteLine($"  Roads parsed:     {roads.Count}");
            TestContext.Out.WriteLine($"  Buildings parsed: {buildings.Count}");
            TestContext.Out.WriteLine($"  PNG size:         {fileSize / 1024} KB");
        }
    }
}
