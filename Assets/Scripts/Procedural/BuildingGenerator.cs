using System;
using System.Collections.Generic;
using UnityEngine;
using TerraDrive.DataInversion;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Extrudes OSM building footprint polygons into 3D wall and roof meshes,
    /// and selects region-appropriate texture identifiers for each surface.
    ///
    /// Usage:
    /// <code>
    ///   BuildingMeshResult result = BuildingGenerator.Extrude(footprint, minHeight: 5f, maxHeight: 15f, region: regionType);
    ///   wallFilter.sharedMesh = result.WallMesh;
    ///   roofFilter.sharedMesh = result.RoofMesh;
    ///   // Apply materials by result.WallTextureId / result.RoofTextureId
    /// </code>
    ///
    /// Heights are derived from a seeded hash of the OSM <c>WayId</c> so the same map always
    /// generates an identical city skyline.
    /// </summary>
    public static class BuildingGenerator
    {
        // ── Constants ──────────────────────────────────────────────────────────

        /// <summary>Fallback UV tile scale for wall textures (1 unit = 1 metre).</summary>
        private const float WallUvScale = 1f;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Extrudes a building footprint into separate wall and flat roof meshes, and
        /// selects region-appropriate texture identifiers for the surfaces.
        /// </summary>
        /// <param name="footprint">
        /// Ordered XZ world-space corner positions of the building outline.
        /// The last point does <em>not</em> need to repeat the first.
        /// </param>
        /// <param name="minHeight">Minimum randomised building height in metres.</param>
        /// <param name="maxHeight">Maximum randomised building height in metres.</param>
        /// <param name="wayId">
        /// OSM way identifier used as the RNG seed.  Pass 0 for a fully random height.
        /// </param>
        /// <param name="region">
        /// Climate zone used to select region-appropriate texture identifiers for
        /// the wall and roof surfaces.  Defaults to <see cref="RegionType.Unknown"/>.
        /// </param>
        /// <returns>
        /// A <see cref="BuildingMeshResult"/> containing the wall and roof meshes plus
        /// texture identifiers ready to assign to <c>MeshFilter</c> and
        /// <c>MeshRenderer</c> components.
        /// </returns>
        public static BuildingMeshResult Extrude(
            IList<Vector3> footprint,
            float minHeight = 4f,
            float maxHeight = 6f,
            long wayId = 0,
            RegionType region = RegionType.Unknown)
        {
            if (footprint == null || footprint.Count < 3)
            {
                Debug.LogWarning("[BuildingGenerator] Footprint must have at least 3 points.");
                return new BuildingMeshResult(new Mesh(), new Mesh(), string.Empty, string.Empty);
            }

            float height = SeededHeight(wayId, minHeight, maxHeight);

            Mesh walls = BuildWalls(footprint, height);
            Mesh roof  = BuildRoof(footprint, height);

            string wallTextureId = RegionTextures.GetWallTextureId(region);
            string roofTextureId = RegionTextures.GetRoofTextureId(region);

            return new BuildingMeshResult(walls, roof, wallTextureId, roofTextureId);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>Returns a deterministic height within [min, max] seeded by <paramref name="wayId"/>.</summary>
        private static float SeededHeight(long wayId, float min, float max)
        {
            if (wayId == 0)
                return UnityEngine.Random.Range(min, max);

            // Simple hash: mix the wayId bits and normalise to [0, 1].
            ulong h = unchecked((ulong)wayId);
            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;

            float t = (float)(h & 0xFFFFFFFFUL) / (float)uint.MaxValue;
            return min + t * (max - min);
        }

        private static Mesh BuildWalls(IList<Vector3> footprint, float height)
        {
            int n = footprint.Count;
            // Each wall face is a quad (2 triangles, 4 unique vertices for clean UV mapping).
            var verts = new List<Vector3>(n * 4);
            var uvs = new List<Vector2>(n * 4);
            var tris = new List<int>(n * 6);

            for (int i = 0; i < n; i++)
            {
                Vector3 a = footprint[i];
                Vector3 b = footprint[(i + 1) % n];

                float wallWidth = Vector3.Distance(a, b);

                int baseIdx = verts.Count;
                // Bottom-left, bottom-right, top-left, top-right
                verts.Add(a);
                verts.Add(b);
                verts.Add(new Vector3(a.x, a.y + height, a.z));
                verts.Add(new Vector3(b.x, b.y + height, b.z));

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(wallWidth * WallUvScale, 0f));
                uvs.Add(new Vector2(0f, height * WallUvScale));
                uvs.Add(new Vector2(wallWidth * WallUvScale, height * WallUvScale));

                tris.Add(baseIdx);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1);

                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 3);
            }

            var mesh = new Mesh { name = "BuildingWalls" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRoof(IList<Vector3> footprint, float height)
        {
            // Flat roof: fan triangulation from centroid.
            int n = footprint.Count;
            var verts = new List<Vector3>(n + 1);
            var uvs = new List<Vector2>(n + 1);
            var tris = new List<int>(n * 3);

            // Centroid
            Vector3 centroid = Vector3.zero;
            foreach (var p in footprint)
                centroid += p;
            centroid /= n;
            centroid.y += height;

            verts.Add(centroid);
            uvs.Add(new Vector2(0.5f, 0.5f));

            foreach (var p in footprint)
            {
                verts.Add(new Vector3(p.x, p.y + height, p.z));
                uvs.Add(new Vector2(p.x * 0.05f, p.z * 0.05f));
            }

            for (int i = 0; i < n; i++)
            {
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add((i + 1) % n + 1);
            }

            var mesh = new Mesh { name = "BuildingRoof" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
