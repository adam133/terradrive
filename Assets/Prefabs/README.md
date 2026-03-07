# Prefabs

Pre-built asset kits for roadside props, foliage, and buildings.

Place regional asset kits in sub-folders. `BuildingGenerator` and the asset scatterer in
`Procedural` will select prefabs from the appropriate folder based on the OSM `addr:country` tag.

## Suggested Structure

```
/Prefabs
  /European_Kit
    /Signs
    /LampPosts
    /Houses
    /Foliage
  /Asian_Kit
    /Signs
    /LampPosts
    /Houses
    /Foliage
  /Generic
    /Barriers
    /TrafficCones
```

## Asset Sources

- [Synty Studios](https://syntystore.com/) — low-poly style kits (POLYGON City Pack, etc.)
- [Quixel Megascans](https://quixel.com/megascans) — photorealistic surface and prop scans (free with Unreal Engine)
