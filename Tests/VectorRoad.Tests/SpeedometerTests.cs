using NUnit.Framework;
using VectorRoad.Vehicle;

namespace VectorRoad.Tests
{
    [TestFixture]
    public class SpeedometerTests
    {
        // ── MpsToMph constant ─────────────────────────────────────────────────

        [Test]
        public void MpsToMph_Constant_IsApproximately2point23694()
        {
            Assert.That(Speedometer.MpsToMph, Is.EqualTo(2.23694f).Within(1e-4f));
        }

        // ── ToMph ─────────────────────────────────────────────────────────────

        [Test]
        public void ToMph_Zero_ReturnsZero()
        {
            Assert.That(Speedometer.ToMph(0f), Is.EqualTo(0f));
        }

        [Test]
        public void ToMph_OneMetrePerSecond_ReturnsConversionFactor()
        {
            float result = Speedometer.ToMph(1f);

            Assert.That(result, Is.EqualTo(Speedometer.MpsToMph).Within(1e-5f));
        }

        [Test]
        public void ToMph_44point704MetresPerSecond_Returns100Mph()
        {
            // 100 mph = 44.704 m/s
            float result = Speedometer.ToMph(44.704f);

            Assert.That(result, Is.EqualTo(100f).Within(0.01f));
        }

        [Test]
        public void ToMph_ThirtyMetresPerSecond_IsApprox67Mph()
        {
            // 30 m/s ≈ 67.108 mph
            float result = Speedometer.ToMph(30f);

            Assert.That(result, Is.EqualTo(67.108f).Within(0.01f));
        }

        [Test]
        public void ToMph_PositiveInput_ReturnsPositiveOutput()
        {
            Assert.That(Speedometer.ToMph(20f), Is.GreaterThan(0f));
        }

        [Test]
        public void ToMph_FasterSpeed_ProducesHigherMph()
        {
            float slow = Speedometer.ToMph(10f);
            float fast = Speedometer.ToMph(30f);

            Assert.That(fast, Is.GreaterThan(slow));
        }

        // ── ToMps ─────────────────────────────────────────────────────────────

        [Test]
        public void ToMps_Zero_ReturnsZero()
        {
            Assert.That(Speedometer.ToMps(0f), Is.EqualTo(0f));
        }

        [Test]
        public void ToMps_100Mph_Returns44point704Mps()
        {
            float result = Speedometer.ToMps(100f);

            Assert.That(result, Is.EqualTo(44.704f).Within(0.01f));
        }

        [Test]
        public void ToMps_60Mph_IsApprox26point82Mps()
        {
            // 60 mph = 26.8224 m/s
            float result = Speedometer.ToMps(60f);

            Assert.That(result, Is.EqualTo(26.8224f).Within(0.01f));
        }

        // ── Round-trip ────────────────────────────────────────────────────────

        [Test]
        public void ToMph_ToMps_RoundTrip_PreservesValue()
        {
            const float originalMps = 25f;

            float roundTripped = Speedometer.ToMps(Speedometer.ToMph(originalMps));

            Assert.That(roundTripped, Is.EqualTo(originalMps).Within(1e-3f));
        }

        [Test]
        public void ToMps_ToMph_RoundTrip_PreservesValue()
        {
            const float originalMph = 70f;

            float roundTripped = Speedometer.ToMph(Speedometer.ToMps(originalMph));

            Assert.That(roundTripped, Is.EqualTo(originalMph).Within(1e-3f));
        }
    }
}
