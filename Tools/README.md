# Tools

Editor and command-line utilities for the VectorRoad pipeline.

---

## OsmDownloader

Downloads road and building data from the [Overpass API](https://overpass-api.de/) for a given GPS coordinate and radius, then saves the result as an `.osm` file for use in Unity.

Implementation note: the `OsmDownloader` and `IOsmDownloader` source files are in
`Assets/Scripts/Tools/` (shared by Unity runtime, tests, and this CLI).  The
`Tools/OsmDownloader/` folder contains the CLI entrypoint (`Program.cs`) and
`OsmDownloader.csproj`.

By default, a DEM elevation grid is also downloaded for the same bounding box (via the [Open-Elevation API](https://open-elevation.com), backed by SRTM 30 m data) and saved as a companion `.elevation.csv` file.  Pass `--no-elevation` to suppress the elevation download.

### Build

```bash
dotnet build Tools/OsmDownloader/OsmDownloader.csproj
```

### Usage

```bash
dotnet run --project Tools/OsmDownloader -- --lat <latitude> --lon <longitude> [--radius <metres>] [--output <path>] [--no-elevation] [--dem-rows <n>] [--dem-cols <n>]
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
| `--no-elevation` | *(elevation on)* | Skip the DEM elevation download |
| `--dem-rows` | `32` | Latitude samples in the elevation grid (min: 2) |
| `--dem-cols` | `32` | Longitude samples in the elevation grid (min: 2) |

### Examples

```bash
# Download 5 km of roads around central London — also downloads london.elevation.csv (default)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm

# Download 2 km of roads around Shibuya, Tokyo — also downloads tokyo_shibuya.elevation.csv
dotnet run --project Tools/OsmDownloader -- --lat 35.6595 --lon 139.7004 --radius 2000 --output ../Assets/Data/tokyo_shibuya.osm

# Skip the elevation download
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm --no-elevation

# Higher-resolution elevation grid (64×64 instead of the default 32×32)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm --dem-rows 64 --dem-cols 64
```

### Output

The tool writes a standard `.osm` XML file to the path specified by `--output` (directories are created automatically).

By default, a companion `<name>.elevation.csv` file is written to the same directory.  The CSV format is:

```
minLat,maxLat,minLon,maxLon,rows,cols
val[0,0],val[0,1],...,val[0,cols-1]
val[1,0],val[1,1],...,val[1,cols-1]
...
val[rows-1,0],...,val[rows-1,cols-1]
```

Row 0 is the southern edge of the bounding box; the last row is the northern edge.  Load the file at runtime with `ElevationGrid.Load(path)` (or via the convenience wrapper `OsmDownloader.LoadElevationGrid(path)`) to get an `ElevationGrid` ready for use.

### Loading at runtime with `MapLoader`

The recommended way to consume the downloaded files in Unity (or in a .NET service) is through `MapLoader.LoadMapAsync`:

```csharp
MapData map = await MapLoader.LoadMapAsync(
    "Assets/Data/london.osm",
    "Assets/Data/london.elevation.csv",
    originLat: 51.5074, originLon: -0.1278);

// Roads and buildings: every node's Y is lifted to the terrain elevation
foreach (RoadSegment road in map.Roads)    { /* RoadMeshExtruder.ExtrudeWithDetails */ }
foreach (BuildingFootprint b in map.Buildings) { /* BuildingGenerator.Extrude */ }

// Terrain mesh: assign directly to a Unity Mesh
mesh.vertices  = map.TerrainMesh.Vertices;
mesh.triangles = map.TerrainMesh.Triangles;
mesh.uv        = map.TerrainMesh.UVs;
mesh.RecalculateNormals();
```

`MapLoader.LoadMapAsync` internally:
1. Loads the `.elevation.csv` via `ElevationGrid.Load`.
2. Passes the `ElevationGrid` as an `IElevationSource` to `OSMParser.ParseAsync` — lifting every road and building node's Y coordinate to match the terrain, without any additional HTTP requests.
3. Calls `TerrainMeshGenerator.Generate` with the same grid to produce the heightfield mesh.
4. Returns all results together in a `MapData` object.

### Overpass Query

The downloader uses the following Overpass QL template, which fetches:

- All **highway** ways (roads, paths, etc.)
- All **building** ways (footprints for procedural extrusion)
- All **waterway** ways (rivers, canals, docks)
- All **natural=water** ways (lakes, ponds, reservoirs)
- All **water** ways (additional water polygon variants)
- All nodes referenced by those ways

```
[out:xml][timeout:90];
(
  way["highway"](around:<radius>,<lat>,<lon>);
  way["building"](around:<radius>,<lat>,<lon>);
  way["waterway"](around:<radius>,<lat>,<lon>);
  way["natural"="water"](around:<radius>,<lat>,<lon>);
  way["water"](around:<radius>,<lat>,<lon>);
);
(._;>;);
out body;
```

### Elevation / DEM

Elevation is downloaded by default alongside every `.osm` file.  The tool:

1. Computes a bounding box that encloses the circular download area using `OsmDownloader.ComputeBoundingBox`.
2. Samples a `rows × cols` grid of geographic coordinates that span the bounding box.
3. Sends the full batch to the [Open-Elevation REST API](https://open-elevation.com) (one POST, no API key required) and receives SRTM 30 m elevation values.
4. Saves the resulting `ElevationGrid` as a CSV file via `OsmDownloader.SaveElevation`.

Pass `--no-elevation` to skip this step entirely.  The public API also supports self-hosted Open-Elevation instances and custom `IElevationSource` implementations — pass an `elevationSource` parameter to `OsmDownloader.DownloadElevationGridAsync` to override the default.
