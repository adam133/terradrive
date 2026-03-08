using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SkiaSharp;
using TerraDrive.DataInversion;
using SysVec3 = System.Numerics.Vector3;
using UnityVec3 = UnityEngine.Vector3;

namespace TerraDrive.Tests
{
    /// <summary>
    /// Renders a perspective (chase-cam) view of the parsed map data from a static
    /// camera position, simulating the in-game <c>ChaseCam</c> controller.
    ///
    /// The camera is placed 8 m behind and 3 m above a "car" sitting at a chosen
    /// world-space XZ position, looking 3 m ahead of the car — matching the
    /// default inspector values in <c>ChaseCam.cs</c>.
    /// </summary>
    public static class ChaseCamRenderer
    {
        // Camera defaults matching ChaseCam.cs inspector defaults.
        private const float FollowDistance    = 8f;
        private const float CameraHeight      = 3f;
        private const float LookAheadDistance = 3f;
        private const float FovDegrees        = 60f;
        private const float NearPlane         = 0.3f;
        private const float FarPlane          = 1500f;

        // Projection rendering constants
        /// <summary>
        /// NDC guard band: allows points slightly outside [-1,1] to avoid popping
        /// at the screen edges when a segment straddles the frustum boundary.
        /// </summary>
        private const float NdcGuardBand = 1.2f;

        // ── Road colours (matching MapRenderer) ───────────────────────────────

        private static SKColor RoadColour(string highwayType) =>
            highwayType switch
            {
                "motorway" or "motorway_link"   => SKColor.Parse("#e892a2"),
                "trunk"    or "trunk_link"       => SKColor.Parse("#f9b29c"),
                "primary"  or "primary_link"     => SKColor.Parse("#fcd6a4"),
                "secondary" or "secondary_link"  => SKColor.Parse("#f7fabf"),
                "tertiary"  or "tertiary_link"   => new SKColor(255, 255, 255),
                "residential" or "living_street" => new SKColor(255, 255, 255),
                _                                => new SKColor(200, 200, 200),
            };

        /// <summary>
        /// Returns the road half-width (in world metres) for a given OSM highway type,
        /// matching the canonical values in <c>RoadMeshExtruder.RoadWidths</c>.
        /// </summary>
        private static float RoadHalfWidth(string highwayType) =>
            highwayType switch
            {
                "motorway" or "motorway_link"    => 10.0f,  // 20 m full width
                "trunk"    or "trunk_link"       =>  7.5f,  // 15 m
                "primary"  or "primary_link"     =>  6.0f,  // 12 m
                "secondary" or "secondary_link"  =>  4.5f,  //  9 m
                "tertiary" or "tertiary_link"    =>  3.5f,  //  7 m
                "residential" or "living_street" =>  2.75f, //  5.5 m
                "service"                        =>  2.0f,  //  4 m
                _                                =>  3.5f,  //  7 m fallback
            };

