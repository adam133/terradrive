# Terrain Scripts

Elevation data sources for applying real-world DEM (Digital Elevation Model) heights to road
splines and building footprints.

| File | Purpose |
|---|---|
| `IElevationSource.cs` | Interface for pluggable DEM backends |
| `OpenElevationSource.cs` | SRTM 30 m elevation via the [Open-Elevation API](https://open-elevation.com) (no API key required) |

## IElevationSource

```csharp
IReadOnlyList<double> elevations = await source.FetchElevationsAsync(
    new[] { (51.5074, -0.1278), (48.8566, 2.3522) });
```

All implementations return elevations in the same order as the supplied locations list so
callers can correlate results by index.

## OpenElevationSource

Uses the public Open-Elevation REST API (backed by SRTM data) to resolve a batch of
geographic coordinates in a single POST request.

```csharp
var source = new OpenElevationSource();                    // uses public endpoint
var source = new OpenElevationSource(httpClient, url);     // inject client / self-hosted endpoint
IReadOnlyList<double> elevations = await source.FetchElevationsAsync(locations);
```

Pass the returned elevation values to `CoordinateConverter.LatLonToUnity(lat, lon, elevMetres)`
to set the Unity Y axis from real-world terrain heights.

### Why Open-Elevation / SRTM?

- **Free, no API key** — unlike Cesium ion or commercial services.
- **Global coverage** — SRTM covers land between ±60° latitude (virtually all driving areas).
- **30 m resolution** — adequate for road and terrain mesh generation.
- **Self-hostable** — swap the default endpoint for a private instance for offline use.
