# Procedural Scripts

Mesh-generation systems that convert parsed OSM data into drivable geometry and roadside props.

| File | Purpose |
|---|---|
| `SplineGenerator.cs` | Builds Catmull-Rom splines from lists of `Vector3` road nodes |
| `RoadMeshExtruder.cs` | Extrudes a UV-mapped road mesh (with optional kerbs and lane markings) along a spline |
| `RoadMeshResult.cs` | Container returned by `RoadMeshExtruder.ExtrudeWithDetails` holding road and kerb meshes plus texture IDs |
| `BridgeElevator.cs` | Smoothly raises a road spline's Y coordinates when the road is a bridge or overpass |
| `RoadSurfaceDeformer.cs` | Applies procedural Y-axis noise to spline points to simulate road surface roughness |
| `BuildingGenerator.cs` | Extrudes building footprints into 3D meshes with randomised heights |
| `BuildingMeshResult.cs` | Result type returned by `BuildingGenerator.Extrude` (meshes + texture IDs) |
| `WaterMeshGenerator.cs` | Generates flat water-surface meshes from `WaterBody` polygon outlines and selects region-appropriate texture IDs |
| `WaterMeshResult.cs` | Result type returned by `WaterMeshGenerator.Generate` (flat mesh + texture ID) |
| `RegionTextures.cs` | Maps `RegionType` to region-appropriate texture asset IDs for roads, buildings, water, and lane markings |
| `MaterialRegistry.cs` | Scene MonoBehaviour that maps texture-ID strings to Unity `Material` assets; apply via `ApplyTo(renderer, textureId)` |
| `PlaceholderMaterialFactory.cs` | Creates solid-colour placeholder `Material` objects for all known texture IDs so meshes render with a recognisable tint before real materials are assigned |
| `RoadsidePropPlacer.cs` | Scatters roadside props (lamp posts, trees, signs, fences) along a road spline |
| `PropPlacement.cs` | Data struct describing a single prop's world-space position, orientation, and type |
| `PropType.cs` | Enum of prop types: `LampPost`, `Tree`, `SignPost`, `Fence` |

## SplineGenerator

```csharp
var spline = SplineGenerator.BuildCatmullRom(nodes, samplesPerSegment: 20);
```

Returns a `List<Vector3>` of evenly-sampled world-space positions along the smoothed curve.

## BridgeElevator

When a `RoadSegment` has `IsBridge = true` (parsed from the OSM `bridge` tag), pass the
spline through `BridgeElevator.ApplyElevation` before extrusion.  The method returns a new
list of points whose Y coordinates are smoothly ramped up at the approach, held at the peak
height through the span, and ramped back down at the departure — using a smooth-step curve
to avoid sharp kinks at the bridge ends.

```csharp
if (road.IsBridge)
{
    splinePoints = BridgeElevator.ApplyElevation(splinePoints);
}
RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, roadType);
```

| Parameter | Default | Description |
|---|---|---|
| `bridgeHeight` | `4.5 m` | Height added at the peak of the bridge above the surrounding surface |
| `rampFraction` | `0.2` | Fraction of the spline length used for each approach/departure ramp (20 %) |

The first `rampFraction` of spline points ascend using a cubic smooth-step curve; the middle
`1 − 2 × rampFraction` are flat at full height; and the last `rampFraction` descend
symmetrically.

## RoadMeshExtruder

Simple road mesh:

```csharp
Mesh roadMesh = RoadMeshExtruder.Extrude(splinePoints, RoadType.Residential);
meshFilter.sharedMesh = roadMesh;
```

With kerbs, lane-marking UV channel, and region-appropriate texture IDs:

```csharp
RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, RoadType.Primary, region: region);
roadFilter.sharedMesh = result.RoadMesh;   // UV0 = asphalt (10 m tile), UV1 = lane markings (6 m tile)
kerbFilter.sharedMesh = result.KerbMesh;   // 4 verts/point, elevated by 0.05 m, width 0.15 m
materialRegistry.ApplyTo(roadRenderer, result.RoadTextureId);
materialRegistry.ApplyTo(kerbRenderer, result.KerbTextureId);
```

Road widths are selected automatically by `RoadType` (e.g. `Motorway` = 20 m, `Residential` = 5.5 m).

## BuildingGenerator

```csharp
BuildingMeshResult result = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f, wayId, region);
wallFilter.sharedMesh = result.WallMesh;
roofFilter.sharedMesh = result.RoofMesh;
materialRegistry.ApplyTo(wallRenderer, result.WallTextureId);
materialRegistry.ApplyTo(roofRenderer, result.RoofTextureId);
```

Heights are randomised per-building using a seeded RNG derived from the OSM `WayId`, so the
same map always produces the same city skyline.

## MaterialRegistry

`MaterialRegistry` is a scene MonoBehaviour that bridges the texture-ID strings produced by
`RegionTextures` (and carried in `RoadMeshResult` / `BuildingMeshResult` / `WaterMeshResult`)
to real Unity `Material` assets.

Add it to a GameObject in the scene and populate the **Entries** list in the Inspector — one
row per texture ID.  All 25 road/kerb/building IDs are pre-populated in
`Assets/Scenes/ProofOfConcept.unity` with null material slots; drag your material assets into
each slot.  The additional water, terrain, and lane-marking IDs are filled automatically at
runtime by `PlaceholderMaterialFactory` for any slot that has no assigned material.

