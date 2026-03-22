namespace VectorRoad.Vehicle
{
    /// <summary>
    /// Speed conversion utilities used by the digital speedometer HUD.
    /// </summary>
    public static class Speedometer
    {
        /// <summary>Conversion factor: 1 m/s = 2.23694 mph.</summary>
        public const float MpsToMph = 2.23694f;

        /// <summary>Converts a speed from metres per second to miles per hour.</summary>
        /// <param name="metresPerSecond">Speed in m/s (must be ≥ 0).</param>
        /// <returns>Equivalent speed in mph.</returns>
        public static float ToMph(float metresPerSecond) => metresPerSecond * MpsToMph;

        /// <summary>Converts a speed from miles per hour to metres per second.</summary>
        /// <param name="mph">Speed in mph (must be ≥ 0).</param>
        /// <returns>Equivalent speed in m/s.</returns>
        public static float ToMps(float mph) => mph / MpsToMph;
    }
}
