using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VectorRoad.Procedural;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class BridgeElevatorTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a straight spline of <paramref name="count"/> points running along the
        /// Z axis, all at Y = 0, spaced 1 m apart.
        /// </summary>
        private static List<Vector3> StraightSpline(int count)
        {
            var pts = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
                pts.Add(new Vector3(0f, 0f, i));
            return pts;
        }

        // ── Null / empty guard ────────────────────────────────────────────────

        [Test]
        public void ApplyElevation_NullInput_ReturnsEmptyList()
        {
            List<Vector3> result = BridgeElevator.ApplyElevation(null!);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ApplyElevation_EmptyInput_ReturnsEmptyList()
        {
            List<Vector3> result = BridgeElevator.ApplyElevation(new List<Vector3>());

            Assert.That(result, Is.Empty);
        }

        // ── Output shape ──────────────────────────────────────────────────────

        [Test]
        public void ApplyElevation_ReturnsSameNumberOfPoints()
        {
            List<Vector3> spline = StraightSpline(20);
            List<Vector3> result = BridgeElevator.ApplyElevation(spline);

            Assert.That(result.Count, Is.EqualTo(spline.Count));
        }

        [Test]
        public void ApplyElevation_DoesNotModifyInputList()
        {
            List<Vector3> spline = StraightSpline(10);
            float originalY = spline[5].y;

            BridgeElevator.ApplyElevation(spline);

            Assert.That(spline[5].y, Is.EqualTo(originalY),
                "The original spline should be unchanged.");
        }

        [Test]
        public void ApplyElevation_PreservesXZCoordinates()
        {
            List<Vector3> spline = StraightSpline(10);
            List<Vector3> result = BridgeElevator.ApplyElevation(spline);

            for (int i = 0; i < spline.Count; i++)
            {
                Assert.That(result[i].x, Is.EqualTo(spline[i].x).Within(1e-5f),
                    $"X must be unchanged at index {i}.");
                Assert.That(result[i].z, Is.EqualTo(spline[i].z).Within(1e-5f),
                    $"Z must be unchanged at index {i}.");
            }
        }

        // ── Bridge elevation ──────────────────────────────────────────────────

        [Test]
        public void ApplyElevation_MiddlePoints_AreFullyElevated()
        {
            // 11 points; with 20 % ramp fraction the middle section is indices 2..8.
            List<Vector3> spline = StraightSpline(11);
            const float height = BridgeElevator.DefaultBridgeHeight;

            List<Vector3> result = BridgeElevator.ApplyElevation(spline, bridgeHeight: height);

            // Midpoint (index 5) must be at full height.
            Assert.That(result[5].y, Is.EqualTo(height).Within(1e-4f),
                "The midpoint of the bridge should be at full bridge height.");
        }

        [Test]
        public void ApplyElevation_FirstPoint_StartsAtOriginalY()
        {
            List<Vector3> spline = StraightSpline(20);
            List<Vector3> result = BridgeElevator.ApplyElevation(spline);

            // At t=0, smooth-step = 0, so Y elevation = 0.
            Assert.That(result[0].y, Is.EqualTo(0f).Within(1e-5f),
                "The first point of the bridge approach ramp should start at the original Y.");
        }

        [Test]
        public void ApplyElevation_LastPoint_EndsAtOriginalY()
        {
            List<Vector3> spline = StraightSpline(20);
            List<Vector3> result = BridgeElevator.ApplyElevation(spline);

            // At t=1, smooth-step = 0, so Y elevation = 0.
            Assert.That(result[result.Count - 1].y, Is.EqualTo(0f).Within(1e-5f),
                "The last point of the bridge departure ramp should return to the original Y.");
        }

        [Test]
        public void ApplyElevation_AllPointsAtOrAboveOriginalY()
        {
            List<Vector3> spline = StraightSpline(20);
            List<Vector3> result = BridgeElevator.ApplyElevation(spline);

            foreach (var v in result)
                Assert.That(v.y, Is.GreaterThanOrEqualTo(0f),
                    "No bridge point should dip below the original surface.");
        }

        [Test]
        public void ApplyElevation_PeakElevationMatchesBridgeHeight()
        {
            List<Vector3> spline = StraightSpline(21);
            const float height = 6f;

            List<Vector3> result = BridgeElevator.ApplyElevation(spline, bridgeHeight: height);

            float maxY = float.MinValue;
            foreach (var v in result)
                if (v.y > maxY) maxY = v.y;

            Assert.That(maxY, Is.EqualTo(height).Within(1e-4f),
                "The peak Y should equal the specified bridge height.");
        }

        [Test]
        public void ApplyElevation_RespectsBridgeHeightParameter()
        {
            List<Vector3> spline = StraightSpline(21);
            const float height1 = 3f;
            const float height2 = 9f;

            List<Vector3> result1 = BridgeElevator.ApplyElevation(spline, bridgeHeight: height1);
            List<Vector3> result2 = BridgeElevator.ApplyElevation(spline, bridgeHeight: height2);

            // Midpoints should differ by the bridge height ratio.
            Assert.That(result2[10].y, Is.GreaterThan(result1[10].y),
                "Higher bridge height should produce greater elevation at the midpoint.");
        }

        // ── Smooth transition ─────────────────────────────────────────────────

        [Test]
        public void ApplyElevation_RampIsMonotonicallyIncreasing()
        {
            // Use enough points so the ramp region has clear intermediate steps.
            List<Vector3> spline = StraightSpline(101);
            List<Vector3> result = BridgeElevator.ApplyElevation(
                spline,
                bridgeHeight: BridgeElevator.DefaultBridgeHeight,
                rampFraction: BridgeElevator.DefaultRampFraction);

            int rampEnd = (int)(100 * BridgeElevator.DefaultRampFraction);

            for (int i = 1; i <= rampEnd; i++)
                Assert.That(result[i].y, Is.GreaterThanOrEqualTo(result[i - 1].y),
                    $"Approach ramp must be non-decreasing at index {i}.");
        }

        [Test]
        public void ApplyElevation_DepartureRampIsMonotonicallyDecreasing()
        {
            List<Vector3> spline = StraightSpline(101);
            List<Vector3> result = BridgeElevator.ApplyElevation(
                spline,
                bridgeHeight: BridgeElevator.DefaultBridgeHeight,
                rampFraction: BridgeElevator.DefaultRampFraction);

            int rampStart = 100 - (int)(100 * BridgeElevator.DefaultRampFraction);

            for (int i = rampStart + 1; i <= 100; i++)
                Assert.That(result[i].y, Is.LessThanOrEqualTo(result[i - 1].y),
                    $"Departure ramp must be non-increasing at index {i}.");
        }

        [Test]
        public void ApplyElevation_NoAbruptJumpsOnApproachRamp()
        {
            // Step-change between adjacent ramp points should be much less than the full
            // bridge height — a smooth ramp distributes the elevation over many points.
            List<Vector3> spline = StraightSpline(101);
            const float height = BridgeElevator.DefaultBridgeHeight;
            List<Vector3> result = BridgeElevator.ApplyElevation(spline, bridgeHeight: height);

            int rampEnd = (int)(100 * BridgeElevator.DefaultRampFraction);
            for (int i = 1; i <= rampEnd; i++)
            {
                float jump = result[i].y - result[i - 1].y;
                Assert.That(jump, Is.LessThan(height * 0.5f),
                    $"Step change at index {i} is too large — bridge approach is not smooth.");
            }
        }

        [Test]
        public void ApplyElevation_MiddleSectionIsFlat()
        {
            // Use 201 points (200 m spline) so the default 20 % ramp (40 m) gives a
            // ~11.25 % grade — comfortably below the 15 % limit — and the ramp fraction
            // is not automatically extended by the grade cap.
            List<Vector3> spline = StraightSpline(201);
            const float height = BridgeElevator.DefaultBridgeHeight;
            const float ramp = BridgeElevator.DefaultRampFraction;
            List<Vector3> result = BridgeElevator.ApplyElevation(spline, bridgeHeight: height, rampFraction: ramp);

            int midStart = (int)(200 * ramp) + 1;
            int midEnd   = 200 - (int)(200 * ramp) - 1;

            for (int i = midStart; i <= midEnd; i++)
                Assert.That(result[i].y, Is.EqualTo(height).Within(1e-4f),
                    $"Middle-span point {i} should be at full bridge height.");
        }

        // ── Single-point edge case ────────────────────────────────────────────

        [Test]
        public void ApplyElevation_SinglePoint_ReturnsOneElevatedPoint()
        {
            var spline = new List<Vector3> { new Vector3(1f, 2f, 3f) };
            List<Vector3> result = BridgeElevator.ApplyElevation(spline, bridgeHeight: 5f);

            Assert.That(result.Count, Is.EqualTo(1));
            // Single point: t=0, smooth-step factor=0 → no elevation added.
            Assert.That(result[0].y, Is.EqualTo(2f).Within(1e-5f));
        }

        // ── ComputeElevationFactor ────────────────────────────────────────────

        [Test]
        public void ComputeElevationFactor_AtZero_IsZero()
        {
            float factor = BridgeElevator.ComputeElevationFactor(0f, 0.2f);

            Assert.That(factor, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ComputeElevationFactor_AtOne_IsZero()
        {
            float factor = BridgeElevator.ComputeElevationFactor(1f, 0.2f);

            Assert.That(factor, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ComputeElevationFactor_AtMidpoint_IsOne()
        {
            float factor = BridgeElevator.ComputeElevationFactor(0.5f, 0.2f);

            Assert.That(factor, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ComputeElevationFactor_ZeroRampFraction_IsAlwaysOne()
        {
            Assert.That(BridgeElevator.ComputeElevationFactor(0f,   0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BridgeElevator.ComputeElevationFactor(0.5f, 0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BridgeElevator.ComputeElevationFactor(1f,   0f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ComputeElevationFactor_EndOfApproachRamp_IsOne()
        {
            const float ramp = 0.2f;
            // At exactly t = rampFraction the smooth-step output should be 1.
            float factor = BridgeElevator.ComputeElevationFactor(ramp, ramp);

            Assert.That(factor, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ComputeElevationFactor_StartOfDepartureRamp_IsOne()
        {
            const float ramp = 0.2f;
            float factor = BridgeElevator.ComputeElevationFactor(1f - ramp, ramp);

            Assert.That(factor, Is.EqualTo(1f).Within(1e-5f));
        }

        // ── Grade-limit enforcement ───────────────────────────────────────────

        [Test]
        public void MaxRampGrade_Is15Percent()
        {
            Assert.That(BridgeElevator.MaxRampGrade, Is.EqualTo(0.15f).Within(1e-6f));
        }

        [Test]
        public void ApplyElevation_GradeLimit_RampExtendedWhenSplineIsTooShort()
        {
            // 21-point spline = 20 m horizontal length.
            // Default rampFraction = 0.2 → 4 m ramp → grade = 4.5 / 4 = 112.5 %,
            // far above the 15 % limit.
            // Minimum ramp length = 4.5 / 0.15 = 30 m, but the spline is only 20 m,
            // so rampFraction is clamped to 0.5 (10 m each side).
            // At the original ramp end (index 4, t = 0.2) the point must still be
            // climbing the approach ramp — NOT yet at full height.
            List<Vector3> spline = StraightSpline(21);
            const float height = BridgeElevator.DefaultBridgeHeight;

            List<Vector3> result = BridgeElevator.ApplyElevation(
                spline, bridgeHeight: height, rampFraction: BridgeElevator.DefaultRampFraction);

            Assert.That(result[4].y, Is.LessThan(height),
                "Point at the original 20 % ramp end must still be climbing when " +
                "the grade cap has extended the ramp.");
        }

        [Test]
        public void ApplyElevation_GradeLimit_RampNotExtendedWhenGradeAlreadyWithinLimit()
        {
            // 301-point spline = 300 m horizontal length.
            // Default rampFraction = 0.2 → 60 m ramp → grade = 4.5 / 60 = 7.5 %,
            // well below the 15 % limit.  rampFraction must not be changed.
            List<Vector3> spline = StraightSpline(301);
            const float height = BridgeElevator.DefaultBridgeHeight;
            const float ramp   = BridgeElevator.DefaultRampFraction;

            List<Vector3> result = BridgeElevator.ApplyElevation(
                spline, bridgeHeight: height, rampFraction: ramp);

            // End of the requested ramp is at index 60 (t = 60/300 = 0.2 = rampFraction).
            // SmoothStep(1) = 1, so the point must be at full bridge height.
            Assert.That(result[60].y, Is.EqualTo(height).Within(1e-4f),
                "When the requested ramp already satisfies the grade limit the " +
                "fraction must not be extended.");
        }
    }
}