```csharp
// Apply road and kerb materials after extrusion:
RoadMeshResult road = RoadMeshExtruder.ExtrudeWithDetails(spline, roadType, region: region);
roadRenderer.sharedMesh = road.RoadMesh;
materialRegistry.ApplyTo(roadRenderer, road.RoadTextureId);

kerbRenderer.sharedMesh = road.KerbMesh;
materialRegistry.ApplyTo(kerbRenderer, road.KerbTextureId);
```

You can also register materials at runtime:

```csharp
materialRegistry.Register("road_asphalt_temperate", myMaterial);
```

`GetMaterial(textureId)` returns the registered `Material` or `null` if the ID is not found.
`ApplyTo(renderer, textureId)` is a no-op when the renderer or material is null.

## PlaceholderMaterialFactory

`PlaceholderMaterialFactory` (internal) creates solid-colour `Material` objects for every
known texture ID so that meshes render with a recognisable tint rather than Unity's default
magenta "missing material" colour.  It is invoked automatically by `MaterialRegistry` in
`Awake` via `PlaceholderMaterialFactory.FillMissing(registry)` — existing
designer-assigned materials are never overwritten.

Placeholder colours are intentionally distinct per category: asphalt roads (dark grey),
dirt/unpaved (brown), kerbs (light grey), building walls (warm tan), building roofs
(dark brown-grey), terrain (grass green), water (blue), lane markings (white).

## RegionTextures

Provides the texture identifier look-up used by both `RoadMeshExtruder` and `BuildingGenerator`.
Can also be called directly to retrieve individual texture IDs:

```csharp
string roadTex  = RegionTextures.GetRoadSurfaceTextureId(region, roadType);
string kerbTex  = RegionTextures.GetKerbTextureId(region);
string wallTex  = RegionTextures.GetWallTextureId(region);
string roofTex  = RegionTextures.GetRoofTextureId(region);
string waterTex = RegionTextures.GetWaterTextureId(region);
string laneTex  = RegionTextures.GetLaneMarkingTextureId(isOneWay);
```

### Region → texture mapping

| Region | Road surface | Kerb | Wall | Roof | Water |
|---|---|---|---|---|---|
| Temperate | `road_asphalt_temperate` | `kerb_stone` | `building_wall_brick` | `building_roof_slate` | `water` |
| Desert | `road_asphalt_desert` | `kerb_concrete` | `building_wall_sandstone` | `building_roof_terracotta` | `water` |
| Tropical | `road_asphalt_tropical` | `kerb_concrete` | `building_wall_stucco` | `building_roof_terracotta` | `water_tropical` |
| Boreal | `road_asphalt_boreal` | `kerb_stone` | `building_wall_timber` | `building_roof_metal` | `water` |
| Arctic | `road_asphalt_arctic` | `kerb_concrete` | `building_wall_concrete` | `building_roof_metal` | `water_arctic` |
| Mediterranean | `road_asphalt_mediterranean` | `kerb_granite` | `building_wall_stucco` | `building_roof_terracotta` | `water` |
| Steppe | `road_asphalt_steppe` | `kerb_concrete` | `building_wall_concrete` | `building_roof_flat` | `water` |
| Unknown | `road_asphalt` | `kerb_stone` | `building_wall_brick` | `building_roof_slate` | `water` |

Unpaved roads (`RoadType.Dirt` and `RoadType.Path`) use a surface texture that varies by region:
`road_dirt` (default), `road_sand` (Desert), `road_mud` (Tropical), `road_gravel_boreal` (Boreal),
`road_gravel_arctic` (Arctic).

Lane-marking textures (`lane_marking_oneway` / `lane_marking_twoway`) are determined by whether
the road is one-way, independently of region.

## RoadSurfaceDeformer

Applies procedural Y-axis imperfections to a road spline to simulate real-world surface roughness
(dips, bumps, potholes).  Roughness amplitude is keyed by `RoadType` — motorways are near-flat
(`0.005 m`) while dirt tracks have the largest variation (`0.15 m`).

```csharp
List<Vector3> deformed = RoadSurfaceDeformer.Deform(splinePoints, RoadType.Residential, seed: (int)wayId);
RoadMeshResult result  = RoadMeshExtruder.ExtrudeWithDetails(deformed, RoadType.Residential);
```

The deformation uses two blended octaves of 1-D value noise (low-frequency broad undulations +
high-frequency sharp bumps) seeded from the OSM way ID so the same map always produces the same
surface profile.

## RoadsidePropPlacer

```csharp
List<PropPlacement> props = RoadsidePropPlacer.Place(splinePoints, RoadType.Residential, region, wayId);
```

Returns paired left/right `PropPlacement` entries at regular spacing intervals.  Prop type and
spacing vary by road class and climate region (e.g. trees are suppressed in desert/arctic zones).

## WaterMeshGenerator

```csharp
WaterMeshResult result = WaterMeshGenerator.Generate(waterBody, region: region);
meshFilter.sharedMesh = result.Mesh;
materialRegistry.ApplyTo(meshRenderer, result.TextureId);
```

Generates a flat fan-triangulated polygon mesh for a water body polygon.  Vertex Y coordinates
are set to the average elevation of the outline nodes so the surface lies flat at the correct
terrain height.  At least 3 outline points are required; outlines with fewer points produce an
empty mesh.
