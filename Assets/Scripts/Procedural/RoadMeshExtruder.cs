using System.Collections.Generic;
using UnityEngine;
using VectorRoad.DataInversion;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Extrudes a UV-mapped road mesh along a sequence of spline positions.
    ///
    /// Usage (simple):
    /// <code>
    ///   Mesh roadMesh = RoadMeshExtruder.Extrude(splinePoints, RoadType.Residential);
    ///   GetComponent&lt;MeshFilter&gt;().sharedMesh = roadMesh;
    /// </code>
    ///
    /// Usage (with kerbs and lane markings):
    /// <code>
    ///   RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(splinePoints, RoadType.Primary);
    ///   roadFilter.sharedMesh = result.RoadMesh;   // UV0 = asphalt, UV1 = lane markings
    ///   kerbFilter.sharedMesh = result.KerbMesh;   // separate kerb geometry
    /// </code>
    /// </summary>
    public static class RoadMeshExtruder
    {
        /// <summary>Width of each kerb strip in metres.</summary>
        public const float DefaultKerbWidth = 0.15f;

        /// <summary>Height of kerb surfaces above the road plane in metres for rural roads.</summary>
        public const float DefaultKerbHeight = 0.05f;

        /// <summary>
        /// Height of the kerb (curb) above the road surface for urban roads, in metres.
        /// Standard 15 cm raised kerb used on residential streets and service roads.
        /// </summary>
        public const float UrbanKerbHeight = 0.15f;

        /// <summary>
        /// Depth of the roadside ditch below the adjacent road surface, in metres.
        /// Applied to rural roads (motorway through tertiary, dirt, path, cycleway).
        /// </summary>
        public const float RuralDitchDepth = 1.0f;

        /// <summary>
        /// Total horizontal width of the roadside ditch profile on each side, in metres.
        /// The V-shaped channel spans from the road edge outward by this distance and
        /// returns to terrain level at the outer edge.
        /// </summary>
        public const float RuralDitchWidth = 3.0f;

        /// <summary>
        /// Vertical offset (in metres) applied to lane-marking overlay vertices to keep
        /// them above the road surface and prevent z-fighting.
        /// </summary>
        public const float LaneMarkingClearance = 0.005f;

        /// <summary>
        /// Constant Y offset (in metres) applied to all road vertices so the road
        /// surface always sits physically above the grass/terrain layer and never
        /// causes z-fighting regardless of surface-deformer roughness.
        /// </summary>
        public const float TerrainClearance = 0.02f;

        /// <summary>
        /// V-axis tile length (metres) used for the lane-marking UV channel (UV1).
        /// Matches a typical UK/US standard dashed-line repeat (3 m dash + 3 m gap = 6 m cycle).
        /// </summary>
        public const float DefaultLaneMarkingTileLength = 6f;

        /// <summary>
        /// Width per lane in metres used when an explicit lane count is provided.
        /// Reflects a standard 3.5 m traffic lane.
        /// </summary>
        public const float DefaultLaneWidth = 3.5f;

        /// <summary>
        /// Canonical road half-widths (in metres) keyed by <see cref="RoadType"/>.
        /// Values approximate real-world carriageway widths for each functional class.
        /// </summary>
        private static readonly Dictionary<RoadType, float> RoadWidths =
            new Dictionary<RoadType, float>
            {
                { RoadType.Motorway,    20f  },   // 3-4 lanes + hard shoulders
                { RoadType.Trunk,       15f  },   // major dual-carriageway
                { RoadType.Primary,     12f  },   // 2 lanes + wide shoulders
                { RoadType.Secondary,    9f  },   // 2 lanes
                { RoadType.Tertiary,     7f  },   // 2 narrow lanes
                { RoadType.Residential,  5.5f },  // narrow residential street
                { RoadType.Service,      4f  },   // access lane
                { RoadType.Dirt,         4f  },   // forest / farm track
                { RoadType.Path,         2f  },   // footpath
                { RoadType.Cycleway,     2f  },   // cycle lane
                { RoadType.Unknown,      7f  },   // sensible fallback
            };

        /// <summary>
        /// Returns the canonical road width in metres for <paramref name="roadType"/>.
        /// </summary>
        /// <param name="roadType">The functional road classification.</param>
        /// <returns>Width in metres.</returns>
        public static float GetWidthForRoadType(RoadType roadType) =>
            RoadWidths.TryGetValue(roadType, out float w) ? w : 7f;

        /// <summary>
        /// Returns the road width in metres, derived from an explicit lane count when
        /// available, or from the road-type lookup as a fallback.
        /// No shoulder width or region factor is applied.
        /// </summary>
        /// <param name="roadType">The functional road classification (used when <paramref name="lanes"/> is 0).</param>
        /// <param name="lanes">
        /// Number of lanes from the OSM <c>lanes</c> tag.  When greater than zero the
        /// width is calculated as <c>lanes × <see cref="DefaultLaneWidth"/></c>.
        /// Pass 0 (or omit) to fall back to the road-type lookup.
        /// </param>
        /// <returns>Width in metres.</returns>
        public static float GetWidthForRoadType(RoadType roadType, int lanes) =>
            lanes > 0 ? lanes * DefaultLaneWidth : GetWidthForRoadType(roadType);

        /// <summary>
        /// Returns the road width in metres using the full regional formula:
        /// <code>
        ///   lanes &gt; 0 : (lanes × <see cref="DefaultLaneWidth"/> + shoulderWidth) × regionFactor
        ///   lanes == 0 : baseTableWidth × regionFactor
        /// </code>
        /// Shoulder widths are taken from <see cref="RegionWidthFactors.GetShoulderWidth"/>;
        /// the region multiplier is taken from <see cref="RegionWidthFactors.GetWidthFactor"/>.
        /// </summary>
        /// <param name="roadType">The functional road classification.</param>
        /// <param name="lanes">
        /// Number of lanes from the OSM <c>lanes</c> tag.  When greater than zero the
        /// carriageway width (<c>lanes × <see cref="DefaultLaneWidth"/></c>) plus the
        /// road-type shoulder width is used; otherwise the canonical table value is used.
        /// </param>
        /// <param name="region">
        /// Geographic/climate region used to scale the computed width.
        /// Defaults to <see cref="RegionType.Unknown"/> (factor 1.0 — no adjustment).
        /// </param>
        /// <returns>Width in metres.</returns>
        public static float GetWidthForRoadType(RoadType roadType, int lanes, RegionType region)
        {
            float regionFactor = RegionWidthFactors.GetWidthFactor(region);
            if (lanes > 0)
            {
                float shoulder = RegionWidthFactors.GetShoulderWidth(roadType);
                return (lanes * DefaultLaneWidth + shoulder) * regionFactor;
            }
            return GetWidthForRoadType(roadType) * regionFactor;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="roadType"/> is considered urban —
        /// i.e. it is likely to be bordered by a raised kerb rather than an open ditch.
        /// Residential streets and service roads are treated as urban; all other
        /// functional classes (motorway through tertiary, dirt, path, cycleway) are
        /// treated as rural and will receive roadside ditches.
        /// </summary>
        /// <param name="roadType">Road classification to test.</param>
        /// <returns><c>true</c> for urban road types; <c>false</c> for rural.</returns>
        public static bool IsUrbanRoadType(RoadType roadType) =>
            roadType == RoadType.Residential || roadType == RoadType.Service;

        /// <summary>
        /// Generates a flat road mesh extruded along <paramref name="splinePoints"/>, using
        /// the canonical width for <paramref name="roadType"/>.
        /// </summary>
        /// <param name="splinePoints">
        /// Ordered world-space centre-line positions along the road.
        /// Requires at least two points.
        /// </param>
        /// <param name="roadType">Road classification used to select the appropriate width.</param>
        /// <param name="uvTileLength">
        /// World-space length (in metres) after which the road texture repeats along the U axis.
        /// Default is 10 m — matching a standard asphalt texture tile.
        /// </param>
        /// <returns>A <see cref="Mesh"/> ready to assign to a <c>MeshFilter</c>.</returns>
        public static Mesh Extrude(IList<Vector3> splinePoints, RoadType roadType, float uvTileLength = 10f) =>
            Extrude(splinePoints, GetWidthForRoadType(roadType), uvTileLength);

        /// <summary>
        /// Generates a flat road mesh extruded along <paramref name="splinePoints"/>.
        /// </summary>
        /// <param name="splinePoints">
        /// Ordered world-space centre-line positions along the road.
        /// Requires at least two points.
        /// </param>
        /// <param name="roadWidth">Total road width in metres.</param>
        /// <param name="uvTileLength">
        /// World-space length (in metres) after which the road texture repeats along the U axis.
        /// Default is 10 m — matching a standard asphalt texture tile.
        /// </param>
        /// <returns>A <see cref="Mesh"/> ready to assign to a <c>MeshFilter</c>.</returns>
        public static Mesh Extrude(IList<Vector3> splinePoints, float roadWidth = 7f, float uvTileLength = 10f)
        {
            if (splinePoints == null || splinePoints.Count < 2)
            {
                Debug.LogWarning("[RoadMeshExtruder] Need at least 2 spline points.");
                return new Mesh();
            }

            int n = splinePoints.Count;
            var vertices = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var triangles = new int[(n - 1) * 6];

            float halfWidth = roadWidth * 0.5f;
            float distanceAlongRoad = 0f;

            for (int i = 0; i < n; i++)
            {
                // Tangent direction along the spline.
                Vector3 tangent;
                if (i == 0)
                    tangent = (splinePoints[1] - splinePoints[0]).normalized;
                else if (i == n - 1)
                    tangent = (splinePoints[n - 1] - splinePoints[n - 2]).normalized;
                else
                    tangent = (splinePoints[i + 1] - splinePoints[i - 1]).normalized;

                // Road-perpendicular direction on the XZ plane.
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

                Vector3 centre = splinePoints[i] + Vector3.up * TerrainClearance;
                vertices[i * 2]     = centre - right * halfWidth;  // left edge
                vertices[i * 2 + 1] = centre + right * halfWidth;  // right edge

                // Accumulate road distance for V-coordinate tiling.
                if (i > 0)
                    distanceAlongRoad += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);

                float v = distanceAlongRoad / uvTileLength;
                uvs[i * 2]     = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            // Build quads from consecutive edge pairs.
            for (int i = 0; i < n - 1; i++)
            {
                int tri = i * 6;
                int v0 = i * 2;

                triangles[tri]     = v0;
                triangles[tri + 1] = v0 + 2;
                triangles[tri + 2] = v0 + 1;

                triangles[tri + 3] = v0 + 1;
                triangles[tri + 4] = v0 + 2;
                triangles[tri + 5] = v0 + 3;
            }

            var mesh = new Mesh { name = "RoadMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── ExtrudeWithDetails overloads ──────────────────────────────────────

        /// <summary>
        /// Generates a road surface mesh (with UV0 for asphalt tiling and UV1 for
        /// lane-marking tiling) plus a separate kerb mesh, using the canonical width
        /// for <paramref name="roadType"/>.  The <see cref="RoadMeshResult"/> returned
        /// includes region-appropriate texture identifiers for the road surface,
        /// the kerb, and the lane markings.
        ///
        /// <para>
        /// Urban road types (<see cref="RoadType.Residential"/> and
        /// <see cref="RoadType.Service"/>) receive a <see cref="UrbanKerbHeight"/> (15 cm)
        /// raised kerb.  All other road types receive roadside ditches that are
        /// <see cref="RuralDitchDepth"/> (1 m) deep and <see cref="RuralDitchWidth"/>
        /// (3 m) wide, rather than a kerb.
        /// </para>
        ///
        /// <para>
        /// Paved road types also receive a <see cref="RoadMeshResult.LaneMarkingMesh"/>
        /// overlay (positioned slightly above the road surface) that can be rendered
        /// with a lane-marking material to make centre lines and edge markings visible.
        /// </para>
        /// </summary>
        /// <param name="splinePoints">
        /// Ordered world-space centre-line positions.  Requires at least two points.
        /// </param>
        /// <param name="roadType">Road classification used to select the width and surface texture.</param>
        /// <param name="uvTileLength">
        /// Asphalt texture V-tile length in metres (UV channel 0).  Default 10 m.
        /// </param>
        /// <param name="laneMarkingTileLength">
        /// Lane-marking texture V-tile length in metres (UV channel 1).  Default 6 m
        /// — matching a standard dashed-line repeat (3 m dash + 3 m gap).
        /// </param>
        /// <param name="kerbWidth">Width of each kerb strip in metres (urban roads only).</param>
        /// <param name="region">
        /// Climate zone used to select region-appropriate texture identifiers.
        /// Defaults to <see cref="RegionType.Unknown"/>.
        /// </param>
        /// <param name="surfaceSeed">
        /// When non-null, <see cref="RoadSurfaceDeformer"/> is applied to the spline
        /// before mesh generation, introducing road-class-appropriate Y-axis
        /// imperfections (dips, bumps, potholes).  Pass the OSM way ID (cast to
        /// <c>int</c>) for stable, per-road deformation.  <c>null</c> (default)
        /// produces a perfectly flat surface, which is useful for unit tests.
        /// </param>
        /// <param name="lanes">
        /// Number of lanes from the OSM <c>lanes</c> tag.  When greater than zero the
        /// road width is computed as <c>(lanes × <see cref="DefaultLaneWidth"/> + shoulderWidth) × regionFactor</c>;
        /// otherwise the road-type width table value scaled by the region factor is used.
        /// Defaults to 0 (use table).
        /// </param>
        /// <param name="isOneWay">
        /// <c>true</c> when the OSM <c>oneway</c> tag indicates single-direction traffic.
        /// Affects which lane-marking texture is selected.  Defaults to <c>false</c>.
        /// </param>
        /// <returns>
        /// A <see cref="RoadMeshResult"/> containing the road mesh, the kerb mesh,
        /// the lane-marking overlay mesh, the optional ditch mesh, and
        /// region-appropriate texture identifiers.
        /// </returns>
        public static RoadMeshResult ExtrudeWithDetails(
            IList<Vector3> splinePoints,
            RoadType roadType,
            float uvTileLength           = 10f,
            float laneMarkingTileLength  = DefaultLaneMarkingTileLength,
            float kerbWidth              = DefaultKerbWidth,
            RegionType region            = RegionType.Unknown,
            int? surfaceSeed             = null,
            int lanes                    = 0,
            bool isOneWay                = false)
        {
            bool urban = IsUrbanRoadType(roadType);
            return ExtrudeWithDetails(
                splinePoints,
                GetWidthForRoadType(roadType, lanes, region),
                uvTileLength,
                laneMarkingTileLength,
                kerbWidth,
                kerbHeight: urban ? UrbanKerbHeight : 0f,
                region,
                roadType,
                surfaceSeed,
                isOneWay,
                generateDitch: !urban);
        }

        /// <summary>
        /// Generates a road surface mesh (with UV0 for asphalt tiling and UV1 for
        /// lane-marking tiling) plus a separate kerb mesh.  The <see cref="RoadMeshResult"/>
        /// returned includes region-appropriate texture identifiers for the road surface,
        /// the kerb, and the lane markings, as well as an optional lane-marking overlay
        /// mesh and an optional roadside ditch mesh.
        /// </summary>
        /// <param name="splinePoints">
        /// Ordered world-space centre-line positions.  Requires at least two points.
        /// </param>
        /// <param name="roadWidth">Total road width in metres.</param>
        /// <param name="uvTileLength">
        /// Asphalt texture V-tile length in metres (UV channel 0).  Default 10 m.
        /// </param>
        /// <param name="laneMarkingTileLength">
        /// Lane-marking texture V-tile length in metres (UV channel 1).  Default 6 m.
        /// </param>
        /// <param name="kerbWidth">Width of each kerb strip in metres.</param>
        /// <param name="kerbHeight">
        /// Height of kerb surfaces above the road plane.  Pass 0 to suppress kerb
        /// generation (no kerb mesh vertices are emitted).
        /// </param>
        /// <param name="region">
        /// Climate zone used to select region-appropriate texture identifiers.
        /// Defaults to <see cref="RegionType.Unknown"/>.
        /// </param>
        /// <param name="roadType">
        /// Road classification used for surface texture selection and lane-marking
        /// overlay generation (paved types receive a lane-marking mesh; unpaved do not).
        /// Defaults to <see cref="RoadType.Unknown"/>.
        /// </param>
        /// <param name="surfaceSeed">
        /// When non-null, <see cref="RoadSurfaceDeformer"/> is applied to the spline
        /// before mesh generation, introducing road-class-appropriate Y-axis
        /// imperfections (dips, bumps, potholes).  Pass the OSM way ID (cast to
        /// <c>int</c>) for stable, per-road deformation.  <c>null</c> (default)
        /// produces a perfectly flat surface.
        /// </param>
        /// <param name="isOneWay">
        /// <c>true</c> when the OSM <c>oneway</c> tag indicates single-direction traffic.
        /// Affects which lane-marking texture is selected.  Defaults to <c>false</c>.
        /// </param>
        /// <param name="generateDitch">
        /// When <c>true</c>, a V-profile roadside ditch mesh is generated on both sides
        /// of the carriageway (see <see cref="RuralDitchDepth"/> and
        /// <see cref="RuralDitchWidth"/>).  Defaults to <c>false</c>.
        /// </param>
        /// <returns>
        /// A <see cref="RoadMeshResult"/> containing the road mesh, the kerb mesh,
        /// the lane-marking overlay mesh (for paved road types), the optional ditch mesh,
        /// and region-appropriate texture identifiers.
        /// </returns>
        public static RoadMeshResult ExtrudeWithDetails(
            IList<Vector3> splinePoints,
            float roadWidth              = 7f,
            float uvTileLength           = 10f,
            float laneMarkingTileLength  = DefaultLaneMarkingTileLength,
            float kerbWidth              = DefaultKerbWidth,
            float kerbHeight             = DefaultKerbHeight,
            RegionType region            = RegionType.Unknown,
            RoadType roadType            = RoadType.Unknown,
            int? surfaceSeed             = null,
            bool isOneWay                = false,
            bool generateDitch           = false)
        {
            if (splinePoints == null || splinePoints.Count < 2)
            {
                Debug.LogWarning("[RoadMeshExtruder] Need at least 2 spline points.");
                return new RoadMeshResult(new Mesh(), new Mesh(), string.Empty, string.Empty);
            }

            // Apply surface imperfections when a seed is provided.
            IList<Vector3> pts = surfaceSeed.HasValue
                ? RoadSurfaceDeformer.Deform(splinePoints, roadType, surfaceSeed.Value)
                : splinePoints;

            // Lift the road above the grass/terrain layer to prevent z-fighting.
            var liftedPts = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                liftedPts[i] = new Vector3(pts[i].x, pts[i].y + TerrainClearance, pts[i].z);
            pts = liftedPts;

            int n = pts.Count;
            float halfWidth = roadWidth * 0.5f;

            // ── Road surface mesh ────────────────────────────────────────────
            var vertices      = new Vector3[n * 2];
            var uv0s          = new Vector2[n * 2];   // asphalt tiling
            var uv1s          = new Vector2[n * 2];   // lane-marking tiling
            var triangles     = new int[(n - 1) * 6];
            float distAlongRoad = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = ComputeTangent(pts, i, n);
                Vector3 right   = Vector3.Cross(Vector3.up, tangent).normalized;

                vertices[i * 2]     = pts[i] - right * halfWidth;  // left edge
                vertices[i * 2 + 1] = pts[i] + right * halfWidth;  // right edge

                if (i > 0)
                    distAlongRoad += Vector3.Distance(pts[i], pts[i - 1]);

                float v0 = distAlongRoad / uvTileLength;
                float v1 = distAlongRoad / laneMarkingTileLength;

                uv0s[i * 2]     = new Vector2(0f, v0);
                uv0s[i * 2 + 1] = new Vector2(1f, v0);
                uv1s[i * 2]     = new Vector2(0f, v1);
                uv1s[i * 2 + 1] = new Vector2(1f, v1);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int tri = i * 6;
                int v0  = i * 2;

                triangles[tri]     = v0;
                triangles[tri + 1] = v0 + 2;
                triangles[tri + 2] = v0 + 1;

                triangles[tri + 3] = v0 + 1;
                triangles[tri + 4] = v0 + 2;
                triangles[tri + 5] = v0 + 3;
            }

            var roadMesh = new Mesh { name = "RoadMesh" };
            roadMesh.SetVertices(vertices);
            roadMesh.SetUVs(0, uv0s);
            roadMesh.SetUVs(1, uv1s);
            roadMesh.SetTriangles(triangles, 0);
            roadMesh.RecalculateNormals();
            roadMesh.RecalculateBounds();

            // ── Lane-marking overlay mesh ────────────────────────────────────
            // Generated for all paved road types so lane lines are visible in-game.
            bool hasPavedMarkings = roadType != RoadType.Dirt
                                 && roadType != RoadType.Path
                                 && roadType != RoadType.Cycleway;
            Mesh? laneMarkingMesh = hasPavedMarkings
                ? BuildLaneMarkingMesh(pts, halfWidth, laneMarkingTileLength)
                : null;

            // ── Kerb mesh ────────────────────────────────────────────────────
            // Skipped when kerbHeight is zero (rural roads use ditches instead).
            Mesh kerbMesh = kerbHeight > 0f
                ? BuildKerbMesh(pts, halfWidth, kerbWidth, kerbHeight, uvTileLength)
                : new Mesh { name = "KerbMesh" };

            // ── Ditch mesh ───────────────────────────────────────────────────
            Mesh? ditchMesh = generateDitch
                ? BuildDitchMesh(pts, halfWidth)
                : null;

            // ── Texture identifiers ──────────────────────────────────────────
            string roadTextureId        = RegionTextures.GetRoadSurfaceTextureId(region, roadType);
            string kerbTextureId        = RegionTextures.GetKerbTextureId(region);
            string laneMarkingTextureId = RegionTextures.GetLaneMarkingTextureId(isOneWay);
            string ditchTextureId       = ditchMesh != null
                ? RegionTextures.GetDitchTextureId(region)
                : string.Empty;

            return new RoadMeshResult(roadMesh, kerbMesh, roadTextureId, kerbTextureId,
                                      laneMarkingTextureId, laneMarkingMesh, ditchMesh, ditchTextureId);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Computes the forward tangent at spline index <paramref name="i"/> using
        /// forward/backward differencing at the ends and central differencing in the middle.
        /// </summary>
        private static Vector3 ComputeTangent(IList<Vector3> pts, int i, int n)
        {
            if (i == 0)     return (pts[1]     - pts[0]).normalized;
            if (i == n - 1) return (pts[n - 1] - pts[n - 2]).normalized;
            return (pts[i + 1] - pts[i - 1]).normalized;
        }

        /// <summary>
        /// Builds a mesh containing the left and right kerb strips alongside the road.
        /// Each strip is a flat quad sequence elevated by <paramref name="kerbHeight"/>
        /// above the spline Y, starting at the road edge and extending outward by
        /// <paramref name="kerbWidth"/>.
        /// </summary>
        private static Mesh BuildKerbMesh(
            IList<Vector3> splinePoints,
            float halfWidth,
            float kerbWidth,
            float kerbHeight,
            float uvTileLength)
        {
            int n = splinePoints.Count;

            // 4 vertices per spline point:
            //   [i*4+0] = left outer   (road-left edge  − right * kerbWidth, elevated)
            //   [i*4+1] = left inner   (road-left edge,                       elevated)
            //   [i*4+2] = right inner  (road-right edge,                      elevated)
            //   [i*4+3] = right outer  (road-right edge + right * kerbWidth,  elevated)
            var vertices  = new Vector3[n * 4];
            var uvs       = new Vector2[n * 4];
            // 2 kerb strips × 2 triangles per segment × 3 indices = 12 per segment
            var triangles = new int[(n - 1) * 12];

            float distAlongRoad = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = ComputeTangent(splinePoints, i, n);
                Vector3 right   = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 kerbBase = splinePoints[i] + new Vector3(0f, kerbHeight, 0f);

                vertices[i * 4]     = kerbBase - right * (halfWidth + kerbWidth); // left outer
                vertices[i * 4 + 1] = kerbBase - right * halfWidth;               // left inner
                vertices[i * 4 + 2] = kerbBase + right * halfWidth;               // right inner
                vertices[i * 4 + 3] = kerbBase + right * (halfWidth + kerbWidth); // right outer

                if (i > 0)
                    distAlongRoad += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);

                float v = distAlongRoad / uvTileLength;

                // Left kerb:  U 0 (outer) → 1 (inner road edge)
                // Right kerb: U 0 (inner road edge) → 1 (outer)
                uvs[i * 4]     = new Vector2(0f, v);
                uvs[i * 4 + 1] = new Vector2(1f, v);
                uvs[i * 4 + 2] = new Vector2(0f, v);
                uvs[i * 4 + 3] = new Vector2(1f, v);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int tri = i * 12;
                int v0  = i * 4;

                // Left kerb strip (outer=v0, inner=v1)
                triangles[tri]     = v0;
                triangles[tri + 1] = v0 + 4;
                triangles[tri + 2] = v0 + 1;

                triangles[tri + 3] = v0 + 1;
                triangles[tri + 4] = v0 + 4;
                triangles[tri + 5] = v0 + 5;

                // Right kerb strip (inner=v2, outer=v3)
                triangles[tri + 6]  = v0 + 2;
                triangles[tri + 7]  = v0 + 6;
                triangles[tri + 8]  = v0 + 3;

                triangles[tri + 9]  = v0 + 3;
                triangles[tri + 10] = v0 + 6;
                triangles[tri + 11] = v0 + 7;
            }

            var mesh = new Mesh { name = "KerbMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a lane-marking overlay mesh by duplicating the road surface
        /// geometry and elevating it by <see cref="LaneMarkingClearance"/> to prevent
        /// z-fighting.  UV channel 0 carries the lane-marking tiling (V repeats every
        /// <paramref name="laneMarkingTileLength"/> metres, U goes 0→1 across the road),
        /// allowing a dedicated lane-marking material to be applied directly.
        /// </summary>
        private static Mesh BuildLaneMarkingMesh(
            IList<Vector3> splinePoints,
            float halfWidth,
            float laneMarkingTileLength)
        {
            int n = splinePoints.Count;
            var vertices  = new Vector3[n * 2];
            var uvs       = new Vector2[n * 2];
            var triangles = new int[(n - 1) * 6];

            float distAlongRoad = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = ComputeTangent(splinePoints, i, n);
                Vector3 right   = Vector3.Cross(Vector3.up, tangent).normalized;

                // Position the overlay a few mm above the road surface.
                Vector3 centre = splinePoints[i] + Vector3.up * LaneMarkingClearance;
                vertices[i * 2]     = centre - right * halfWidth;
                vertices[i * 2 + 1] = centre + right * halfWidth;

                if (i > 0)
                    distAlongRoad += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);

                float v = distAlongRoad / laneMarkingTileLength;
                uvs[i * 2]     = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int tri = i * 6;
                int v0  = i * 2;

                triangles[tri]     = v0;
                triangles[tri + 1] = v0 + 2;
                triangles[tri + 2] = v0 + 1;

                triangles[tri + 3] = v0 + 1;
                triangles[tri + 4] = v0 + 2;
                triangles[tri + 5] = v0 + 3;
            }

            var mesh = new Mesh { name = "LaneMarkingMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a V-profile roadside ditch mesh on both sides of the carriageway.
        /// Each side has three profile vertices per spline point:
        /// <list type="bullet">
        ///   <item>inner edge — at the road edge, road-surface height</item>
        ///   <item>bottom — half a <paramref name="ditchWidth"/> outward, dropped by <paramref name="ditchDepth"/></item>
        ///   <item>outer edge — a full <paramref name="ditchWidth"/> outward, back to road-surface height</item>
        /// </list>
        /// UV channel 0: U 0→1 (inner→outer), V tiles every 10 m along the road.
        /// </summary>
        private static Mesh BuildDitchMesh(
            IList<Vector3> splinePoints,
            float halfWidth,
            float ditchDepth = RuralDitchDepth,
            float ditchWidth = RuralDitchWidth)
        {
            int n = splinePoints.Count;

            // 6 vertices per spline point:
            //   [i*6+0] = left inner  (road edge,            road_y)
            //   [i*6+1] = left bottom (road_edge+ditchWidth/2, road_y-ditchDepth)
            //   [i*6+2] = left outer  (road_edge+ditchWidth, road_y)
            //   [i*6+3] = right inner (road edge,            road_y)
            //   [i*6+4] = right bottom(road_edge+ditchWidth/2, road_y-ditchDepth)
            //   [i*6+5] = right outer (road_edge+ditchWidth, road_y)
            var vertices  = new Vector3[n * 6];
            var uvs       = new Vector2[n * 6];
            // 2 sides × 2 slopes × 2 triangles × 3 indices = 24 per segment
            var triangles = new int[(n - 1) * 24];

            float halfDitch     = ditchWidth * 0.5f;
            float distAlongRoad = 0f;

            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = ComputeTangent(splinePoints, i, n);
                Vector3 right   = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 centre  = splinePoints[i];
                Vector3 down    = new Vector3(0f, -ditchDepth, 0f);

                // Left side (outward = −right direction)
                vertices[i * 6]     = centre - right * halfWidth;                     // left inner
                vertices[i * 6 + 1] = centre - right * (halfWidth + halfDitch) + down; // left bottom
                vertices[i * 6 + 2] = centre - right * (halfWidth + ditchWidth);      // left outer

                // Right side (outward = +right direction)
                vertices[i * 6 + 3] = centre + right * halfWidth;                     // right inner
                vertices[i * 6 + 4] = centre + right * (halfWidth + halfDitch) + down; // right bottom
                vertices[i * 6 + 5] = centre + right * (halfWidth + ditchWidth);      // right outer

                if (i > 0)
                    distAlongRoad += Vector3.Distance(splinePoints[i], splinePoints[i - 1]);

                float v = distAlongRoad / 10f;

                // U: 0 (inner/road edge) → 0.5 (bottom) → 1 (outer)
                uvs[i * 6]     = new Vector2(0f,   v);
                uvs[i * 6 + 1] = new Vector2(0.5f, v);
                uvs[i * 6 + 2] = new Vector2(1f,   v);
                uvs[i * 6 + 3] = new Vector2(0f,   v);
                uvs[i * 6 + 4] = new Vector2(0.5f, v);
                uvs[i * 6 + 5] = new Vector2(1f,   v);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int tri = i * 24;
                int v0  = i * 6;

                // Left inner slope (inner → bottom), CW from above
                triangles[tri]      = v0 + 6;   // left inner, far
                triangles[tri + 1]  = v0;        // left inner, near
                triangles[tri + 2]  = v0 + 1;   // left bottom, near
                triangles[tri + 3]  = v0 + 6;   // left inner, far
                triangles[tri + 4]  = v0 + 1;   // left bottom, near
                triangles[tri + 5]  = v0 + 7;   // left bottom, far

                // Left outer slope (bottom → outer), CW from above
                triangles[tri + 6]  = v0 + 7;   // left bottom, far
                triangles[tri + 7]  = v0 + 1;   // left bottom, near
                triangles[tri + 8]  = v0 + 2;   // left outer, near
                triangles[tri + 9]  = v0 + 7;   // left bottom, far
                triangles[tri + 10] = v0 + 2;   // left outer, near
                triangles[tri + 11] = v0 + 8;   // left outer, far

                // Right inner slope (inner → bottom), CW from above
                triangles[tri + 12] = v0 + 3;   // right inner, near
                triangles[tri + 13] = v0 + 9;   // right inner, far
                triangles[tri + 14] = v0 + 10;  // right bottom, far
                triangles[tri + 15] = v0 + 3;   // right inner, near
                triangles[tri + 16] = v0 + 10;  // right bottom, far
                triangles[tri + 17] = v0 + 4;   // right bottom, near

                // Right outer slope (bottom → outer), CW from above
                triangles[tri + 18] = v0 + 4;   // right bottom, near
                triangles[tri + 19] = v0 + 10;  // right bottom, far
                triangles[tri + 20] = v0 + 11;  // right outer, far
                triangles[tri + 21] = v0 + 4;   // right bottom, near
                triangles[tri + 22] = v0 + 11;  // right outer, far
                triangles[tri + 23] = v0 + 5;   // right outer, near
            }

            var mesh = new Mesh { name = "DitchMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
