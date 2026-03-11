# Core Scripts

Game-wide managers, state machines, coordinate utilities, and the map-loading pipeline.

| File | Purpose |
|---|---|
| `GameManager.cs` | Singleton entry-point; owns the high-level game state machine |
| `CoordinateConverter.cs` | Converts WGS-84 GPS coordinates to Unity world-space XZ metres |
| `MapLoader.cs` | End-to-end async pipeline: loads `.osm` + `.elevation.csv`, parses with terrain elevation, returns a `MapData` object |
| `MapData.cs` | Container holding roads, buildings, water bodies, region type, terrain mesh, and elevation grid |
| `MapSceneBuilder.cs` | Unity MonoBehaviour that drives `MapLoader` at runtime and instantiates terrain, road, building, and water GameObjects in the scene |
| `OsmLevelLoader.cs` | Pure C# GPS-coordinate settings/validator for the **TerraDrive → Load OSM File / Generate Level** editor menu item; holds `Latitude`, `Longitude`, `Radius` and exposes `Validate()` / `IsValid()` |
| `LocationMenuController.cs` | Downloads OSM + elevation data for a GPS coordinate and generates the full `MapData` via `MapLoader`; entry point for an in-game "change location" menu |
| `LocationLoadResult.cs` | Result returned by `LocationMenuController.LoadLocationAsync`: contains `MapData`, `OriginLatitude`, `OriginLongitude`, and `PlayerSpawnPosition` (always world origin) |

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
foreach (WaterBody water in map.WaterBodies) { /* generate flat surface mesh */ }

// Terrain mesh is ready for direct Unity Mesh assignment
mesh.vertices  = map.TerrainMesh.Vertices;
mesh.triangles = map.TerrainMesh.Triangles;
mesh.uv        = map.TerrainMesh.UVs;
mesh.RecalculateNormals();
```

The pipeline is: load elevation grid → parse OSM with terrain elevation → generate heightfield
terrain mesh.  All results are returned in `MapData` which also exposes the raw `ElevationGrid`.

## MapSceneBuilder

`MapSceneBuilder` is a Unity `MonoBehaviour` that wires the full data pipeline into a running
scene.  Add it to any scene GameObject, configure the paths in the Inspector, and press Play:

| Inspector field | Default | Notes |
|---|---|---|
| `OsmFilePath` | `Assets/Data/map.osm.xml` | Path to the `.osm` file (absolute or project-root-relative) |
| `ElevationCsvPath` | `Assets/Data/map.elevation.csv` | Companion `.elevation.csv` file |
| `OriginLatitude` | `0` | Map origin latitude; `0` inherits from `GameManager` |
| `OriginLongitude` | `0` | Map origin longitude; `0` inherits from `GameManager` |
| `Registry` | *(scene ref)* | `MaterialRegistry` used to apply materials to generated meshes |
| `Vehicle` | *(optional)* | Vehicle `Transform` to position at the map origin after loading |
| `VehicleSpawnHeight` | `2` | Height above origin (metres) at which the vehicle is placed |

At runtime `MapSceneBuilder` drives the `GameManager` state machine:

```
MainMenu → LoadingMap → GeneratingLevel → Racing
```

One `GameObject` is spawned per road segment (with `Surface` and `Kerb` children), per
building footprint (with `Walls` and `Roof` children), and per water body (a single flat
`Water_<wayId>` mesh on the `Water` layer).  A single `Terrain` GameObject carries
the heightfield mesh and a `MeshCollider`.

The `ProofOfConcept.unity` scene ships with `MapSceneBuilder` pre-wired to the bundled
`Assets/Data/map.osm.xml` + `Assets/Data/map.elevation.csv` sample data (Ames, Iowa).

## LocationMenuController

`LocationMenuController` encapsulates the logic for choosing a new real-world location
from a menu UI.  Set the `Latitude`, `Longitude`, and (optionally) `Radius` properties,
then call `LoadLocationAsync` — it downloads OSM + elevation data, resets the coordinate
origin, and returns a `LocationLoadResult` with the fully generated `MapData`.

```csharp
var menu = new LocationMenuController();   // uses default OsmDownloader + temp directory
menu.Latitude  = 48.8566;
menu.Longitude =  2.3522;

LocationLoadResult result = await menu.LoadLocationAsync();

// Move the player car to the new map origin:
playerTransform.position = result.PlayerSpawnPosition;   // always (0, 0, 0)

// Keep GameManager in sync:
GameManager.Instance.SetLocation(result.OriginLatitude, result.OriginLongitude);
```

Inject a custom `IOsmDownloader` and data directory via the constructor for testing or
to control where downloaded files are cached.
