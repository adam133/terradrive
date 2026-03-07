# DataInversion Scripts

Parsing utilities that convert raw OpenStreetMap XML into strongly-typed C# data structures.

| File | Purpose |
|---|---|
| `OSMParser.cs` | Reads an `.osm` file and produces `List<RoadSegment>`, `List<BuildingFootprint>`, and a `RegionType` |
| `RegionType.cs` | Enum of broad climate/biome zones inferred from OSM country tags |
| `MapNode.cs` | Struct representing a single OSM node (id, lat, lon, elevation) |
| `MapWay.cs` | Class representing a single OSM way (id, nodes, tags, road type) |
| `RoadType.cs` | Enum classifying an OSM way by road surface or functional category |

## OSMParser

```csharp
var (roads, buildings, region) = OSMParser.Parse("Assets/Data/london.osm", originLat, originLon);
```

The parser reads `country` and `addr:country` tags from OSM nodes to detect the map's region.
The most common country code found in those tags is mapped to a `RegionType` value.

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

## RegionType

| Value | Climate / Biome | Example countries |
|---|---|---|
| `Unknown` | Could not be determined | — |
| `Temperate` | Four seasons, moderate rainfall | GB, DE, FR, US, JP |
| `Desert` | Arid, extreme heat | SA, AE, EG, AU |
| `Tropical` | High heat and humidity | BR, ID, NG, TH |
| `Boreal` | Long cold winters (taiga) | RU, FI, SE, NO |
| `Arctic` | Permafrost, tundra | GL, IS, SJ |
| `Mediterranean` | Hot dry summers, mild wet winters | ES, IT, GR, IL |
| `Steppe` | Grassland, semi-arid continental | KZ, MN, UA |
