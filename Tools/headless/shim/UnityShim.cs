// Managed stand-ins for the UnityEngine / Unity.Collections / Jobs APIs used by the simulation layers.
// Used only to run the simulation headless outside Unity (behaviour checks; timings are NOT representative of Burst).
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace UnityEngine {
  public static class Debug {
    public static int Errors, Warnings, Exceptions;
    public static void Log(object m) => Console.WriteLine(m);
    public static void LogWarning(object m) { Warnings++; Console.WriteLine("WARN: " + m); }
    public static void LogError(object m) { Errors++; Console.WriteLine("ERROR: " + m); }
    public static void LogException(Exception e) { Exceptions++; Console.WriteLine("EXCEPTION: " + e); }
    public static void Assert(bool c, string m = null) { if (!c) LogError("Assert: " + m); }
    public static void LogFormat(string f, params object[] a) => Log(string.Format(f, a));
  }
  public static class Application {
    public static bool isPlaying => false; public static bool isBatchMode => true; public static string version => "0.0.0-headless";
    static string Env(string k) => System.Environment.GetEnvironmentVariable(k) ?? throw new System.InvalidOperationException(k + " is not set (run via Tools/headless/headless.py)");
    public static string streamingAssetsPath = System.IO.Path.Combine(Env("PG_BUILD"), "sa"); public static string persistentDataPath = System.IO.Path.Combine(Env("PG_BUILD"), "persist"); public static string dataPath = System.IO.Path.Combine(Env("PG_REPO"), "Assets");
  }
  public static class Mathf {
    public const float PI = MathF.PI; public const float Epsilon = float.Epsilon; public const float Infinity = float.PositiveInfinity; public const float Deg2Rad = PI / 180f; public const float Rad2Deg = 180f / PI;
    public static float Pow(float a, float b) => MathF.Pow(a, b); public static float Sqrt(float a) => MathF.Sqrt(a); public static float Abs(float a) => MathF.Abs(a); public static int Abs(int a) => Math.Abs(a);
    public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v); public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v); public static float Clamp01(float v) => Clamp(v, 0, 1);
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t); public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t; public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0;
    public static float Min(float a, float b) => a < b ? a : b; public static float Max(float a, float b) => a > b ? a : b; public static int Min(int a, int b) => a < b ? a : b; public static int Max(int a, int b) => a > b ? a : b;
    public static int FloorToInt(float v) => (int)MathF.Floor(v); public static int CeilToInt(float v) => (int)MathF.Ceiling(v); public static int RoundToInt(float v) => (int)MathF.Round(v, MidpointRounding.ToEven);
    public static float Floor(float v) => MathF.Floor(v); public static float Ceil(float v) => MathF.Ceiling(v); public static float Round(float v) => MathF.Round(v, MidpointRounding.ToEven);
    public static float Sin(float v) => MathF.Sin(v); public static float Cos(float v) => MathF.Cos(v); public static float Atan2(float y, float x) => MathF.Atan2(y, x); public static float Sign(float v) => v >= 0 ? 1 : -1; public static float Log(float v) => MathF.Log(v); public static float Exp(float v) => MathF.Exp(v);
    public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return b * t + a * (1f - t); }
    public static float Repeat(float t, float l) => Clamp(t - MathF.Floor(t / l) * l, 0, l); public static float PingPong(float t, float l) { t = Repeat(t, l * 2); return l - MathF.Abs(t - l); }
    public static bool Approximately(float a, float b) => MathF.Abs(b - a) < MathF.Max(1e-6f * MathF.Max(MathF.Abs(a), MathF.Abs(b)), 1e-45f * 8);
  }
  [Serializable] public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f); public static implicit operator Color32(Color c) => new Color32((byte)Math.Round(Math.Clamp(c.r, 0, 1) * 255), (byte)Math.Round(Math.Clamp(c.g, 0, 1) * 255), (byte)Math.Round(Math.Clamp(c.b, 0, 1) * 255), (byte)Math.Round(Math.Clamp(c.a, 0, 1) * 255)); public static Color32 Lerp(Color32 a, Color32 b, float t) => new Color32((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), (byte)(a.a + (b.a - a.a) * t)); public override string ToString() => $"RGBA({r},{g},{b},{a})"; }
  [Serializable] public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white => new Color(1, 1, 1); public static Color black => new Color(0, 0, 0); public static Color clear => new Color(0, 0, 0, 0); public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f); public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a); public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); } public static void RGBToHSV(Color c, out float h, out float s, out float v) { float max = Math.Max(c.r, Math.Max(c.g, c.b)), min = Math.Min(c.r, Math.Min(c.g, c.b)); v = max; s = max > 0 ? (max - min) / max : 0; float d = max - min; if (d <= 0) { h = 0; return; } if (max == c.r) h = ((c.g - c.b) / d) % 6; else if (max == c.g) h = (c.b - c.r) / d + 2; else h = (c.r - c.g) / d + 4; h /= 6; if (h < 0) h += 1; } public static Color HSVToRGB(float h, float s, float v) { h = (h % 1 + 1) % 1 * 6; int i = (int)h; float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f)); switch (i) { case 0: return new Color(v, t, p); case 1: return new Color(q, v, p); case 2: return new Color(p, v, t); case 3: return new Color(p, q, v); case 4: return new Color(t, p, v); default: return new Color(v, p, q); } } public Color linear => this; public Color gamma => this; }
  [Serializable] public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => default; public static Vector2 one => new Vector2(1, 1); public float magnitude => MathF.Sqrt(x * x + y * y); public float sqrMagnitude => x * x + y * y; public Vector2 normalized { get { var m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : default; } } public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y); public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y); public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y); public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f); public static Vector2 operator *(float f, Vector2 a) => new Vector2(a.x * f, a.y * f); public static Vector2 operator /(Vector2 a, float f) => new Vector2(a.x / f, a.y / f); public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y; public static bool operator !=(Vector2 a, Vector2 b) => !(a == b); public override bool Equals(object o) => o is Vector2 v && v == this; public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() << 2; public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0); public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y); public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude; public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; } public static implicit operator Unity.Mathematics.float2(Vector2 v) => new Unity.Mathematics.float2(v.x, v.y); public static implicit operator Vector2(Unity.Mathematics.float2 v) => new Vector2(v.x, v.y); }
  [Serializable] public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero => default; public static Vector3 one => new Vector3(1, 1, 1); public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f); public static Vector3 operator *(float f, Vector3 a) => a * f; public static Vector3 operator /(Vector3 a, float f) => new Vector3(a.x / f, a.y / f, a.z / f); public float magnitude => MathF.Sqrt(x * x + y * y + z * z); public float sqrMagnitude => x * x + y * y + z * z; public Vector3 normalized { get { var m = magnitude; return m > 1e-5f ? this / m : default; } } public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z; public static Vector3 Normalize(Vector3 v) => v.normalized; public static implicit operator Unity.Mathematics.float3(Vector3 v) => new Unity.Mathematics.float3(v.x, v.y, v.z); public static implicit operator Vector3(Unity.Mathematics.float3 v) => new Vector3(v.x, v.y, v.z); }
  [Serializable] public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static implicit operator Unity.Mathematics.float4(Vector4 v) => new Unity.Mathematics.float4(v.x, v.y, v.z, v.w); public static implicit operator Vector4(Unity.Mathematics.float4 v) => new Vector4(v.x, v.y, v.z, v.w); }
  [Serializable] public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x = x; this.y = y; } }
  public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5, R8 = 63, R16 = 9, RFloat = 18, RGBAFloat = 20, RGBAHalf = 17, RHalf = 15 }
  public enum FilterMode { Point, Bilinear, Trilinear } public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
  public class Object { public string name; public static void Destroy(Object o) {} public static void DestroyImmediate(Object o) {} public static implicit operator bool(Object o) => o is not null; }
  public class Texture2D : Object {
    public int width, height; Color32[] _px; public FilterMode filterMode; public TextureWrapMode wrapMode;
    public Texture2D(int w, int h, TextureFormat f = TextureFormat.RGBA32, bool mip = false) { width = w; height = h; _px = new Color32[w * h]; }
    public Texture2D(int w, int h, TextureFormat f, bool mip, bool linear) : this(w, h, f, mip) {}
    public void SetPixels32(Color32[] p) { Array.Copy(p, _px, Math.Min(p.Length, _px.Length)); } public Color32[] GetPixels32() => (Color32[])_px.Clone(); public void Apply(bool a = true, bool b = false) {}
    public byte[] EncodeToPNG() { var b = new byte[8 + _px.Length * 4]; b[0] = 0x89; b[1] = (byte)'P'; b[2] = (byte)'N'; b[3] = (byte)'G'; for (int i = 0; i < _px.Length; i++) { b[8 + i * 4] = _px[i].r; b[9 + i * 4] = _px[i].g; b[10 + i * 4] = _px[i].b; b[11 + i * 4] = _px[i].a; } return b; }
    public bool LoadImage(byte[] data) => throw new NotSupportedException("PNG decoding is not available in the headless harness");
  }
  public static class ImageConversion { public static byte[] EncodeToPNG(this Texture2D t) => t.EncodeToPNG(); }
  [AttributeUsage(AttributeTargets.All)] public class SerializeField : Attribute {}
  [AttributeUsage(AttributeTargets.All)] public class HideInInspector : Attribute {}
}
namespace UnityEditor {
  [AttributeUsage(AttributeTargets.Method)] public class MenuItem : Attribute { public MenuItem(string p) {} public MenuItem(string p, bool v) {} public MenuItem(string p, bool v, int pr) {} }
  public static class EditorApplication { public static int ExitCode = -1; public static void Exit(int c) { ExitCode = c; } }
}
namespace PG.EditorTools { public static class DataSync { public static void Sync() { HarnessData.Sync(); } } }
public static class HarnessData {
  public static void Sync() {
    var src = System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("PG_REPO"), "Data"); var dst = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "Data");
    System.IO.Directory.CreateDirectory(dst);
    foreach (var f in System.IO.Directory.GetFiles(src, "*.json")) System.IO.File.Copy(f, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(f)), true);
  }
}
namespace Unity.Profiling {
  public struct ProfilerMarker { public ProfilerMarker(string n) {} public void Begin() {} public void End() {} public AutoScope Auto() => default; public struct AutoScope : IDisposable { public void Dispose() {} } }
}
namespace Unity.Collections {
  public enum Allocator { Invalid, None, Temp, TempJob, Persistent, AudioKernel, Domain }
  public enum NativeArrayOptions { UninitializedMemory, ClearMemory }
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)] public class ReadOnlyAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)] public class WriteOnlyAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.Field)] public class DeallocateOnJobCompletionAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.All)] public class NativeDisableParallelForRestrictionAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.All)] public class NativeDisableContainerSafetyRestrictionAttribute : Attribute {}
  public sealed class NativeBuffer<T> { public T[] Data; public GCHandle Pin; public bool Disposed; public NativeBuffer(int n) { Data = new T[n]; } }
  public struct NativeArray<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArray<T>> where T : struct {
    internal NativeBuffer<T> _buf; internal int _off, _len;
    public NativeArray(int length, Allocator a, NativeArrayOptions o = NativeArrayOptions.ClearMemory) { _buf = new NativeBuffer<T>(length); _off = 0; _len = length; }
    public NativeArray(T[] src, Allocator a) { _buf = new NativeBuffer<T>(src.Length); Array.Copy(src, _buf.Data, src.Length); _off = 0; _len = src.Length; }
    public NativeArray(NativeArray<T> src, Allocator a) { _buf = new NativeBuffer<T>(src.Length); Array.Copy(src._buf.Data, src._off, _buf.Data, 0, src._len); _off = 0; _len = src._len; }
    internal NativeArray(NativeBuffer<T> b, int off, int len) { _buf = b; _off = off; _len = len; }
    public int Length => _len;
    public bool IsCreated => _buf != null && !_buf.Disposed;
    public T this[int i] {
      get { if ((uint)i >= (uint)_len) throw new IndexOutOfRangeException($"Index {i} out of range of '{_len}' Length."); return _buf.Data[_off + i]; }
      set { if ((uint)i >= (uint)_len) throw new IndexOutOfRangeException($"Index {i} out of range of '{_len}' Length."); _buf.Data[_off + i] = value; }
    }
    internal ref T Ref(int i) { if ((uint)i >= (uint)_len) throw new IndexOutOfRangeException(); return ref _buf.Data[_off + i]; }
    public void Dispose() { if (_buf == null) throw new ObjectDisposedException("NativeArray not created"); if (_buf.Disposed) throw new ObjectDisposedException("NativeArray already disposed"); if (_buf.Pin.IsAllocated) _buf.Pin.Free(); _buf.Disposed = true; }
    public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle h) { Dispose(); return h; }
    public void CopyFrom(T[] a) { if (a.Length != _len) throw new ArgumentException("length mismatch"); Array.Copy(a, 0, _buf.Data, _off, _len); }
    public void CopyFrom(NativeArray<T> a) { if (a._len != _len) throw new ArgumentException("length mismatch"); Array.Copy(a._buf.Data, a._off, _buf.Data, _off, _len); }
    public void CopyTo(T[] a) => Array.Copy(_buf.Data, _off, a, 0, _len);
    public void CopyTo(NativeArray<T> a) => a.CopyFrom(this);
    public T[] ToArray() { var r = new T[_len]; CopyTo(r); return r; }
    public NativeArray<T> GetSubArray(int start, int len) { if (start < 0 || len < 0 || start + len > _len) throw new ArgumentOutOfRangeException(); return new NativeArray<T>(_buf, _off + start, len); }
    public NativeSlice<T> Slice(int start, int len) => new NativeSlice<T>(this, start, len);
    public NativeSlice<T> Slice(int start) => new NativeSlice<T>(this, start, _len - start);
    public Span<T> AsSpan() => new Span<T>(_buf.Data, _off, _len);
    public ReadOnlySpan<T> AsReadOnlySpan() => new ReadOnlySpan<T>(_buf.Data, _off, _len);
    public ReadOnly AsReadOnly() => new ReadOnly(this);
    public static void Copy(NativeArray<T> src, NativeArray<T> dst) => dst.CopyFrom(src);
    public static void Copy(NativeArray<T> src, NativeArray<T> dst, int len) => Array.Copy(src._buf.Data, src._off, dst._buf.Data, dst._off, len);
    public static void Copy(NativeArray<T> src, int si, NativeArray<T> dst, int di, int len) { if (si + len > src._len || di + len > dst._len) throw new ArgumentOutOfRangeException(); Array.Copy(src._buf.Data, src._off + si, dst._buf.Data, dst._off + di, len); }
    public static void Copy(T[] src, NativeArray<T> dst, int len) => Array.Copy(src, 0, dst._buf.Data, dst._off, len);
    public static void Copy(NativeArray<T> src, T[] dst, int len) => Array.Copy(src._buf.Data, src._off, dst, 0, len);
    public static void Copy(T[] src, int si, NativeArray<T> dst, int di, int len) => Array.Copy(src, si, dst._buf.Data, dst._off + di, len);
    public static void Copy(NativeArray<T> src, int si, T[] dst, int di, int len) => Array.Copy(src._buf.Data, src._off + si, dst, di, len);
    public NativeArray<U> Reinterpret<U>() where U : struct { if (typeof(U) == typeof(T)) return (NativeArray<U>)(object)this; throw new NotSupportedException("Reinterpret across types is not supported in the harness"); }
    public NativeArray<U> Reinterpret<U>(int size) where U : struct => Reinterpret<U>();
    public Enumerator GetEnumerator() => new Enumerator(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public bool Equals(NativeArray<T> o) => _buf == o._buf && _off == o._off && _len == o._len;
    public override bool Equals(object o) => o is NativeArray<T> a && Equals(a); public override int GetHashCode() => _buf?.GetHashCode() ?? 0;
    public static bool operator ==(NativeArray<T> a, NativeArray<T> b) => a.Equals(b); public static bool operator !=(NativeArray<T> a, NativeArray<T> b) => !a.Equals(b);
    public struct Enumerator : IEnumerator<T> { NativeArray<T> _a; int _i; public Enumerator(NativeArray<T> a) { _a = a; _i = -1; } public T Current => _a[_i]; object IEnumerator.Current => Current; public bool MoveNext() => ++_i < _a.Length; public void Reset() => _i = -1; public void Dispose() {} }
    public struct ReadOnly : IEnumerable<T> { NativeArray<T> _a; public ReadOnly(NativeArray<T> a) { _a = a; } public T this[int i] => _a[i]; public int Length => _a.Length; public bool IsCreated => _a.IsCreated; public T[] ToArray() => _a.ToArray(); public void CopyTo(T[] d) => _a.CopyTo(d); public void CopyTo(NativeArray<T> d) => _a.CopyTo(d); public ReadOnlySpan<T> AsReadOnlySpan() => _a.AsReadOnlySpan(); public Enumerator GetEnumerator() => new Enumerator(_a); IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator(); }
  }
  public struct NativeSlice<T> : IEnumerable<T> where T : struct { NativeArray<T> _a; int _s, _l; public NativeSlice(NativeArray<T> a, int s, int l) { _a = a; _s = s; _l = l; } public NativeSlice(NativeArray<T> a) : this(a, 0, a.Length) {} public T this[int i] { get { if ((uint)i >= (uint)_l) throw new IndexOutOfRangeException(); return _a[_s + i]; } set { if ((uint)i >= (uint)_l) throw new IndexOutOfRangeException(); _a[_s + i] = value; } } public int Length => _l; public void CopyFrom(T[] a) { for (int i = 0; i < _l; i++) this[i] = a[i]; } public void CopyFrom(NativeSlice<T> a) { for (int i = 0; i < _l; i++) this[i] = a[i]; } public void CopyTo(T[] a) { for (int i = 0; i < _l; i++) a[i] = this[i]; } public void CopyTo(NativeArray<T> a) { for (int i = 0; i < _l; i++) a[i] = this[i]; } public T[] ToArray() { var r = new T[_l]; CopyTo(r); return r; } public static implicit operator NativeSlice<T>(NativeArray<T> a) => new NativeSlice<T>(a); public IEnumerator<T> GetEnumerator() { for (int i = 0; i < _l; i++) yield return this[i]; } IEnumerator IEnumerable.GetEnumerator() => GetEnumerator(); }
}
namespace Unity.Collections.LowLevel.Unsafe {
  public static unsafe class UnsafeUtility {
    public static int SizeOf<T>() where T : struct => Marshal.SizeOf<T>();
    public static void MemClear(void* p, long n) { for (long i = 0; i < n; i++) ((byte*)p)[i] = 0; }
    public static void MemCpy(void* d, void* s, long n) => Buffer.MemoryCopy(s, d, n, n);
    public static ref T ArrayElementAsRef<T>(void* p, int i) where T : struct => throw new NotSupportedException();
  }
  public static unsafe class NativeArrayUnsafeUtility {
    public static void* GetUnsafeReadOnlyPtr<T>(Unity.Collections.NativeArray<T> a) where T : struct { var b = a._buf; if (!b.Pin.IsAllocated) b.Pin = GCHandle.Alloc(b.Data, GCHandleType.Pinned); return (byte*)b.Pin.AddrOfPinnedObject() + (long)a._off * UnsafeUtility.SizeOf<T>(); }
    public static void* GetUnsafePtr<T>(Unity.Collections.NativeArray<T> a) where T : struct => GetUnsafeReadOnlyPtr(a);
  }
}
namespace Unity.Jobs {
  public struct JobHandle { public void Complete() {} public bool IsCompleted => true; public static JobHandle CombineDependencies(JobHandle a, JobHandle b) => default; public static JobHandle CombineDependencies(JobHandle a, JobHandle b, JobHandle c) => default; public static void ScheduleBatchedJobs() {} public static void CompleteAll(ref JobHandle a, ref JobHandle b) {} }
  public interface IJob { void Execute(); }
  public interface IJobParallelFor { void Execute(int index); }
  public interface IJobFor { void Execute(int index); }
  public static class IJobExtensions { public static JobHandle Schedule<T>(this T job, JobHandle dep = default) where T : struct, IJob { job.Execute(); return default; } public static void Run<T>(this T job) where T : struct, IJob => job.Execute(); }
  public static class IJobParallelForExtensions { public static JobHandle Schedule<T>(this T job, int n, int batch, JobHandle dep = default) where T : struct, IJobParallelFor { for (int i = 0; i < n; i++) job.Execute(i); return default; } public static void Run<T>(this T job, int n) where T : struct, IJobParallelFor { for (int i = 0; i < n; i++) job.Execute(i); } }
  public static class IJobForExtensions { public static JobHandle Schedule<T>(this T job, int n, JobHandle dep) where T : struct, IJobFor { for (int i = 0; i < n; i++) job.Execute(i); return default; } public static JobHandle ScheduleParallel<T>(this T job, int n, int b, JobHandle dep) where T : struct, IJobFor { for (int i = 0; i < n; i++) job.Execute(i); return default; } public static void Run<T>(this T job, int n) where T : struct, IJobFor { for (int i = 0; i < n; i++) job.Execute(i); } }
}
