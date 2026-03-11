# DataInversion Scripts

Parsing utilities that convert raw OpenStreetMap XML into strongly-typed C# data structures.

| File | Purpose |
|---|---|
| `OSMParser.cs` | Reads an `.osm` file and produces `List<RoadSegment>`, `List<BuildingFootprint>`, `List<WaterBody>`, and a `RegionType` |
| `RegionType.cs` | Enum of broad climate/biome zones inferred from OSM country tags |
| `MapNode.cs` | Struct representing a single OSM node (id, lat, lon, elevation) |
| `MapWay.cs` | Class representing a single OSM way (id, nodes, tags, road type) |
| `RoadType.cs` | Enum classifying an OSM way by road surface or functional category |
| `WaterBody.cs` | Class representing a water body polygon (lake, pond, riverbank, reservoir) parsed from an OSM way |

## OSMParser

```csharp
// Synchronous parse — node Y coordinates are always 0.
var (roads, buildings, waterBodies, region) = OSMParser.Parse("Assets/Data/london.osm", originLat, originLon);

// Async parse with terrain elevation — node Y coordinates are set from the DEM source.
var (roads, buildings, waterBodies, region) = await OSMParser.ParseAsync(
    "Assets/Data/london.osm", originLat, originLon, elevationSource);
```

The parser reads `country` and `addr:country` tags from OSM nodes to detect the map's region.
The most common country code found in those tags is mapped to a `RegionType` value.

Pass an `ElevationGrid` (which implements `IElevationSource`) as the `elevationSource`
argument to `ParseAsync` to raise road splines and building footprints to match the terrain
surface without additional network requests — see
[`Assets/Scripts/Terrain/README.md`](../Terrain/README.md) for the full scene wiring pattern.

### RoadSegment

| Property | Type | Description |
|---|---|---|
| `WayId` | `long` | OSM way identifier |
| `HighwayType` | `string` | Value of the `highway` tag (e.g. `"primary"`, `"residential"`) |
| `Nodes` | `List<Vector3>` | World-space positions of the way's nodes. Y = 0 with `Parse`; Y = terrain elevation (metres) with `ParseAsync` |
| `Tags` | `Dictionary<string,string>` | All OSM tags on this way |
| `IsBridge` | `bool` | `true` when the way has a `bridge` tag with a value other than `"no"` (e.g. `"yes"`, `"viaduct"`) |
| `Lanes` | `int` | Number of lanes from the OSM `lanes` tag; `0` when absent or unparseable |
| `IsOneWay` | `bool` | `true` when the OSM `oneway` tag is `"yes"`, `"1"`, or `"true"` |

### BuildingFootprint

| Property | Type | Description |
|---|---|---|
| `WayId` | `long` | OSM way identifier |
| `Footprint` | `List<Vector3>` | Ordered world-space corner positions of the building outline. Y = 0 with `Parse`; Y = terrain elevation (metres) with `ParseAsync` |
| `Tags` | `Dictionary<string,string>` | All OSM tags (e.g. `building:levels`) |

### WaterBody

| Property | Type | Description |
|---|---|---|
| `WayId` | `long` | OSM way identifier |
| `Outline` | `List<Vector3>` | Ordered world-space corner positions of the water polygon. Y = terrain elevation |
| `Tags` | `Dictionary<string,string>` | All OSM tags on this way |
| `WaterType` | `string` | Water sub-type derived from OSM tags (e.g. `"lake"`, `"pond"`, `"reservoir"`, `"riverbank"`); defaults to `"water"` |

OSM ways are classified as water bodies when they carry `natural=water`, `waterway=riverbank`, `waterway=dock`, or `landuse=reservoir` tags and have at least 3 nodes.

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
