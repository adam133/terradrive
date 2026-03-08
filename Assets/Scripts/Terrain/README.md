# Terrain Scripts

Elevation data sources for applying real-world DEM (Digital Elevation Model) heights to road
splines and building footprints, and a terrain mesh generator.

| File | Purpose |
|---|---|
| `IElevationSource.cs` | Interface for pluggable DEM backends |
| `OpenElevationSource.cs` | SRTM 30 m elevation via the [Open-Elevation API](https://open-elevation.com) (no API key required) |
| `ElevationGrid.cs` | Regular lat/lon grid of DEM samples; `SampleAsync` batch-fetches from any `IElevationSource`; `SampleElevation` bilinearly interpolates at arbitrary coordinates; also implements `IElevationSource` for scene wiring |
| `TerrainMeshGenerator.cs` | Generates a UV-mapped heightfield mesh from an `ElevationGrid` |
| `TerrainMeshResult.cs` | Container returned by `TerrainMeshGenerator.Generate` (vertices, triangles, UVs) |

## IElevationSource

```csharp
IReadOnlyList<double> elevations = await source.FetchElevationsAsync(
    new[] { (51.5074, -0.1278), (48.8566, 2.3522) });
```

All implementations return elevations in the same order as the supplied locations list so
callers can correlate results by index.

## OpenElevationSource

Uses the public Open-Elevation REST API (backed by SRTM data) to resolve a batch of
geographic coordinates in a single POST request.

```csharp
var source = new OpenElevationSource();                    // uses public endpoint
var source = new OpenElevationSource(httpClient, url);     // inject client / self-hosted endpoint
IReadOnlyList<double> elevations = await source.FetchElevationsAsync(locations);
```

Pass the returned elevation values to `CoordinateConverter.LatLonToUnity(lat, lon, elevMetres)`
to set the Unity Y axis from real-world terrain heights.

### Why Open-Elevation / SRTM?

- **Free, no API key** — unlike Cesium ion or commercial services.
- **Global coverage** — SRTM covers land between ±60° latitude (virtually all driving areas).
- **30 m resolution** — adequate for road and terrain mesh generation.
- **Self-hostable** — swap the default endpoint for a private instance for offline use.

## ElevationGrid

A regular lat/lon grid of terrain elevation samples.  Construct it directly from a 2-D array
or use the async factory to populate it from any `IElevationSource`:

```csharp
ElevationGrid grid = await ElevationGrid.SampleAsync(
    minLat, maxLat, minLon, maxLon, rows: 64, cols: 64, elevationSource);

double elevMetres = grid[row, col];
double lat = grid.LatAtRow(row);
double lon = grid.LonAtCol(col);
```

### SampleElevation

Returns the terrain height in metres at any arbitrary geographic coordinate via bilinear
interpolation across the four nearest grid cells.  Coordinates outside the grid extent are
clamped to the nearest boundary:

```csharp
double elev = grid.SampleElevation(lat, lon);
```

### ElevationGrid as IElevationSource (scene wiring)

`ElevationGrid` implements `IElevationSource`, so a pre-built terrain grid can be passed
directly to any API that accepts an elevation source — most importantly `OSMParser.ParseAsync`
— to raise road splines and building footprints to match the terrain surface **without
issuing additional network requests**:

```csharp
// 1. Build terrain grid (one network request)
ElevationGrid grid = await ElevationGrid.SampleAsync(
    minLat, maxLat, minLon, maxLon, rows: 64, cols: 64,
    new OpenElevationSource());

// 2. Generate terrain mesh
TerrainMeshResult terrain = TerrainMeshGenerator.Generate(grid, originLat, originLon);

// 3. Parse OSM — road nodes and building corners are lifted to terrain height.
//    The same grid is reused as the IElevationSource; no extra HTTP calls.
var (roads, buildings, region) = await OSMParser.ParseAsync(
    "Assets/Data/london.osm", originLat, originLon, grid);
```

## TerrainMeshGenerator

Generates a Unity-compatible heightfield mesh from an `ElevationGrid`.  Each grid cell becomes
a quad split into two triangles; vertex Y coordinates are set from the DEM elevations via
`CoordinateConverter`.

```csharp
TerrainMeshResult result = TerrainMeshGenerator.Generate(grid, originLat, originLon);

var mesh = new Mesh();
mesh.vertices  = result.Vertices;
mesh.triangles = result.Triangles;
mesh.uv        = result.UVs;
mesh.RecalculateNormals();
GetComponent<MeshFilter>().sharedMesh = mesh;
```
