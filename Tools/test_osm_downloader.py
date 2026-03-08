"""
test_osm_downloader.py
----------------------
Unit tests for osm_downloader.py.

Verifies that build_query, download_osm, and save_osm work correctly, and
that the XML written to disk has the same structural shape as the test
Assets/Data/map.osm.xml file:

  <osm>
    <node id="..." lat="..." lon="..."/>
    <way  id="...">
      <nd  ref="..."/>
      <tag k="..."  v="..."/>
    </way>
  </osm>

Run with:
    pytest Tools/test_osm_downloader.py -v
"""

import os
import sys
import tempfile
import textwrap
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import MagicMock, patch

import pytest
import requests

# Make sure osm_downloader is importable when running from the repo root or
# from the Tools/ directory.
sys.path.insert(0, os.path.dirname(__file__))
import osm_downloader


# ---------------------------------------------------------------------------
# Minimal Overpass XML that mirrors the structure returned by the real API
# ---------------------------------------------------------------------------
SAMPLE_OVERPASS_XML = textwrap.dedent("""\
    <?xml version="1.0" encoding="UTF-8"?>
    <osm version="0.6" generator="Overpass API 0.7.62.1">
      <note>The data included in this document is from www.openstreetmap.org.</note>
      <meta osm_base="2024-01-01T00:00:00Z"/>
      <node id="1" lat="51.5000" lon="-0.1000">
        <tag k="name" v="Node A"/>
      </node>
      <node id="2" lat="51.5010" lon="-0.1010"/>
      <node id="3" lat="51.5020" lon="-0.1020"/>
      <node id="4" lat="51.5000" lon="-0.1020"/>
      <way id="100">
        <nd ref="1"/>
        <nd ref="2"/>
        <tag k="highway" v="primary"/>
        <tag k="name" v="Test Road"/>
      </way>
      <way id="200">
        <nd ref="3"/>
        <nd ref="4"/>
        <nd ref="3"/>
        <tag k="building" v="yes"/>
        <tag k="building:levels" v="3"/>
      </way>
    </osm>
""")


# ---------------------------------------------------------------------------
# build_query
# ---------------------------------------------------------------------------

class TestBuildQuery(unittest.TestCase):
    def test_contains_lat_lon_radius(self):
        q = osm_downloader.build_query(51.5, -0.1, 1000)
        self.assertIn("51.5", q)
        self.assertIn("-0.1", q)
        self.assertIn("1000", q)

    def test_highway_filter_present(self):
        q = osm_downloader.build_query(0.0, 0.0, 500)
        self.assertIn('way["highway"]', q)

    def test_building_filter_present(self):
        q = osm_downloader.build_query(0.0, 0.0, 500)
        self.assertIn('way["building"]', q)

    def test_recurse_down_present(self):
        """(._;>;) must be in the query so referenced nodes are included."""
        q = osm_downloader.build_query(0.0, 0.0, 500)
        self.assertIn("._;>;", q)

    def test_out_xml_directive(self):
        q = osm_downloader.build_query(0.0, 0.0, 500)
        self.assertIn("[out:xml]", q)

    def test_different_radii_produce_different_queries(self):
        q1 = osm_downloader.build_query(0.0, 0.0, 500)
        q2 = osm_downloader.build_query(0.0, 0.0, 2000)
        self.assertNotEqual(q1, q2)


# ---------------------------------------------------------------------------
# download_osm (HTTP mocked)
# ---------------------------------------------------------------------------

