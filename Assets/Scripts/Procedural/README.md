# Procedural Scripts

Mesh-generation systems that convert parsed OSM data into drivable geometry and roadside props.

| File | Purpose |
|---|---|
| `SplineGenerator.cs` | Builds Catmull-Rom splines from lists of `Vector3` road nodes |
| `RoadMeshExtruder.cs` | Extrudes a UV-mapped road mesh (with optional kerbs and lane markings) along a spline |
| `RoadMeshResult.cs` | Container returned by `RoadMeshExtruder.ExtrudeWithDetails` holding `RoadMesh` and `KerbMesh` |
| `BuildingGenerator.cs` | Extrudes building footprints into 3D meshes with randomised heights |
| `RoadsidePropPlacer.cs` | Scatters roadside props (lamp posts, trees, signs, fences) along a road spline |
| `PropPlacement.cs` | Data struct describing a single prop's world-space position, orientation, and type |
| `PropType.cs` | Enum of prop types: `LampPost`, `Tree`, `SignPost`, `Fence` |

## SplineGenerator

```csharp
var spline = SplineGenerator.BuildCatmullRom(nodes, samplesPerSegment: 20);
```

Returns a `List<Vector3>` of evenly-sampled world-space positions along the smoothed curve.

## RoadMeshExtruder

Simple road mesh:

```csharp
Mesh roadMesh = RoadMeshExtruder.Extrude(splinePoints, RoadType.Residential);
meshFilter.sharedMesh = roadMesh;
```

With kerbs and lane-marking UV channel:

```csharp
RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, RoadType.Primary);
roadFilter.sharedMesh = result.RoadMesh;   // UV0 = asphalt (10 m tile), UV1 = lane markings (6 m tile)
kerbFilter.sharedMesh = result.KerbMesh;   // 4 verts/point, elevated by 0.05 m, width 0.15 m
```

Road widths are selected automatically by `RoadType` (e.g. `Motorway` = 20 m, `Residential` = 5.5 m).

## BuildingGenerator

```csharp
var (wallMesh, roofMesh) = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f, wayId);
```

Heights are randomised per-building using a seeded RNG derived from the OSM `WayId`, so the
same map always produces the same city skyline.

## RoadsidePropPlacer

```csharp
List<PropPlacement> props = RoadsidePropPlacer.Place(splinePoints, RoadType.Residential, region, wayId);
```

Returns paired left/right `PropPlacement` entries at regular spacing intervals.  Prop type and
spacing vary by road class and climate region (e.g. trees are suppressed in desert/arctic zones).
