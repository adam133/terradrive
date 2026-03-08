# Tools

Editor and command-line utilities for the TerraDrive pipeline.

---

## OsmDownloader

Downloads road and building data from the [Overpass API](https://overpass-api.de/) for a given GPS coordinate and radius, then saves the result as an `.osm` file for use in Unity.

Optionally, pass `--elevation` to also download a DEM elevation grid for the same bounding box (via the [Open-Elevation API](https://open-elevation.com), backed by SRTM 30 m data) and save it as a companion `.elevation.csv` file.

### Build

```bash
dotnet build Tools/OsmDownloader/OsmDownloader.csproj
```

### Usage

```bash
dotnet run --project Tools/OsmDownloader -- --lat <latitude> --lon <longitude> [--radius <metres>] [--output <path>] [--elevation] [--dem-rows <n>] [--dem-cols <n>]
```

Or run the compiled binary directly after publishing:

```bash
dotnet publish Tools/OsmDownloader/OsmDownloader.csproj -c Release -o Tools/OsmDownloader/publish
Tools/OsmDownloader/publish/OsmDownloader --lat <latitude> --lon <longitude> [--radius <metres>] [--output <path>]
```

| Argument | Default | Description |
|---|---|---|
| `--lat` | *(required)* | Centre latitude (WGS-84) |
| `--lon` | *(required)* | Centre longitude (WGS-84) |
| `--radius` | `5000` | Search radius in metres |
| `--output` | `output.osm` | Path to write the `.osm` file |
| `--elevation` | *(off)* | Also download and save DEM elevation data alongside the `.osm` |
| `--dem-rows` | `32` | Latitude samples in the elevation grid (min: 2) |
| `--dem-cols` | `32` | Longitude samples in the elevation grid (min: 2) |

### Examples

```bash
# Download 5 km of roads around central London
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm

# Download 2 km of roads around Shibuya, Tokyo
dotnet run --project Tools/OsmDownloader -- --lat 35.6595 --lon 139.7004 --radius 2000 --output ../Assets/Data/tokyo_shibuya.osm

# Download roads AND elevation data (saves london.osm + london.elevation.csv)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm --elevation

# Higher-resolution elevation grid (64×64 instead of the default 32×32)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm --elevation --dem-rows 64 --dem-cols 64
```

### Output

The tool writes a standard `.osm` XML file to the path specified by `--output` (directories are created automatically).

When `--elevation` is supplied, a companion `<name>.elevation.csv` file is written to the same directory.  The CSV format is:

```
minLat,maxLat,minLon,maxLon,rows,cols
val[0,0],val[0,1],...,val[0,cols-1]
val[1,0],val[1,1],...,val[1,cols-1]
...
val[rows-1,0],...,val[rows-1,cols-1]
```

Row 0 is the southern edge of the bounding box; the last row is the northern edge.  Load the file at runtime with `OsmDownloader.LoadElevationGrid(path)` to get an `ElevationGrid` object ready for use with `TerrainMeshGenerator.Generate` and `OSMParser.ParseAsync`.

### Overpass Query

The downloader uses the following Overpass QL template, which fetches:

- All **highway** ways (roads, paths, etc.)
- All **building** ways (footprints for procedural extrusion)
- All nodes referenced by those ways

```
[out:xml][timeout:90];
(
  way["highway"](around:<radius>,<lat>,<lon>);
  way["building"](around:<radius>,<lat>,<lon>);
);
(._;>;);
out body;
```

### Elevation / DEM

When `--elevation` is specified, the tool:

1. Computes a bounding box that encloses the circular download area using `OsmDownloader.ComputeBoundingBox`.
2. Samples a `rows × cols` grid of geographic coordinates that span the bounding box.
3. Sends the full batch to the [Open-Elevation REST API](https://open-elevation.com) (one POST, no API key required) and receives SRTM 30 m elevation values.
4. Saves the resulting `ElevationGrid` as a CSV file via `OsmDownloader.SaveElevation`.

The public API also supports self-hosted Open-Elevation instances and custom `IElevationSource` implementations — pass an `elevationSource` parameter to `OsmDownloader.DownloadElevationGridAsync` to override the default.
