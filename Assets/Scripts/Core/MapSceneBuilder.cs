using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using TerraDrive.DataInversion;
using TerraDrive.Procedural;

namespace TerraDrive.Core
{
    /// <summary>
    /// Loads a pre-downloaded OSM map at startup and instantiates all scene geometry
    /// (terrain, road meshes, building meshes) from the data returned by
    /// <see cref="MapLoader.LoadMapAsync"/>.
    ///
    /// <para>
    /// Attach this component to any GameObject in the scene, wire the
    /// <see cref="Registry"/> and optional <see cref="Vehicle"/> references in the
    /// Inspector, then press Play.  The component drives the
    /// <see cref="GameManager"/> state machine through
    /// <c>LoadingMap → GeneratingLevel → Racing</c> as each stage completes.
    /// </para>
    ///
    /// <para>
    /// File paths can be absolute or relative.  Relative paths are resolved from
    /// <c>Application.dataPath/..</c> (the project root in the Unity Editor;
    /// the executable folder in a standalone build).
    /// </para>
    ///
    /// Quick-start defaults (matching the bundled sample data):
    /// <list type="bullet">
    ///   <item><see cref="OsmFilePath"/>: <c>Assets/Data/map.osm.xml</c></item>
    ///   <item><see cref="ElevationCsvPath"/>: <c>Assets/Data/map.elevation.csv</c></item>
    ///   <item>
    ///     Origin: taken from <see cref="GameManager.OriginLatitude"/> /
    ///     <see cref="GameManager.OriginLongitude"/> when both inspector fields are zero.
    ///   </item>
    /// </list>
    /// </summary>
    public class MapSceneBuilder : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Map Data")]
        [Tooltip("Path to the .osm XML file.  Absolute, or relative to the project root.")]
        public string OsmFilePath = "Assets/Data/map.osm.xml";

        [Tooltip("Path to the companion .elevation.csv file.  Absolute, or relative to the project root.")]
        public string ElevationCsvPath = "Assets/Data/map.elevation.csv";

        [Header("Origin (leave both 0 to inherit from GameManager)")]
        [Tooltip("Latitude of the map origin (world 0,0,0).  0 = use GameManager.OriginLatitude.")]
        public double OriginLatitude;

        [Tooltip("Longitude of the map origin (world 0,0,0).  0 = use GameManager.OriginLongitude.")]
        public double OriginLongitude;

        [Header("Scene Wiring")]
        [Tooltip("MaterialRegistry used to apply textures to generated meshes.")]
        public MaterialRegistry Registry;

        [Tooltip("Optional vehicle Transform positioned at the map origin once loading completes.")]
        public Transform Vehicle;

        [Tooltip("Height above world origin (metres) at which the vehicle is placed.")]
        public float VehicleSpawnHeight = 2f;

        // ── Private state ──────────────────────────────────────────────────────

