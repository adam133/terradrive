using System.IO;
using NUnit.Framework;
using TerraDrive.Core;
using TerraDrive.DataInversion;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Tests that parse the <c>Assets/StreamingAssets/Data/map.osm.xml</c> sample file to verify
    /// the OSM parser handles real-world data correctly.
    /// </summary>
    [TestFixture]
    public class OSMParserRealDataTests
    {
        // Ames, Iowa — the origin used for the bundled test map.
        private const double OriginLat =  41.8957;
        private const double OriginLon = -93.5888;

        // ── helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Locates <c>Assets/StreamingAssets/Data/map.osm.xml</c> by walking up the directory tree
        /// from the test assembly location.
        /// </summary>
        private static string FindOsmMapFile()
        {
            string dir = Path.GetDirectoryName(
                typeof(OSMParserRealDataTests).Assembly.Location)
                ?? Directory.GetCurrentDirectory();

            for (int depth = 0; depth < 8; depth++)
            {
                string candidate = Path.Combine(dir, "Assets", "StreamingAssets", "Data", "map.osm.xml");
                if (File.Exists(candidate))
                    return candidate;

                string? parent = Path.GetDirectoryName(dir);
                if (parent == null || parent == dir) break;
                dir = parent;
            }

            throw new FileNotFoundException(
                "Could not locate Assets/StreamingAssets/Data/map.osm.xml in the repository tree.");
        }

        // ── tests ──────────────────────────────────────────────────────────────

        [Test]
        [Description("Verifies the sample OSM file contains a reasonable number of " +
                     "roads and buildings — a sanity check on the parser for real data.")]
        public void Parse_SampleOsmMap_ContainsExpectedFeatures()
        {
            string osmPath = FindOsmMapFile();

            CoordinateConverter.ResetWorldOrigin();
            var (roads, buildings, _, _) = OSMParser.Parse(osmPath, OriginLat, OriginLon);

            Assert.That(roads.Count,     Is.GreaterThan(10), "Should find at least 10 roads");
            Assert.That(buildings.Count, Is.GreaterThan(0),  "Should find at least 1 building");
        }
    }
}
