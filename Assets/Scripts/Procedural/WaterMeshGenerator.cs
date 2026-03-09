using System.Collections.Generic;
using UnityEngine;
using TerraDrive.DataInversion;

namespace TerraDrive.Procedural
{
    /// <summary>
    /// Generates flat water-surface meshes from <see cref="WaterBody"/> polygon outlines
    /// and selects region-appropriate texture identifiers.
    ///
    /// <para>
    /// The mesh is a fan-triangulated polygon whose vertices are all set to the average
    /// Y elevation of the outline nodes, so the water surface lies flat at the correct
    /// terrain height.
    /// </para>
    ///
    /// Usage:
    /// <code>
    ///   WaterMeshResult result = WaterMeshGenerator.Generate(waterBody, region: region);
    ///   meshFilter.sharedMesh = result.Mesh;
    ///   // Apply material by result.TextureId
    /// </code>
    /// </summary>
    public static class WaterMeshGenerator
    {
        /// <summary>
        /// World-space UV tile scale for water textures (tiles per metre).
        /// A value of 0.05 tiles one texture repeat every 20 metres, which gives a
        /// natural scale for lake and river surface patterns visible from the default
        /// chase-camera distance.
        /// </summary>
        internal const float UvScale = 0.05f;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a flat polygon mesh for a water body and selects a
        /// region-appropriate texture identifier.
        /// </summary>
        /// <param name="waterBody">
        /// The water body whose <see cref="WaterBody.Outline"/> defines the polygon.
        /// At least 3 points are required; outlines with fewer points produce an empty mesh.
        /// </param>
        /// <param name="region">
        /// Climate zone used to select a region-appropriate texture identifier.
        /// Defaults to <see cref="RegionType.Unknown"/>.
        /// </param>
        /// <returns>
        /// A <see cref="WaterMeshResult"/> containing the flat mesh and texture identifier
        /// ready to assign to <c>MeshFilter</c> and <c>MeshRenderer</c> components.
        /// </returns>
        public static WaterMeshResult Generate(
            WaterBody waterBody,
            RegionType region = RegionType.Unknown)
        {
            if (waterBody == null || waterBody.Outline == null || waterBody.Outline.Count < 3)
            {
                Debug.LogWarning("[WaterMeshGenerator] Water body outline must have at least 3 points.");
                return new WaterMeshResult(new Mesh(), string.Empty);
            }

            Mesh   mesh      = BuildMesh(waterBody.Outline);
            string textureId = RegionTextures.GetWaterTextureId(region);

            return new WaterMeshResult(mesh, textureId);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static Mesh BuildMesh(IList<Vector3> outline)
        {
            int n = outline.Count;

            // Compute the average Y (terrain elevation) across all outline nodes so
            // the water surface sits flat at the correct height.
            float avgY = 0f;
            foreach (var p in outline)
                avgY += p.y;
            avgY /= n;

            // Fan triangulation from centroid — the same approach used by
            // BuildingGenerator.BuildRoof for flat cap meshes.
            var verts = new List<Vector3>(n + 1);
            var uvs   = new List<Vector2>(n + 1);
            var tris  = new List<int>(n * 3);

            // Centroid
            Vector3 centroid = Vector3.zero;
            foreach (var p in outline)
                centroid += p;
            centroid /= n;
            centroid.y = avgY;

            verts.Add(centroid);
            uvs.Add(new Vector2(centroid.x * UvScale, centroid.z * UvScale));

            foreach (var p in outline)
            {
                verts.Add(new Vector3(p.x, avgY, p.z));
                uvs.Add(new Vector2(p.x * UvScale, p.z * UvScale));
            }

            for (int i = 0; i < n; i++)
            {
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add((i + 1) % n + 1);
            }

            var mesh = new Mesh { name = "WaterSurface" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
