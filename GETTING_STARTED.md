# Getting Started with VectorRoad

This guide walks you through running VectorRoad as a proof of concept — from verifying the
core pipeline with the .NET test suite, through setting up a Unity scene, to producing a
playable standalone executable.

---

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| [Unity Hub](https://unity.com/download) + **Unity 6.3 LTS** | 6000.3.x | Game engine (required to build and play) |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) | 8.0+ | Run unit/integration tests and the OSM downloader outside Unity |

---

## Step 1 — Verify the Core Pipeline (No Unity Required)

The C# logic (OSM parsing, mesh generation, coordinate conversion, vehicle camera) can be
validated entirely outside Unity using the .NET test project.

```bash
dotnet test Tests/VectorRoad.Tests/VectorRoad.Tests.csproj
```

A successful run confirms:

- OSM files are parsed correctly into `RoadSegment` and `BuildingFootprint` objects.
- GPS coordinates are projected to Unity world-space via `CoordinateConverter`.
- Road splines, kerbs, and building meshes are generated without errors.
- Roadside props are placed along splines.
- The chase-camera math produces a valid perspective view (renders `chase-cam-preview.png`).

> **Note:** The integration tests write preview images to the directory specified by the
> `CHASE_CAM_PREVIEW_DIR` and `MAP_PREVIEW_DIR` environment variables. Set these to a local
> folder before running if you want to inspect the rendered output:
>
> ```bash
> export CHASE_CAM_PREVIEW_DIR=/tmp/vectorroad-previews
> export MAP_PREVIEW_DIR=/tmp/vectorroad-previews
> mkdir -p /tmp/vectorroad-previews
> dotnet test Tests/VectorRoad.Tests/VectorRoad.Tests.csproj
> ```

---

## Step 2 — Download Real-World Map Data

Use the bundled Overpass API downloader to fetch road and building data for any location.
By default, a DEM elevation grid is also downloaded and saved as a companion `.elevation.csv` file:

```bash
# Central London (5 km radius — good first test)
# Saves london.osm + london.elevation.csv
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output Assets/Data/london.osm

# Smaller area — faster to generate, ideal for a first run
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 1000 \
    --output Assets/Data/london_small.osm
```

To use a higher-resolution elevation grid, pass `--dem-rows` and `--dem-cols`:

```bash
# 64×64 elevation samples instead of the default 32×32
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output Assets/Data/london.osm --dem-rows 64 --dem-cols 64
```

To skip the elevation download entirely, pass `--no-elevation`:

```bash
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output Assets/Data/london.osm --no-elevation
```

The `.osm` file is read by `OSMParser` at runtime; the `.elevation.csv` file is loaded by
`MapLoader.LoadMapAsync` (or directly via `ElevationGrid.Load`) and used to lift every road
and building node to the real-world terrain elevation and to generate the heightfield terrain
mesh.  See [`Tools/README.md`](Tools/README.md) for the full argument reference.

Source note: the downloader implementation (`OsmDownloader`, `IOsmDownloader`) is in
`Assets/Scripts/Tools/`; `Tools/OsmDownloader/` is the CLI wrapper project used by
`dotnet run --project Tools/OsmDownloader`.

---

## Step 3 — Open / Create the Project in Unity

### Option A — Unity Hub (recommended for development)

1. **Install Unity 6.3 LTS** via [Unity Hub](https://unity.com/download).
   When prompted, include the **Windows/Mac/Linux Standalone Build Support** module for
   the platform you want to export to.

2. In Unity Hub click **Add → Add project from disk** and select the root of this
   repository (the folder that contains `Assets/`, `Tools/`, and `Tests/`).

3. Unity will import all assets. This may take a few minutes on first open.

### Option B — Command-line / headless (batch mode)

If you prefer to bootstrap the project without opening the Unity Editor UI (e.g. on a
CI server or a headless machine), use Unity's batch-mode flags:

```bat
:: Windows — create / import the project
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" ^
    -batchmode -quit ^
    -createProject "C:\Users\Adam\Documents\GitHub\vectorroad"
```

```bash
# macOS / Linux — create / import the project
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/vectorroad" \
    -createProject "/path/to/vectorroad"
```

Once the project has been imported, apply the standard VectorRoad project settings
(gravity = -9.81, Road and Terrain layers) by executing the bundled setup script:

```bat
:: Windows
"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" ^
    -batchmode -quit ^
    -projectPath "C:\Users\Adam\Documents\GitHub\vectorroad" ^
    -executeMethod VectorRoad.Editor.ProjectSetup.Configure
```

```bash
# macOS / Linux
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/vectorroad" \
    -executeMethod VectorRoad.Editor.ProjectSetup.Configure
```

The script configures the following defaults:

| Setting | Value |
|---|---|
| Physics gravity | `(0, -9.81, 0)` m/s² |
| User layer 8 | `Terrain` |
| User layer 9 | `Road` |

You can also trigger the same setup interactively at any time from the Unity menu bar:
**VectorRoad → Configure Project**.

### 3b. Load an OSM map with the editor menu item

The quickest way to try any location — no manual file downloads or Inspector edits needed:

1. Click **VectorRoad → Load OSM File / Generate Level** in the Unity menu bar.
   An editor window opens.
2. Enter the **Latitude** and **Longitude** of your chosen map centre (decimal degrees,
   WGS-84).  For example, central London: `51.5074`, `-0.1278`.
3. Set the **Radius** in metres (default 500 m) to control how large an area is downloaded.
4. Optionally change the **Output Directory** where the downloaded files are saved
   (default: `Assets/Data/`).
5. Click **Download & Generate Level**.  A progress bar shows download status while
   VectorRoad fetches OSM road/building data from the Overpass API and the DEM elevation
   grid from the Open-Elevation API.
6. After download the active scene's `MapSceneBuilder` and `GameManager` are configured
   automatically.  The scene is marked dirty — save it if you want to keep the settings.
7. A confirmation dialog asks whether to **Enter Play Mode** immediately to build the
   terrain, road, building, and water geometry.

The scene contains:
- **Directional Light** — a sun-like light angled at (50°, −30°, 0°).
- **GameManager** — the singleton state machine, defaulting to `MainMenu` state and centred on Ames, Iowa (41.8957, −93.5888) — the geographic origin of the bundled sample data.
- **MaterialRegistry** — pre-populated with 25 assignable texture-ID slots (road surfaces, kerbs, building walls, building roofs).  Each slot is empty by default; drag your Unity `Material` assets into the Inspector to wire them up (see §4a below).  Water, terrain, and lane-marking slots are filled automatically with solid-colour placeholders at startup.
- **MapSceneBuilder** — wired to `Assets/StreamingAssets/Data/map.osm.xml` + `Assets/StreamingAssets/Data/map.elevation.csv`.  On Play it loads the map, builds the terrain/road/building/water geometry, and transitions the `GameManager` through `LoadingMap → GeneratingLevel → Racing` automatically.

You still need to add a vehicle and camera manually (§4b–4e).  The terrain mesh is generated automatically by `MapSceneBuilder`, so you no longer need to create a flat ground plane.

### 4a. Assign materials to the MaterialRegistry

1. Select the **MaterialRegistry** GameObject in the Hierarchy.
2. In the Inspector, expand **Entries**.  You will see 25 rows for road/kerb/building texture IDs:

   | Texture ID | What it covers |
   |---|---|
   | `road_asphalt` | Default (unknown region) road surface |
   | `road_asphalt_temperate` … `road_asphalt_steppe` | Paved roads per climate region |
   | `road_dirt`, `road_sand`, `road_mud`, `road_gravel_boreal`, `road_gravel_arctic` | Unpaved road surfaces |
   | `kerb_stone`, `kerb_concrete`, `kerb_granite` | Kerb/curb strips |
   | `building_wall_brick` … `building_wall_concrete` | Building wall surfaces |
   | `building_roof_slate` … `building_roof_flat` | Building roof surfaces |

3. Drag a Unity `Material` asset into the **Material** field of each row.  Rows left empty
   are silently skipped by `MaterialRegistry.ApplyTo`; the mesh will render with Unity's
   default magenta error material until a material is assigned.

> **Tip:** Start with a small set — assign one asphalt material to all road rows and one
> brick material to all building rows.  You can refine region-specific materials later.

> **Note:** Water, terrain, and lane-marking texture IDs (`water`, `water_arctic`,
> `water_tropical`, `terrain_grass`, `lane_marking_oneway`, `lane_marking_twoway`) are
> filled automatically with solid-colour placeholder materials by `PlaceholderMaterialFactory`
> in `Awake` for any slot that has no assigned material.

### 4b. Create a vehicle

1. Create an empty GameObject named `Car`.
2. Add a **Rigidbody** component (mass ≈ 1500 kg).
3. Add a **Box Collider** to approximate the car body (size ≈ `(2, 0.5, 4.5)`).
4. Create four empty child GameObjects named `WheelFL`, `WheelFR`, `WheelRL`, `WheelRR`
   and position them at the four corners of the car body (e.g. `(±0.8, 0, ±1.4)`).
5. Add a **WheelCollider** component to each wheel child.
6. Add **CarController** to the `Car` root and wire up the four `WheelCollider` references
   in the Inspector.
7. Optionally add visible wheel meshes (cylinder primitives work fine) and assign them to
   the **Visual Wheel Transforms** fields.
8. Drag the `Car` GameObject into the **Vehicle** field on the **MapSceneBuilder** component
   so it is repositioned above the map origin when loading completes.

### 4c. Add the chase camera

1. Select the **Main Camera** in the Hierarchy.
2. Add the **ChaseCam** component.
3. Drag the `Car` GameObject from the Hierarchy into the **Target** field.

> The scene already contains a **Directional Light** — no need to add one unless you deleted
> it or are starting from a different base scene.

> **No ground plane needed** — `MapSceneBuilder` generates a heightfield terrain mesh with a
> `MeshCollider` from the elevation data automatically when the scene starts.

---

## Step 5 — Play in the Editor

Press **▶ Play** in the Unity toolbar.

| Key | Action |
|---|---|
| **W / Up Arrow** | Accelerate |
| **S / Down Arrow** | Brake / reverse |
| **A / Left Arrow** | Steer left |
| **D / Right Arrow** | Steer right |
| **Space** | Handbrake (activates drift friction model) |

The chase camera will follow the car automatically. You should see the car roll forward on
the ground plane, steer, and drift when the handbrake is held.

---

## Step 6 — Build a Standalone Executable

Once the proof-of-concept scene works in Play mode you can export a standalone binary:

1. Open **File → Build Settings**.
2. Click **Add Open Scenes** to include `ProofOfConcept.unity`.
3. Select your target platform (PC/Mac/Linux Standalone, or any other installed module).
4. Click **Build** (or **Build And Run** to launch immediately).
5. Choose an output directory (e.g. `Builds/ProofOfConcept/`).

Unity will compile the project and produce:

- **Windows:** `VectorRoad.exe` + `VectorRoad_Data/` folder
- **macOS:** `VectorRoad.app` bundle
- **Linux:** `VectorRoad.x86_64` binary + `VectorRoad_Data/` folder

Run the produced binary to play the game outside the editor.

> **Tip — Development Build:** Tick **Development Build** in Build Settings to keep the
> profiler and console overlay available in the executable. Useful while iterating on the
> proof of concept.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `dotnet test` fails to build | .NET 8 SDK not installed | Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8) |
| OSM download times out | Overpass API load | Reduce `--radius` or retry later |
| Elevation download fails or returns zeros | Open-Elevation API unavailable | Retry later, or self-host an Open-Elevation instance and pass its URL to `OpenElevationSource` |
| Car falls through ground | Wheel colliders not touching the plane | Move `Car` up until the WheelColliders rest on the Plane |
| Car spins on the spot | WheelCollider radii too small | Increase the **Radius** on each WheelCollider to match the visual wheel |
| Camera stutters | `positionDamping` too high | Lower **Position Damping** on the ChaseCam component (try `3`) |
| No `WheelCollider` in Add Component list | Wrong Unity version | Ensure Unity 6.3 LTS is installed (WheelCollider is a built-in Physics component) |
| Batch-mode setup exits with code 1 | Editor script exception | Check the Unity `Editor.log` for the `[ProjectSetup]` error line |
| `release.yml` build fails with license error | Unity secrets not configured | See [game-ci docs](https://game.ci/docs/github/activation) to generate and upload `UNITY_LICENSE` |

---

## What Works Now vs What Is Planned

| Feature | Status |
|---|---|
| OSM parsing → road/building/water data | ✅ Working |
| Spline generation | ✅ Working |
| Road + kerb mesh extrusion | ✅ Working |
| Building footprint → 3D mesh | ✅ Working |
| Water body → flat surface mesh | ✅ Working — `WaterMeshGenerator.Generate` produces a fan-triangulated polygon mesh for lakes, ponds, riverbanks, and reservoirs |
| Roadside prop placement | ✅ Working |
| Region / biome detection from OSM tags | ✅ Working |
| Elevation (DEM) integration | ✅ Working — `ElevationGrid.SampleAsync` + `TerrainMeshGenerator.Generate` + `OSMParser.ParseAsync` + `OsmDownloader` downloads SRTM grid alongside `.osm` by default |
| Car physics + chase camera | ✅ Working |
| Game state machine | ✅ Working |
| CLI project create + configure (batch mode) | ✅ Working — `ProjectSetup.Configure` via `-executeMethod` |
| Editor menu: Load OSM File / Generate Level | ✅ Working — **VectorRoad → Load OSM File / Generate Level** opens an editor window, accepts lat/lon/radius, downloads OSM + elevation data via `OsmDownloader`, wires `MapSceneBuilder` and `GameManager`, and optionally enters Play mode |
| Automated release builds (CI/CD) | ✅ Working — push to `release` branch triggers `release.yml` |
| Texture ID → Material wiring | ✅ Working — `MaterialRegistry` scene component + placeholder auto-fill via `PlaceholderMaterialFactory` |
| Runtime scene assembly | ✅ Working — `MapSceneBuilder` loads OSM + elevation data on Play, instantiates terrain / road / building / water GameObjects, and drives the `GameManager` state machine (`LoadingMap → GeneratingLevel → Racing`); pre-wired in `ProofOfConcept.unity` |
| Location menu controller | ✅ Working — `LocationMenuController.LoadLocationAsync` downloads OSM + elevation data for any GPS coordinate and returns a fully built `MapData` with spawn position |
| Speedometer HUD + minimap renderer | ⚠️ Partial — `SpeedometerHud` (reads vehicle speed → MPH) and `MinimapRenderer` (road segments → minimap lines) exist; in-scene canvas overlay is planned |
| Prefab selection per region kit | 🔲 Planned |
| Race logic, checkpoints, HUD overlay | 🔲 Planned |
| AI opponents | 🔲 Planned |

For the full roadmap see the [main README](README.md#phased-implementation-plan).
