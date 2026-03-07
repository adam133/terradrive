using System;
using System.IO;
using NUnit.Framework;
using TerraDrive.Core;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Integration tests that parse the real <c>Assets/Data/map.osm.xml</c> sample
    /// file, render a top-down PNG preview with <see cref="MapRenderer"/>, and save
    /// it so that the CI workflow can upload it as a pull-request artifact.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class MapRendererIntegrationTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Walks up from the test-binary directory until it finds
        /// <c>Assets/Data/map.osm.xml</c>, matching the repository layout regardless
        /// of the build configuration (Debug / Release) or working directory.
        /// </summary>
        private static string FindOsmMapFile()
        {
            string dir = Path.GetDirectoryName(
                typeof(MapRendererIntegrationTests).Assembly.Location)
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
                "Could not locate Assets/Data/map.osm.xml relative to the test binary. " +
                "Ensure the repository is checked out in full.");
        }

        // ── tests ──────────────────────────────────────────────────────────────

        [Test]
        [Description("Loads the sample OSM file, renders a map preview PNG, and saves " +
                     "it for the CI artifact upload step.")]
        public void Render_SampleOsmMap_ProducesPngFile()
        {
            // ── Arrange ────────────────────────────────────────────────────────
            string osmPath = FindOsmMapFile();

            // Centre of the map bounds (minlat=41.8739, maxlat=41.9175,
            //                           minlon=-93.6240, maxlon=-93.5535)
            const double originLat =  41.8957;
            const double originLon = -93.5888;

            CoordinateConverter.ResetWorldOrigin();
            var (roads, buildings, _) = OSMParser.Parse(osmPath, originLat, originLon);

            // ── Act ────────────────────────────────────────────────────────────
            using var bitmap = MapRenderer.Render(roads, buildings);

            string outputDir = Environment.GetEnvironmentVariable("MAP_PREVIEW_DIR")
                ?? TestContext.CurrentContext.WorkDirectory;
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "map-preview.png");

            MapRenderer.SaveAsPng(bitmap, outputPath);

            // ── Assert ─────────────────────────────────────────────────────────
            Assert.That(File.Exists(outputPath), Is.True,
                $"Expected map-preview.png at: {outputPath}");

            long fileSize = new FileInfo(outputPath).Length;
            Assert.That(fileSize, Is.GreaterThan(10_000L),
                "Rendered PNG should be larger than 10 KB");

            // Dimensions
            Assert.That(bitmap.Width,  Is.EqualTo(1200));
            Assert.That(bitmap.Height, Is.EqualTo(900));

            TestContext.Out.WriteLine($"Map preview written to: {outputPath}");
            TestContext.Out.WriteLine($"  Roads parsed:     {roads.Count}");
            TestContext.Out.WriteLine($"  Buildings parsed: {buildings.Count}");
            TestContext.Out.WriteLine($"  PNG size:         {fileSize / 1024} KB");
        }

        [Test]
        [Description("Verifies the sample OSM file contains a reasonable number of " +
                     "roads and buildings — a sanity check on the parser for real data.")]
        public void Parse_SampleOsmMap_ContainsExpectedFeatures()
        {
            string osmPath = FindOsmMapFile();

            CoordinateConverter.ResetWorldOrigin();
            var (roads, buildings, _) = OSMParser.Parse(osmPath, 41.8957, -93.5888);

            Assert.That(roads.Count,     Is.GreaterThan(10), "Should find at least 10 roads");
            Assert.That(buildings.Count, Is.GreaterThan(0),  "Should find at least 1 building");
        }
    }
}
