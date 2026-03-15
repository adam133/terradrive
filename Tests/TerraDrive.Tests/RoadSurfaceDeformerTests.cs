using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TerraDrive.DataInversion;
using TerraDrive.Procedural;

namespace TerraDrive.Tests
{
    [TestFixture]
    public class RoadSurfaceDeformerTests
    {
        // Simple straight road: 5 points, 10 m apart (40 m total).
        private static readonly List<Vector3> StraightRoad = new List<Vector3>
        {
            new Vector3(0f, 0f,  0f),
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, 20f),
            new Vector3(0f, 0f, 30f),
            new Vector3(0f, 0f, 40f),
        };

        // ── GetRoughnessAmplitude ─────────────────────────────────────────────

        [Test]
        public void GetRoughnessAmplitude_AllRoadTypesReturnPositiveValue()
        {
            foreach (RoadType rt in System.Enum.GetValues(typeof(RoadType)))
                Assert.That(RoadSurfaceDeformer.GetRoughnessAmplitude(rt), Is.GreaterThan(0f),
                    $"Roughness amplitude for {rt} must be positive.");
        }

        [Test]
        public void GetRoughnessAmplitude_MajorRoadsSmoother_Motorway_LessThan_Residential()
        {
            float motorway    = RoadSurfaceDeformer.GetRoughnessAmplitude(RoadType.Motorway);
            float residential = RoadSurfaceDeformer.GetRoughnessAmplitude(RoadType.Residential);

            Assert.That(motorway, Is.LessThan(residential),
                "Motorways must have a smaller roughness amplitude than residential streets.");
        }

        [Test]
        public void GetRoughnessAmplitude_MajorRoadsSmoother_Trunk_LessThan_Tertiary()
        {
            float trunk    = RoadSurfaceDeformer.GetRoughnessAmplitude(RoadType.Trunk);
            float tertiary = RoadSurfaceDeformer.GetRoughnessAmplitude(RoadType.Tertiary);

            Assert.That(trunk, Is.LessThan(tertiary),
                "Trunk roads must be smoother than tertiary roads.");
        }

        [Test]
        public void GetRoughnessAmplitude_Dirt_IsRoughest()
        {
            float dirt = RoadSurfaceDeformer.GetRoughnessAmplitude(RoadType.Dirt);

            foreach (RoadType rt in System.Enum.GetValues(typeof(RoadType)))
            {
                if (rt == RoadType.Dirt) continue;
                Assert.That(dirt, Is.GreaterThanOrEqualTo(
                    RoadSurfaceDeformer.GetRoughnessAmplitude(rt)),
                    $"Dirt track amplitude must be >= amplitude for {rt}.");
            }
        }

        [Test]
        public void GetRoughnessAmplitude_Motorway_IsSmallest()
        {
            float motorway = RoadSurfaceDeformer.GetRoughnessAmplitude(RoadType.Motorway);

            foreach (RoadType rt in System.Enum.GetValues(typeof(RoadType)))
            {
                if (rt == RoadType.Motorway) continue;
                Assert.That(motorway, Is.LessThanOrEqualTo(
                    RoadSurfaceDeformer.GetRoughnessAmplitude(rt)),
                    $"Motorway amplitude must be <= amplitude for {rt}.");
            }
        }

        // ── Deform: edge cases ────────────────────────────────────────────────

        [Test]
        public void Deform_NullInput_ReturnsEmptyList()
        {
            List<Vector3> result = RoadSurfaceDeformer.Deform(null!, RoadType.Residential, seed: 42);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Deform_SinglePoint_ReturnsEmptyList()
        {
            var single = new[] { new Vector3(0f, 0f, 0f) };
            List<Vector3> result = RoadSurfaceDeformer.Deform(single, RoadType.Residential, seed: 42);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Deform_PreservesPointCount()
        {
            List<Vector3> result = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed: 1);

            Assert.That(result.Count, Is.EqualTo(StraightRoad.Count));
        }

        // ── Deform: X and Z are unchanged ────────────────────────────────────

        [Test]
        public void Deform_XCoordinate_IsUnchanged()
        {
            List<Vector3> result = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed: 7);

            for (int i = 0; i < StraightRoad.Count; i++)
                Assert.That(result[i].x, Is.EqualTo(StraightRoad[i].x).Within(1e-6f),
                    $"X must not change at index {i}.");
        }

        [Test]
        public void Deform_ZCoordinate_IsUnchanged()
        {
            List<Vector3> result = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed: 7);

