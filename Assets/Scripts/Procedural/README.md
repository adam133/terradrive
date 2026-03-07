# Procedural Scripts

Mesh-generation systems that convert parsed OSM data into drivable geometry.

| File | Purpose |
|---|---|
| `SplineGenerator.cs` | Builds Catmull-Rom splines from lists of `Vector3` road nodes |
| `RoadMeshExtruder.cs` | Extrudes a UV-mapped road mesh along a spline |
| `BuildingGenerator.cs` | Extrudes building footprints into 3D meshes with randomised heights |

## SplineGenerator

```csharp
var spline = SplineGenerator.BuildCatmullRom(nodes, samplesPerSegment: 20);
```

Returns a `List<Vector3>` of evenly-sampled world-space positions along the smoothed curve.

## RoadMeshExtruder

```csharp
var mesh = RoadMeshExtruder.Extrude(splinePoints, roadWidth: 7f);
meshFilter.sharedMesh = mesh;
```

UV coordinates tile along the road length so an asphalt texture repeats at a natural scale.

## BuildingGenerator

```csharp
var (wallMesh, roofMesh) = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f);
```

Heights are randomised per-building using a seeded RNG derived from the OSM `WayId`, so the
same map always produces the same city skyline.
