// Minimal stubs for UnityEngine types so the game source files can be compiled
// and tested in a plain .NET NUnit project without a Unity installation.

using System;

namespace UnityEngine
{
    /// <summary>Stub matching the subset of UnityEngine.Vector3 used by the game code.</summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);

        public bool Equals(Vector3 other) =>
            Math.Abs(x - other.x) < 1e-5f &&
            Math.Abs(y - other.y) < 1e-5f &&
            Math.Abs(z - other.z) < 1e-5f;

        public override string ToString() => $"({x}, {y}, {z})";
    }

    /// <summary>Stub for UnityEngine.Debug — swallows log output during tests.</summary>
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    /// <summary>Stub for UnityEngine.Mathf constants used by CoordinateConverter.</summary>
    public static class Mathf
    {
        public const float Deg2Rad = (float)(Math.PI / 180.0);
        public const float Rad2Deg = (float)(180.0 / Math.PI);
    }
}
