# VectorRoad

A semi-realistic racing game built on real-world roads using OpenStreetMap data and locally generated 3D assets.

---

## Vision

VectorRoad lets players race on procedurally generated tracks derived from actual GPS road data. Every road surface, building, and piece of roadside scenery is generated at runtime from real-world sources — so every location on Earth is a potential race track.

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
/VectorRoad
  /Assets
    /Scripts
      /Core          ← Game managers, state machines, coordinate helpers
      /DataInversion ← OSM / DEM parsing logic
      /Editor        ← Unity Editor-only scripts (ProjectSetup, batch-mode helpers)
      /Hud           ← Minimap renderer and HUD utilities
      /Procedural    ← Mesh generation (Roads, Buildings, Props)
      /Terrain       ← Elevation data sources (IElevationSource, OpenElevationSource)
      /Vehicle       ← Physics, controls, chase camera, and speedometer
    /Prefabs         ← Asset kits (Signs, Houses, Foliage)
    /Shaders         ← World-mapping and road textures
  /Tools             ← Editor scripts for map downloading
```

Note: `OsmDownloader` and `IOsmDownloader` source now lives in
`Assets/Scripts/Tools/` so Unity, tests, and the CLI share one implementation.
`Tools/OsmDownloader/` contains the .NET CLI project entrypoint (`Program.cs`) and
project file.

---

## Phased Implementation Plan

### Phase 1 — Data Scraper (C#) ✅

**Goal:** Given a GPS coordinate, produce a clean `.osm` file of nearby roads and elevation data.

See [`Assets/Scripts/Tools/`](Assets/Scripts/Tools/),
[`Tools/OsmDownloader/`](Tools/OsmDownloader/), and [`Tools/README.md`](Tools/README.md).

### Phase 2 — Spline Generator ✅

**Goal:** Convert raw OSM nodes (lat/lon points) into smooth Catmull-Rom splines visible in the editor.

See [`Assets/Scripts/DataInversion/OSMParser.cs`](Assets/Scripts/DataInversion/OSMParser.cs) and
[`Assets/Scripts/Procedural/SplineGenerator.cs`](Assets/Scripts/Procedural/SplineGenerator.cs).

### Phase 3 — Mesh Extruder ✅

**Goal:** Turn a spline into a drivable, UV-mapped 3D road mesh.

See [`Assets/Scripts/Procedural/RoadMeshExtruder.cs`](Assets/Scripts/Procedural/RoadMeshExtruder.cs) and [`Assets/Scripts/Procedural/BridgeElevator.cs`](Assets/Scripts/Procedural/BridgeElevator.cs).

### Phase 4 — Biomes & Asset Scatterer ⚠️ In Progress

**Goal:** Populate roadsides with region-appropriate props (signs, lamp posts, buildings).

- [ ] Select prefabs from the matching regional kit folder (`European_Kit`, `Asian_Kit`, etc.).
- See [`Assets/Scripts/Procedural/BuildingGenerator.cs`](Assets/Scripts/Procedural/BuildingGenerator.cs),
  [`Assets/Scripts/Procedural/RoadsidePropPlacer.cs`](Assets/Scripts/Procedural/RoadsidePropPlacer.cs),
  [`Assets/Scripts/Procedural/RegionTextures.cs`](Assets/Scripts/Procedural/RegionTextures.cs), and
  [`Assets/Scripts/DataInversion/OSMParser.cs`](Assets/Scripts/DataInversion/OSMParser.cs).

### Phase 5 — Game State & Manager ✅

**Goal:** Wire up a singleton game-state machine to coordinate map loading, level generation, and racing.

See [`Assets/Scripts/Core/GameManager.cs`](Assets/Scripts/Core/GameManager.cs) and [`Assets/Scripts/Core/MapSceneBuilder.cs`](Assets/Scripts/Core/MapSceneBuilder.cs).

### Phase 6 — Vehicle Physics ⚠️ In Progress

**Goal:** Implement a semi-realistic car controller that feels fun to drive on procedurally generated roads.

- [ ] Add audio (engine rev, tyre squeal, collision sounds).
- See [`Assets/Scripts/Vehicle/CarController.cs`](Assets/Scripts/Vehicle/CarController.cs) and
  [`Assets/Scripts/Vehicle/ChaseCam.cs`](Assets/Scripts/Vehicle/ChaseCam.cs).

### Phase 7 — Terrain Elevation ✅

**Goal:** Apply real-world elevation data so roads and buildings sit on accurate terrain rather than a flat plane.

See [`Assets/Scripts/Terrain/IElevationSource.cs`](Assets/Scripts/Terrain/IElevationSource.cs),
[`Assets/Scripts/Terrain/OpenElevationSource.cs`](Assets/Scripts/Terrain/OpenElevationSource.cs),
[`Assets/Scripts/Terrain/ElevationGrid.cs`](Assets/Scripts/Terrain/ElevationGrid.cs), and
[`Assets/Scripts/Terrain/TerrainMeshGenerator.cs`](Assets/Scripts/Terrain/TerrainMeshGenerator.cs).

### Phase 8 — Race Logic & HUD ⚠️ In Progress

**Goal:** Turn the open-world drive into a timed race with checkpoints, lap counting, and a results screen.

- [ ] Build in-scene HUD overlay (canvas with speedometer readout, lap timer, position counter, minimap).
- [ ] Define race checkpoints along the generated road splines.
- [ ] Implement lap timing and best-lap tracking.
- [ ] Add a results / podium screen wired to the `GameManager.Results` state.
- [ ] Implement AI opponent vehicles that follow the road spline.
- See [`Assets/Scripts/Vehicle/SpeedometerHud.cs`](Assets/Scripts/Vehicle/SpeedometerHud.cs) and
  [`Assets/Scripts/Hud/MinimapRenderer.cs`](Assets/Scripts/Hud/MinimapRenderer.cs).

### Phase 9 — CLI Project Setup & CI/CD Releases ✅

**Goal:** Allow the Unity project to be created and configured entirely from the command line, and automate release builds via GitHub Actions.

See [`Assets/Scripts/Editor/ProjectSetup.cs`](Assets/Scripts/Editor/ProjectSetup.cs) and
[`.github/workflows/release.yml`](.github/workflows/release.yml).

---

## Testing

Unit tests live in [`Tests/VectorRoad.Tests/`](Tests/VectorRoad.Tests/) and use NUnit on .NET 8.
They cover the following modules:

| Test file | Module(s) covered |
|---|---|
| `CoordinateConverterTests.cs` | `CoordinateConverter` |
| `OSMParserTests.cs` | `OSMParser`, `RoadSegment`, `BuildingFootprint`, `WaterBody` |
| `OSMParserRealDataTests.cs` | `OSMParser` against real-format OSM XML |
| `MapNodeTests.cs` | `MapNode` |
| `MapWayTests.cs` | `MapWay` |
| `RoadTypeTests.cs` | `RoadType` |
| `RegionTypeTests.cs` | `RegionType`, `OSMParser.DetectRegion` |
| `RoadMeshExtruderTests.cs` | `RoadMeshExtruder`, `RoadMeshResult` |
| `BridgeElevatorTests.cs` | `BridgeElevator` |
| `BuildingGeneratorTests.cs` | `BuildingGenerator`, `BuildingMeshResult` |
| `RoadSurfaceDeformerTests.cs` | `RoadSurfaceDeformer` |
| `RoadsidePropPlacerTests.cs` | `RoadsidePropPlacer`, `PropPlacement`, `PropType` |
| `WaterMeshGeneratorTests.cs` | `WaterMeshGenerator`, `WaterMeshResult` |
| `OpenElevationSourceTests.cs` | `OpenElevationSource`, `IElevationSource` |
| `TerrainMeshGeneratorTests.cs` | `ElevationGrid`, `ElevationGrid.SampleElevation`, `ElevationGrid` as `IElevationSource`, `TerrainMeshGenerator`, `TerrainMeshResult` |
| `OsmDownloaderTests.cs` | `OsmDownloader` (including elevation grid download, save, and load) |
| `MapLoaderTests.cs` | `MapLoader`, `MapData` (end-to-end load from `.osm` + `.elevation.csv` → roads, buildings, water bodies, terrain mesh) |
| `MaterialRegistryTests.cs` | `MaterialRegistry` |
| `PlaceholderMaterialFactoryTests.cs` | `PlaceholderMaterialFactory` |
| `SpeedometerTests.cs` | `Speedometer`, `SpeedometerHud` |
| `MinimapRendererTests.cs` | `MinimapRenderer`, `MinimapLine` |
| `OsmLevelLoaderTests.cs` | `OsmLevelLoader` (GPS coordinate settings & validation for the **VectorRoad → Load OSM File / Generate Level** editor menu item) |
| `LocationMenuControllerTests.cs` | `LocationMenuController`, `LocationLoadResult` |

```bash
dotnet test Tests/VectorRoad.Tests/VectorRoad.Tests.csproj
```

---

## Getting Started

For step-by-step instructions on running VectorRoad as a proof of concept — including
verifying the pipeline with the .NET test suite, assembling the Unity scene, and producing
a standalone executable — see **[GETTING_STARTED.md](GETTING_STARTED.md)**.

### Quick Reference

#### Prerequisites

- Unity 6.3 LTS or later (URP or HDRP recommended)
- .NET 8 SDK (for running unit tests and the OSM downloader tool outside Unity)

#### Verify the pipeline (no Unity needed)

```bash
dotnet test Tests/VectorRoad.Tests/VectorRoad.Tests.csproj
```

#### Download Map Data

```bash
# OSM roads and buildings + DEM elevation grid (default — saves london.osm and london.elevation.csv)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output Assets/Data/london.osm