class TestDownloadOsm(unittest.TestCase):
    def _make_mock_response(self, text, status_code=200):
        mock_resp = MagicMock()
        mock_resp.text = text
        mock_resp.content = text.encode()
        mock_resp.status_code = status_code
        mock_resp.raise_for_status = MagicMock()
        return mock_resp

    @patch("osm_downloader.requests.post")
    def test_returns_response_text(self, mock_post):
        mock_post.return_value = self._make_mock_response(SAMPLE_OVERPASS_XML)

        result = osm_downloader.download_osm(51.5, -0.1, 1000)

        self.assertEqual(result, SAMPLE_OVERPASS_XML)

    @patch("osm_downloader.requests.post")
    def test_posts_to_overpass_url(self, mock_post):
        mock_post.return_value = self._make_mock_response(SAMPLE_OVERPASS_XML)

        osm_downloader.download_osm(51.5, -0.1, 1000)

        mock_post.assert_called_once()
        call_args = mock_post.call_args
        self.assertEqual(call_args[0][0], osm_downloader.OVERPASS_URL)

    @patch("osm_downloader.requests.post")
    def test_query_passed_as_data(self, mock_post):
        mock_post.return_value = self._make_mock_response(SAMPLE_OVERPASS_XML)

        osm_downloader.download_osm(51.5, -0.1, 500)

        call_kwargs = mock_post.call_args[1]
        self.assertIn("data", call_kwargs)
        self.assertIn("51.5", call_kwargs["data"]["data"])

    @patch("osm_downloader.requests.post")
    def test_raises_on_http_error(self, mock_post):
        mock_resp = self._make_mock_response("", status_code=503)
        mock_resp.raise_for_status.side_effect = requests.HTTPError("503 Service Unavailable")
        mock_post.return_value = mock_resp

        with self.assertRaises(requests.HTTPError):
            osm_downloader.download_osm(51.5, -0.1, 1000)


# ---------------------------------------------------------------------------
# Retry behaviour on 429 Too Many Requests (time.sleep mocked)
# ---------------------------------------------------------------------------

class TestRetryBehavior(unittest.TestCase):
    """
    Verifies that download_osm retries on HTTP 429 with exponential backoff,
    succeeds once a subsequent attempt returns 200, and raises after all
    retries are exhausted.

    time.sleep is patched so tests run instantly.
    """

    def _make_429(self, retry_after=None):
        mock_resp = MagicMock()
        mock_resp.status_code = 429
        headers = {}
        if retry_after is not None:
            headers["Retry-After"] = str(retry_after)
        mock_resp.headers = headers
        mock_resp.raise_for_status.side_effect = requests.HTTPError("429 Too Many Requests")
        return mock_resp

    def _make_200(self):
        mock_resp = MagicMock()
        mock_resp.status_code = 200
        mock_resp.text = SAMPLE_OVERPASS_XML
        mock_resp.content = SAMPLE_OVERPASS_XML.encode()
        mock_resp.raise_for_status = MagicMock()
        return mock_resp

    @patch("osm_downloader.time.sleep")
    @patch("osm_downloader.requests.post")
    def test_retries_on_429_and_succeeds(self, mock_post, mock_sleep):
        """A 429 followed by a 200 succeeds and returns the XML."""
        mock_post.side_effect = [self._make_429(), self._make_200()]

        result = osm_downloader.download_osm(51.5, -0.1, 1000)

        self.assertEqual(result, SAMPLE_OVERPASS_XML)
        self.assertEqual(mock_post.call_count, 2)
        mock_sleep.assert_called_once()

    @patch("osm_downloader.time.sleep")
    @patch("osm_downloader.requests.post")
    def test_raises_after_max_retries_exhausted(self, mock_post, mock_sleep):
        """Raises HTTPError once all retries are exhausted."""
        mock_post.side_effect = [self._make_429()] * (osm_downloader._MAX_RETRIES + 1)

        with self.assertRaises(requests.HTTPError):
            osm_downloader.download_osm(51.5, -0.1, 1000)

        self.assertEqual(mock_post.call_count, osm_downloader._MAX_RETRIES + 1)

    @patch("osm_downloader.time.sleep")
    @patch("osm_downloader.requests.post")
    def test_retry_after_header_sets_sleep_delay(self, mock_post, mock_sleep):
        """Retry-After header value is used as the sleep delay."""
        mock_post.side_effect = [self._make_429(retry_after=30), self._make_200()]

        osm_downloader.download_osm(51.5, -0.1, 1000)

        mock_sleep.assert_called_once_with(30.0)

    @patch("osm_downloader.time.sleep")
    @patch("osm_downloader.requests.post")
    def test_exponential_backoff_without_retry_after(self, mock_post, mock_sleep):
        """Without a Retry-After header, delay doubles on each attempt."""
        mock_post.side_effect = [
            self._make_429(),  # attempt 0 → sleep BASE * 2^0
            self._make_429(),  # attempt 1 → sleep BASE * 2^1
            self._make_200(),
        ]

        osm_downloader.download_osm(51.5, -0.1, 1000)

        delays = [call.args[0] for call in mock_sleep.call_args_list]
        self.assertEqual(delays[0], osm_downloader._BACKOFF_BASE * (2 ** 0))
        self.assertEqual(delays[1], osm_downloader._BACKOFF_BASE * (2 ** 1))

    @patch("osm_downloader.time.sleep")
    @patch("osm_downloader.requests.post")
    def test_non_429_error_raises_immediately(self, mock_post, mock_sleep):
        """A 503 error is not retried and raises immediately."""
        mock_resp = MagicMock()
        mock_resp.status_code = 503
        mock_resp.raise_for_status.side_effect = requests.HTTPError("503 Service Unavailable")
        mock_post.return_value = mock_resp

        with self.assertRaises(requests.HTTPError):
            osm_downloader.download_osm(51.5, -0.1, 1000)

        mock_post.assert_called_once()
        mock_sleep.assert_not_called()


