# TerraDrive

A semi-realistic racing game built on real-world roads using OpenStreetMap data and locally generated 3D assets.

---

## Vision

TerraDrive lets players race on procedurally generated tracks derived from actual GPS road data. Every road surface, building, and piece of roadside scenery is generated at runtime from real-world sources — so every location on Earth is a potential race track.

---

## Tech Stack

| Feature | Recommended Tool |
|---|---|
| Map Data | [OpenStreetMap (OSM)](https://www.openstreetmap.org/) / [Overpass API](https://overpass-api.de/) |
| Terrain / 3D Tiles | [Cesium ion](https://cesium.com/platform/cesium-ion/) |
| Game Engine | Unity 6.3 LTS (current implementation) / Unreal Engine 5 (PCG + Nanite, production goal) |
| Road Systems | Houdini Engine (road-to-mesh conversion) |
| Asset Variation | [Synty Studios](https://syntystore.com/) low-poly kits / [Quixel Megascans](https://quixel.com/megascans) |

---

## Project Structure

```
/TerraDrive
  /Assets
    /Scripts
      /Core          ← Game managers, state machines, coordinate helpers
      /DataInversion ← OSM / DEM parsing logic
      /Editor        ← Unity Editor-only scripts (ProjectSetup, batch-mode helpers)
      /Procedural    ← Mesh generation (Roads, Buildings, Props)
      /Terrain       ← Elevation data sources (IElevationSource, OpenElevationSource)
      /Vehicle       ← Physics, controls, and chase camera
    /Prefabs         ← Asset kits (Signs, Houses, Foliage)
    /Shaders         ← World-mapping and road textures
  /Tools             ← Editor scripts for map downloading
```

---

## Phased Implementation Plan

### Phase 1 — Data Scraper (C#) ✅

**Goal:** Given a GPS coordinate, produce a clean `.osm` file of nearby roads and elevation data.

- [x] Use the [Overpass API](https://overpass-api.de/) to download road data within a configurable radius (default 5 km).
- [x] Save `.osm` files to a local project folder for offline / editor use.
- [ ] Optionally bundle elevation/DEM data alongside the `.osm` download.
- See [`Tools/OsmDownloader/`](Tools/OsmDownloader/) and [`Tools/README.md`](Tools/README.md).

### Phase 2 — Spline Generator ✅

**Goal:** Convert raw OSM nodes (lat/lon points) into smooth Catmull-Rom splines visible in the editor.

- [x] Parse OSM `<way>` elements tagged with `highway` into `RoadSegment` objects.
- [x] Parse OSM `<way>` elements tagged with `building` into `BuildingFootprint` objects.
- [x] Project WGS-84 GPS coordinates to Unity world-space XZ using a Web Mercator (EPSG:3857) projection (offsets are in metres near the origin; scale distortion increases at high latitudes).
- [x] Fit a Catmull-Rom spline through the projected points.
- [x] Model raw OSM nodes and ways as typed C# structs/classes (`MapNode`, `MapWay`, `RoadType`).
- See [`Assets/Scripts/DataInversion/OSMParser.cs`](Assets/Scripts/DataInversion/OSMParser.cs) and
  [`Assets/Scripts/Procedural/SplineGenerator.cs`](Assets/Scripts/Procedural/SplineGenerator.cs).

### Phase 3 — Mesh Extruder ✅

**Goal:** Turn a spline into a drivable, UV-mapped 3D road mesh.

- [x] Extrude a configurable-width road mesh along each spline.
- [x] Generate UV coordinates suitable for a tiling asphalt texture.
- [x] Add road-type-based width variation (motorways wider than residential streets).
- [x] Add kerbs and lane markings as separate meshes or UV channels.
- [x] Detect `bridge`/`viaduct` OSM tags and mark road segments with `IsBridge`.
- [x] Smoothly elevate bridge splines above the surface mesh using `BridgeElevator` (smooth-step ramps at approach and departure).
- See [`Assets/Scripts/Procedural/RoadMeshExtruder.cs`](Assets/Scripts/Procedural/RoadMeshExtruder.cs) and [`Assets/Scripts/Procedural/BridgeElevator.cs`](Assets/Scripts/Procedural/BridgeElevator.cs).

### Phase 4 — Biomes & Asset Scatterer ⚠️ In Progress

**Goal:** Populate roadsides with region-appropriate props (signs, lamp posts, buildings).

- [x] Extrude building footprints into 3D wall and roof meshes with deterministic randomised heights.
- [x] Read the `country` or `addr:country` tag from OSM nodes to detect region/biome.
- [x] Scatter roadside props (signs, lamp posts, fences) along road splines.
- [x] Select region-appropriate texture IDs for road and building meshes (`RegionTextures`).
- [ ] Select prefabs from the matching regional kit folder (`European_Kit`, `Asian_Kit`, etc.).
- [ ] Wire texture IDs to Unity material assets in the scene.
- See [`Assets/Scripts/Procedural/BuildingGenerator.cs`](Assets/Scripts/Procedural/BuildingGenerator.cs),
  [`Assets/Scripts/Procedural/RoadsidePropPlacer.cs`](Assets/Scripts/Procedural/RoadsidePropPlacer.cs),
  [`Assets/Scripts/Procedural/RegionTextures.cs`](Assets/Scripts/Procedural/RegionTextures.cs), and
  [`Assets/Scripts/DataInversion/OSMParser.cs`](Assets/Scripts/DataInversion/OSMParser.cs).

### Phase 5 — Game State & Manager ✅

**Goal:** Wire up a singleton game-state machine to coordinate map loading, level generation, and racing.

- [x] Implement `GameManager` singleton with a `GameState` enum (`MainMenu`, `LoadingMap`, `GeneratingLevel`, `Racing`, `Paused`, `Results`).
- [x] Expose `OnStateChanged` events so subsystems can react without tight coupling.
- [ ] Connect `GameManager` state transitions to the OSM loading and procedural generation pipeline.
- [ ] Implement an in-editor **TerraDrive → Load OSM File / Generate Level** menu item.
- See [`Assets/Scripts/Core/GameManager.cs`](Assets/Scripts/Core/GameManager.cs).

### Phase 6 — Vehicle Physics ✅

**Goal:** Implement a semi-realistic car controller that feels fun to drive on procedurally generated roads.

- [x] `WheelCollider`-based rear-wheel-drive car with configurable motor torque, brake torque, and steering angle.
- [x] Drift friction model — reduced sideways stiffness when the handbrake (`Space`) is held.
- [x] Anti-roll bar on both axles to prevent cornering flips.
- [x] Visual wheel mesh synchronisation (position + rotation).
- [x] Chase-cam controller with configurable follow distance, height, look-ahead, and smoothing.
- [ ] Add audio (engine rev, tyre squeal, collision sounds).
- See [`Assets/Scripts/Vehicle/CarController.cs`](Assets/Scripts/Vehicle/CarController.cs) and
  [`Assets/Scripts/Vehicle/ChaseCam.cs`](Assets/Scripts/Vehicle/ChaseCam.cs).

### Phase 7 — Terrain Elevation ✅

**Goal:** Apply real-world elevation data so roads and buildings sit on accurate terrain rather than a flat plane.

- [x] Define `IElevationSource` interface for pluggable DEM backends.
- [x] Implement `OpenElevationSource` — fetches SRTM 30 m elevation data from the [Open-Elevation API](https://open-elevation.com) (no API key required, self-hostable for offline use).
- [x] Add elevation overloads to `CoordinateConverter` (`LatLonToUnity(lat, lon, elevMetres)` and `LatLonToUnity(lat, lon, originLat, originLon, elevMetres)`) that set the Unity Y axis.
- [x] Sample elevation for every OSM node during map load and apply Y positions to `RoadSegment` nodes and `BuildingFootprint` corners (via `OSMParser.ParseAsync`).
- [x] Implement `ElevationGrid` — a regular lat/lon grid of DEM samples with a `SampleAsync` factory that batch-fetches from any `IElevationSource`.
- [x] Generate a terrain mesh from an `ElevationGrid` using `TerrainMeshGenerator.Generate` — produces a UV-mapped heightfield mesh whose Y coordinates match the sampled elevation data.
- [ ] Raise road splines and building footprints to match sampled terrain heights (requires Unity scene wiring).
- See [`Assets/Scripts/Terrain/IElevationSource.cs`](Assets/Scripts/Terrain/IElevationSource.cs),
  [`Assets/Scripts/Terrain/OpenElevationSource.cs`](Assets/Scripts/Terrain/OpenElevationSource.cs),
  [`Assets/Scripts/Terrain/ElevationGrid.cs`](Assets/Scripts/Terrain/ElevationGrid.cs), and
  [`Assets/Scripts/Terrain/TerrainMeshGenerator.cs`](Assets/Scripts/Terrain/TerrainMeshGenerator.cs).

### Phase 8 — Race Logic & HUD 🔲 Planned

**Goal:** Turn the open-world drive into a timed race with checkpoints, lap counting, and a results screen.

- [ ] Define race checkpoints along the generated road splines.
- [ ] Implement lap timing and best-lap tracking.
- [ ] Build a HUD (speedometer, lap timer, position, minimap).
- [ ] Add a results / podium screen wired to the `GameManager.Results` state.
- [ ] Implement AI opponent vehicles that follow the road spline.

### Phase 9 — CLI Project Setup & CI/CD Releases ✅

**Goal:** Allow the Unity project to be created and configured entirely from the command line, and automate release builds via GitHub Actions.

- [x] Implement `ProjectSetup.Configure` Editor script callable in batch mode via
      `-executeMethod TerraDrive.Editor.ProjectSetup.Configure`.
- [x] Set physics gravity to `(0, -9.81, 0)` and register `Terrain` + `Road` user layers.
- [x] Expose the same configuration as a **TerraDrive → Configure Project** menu item.
- [x] Add `release.yml` GitHub Actions workflow — push to the `release` branch runs tests,
      builds for Windows/macOS/Linux via `game-ci/unity-builder`, and publishes a GitHub Release.
- See [`Assets/Scripts/Editor/ProjectSetup.cs`](Assets/Scripts/Editor/ProjectSetup.cs) and
  [`.github/workflows/release.yml`](.github/workflows/release.yml).

---

## Testing

Unit and integration tests live in [`Tests/TerraDrive.Tests/`](Tests/TerraDrive.Tests/) and use NUnit on .NET 8.
They cover the following modules:

| Test file | Module(s) covered |
|---|---|
| `CoordinateConverterTests.cs` | `CoordinateConverter` |
| `OSMParserTests.cs` | `OSMParser`, `RoadSegment`, `BuildingFootprint` |
| `MapNodeTests.cs` | `MapNode` |
| `MapWayTests.cs` | `MapWay` |
| `RoadTypeTests.cs` | `RoadType` |
| `RegionTypeTests.cs` | `RegionType`, `OSMParser.DetectRegion` |
| `RoadMeshExtruderTests.cs` | `RoadMeshExtruder`, `RoadMeshResult` |
| `BridgeElevatorTests.cs` | `BridgeElevator` |
| `BuildingGeneratorTests.cs` | `BuildingGenerator`, `BuildingMeshResult` |
| `RoadSurfaceDeformerTests.cs` | `RoadSurfaceDeformer` |
| `RoadsidePropPlacerTests.cs` | `RoadsidePropPlacer`, `PropPlacement`, `PropType` |
| `OpenElevationSourceTests.cs` | `OpenElevationSource`, `IElevationSource` |
| `TerrainMeshGeneratorTests.cs` | `ElevationGrid`, `TerrainMeshGenerator`, `TerrainMeshResult` |
| `OsmDownloaderTests.cs` | `OsmDownloader` |
| `ChaseCamIntegrationTests.cs` | `ChaseCam` (integration, renders `chase-cam-preview.png`) |
| `MapRendererIntegrationTests.cs` | `OSMParser` + `SplineGenerator` (integration, renders `map-preview.png`) |

```bash
dotnet test Tests/TerraDrive.Tests/TerraDrive.Tests.csproj
```

---

## Getting Started

For step-by-step instructions on running TerraDrive as a proof of concept — including
verifying the pipeline with the .NET test suite, assembling the Unity scene, and producing
a standalone executable — see **[GETTING_STARTED.md](GETTING_STARTED.md)**.

### Quick Reference

#### Prerequisites

- Unity 6.3 LTS or later (URP or HDRP recommended)
- .NET 8 SDK (for running unit tests and the OSM downloader tool outside Unity)

#### Verify the pipeline (no Unity needed)

```bash
dotnet test Tests/TerraDrive.Tests/TerraDrive.Tests.csproj
```

#### Download Map Data

```bash
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output Assets/Data/london.osm
```

#### Open in Unity

1. Open the project root in Unity Hub.
2. Create a new scene, add `GameManager`, a vehicle with `CarController`, and a `ChaseCam`
   on the main camera (see [GETTING_STARTED.md](GETTING_STARTED.md) for full scene setup).
3. Press **▶ Play** to drive, or use **File → Build Settings → Build** to export a
   standalone executable.

---

## CLI — Create & Configure the Unity Project

TerraDrive ships an Editor script ([`Assets/Scripts/Editor/ProjectSetup.cs`](Assets/Scripts/Editor/ProjectSetup.cs))
that can be run from the command line in Unity's **batch mode** to create the project and
apply the standard project settings (gravity, layers) without opening the Unity Editor UI.

### Step 1 — Create the project

If you are bootstrapping from a fresh clone (no `Library/` folder yet), Unity must first
import the project. Pass `-createProject` to force it to generate all project metadata:

```bat
:: Windows — adjust the path to your installed Unity version
"C:\Program Files\Unity\Hub\Editor\6000.3.x\Editor\Unity.exe" ^
    -batchmode -quit ^
    -createProject "C:\path\to\terradrive"
```

```bash
# macOS / Linux
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/terradrive" \
    -createProject "/path/to/terradrive"
```

### Step 2 — Apply project defaults

Once the project has been imported, run the `ProjectSetup.Configure` method to apply the
standard TerraDrive settings:

| Setting | Value |
|---|---|
| Physics gravity | `(0, -9.81, 0)` m/s² |
| User layer 8 | `Terrain` |
| User layer 9 | `Road` |

```bat
:: Windows
"C:\Program Files\Unity\Hub\Editor\6000.3.x\Editor\Unity.exe" ^
    -batchmode -quit ^
    -projectPath "C:\path\to\terradrive" ^
    -executeMethod TerraDrive.Editor.ProjectSetup.Configure
```

```bash
# macOS / Linux
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/terradrive" \
    -executeMethod TerraDrive.Editor.ProjectSetup.Configure
```

You can also run the same configuration interactively from the Unity menu bar:
**TerraDrive → Configure Project**.

---

## CI/CD — Automated Builds & Releases

The repository ships two GitHub Actions workflows.

| Workflow | Trigger | What it does |
|---|---|---|
| [`tests.yml`](.github/workflows/tests.yml) | Every push / PR to `main` | Runs the .NET unit tests |
| [`release.yml`](.github/workflows/release.yml) | Push to `release` branch | Runs tests → builds for Windows, macOS, Linux → publishes a GitHub Release |

### Publishing a release

1. Merge your changes into the `release` branch (or push directly):

   ```bash
   git checkout release
   git merge main          # or cherry-pick the commits you want
   git push origin release
   ```

   Optionally tag the commit first so the release gets a human-readable version number:

   ```bash
   git tag v2025.06.01
   git push origin v2025.06.01
   git push origin release
   ```

2. The `release.yml` workflow will:
   - Determine the version from the tag (or fall back to `YYYY.MM.DD-<short SHA>`).
   - Run the .NET unit-test suite.
   - Build the project for **Windows 64-bit**, **macOS**, and **Linux 64-bit** via
     [`game-ci/unity-builder`](https://game.ci/).
   - Upload each platform zip as a workflow artifact.
   - Create a GitHub Release named `TerraDrive vX.Y.Z` with all three zips attached.

### Required secrets

Set the following repository secrets (**Settings → Secrets and variables → Actions**):

| Secret | Description |
|---|---|
| `UNITY_LICENSE` | Contents of a valid Unity `.ulf` license file |
| `UNITY_EMAIL` | Unity account e-mail address |
| `UNITY_PASSWORD` | Unity account password |

See the [game-ci documentation](https://game.ci/docs/github/activation) for how to obtain
and encode the Unity license file.

---

## Development Tips

- **Work in Editor Mode first.** Write Editor Scripts that let you click a button to download the map and generate the level. Move to runtime generation only after the pipeline is solid.
- **Use Mock Data.** Before battling the Overpass API, feed the parser a small hand-crafted JSON/XML file with four coordinates to build a test road.
- **The Y-Axis Problem.** Real-world coordinates are Latitude/Longitude; Unity uses metres. Always use `CoordinateConverter.LatLonToUnity()` (see [`Assets/Scripts/Core/CoordinateConverter.cs`](Assets/Scripts/Core/CoordinateConverter.cs)) to convert WGS-84 to world space. Note that scale distortion increases at higher latitudes, so large maps may need correction.
- **Deterministic Builds.** `BuildingGenerator` seeds its RNG from the OSM `WayId`, so the same map always produces an identical city skyline — useful for reproducible testing.

---

## Contributing

Pull requests are welcome. Please open an issue first to discuss major changes.

## License

MIT