            for (int i = 0; i < StraightRoad.Count; i++)
                Assert.That(result[i].z, Is.EqualTo(StraightRoad[i].z).Within(1e-6f),
                    $"Z must not change at index {i}.");
        }

        // ── Deform: Y perturbations ───────────────────────────────────────────

        [Test]
        public void Deform_YCoordinate_ChangesForAtLeastOnePoint()
        {
            // The first spline point always stays at Y=0 (distance=0 → noise input = 0
            // which can produce a specific value), but inner points should be perturbed.
            List<Vector3> result = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed: 5);

            bool anyChanged = false;
            for (int i = 0; i < StraightRoad.Count; i++)
            {
                if (System.Math.Abs(result[i].y - StraightRoad[i].y) > 1e-6f)
                {
                    anyChanged = true;
                    break;
                }
            }

            Assert.That(anyChanged, Is.True, "At least one Y coordinate must be perturbed.");
        }

        [Test]
        public void Deform_YDeviation_WithinAmplitudeBounds()
        {
            const RoadType roadType = RoadType.Residential;
            float amplitude = RoadSurfaceDeformer.GetRoughnessAmplitude(roadType);

            List<Vector3> result = RoadSurfaceDeformer.Deform(StraightRoad, roadType, seed: 99);

            foreach (var (original, deformed) in StraightRoad.Zip(result, (a, b) => (a, b)))
            {
                float dy = System.Math.Abs(deformed.y - original.y);
                Assert.That(dy, Is.LessThanOrEqualTo(amplitude + 1e-5f),
                    "Y deviation must not exceed the roughness amplitude.");
            }
        }

        [Test]
        public void Deform_MotorwayDeviation_LessThan_ResidentialDeviation()
        {
            const int seed = 12345;

            List<Vector3> motorwayResult    = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Motorway,    seed);
            List<Vector3> residentialResult = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed);

            float motorwayMax    = motorwayResult.Zip(StraightRoad,    (d, o) => System.Math.Abs(d.y - o.y)).Max();
            float residentialMax = residentialResult.Zip(StraightRoad, (d, o) => System.Math.Abs(d.y - o.y)).Max();

            Assert.That(motorwayMax, Is.LessThan(residentialMax),
                "Motorway surface must be flatter than a residential street.");
        }

        // ── Deform: determinism ───────────────────────────────────────────────

        [Test]
        public void Deform_SameSeed_ProducesSameResult()
        {
            const int seed = 42;

            List<Vector3> first  = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Tertiary, seed);
            List<Vector3> second = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Tertiary, seed);

            for (int i = 0; i < first.Count; i++)
                Assert.That(first[i].y, Is.EqualTo(second[i].y).Within(1e-6f),
                    $"Y at index {i} must be identical for the same seed.");
        }

        [Test]
        public void Deform_DifferentSeeds_ProduceDifferentResults()
        {
            List<Vector3> r1 = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed: 1);
            List<Vector3> r2 = RoadSurfaceDeformer.Deform(StraightRoad, RoadType.Residential, seed: 2);

            // At least one Y value should differ between the two seeds.
            bool anyDifferent = r1.Zip(r2, (a, b) => System.Math.Abs(a.y - b.y))
                                   .Any(diff => diff > 1e-6f);

            Assert.That(anyDifferent, Is.True,
                "Different seeds must produce different Y perturbations.");
        }

        // ── Integration: ExtrudeWithDetails with surfaceSeed ──────────────────

        [Test]
        public void ExtrudeWithDetails_WithSeed_RoadMesh_HasDeformedY()
        {
            // Without seed: road vertices at Y = 0 (flat spline).
            // With seed:    some vertices should deviate from Y = 0.
            RoadMeshResult flat     = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Residential);
            RoadMeshResult deformed = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Residential, surfaceSeed: 100);

            bool anyDeformed = deformed.RoadMesh.Vertices
                .Zip(flat.RoadMesh.Vertices, (d, f) => System.Math.Abs(d.y - f.y))
                .Any(diff => diff > 1e-6f);

            Assert.That(anyDeformed, Is.True,
                "At least one road vertex Y must differ when surfaceSeed is provided.");
        }

        [Test]
        public void ExtrudeWithDetails_NoSeed_RoadMesh_IsFlat()
        {
            // Without a seed the road surface must remain flat.
            // Y is offset by TerrainClearance (applied to prevent z-fighting with terrain).
            RoadMeshResult result = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Residential);

            foreach (var v in result.RoadMesh.Vertices)
                Assert.That(v.y, Is.EqualTo(RoadMeshExtruder.TerrainClearance).Within(1e-5f),
                    "Road vertex Y must equal TerrainClearance when no surfaceSeed is provided.");
        }

        [Test]
        public void ExtrudeWithDetails_WithSeed_SameSeed_ProducesSameVertices()
        {
            const int seed = 77;

            RoadMeshResult r1 = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Tertiary, surfaceSeed: seed);
            RoadMeshResult r2 = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Tertiary, surfaceSeed: seed);

            for (int i = 0; i < r1.RoadMesh.Vertices.Length; i++)
                Assert.That(r1.RoadMesh.Vertices[i].y,
                    Is.EqualTo(r2.RoadMesh.Vertices[i].y).Within(1e-6f),
                    $"Vertex {i} Y must be identical for the same seed.");
        }

        [Test]
        public void ExtrudeWithDetails_WithSeed_MotorwayFlatter_Than_Residential()
        {
            const int seed = 999;

            RoadMeshResult motorway    = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Motorway,    surfaceSeed: seed);
            RoadMeshResult residential = RoadMeshExtruder.ExtrudeWithDetails(StraightRoad, RoadType.Residential, surfaceSeed: seed);

            float motorwayMaxDy    = motorway.RoadMesh.Vertices.Select(v => System.Math.Abs(v.y)).Max();
            float residentialMaxDy = residential.RoadMesh.Vertices.Select(v => System.Math.Abs(v.y)).Max();

            Assert.That(motorwayMaxDy, Is.LessThan(residentialMaxDy),
                "Motorway road surface must be flatter than residential when the same seed is used.");
        }
    }
}
