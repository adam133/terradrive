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
| Game Engine | Unity 2022.3 LTS (current implementation) / Unreal Engine 5 (PCG + Nanite, production goal) |
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
      /Procedural    ← Mesh generation (Roads, Buildings)
      /Vehicle       ← Physics and controls
    /Prefabs         ← Asset kits (Signs, Houses, Foliage)
    /Shaders         ← World-mapping and road textures
  /Tools             ← Editor scripts for map downloading
```

---

## Phased Implementation Plan

### Phase 1 — Data Scraper (Python / C#)

**Goal:** Given a GPS coordinate, produce a clean `.osm` file of nearby roads and elevation data.

- Use the [Overpass API](https://overpass-api.de/) to download road data within a configurable radius (default 5 km).
- Save `.osm` files to a local project folder for offline / editor use.
- See [`Tools/osm_downloader.py`](Tools/osm_downloader.py) and [`Tools/README.md`](Tools/README.md).

### Phase 2 — Spline Generator

**Goal:** Convert raw OSM nodes (lat/lon points) into smooth Catmull-Rom splines visible in the editor.

- Parse OSM `<way>` elements tagged with `highway` into `RoadSegment` objects.
- Project WGS-84 GPS coordinates to Unity world-space XZ using a Mercator projection.
- Fit a Catmull-Rom spline through the projected points.
- See [`Assets/Scripts/DataInversion/OSMParser.cs`](Assets/Scripts/DataInversion/OSMParser.cs) and
  [`Assets/Scripts/Procedural/SplineGenerator.cs`](Assets/Scripts/Procedural/SplineGenerator.cs).

### Phase 3 — Mesh Extruder

**Goal:** Turn a spline into a drivable, UV-mapped 3D road mesh.

- Extrude a configurable-width road mesh along each spline.
- Generate UV coordinates suitable for a tiling asphalt texture.
- See [`Assets/Scripts/Procedural/RoadMeshExtruder.cs`](Assets/Scripts/Procedural/RoadMeshExtruder.cs).

### Phase 4 — Biomes & Asset Scatterer

**Goal:** Populate roadsides with region-appropriate props (signs, lamp posts, buildings).

- Read the `country` or `addr:country` tag from OSM nodes.
- Select prefabs from the matching regional kit folder (`European_Kit`, `Asian_Kit`, etc.).
- Extrude building footprints into 3D meshes with randomised heights.
- See [`Assets/Scripts/Procedural/BuildingGenerator.cs`](Assets/Scripts/Procedural/BuildingGenerator.cs).

---

## Getting Started

### Prerequisites

- Unity 2022.3 LTS or later (URP or HDRP recommended)
- Python 3.9+ (for the OSM downloader tool)
- `requests` Python package (`pip install requests`)

### Download Map Data

```bash
cd Tools
python osm_downloader.py --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm
```

### Open in Unity

1. Open the project in Unity.
2. In the editor, navigate to **TerraDrive → Load OSM File** and select the downloaded `.osm` file.
3. Click **Generate Level** to build the splines, road meshes, and roadside assets.

---

## Development Tips

- **Work in Editor Mode first.** Write Editor Scripts that let you click a button to download the map and generate the level. Move to runtime generation only after the pipeline is solid.
- **Use Mock Data.** Before battling the Overpass API, feed the parser a small hand-crafted JSON/XML file with four coordinates to build a test road.
- **The Y-Axis Problem.** Real-world coordinates are Latitude/Longitude; Unity uses metres. Always use `CoordinateConverter.LatLonToUnity()` (see [`Assets/Scripts/Core/CoordinateConverter.cs`](Assets/Scripts/Core/CoordinateConverter.cs)) to convert WGS-84 to world space.

---

## Contributing

Pull requests are welcome. Please open an issue first to discuss major changes.

## License

MIT
