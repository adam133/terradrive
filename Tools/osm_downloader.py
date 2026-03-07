"""
osm_downloader.py
-----------------
Downloads OpenStreetMap road and building data via the Overpass API for a
given GPS coordinate and radius, then saves the result as a standard .osm file.

Usage:
    python osm_downloader.py --lat 51.5074 --lon -0.1278 --radius 5000 --output ../Assets/Data/london.osm

Requirements:
    pip install requests
"""

import argparse
import os
import sys

import requests

OVERPASS_URL = "https://overpass-api.de/api/interpreter"

OVERPASS_QUERY_TEMPLATE = """
[out:xml][timeout:90];
(
  way["highway"](around:{radius},{lat},{lon});
  way["building"](around:{radius},{lat},{lon});
);
(._;>;);
out body;
""".strip()


def build_query(lat: float, lon: float, radius: int) -> str:
    """Return an Overpass QL query string for the given position and radius."""
    return OVERPASS_QUERY_TEMPLATE.format(lat=lat, lon=lon, radius=radius)


def download_osm(lat: float, lon: float, radius: int) -> str:
    """
    Query the Overpass API and return the raw OSM XML response.

    Parameters
    ----------
    lat : float
        Centre latitude in decimal degrees (WGS-84).
    lon : float
        Centre longitude in decimal degrees (WGS-84).
    radius : int
        Search radius in metres.

    Returns
    -------
    str
        Raw OSM XML string.

    Raises
    ------
    requests.HTTPError
        If the Overpass API returns a non-2xx status code.
    """
    query = build_query(lat, lon, radius)
    print(f"Querying Overpass API (lat={lat}, lon={lon}, radius={radius}m)...")
    response = requests.post(OVERPASS_URL, data={"data": query}, timeout=120)
    response.raise_for_status()
    print(f"Received {len(response.content):,} bytes from Overpass API.")
    return response.text


def save_osm(content: str, output_path: str) -> None:
    """Write *content* to *output_path*, creating parent directories as needed."""
    parent = os.path.dirname(os.path.abspath(output_path))
    os.makedirs(parent, exist_ok=True)
    with open(output_path, "w", encoding="utf-8") as fh:
        fh.write(content)
    print(f"Saved OSM data to: {output_path}")


def parse_args(argv=None):
    parser = argparse.ArgumentParser(
        description="Download OSM road/building data via the Overpass API.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--lat", type=float, required=True, help="Centre latitude (WGS-84)")
    parser.add_argument("--lon", type=float, required=True, help="Centre longitude (WGS-84)")
    parser.add_argument("--radius", type=int, default=5000, help="Search radius in metres")
    parser.add_argument("--output", default="output.osm", help="Output .osm file path")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    try:
        content = download_osm(args.lat, args.lon, args.radius)
        save_osm(content, args.output)
    except requests.HTTPError as exc:
        print(f"ERROR: Overpass API request failed: {exc}", file=sys.stderr)
        sys.exit(1)
    except requests.RequestException as exc:
        print(f"ERROR: Network error: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
