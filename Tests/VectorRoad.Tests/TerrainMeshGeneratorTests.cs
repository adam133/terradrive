using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.Core;
using VectorRoad.Terrain;

namespace VectorRoad.Tests
{
    /// <summary>
    /// Unit tests for <see cref="ElevationGrid"/>, <see cref="TerrainMeshGenerator"/>,
    /// and <see cref="TerrainMeshResult"/>.
    /// </summary>
    [TestFixture]
    public class TerrainMeshGeneratorTests
    {
        [SetUp]
        public void SetUp()
        {
            // TerrainMeshGenerator calls CoordinateConverter which has static state.
            CoordinateConverter.ResetWorldOrigin();
        }

        // ── ElevationGrid construction ─────────────────────────────────────────

        [Test]
        public void ElevationGrid_Constructor_StoresProperties()
        {
            var elevations = new double[3, 4];
            var grid = new ElevationGrid(10.0, 20.0, 30.0, 40.0, elevations);

            Assert.That(grid.MinLat, Is.EqualTo(10.0));
            Assert.That(grid.MaxLat, Is.EqualTo(20.0));
            Assert.That(grid.MinLon, Is.EqualTo(30.0));
            Assert.That(grid.MaxLon, Is.EqualTo(40.0));
            Assert.That(grid.Rows,   Is.EqualTo(3));
            Assert.That(grid.Cols,   Is.EqualTo(4));
        }

        [Test]
        public void ElevationGrid_Indexer_ReturnsCorrectValue()
        {
            var elevations = new double[2, 2] { { 100.0, 200.0 }, { 300.0, 400.0 } };
            var grid = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            Assert.That(grid[0, 0], Is.EqualTo(100.0));
            Assert.That(grid[0, 1], Is.EqualTo(200.0));
            Assert.That(grid[1, 0], Is.EqualTo(300.0));
            Assert.That(grid[1, 1], Is.EqualTo(400.0));
        }

        [Test]
        public void ElevationGrid_LatAtRow_ReturnsInterpolatedLatitude()
        {
            var grid = new ElevationGrid(10.0, 20.0, 0.0, 1.0, new double[3, 2]);

            Assert.That(grid.LatAtRow(0), Is.EqualTo(10.0).Within(1e-10));
            Assert.That(grid.LatAtRow(2), Is.EqualTo(20.0).Within(1e-10));
            Assert.That(grid.LatAtRow(1), Is.EqualTo(15.0).Within(1e-10));
        }

        [Test]
        public void ElevationGrid_LonAtCol_ReturnsInterpolatedLongitude()
        {
            var grid = new ElevationGrid(0.0, 1.0, -10.0, 10.0, new double[2, 5]);

            Assert.That(grid.LonAtCol(0), Is.EqualTo(-10.0).Within(1e-10));
            Assert.That(grid.LonAtCol(4), Is.EqualTo( 10.0).Within(1e-10));
            Assert.That(grid.LonAtCol(2), Is.EqualTo(  0.0).Within(1e-10));
        }

