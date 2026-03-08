# HUD Scripts

Head-up display utilities that consume game state and render it for the player.

| File | Purpose |
|---|---|
| `MinimapRenderer.cs` | Converts road segments to normalised [0, 1] minimap line coordinates relative to the player's position |

## MinimapRenderer

`MinimapRenderer` is a plain C# class (not a `MonoBehaviour`) that can be created and called
from any HUD script.  It projects nearby road segments into a normalised minimap coordinate
space where **(0.5, 0.5) is always the player**.

```csharp
var minimap = new MinimapRenderer { Radius = 200f };
List<MinimapLine> lines = minimap.BuildLines(mapData.Roads, carTransform.position, carYaw);

// Draw each line on a Unity UI RawImage or via GL/Canvas:
foreach (MinimapLine line in lines)
{
    // line.Start and line.End are in [0, 1] minimap space
    // line.RoadType can be used to pick a colour (e.g. motorway = yellow, residential = white)
}
```

### Properties

| Property | Default | Description |
|---|---|---|
| `Radius` | `150 m` | World-space radius of the visible minimap area.  Roads whose both endpoints are outside this radius are excluded. |

### MinimapLine

Each `MinimapLine` returned by `BuildLines` has:

| Property | Type | Description |
|---|---|---|
| `Start` | `Vector2` | Start point in normalised [0, 1] minimap space |
| `End` | `Vector2` | End point in normalised [0, 1] minimap space |
| `RoadType` | `RoadType` | Functional road type — use to select the line colour when rendering |

### Player-rotation support

Pass the player's current yaw angle (degrees, clockwise from north / +Z) as the third argument
to `BuildLines`.  When non-zero, the entire map is rotated so the player's forward direction
always points toward the top of the minimap display.
