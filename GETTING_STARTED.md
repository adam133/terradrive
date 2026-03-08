# Getting Started with TerraDrive

This guide walks you through running TerraDrive as a proof of concept — from verifying the
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
dotnet test Tests/TerraDrive.Tests/TerraDrive.Tests.csproj
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
> export CHASE_CAM_PREVIEW_DIR=/tmp/terradrive-previews
> export MAP_PREVIEW_DIR=/tmp/terradrive-previews
> mkdir -p /tmp/terradrive-previews
> dotnet test Tests/TerraDrive.Tests/TerraDrive.Tests.csproj
> ```

---

## Step 2 — Download Real-World Map Data

Use the bundled Overpass API downloader to fetch road and building data for any location:

```bash
# Central London (5 km radius — good first test)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output Assets/Data/london.osm

# Smaller area — faster to generate, ideal for a first run
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 1000 \
    --output Assets/Data/london_small.osm
```

To also bundle DEM elevation data alongside the `.osm` file, add `--elevation`.  This
downloads a SRTM 30 m elevation grid for the same bounding box and saves it as a companion
`.elevation.csv` file (e.g. `london.elevation.csv`):

```bash
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output Assets/Data/london.osm --elevation

# Higher-resolution elevation grid (64×64 samples instead of the default 32×32)
dotnet run --project Tools/OsmDownloader -- --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output Assets/Data/london.osm --elevation --dem-rows 64 --dem-cols 64
```

The `.osm` file is read by `OSMParser` at runtime; the `.elevation.csv` file can be loaded
with `OsmDownloader.LoadElevationGrid(path)` into an `ElevationGrid` for use with
`TerrainMeshGenerator.Generate` and `OSMParser.ParseAsync`.
See [`Tools/README.md`](Tools/README.md) for the full argument reference.

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
"C:\Program Files\Unity\Hub\Editor\6000.3.x\Editor\Unity.exe" ^
    -batchmode -quit ^
    -createProject "C:\path\to\terradrive"
```

```bash
# macOS / Linux — create / import the project
/Applications/Unity/Hub/Editor/6000.3.x/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit \
    -projectPath "/path/to/terradrive" \
    -createProject "/path/to/terradrive"
```

Once the project has been imported, apply the standard TerraDrive project settings
(gravity = -9.81, Road and Terrain layers) by executing the bundled setup script:

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

The script configures the following defaults:

| Setting | Value |
|---|---|
| Physics gravity | `(0, -9.81, 0)` m/s² |
| User layer 8 | `Terrain` |
| User layer 9 | `Road` |

You can also trigger the same setup interactively at any time from the Unity menu bar:
**TerraDrive → Configure Project**.

---

## Step 4 — Open the Proof-of-Concept Scene

A pre-built scene is included at `Assets/Scenes/ProofOfConcept.unity`.  Open it in Unity:

**File → Open Scene → Assets/Scenes/ProofOfConcept.unity**

The scene contains:
- **Directional Light** — a sun-like light angled at (50°, −30°, 0°).
- **GameManager** — the singleton state machine, defaulting to `MainMenu` state and centred on London (51.5074, −0.1278).
- **MaterialRegistry** — pre-populated with all 25 texture-ID slots (road surfaces, kerbs, building walls, building roofs).  Each slot is empty by default; drag your Unity `Material` assets into the Inspector to wire them up (see §4a below).

You still need to add a ground plane, vehicle, and camera manually (§4b–4e).

### 4a. Assign materials to the MaterialRegistry

1. Select the **MaterialRegistry** GameObject in the Hierarchy.
2. In the Inspector, expand **Entries**.  You will see 25 rows, one per texture ID:

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

### 4b. Add a flat ground plane

**GameObject → 3D Object → Plane**. Scale it to `(100, 1, 100)` so the car has somewhere
to drive while the procedural road mesh is not yet connected.

### 4c. Create a vehicle

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

### 4e. Add the chase camera

1. Select the **Main Camera** in the Hierarchy.
2. Add the **ChaseCam** component.
3. Drag the `Car` GameObject from the Hierarchy into the **Target** field.

> The scene already contains a **Directional Light** — no need to add one unless you deleted
> it or are starting from a different base scene.

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

- **Windows:** `TerraDrive.exe` + `TerraDrive_Data/` folder
- **macOS:** `TerraDrive.app` bundle
- **Linux:** `TerraDrive.x86_64` binary + `TerraDrive_Data/` folder

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
| OSM parsing → road/building data | ✅ Working |
| Spline generation | ✅ Working |
| Road + kerb mesh extrusion | ✅ Working |
| Building footprint → 3D mesh | ✅ Working |
| Roadside prop placement | ✅ Working |
| Region / biome detection from OSM tags | ✅ Working |
| Elevation (DEM) integration | ✅ Working — `ElevationGrid.SampleAsync` + `TerrainMeshGenerator.Generate` + `OSMParser.ParseAsync` + `OsmDownloader --elevation` (downloads SRTM grid alongside `.osm`) |
| Car physics + chase camera | ✅ Working |
| Game state machine | ✅ Working |
| CLI project create + configure (batch mode) | ✅ Working — `ProjectSetup.Configure` via `-executeMethod` |
| Automated release builds (CI/CD) | ✅ Working — push to `release` branch triggers `release.yml` |
| Texture ID → Material wiring | ✅ Working — `MaterialRegistry` scene component + `ProofOfConcept.unity` with all 25 texture slots |
| Prefab selection per region kit | 🔲 Planned |
| Race logic, checkpoints, HUD | 🔲 Planned |
| AI opponents | 🔲 Planned |

For the full roadmap see the [main README](README.md#phased-implementation-plan).