# Skip elevation download
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 --output Assets/Data/london.osm --no-elevation
```

#### Open in Unity

1. Open the project root in Unity Hub.
2. Create a new scene, add `GameManager`, a vehicle with `CarController`, and a `ChaseCam`
   on the main camera (see [GETTING_STARTED.md](GETTING_STARTED.md) for full scene setup).
3. Press **▶ Play** to drive, or use **File → Build Settings → Build** to export a
   standalone executable.

---

## CLI — Create & Configure the Unity Project

VectorRoad ships an Editor script ([`Assets/Scripts/Editor/ProjectSetup.cs`](Assets/Scripts/Editor/ProjectSetup.cs))
that can be run from the command line in Unity's **batch mode** to create the project and
apply the standard project settings (gravity, layers) without opening the Unity Editor UI.

### Step 1 — Create the project

If you are bootstrapping from a fresh clone (no `Library/` folder yet), Unity must first
import the project. Pass `-createProject` to force it to generate all project metadata:

```bat
:: Windows — adjust the path to your installed Unity version
"C:\Program Files\Unity\Hub\Editor\6000.3.x\Editor\Unity.exe" ^
    -batchmode -quit ^
    -createProject "C:\path\to\vectorroad"
```

```bash
# macOS / Linux
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/vectorroad" \
    -createProject "/path/to/vectorroad"