        [Test]
        public void ElevationGrid_Constructor_NullElevations_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ElevationGrid(0.0, 1.0, 0.0, 1.0, null!));
        }

        [Test]
        public void ElevationGrid_Constructor_MinLatNotLessThanMaxLat_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new ElevationGrid(5.0, 5.0, 0.0, 1.0, new double[2, 2]));
        }

        [Test]
        public void ElevationGrid_Constructor_MinLonNotLessThanMaxLon_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new ElevationGrid(0.0, 1.0, 5.0, 5.0, new double[2, 2]));
        }

        [Test]
        public void ElevationGrid_Constructor_SingleRow_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new ElevationGrid(0.0, 1.0, 0.0, 1.0, new double[1, 2]));
        }

        [Test]
        public void ElevationGrid_Constructor_SingleCol_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => new ElevationGrid(0.0, 1.0, 0.0, 1.0, new double[2, 1]));
        }

        // ── ElevationGrid.SampleAsync ──────────────────────────────────────────

        [Test]
        public async Task ElevationGrid_SampleAsync_PopulatesGridFromElevationSource()
        {
            // Source returns the row index as elevation (0, 0, 0, 1, 1, 1, 2, 2, 2)
            // for a 3×3 grid (rows × cols)
            int callIndex = 0;
            var source = new LambdaElevationSource(locations =>
            {
                int n = locations.Count;
                var result = new double[n];
                for (int i = 0; i < n; i++)
                    result[i] = callIndex++ * 10.0;
                return Task.FromResult<IReadOnlyList<double>>(result);
            });

            ElevationGrid grid = await ElevationGrid.SampleAsync(
                0.0, 1.0, 0.0, 1.0, rows: 2, cols: 2, source);

            Assert.That(grid.Rows, Is.EqualTo(2));
            Assert.That(grid.Cols, Is.EqualTo(2));
            // Values should be filled in row-major order: 0, 10, 20, 30
            Assert.That(grid[0, 0], Is.EqualTo(0.0));
            Assert.That(grid[0, 1], Is.EqualTo(10.0));
            Assert.That(grid[1, 0], Is.EqualTo(20.0));
            Assert.That(grid[1, 1], Is.EqualTo(30.0));
        }

        [Test]
        public async Task ElevationGrid_SampleAsync_SendsAllLocationsInOneCall()
        {
            int callCount  = 0;
            int batchSize  = 0;

            var source = new LambdaElevationSource(locations =>
            {
                callCount++;
                batchSize = locations.Count;
                return Task.FromResult<IReadOnlyList<double>>(new double[locations.Count]);
            });

            await ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, rows: 3, cols: 4, source);

            Assert.That(callCount, Is.EqualTo(1), "All points should be fetched in a single batch.");
            Assert.That(batchSize, Is.EqualTo(12), "3 rows × 4 cols = 12 points.");
        }

        [Test]
        public async Task ElevationGrid_SampleAsync_LocationsAreInRowMajorOrder()
        {
            var captured = new List<(double lat, double lon)>();
            var source = new LambdaElevationSource(locations =>
            {
                captured.AddRange(locations);
                return Task.FromResult<IReadOnlyList<double>>(new double[locations.Count]);
            });

            await ElevationGrid.SampleAsync(10.0, 20.0, 30.0, 40.0, rows: 2, cols: 3, source);

            // Row 0 (lat=10): lon=30, 35, 40  then Row 1 (lat=20): lon=30, 35, 40
            Assert.That(captured[0].lat, Is.EqualTo(10.0).Within(1e-9));
            Assert.That(captured[0].lon, Is.EqualTo(30.0).Within(1e-9));
            Assert.That(captured[2].lat, Is.EqualTo(10.0).Within(1e-9));
            Assert.That(captured[2].lon, Is.EqualTo(40.0).Within(1e-9));
            Assert.That(captured[3].lat, Is.EqualTo(20.0).Within(1e-9));
            Assert.That(captured[3].lon, Is.EqualTo(30.0).Within(1e-9));
        }

        [Test]
        public void ElevationGrid_SampleAsync_NullSource_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                () => ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, 2, 2, null!));
        }

        [Test]
        public void ElevationGrid_SampleAsync_TooFewRows_ThrowsArgumentOutOfRangeException()
        {
            var source = new LambdaElevationSource(_ =>
                Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()));

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, rows: 1, cols: 2, source));
        }

        [Test]
        public void ElevationGrid_SampleAsync_TooFewCols_ThrowsArgumentOutOfRangeException()
        {
            var source = new LambdaElevationSource(_ =>
                Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()));

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, rows: 2, cols: 1, source));
        }

        [Test]
        public void ElevationGrid_SampleAsync_CancellationRequested_PropagatesCancellation()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var source = new LambdaElevationSource(_ => throw new OperationCanceledException());

            Assert.ThrowsAsync<OperationCanceledException>(
                () => ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, 2, 2, source, cancellationToken: cts.Token));
        }

        [Test]
        public void ElevationGrid_SampleAsync_ZeroBatchSize_ThrowsArgumentOutOfRangeException()
        {
            var source = new LambdaElevationSource(_ =>
                Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()));

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, 2, 2, source, batchSize: 0));
        }

        [Test]
        public async Task ElevationGrid_SampleAsync_BatchingWhenPointsExceedBatchSize_SplitsRequests()
        {
            // 3×4 = 12 points, batch size 5 → 3 calls (5+5+2)
            var batchSizes = new List<int>();
            var source = new LambdaElevationSource(locations =>
            {
                batchSizes.Add(locations.Count);
                return Task.FromResult<IReadOnlyList<double>>(new double[locations.Count]);
            });

            await ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, rows: 3, cols: 4, source, batchSize: 5);

            Assert.That(batchSizes.Count, Is.EqualTo(3), "12 points ÷ batch size 5 → 3 calls.");
            Assert.That(batchSizes[0], Is.EqualTo(5));
            Assert.That(batchSizes[1], Is.EqualTo(5));
            Assert.That(batchSizes[2], Is.EqualTo(2));
        }

        [Test]
        public async Task ElevationGrid_SampleAsync_BatchedResult_MatchesUnbatchedResult()
        {
            // Same elevation values regardless of how they are split across batches.
            double[] expectedValues = { 10, 20, 30, 40, 50, 60 }; // 2×3 grid
            int callIndex = 0;

            var source = new LambdaElevationSource(locations =>
            {
                var result = new double[locations.Count];
                for (int i = 0; i < locations.Count; i++)
                    result[i] = expectedValues[callIndex++];
                return Task.FromResult<IReadOnlyList<double>>(result);
            });

            ElevationGrid grid = await ElevationGrid.SampleAsync(
                0.0, 1.0, 0.0, 1.0, rows: 2, cols: 3, source, batchSize: 4);

            // Verify all six values were stored in the correct row-major positions.
            Assert.That(grid[0, 0], Is.EqualTo(10));
            Assert.That(grid[0, 1], Is.EqualTo(20));
            Assert.That(grid[0, 2], Is.EqualTo(30));
            Assert.That(grid[1, 0], Is.EqualTo(40));
            Assert.That(grid[1, 1], Is.EqualTo(50));
            Assert.That(grid[1, 2], Is.EqualTo(60));
        }

        [Test]
        public async Task ElevationGrid_SampleAsync_BatchSizeExactlyMatchesTotal_UsesSingleCall()
        {
            // batchSize == rows*cols → should still use exactly one call.
            int callCount = 0;
            var source = new LambdaElevationSource(locations =>
            {
                callCount++;
                return Task.FromResult<IReadOnlyList<double>>(new double[locations.Count]);
            });

            await ElevationGrid.SampleAsync(0.0, 1.0, 0.0, 1.0, rows: 2, cols: 3, source, batchSize: 6);

            Assert.That(callCount, Is.EqualTo(1));
        }

        // ── TerrainMeshGenerator.Generate — vertex counts ─────────────────────

        [Test]
        public void Generate_2x2Grid_ProducesFourVertices()
        {
            var grid   = FlatGrid(2, 2, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            Assert.That(result.Vertices.Length, Is.EqualTo(4));
        }

        [Test]
        public void Generate_3x4Grid_ProducesCorrectVertexCount()
        {
            var grid   = FlatGrid(3, 4, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            Assert.That(result.Vertices.Length, Is.EqualTo(12), "3 × 4 = 12 vertices");
        }

        [Test]
        public void Generate_VertexCount_MatchesRowsTimesCols()
        {
            for (int rows = 2; rows <= 5; rows++)
            for (int cols = 2; cols <= 5; cols++)
            {
                var grid   = FlatGrid(rows, cols, elev: 0.0);
                var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

                Assert.That(result.Vertices.Length, Is.EqualTo(rows * cols),
                    $"rows={rows}, cols={cols}");
            }
        }

        // ── TerrainMeshGenerator.Generate — triangle counts ───────────────────

        [Test]
        public void Generate_2x2Grid_ProducesSixTriangleIndices()
        {
            // 1 quad → 2 triangles → 6 indices
            var grid   = FlatGrid(2, 2, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            Assert.That(result.Triangles.Length, Is.EqualTo(6));
        }

        [Test]
        public void Generate_TriangleCount_MatchesExpectedFormula()
        {
            for (int rows = 2; rows <= 5; rows++)
            for (int cols = 2; cols <= 5; cols++)
            {
                var grid   = FlatGrid(rows, cols, elev: 0.0);
                var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

                int expected = (rows - 1) * (cols - 1) * 6;
                Assert.That(result.Triangles.Length, Is.EqualTo(expected),
                    $"rows={rows}, cols={cols}");
            }
        }

        [Test]
        public void Generate_AllTriangleIndices_AreWithinVertexBounds()
        {
            var grid   = FlatGrid(4, 4, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            foreach (int idx in result.Triangles)
            {
                Assert.That(idx, Is.GreaterThanOrEqualTo(0));
                Assert.That(idx, Is.LessThan(result.Vertices.Length));
            }
        }

        // ── TerrainMeshGenerator.Generate — UV coordinates ───────────────────

        [Test]
        public void Generate_UVCount_MatchesVertexCount()
        {
            var grid   = FlatGrid(3, 3, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            Assert.That(result.UVs.Length, Is.EqualTo(result.Vertices.Length));
        }

        [Test]
        public void Generate_SouthWestCornerUV_IsZeroZero()
        {
            var grid   = FlatGrid(2, 2, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            // Row 0, Col 0 → vertex index 0 → UV (0, 0)
            Vector2 uv = result.UVs[0];
            Assert.That(uv.x, Is.EqualTo(0f).Within(1e-5f), "SW corner U should be 0");
            Assert.That(uv.y, Is.EqualTo(0f).Within(1e-5f), "SW corner V should be 0");
        }

        [Test]
        public void Generate_NorthEastCornerUV_IsOneOne()
        {
            var grid   = FlatGrid(2, 2, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            // Row 1, Col 1 → vertex index 3 → UV (1, 1)
            Vector2 uv = result.UVs[3];
            Assert.That(uv.x, Is.EqualTo(1f).Within(1e-5f), "NE corner U should be 1");
            Assert.That(uv.y, Is.EqualTo(1f).Within(1e-5f), "NE corner V should be 1");
        }

        [Test]
        public void Generate_AllUVValues_AreInZeroOneRange()
        {
            var grid   = FlatGrid(4, 5, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            foreach (Vector2 uv in result.UVs)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f), "U must be in [0,1]");
                Assert.That(uv.y, Is.InRange(0f, 1f), "V must be in [0,1]");
            }
        }

        // ── TerrainMeshGenerator.Generate — Y (elevation) axis ───────────────

        [Test]
        public void Generate_FlatGrid_AllVerticesHaveSameY()
        {
            const double elev = 42.5;
            var grid   = FlatGrid(3, 3, elev);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            foreach (Vector3 v in result.Vertices)
                Assert.That(v.y, Is.EqualTo((float)elev).Within(1e-3f),
                    "All vertices should have the flat elevation.");
        }

        [Test]
        public void Generate_ZeroElevation_AllVerticesHaveYZero()
        {
            var grid   = FlatGrid(3, 3, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            foreach (Vector3 v in result.Vertices)
                Assert.That(v.y, Is.EqualTo(0f), "Zero elevation → Y = 0.");
        }

        [Test]
        public void Generate_NegativeElevation_VerticesHaveNegativeY()
        {
            const double elev = -50.0;
            var grid   = FlatGrid(2, 2, elev);
            var result = TerrainMeshGenerator.Generate(grid, 0.0, 0.0);

            foreach (Vector3 v in result.Vertices)
                Assert.That(v.y, Is.EqualTo((float)elev).Within(1e-3f),
                    "Negative elevation (e.g. Death Valley) should produce negative Y.");
        }

        [Test]
        public void Generate_VaryingElevations_EachVertexHasCorrectY()
        {
            // 2×2 grid with known per-cell elevations
            var elevations = new double[2, 2] { { 10.0, 20.0 }, { 30.0, 40.0 } };
            var grid = new ElevationGrid(51.0, 52.0, -1.0, 0.0, elevations);

            var result = TerrainMeshGenerator.Generate(grid, 51.0, -1.0);

            // Row-major order: [0,0]=10, [0,1]=20, [1,0]=30, [1,1]=40
            Assert.That(result.Vertices[0].y, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(result.Vertices[1].y, Is.EqualTo(20f).Within(1e-3f));
            Assert.That(result.Vertices[2].y, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(result.Vertices[3].y, Is.EqualTo(40f).Within(1e-3f));
        }

        // ── TerrainMeshGenerator.Generate — XZ (horizontal) axis ─────────────

        [Test]
        public void Generate_OriginVertex_HasZeroXZ()
        {
            var grid   = FlatGrid(2, 2, elev: 0.0);
            var result = TerrainMeshGenerator.Generate(grid, grid.MinLat, grid.MinLon);

            // The SW corner should project to X=0, Z=0 when origin matches MinLat/MinLon.
            Vector3 sw = result.Vertices[0];
            Assert.That(sw.x, Is.EqualTo(0f).Within(1.0f),
                "Origin vertex X should be ~0.");
            Assert.That(sw.z, Is.EqualTo(0f).Within(1.0f),
                "Origin vertex Z should be ~0.");
        }

        [Test]
        public void Generate_NorthOfOrigin_HasPositiveZ()
        {
            // Single strip: 2 rows (south and north), 2 cols
            var elevations = new double[2, 2];
            var grid = new ElevationGrid(51.0, 52.0, -1.0, 0.0, elevations);
            var result = TerrainMeshGenerator.Generate(grid, 51.0, -1.0);

            // Row 0 = south (Z≈0), Row 1 = north (Z > 0)
            Assert.That(result.Vertices[2].z, Is.GreaterThan(0f),
                "Northern row vertices should have positive Z.");
        }

        [Test]
        public void Generate_EastOfOrigin_HasPositiveX()
        {
            // Single strip: 2 rows, 2 cols (west and east)
            var elevations = new double[2, 2];
            var grid = new ElevationGrid(51.0, 52.0, -1.0, 0.0, elevations);
            var result = TerrainMeshGenerator.Generate(grid, 51.0, -1.0);

            // Col 0 = west (X≈0), Col 1 = east (X > 0)
            Assert.That(result.Vertices[1].x, Is.GreaterThan(0f),
                "Eastern column vertices should have positive X.");
        }

        // ── TerrainMeshGenerator.Generate — null guard ───────────────────────

        [Test]
        public void Generate_NullGrid_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => TerrainMeshGenerator.Generate(null!, 0.0, 0.0));
        }

        // ── TerrainMeshResult properties ──────────────────────────────────────

        [Test]
        public void TerrainMeshResult_StoresAllArrays()
        {
            var verts = new Vector3[] { new Vector3(1, 2, 3) };
            var tris  = new int[]     { 0 };
            var uvs   = new Vector2[] { new Vector2(0, 0) };

            var result = new TerrainMeshResult(verts, tris, uvs);

            Assert.That(result.Vertices,  Is.SameAs(verts));
            Assert.That(result.Triangles, Is.SameAs(tris));
            Assert.That(result.UVs,       Is.SameAs(uvs));
        }

        // ── ElevationGrid.SampleElevation — bilinear interpolation ────────────

        [Test]
        public void SampleElevation_AtGridCellCenter_ReturnsExactValue()
        {
            // 2×2 grid: SW=10, SE=20, NW=30, NE=40
            var elevations = new double[2, 2] { { 10.0, 20.0 }, { 30.0, 40.0 } };
            var grid = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            // Exactly at each corner.
            Assert.That(grid.SampleElevation(0.0, 0.0), Is.EqualTo(10.0).Within(1e-10));
            Assert.That(grid.SampleElevation(0.0, 1.0), Is.EqualTo(20.0).Within(1e-10));
            Assert.That(grid.SampleElevation(1.0, 0.0), Is.EqualTo(30.0).Within(1e-10));
            Assert.That(grid.SampleElevation(1.0, 1.0), Is.EqualTo(40.0).Within(1e-10));
        }

        [Test]
        public void SampleElevation_AtCentre_ReturnsMeanOfFourCorners()
        {
            // 2×2 grid: four distinct elevations.  The centre (lat=0.5, lon=0.5) should
            // bilinearly interpolate to the mean of all four corners.
            var elevations = new double[2, 2] { { 10.0, 30.0 }, { 50.0, 70.0 } };
            var grid = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            double expected = (10.0 + 30.0 + 50.0 + 70.0) / 4.0;
            Assert.That(grid.SampleElevation(0.5, 0.5), Is.EqualTo(expected).Within(1e-10));
        }

        [Test]
        public void SampleElevation_MidpointAlongLatAxis_InterpolatesRows()
        {
            // Uniform column; varies only in the row direction.
            var elevations = new double[2, 2] { { 0.0, 0.0 }, { 100.0, 100.0 } };
            var grid = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            // lat=0.25 → 25 % of the way from row 0 to row 1 → elevation = 25
            Assert.That(grid.SampleElevation(0.25, 0.5), Is.EqualTo(25.0).Within(1e-10));
        }

        [Test]
        public void SampleElevation_MidpointAlongLonAxis_InterpolateCols()
        {
            // Uniform row; varies only in the column direction.
            var elevations = new double[2, 2] { { 0.0, 100.0 }, { 0.0, 100.0 } };
            var grid = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            // lon=0.6 → 60 % of the way from col 0 to col 1 → elevation = 60
            Assert.That(grid.SampleElevation(0.5, 0.6), Is.EqualTo(60.0).Within(1e-10));
        }

        [Test]
        public void SampleElevation_OutsideBoundsLow_ClampsToEdge()
        {
            var elevations = new double[2, 2] { { 5.0, 5.0 }, { 5.0, 5.0 } };
            var grid = new ElevationGrid(10.0, 20.0, 30.0, 40.0, elevations);

            // Latitude below MinLat should clamp to the southern row.
            Assert.That(grid.SampleElevation(0.0, 35.0), Is.EqualTo(5.0).Within(1e-10));
        }

        [Test]
        public void SampleElevation_OutsideBoundsHigh_ClampsToEdge()
        {
            var elevations = new double[2, 2] { { 5.0, 5.0 }, { 5.0, 5.0 } };
            var grid = new ElevationGrid(10.0, 20.0, 30.0, 40.0, elevations);

            // Latitude above MaxLat should clamp to the northern row.
            Assert.That(grid.SampleElevation(99.0, 35.0), Is.EqualTo(5.0).Within(1e-10));
        }

        [Test]
        public void SampleElevation_FlatGrid_AlwaysReturnsSameElevation()
        {
            const double elev = 42.0;
            var elevations = new double[3, 4];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                    elevations[r, c] = elev;

            var grid = new ElevationGrid(10.0, 20.0, 30.0, 40.0, elevations);

            Assert.That(grid.SampleElevation(10.0, 30.0), Is.EqualTo(elev).Within(1e-10));
            Assert.That(grid.SampleElevation(15.0, 35.0), Is.EqualTo(elev).Within(1e-10));
            Assert.That(grid.SampleElevation(20.0, 40.0), Is.EqualTo(elev).Within(1e-10));
            Assert.That(grid.SampleElevation(12.5, 32.5), Is.EqualTo(elev).Within(1e-10));
        }

        // ── ElevationGrid as IElevationSource ─────────────────────────────────

        [Test]
        public async Task ElevationGrid_AsIElevationSource_ReturnsCorrectElevations()
        {
            // 2×2 grid: SW=0, SE=100, NW=200, NE=300
            var elevations = new double[2, 2] { { 0.0, 100.0 }, { 200.0, 300.0 } };
            var grid = new ElevationGrid(0.0, 2.0, 0.0, 2.0, elevations);

            IElevationSource source = grid;
            IReadOnlyList<double> results = await source.FetchElevationsAsync(
                new[] { (0.0, 0.0), (2.0, 2.0) });

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0], Is.EqualTo(0.0).Within(1e-10),   "SW corner should be 0");
            Assert.That(results[1], Is.EqualTo(300.0).Within(1e-10), "NE corner should be 300");
        }

        [Test]
        public async Task ElevationGrid_AsIElevationSource_EmptyLocations_ReturnsEmpty()
        {
            var elevations = new double[2, 2];
            var grid       = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            IElevationSource source  = grid;
            IReadOnlyList<double> results =
                await source.FetchElevationsAsync(Array.Empty<(double, double)>());

            Assert.That(results.Count, Is.EqualTo(0));
        }

        [Test]
        public void ElevationGrid_AsIElevationSource_NullLocations_ThrowsArgumentNullException()
        {
            var elevations = new double[2, 2];
            var grid       = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            IElevationSource source = grid;
            Assert.ThrowsAsync<ArgumentNullException>(
                () => source.FetchElevationsAsync(null!));
        }

        [Test]
        public void ElevationGrid_AsIElevationSource_CancellationRequested_ThrowsOperationCanceledException()
        {
            var elevations = new double[2, 2] { { 1.0, 2.0 }, { 3.0, 4.0 } };
            var grid       = new ElevationGrid(0.0, 1.0, 0.0, 1.0, elevations);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            IElevationSource source = grid;
            Assert.ThrowsAsync<OperationCanceledException>(
                () => source.FetchElevationsAsync(
                    new[] { (0.5, 0.5) }, cts.Token));
        }

        [Test]
        public async Task ElevationGrid_AsIElevationSource_CanBeUsedWithSampleAsync()
        {
            // Build a source grid with known elevations.
            var sourceElevations = new double[3, 3]
            {
                {  0.0,  10.0,  20.0 },
                { 30.0,  40.0,  50.0 },
                { 60.0,  70.0,  80.0 },
            };
            var sourceGrid = new ElevationGrid(0.0, 2.0, 0.0, 2.0, sourceElevations);

            // Resample the same extent at 2×2 using the source grid as the IElevationSource.
            ElevationGrid resampledGrid = await ElevationGrid.SampleAsync(
                0.0, 2.0, 0.0, 2.0, rows: 2, cols: 2, sourceGrid);

            // Corners of the resampled grid should match the corners of the source grid.
            Assert.That(resampledGrid[0, 0], Is.EqualTo(0.0).Within(1e-10),  "SW corner");
            Assert.That(resampledGrid[0, 1], Is.EqualTo(20.0).Within(1e-10), "SE corner");
            Assert.That(resampledGrid[1, 0], Is.EqualTo(60.0).Within(1e-10), "NW corner");
            Assert.That(resampledGrid[1, 1], Is.EqualTo(80.0).Within(1e-10), "NE corner");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Creates a flat elevation grid (all cells at the same elevation).</summary>
        private static ElevationGrid FlatGrid(int rows, int cols, double elev)
        {
            var data = new double[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    data[r, c] = elev;

            return new ElevationGrid(51.0, 52.0, -1.0, 0.0, data);
        }

        private sealed class LambdaElevationSource : IElevationSource
        {
            private readonly Func<IReadOnlyList<(double lat, double lon)>, Task<IReadOnlyList<double>>> _handler;

            public LambdaElevationSource(
                Func<IReadOnlyList<(double lat, double lon)>, Task<IReadOnlyList<double>>> handler)
                => _handler = handler;

            public Task<IReadOnlyList<double>> FetchElevationsAsync(
                IReadOnlyList<(double lat, double lon)> locations,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _handler(locations);
            }
        }
    }
}
