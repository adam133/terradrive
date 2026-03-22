using System;
using System.Net.Http;
using System.Threading.Tasks;
using VectorRoad.Terrain;
using VectorRoad.Tools;

/// <summary>
/// Entry point for the OsmDownloader command-line tool.
///
/// Usage:
///   OsmDownloader --lat &lt;latitude&gt; --lon &lt;longitude&gt; [--radius &lt;metres&gt;] [--output &lt;path&gt;]
///                [--no-elevation] [--dem-rows &lt;n&gt;] [--dem-cols &lt;n&gt;]
///
/// Example:
///   OsmDownloader --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        double? lat        = null;
        double? lon        = null;
        int     radius     = 5000;
        string  output     = "output.osm";
        bool    elevation  = true;   // elevation is downloaded by default; suppress with --no-elevation
        int     demRows    = 0;      // 0 = auto-compute from SRTM 30 m spacing
        int     demCols    = 0;      // 0 = auto-compute from SRTM 30 m spacing

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--lat" when i + 1 < args.Length:
                    if (!double.TryParse(args[++i],
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double latVal))
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --lat: {args[i]}");
                        return 1;
                    }
                    lat = latVal;
                    break;

                case "--lon" when i + 1 < args.Length:
                    if (!double.TryParse(args[++i],
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double lonVal))
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --lon: {args[i]}");
                        return 1;
                    }
                    lon = lonVal;
                    break;

                case "--radius" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int radiusVal) || radiusVal <= 0)
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --radius: {args[i]}");
                        return 1;
                    }
                    radius = radiusVal;
                    break;

                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;

                case "--no-elevation":
                    elevation = false;
                    break;

                case "--elevation":
                    elevation = true;
                    break;

                case "--dem-rows" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int rowsVal) || rowsVal < 0)
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --dem-rows: {args[i]} (must be ≥ 0)");
                        return 1;
                    }
                    demRows = rowsVal;
                    break;

                case "--dem-cols" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int colsVal) || colsVal < 0)
                    {
                        Console.Error.WriteLine($"ERROR: Invalid value for --dem-cols: {args[i]} (must be ≥ 0)");
                        return 1;
                    }
                    demCols = colsVal;
                    break;

                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;

                default:
                    Console.Error.WriteLine($"ERROR: Unknown argument: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        if (lat is null || lon is null)
        {
            Console.Error.WriteLine("ERROR: --lat and --lon are required.");
            PrintUsage();
            return 1;
        }

        var downloader = new OsmDownloader();
        try
        {
            // ── OSM download ─────────────────────────────────────────────────
            string content = await downloader.DownloadOsmAsync(lat.Value, lon.Value, radius);
            OsmDownloader.SaveOsm(content, output);

            // ── Elevation / DEM download (on by default) ─────────────────────
            if (elevation)
            {
                string elevOutput = DeriveElevationPath(output);
                ElevationGrid grid = await downloader.DownloadElevationGridAsync(
                    lat.Value, lon.Value, radius, demRows, demCols);
                OsmDownloader.SaveElevation(grid, elevOutput);
            }

            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"ERROR: HTTP request failed: {ex.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("ERROR: Request was cancelled.");
            return 1;
        }
    }

    /// <summary>
    /// Derives the elevation CSV output path from the OSM output path by replacing
    /// the <c>.osm</c> extension with <c>.elevation.csv</c>, or appending
    /// <c>.elevation.csv</c> when the path has a different extension.
    /// </summary>
    internal static string DeriveElevationPath(string osmPath)
    {
        string dir  = System.IO.Path.GetDirectoryName(osmPath) ?? string.Empty;
        string name = System.IO.Path.GetFileNameWithoutExtension(osmPath);
        return System.IO.Path.Combine(dir, name + ".elevation.csv");
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "Usage: OsmDownloader --lat <latitude> --lon <longitude> " +
            "[--radius <metres>] [--output <path>] [--no-elevation] " +
            "[--dem-rows <n>] [--dem-cols <n>]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --lat           Centre latitude in decimal degrees (WGS-84, required)");
        Console.WriteLine("  --lon           Centre longitude in decimal degrees (WGS-84, required)");
        Console.WriteLine("  --radius        Search radius in metres (default: 5000)");
        Console.WriteLine("  --output        Output .osm file path (default: output.osm)");
        Console.WriteLine("  --no-elevation  Skip the DEM elevation download (elevation is included by default)");
        Console.WriteLine($"  --dem-rows      Latitude samples in the elevation grid (0 = auto from SRTM {OsmDownloader.SrtmSpacingMetres:F0} m spacing, default: 0)");
        Console.WriteLine($"  --dem-cols      Longitude samples in the elevation grid (0 = auto from SRTM {OsmDownloader.SrtmSpacingMetres:F0} m spacing, default: 0)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Download OSM + elevation at full SRTM 30 m resolution (default behaviour)");
        Console.WriteLine("  OsmDownloader --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm");
        Console.WriteLine("  # Skip elevation download");
        Console.WriteLine("  OsmDownloader --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm --no-elevation");
        Console.WriteLine("  # Override grid size explicitly");
        Console.WriteLine("  OsmDownloader --lat 35.6595 --lon 139.7004 --radius 2000 --output ../Assets/Data/tokyo_shibuya.osm --dem-rows 64 --dem-cols 64");
    }
}
