# Tools

Editor and command-line utilities for the TerraDrive pipeline.

---

## osm_downloader.py

Downloads road and building data from the [Overpass API](https://overpass-api.de/) for a given GPS coordinate and radius, then saves the result as an `.osm` file for use in Unity.

### Requirements

```bash
pip install requests
```

### Usage

```bash
python osm_downloader.py --lat <latitude> --lon <longitude> [--radius <meters>] [--output <path>]
```

| Argument | Default | Description |
|---|---|---|
| `--lat` | *(required)* | Centre latitude (WGS-84) |
| `--lon` | *(required)* | Centre longitude (WGS-84) |
| `--radius` | `5000` | Search radius in metres |
| `--output` | `output.osm` | Path to write the `.osm` file |

### Example

```bash
# Download 5 km of roads around central London
python osm_downloader.py --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm

# Download 2 km of roads around Shibuya, Tokyo
python osm_downloader.py --lat 35.6595 --lon 139.7004 --radius 2000 --output ../Assets/Data/tokyo_shibuya.osm
```

### Output

The tool writes a standard `.osm` XML file. The file is saved to the path specified by `--output` (directories are created automatically).

### Overpass Query

The downloader uses the following Overpass QL template, which fetches:

- All **highway** ways (roads, paths, etc.)
- All **building** ways (footprints for procedural extrusion)
- All nodes referenced by those ways

```
[out:xml][timeout:90];
(
  way["highway"](around:<radius>,<lat>,<lon>);
  way["building"](around:<radius>,<lat>,<lon>);
);
(._;>;);
out body;
```
