# DataInversion Scripts

Parsing utilities that convert raw OpenStreetMap XML into strongly-typed C# data structures.

| File | Purpose |
|---|---|
| `OSMParser.cs` | Reads an `.osm` file and produces `List<RoadSegment>` and `List<BuildingFootprint>` |

## OSMParser

```csharp
var (roads, buildings) = OSMParser.Parse("Assets/Data/london.osm", originLat, originLon);
```

### RoadSegment

| Property | Type | Description |
|---|---|---|
| `WayId` | `long` | OSM way identifier |
| `HighwayType` | `string` | Value of the `highway` tag (e.g. `"primary"`, `"residential"`) |
| `Nodes` | `List<Vector3>` | World-space XZ positions (Y = 0) of the way's nodes |
| `Tags` | `Dictionary<string,string>` | All OSM tags on this way |

### BuildingFootprint

| Property | Type | Description |
|---|---|---|
| `WayId` | `long` | OSM way identifier |
| `Footprint` | `List<Vector3>` | Ordered world-space XZ corners of the building outline |
| `Tags` | `Dictionary<string,string>` | All OSM tags (e.g. `building:levels`) |