        /// <summary>
        /// Produces a deterministic building height in [<paramref name="minH"/>,
        /// <paramref name="maxH"/>] metres using the same hash as
        /// <c>BuildingGenerator.SeededHeight</c>, so the preview matches the game world.
        /// </summary>
        private static float BuildingSeededHeight(long wayId, float minH = 5f, float maxH = 15f)
        {
            if (wayId == 0) return (minH + maxH) * 0.5f;
            ulong h = unchecked((ulong)wayId);
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33; h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;
            float t = (float)(h & 0xFFFFFFFFUL) / (float)uint.MaxValue;
            return minH + t * (maxH - minH);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Renders a perspective chase-cam view of the supplied map data.
        /// </summary>
        /// <param name="roads">Road segments from <see cref="OSMParser"/>.</param>
        /// <param name="buildings">Building footprints from <see cref="OSMParser"/>.</param>
        /// <param name="carX">World-space X of the static car position.</param>
        /// <param name="carZ">World-space Z of the static car position.</param>
        /// <param name="width">Output image width in pixels.</param>
        /// <param name="height">Output image height in pixels.</param>
        /// <returns>A new <see cref="SKBitmap"/> owned by the caller.</returns>
        public static SKBitmap Render(
            List<RoadSegment>      roads,
            List<BuildingFootprint> buildings,
            float carX  = 0f,
            float carZ  = 0f,
            int   width = 1600,
            int   height = 900)
        {
            // Camera: FollowDistance behind the car, CameraHeight above it,
            // looking LookAheadDistance ahead of the car — matching ChaseCam.cs defaults.
            var cameraPos  = new SysVec3(carX, CameraHeight, carZ - FollowDistance);
            var lookTarget = new SysVec3(carX, 0f,           carZ + LookAheadDistance);

            Matrix4x4 view = Matrix4x4.CreateLookAt(cameraPos, lookTarget, SysVec3.UnitY);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(
                FovDegrees * MathF.PI / 180f,
                (float)width / height,
                NearPlane, FarPlane);
            Matrix4x4 vp = view * proj;

            var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);

            // 1. Sky + ground background
            float horizonY = ComputeHorizonY(vp, width, height);
            DrawBackground(canvas, width, height, horizonY);

            // 2. Building floor polygons (under roads)
            foreach (var b in buildings)
            {
                if (b.Footprint.Count < 3) continue;
                DrawBuilding3D(canvas, b, cameraPos, vp, width, height);
            }

            // 3. Roads
            foreach (var r in roads)
            {
                if (r.Nodes.Count < 2) continue;
                DrawRoadSegment(canvas, r, vp, width, height);
            }

            // 4. Car marker at the static position
            DrawCarMarker(canvas, carX, carZ, vp, width, height);

            // 5. Info overlay
            DrawOverlay(canvas, width, height, carX, carZ, roads.Count, buildings.Count);

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

        // ── Projection ─────────────────────────────────────────────────────────

        /// <summary>
        /// Projects a world-space point to a screen-space pixel coordinate.
        /// Returns <c>false</c> if the point is behind the camera or outside
        /// a small NDC guard band.
        /// </summary>
        private static bool Project(
            float wx, float wy, float wz,
            Matrix4x4 vp,
            int w, int h,
            out SKPoint screen)
        {
            var clip = Vector4.Transform(new Vector4(wx, wy, wz, 1f), vp);

            if (clip.W <= 0f)                              // behind camera
            { screen = default; return false; }

            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;

            // Reject points well outside the visible frustum
            if (ndcX < -NdcGuardBand || ndcX > NdcGuardBand || ndcY < -NdcGuardBand || ndcY > NdcGuardBand)
            { screen = default; return false; }

            screen = new SKPoint(
                (ndcX + 1f) * 0.5f * w,
                (1f - ndcY) * 0.5f * h);
            return true;
        }

        private static float ComputeHorizonY(Matrix4x4 vp, int w, int h)
        {
            // Project a ground-level point far in front of the camera to find the horizon.
            if (Project(0f, 0f, 999f, vp, w, h, out var sp))
                return Math.Clamp(sp.Y, 0f, h);
            return h * 0.4f;
        }

        // ── Drawing helpers ────────────────────────────────────────────────────

        private static void DrawBackground(SKCanvas canvas, int w, int h, float horizonY)
        {
            // Sky gradient
            using var skyPaint  = new SKPaint { IsAntialias = false };
            using var skyShader = SKShader.CreateLinearGradient(
                new SKPoint(0f, 0f), new SKPoint(0f, horizonY),
                new[] { SKColor.Parse("#2c6fad"), SKColor.Parse("#87ceeb") },
                SKShaderTileMode.Clamp);
            skyPaint.Shader = skyShader;
            canvas.DrawRect(0f, 0f, w, horizonY, skyPaint);

            // Ground gradient
            using var groundPaint  = new SKPaint { IsAntialias = false };
            using var groundShader = SKShader.CreateLinearGradient(
                new SKPoint(0f, horizonY), new SKPoint(0f, h),
                new[] { SKColor.Parse("#6b8e5e"), SKColor.Parse("#3d5c2e") },
                SKShaderTileMode.Clamp);
            groundPaint.Shader = groundShader;
            canvas.DrawRect(0f, horizonY, w, h - horizonY, groundPaint);
        }

        private static void DrawRoadSegment(
            SKCanvas canvas, RoadSegment road,
            Matrix4x4 vp, int w, int h)
        {
            var colour = RoadColour(road.HighwayType);
            float hw   = RoadHalfWidth(road.HighwayType);
            var nodes  = road.Nodes;

            for (int i = 0; i < nodes.Count - 1; i++)
            {
                var n0 = nodes[i];
                var n1 = nodes[i + 1];

                // Edge direction in the XZ plane.
                float dx = n1.x - n0.x, dz = n1.z - n0.z;
                float len = MathF.Sqrt(dx * dx + dz * dz);
                if (len < 0.01f) continue;

                // Right perpendicular: Cross((0,1,0), (dx/len, 0, dz/len)) = (dz/len, 0, -dx/len)
                float rx = dz / len, rz = -dx / len;

                // Four road-surface corners at the node Y elevations.
                float lx0 = n0.x - rx * hw, lz0 = n0.z - rz * hw; // left  n0
                float rx0 = n0.x + rx * hw, rz0 = n0.z + rz * hw; // right n0
                float rx1 = n1.x + rx * hw, rz1 = n1.z + rz * hw; // right n1
                float lx1 = n1.x - rx * hw, lz1 = n1.z - rz * hw; // left  n1

                bool okLL = Project(lx0, n0.y, lz0, vp, w, h, out var sLL);
                bool okRL = Project(rx0, n0.y, rz0, vp, w, h, out var sRL);
                bool okRR = Project(rx1, n1.y, rz1, vp, w, h, out var sRR);
                bool okLR = Project(lx1, n1.y, lz1, vp, w, h, out var sLR);

                if (!okLL || !okRL || !okRR || !okLR) continue;

                using var path = new SKPath();
                path.MoveTo(sLL);
                path.LineTo(sRL);
                path.LineTo(sRR);
                path.LineTo(sLR);
                path.Close();

                // Casing (outline)
                using var casingPaint = new SKPaint
                {
                    Color       = new SKColor(150, 140, 130),
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f,
                    IsAntialias = true,
                };
                canvas.DrawPath(path, casingPaint);

                // Fill
                using var fillPaint = new SKPaint
                {
                    Color       = colour,
                    Style       = SKPaintStyle.Fill,
                    IsAntialias = true,
                };
                canvas.DrawPath(path, fillPaint);
            }
        }

        /// <summary>
        /// Draws a building as a 3D extruded structure — walls on each footprint edge
        /// plus a flat roof — matching the geometry produced by
        /// <c>BuildingGenerator.Extrude()</c> in the game engine.
        /// Back-facing walls (facing away from the camera) are culled so that only
        /// the street-visible faces are rendered.
        /// </summary>
        private static void DrawBuilding3D(
            SKCanvas canvas, BuildingFootprint building,
            SysVec3 cameraPos,
            Matrix4x4 vp, int w, int h)
        {
            var footprint = building.Footprint;
            float height  = BuildingSeededHeight(building.WayId);
            int   n       = footprint.Count;

            // Pre-compute footprint centroid for outward-normal determination.
            float centX = 0f, centZ = 0f;
            foreach (var p in footprint) { centX += p.x; centZ += p.z; }
            centX /= n; centZ /= n;

            using var wallFill = new SKPaint
            {
                Color       = SKColor.Parse("#c8b9a8"),
                Style       = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using var wallStroke = new SKPaint
            {
                Color       = SKColor.Parse("#9e8e80"),
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                IsAntialias = true,
            };

            // Draw each wall face — only those facing the camera.
            for (int i = 0; i < n; i++)
            {
                var a = footprint[i];
                var b = footprint[(i + 1) % n];

                // Edge direction in XZ.
                float edgeDx = b.x - a.x, edgeDz = b.z - a.z;

                // One candidate outward normal: perpendicular right of (edgeDx, edgeDz).
                float nx = edgeDz, nz = -edgeDx;

                // Ensure the normal points away from the centroid (outward).
                float faceCx = (a.x + b.x) * 0.5f, faceCz = (a.z + b.z) * 0.5f;
                float outDot  = nx * (faceCx - centX) + nz * (faceCz - centZ);
                if (outDot < 0f) { nx = -nx; nz = -nz; }

                // Skip the wall if the camera is on the interior side (back-face cull).
                float camDot = nx * (cameraPos.X - faceCx) + nz * (cameraPos.Z - faceCz);
                if (camDot <= 0f) continue;

                // Project the four wall corners (bottom-left, bottom-right, top-right, top-left).
                bool ok;
                ok  = Project(a.x, a.y,          a.z, vp, w, h, out var sA0);
                ok &= Project(b.x, b.y,          b.z, vp, w, h, out var sB0);
                ok &= Project(b.x, b.y + height, b.z, vp, w, h, out var sB1);
                ok &= Project(a.x, a.y + height, a.z, vp, w, h, out var sA1);
                if (!ok) continue;

                using var wallPath = new SKPath();
                wallPath.MoveTo(sA0);
                wallPath.LineTo(sB0);
                wallPath.LineTo(sB1);
                wallPath.LineTo(sA1);
                wallPath.Close();

                canvas.DrawPath(wallPath, wallFill);
                canvas.DrawPath(wallPath, wallStroke);
            }

            // Draw the roof polygon at the top of the building.
            using var roofPath = new SKPath();
            bool started = false;
            foreach (var v in footprint)
            {
                if (!Project(v.x, v.y + height, v.z, vp, w, h, out var sp))
                { started = false; continue; }
                if (!started) { roofPath.MoveTo(sp); started = true; }
                else          { roofPath.LineTo(sp); }
            }
            if (!started) return;
            roofPath.Close();

            using var roofFill = new SKPaint
            {
                Color       = SKColor.Parse("#a09080"),
                Style       = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using var roofStroke = new SKPaint
            {
                Color       = SKColor.Parse("#9e8e80"),
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                IsAntialias = true,
            };
            canvas.DrawPath(roofPath, roofFill);
            canvas.DrawPath(roofPath, roofStroke);
        }

        /// <summary>
        /// Draws the car as a simple 3D box (4 m long × 2 m wide × 1.5 m tall)
        /// so it reads as a proper vehicle rather than a flat ground rectangle.
        /// Faces are drawn back-to-front (painter's algorithm approximation).
        /// </summary>
        private static void DrawCarMarker(
            SKCanvas canvas, float cx, float cz,
            Matrix4x4 vp, int w, int h)
        {
            const float hw        = 1f;    // half-width  (total 2 m)
            const float hl        = 2f;    // half-length (total 4 m)
            const float carHeight = 1.5f;  // roof height above ground

            // 8 corners — indices 0-3 at Y=0 (bottom), 4-7 at Y=carHeight (top).
            // 0: rear-left   1: rear-right   2: front-right   3: front-left
            // Camera is behind (cz-FollowDistance), so "rear" = cz-hl faces the camera.
            var pts = new SKPoint[8];
            var vis = new bool[8];
            vis[0] = Project(cx - hw, 0f,          cz - hl, vp, w, h, out pts[0]);
            vis[1] = Project(cx + hw, 0f,          cz - hl, vp, w, h, out pts[1]);
            vis[2] = Project(cx + hw, 0f,          cz + hl, vp, w, h, out pts[2]);
            vis[3] = Project(cx - hw, 0f,          cz + hl, vp, w, h, out pts[3]);
            vis[4] = Project(cx - hw, carHeight,   cz - hl, vp, w, h, out pts[4]);
            vis[5] = Project(cx + hw, carHeight,   cz - hl, vp, w, h, out pts[5]);
            vis[6] = Project(cx + hw, carHeight,   cz + hl, vp, w, h, out pts[6]);
            vis[7] = Project(cx - hw, carHeight,   cz + hl, vp, w, h, out pts[7]);

            // Draw faces back-to-front: front (far), sides, top, then rear (nearest camera).
            DrawBoxFace(canvas, pts, vis, 3, 2, 6, 7, new SKColor(160, 35, 35)); // front face
            DrawBoxFace(canvas, pts, vis, 3, 0, 4, 7, new SKColor(180, 40, 40)); // left  face
            DrawBoxFace(canvas, pts, vis, 1, 2, 6, 5, new SKColor(180, 40, 40)); // right face
            DrawBoxFace(canvas, pts, vis, 4, 5, 6, 7, new SKColor(220, 55, 55)); // top   face
            DrawBoxFace(canvas, pts, vis, 0, 1, 5, 4, new SKColor(200, 50, 50)); // rear  face (closest to camera)
        }

        /// <summary>
        /// Draws one quad face of a box, identified by four corner indices into
        /// <paramref name="pts"/>/<paramref name="vis"/>.  Skipped silently when any
        /// corner failed to project.
        /// </summary>
        private static void DrawBoxFace(
            SKCanvas canvas, SKPoint[] pts, bool[] vis,
            int i0, int i1, int i2, int i3, SKColor colour)
        {
            if (!vis[i0] || !vis[i1] || !vis[i2] || !vis[i3]) return;

            using var path = new SKPath();
            path.MoveTo(pts[i0]);
            path.LineTo(pts[i1]);
            path.LineTo(pts[i2]);
            path.LineTo(pts[i3]);
            path.Close();

            using var fillPaint = new SKPaint
            {
                Color       = colour,
                Style       = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            canvas.DrawPath(path, fillPaint);

            using var edgePaint = new SKPaint
            {
                Color       = SKColors.White,
                Style       = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntialias = true,
            };
            canvas.DrawPath(path, edgePaint);
        }

        private static void DrawOverlay(
            SKCanvas canvas, int w, int h,
            float cx, float cz, int roadCount, int buildingCount)
        {
            string[] lines =
            {
                "Chase Cam View",
                $"Car position: ({cx:F0}, {cz:F0})",
                $"Roads: {roadCount}   Buildings: {buildingCount}",
                $"Camera: {FollowDistance} m behind, {CameraHeight} m above",
            };

            using var font = new SKFont { Size = 14f };
            using var textPaint = new SKPaint
            {
                Color       = new SKColor(240, 240, 240),
                IsAntialias = true,
            };
            using var bgPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 150),
            };

            const float margin = 10f;
            const float lineH  = 20f;
            float boxW = 310f;
            float boxH = lines.Length * lineH + margin * 2f;

            canvas.DrawRect(margin, margin, boxW, boxH, bgPaint);

            for (int i = 0; i < lines.Length; i++)
                canvas.DrawText(
                    lines[i],
                    margin * 2f,
                    margin + (i + 1) * lineH,
                    SKTextAlign.Left,
                    font,
                    textPaint);
        }
    }
}
