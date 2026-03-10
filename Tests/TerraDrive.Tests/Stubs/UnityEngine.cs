// Minimal stubs for UnityEngine types so the game source files can be compiled
// and tested in a plain .NET NUnit project without a Unity installation.

using System;
using System.Collections.Generic;

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
        public static Vector3 up   => new Vector3(0f, 1f, 0f);

        /// <summary>Returns a version of this vector with magnitude 1.</summary>
        public Vector3 normalized
        {
            get
            {
                float len = (float)Math.Sqrt(x * x + y * y + z * z);
                return len < 1e-7f ? zero : new Vector3(x / len, y / len, z / len);
            }
        }

        /// <summary>Cross product of two vectors.</summary>
        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);

        /// <summary>Euclidean distance between two points.</summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b) =>
            new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

        public static Vector3 operator +(Vector3 a, Vector3 b) =>
            new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

        public static Vector3 operator *(Vector3 a, float s) =>
            new Vector3(a.x * s, a.y * s, a.z * s);

        public bool Equals(Vector3 other) =>
            Math.Abs(x - other.x) < 1e-5f &&
            Math.Abs(y - other.y) < 1e-5f &&
            Math.Abs(z - other.z) < 1e-5f;

        public static Vector3 operator /(Vector3 a, float s) =>
            new Vector3(a.x / s, a.y / s, a.z / s);

        public override string ToString() => $"({x}, {y}, {z})";
    }

    /// <summary>Stub for UnityEngine.Vector2.</summary>
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public override string ToString() => $"({x}, {y})";
    }

    /// <summary>Stub for UnityEngine.Mesh — stores geometry for test assertions.</summary>
    public class Mesh
    {
        public string name { get; set; } = string.Empty;

        public Vector3[] Vertices  { get; private set; } = Array.Empty<Vector3>();
        public int[]     Triangles { get; private set; } = Array.Empty<int>();

        private readonly Dictionary<int, Vector2[]> _uvChannels = new Dictionary<int, Vector2[]>();

        /// <summary>Returns the UV array for channel 0 (backward-compatible shorthand).</summary>
        public Vector2[] UVs => GetUVs(0);

        public void SetVertices(Vector3[] vertices) => Vertices = vertices ?? Array.Empty<Vector3>();
        public void SetVertices(List<Vector3> vertices) => Vertices = vertices?.ToArray() ?? Array.Empty<Vector3>();

        public void SetUVs(int channel, Vector2[] uvs) =>
            _uvChannels[channel] = uvs ?? Array.Empty<Vector2>();
        public void SetUVs(int channel, List<Vector2> uvs) =>
            _uvChannels[channel] = uvs?.ToArray() ?? Array.Empty<Vector2>();

        /// <summary>Returns the UV array for the given channel, or an empty array if not set.</summary>
        public Vector2[] GetUVs(int channel) =>
            _uvChannels.TryGetValue(channel, out var uvs) ? uvs : Array.Empty<Vector2>();

        public void SetTriangles(int[] triangles, int submesh) => Triangles = triangles ?? Array.Empty<int>();
        public void SetTriangles(List<int> triangles, int submesh) => Triangles = triangles?.ToArray() ?? Array.Empty<int>();
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
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

    /// <summary>Stub for UnityEngine.Random used by BuildingGenerator.</summary>
    public static class Random
    {
        private static readonly System.Random _rng = new System.Random();

        /// <summary>Returns a random float in the range [<paramref name="min"/>, <paramref name="max"/>).</summary>
        public static float Range(float min, float max) =>
            min + (float)_rng.NextDouble() * (max - min);
    }

    /// <summary>Stub for UnityEngine.SerializeField — no-op in tests.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SerializeFieldAttribute : Attribute { }

    /// <summary>Stub for UnityEngine.MonoBehaviour — base class for Unity components.</summary>
    public class MonoBehaviour { }

    /// <summary>Stub for UnityEngine.Color — stores RGBA channels for test assertions.</summary>
    public struct Color
    {
        public float r, g, b, a;

        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public static Color white => new Color(1f, 1f, 1f);
        public static Color gray  => new Color(0.5f, 0.5f, 0.5f);
        public static Color black => new Color(0f, 0f, 0f);
    }

    /// <summary>Stub for UnityEngine.Shader — always returns a non-null instance.</summary>
    public class Shader
    {
        public static Shader? Find(string name) => new Shader();
    }

    /// <summary>Stub for UnityEngine.Material — minimal surface material.</summary>
    public class Material
    {
        public string name  { get; set; }
        public Color  color { get; set; } = Color.white;

        public Material(string name = "") { this.name = name ?? string.Empty; }
        public Material(Shader? shader)   { this.name = string.Empty; }

        public void SetFloat(string propertyName, float value) { }
        public void SetColor(string propertyName, Color value) { color = value; }
    }

    /// <summary>Stub for UnityEngine.MeshRenderer — holds a shared material reference.</summary>
    public class MeshRenderer
    {
        public Material sharedMaterial { get; set; }
    }
}
