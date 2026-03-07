# Procedural Scripts

Mesh-generation systems that convert parsed OSM data into drivable geometry and roadside props.

| File | Purpose |
|---|---|
| `SplineGenerator.cs` | Builds Catmull-Rom splines from lists of `Vector3` road nodes |
| `RoadMeshExtruder.cs` | Extrudes a UV-mapped road mesh (with optional kerbs and lane markings) along a spline |
| `RoadMeshResult.cs` | Container returned by `RoadMeshExtruder.ExtrudeWithDetails` holding `RoadMesh` and `KerbMesh` |
| `BridgeElevator.cs` | Smoothly raises a road spline's Y coordinates when the road is a bridge or overpass |
| `BuildingGenerator.cs` | Extrudes building footprints into 3D meshes with randomised heights |
| `RegionTextures.cs` | Maps `RegionType` to region-appropriate texture asset IDs for roads and buildings |
| `BuildingMeshResult.cs` | Result type returned by `BuildingGenerator.Extrude` (meshes + texture IDs) |
| `RoadMeshResult.cs` | Result type returned by `RoadMeshExtruder.ExtrudeWithDetails` (meshes + texture IDs) |
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

With kerbs and lane-marking UV channel:

```csharp
// Simple road mesh (no texture selection):
var mesh = RoadMeshExtruder.Extrude(splinePoints, roadWidth: 7f);
meshFilter.sharedMesh = mesh;

// Detailed road mesh with region-appropriate texture identifiers:
RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, RoadType.Primary, region: region);
roadFilter.sharedMesh   = result.RoadMesh;   // UV0 = asphalt, UV1 = lane markings
kerbFilter.sharedMesh   = result.KerbMesh;
// Apply materials keyed by result.RoadTextureId and result.KerbTextureId
RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, RoadType.Primary);
roadFilter.sharedMesh = result.RoadMesh;   // UV0 = asphalt (10 m tile), UV1 = lane markings (6 m tile)
kerbFilter.sharedMesh = result.KerbMesh;   // 4 verts/point, elevated by 0.05 m, width 0.15 m
```

Road widths are selected automatically by `RoadType` (e.g. `Motorway` = 20 m, `Residential` = 5.5 m).

## BuildingGenerator

```csharp
BuildingMeshResult result = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f, region: region);
wallFilter.sharedMesh = result.WallMesh;
roofFilter.sharedMesh = result.RoofMesh;
// Apply materials keyed by result.WallTextureId and result.RoofTextureId
var (wallMesh, roofMesh) = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f, wayId);
```

Heights are randomised per-building using a seeded RNG derived from the OSM `WayId`, so the
same map always produces the same city skyline.

## RegionTextures

Provides the texture identifier look-up used by both `RoadMeshExtruder` and `BuildingGenerator`.
Can also be called directly to retrieve individual texture IDs:

```csharp
string roadTex = RegionTextures.GetRoadSurfaceTextureId(region, roadType);
string kerbTex = RegionTextures.GetKerbTextureId(region);
string wallTex = RegionTextures.GetWallTextureId(region);
string roofTex = RegionTextures.GetRoofTextureId(region);
```

### Region → texture mapping

| Region | Road surface | Kerb | Wall | Roof |
|---|---|---|---|---|
| Temperate | `road_asphalt_temperate` | `kerb_stone` | `building_wall_brick` | `building_roof_slate` |
| Desert | `road_asphalt_desert` | `kerb_concrete` | `building_wall_sandstone` | `building_roof_terracotta` |
| Tropical | `road_asphalt_tropical` | `kerb_concrete` | `building_wall_stucco` | `building_roof_terracotta` |
| Boreal | `road_asphalt_boreal` | `kerb_stone` | `building_wall_timber` | `building_roof_metal` |
| Arctic | `road_asphalt_arctic` | `kerb_concrete` | `building_wall_concrete` | `building_roof_metal` |
| Mediterranean | `road_asphalt_mediterranean` | `kerb_granite` | `building_wall_stucco` | `building_roof_terracotta` |
| Steppe | `road_asphalt_steppe` | `kerb_concrete` | `building_wall_concrete` | `building_roof_flat` |
| Unknown | `road_asphalt` | `kerb_stone` | `building_wall_brick` | `building_roof_slate` |

Unpaved roads (`RoadType.Dirt` and `RoadType.Path`) use a surface texture that varies by region:
`road_dirt` (default), `road_sand` (Desert), `road_mud` (Tropical), `road_gravel_boreal` (Boreal),
`road_gravel_arctic` (Arctic).
## RoadsidePropPlacer

```csharp
List<PropPlacement> props = RoadsidePropPlacer.Place(splinePoints, RoadType.Residential, region, wayId);
```

Returns paired left/right `PropPlacement` entries at regular spacing intervals.  Prop type and
spacing vary by road class and climate region (e.g. trees are suppressed in desert/arctic zones).
