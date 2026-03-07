using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using TerraDrive.DataInversion;
using UnityEngine;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Renders a top-down 2-D preview of the parsed map data (roads and building
    /// footprints) to a <see cref="SKBitmap"/> using an OSM-inspired colour scheme.
    /// </summary>
    public static class MapRenderer
    {
        // Road styles keyed by OSM highway value: (fill colour, pixel stroke width)
        private static readonly Dictionary<string, (SKColor fill, float width)> RoadStyles =
            new Dictionary<string, (SKColor, float)>
            {
                { "motorway",     (SKColor.Parse("#e892a2"), 8f) },
                { "motorway_link",(SKColor.Parse("#e892a2"), 4f) },
                { "trunk",        (SKColor.Parse("#f9b29c"), 7f) },
                { "trunk_link",   (SKColor.Parse("#f9b29c"), 4f) },
                { "primary",      (SKColor.Parse("#fcd6a4"), 6f) },
                { "primary_link", (SKColor.Parse("#fcd6a4"), 3f) },
                { "secondary",    (SKColor.Parse("#f7fabf"), 5f) },
                { "secondary_link",(SKColor.Parse("#f7fabf"), 3f) },
                { "tertiary",     (new SKColor(255, 255, 255), 4f) },
                { "tertiary_link",(new SKColor(255, 255, 255), 2f) },
                { "residential",  (new SKColor(255, 255, 255), 3f) },
                { "service",      (new SKColor(255, 255, 255), 2f) },
                { "unclassified", (new SKColor(255, 255, 255), 2f) },
                { "living_street",(new SKColor(230, 230, 230), 2f) },
                { "pedestrian",   (new SKColor(220, 220, 255), 2f) },
                { "cycleway",     (SKColor.Parse("#99ccff"), 1.5f) },
                { "footway",      (SKColor.Parse("#ff9999"), 1f) },
                { "path",         (SKColor.Parse("#ff9999"), 1f) },
            };

        private static readonly (SKColor fill, float width) DefaultRoadStyle =
            (new SKColor(200, 200, 200), 1.5f);

        // Draw order: least-important roads first so major roads render on top
        private static readonly string[] DrawOrder =
        {
            "path", "footway", "cycleway", "pedestrian", "living_street",
            "service", "unclassified", "residential",
            "tertiary_link", "tertiary",
            "secondary_link", "secondary",
            "primary_link", "primary",
            "trunk_link", "trunk",
            "motorway_link", "motorway",
        };

        /// <summary>
        /// Renders the supplied roads and buildings into a bitmap.
        /// </summary>
        /// <param name="roads">Road segments produced by <see cref="OSMParser"/>.</param>
        /// <param name="buildings">Building footprints produced by <see cref="OSMParser"/>.</param>
        /// <param name="width">Output image width in pixels (default 1200).</param>
        /// <param name="height">Output image height in pixels (default 900).</param>
        /// <returns>A new <see cref="SKBitmap"/> owned by the caller.</returns>
        public static SKBitmap Render(
            List<RoadSegment> roads,
            List<BuildingFootprint> buildings,
            int width = 1200,
            int height = 900)
        {
            // ── 1. Compute world-space bounding box ───────────────────────────
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            void Expand(List<Vector3> nodes)
            {
                foreach (var n in nodes)
                {
                    if (n.x < minX) minX = n.x;
                    if (n.x > maxX) maxX = n.x;
                    if (n.z < minZ) minZ = n.z;
                    if (n.z > maxZ) maxZ = n.z;
                }
            }

            foreach (var road in roads)     Expand(road.Nodes);
            foreach (var b    in buildings) Expand(b.Footprint);

            if (minX == float.MaxValue) { minX = -1; maxX = 1; minZ = -1; maxZ = 1; }

            // ── 2. Compute scale that preserves aspect ratio ──────────────────
            const float padding = 24f;
            float rangeX = maxX - minX;
            float rangeZ = maxZ - minZ;
            if (rangeX <= 0) rangeX = 1;
            if (rangeZ <= 0) rangeZ = 1;

            float scaleX = (width  - 2 * padding) / rangeX;
            float scaleZ = (height - 2 * padding) / rangeZ;
            float scale  = Math.Min(scaleX, scaleZ);

            // Centre the map within the image
            float offsetX = padding + ((width  - 2 * padding) - rangeX * scale) / 2f;
            float offsetZ = padding + ((height - 2 * padding) - rangeZ * scale) / 2f;

            SKPoint ToImage(Vector3 v) => new SKPoint(
                offsetX + (v.x - minX) * scale,
                offsetZ + (maxZ - v.z) * scale);   // flip Z so north is up

            // ── 3. Create surface ─────────────────────────────────────────────
            var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColor.Parse("#f2efe9")); // OSM land colour

            // ── 4. Draw building footprints ───────────────────────────────────
            using var buildingFillPaint = new SKPaint
            {
                Color = SKColor.Parse("#d9d0c9"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using var buildingStrokePaint = new SKPaint
            {
                Color = SKColor.Parse("#b5aca4"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                IsAntialias = true,
            };

            foreach (var building in buildings)
            {
                if (building.Footprint.Count < 3) continue;
                using var path = new SKPath();
                path.MoveTo(ToImage(building.Footprint[0]));
                for (int i = 1; i < building.Footprint.Count; i++)
                    path.LineTo(ToImage(building.Footprint[i]));
                path.Close();
                canvas.DrawPath(path, buildingFillPaint);
                canvas.DrawPath(path, buildingStrokePaint);
            }

            // ── 5. Draw roads ─────────────────────────────────────────────────
            var knownTypes = new HashSet<string>(DrawOrder);

            // Unknown highway types first (under everything)
            DrawRoads(canvas, roads, ToImage, null, knownTypes, drawUnknownTypes: true);

            // Known types in draw order
            foreach (string roadType in DrawOrder)
                DrawRoads(canvas, roads, ToImage, roadType, knownTypes, drawUnknownTypes: false);

            // ── 6. Legend ─────────────────────────────────────────────────────
            DrawLegend(canvas, width, height, roads.Count, buildings.Count);

            return bitmap;
        }

        /// <summary>Encodes <paramref name="bitmap"/> as a PNG file at <paramref name="outputPath"/>.</summary>
        public static void SaveAsPng(SKBitmap bitmap, string outputPath)
        {
            using var image  = SKImage.FromBitmap(bitmap);
            using var data   = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write);
            data.SaveTo(stream);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static void DrawRoads(
            SKCanvas canvas,
            List<RoadSegment> roads,
            Func<Vector3, SKPoint> toImage,
            string? filterType,
            HashSet<string> knownTypes,
            bool drawUnknownTypes)
        {
            foreach (var road in roads)
            {
                bool isKnown = knownTypes.Contains(road.HighwayType);
                if ( drawUnknownTypes &&  isKnown) continue;
                if (!drawUnknownTypes && !isKnown) continue;
                if (filterType != null && road.HighwayType != filterType) continue;
                if (road.Nodes.Count < 2) continue;

                var (fill, lineWidth) = RoadStyles.TryGetValue(road.HighwayType, out var s)
                    ? s : DefaultRoadStyle;

                using var path = new SKPath();
                path.MoveTo(toImage(road.Nodes[0]));
                for (int i = 1; i < road.Nodes.Count; i++)
                    path.LineTo(toImage(road.Nodes[i]));

                // Casing (outline)
                using var casingPaint = new SKPaint
                {
                    Color       = new SKColor(180, 170, 160),
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = lineWidth + 2f,
                    StrokeCap   = SKStrokeCap.Round,
                    StrokeJoin  = SKStrokeJoin.Round,
                    IsAntialias = true,
                };
                canvas.DrawPath(path, casingPaint);

                // Fill
                using var fillPaint = new SKPaint
                {
                    Color       = fill,
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = lineWidth,
                    StrokeCap   = SKStrokeCap.Round,
                    StrokeJoin  = SKStrokeJoin.Round,
                    IsAntialias = true,
                };
                canvas.DrawPath(path, fillPaint);
            }
        }

        private static void DrawLegend(
            SKCanvas canvas, int width, int height, int roadCount, int buildingCount)
        {
            const float margin = 8f;
            const float lineH  = 16f;
            // Small upward nudge so the text sits visually centred in each line slot
            // (SkiaSharp DrawText baseline is at the Y coordinate, not the top).
            const float textBaselineOffset = 2f;

            string[] lines =
            {
                $"Roads: {roadCount}",
                $"Buildings: {buildingCount}",
            };

            using var font = new SKFont { Size = 13f };
            using var textPaint = new SKPaint
            {
                Color       = new SKColor(60, 60, 60),
                IsAntialias = true,
            };
            using var bgPaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 200),
            };

            float boxW = 130f;
            float boxH = lines.Length * lineH + margin * 2;
            float boxX = width  - boxW - margin;
            float boxY = height - boxH - margin;

            canvas.DrawRect(boxX, boxY, boxW, boxH, bgPaint);
            for (int i = 0; i < lines.Length; i++)
                canvas.DrawText(
                    lines[i],
                    boxX + margin,
                    boxY + margin + (i + 1) * lineH - textBaselineOffset,
                    SKTextAlign.Left,
                    font,
                    textPaint);
        }
    }
}