# ---------------------------------------------------------------------------
# save_osm
# ---------------------------------------------------------------------------

class TestSaveOsm(unittest.TestCase):
    def test_file_is_written(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            path = os.path.join(tmpdir, "out.osm")
            osm_downloader.save_osm(SAMPLE_OVERPASS_XML, path)
            self.assertTrue(os.path.exists(path))

    def test_file_content_matches(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            path = os.path.join(tmpdir, "out.osm")
            osm_downloader.save_osm(SAMPLE_OVERPASS_XML, path)
            with open(path, encoding="utf-8") as fh:
                content = fh.read()
            self.assertEqual(content, SAMPLE_OVERPASS_XML)

    def test_creates_parent_directories(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            nested = os.path.join(tmpdir, "a", "b", "c", "out.osm")
            osm_downloader.save_osm(SAMPLE_OVERPASS_XML, nested)
            self.assertTrue(os.path.exists(nested))

    def test_utf8_characters_preserved(self):
        german_street = "Stra\u00dfe"  # "Straße" — non-ASCII to test UTF-8 roundtrip
        content = (
            '<?xml version="1.0"?>'
            '<osm>'
            '<node id="1" lat="0" lon="0">'
            f'<tag k="name" v="{german_street}"/>'
            '</node>'
            '</osm>'
        )
        with tempfile.TemporaryDirectory() as tmpdir:
            path = os.path.join(tmpdir, "utf8.osm")
            osm_downloader.save_osm(content, path)
            with open(path, encoding="utf-8") as fh:
                result = fh.read()
        self.assertIn(german_street, result)


# ---------------------------------------------------------------------------
# Output structure — same shape as Assets/Data/map.osm.xml
# ---------------------------------------------------------------------------

class TestOutputStructure(unittest.TestCase):
    """
    Verify that the XML written by save_osm has the same core structure as
    the repository's test OSM file (Assets/Data/map.osm.xml):

      * Root element is <osm>
      * Contains <node> elements with id, lat, lon attributes
      * Contains <way>  elements with id attribute
      * <way> elements contain <nd> children with ref attribute
      * <node> and <way> elements may contain <tag> children with k and v
    """

    def setUp(self):
        self._tmpdir = tempfile.mkdtemp()
        self._path = os.path.join(self._tmpdir, "test.osm")
        osm_downloader.save_osm(SAMPLE_OVERPASS_XML, self._path)
        self._tree = ET.parse(self._path)
        self._root = self._tree.getroot()

    def tearDown(self):
        import shutil
        shutil.rmtree(self._tmpdir, ignore_errors=True)

    def test_root_element_is_osm(self):
        self.assertEqual(self._root.tag, "osm")

    def test_osm_has_version_attribute(self):
        self.assertIn("version", self._root.attrib)

    def test_contains_node_elements(self):
        nodes = self._root.findall("node")
        self.assertGreater(len(nodes), 0, "Expected at least one <node> element")

    def test_nodes_have_id_lat_lon(self):
        for node in self._root.findall("node"):
            self.assertIn("id",  node.attrib, f"<node> missing 'id': {node.attrib}")
            self.assertIn("lat", node.attrib, f"<node> missing 'lat': {node.attrib}")
            self.assertIn("lon", node.attrib, f"<node> missing 'lon': {node.attrib}")

    def test_node_lat_lon_are_numeric(self):
        for node in self._root.findall("node"):
            float(node.attrib["lat"])   # raises ValueError if not numeric
            float(node.attrib["lon"])

    def test_contains_way_elements(self):
        ways = self._root.findall("way")
        self.assertGreater(len(ways), 0, "Expected at least one <way> element")

    def test_ways_have_id(self):
        for way in self._root.findall("way"):
            self.assertIn("id", way.attrib, f"<way> missing 'id': {way.attrib}")

    def test_ways_contain_nd_elements(self):
        for way in self._root.findall("way"):
            nds = way.findall("nd")
            self.assertGreater(len(nds), 0,
                f"<way id={way.attrib.get('id')}> has no <nd> children")

    def test_nd_elements_have_ref(self):
        for way in self._root.findall("way"):
            for nd in way.findall("nd"):
                self.assertIn("ref", nd.attrib,
                    f"<nd> inside way {way.attrib.get('id')} missing 'ref'")

    def test_nd_ref_is_numeric(self):
        for way in self._root.findall("way"):
            for nd in way.findall("nd"):
                int(nd.attrib["ref"])   # raises ValueError if not numeric

    def test_tags_have_k_and_v(self):
        for elem in list(self._root.findall("node")) + list(self._root.findall("way")):
            for tag in elem.findall("tag"):
                self.assertIn("k", tag.attrib,
                    f"<tag> missing 'k' inside {elem.tag} id={elem.attrib.get('id')}")
                self.assertIn("v", tag.attrib,
                    f"<tag> missing 'v' inside {elem.tag} id={elem.attrib.get('id')}")

    def test_highway_way_has_highway_tag(self):
        ways = {w.attrib["id"]: w for w in self._root.findall("way")}
        way100 = ways.get("100")
        self.assertIsNotNone(way100, "Expected way id=100 in sample output")
        tags = {t.attrib["k"]: t.attrib["v"] for t in way100.findall("tag")}
        self.assertEqual(tags.get("highway"), "primary")

    def test_building_way_has_building_tag(self):
        ways = {w.attrib["id"]: w for w in self._root.findall("way")}
        way200 = ways.get("200")
        self.assertIsNotNone(way200, "Expected way id=200 in sample output")
        tags = {t.attrib["k"]: t.attrib["v"] for t in way200.findall("tag")}
        self.assertIn("building", tags)

    def test_structure_matches_reference_osm_file(self):
        """
        The reference file (Assets/Data/map.osm.xml) was fetched from the
        standard OSM API.  The Overpass API returns the same structural
        elements: <osm>, <node id lat lon>, <way id>, <nd ref>, <tag k v>.
        Verify that the downloader output satisfies exactly those constraints.
        """
        # Locate the reference file relative to this test file's directory
        tools_dir = os.path.dirname(os.path.abspath(__file__))
        repo_root = os.path.dirname(tools_dir)
        ref_path = os.path.join(repo_root, "Assets", "Data", "map.osm.xml")

        if not os.path.exists(ref_path):
            self.skipTest(f"Reference file not found: {ref_path}")

        ref_root = ET.parse(ref_path).getroot()

        # Both roots must be <osm>
        self.assertEqual(self._root.tag, ref_root.tag)

        # Both must have <node> and <way> children
        self.assertGreater(len(ref_root.findall("node")), 0)
        self.assertGreater(len(ref_root.findall("way")),  0)

        # Check a small sample of nodes/ways rather than all ~50k elements.
        # 5 is enough to confirm the pattern holds for the reference format.
        _SAMPLE_SIZE = 5

        # Reference nodes have id, lat, lon — our output must too
        for node in ref_root.findall("node")[:_SAMPLE_SIZE]:
            self.assertIn("id",  node.attrib)
            self.assertIn("lat", node.attrib)
            self.assertIn("lon", node.attrib)

        # Reference ways have nd/ref children — our output must too
        for way in ref_root.findall("way")[:_SAMPLE_SIZE]:
            for nd in way.findall("nd"):
                self.assertIn("ref", nd.attrib)


# ---------------------------------------------------------------------------
# parse_args
# ---------------------------------------------------------------------------

class TestParseArgs(unittest.TestCase):
    def test_required_lat_lon(self):
        args = osm_downloader.parse_args(["--lat", "51.5", "--lon", "-0.1"])
        self.assertAlmostEqual(args.lat, 51.5)
        self.assertAlmostEqual(args.lon, -0.1)

    def test_default_radius(self):
        args = osm_downloader.parse_args(["--lat", "0", "--lon", "0"])
        self.assertEqual(args.radius, 5000)

    def test_custom_radius(self):
        args = osm_downloader.parse_args(["--lat", "0", "--lon", "0", "--radius", "1000"])
        self.assertEqual(args.radius, 1000)

    def test_default_output(self):
        args = osm_downloader.parse_args(["--lat", "0", "--lon", "0"])
        self.assertEqual(args.output, "output.osm")

    def test_custom_output(self):
        args = osm_downloader.parse_args(["--lat", "0", "--lon", "0", "--output", "my.osm"])
        self.assertEqual(args.output, "my.osm")

    def test_missing_lat_exits(self):
        with self.assertRaises(SystemExit):
            osm_downloader.parse_args(["--lon", "-0.1"])

    def test_missing_lon_exits(self):
        with self.assertRaises(SystemExit):
            osm_downloader.parse_args(["--lat", "51.5"])


# ---------------------------------------------------------------------------
# main() end-to-end (HTTP + file I/O mocked)
# ---------------------------------------------------------------------------

class TestMain(unittest.TestCase):
    @patch("osm_downloader.requests.post")
    def test_main_writes_file(self, mock_post):
        mock_resp = MagicMock()
        mock_resp.text = SAMPLE_OVERPASS_XML
        mock_resp.content = SAMPLE_OVERPASS_XML.encode()
        mock_resp.raise_for_status = MagicMock()
        mock_post.return_value = mock_resp

        with tempfile.TemporaryDirectory() as tmpdir:
            out = os.path.join(tmpdir, "result.osm")
            osm_downloader.main(["--lat", "51.5", "--lon", "-0.1", "--output", out])
            self.assertTrue(os.path.exists(out))
            tree = ET.parse(out)
            self.assertEqual(tree.getroot().tag, "osm")

    @patch("osm_downloader.requests.post")
    def test_main_exits_on_http_error(self, mock_post):
        mock_resp = MagicMock()
        mock_resp.raise_for_status.side_effect = requests.HTTPError("503")
        mock_post.return_value = mock_resp

        with self.assertRaises(SystemExit) as ctx:
            osm_downloader.main(["--lat", "51.5", "--lon", "-0.1"])
        self.assertEqual(ctx.exception.code, 1)


# ---------------------------------------------------------------------------
# Des Moines, IA coordinates — unit tests (no network I/O)
# ---------------------------------------------------------------------------

class TestDesMoinesCoordinates(unittest.TestCase):
    """
    Unit tests verifying that osm_downloader handles the downtown Des Moines,
    IA coordinates (41.587881, -93.620142) correctly without any network I/O.
    """

    LAT = 41.587881
    LON = -93.620142
    RADIUS = 5000

    def test_build_query_contains_des_moines_coordinates(self):
        q = osm_downloader.build_query(self.LAT, self.LON, self.RADIUS)
        self.assertIn(str(self.LAT), q)
        self.assertIn(str(self.LON), q)
        self.assertIn(str(self.RADIUS), q)

    def test_parse_args_accepts_des_moines_coordinates(self):
        args = osm_downloader.parse_args([
            "--lat", str(self.LAT),
            "--lon", str(self.LON),
            "--radius", str(self.RADIUS),
        ])
        self.assertAlmostEqual(args.lat, self.LAT)
        self.assertAlmostEqual(args.lon, self.LON)
        self.assertEqual(args.radius, self.RADIUS)


# ---------------------------------------------------------------------------
# Integration: downtown Des Moines, IA  (41.587881, -93.620142)
# These tests call the live Overpass API and require network access.
# ---------------------------------------------------------------------------

@pytest.mark.integration
class TestOsmDownloaderIntegration(unittest.TestCase):
    """
    Integration tests for osm_downloader.py that call the live Overpass API.

    These tests require network access and may take up to 30 seconds.
    They are excluded from the standard unit-test run (tests.yml) and are
    executed only by the map-preview CI workflow (map-preview.yml).
    """

    LAT = 41.587881
    LON = -93.620142
    RADIUS = 5000

    def test_download_osm_returns_valid_xml(self):
        """download_osm returns a non-empty, well-formed OSM XML document."""
        result = osm_downloader.download_osm(self.LAT, self.LON, self.RADIUS)
        self.assertIsInstance(result, str)
        self.assertIn("<osm", result)
        root = ET.fromstring(result)
        self.assertEqual(root.tag, "osm")

    def test_download_osm_contains_nodes_and_ways(self):
        """The live response for downtown Des Moines contains nodes and ways."""
        result = osm_downloader.download_osm(self.LAT, self.LON, self.RADIUS)
        root = ET.fromstring(result)
        self.assertGreater(len(root.findall("node")), 0,
            "Expected at least one <node> in the live Overpass response")
        self.assertGreater(len(root.findall("way")), 0,
            "Expected at least one <way> in the live Overpass response")

    def test_download_osm_contains_highway_ways(self):
        """The live response includes at least one road (highway tag)."""
        result = osm_downloader.download_osm(self.LAT, self.LON, self.RADIUS)
        root = ET.fromstring(result)
        highway_ways = [
            w for w in root.findall("way")
            if any(t.attrib.get("k") == "highway" for t in w.findall("tag"))
        ]
        self.assertGreater(len(highway_ways), 0,
            "Expected at least one way with highway tag in downtown Des Moines")

    def test_main_downloads_and_saves_des_moines_osm(self):
        """main() downloads real Des Moines data and writes a valid .osm file."""
        with tempfile.TemporaryDirectory() as tmpdir:
            out = os.path.join(tmpdir, "des_moines.osm")
            osm_downloader.main([
                "--lat", str(self.LAT),
                "--lon", str(self.LON),
                "--radius", str(self.RADIUS),
                "--output", out,
            ])
            self.assertTrue(os.path.exists(out))
            root = ET.parse(out).getroot()
            self.assertEqual(root.tag, "osm")
            self.assertGreater(len(root.findall("node")), 0)
            self.assertGreater(len(root.findall("way")), 0)


if __name__ == "__main__":
    unittest.main()
