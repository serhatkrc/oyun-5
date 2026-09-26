// AUTO STUBS for compile checking only (Unity.Collections package, Burst, InputSystem).
#pragma warning disable
using System;
using System.Collections;
using System.Collections.Generic;
namespace Unity.Burst {
  public enum FloatMode { Default, Strict, Deterministic, Fast }
  public enum FloatPrecision { Standard, High, Medium, Low }
  public enum OptimizeFor { Default, Performance, Size, FastCompilation, Balanced }
  [AttributeUsage(AttributeTargets.All)] public class BurstCompileAttribute : Attribute {
    public BurstCompileAttribute() {} public BurstCompileAttribute(FloatPrecision p, FloatMode m) {}
    public FloatMode FloatMode { get; set; } public FloatPrecision FloatPrecision { get; set; } public bool CompileSynchronously { get; set; } public bool DisableSafetyChecks { get; set; } public OptimizeFor OptimizeFor { get; set; } }
  [AttributeUsage(AttributeTargets.All)] public class BurstDiscardAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.All)] public class NoAliasAttribute : Attribute {}
}
namespace Unity.Burst.CompilerServices { public static class Hint { public static bool Likely(bool b)=>b; public static bool Unlikely(bool b)=>b; public static void Assume(bool b){} } }
namespace Unity.Collections {
  using Unity.Jobs;
  public struct NativeList<T> : IDisposable, IEnumerable<T> where T : unmanaged {
    public NativeList(Allocator a) {} public NativeList(int cap, Allocator a) {}
    public T this[int i] { get => default; set {} }
    public int Length { get => 0; set {} } public int Capacity { get => 0; set {} }
    public bool IsCreated => false; public bool IsEmpty => false;
    public void Add(in T v) {} public void AddNoResize(T v) {} public void AddRange(NativeArray<T> a) {} public unsafe void AddRange(void* p, int n) {}
    public void AddReplicate(in T v, int n) {}
    public void Clear() {} public void Dispose() {} public JobHandle Dispose(JobHandle h) => h;
    public NativeArray<T> AsArray() => default; public NativeArray<T> AsDeferredJobArray() => default; public NativeArray<T>.ReadOnly AsReadOnly() => default;
    public ref T ElementAt(int i) => throw null;
    public void RemoveAt(int i) {} public void RemoveAtSwapBack(int i) {} public void RemoveRange(int i, int n) {} public void RemoveRangeSwapBack(int i, int n) {}
    public void InsertRange(int i, int n) {} public void InsertRangeWithBeginEnd(int b, int e) {}
    public void Resize(int n, NativeArrayOptions o) {} public void ResizeUninitialized(int n) {} public void SetCapacity(int n) {} public void TrimExcess() {}
    public void CopyFrom(NativeArray<T> a) {} public void CopyFrom(in NativeList<T> a) {}
    public T[] ToArray() => null; public NativeArray<T> ToArray(Allocator a) => default;
    public ParallelWriter AsParallelWriter() => default;
    public struct ParallelWriter { public void AddNoResize(T v) {} }
    public NativeArray<T>.Enumerator GetEnumerator() => default;
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => null; IEnumerator IEnumerable.GetEnumerator() => null;
    public static implicit operator NativeArray<T>(NativeList<T> l) => default;
  }
  public struct NativeBitArray : IDisposable {
    public NativeBitArray(int n, Allocator a, NativeArrayOptions o = NativeArrayOptions.ClearMemory) {}
    public bool IsSet(int i) => false; public void Set(int i, bool v) {} public void Clear() {} public void Dispose() {} public bool IsCreated => false; public int Length => 0;
    public void SetBits(int i, bool v, int n) {} public int CountBits(int i, int n) => 0; public bool TestAny(int i, int n = 1) => false;
  }
  public struct NativeQueue<T> : IDisposable where T : unmanaged {
    public NativeQueue(Allocator a) {} public int Count => 0; public bool IsCreated => false; public void Enqueue(T v) {} public T Dequeue() => default; public bool TryDequeue(out T v) { v = default; return false; } public T Peek() => default; public void Clear() {} public void Dispose() {} public bool IsEmpty() => true;
    public NativeArray<T> ToArray(Allocator a) => default; public ParallelWriter AsParallelWriter() => default; public struct ParallelWriter { public void Enqueue(T v) {} }
  }
  public struct KVPair<K, V> { public K Key => default; public V Value => default; }
  public struct NativeHashMap<K, V> : IDisposable, IEnumerable<KVPair<K, V>> where K : unmanaged, IEquatable<K> where V : unmanaged {
    public NativeHashMap(int cap, Allocator a) {} public int Count => 0; public int Capacity { get => 0; set {} } public bool IsCreated => false; public bool IsEmpty => true;
    public V this[K k] { get => default; set {} } public bool TryGetValue(K k, out V v) { v = default; return false; } public bool TryAdd(K k, V v) => false; public void Add(K k, V v) {} public bool Remove(K k) => false; public bool ContainsKey(K k) => false; public void Clear() {} public void Dispose() {}
    public NativeArray<K> GetKeyArray(Allocator a) => default; public NativeArray<V> GetValueArray(Allocator a) => default;
    public IEnumerator<KVPair<K, V>> GetEnumerator() => null; IEnumerator IEnumerable.GetEnumerator() => null;
  }
  public struct NativeParallelHashMap<K, V> : IDisposable where K : unmanaged, IEquatable<K> where V : unmanaged {
    public NativeParallelHashMap(int cap, Allocator a) {} public int Count() => 0; public bool IsCreated => false; public V this[K k] { get => default; set {} } public bool TryGetValue(K k, out V v) { v = default; return false; } public bool TryAdd(K k, V v) => false; public void Add(K k, V v) {} public bool Remove(K k) => false; public bool ContainsKey(K k) => false; public void Clear() {} public void Dispose() {} public NativeArray<K> GetKeyArray(Allocator a) => default;
  }
  public struct NativeParallelMultiHashMap<K, V> : IDisposable where K : unmanaged, IEquatable<K> where V : unmanaged {
    public NativeParallelMultiHashMap(int cap, Allocator a) {} public bool IsCreated => false; public void Add(K k, V v) {} public void Clear() {} public void Dispose() {}
    public bool TryGetFirstValue(K k, out V v, out NativeParallelMultiHashMapIterator<K> it) { v = default; it = default; return false; } public bool TryGetNextValue(out V v, ref NativeParallelMultiHashMapIterator<K> it) { v = default; return false; }
  }
  public struct NativeParallelMultiHashMapIterator<K> where K : unmanaged {}
  public struct NativeHashSet<T> : IDisposable where T : unmanaged, IEquatable<T> { public NativeHashSet(int c, Allocator a) {} public bool Add(T v) => false; public bool Contains(T v) => false; public bool Remove(T v) => false; public void Clear() {} public void Dispose() {} public int Count => 0; public bool IsCreated => false; }
  public struct NativeReference<T> : IDisposable where T : unmanaged { public NativeReference(Allocator a) {} public NativeReference(T v, Allocator a) {} public T Value { get => default; set {} } public bool IsCreated => false; public void Dispose() {} }
  public struct FixedString32Bytes { public FixedString32Bytes(string s) {} public static implicit operator FixedString32Bytes(string s) => default; public override string ToString() => ""; public int Length => 0; }
  public struct FixedString64Bytes { public FixedString64Bytes(string s) {} public static implicit operator FixedString64Bytes(string s) => default; public override string ToString() => ""; public int Length => 0; }
  public struct FixedString128Bytes { public FixedString128Bytes(string s) {} public static implicit operator FixedString128Bytes(string s) => default; public override string ToString() => ""; public int Length => 0; }
  public struct FixedList32Bytes<T> where T : unmanaged { public int Length { get => 0; set {} } public T this[int i] { get => default; set {} } public void Add(in T v) {} public void Clear() {} public void RemoveAtSwapBack(int i) {} public int Capacity => 0; }
  public struct FixedList64Bytes<T> where T : unmanaged { public int Length { get => 0; set {} } public T this[int i] { get => default; set {} } public void Add(in T v) {} public void Clear() {} public void RemoveAtSwapBack(int i) {} public int Capacity => 0; }
  public struct FixedList128Bytes<T> where T : unmanaged { public int Length { get => 0; set {} } public T this[int i] { get => default; set {} } public void Add(in T v) {} public void Clear() {} public void RemoveAtSwapBack(int i) {} public int Capacity => 0; }
  public static class NativeArrayExtensions {
    public static void Sort<T>(this NativeArray<T> a) where T : unmanaged, IComparable<T> {}
    public static void Sort<T, U>(this NativeArray<T> a, U c) where T : unmanaged where U : IComparer<T> {}
    public static void Sort<T>(this NativeList<T> a) where T : unmanaged, IComparable<T> {}
    public static void Sort<T, U>(this NativeList<T> a, U c) where T : unmanaged where U : IComparer<T> {}
    public static void Sort<T>(this NativeSlice<T> a) where T : unmanaged, IComparable<T> {}
    public static bool Contains<T, U>(this NativeArray<T> a, U v) where T : unmanaged, IEquatable<U> => false;
    public static int IndexOf<T, U>(this NativeArray<T> a, U v) where T : unmanaged, IEquatable<U> => -1;
    public static bool Contains<T, U>(this NativeList<T> a, U v) where T : unmanaged, IEquatable<U> => false;
    public static int IndexOf<T, U>(this NativeList<T> a, U v) where T : unmanaged, IEquatable<U> => -1;
    public static bool Contains<T, U>(this NativeArray<T>.ReadOnly a, U v) where T : unmanaged, IEquatable<U> => false;
    public static int IndexOf<T, U>(this NativeArray<T>.ReadOnly a, U v) where T : unmanaged, IEquatable<U> => -1;
  }
  public static class xxHash3 { public static unsafe Unity.Mathematics.uint2 Hash64(void* p, long n) => default; public static unsafe Unity.Mathematics.uint4 Hash128(void* p, long n) => default; }
  public static class CollectionHelper { public static int Align(int s, int a) => s; }
}
namespace Unity.Collections.LowLevel.Unsafe {
  public struct UnsafeList<T> : IDisposable where T : unmanaged { public UnsafeList(int c, Unity.Collections.AllocatorManager.AllocatorHandle a) {} public int Length { get => 0; set {} } public T this[int i] { get => default; set {} } public void Add(in T v) {} public void Clear() {} public void Dispose() {} public bool IsCreated => false; }
}
namespace Unity.Collections { public static class AllocatorManager { public struct AllocatorHandle { public static implicit operator AllocatorHandle(Allocator a) => default; } } }
namespace UnityEngine.InputSystem {
  public enum InputActionType { Value, Button, PassThrough }
  public struct InputBinding {}
  public class InputAction : IDisposable {
    public InputAction(string name = null, InputActionType type = default, string binding = null, string interactions = null, string processors = null, string expectedControlType = null) {}
    public struct CallbackContext { public TValue ReadValue<TValue>() where TValue : struct => default; public bool performed => false; public bool started => false; public bool canceled => false; public InputAction action => null; public InputControl control => null; public double time => 0; }
    public event Action<CallbackContext> started, performed, canceled;
    public CompositeSyntax AddCompositeBinding(string c, string interactions = null, string processors = null) => default;
    public BindingSyntax AddBinding(string path, string interactions = null, string processors = null, string groups = null) => default;
    public struct CompositeSyntax { public CompositeSyntax With(string name, string binding, string groups = null, string processors = null) => this; }
    public struct BindingSyntax { public BindingSyntax WithModifiers(string m) => this; }
    public void Enable() {} public void Disable() {} public void Dispose() {} public bool enabled => false; public string name => "";
    public TValue ReadValue<TValue>() where TValue : struct => default; public bool WasPressedThisFrame() => false; public bool WasReleasedThisFrame() => false; public bool IsPressed() => false; public bool triggered => false; public bool WasPerformedThisFrame() => false; public float ReadValueAsObject() => 0;
  }
  public class InputActionMap : IDisposable { public InputActionMap(string n = null) {} public InputAction AddAction(string name, InputActionType type = default, string binding = null, string interactions = null, string processors = null, string groups = null, string expectedControlType = null) => null; public void Enable() {} public void Disable() {} public void Dispose() {} public InputAction FindAction(string n, bool t = false) => null; }
  public class InputControl { public string path => ""; public InputDevice device => null; }
  public class InputControl<T> : InputControl where T : struct { public T ReadValue() => default; }
  public class ButtonControl : InputControl<float> { public bool isPressed => false; public bool wasPressedThisFrame => false; public bool wasReleasedThisFrame => false; }
  public class Vector2Control : InputControl<UnityEngine.Vector2> { }
  public class InputDevice : InputControl { }
  public class Pointer : InputDevice { public Vector2Control position => null; public Vector2Control delta => null; public ButtonControl press => null; public static Pointer current => null; }
  public class Mouse : Pointer { public static new Mouse current => null; public ButtonControl leftButton => null; public ButtonControl rightButton => null; public ButtonControl middleButton => null; public Vector2Control scroll => null; }
  public class TouchControl : InputControl { public ButtonControl press => null; public Vector2Control position => null; public Vector2Control delta => null; public bool isInProgress => false; }
  public class Touchscreen : Pointer { public static new Touchscreen current => null; public TouchControl primaryTouch => null; public System.Collections.Generic.IReadOnlyList<TouchControl> touches => null; }
  public class KeyControl : ButtonControl { }
  public enum Key { None, Space, Enter, Escape, Tab, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt, UpArrow, DownArrow, LeftArrow, RightArrow, Minus, Equals, Backquote, Delete, Backspace, Period, Comma, Slash, LeftBracket, RightBracket, NumpadPlus, NumpadMinus }
  public class Keyboard : InputDevice { public static Keyboard current => null; public KeyControl this[Key k] => null; public KeyControl spaceKey => null; public KeyControl escapeKey => null; public KeyControl shiftKey => null; public KeyControl ctrlKey => null; public KeyControl altKey => null; }
  public static class InputSystem { }
}
namespace UnityEngine.InputSystem.Controls { }
namespace UnityEngine.InputSystem.EnhancedTouch { }
namespace Unity.Collections {
  public struct FixedList512Bytes<T> where T : unmanaged { public int Length { get => 0; set {} } public T this[int i] { get => default; set {} } public void Add(in T v) {} public void Clear() {} public void RemoveAtSwapBack(int i) {} public int Capacity => 0; public void RemoveAt(int i) {} public bool IsEmpty => true; public ref T ElementAt(int i) => throw null; }
  public struct FixedList4096Bytes<T> where T : unmanaged { public int Length { get => 0; set {} } public T this[int i] { get => default; set {} } public void Add(in T v) {} public void Clear() {} public void RemoveAtSwapBack(int i) {} public int Capacity => 0; public void RemoveAt(int i) {} public bool IsEmpty => true; }
  // Unity 2022.2+ NativeArray span accessors (missing from the 2021 reference assemblies)
  public static class NativeArraySpanStubExtensions {
    public static System.Span<T> AsSpan<T>(this NativeArray<T> a) where T : struct => default;
    public static System.ReadOnlySpan<T> AsReadOnlySpan<T>(this NativeArray<T> a) where T : struct => default;
  }
}
