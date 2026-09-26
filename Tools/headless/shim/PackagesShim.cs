// Working managed stand-ins for the Unity.Collections package + Burst attributes (headless harness only).
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
namespace Unity.Burst {
  public enum FloatMode { Default, Strict, Deterministic, Fast } public enum FloatPrecision { Standard, High, Medium, Low } public enum OptimizeFor { Default, Performance, Size, FastCompilation, Balanced }
  [AttributeUsage(AttributeTargets.All)] public class BurstCompileAttribute : Attribute { public BurstCompileAttribute() {} public BurstCompileAttribute(FloatPrecision p, FloatMode m) {} public FloatMode FloatMode { get; set; } public FloatPrecision FloatPrecision { get; set; } public bool CompileSynchronously { get; set; } public bool DisableSafetyChecks { get; set; } public OptimizeFor OptimizeFor { get; set; } }
  [AttributeUsage(AttributeTargets.All)] public class BurstDiscardAttribute : Attribute {} [AttributeUsage(AttributeTargets.All)] public class NoAliasAttribute : Attribute {}
}
namespace Unity.Burst.CompilerServices { public static class Hint { public static bool Likely(bool b) => b; public static bool Unlikely(bool b) => b; public static void Assume(bool b) {} } }
namespace Unity.Collections {
  using Unity.Jobs;
  sealed class ListBuf<T> where T : struct { public NativeBuffer<T> B = new NativeBuffer<T>(4); public int Len; public bool Disposed; }
  public struct NativeList<T> : IDisposable, IEnumerable<T> where T : unmanaged {
    ListBuf<T> _l;
    public NativeList(Allocator a) : this(4, a) {}
    public NativeList(int cap, Allocator a) { _l = new ListBuf<T>(); _l.B = new NativeBuffer<T>(Math.Max(1, cap)); }
    void Check() { if (_l == null || _l.Disposed) throw new ObjectDisposedException("NativeList"); }
    void Ensure(int n) { if (n <= _l.B.Data.Length) return; var nb = new NativeBuffer<T>(Math.Max(n, _l.B.Data.Length * 2)); Array.Copy(_l.B.Data, nb.Data, _l.Len); _l.B.Disposed = true; _l.B = nb; }
    public T this[int i] { get { Check(); if ((uint)i >= (uint)_l.Len) throw new IndexOutOfRangeException($"Index {i} is out of range in NativeList of '{_l.Len}' Length."); return _l.B.Data[i]; } set { Check(); if ((uint)i >= (uint)_l.Len) throw new IndexOutOfRangeException($"Index {i} is out of range in NativeList of '{_l.Len}' Length."); _l.B.Data[i] = value; } }
    public int Length { get { Check(); return _l.Len; } set { Resize(value, NativeArrayOptions.ClearMemory); } }
    public int Capacity { get { Check(); return _l.B.Data.Length; } set { Check(); Ensure(value); } }
    public bool IsCreated => _l != null && !_l.Disposed; public bool IsEmpty => !IsCreated || _l.Len == 0;
    public void Add(in T v) { Check(); Ensure(_l.Len + 1); _l.B.Data[_l.Len++] = v; }
    public void AddNoResize(T v) { Check(); if (_l.Len >= _l.B.Data.Length) throw new InvalidOperationException("AddNoResize over capacity"); _l.B.Data[_l.Len++] = v; }
    public void AddRange(NativeArray<T> a) { for (int i = 0; i < a.Length; i++) Add(a[i]); }
    public void AddRange(NativeList<T> a) { for (int i = 0; i < a.Length; i++) Add(a[i]); }
    public void AddReplicate(in T v, int n) { for (int i = 0; i < n; i++) Add(v); }
    public void Clear() { Check(); _l.Len = 0; }
    public void Dispose() { Check(); _l.Disposed = true; _l.B.Disposed = true; }
    public JobHandle Dispose(JobHandle h) { Dispose(); return h; }
    public NativeArray<T> AsArray() { Check(); return new NativeArray<T>(_l.B, 0, _l.Len); }
    public NativeArray<T> AsDeferredJobArray() => AsArray();
    public NativeArray<T>.ReadOnly AsReadOnly() => AsArray().AsReadOnly();
    public ref T ElementAt(int i) { Check(); if ((uint)i >= (uint)_l.Len) throw new IndexOutOfRangeException(); return ref _l.B.Data[i]; }
    public void RemoveAt(int i) { Check(); if ((uint)i >= (uint)_l.Len) throw new IndexOutOfRangeException(); Array.Copy(_l.B.Data, i + 1, _l.B.Data, i, _l.Len - i - 1); _l.Len--; }
    public void RemoveAtSwapBack(int i) { Check(); if ((uint)i >= (uint)_l.Len) throw new IndexOutOfRangeException(); _l.B.Data[i] = _l.B.Data[_l.Len - 1]; _l.Len--; }
    public void RemoveRange(int i, int n) { Check(); Array.Copy(_l.B.Data, i + n, _l.B.Data, i, _l.Len - i - n); _l.Len -= n; }
    public void RemoveRangeSwapBack(int i, int n) { RemoveRange(i, n); }
    public void InsertRange(int i, int n) { Check(); Ensure(_l.Len + n); Array.Copy(_l.B.Data, i, _l.B.Data, i + n, _l.Len - i); Array.Clear(_l.B.Data, i, n); _l.Len += n; }
    public void InsertRangeWithBeginEnd(int b, int e) => InsertRange(b, e - b);
    public void Resize(int n, NativeArrayOptions o) { Check(); Ensure(n); if (n > _l.Len) Array.Clear(_l.B.Data, _l.Len, n - _l.Len); _l.Len = n; }
    public void ResizeUninitialized(int n) => Resize(n, NativeArrayOptions.ClearMemory);
    public void SetCapacity(int n) { Check(); Ensure(n); } public void TrimExcess() {}
    public void CopyFrom(NativeArray<T> a) { Resize(a.Length, NativeArrayOptions.UninitializedMemory); for (int i = 0; i < a.Length; i++) _l.B.Data[i] = a[i]; }
    public void CopyFrom(in NativeList<T> a) => CopyFrom(a.AsArray());
    public T[] ToArray() => AsArray().ToArray(); public NativeArray<T> ToArray(Allocator a) => new NativeArray<T>(AsArray(), a);
    public ParallelWriter AsParallelWriter() => new ParallelWriter(this);
    public struct ParallelWriter { NativeList<T> _l; public ParallelWriter(NativeList<T> l) { _l = l; } public void AddNoResize(T v) { lock (_l._l) _l.AddNoResize(v); } }
    public NativeArray<T>.Enumerator GetEnumerator() => AsArray().GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public static implicit operator NativeArray<T>(NativeList<T> l) => l.AsArray();
  }
  public struct NativeBitArray : IDisposable {
    bool[] _b; bool[] _disp;
    public NativeBitArray(int n, Allocator a, NativeArrayOptions o = NativeArrayOptions.ClearMemory) { _b = new bool[n]; _disp = new bool[1]; }
    public bool IsSet(int i) => _b[i]; public void Set(int i, bool v) => _b[i] = v; public void Clear() => Array.Clear(_b, 0, _b.Length); public void Dispose() => _disp[0] = true; public bool IsCreated => _b != null && !_disp[0]; public int Length => _b.Length;
    public void SetBits(int i, bool v, int n) { for (int k = 0; k < n; k++) _b[i + k] = v; } public int CountBits(int i, int n) { int c = 0; for (int k = 0; k < n; k++) if (_b[i + k]) c++; return c; } public bool TestAny(int i, int n = 1) => CountBits(i, n) > 0;
  }
  public struct NativeQueue<T> : IDisposable where T : unmanaged {
    Queue<T> _q; bool[] _d;
    public NativeQueue(Allocator a) { _q = new Queue<T>(); _d = new bool[1]; }
    public int Count => _q.Count; public bool IsCreated => _q != null && !_d[0]; public void Enqueue(T v) => _q.Enqueue(v); public T Dequeue() => _q.Dequeue(); public bool TryDequeue(out T v) => _q.TryDequeue(out v); public T Peek() => _q.Peek(); public void Clear() => _q.Clear(); public void Dispose() => _d[0] = true; public bool IsEmpty() => _q.Count == 0;
    public NativeArray<T> ToArray(Allocator a) => new NativeArray<T>(_q.ToArray(), a); public ParallelWriter AsParallelWriter() => new ParallelWriter(_q); public struct ParallelWriter { Queue<T> _q; public ParallelWriter(Queue<T> q) { _q = q; } public void Enqueue(T v) { lock (_q) _q.Enqueue(v); } }
  }
  public struct KVPair<K, V> { public K Key; public V Value; }
  public struct NativeHashMap<K, V> : IDisposable, IEnumerable<KVPair<K, V>> where K : unmanaged, IEquatable<K> where V : unmanaged {
    Dictionary<K, V> _d; bool[] _x;
    public NativeHashMap(int cap, Allocator a) { _d = new Dictionary<K, V>(cap); _x = new bool[1]; }
    public int Count => _d.Count; public int Capacity { get => _d.Count; set {} } public bool IsCreated => _d != null && !_x[0]; public bool IsEmpty => _d.Count == 0;
    public V this[K k] { get => _d[k]; set => _d[k] = value; } public bool TryGetValue(K k, out V v) => _d.TryGetValue(k, out v); public bool TryAdd(K k, V v) => _d.TryAdd(k, v); public void Add(K k, V v) => _d.Add(k, v); public bool Remove(K k) => _d.Remove(k); public bool ContainsKey(K k) => _d.ContainsKey(k); public void Clear() => _d.Clear(); public void Dispose() => _x[0] = true;
    public NativeArray<K> GetKeyArray(Allocator a) { var k = new K[_d.Count]; _d.Keys.CopyTo(k, 0); return new NativeArray<K>(k, a); } public NativeArray<V> GetValueArray(Allocator a) { var v = new V[_d.Count]; _d.Values.CopyTo(v, 0); return new NativeArray<V>(v, a); }
    public IEnumerator<KVPair<K, V>> GetEnumerator() { foreach (var kv in _d) yield return new KVPair<K, V> { Key = kv.Key, Value = kv.Value }; } IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
  public struct NativeParallelHashMap<K, V> : IDisposable where K : unmanaged, IEquatable<K> where V : unmanaged {
    Dictionary<K, V> _d; bool[] _x; public NativeParallelHashMap(int cap, Allocator a) { _d = new Dictionary<K, V>(cap); _x = new bool[1]; } public int Count() => _d.Count; public bool IsCreated => _d != null && !_x[0]; public V this[K k] { get => _d[k]; set => _d[k] = value; } public bool TryGetValue(K k, out V v) => _d.TryGetValue(k, out v); public bool TryAdd(K k, V v) => _d.TryAdd(k, v); public void Add(K k, V v) => _d.Add(k, v); public bool Remove(K k) => _d.Remove(k); public bool ContainsKey(K k) => _d.ContainsKey(k); public void Clear() => _d.Clear(); public void Dispose() => _x[0] = true; public NativeArray<K> GetKeyArray(Allocator a) { var k = new K[_d.Count]; _d.Keys.CopyTo(k, 0); return new NativeArray<K>(k, a); }
  }
  public struct NativeParallelMultiHashMapIterator<K> where K : unmanaged { internal int Index; internal K Key; }
  public struct NativeParallelMultiHashMap<K, V> : IDisposable where K : unmanaged, IEquatable<K> where V : unmanaged {
    Dictionary<K, List<V>> _d; bool[] _x; public NativeParallelMultiHashMap(int cap, Allocator a) { _d = new Dictionary<K, List<V>>(); _x = new bool[1]; } public bool IsCreated => _d != null && !_x[0];
    public void Add(K k, V v) { if (!_d.TryGetValue(k, out var l)) _d[k] = l = new List<V>(); l.Add(v); } public void Clear() => _d.Clear(); public void Dispose() => _x[0] = true;
    public bool TryGetFirstValue(K k, out V v, out NativeParallelMultiHashMapIterator<K> it) { it = new NativeParallelMultiHashMapIterator<K> { Key = k, Index = 0 }; if (_d.TryGetValue(k, out var l) && l.Count > 0) { v = l[0]; return true; } v = default; return false; }
    public bool TryGetNextValue(out V v, ref NativeParallelMultiHashMapIterator<K> it) { it.Index++; if (_d.TryGetValue(it.Key, out var l) && it.Index < l.Count) { v = l[it.Index]; return true; } v = default; return false; }
  }
  public struct NativeHashSet<T> : IDisposable where T : unmanaged, IEquatable<T> { HashSet<T> _s; bool[] _x; public NativeHashSet(int c, Allocator a) { _s = new HashSet<T>(); _x = new bool[1]; } public bool Add(T v) => _s.Add(v); public bool Contains(T v) => _s.Contains(v); public bool Remove(T v) => _s.Remove(v); public void Clear() => _s.Clear(); public void Dispose() => _x[0] = true; public int Count => _s.Count; public bool IsCreated => _s != null && !_x[0]; }
  public struct NativeReference<T> : IDisposable where T : unmanaged { T[] _v; public NativeReference(Allocator a) { _v = new T[1]; } public NativeReference(T v, Allocator a) { _v = new[] { v }; } public T Value { get => _v[0]; set => _v[0] = value; } public bool IsCreated => _v != null; public void Dispose() {} }
  public struct FixedString32Bytes { string _s; public FixedString32Bytes(string s) { _s = s; } public static implicit operator FixedString32Bytes(string s) => new FixedString32Bytes(s); public override string ToString() => _s ?? ""; public int Length => (_s ?? "").Length; }
  public struct FixedString64Bytes { string _s; public FixedString64Bytes(string s) { _s = s; } public static implicit operator FixedString64Bytes(string s) => new FixedString64Bytes(s); public override string ToString() => _s ?? ""; public int Length => (_s ?? "").Length; }
  public struct FixedString128Bytes { string _s; public FixedString128Bytes(string s) { _s = s; } public static implicit operator FixedString128Bytes(string s) => new FixedString128Bytes(s); public override string ToString() => _s ?? ""; public int Length => (_s ?? "").Length; }
  // Fixed lists: value semantics like the real ones (copy on assignment) via a small inline-ish array cloned on write is overkill; use capacity-bounded arrays with copy-on-assign emulation through struct fields.
  public struct FixedList32Bytes<T> where T : unmanaged { FixedCore<T> _c; int Cap => Math.Max(1, (32 - 2) / System.Runtime.InteropServices.Marshal.SizeOf<T>()); public int Length { get => _c.Len; set { _c.Own(Cap); _c.Len = value; } } public T this[int i] { get => _c.Get(i); set { _c.Own(Cap); _c.Set(i, value); } } public void Add(in T v) { _c.Own(Cap); _c.Add(v, Cap); } public void Clear() { _c.Own(Cap); _c.Len = 0; } public void RemoveAtSwapBack(int i) { _c.Own(Cap); _c.SwapBack(i); } public void RemoveAt(int i) { _c.Own(Cap); _c.RemoveAt(i); } public int Capacity => Cap; public bool IsEmpty => _c.Len == 0; public ref T ElementAt(int i) { _c.Own(Cap); return ref _c.Ref(i); } }
  public struct FixedList64Bytes<T> where T : unmanaged { FixedCore<T> _c; int Cap => Math.Max(1, (64 - 2) / System.Runtime.InteropServices.Marshal.SizeOf<T>()); public int Length { get => _c.Len; set { _c.Own(Cap); _c.Len = value; } } public T this[int i] { get => _c.Get(i); set { _c.Own(Cap); _c.Set(i, value); } } public void Add(in T v) { _c.Own(Cap); _c.Add(v, Cap); } public void Clear() { _c.Own(Cap); _c.Len = 0; } public void RemoveAtSwapBack(int i) { _c.Own(Cap); _c.SwapBack(i); } public void RemoveAt(int i) { _c.Own(Cap); _c.RemoveAt(i); } public int Capacity => Cap; public bool IsEmpty => _c.Len == 0; public ref T ElementAt(int i) { _c.Own(Cap); return ref _c.Ref(i); } }
  public struct FixedList128Bytes<T> where T : unmanaged { FixedCore<T> _c; int Cap => Math.Max(1, (128 - 2) / System.Runtime.InteropServices.Marshal.SizeOf<T>()); public int Length { get => _c.Len; set { _c.Own(Cap); _c.Len = value; } } public T this[int i] { get => _c.Get(i); set { _c.Own(Cap); _c.Set(i, value); } } public void Add(in T v) { _c.Own(Cap); _c.Add(v, Cap); } public void Clear() { _c.Own(Cap); _c.Len = 0; } public void RemoveAtSwapBack(int i) { _c.Own(Cap); _c.SwapBack(i); } public void RemoveAt(int i) { _c.Own(Cap); _c.RemoveAt(i); } public int Capacity => Cap; public bool IsEmpty => _c.Len == 0; public ref T ElementAt(int i) { _c.Own(Cap); return ref _c.Ref(i); } }
  public struct FixedList512Bytes<T> where T : unmanaged { FixedCore<T> _c; int Cap => Math.Max(1, (512 - 2) / System.Runtime.InteropServices.Marshal.SizeOf<T>()); public int Length { get => _c.Len; set { _c.Own(Cap); _c.Len = value; } } public T this[int i] { get => _c.Get(i); set { _c.Own(Cap); _c.Set(i, value); } } public void Add(in T v) { _c.Own(Cap); _c.Add(v, Cap); } public void Clear() { _c.Own(Cap); _c.Len = 0; } public void RemoveAtSwapBack(int i) { _c.Own(Cap); _c.SwapBack(i); } public void RemoveAt(int i) { _c.Own(Cap); _c.RemoveAt(i); } public int Capacity => Cap; public bool IsEmpty => _c.Len == 0; public ref T ElementAt(int i) { _c.Own(Cap); return ref _c.Ref(i); } }
  public struct FixedList4096Bytes<T> where T : unmanaged { FixedCore<T> _c; int Cap => Math.Max(1, (4096 - 2) / System.Runtime.InteropServices.Marshal.SizeOf<T>()); public int Length { get => _c.Len; set { _c.Own(Cap); _c.Len = value; } } public T this[int i] { get => _c.Get(i); set { _c.Own(Cap); _c.Set(i, value); } } public void Add(in T v) { _c.Own(Cap); _c.Add(v, Cap); } public void Clear() { _c.Own(Cap); _c.Len = 0; } public void RemoveAtSwapBack(int i) { _c.Own(Cap); _c.SwapBack(i); } public void RemoveAt(int i) { _c.Own(Cap); _c.RemoveAt(i); } public int Capacity => Cap; public bool IsEmpty => _c.Len == 0; }
  // Copy-on-write core so struct copies behave like value types.
  internal struct FixedCore<T> where T : unmanaged {
    T[] _a; object _owner; public int Len;
    public void Own(int cap) { if (_a == null) { _a = new T[cap]; _owner = null; } }
    public T Get(int i) { if ((uint)i >= (uint)Len) throw new IndexOutOfRangeException(); return _a[i]; }
    public void Set(int i, T v) { if ((uint)i >= (uint)Len) throw new IndexOutOfRangeException(); Detach(); _a[i] = v; }
    public ref T Ref(int i) { if ((uint)i >= (uint)Len) throw new IndexOutOfRangeException(); Detach(); return ref _a[i]; }
    public void Add(T v, int cap) { if (Len >= cap) throw new IndexOutOfRangeException("FixedList full"); Detach(); _a[Len++] = v; }
    public void SwapBack(int i) { Detach(); _a[i] = _a[Len - 1]; Len--; }
    public void RemoveAt(int i) { Detach(); Array.Copy(_a, i + 1, _a, i, Len - i - 1); Len--; }
    void Detach() { _a = (T[])_a.Clone(); }
  }
  public static class NativeArrayExtensions {
    public static void Sort<T>(this NativeArray<T> a) where T : unmanaged, IComparable<T> { var t = a.ToArray(); Array.Sort(t); a.CopyFrom(t); }
    public static void Sort<T, U>(this NativeArray<T> a, U c) where T : unmanaged where U : IComparer<T> { var t = a.ToArray(); Array.Sort(t, c); a.CopyFrom(t); }
    public static void Sort<T>(this NativeList<T> a) where T : unmanaged, IComparable<T> => a.AsArray().Sort();
    public static void Sort<T, U>(this NativeList<T> a, U c) where T : unmanaged where U : IComparer<T> => a.AsArray().Sort(c);
    public static bool Contains<T, U>(this NativeArray<T> a, U v) where T : unmanaged, IEquatable<U> => IndexOf(a, v) >= 0;
    public static int IndexOf<T, U>(this NativeArray<T> a, U v) where T : unmanaged, IEquatable<U> { for (int i = 0; i < a.Length; i++) if (a[i].Equals(v)) return i; return -1; }
    public static bool Contains<T, U>(this NativeList<T> a, U v) where T : unmanaged, IEquatable<U> => IndexOf(a.AsArray(), v) >= 0;
    public static int IndexOf<T, U>(this NativeList<T> a, U v) where T : unmanaged, IEquatable<U> => IndexOf(a.AsArray(), v);
    public static bool Contains<T, U>(this NativeArray<T>.ReadOnly a, U v) where T : unmanaged, IEquatable<U> { for (int i = 0; i < a.Length; i++) if (a[i].Equals(v)) return true; return false; }
    public static int IndexOf<T, U>(this NativeArray<T>.ReadOnly a, U v) where T : unmanaged, IEquatable<U> { for (int i = 0; i < a.Length; i++) if (a[i].Equals(v)) return i; return -1; }
  }
  public static class xxHash3 {
    public static unsafe Unity.Mathematics.uint2 Hash64(void* p, long n) { ulong h = 14695981039346656037UL; byte* b = (byte*)p; for (long i = 0; i < n; i++) h = (h ^ b[i]) * 1099511628211UL; return new Unity.Mathematics.uint2((uint)h, (uint)(h >> 32)); }
  }
  public static class AllocatorManager { public struct AllocatorHandle { public static implicit operator AllocatorHandle(Allocator a) => default; } }
}
