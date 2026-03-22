using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VectorRoad.DataInversion;

namespace VectorRoad.Hud
{
    /// <summary>
    /// Renders the <see cref="MinimapRenderer"/> output onto a UI <see cref="RawImage"/>
    /// each frame by painting road lines onto a <see cref="Texture2D"/>.
    ///
    /// <para>
    /// Attach to any persistent GameObject (e.g. the same one that holds
    /// <see cref="VectorRoad.Core.MapSceneBuilder"/>), assign a <see cref="RawImage"/>
    /// from the HUD Canvas to <see cref="Target"/>, then wait for
    /// <see cref="VectorRoad.Core.MapSceneBuilder"/> to call <see cref="Init"/> once
    /// the map has loaded.
    /// </para>
    /// </summary>
    public sealed class MinimapHud : MonoBehaviour
    {
        [Tooltip("RawImage on the HUD Canvas that will display the minimap.")]
        public RawImage Target;

        [Tooltip("World-space radius (metres) shown on the minimap.")]
        public float Radius = 150f;

        [Tooltip("Side length in pixels of the minimap texture (square).")]
        public int Resolution = 256;

        private MinimapRenderer   _renderer;
        private Texture2D         _texture;
        private Transform         _vehicle;
        private List<RoadSegment> _roads;

        // Two pixel buffers: _clearBuffer is the blank background (never mutated after Init),
        // _workBuffer is reused each frame to avoid per-frame heap allocations.
        private Color32[] _clearBuffer;
        private Color32[] _workBuffer;

        private static readonly Color32 Background = new Color32(30,  30,  30,  210);
        private static readonly Color32 RoadColor  = new Color32(220, 185, 80,  255);
        private static readonly Color32 PlayerDot  = new Color32(255, 60,  60,  255);

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Supplies the vehicle <see cref="Transform"/> used as the map centre and the
        /// road segments to draw each frame.
        /// Called by <see cref="VectorRoad.Core.MapSceneBuilder"/> after the map loads.
        /// </summary>
        public void Init(Transform vehicle, IEnumerable<RoadSegment> roads)
        {
            _vehicle  = vehicle;
            _roads    = new List<RoadSegment>(roads);
            _renderer = new MinimapRenderer { Radius = Radius };

            int count    = Resolution * Resolution;
            _clearBuffer = new Color32[count];
            _workBuffer  = new Color32[count];
            for (int i = 0; i < count; i++)
                _clearBuffer[i] = Background;

            _texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            if (Target != null)
                Target.texture = _texture;
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }

        private void Update()
        {
            if (_vehicle == null || _roads == null || _texture == null)
                return;

            float yaw   = _vehicle.eulerAngles.y;
            var   lines = _renderer.BuildLines(_roads, _vehicle.position, yaw);

            // Reset work buffer to the background.
            System.Array.Copy(_clearBuffer, _workBuffer, _clearBuffer.Length);

            // Draw road lines.
            foreach (MinimapLine line in lines)
            {
                DrawLine(
                    (int)(line.Start.x * Resolution), (int)(line.Start.y * Resolution),
                    (int)(line.End.x   * Resolution), (int)(line.End.y   * Resolution),
                    RoadColor);
            }

            // Player dot at the minimap centre.
            int cx = Resolution / 2;
            int cy = Resolution / 2;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
                SetPixel(cx + dx, cy + dy, PlayerDot);

            _texture.SetPixels32(_workBuffer);
            _texture.Apply();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Bresenham's line algorithm writing into <see cref="_workBuffer"/>.</summary>
        private void DrawLine(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx  =  Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy  = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                SetPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 >= dy) { if (x0 == x1) break; err += dy; x0 += sx; }
                if (e2 <= dx) { if (y0 == y1) break; err += dx; y0 += sy; }
            }
        }

        private void SetPixel(int x, int y, Color32 color)
        {
            if ((uint)x < (uint)Resolution && (uint)y < (uint)Resolution)
                _workBuffer[y * Resolution + x] = color;
        }
    }
}
