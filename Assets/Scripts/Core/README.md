# Core Scripts

Game-wide managers, state machines, and coordinate utilities.

| File | Purpose |
|---|---|
| `GameManager.cs` | Singleton entry-point; owns the high-level game state machine |
| `CoordinateConverter.cs` | Converts WGS-84 GPS coordinates to Unity world-space XZ metres |

## CoordinateConverter

Call `CoordinateConverter.LatLonToUnity(lat, lon, originLat, originLon)` to project any GPS
coordinate relative to a map origin into Unity world-space metres.  The origin is typically the
centre coordinate passed to the Overpass downloader.

## GameManager

`GameManager.Instance` exposes the current `GameState` enum and fires `OnStateChanged` events so
other systems can react without tight coupling.