        private CancellationTokenSource _cts;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Start()
        {
            _cts = new CancellationTokenSource();
            StartCoroutine(LoadAndBuildRoutine(_cts.Token));
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ── Build pipeline ─────────────────────────────────────────────────────

        private IEnumerator LoadAndBuildRoutine(CancellationToken ct)
        {
            double originLat = OriginLatitude;
            double originLon = OriginLongitude;
            if (originLat == 0 && originLon == 0 && GameManager.Instance != null)
            {
                originLat = GameManager.Instance.OriginLatitude;
                originLon = GameManager.Instance.OriginLongitude;
            }

            string osmPath = ResolvePath(OsmFilePath);
            string csvPath = ResolvePath(ElevationCsvPath);

            GameManager.Instance?.SetState(GameState.LoadingMap);
            Debug.Log($"[MapSceneBuilder] Loading map: {osmPath}");

            var task = MapLoader.LoadMapAsync(osmPath, csvPath, originLat, originLon, ct);
            yield return new WaitUntil(() => task.IsCompleted);

            if (ct.IsCancellationRequested)
                yield break;

            if (task.IsFaulted)
            {
                Debug.LogError(
                    $"[MapSceneBuilder] Map load failed: {task.Exception?.GetBaseException().Message}");
                yield break;
            }

            MapData map = task.Result;
            Debug.Log(
                $"[MapSceneBuilder] Building level: {map.Roads.Count} roads, " +
                $"{map.Buildings.Count} buildings, {map.WaterBodies.Count} water bodies.");

            GameManager.Instance?.SetState(GameState.GeneratingLevel);

            BuildTerrain(map);
            yield return null;

            foreach (RoadSegment road in map.Roads)
            {
                BuildRoad(road, map.Region);
                yield return null;
            }

            foreach (BuildingFootprint building in map.Buildings)
            {
                BuildBuilding(building, map.Region);
                yield return null;
            }

            foreach (WaterBody water in map.WaterBodies)
            {
                BuildWater(water, map.Region);
                yield return null;
            }

            PositionVehicle();

            GameManager.Instance?.SetState(GameState.Racing);
            Debug.Log("[MapSceneBuilder] Level generation complete.");
        }

        // ── Geometry builders ──────────────────────────────────────────────────

        private void BuildTerrain(MapData map)
        {
            var go   = new GameObject("Terrain");
            var mesh = new Mesh { name = "TerrainMesh" };
            mesh.vertices  = map.TerrainMesh.Vertices;
            mesh.triangles = map.TerrainMesh.Triangles;
            mesh.uv        = map.TerrainMesh.UVs;
            mesh.RecalculateNormals();

            go.AddComponent<MeshFilter>().sharedMesh   = mesh;
            var renderer                               = go.AddComponent<MeshRenderer>();
            Registry?.ApplyTo(renderer, "terrain_grass");
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = LayerMask.NameToLayer("Terrain");
        }

        private void BuildRoad(RoadSegment road, RegionType region)
        {
            if (road.Nodes == null || road.Nodes.Count < 2)
                return;

            RoadType roadType = RoadTypeParser.Parse(road.HighwayType);
            var      spline   = SplineGenerator.BuildCatmullRom(road.Nodes);

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                spline, roadType, region: region,
                surfaceSeed: (int)(road.WayId & int.MaxValue));

            var parent = new GameObject($"Road_{road.WayId}");

            var surfaceGo = new GameObject("Surface");
            surfaceGo.transform.SetParent(parent.transform, false);
            surfaceGo.AddComponent<MeshFilter>().sharedMesh   = result.RoadMesh;
            var surfaceRenderer                               = surfaceGo.AddComponent<MeshRenderer>();
            Registry?.ApplyTo(surfaceRenderer, result.RoadTextureId);
            surfaceGo.AddComponent<MeshCollider>().sharedMesh = result.RoadMesh;
            surfaceGo.layer = LayerMask.NameToLayer("Road");

            if (result.KerbMesh != null && result.KerbMesh.vertexCount > 0)
            {
                var kerbGo = new GameObject("Kerb");
                kerbGo.transform.SetParent(parent.transform, false);
                kerbGo.AddComponent<MeshFilter>().sharedMesh = result.KerbMesh;
                var kerbRenderer = kerbGo.AddComponent<MeshRenderer>();
                Registry?.ApplyTo(kerbRenderer, result.KerbTextureId);
            }
        }

        private void BuildBuilding(BuildingFootprint building, RegionType region)
        {
            if (building.Footprint == null || building.Footprint.Count < 3)
                return;

            BuildingMeshResult result = BuildingGenerator.Extrude(
                building.Footprint, wayId: building.WayId, region: region);

            var parent = new GameObject($"Building_{building.WayId}");

            var wallGo = new GameObject("Walls");
            wallGo.transform.SetParent(parent.transform, false);
            wallGo.AddComponent<MeshFilter>().sharedMesh = result.WallMesh;
            var wallRenderer = wallGo.AddComponent<MeshRenderer>();
            Registry?.ApplyTo(wallRenderer, result.WallTextureId);

            var roofGo = new GameObject("Roof");
            roofGo.transform.SetParent(parent.transform, false);
            roofGo.AddComponent<MeshFilter>().sharedMesh = result.RoofMesh;
            var roofRenderer = roofGo.AddComponent<MeshRenderer>();
            Registry?.ApplyTo(roofRenderer, result.RoofTextureId);
        }

        private void BuildWater(WaterBody water, RegionType region)
        {
            if (water.Outline == null || water.Outline.Count < 3)
                return;

            WaterMeshResult result = WaterMeshGenerator.Generate(water, region: region);

            var go = new GameObject($"Water_{water.WayId}");
            go.AddComponent<MeshFilter>().sharedMesh   = result.Mesh;
            var renderer                               = go.AddComponent<MeshRenderer>();
            Registry?.ApplyTo(renderer, result.TextureId);
            go.layer = LayerMask.NameToLayer("Water");
        }

        private void PositionVehicle()
        {
            if (Vehicle == null)
                return;
            Vehicle.position = new Vector3(0f, VehicleSpawnHeight, 0f);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves a file path.  Absolute paths are returned unchanged.
        /// Relative paths are combined with <c>Application.dataPath/..</c>
        /// so they work from both the Unity Editor and standalone builds.
        /// </summary>
        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }
    }
}