```

### Step 2 — Apply project defaults

Once the project has been imported, run the `ProjectSetup.Configure` method to apply the
standard VectorRoad settings:

| Setting | Value |
|---|---|
| Physics gravity | `(0, -9.81, 0)` m/s² |
| User layer 8 | `Terrain` |
| User layer 9 | `Road` |

```bat
:: Windows
"C:\Program Files\Unity\Hub\Editor\6000.3.x\Editor\Unity.exe" ^
    -batchmode -quit ^
    -projectPath "C:\path\to\vectorroad" ^
    -executeMethod VectorRoad.Editor.ProjectSetup.Configure
```

```bash
# macOS / Linux
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/vectorroad" \
    -executeMethod VectorRoad.Editor.ProjectSetup.Configure
```

You can also run the same configuration interactively from the Unity menu bar:
**VectorRoad → Configure Project**.

### Load OSM File / Generate Level (interactive)

Once the project is open in the Unity Editor you can download a real-world map and
generate the full level (terrain + roads + buildings) in a few clicks — no manual file
management required:

1. Click **VectorRoad → Load OSM File / Generate Level** in the Unity menu bar.
   An editor window opens.
2. Enter the **Latitude** and **Longitude** of the map origin (decimal degrees, WGS-84).
3. Set the **Radius** (metres) that controls how large an area is downloaded
   (default: 500 m).
4. Optionally change the **Output Directory** where the downloaded files are saved
   (default: `Assets/Data/`).
5. Click **Download & Generate Level**.  A progress bar shows download status while
   VectorRoad fetches OSM road/building data from the Overpass API and the DEM
   elevation grid from the Open-Elevation API.
6. After download, the active scene's `MapSceneBuilder` component (created automatically
   if absent) is wired to the downloaded files and the `GameManager` origin is synced.
7. A confirmation dialog asks whether to **Enter Play Mode** immediately.  Clicking
   *Enter Play Mode* starts the `MapSceneBuilder` coroutine which drives the
   `GameManager` through `LoadingMap → GeneratingLevel → Racing`.

See [`Assets/Scripts/Editor/LoadOsmMenuEditor.cs`](Assets/Scripts/Editor/LoadOsmMenuEditor.cs)
and [`Assets/Scripts/Core/OsmLevelLoader.cs`](Assets/Scripts/Core/OsmLevelLoader.cs).

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
   - Create a GitHub Release named `VectorRoad vX.Y.Z` with all three zips attached.

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
