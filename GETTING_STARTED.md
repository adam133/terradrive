# Getting Started with TerraDrive

This guide walks you through running TerraDrive as a proof of concept — from verifying the
core pipeline with the .NET test suite, through setting up a Unity scene, to producing a
playable standalone executable.

---

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| [Unity Hub](https://unity.com/download) + **Unity 6.3 LTS** | 6000.3.x | Game engine (required to build and play) |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) | 8.0+ | Run unit/integration tests outside Unity |
| [Python](https://www.python.org/downloads/) | 3.14+ | OSM map downloader script |
| `requests` Python package | latest | Used by `osm_downloader.py` |

```bash
# Install the Python dependency
pip install requests
```

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
cd Tools

# Central London (5 km radius — good first test)
python osm_downloader.py --lat 51.5074 --lon -0.1278 --radius 5000 \
    --output ../Assets/Data/london.osm

# Smaller area — faster to generate, ideal for a first run
python osm_downloader.py --lat 51.5074 --lon -0.1278 --radius 1000 \
    --output ../Assets/Data/london_small.osm
```

The `.osm` file is saved to `Assets/Data/` and is read by `OSMParser` at runtime.
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

## Step 4 — Create the Proof-of-Concept Scene

There is no pre-built scene included in the repository yet, so you need to assemble one
manually. This only takes a few minutes.

### 4a. Create a new scene

**File → New Scene → Basic (Built-in)** (or URP/HDRP equivalent).
Save it as `Assets/Scenes/ProofOfConcept.unity`.

### 4b. Add the GameManager

1. Create an empty GameObject: **GameObject → Create Empty**. Name it `GameManager`.
2. In the Inspector click **Add Component** and search for `GameManager`.
3. Set **Origin Latitude** / **Origin Longitude** to the centre coordinate you used when
   downloading the `.osm` file (e.g. `51.5074` / `-0.1278` for London).

### 4c. Add a flat ground plane

**GameObject → 3D Object → Plane**. Scale it to `(100, 1, 100)` so the car has somewhere
to drive while the procedural road mesh is not yet connected.

### 4d. Create a vehicle

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

### 4f. Add a light

**GameObject → Light → Directional Light** if one is not already present.

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
| Elevation (DEM) integration | ✅ Working — `ElevationGrid.SampleAsync` + `TerrainMeshGenerator.Generate` + `OSMParser.ParseAsync` |
| Car physics + chase camera | ✅ Working |
| Game state machine | ✅ Working |
| CLI project create + configure (batch mode) | ✅ Working — `ProjectSetup.Configure` via `-executeMethod` |
| Automated release builds (CI/CD) | ✅ Working — push to `release` branch triggers `release.yml` |
| Prefab selection per region kit | 🔲 Planned |
| Race logic, checkpoints, HUD | 🔲 Planned |
| AI opponents | 🔲 Planned |

For the full roadmap see the [main README](README.md#phased-implementation-plan).
