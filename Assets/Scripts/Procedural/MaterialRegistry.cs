using System.Collections.Generic;
using UnityEngine;

namespace VectorRoad.Procedural
{
    /// <summary>
    /// Maps texture-ID strings (as returned by <see cref="RegionTextures"/>) to Unity
    /// <c>Material</c> assets, and provides helpers to apply those materials to
    /// <c>MeshRenderer</c> components.
    ///
    /// Attach this component to a scene GameObject and populate the <c>_entries</c> list
    /// in the Inspector: one row per distinct texture ID.  At runtime the component builds
    /// an internal dictionary so every material look-up is O(1).
    ///
    /// Usage (from a scene-assembly MonoBehaviour):
    /// <code>
    ///   // After building meshes from the procedural generators:
    ///   RoadMeshResult road = RoadMeshExtruder.ExtrudeWithDetails(spline, roadType, region: region);
    ///   roadRenderer.sharedMesh = road.RoadMesh;
    ///   materialRegistry.ApplyTo(roadRenderer, road.RoadTextureId);
    ///
    ///   kerbRenderer.sharedMesh = road.KerbMesh;
    ///   materialRegistry.ApplyTo(kerbRenderer, road.KerbTextureId);
    ///
    ///   BuildingMeshResult bld = BuildingGenerator.Extrude(footprint, region: region);
    ///   wallRenderer.sharedMesh = bld.WallMesh;
    ///   materialRegistry.ApplyTo(wallRenderer, bld.WallTextureId);
    ///
    ///   roofRenderer.sharedMesh = bld.RoofMesh;
    ///   materialRegistry.ApplyTo(roofRenderer, bld.RoofTextureId);
    /// </code>
    /// </summary>
    public class MaterialRegistry : MonoBehaviour
    {
        // ── Serialised data ────────────────────────────────────────────────────

        /// <summary>A single texture-ID → material binding.</summary>
        [System.Serializable]
        public struct MaterialEntry
        {
            /// <summary>
            /// Texture identifier as returned by <see cref="RegionTextures"/>
            /// (e.g. <c>"road_asphalt_temperate"</c>).
            /// </summary>
            public string TextureId;

            /// <summary>
            /// The Unity material asset to apply when <see cref="TextureId"/> is requested.
            /// </summary>
            public Material Material;
        }

        [SerializeField]
        private List<MaterialEntry> _entries = new List<MaterialEntry>();

        // ── Runtime look-up ────────────────────────────────────────────────────

        private Dictionary<string, Material> _lookup = new Dictionary<string, Material>();

        private void Awake()
        {
            BuildLookup();
            PlaceholderMaterialFactory.FillMissing(this);
        }

        /// <summary>
        /// (Re-)builds the internal look-up dictionary from the current <c>_entries</c> list.
        /// Called automatically in <c>Awake</c>; exposed as <c>internal</c> so the test
        /// project can initialise the registry without relying on the Unity lifecycle.
        /// </summary>
        internal void BuildLookup()
        {
            _lookup = new Dictionary<string, Material>(_entries.Count);
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.TextureId) && entry.Material != null)
                    _lookup[entry.TextureId] = entry.Material;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a <paramref name="material"/> for the given <paramref name="textureId"/>
        /// so it can be retrieved later via <see cref="GetMaterial"/>.
        ///
        /// If <paramref name="textureId"/> is already registered the new material replaces
        /// the old one.  Null or empty IDs are silently ignored.
        /// A null <paramref name="material"/> is stored in the entry list but is not added to
        /// the look-up dictionary, so <see cref="GetMaterial"/> will still return <c>null</c>
        /// for that ID until a non-null material is registered.
        /// </summary>
        public void Register(string textureId, Material material)
        {
            if (string.IsNullOrEmpty(textureId)) return;

            _entries.Add(new MaterialEntry { TextureId = textureId, Material = material });
            if (material != null)
                _lookup[textureId] = material;
        }

        /// <summary>
        /// Returns the <c>Material</c> registered for <paramref name="textureId"/>,
        /// or <c>null</c> if no entry exists for that ID.
        /// </summary>
        public Material GetMaterial(string textureId)
        {
            if (string.IsNullOrEmpty(textureId))
                return null;

            _lookup.TryGetValue(textureId, out var mat);
            return mat;
        }

        /// <summary>
        /// Assigns the material registered for <paramref name="textureId"/> as the
        /// <c>sharedMaterial</c> on <paramref name="renderer"/>.
        /// Does nothing if <paramref name="renderer"/> is <c>null</c> or if no material
        /// is registered for the given ID.
        /// </summary>
        public void ApplyTo(MeshRenderer renderer, string textureId)
        {
            if (renderer == null) return;

            var mat = GetMaterial(textureId);
            if (mat != null)
                renderer.sharedMaterial = mat;
        }
    }
}
