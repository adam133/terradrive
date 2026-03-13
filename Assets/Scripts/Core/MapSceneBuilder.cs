using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using TerraDrive.DataInversion;
using TerraDrive.Procedural;
using TerraDrive.Terrain;
using TerraDrive.Vehicle;

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
                BuildRoad(road, map.Region, map.TerrainMesh, map.ElevationGrid);
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

            PositionVehicle(map);

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

        private void BuildRoad(
            RoadSegment road,
            RegionType region,
            TerrainMeshResult terrainMesh,
            ElevationGrid elevationGrid)
        {
            if (road.Nodes == null || road.Nodes.Count < 2)
                return;

            RoadType roadType = RoadTypeParser.Parse(road.HighwayType);
            var spline = SplineGenerator.BuildCatmullRom(road.Nodes);
            var deformedSpline = RoadSurfaceDeformer.Deform(
                spline,
                roadType,
                (int)(road.WayId & int.MaxValue));
            var terrainAlignedSpline = ClampRoadSplineToTerrain(deformedSpline, terrainMesh, elevationGrid);

            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(
                terrainAlignedSpline, roadType, region: region,
                lanes: road.Lanes,
                isOneWay: road.IsOneWay);

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

        private static Vector3[] ClampRoadSplineToTerrain(
            System.Collections.Generic.IList<Vector3> spline,
            TerrainMeshResult terrainMesh,
            ElevationGrid elevationGrid)
        {
            var clamped = new Vector3[spline.Count];

            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 point = spline[i];
                float terrainHeight = SampleRenderedTerrainHeight(point, terrainMesh, elevationGrid);
                clamped[i] = new Vector3(point.x, Mathf.Max(point.y, terrainHeight), point.z);
            }

            return clamped;
        }

        private static float SampleRenderedTerrainHeight(
            Vector3 worldPoint,
            TerrainMeshResult terrainMesh,
            ElevationGrid elevationGrid)
        {
            Vector3[] vertices = terrainMesh.Vertices;
            int rows = elevationGrid.Rows;
            int cols = elevationGrid.Cols;

            float x = Mathf.Clamp(worldPoint.x, vertices[0].x, vertices[cols - 1].x);
            float z = Mathf.Clamp(worldPoint.z, vertices[0].z, vertices[(rows - 1) * cols].z);

            int col = FindColumnIndex(vertices, cols, x);
            int row = FindRowIndex(vertices, rows, cols, z);

            int blIndex = row * cols + col;
            int brIndex = blIndex + 1;
            int tlIndex = blIndex + cols;
            int trIndex = tlIndex + 1;

            Vector3 bl = vertices[blIndex];
            Vector3 br = vertices[brIndex];
            Vector3 tl = vertices[tlIndex];
            Vector3 tr = vertices[trIndex];

            float cellWidth = br.x - bl.x;
            float cellDepth = tl.z - bl.z;
            if (Mathf.Approximately(cellWidth, 0f) || Mathf.Approximately(cellDepth, 0f))
                return bl.y;

            float cellX = Mathf.Clamp01((x - bl.x) / cellWidth);
            float cellZ = Mathf.Clamp01((z - bl.z) / cellDepth);
            Vector2 point = new Vector2(x, z);

            return cellZ >= cellX
                ? InterpolateTriangleHeight(point, bl, tl, tr)
                : InterpolateTriangleHeight(point, bl, tr, br);
        }

        private static int FindColumnIndex(Vector3[] vertices, int cols, float x)
        {
            int low = 0;
            int high = cols - 2;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                float left = vertices[mid].x;
                float right = vertices[mid + 1].x;

                if (x < left)
                {
                    high = mid - 1;
                }
                else if (x > right)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return Mathf.Clamp(low, 0, cols - 2);
        }

        private static int FindRowIndex(Vector3[] vertices, int rows, int cols, float z)
        {
            int low = 0;
            int high = rows - 2;

            while (low <= high)
            {
                int mid = (low + high) >> 1;
                float bottom = vertices[mid * cols].z;
                float top = vertices[(mid + 1) * cols].z;

                if (z < bottom)
                {
                    high = mid - 1;
                }
                else if (z > top)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return Mathf.Clamp(low, 0, rows - 2);
        }

        private static float InterpolateTriangleHeight(Vector2 point, Vector3 a, Vector3 b, Vector3 c)
        {
            float denominator = ((b.z - c.z) * (a.x - c.x)) + ((c.x - b.x) * (a.z - c.z));
            if (Mathf.Approximately(denominator, 0f))
                return Mathf.Max(a.y, b.y, c.y);

            float wa = (((b.z - c.z) * (point.x - c.x)) + ((c.x - b.x) * (point.y - c.z))) / denominator;
            float wb = (((c.z - a.z) * (point.x - c.x)) + ((a.x - c.x) * (point.y - c.z))) / denominator;
            float wc = 1f - wa - wb;

            return (wa * a.y) + (wb * b.y) + (wc * c.y);
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

        // Road types considered drivable for vehicle spawning, in priority order.
        private static readonly string[] _drivableRoadTypes =
        {
            "motorway", "trunk", "primary", "secondary", "tertiary",
            "unclassified", "residential", "living_street", "road"
        };

        private void PositionVehicle(MapData map)
        {
            // If no vehicle was assigned in the Inspector, create a simple box car.
            if (Vehicle == null)
                Vehicle = CreateBoxCar().transform;

            EnsureDriveableVehicle(Vehicle.gameObject);

            Vector3 spawnPoint = FindRoadSpawnPoint(map);
            Vehicle.position = spawnPoint;
            Vehicle.rotation = FindRoadSpawnRotation(map, spawnPoint);

            // Wire the main camera up as a chase camera targeting this vehicle.
            if (Camera.main != null)
            {
                ChaseCam cam = Camera.main.GetComponent<ChaseCam>();
                if (cam == null)
                    cam = Camera.main.gameObject.AddComponent<ChaseCam>();
                cam.target = Vehicle;
            }
        }

        /// <summary>
        /// Picks the midpoint of the drivable road segment whose midpoint is nearest
        /// to the world origin.  Falls back to any road if no drivable road is found,
        /// and finally to <c>(0, VehicleSpawnHeight, 0)</c> if there are no roads.
        /// </summary>
        private Vector3 FindRoadSpawnPoint(MapData map)
        {
            RoadSegment best = FindBestRoadSegment(map);
            if (best == null)
                return new Vector3(0f, VehicleSpawnHeight, 0f);

            // Use the midpoint of the segment so the car starts on a straight section.
            Vector3 mid = best.Nodes[best.Nodes.Count / 2];
            return new Vector3(mid.x, mid.y + VehicleSpawnHeight, mid.z);
        }

        /// <summary>
        /// Returns a rotation aligned with the road direction at the spawn point.
        /// Falls back to identity if the road has fewer than two nodes.
        /// </summary>
        private Quaternion FindRoadSpawnRotation(MapData map, Vector3 spawnPoint)
        {
            RoadSegment best = FindBestRoadSegment(map);
            if (best == null || best.Nodes.Count < 2)
                return Quaternion.identity;

            int mid = best.Nodes.Count / 2;
            Vector3 a = best.Nodes[Mathf.Max(mid - 1, 0)];
            Vector3 b = best.Nodes[Mathf.Min(mid + 1, best.Nodes.Count - 1)];
            Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
            if (dir.sqrMagnitude < 0.0001f)
                return Quaternion.identity;
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private RoadSegment FindBestRoadSegment(MapData map)
        {
            if (map.Roads == null || map.Roads.Count == 0)
                return null;

            // Try each priority level in order; within each level pick the segment
            // whose midpoint is closest to the world origin.
            foreach (string roadType in _drivableRoadTypes)
            {
                RoadSegment best = null;
                float bestDist = float.MaxValue;

                foreach (RoadSegment seg in map.Roads)
                {
                    if (seg.Nodes == null || seg.Nodes.Count < 2) continue;
                    if (!string.Equals(seg.HighwayType, roadType,
                            System.StringComparison.OrdinalIgnoreCase)) continue;

                    Vector3 mid = seg.Nodes[seg.Nodes.Count / 2];
                    float dist = mid.x * mid.x + mid.z * mid.z; // sqr distance in XZ
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = seg;
                    }
                }

                if (best != null)
                    return best;
            }

            // Fallback: any road, closest midpoint to origin.
            RoadSegment fallback = null;
            float fallbackDist = float.MaxValue;
            foreach (RoadSegment seg in map.Roads)
            {
                if (seg.Nodes == null || seg.Nodes.Count < 2) continue;
                Vector3 mid = seg.Nodes[seg.Nodes.Count / 2];
                float dist = mid.x * mid.x + mid.z * mid.z;
                if (dist < fallbackDist)
                {
                    fallbackDist = dist;
                    fallback = seg;
                }
            }
            return fallback;
        }

        /// <summary>
        /// Builds a minimal "box car" from primitives so the player has something
        /// visible before a proper vehicle prefab is wired up.
        /// </summary>
        private static GameObject CreateBoxCar()
        {
            // Root with physics
            var root = new GameObject("BoxCar");
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 5f;
            rb.centerOfMass = new Vector3(0f, -0.3f, 0f);

            // Body (main visible cube)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(1.8f, 0.6f, 4f);
            body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            // Body collider handled by the MeshCollider on the root – remove the
            // auto-added BoxCollider to avoid duplicate physics shapes.
            Object.Destroy(body.GetComponent<Collider>());

            // Cabin on top
            var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin";
            cabin.transform.SetParent(root.transform, false);
            cabin.transform.localScale = new Vector3(1.6f, 0.5f, 2f);
            cabin.transform.localPosition = new Vector3(0f, 0.85f, 0.1f);
            Object.Destroy(cabin.GetComponent<Collider>());

            // Body/obstacle collider – deliberately kept ABOVE the wheel contact
            // patch so it never holds the vehicle off the ground before the
            // WheelColliders can make contact.
            // Wheel-contact math (from EnsureDriveableVehicle):
            //   center  y = 0.35,  suspensionDist = 0.2,  targetPos = 0.5
            //   rest offset = 0.5 × 0.2 = 0.10  →  wheel-centre at  0.25
            //   contact patch at   0.25 − 0.35 (radius) = −0.10 below root
            // BoxCollider bottom must be > −0.10 so the wheels hit first.
            var col = root.AddComponent<BoxCollider>();
            col.size   = new Vector3(1.8f, 0.8f, 4f);   // 0.8 tall (was 1.1)
            col.center = new Vector3(0f, 0.55f, 0f);     // bottom = 0.55−0.40 = +0.15 above root

            EnsureDriveableVehicle(root);

            return root;
        }

        private static void EnsureDriveableVehicle(GameObject vehicleRoot)
        {
            if (vehicleRoot == null)
                return;

            if (vehicleRoot.GetComponent<Rigidbody>() == null)
            {
                var rb = vehicleRoot.AddComponent<Rigidbody>();
                rb.mass = 1200f;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 5f;
            }

            var controller = vehicleRoot.GetComponent<CarController>();
            if (controller == null)
                controller = vehicleRoot.AddComponent<CarController>();

            if (HasCompleteWheelSetup(controller))
                return;

            Transform wheelRoot = vehicleRoot.transform.Find("GeneratedWheels");
            if (wheelRoot == null)
            {
                wheelRoot = new GameObject("GeneratedWheels").transform;
                wheelRoot.SetParent(vehicleRoot.transform, false);
            }

            SetupWheel(controller, wheelRoot, "FrontLeft", new Vector3(-0.9f, 0.35f, 1.35f), true);
            SetupWheel(controller, wheelRoot, "FrontRight", new Vector3(0.9f, 0.35f, 1.35f), true);
            SetupWheel(controller, wheelRoot, "RearLeft", new Vector3(-0.9f, 0.35f, -1.35f), false);
            SetupWheel(controller, wheelRoot, "RearRight", new Vector3(0.9f, 0.35f, -1.35f), false);

            controller.motorTorque = 2200f;
            controller.brakeTorque = 3500f;
            controller.maxSteerAngle = 28f;
            controller.normalFriction = 1.3f;
            controller.driftFriction = 0.55f;
            controller.antiRollStiffness = 6500f;
        }

        private static bool HasCompleteWheelSetup(CarController controller)
        {
            return controller != null
                && controller.frontLeftCollider != null
                && controller.frontRightCollider != null
                && controller.rearLeftCollider != null
                && controller.rearRightCollider != null
                && controller.frontLeftMesh != null
                && controller.frontRightMesh != null
                && controller.rearLeftMesh != null
                && controller.rearRightMesh != null;
        }

        private static void SetupWheel(
            CarController controller,
            Transform wheelRoot,
            string wheelName,
            Vector3 localPosition,
            bool steer)
        {
            Transform mount = wheelRoot.Find(wheelName);
            if (mount == null)
            {
                mount = new GameObject(wheelName).transform;
                mount.SetParent(wheelRoot, false);
            }

            mount.localPosition = localPosition;
            mount.localRotation = Quaternion.identity;

            var collider = mount.GetComponent<WheelCollider>();
            if (collider == null)
                collider = mount.gameObject.AddComponent<WheelCollider>();

            ConfigureWheelCollider(collider, steer);

            Transform mesh = mount.Find("Mesh");
            if (mesh == null)
            {
                var wheelVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheelVisual.name = "Mesh";
                mesh = wheelVisual.transform;
                mesh.SetParent(mount, false);
                Object.Destroy(wheelVisual.GetComponent<Collider>());
            }

            mesh.localPosition = Vector3.zero;
            mesh.localRotation = Quaternion.Euler(0f, 0f, 90f);
            mesh.localScale = new Vector3(0.35f, 0.14f, 0.35f);

            AssignWheel(controller, wheelName, collider, mesh);
        }

        private static void ConfigureWheelCollider(WheelCollider collider, bool steer)
        {
            collider.radius = 0.35f;
            collider.mass = 22f;
            collider.suspensionDistance = 0.2f;
            collider.forceAppPointDistance = 0.1f;

            JointSpring suspension = collider.suspensionSpring;
            suspension.spring = 30000f;
            suspension.damper = 4500f;
            suspension.targetPosition = 0.5f;
            collider.suspensionSpring = suspension;

            WheelFrictionCurve forward = collider.forwardFriction;
            forward.extremumSlip = 0.4f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = 0.8f;
            forward.asymptoteValue = 0.75f;
            forward.stiffness = 1.2f;
            collider.forwardFriction = forward;

            WheelFrictionCurve sideways = collider.sidewaysFriction;
            sideways.extremumSlip = 0.25f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = 0.5f;
            sideways.asymptoteValue = 0.75f;
            sideways.stiffness = steer ? 1.2f : 1.3f;
            collider.sidewaysFriction = sideways;
        }

        private static void AssignWheel(
            CarController controller,
            string wheelName,
            WheelCollider collider,
            Transform mesh)
        {
            switch (wheelName)
            {
                case "FrontLeft":
                    controller.frontLeftCollider = collider;
                    controller.frontLeftMesh = mesh;
                    break;
                case "FrontRight":
                    controller.frontRightCollider = collider;
                    controller.frontRightMesh = mesh;
                    break;
                case "RearLeft":
                    controller.rearLeftCollider = collider;
                    controller.rearLeftMesh = mesh;
                    break;
                case "RearRight":
                    controller.rearRightCollider = collider;
                    controller.rearRightMesh = mesh;
                    break;
            }
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
