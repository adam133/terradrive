# Shaders

Custom shader assets for world-mapped road surfaces and terrain blending.

| Shader | Purpose |
|---|---|
| `RoadSurface` | Tiling asphalt with world-space UV blending and wet-road cubemap reflections |
| `TerrainBlend` | Blends up to four terrain layers (grass, dirt, gravel, rock) by vertex colour |

## Road Surface Shader

The road shader samples an asphalt albedo + normal map at a world-space UV scale of
approximately 1 unit = 1 metre, so the texture tiles naturally regardless of road length.
A secondary detail normal map adds micro-surface roughness variation.

## Terrain Blend Shader

Uses vertex colours painted by the procedural terrain system to blend between biome-specific
ground textures.  Red = grass, Green = dirt/soil, Blue = gravel, Alpha = rock/asphalt shoulder.
