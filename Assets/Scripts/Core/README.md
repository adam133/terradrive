# Core Scripts

Game-wide managers, state machines, coordinate utilities, and the map-loading pipeline.

| File | Purpose |
|---|---|
| `GameManager.cs` | Singleton entry-point; owns the high-level game state machine |
| `CoordinateConverter.cs` | Converts WGS-84 GPS coordinates to Unity world-space XZ metres |
| `MapLoader.cs` | End-to-end async pipeline: loads `.osm` + `.elevation.csv`, parses with terrain elevation, returns a `MapData` object |
| `MapData.cs` | Container holding roads, buildings, region type, terrain mesh, and elevation grid |

## CoordinateConverter

Call `CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon)` to project any GPS
coordinate relative to a map origin into Unity world-space metres.  The origin is typically the
centre coordinate passed to the Overpass downloader.

## GameManager

`GameManager.Instance` exposes the current `GameState` enum and fires `OnStateChanged` events so
other systems can react without tight coupling.

## MapLoader

```csharp
MapData map = await MapLoader.LoadMapAsync(
    "Assets/Data/london.osm",
    "Assets/Data/london.elevation.csv",
    originLat: 51.5074, originLon: -0.1278);

// Roads and buildings have their Y coordinates set to terrain elevation
foreach (RoadSegment road in map.Roads)  { /* extrude mesh */ }
foreach (BuildingFootprint b in map.Buildings) { /* extrude mesh */ }

// Terrain mesh is ready for direct Unity Mesh assignment
mesh.vertices  = map.TerrainMesh.Vertices;
mesh.triangles = map.TerrainMesh.Triangles;
mesh.uv        = map.TerrainMesh.UVs;
mesh.RecalculateNormals();
```

The pipeline is: load elevation grid → parse OSM with terrain elevation → generate heightfield
terrain mesh.  All results are returned in `MapData` which also exposes the raw `ElevationGrid`.
