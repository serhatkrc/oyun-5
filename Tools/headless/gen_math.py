# Generates a WORKING managed subset of Unity.Mathematics (for offline compile + headless runs).
import itertools
comps="xyzw"
out=["// AUTO-GENERATED managed subset of Unity.Mathematics for offline builds. Not the real package.",
     "#pragma warning disable","using System;","using System.Runtime.CompilerServices;","namespace Unity.Mathematics {"]
bases=["float","int","uint","double","bool"]
def cast(b,expr): return f"({b})({expr})"
for b in bases:
    for n in (2,3,4):
        t=f"{b}{n}"; c=comps[:n]
        L=[f"[Serializable] public partial struct {t} : IEquatable<{t}>, IFormattable {{"]
        L.append(" public "+b+" "+", ".join(c)+";")
        L.append(f" public {t}({', '.join(b+' '+x for x in c)}) {{ {' '.join('this.'+x+'='+x+';' for x in c)} }}")
        L.append(f" public {t}({b} v) {{ {' '.join(x+'=v;' for x in c)} }}")
        if n==3:
            L.append(f" public {t}({b}2 xy, {b} z) {{ x=xy.x; y=xy.y; this.z=z; }}")
            L.append(f" public {t}({b} x, {b}2 yz) {{ this.x=x; y=yz.x; z=yz.y; }}")
        if n==4:
            L.append(f" public {t}({b}3 xyz, {b} w) {{ x=xyz.x; y=xyz.y; z=xyz.z; this.w=w; }}")
            L.append(f" public {t}({b}2 xy, {b}2 zw) {{ x=xy.x; y=xy.y; z=zw.x; w=zw.y; }}")
            L.append(f" public {t}({b}2 xy, {b} z, {b} w) {{ x=xy.x; y=xy.y; this.z=z; this.w=w; }}")
            L.append(f" public {t}({b} x, {b}2 yz, {b} w) {{ this.x=x; y=yz.x; z=yz.y; this.w=w; }}")
            L.append(f" public {t}({b} x, {b} y, {b}2 zw) {{ this.x=x; this.y=y; z=zw.x; w=zw.y; }}")
            L.append(f" public {t}({b} x, {b}3 yzw) {{ this.x=x; y=yzw.x; z=yzw.y; w=yzw.z; }}")
        for k in (2,3):
            for combo in itertools.product(c, repeat=k):
                name="".join(combo)
                get=f"new {b}{k}({', '.join(combo)})"
                if len(set(combo))==k:
                    setter=" ".join(f"{ch}=value.{comps[i]};" for i,ch in enumerate(combo))
                    L.append(f" public {b}{k} {name} {{ get => {get}; set {{ {setter} }} }}")
                else:
                    L.append(f" public {b}{k} {name} => {get};")
        cases=" ".join(f"case {i}: return {ch};" for i,ch in enumerate(c))
        scases=" ".join(f"case {i}: {ch}=value; return;" for i,ch in enumerate(c))
        L.append(f" public {b} this[int i] {{ get {{ switch(i){{ {cases} }} throw new IndexOutOfRangeException(); }} set {{ switch(i){{ {scases} }} throw new IndexOutOfRangeException(); }} }}")
        L.append(f" public static implicit operator {t}({b} v) => new {t}(v);")
        if b!="bool":
            for o in ["float","int","uint","double"]:
                if o==b: continue
                imp=(b=="float" and o in("int","uint")) or (b=="double")
                kw="implicit" if imp else "explicit"
                L.append(f" public static {kw} operator {t}({o}{n} v) => new {t}({', '.join(cast(b,'v.'+x) for x in c)});")
                if not imp: L.append(f" public static explicit operator {t}({o} v) => new {t}({cast(b,'v')});")
            if b in("float","double"):
                L.append(f" public static explicit operator {t}(bool{n} v) => new {t}({', '.join('(v.'+x+'?1:0)' for x in c)});")
            else:
                L.append(f" public static explicit operator {t}(bool{n} v) => new {t}({', '.join('('+b+')(v.'+x+'?1:0)' for x in c)});")
            for op in "+-*/%":
                L.append(f" public static {t} operator {op}({t} a, {t} b) => new {t}({', '.join(cast(b_ if False else b,'a.'+x+op+'b.'+x) for x in c for b_ in [b])});")
                L.append(f" public static {t} operator {op}({t} a, {b} b) => new {t}({', '.join(cast(b,'a.'+x+op+'b') for x in c)});")
                L.append(f" public static {t} operator {op}({b} a, {t} b) => new {t}({', '.join(cast(b,'a'+op+'b.'+x) for x in c)});")
            neg = ', '.join(cast(b,'-a.'+x) if b!='uint' else cast(b,'0u-a.'+x) for x in c)
            L.append(f" public static {t} operator -({t} a) => new {t}({neg});")
            L.append(f" public static {t} operator +({t} a) => a;")
            L.append(f" public static {t} operator ++({t} a) => a + ({b})1;")
            L.append(f" public static {t} operator --({t} a) => a - ({b})1;")
            for op in ["<",">","<=",">="]:
                L.append(f" public static bool{n} operator {op}({t} a, {t} b) => new bool{n}({', '.join('a.'+x+op+'b.'+x for x in c)});")
                L.append(f" public static bool{n} operator {op}({t} a, {b} b) => new bool{n}({', '.join('a.'+x+op+'b' for x in c)});")
                L.append(f" public static bool{n} operator {op}({b} a, {t} b) => new bool{n}({', '.join('a'+op+'b.'+x for x in c)});")
        if b in ("int","uint"):
            for op in "&|^":
                L.append(f" public static {t} operator {op}({t} a, {t} b) => new {t}({', '.join(cast(b,'a.'+x+op+'b.'+x) for x in c)});")
                L.append(f" public static {t} operator {op}({t} a, {b} b) => new {t}({', '.join(cast(b,'a.'+x+op+'b') for x in c)});")
            L.append(f" public static {t} operator <<({t} a, int s) => new {t}({', '.join(cast(b,'a.'+x+'<<s') for x in c)});")
            L.append(f" public static {t} operator >>({t} a, int s) => new {t}({', '.join(cast(b,'a.'+x+'>>s') for x in c)});")
            L.append(f" public static {t} operator ~({t} a) => new {t}({', '.join(cast(b,'~a.'+x) for x in c)});")
        if b=="bool":
            for op in "&|^":
                L.append(f" public static {t} operator {op}({t} a, {t} b) => new {t}({', '.join('a.'+x+op+'b.'+x for x in c)});")
                L.append(f" public static {t} operator {op}({t} a, bool b) => new {t}({', '.join('a.'+x+op+'b' for x in c)});")
            L.append(f" public static {t} operator !({t} a) => new {t}({', '.join('!a.'+x for x in c)});")
        L.append(f" public static bool{n} operator ==({t} a, {t} b) => new bool{n}({', '.join('a.'+x+'==b.'+x for x in c)});")
        L.append(f" public static bool{n} operator !=({t} a, {t} b) => new bool{n}({', '.join('a.'+x+'!=b.'+x for x in c)});")
        L.append(f" public static bool{n} operator ==({t} a, {b} b) => new bool{n}({', '.join('a.'+x+'==b' for x in c)});")
        L.append(f" public static bool{n} operator !=({t} a, {b} b) => new bool{n}({', '.join('a.'+x+'!=b' for x in c)});")
        L.append(f" public bool Equals({t} o) => {' && '.join(x+'.Equals(o.'+x+')' for x in c)};")
        L.append(f" public override bool Equals(object o) => o is {t} v && Equals(v);")
        L.append(f" public override int GetHashCode() => (int)math.hash(this);")
        L.append(f" public override string ToString() => $\"{t}({', '.join('{'+x+'}' for x in c)})\";")
        L.append(f" public string ToString(string f, IFormatProvider p) => ToString();")
        if b!="bool": L.append(f" public static readonly {t} zero = default;")
        L.append("}")
        out+=L
M=["public static partial class math {",
   " public const float PI = 3.14159265358979f; public const float SQRT2 = 1.41421356237f; public const float E=2.71828182846f; public const float EPSILON=1.1920929e-7f; public const float INFINITY=float.PositiveInfinity; public const float TAU=6.28318530718f; public const float PI2=6.28318530718f; public const float FLT_MIN_NORMAL=1.175494351e-38f; public const double PI_DBL=Math.PI; public const float TODEGREES=57.29578f; public const float TORADIANS=0.0174532925f;"]
numeric=["float","int","uint","double"]
def comp(b,n,fn):  # componentwise
    c=comps[:n]; return f"new {b}{n}({', '.join(fn(x) for x in c)})"
fl={"float":"MathF","double":"Math"}
for b in numeric:
    M.append(f" public static {b} max({b} a, {b} b) => a > b ? a : b;")
    M.append(f" public static {b} min({b} a, {b} b) => a < b ? a : b;")
    M.append(f" public static {b} clamp({b} v, {b} a, {b} b) => max(a, min(b, v));")
    if b=="uint": M.append(" public static uint abs(uint v) => v; public static uint sign(uint v) => v>0?1u:0u;")
    elif b=="int": M.append(" public static int abs(int v) => v<0?-v:v; public static int sign(int v) => v>0?1:(v<0?-1:0);")
    else:
        F=fl[b]
        M.append(f" public static {b} abs({b} v) => {F}.Abs(v); public static {b} sign({b} v) => v>0?1:(v<0?-1:0);")
        for f,impl in [("floor","Floor"),("ceil","Ceiling"),("sqrt","Sqrt"),("sin","Sin"),("cos","Cos"),("tan","Tan"),("exp","Exp"),("log","Log"),("atan","Atan"),("asin","Asin"),("acos","Acos"),("trunc","Truncate")]:
            M.append(f" public static {b} {f}({b} v) => {F}.{impl}(v);")
        M.append(f" public static {b} round({b} v) => {F}.Round(v, MidpointRounding.ToEven);")
        M.append(f" public static {b} log2({b} v) => {F}.Log(v, 2); public static {b} exp2({b} v) => {F}.Pow(2, v); public static {b} rsqrt({b} v) => 1/{F}.Sqrt(v); public static {b} rcp({b} v) => 1/v;")
        M.append(f" public static {b} frac({b} v) => v - {F}.Floor(v); public static {b} saturate({b} v) => clamp(v, 0, 1);")
        M.append(f" public static {b} radians({b} v) => v * ({b})0.0174532925199432957; public static {b} degrees({b} v) => v * ({b})57.295779513082320876;")
        M.append(f" public static {b} pow({b} a, {b} b) => {F}.Pow(a, b); public static {b} fmod({b} a, {b} b) => a % b; public static {b} atan2({b} y, {b} x) => {F}.Atan2(y, x); public static {b} step({b} e, {b} x) => x >= e ? 1 : 0;")
        M.append(f" public static {b} lerp({b} a, {b} b, {b} t) => a + t * (b - a);")
        M.append(f" public static {b} unlerp({b} a, {b} b, {b} x) => (x - a) / (b - a);")
        M.append(f" public static {b} remap({b} a, {b} b, {b} c, {b} d, {b} x) => lerp(c, d, unlerp(a, b, x));")
        M.append(f" public static {b} smoothstep({b} a, {b} b, {b} x) {{ var t = saturate((x - a) / (b - a)); return t * t * (3 - 2 * t); }}")
        M.append(f" public static {b} distance({b} a, {b} b) => abs(b - a);")
        M.append(f" public static bool isnan({b} v) => {b}.IsNaN(v); public static bool isfinite({b} v) => !{b}.IsNaN(v) && !{b}.IsInfinity(v); public static bool isinf({b} v) => {b}.IsInfinity(v);")
    for n in (2,3,4):
        t=f"{b}{n}"; c=comps[:n]
        M.append(f" public static {t} max({t} a, {t} b) => {comp(b,n,lambda x: f'max(a.{x}, b.{x})')};")
        M.append(f" public static {t} min({t} a, {t} b) => {comp(b,n,lambda x: f'min(a.{x}, b.{x})')};")
        M.append(f" public static {t} clamp({t} v, {t} a, {t} b) => max(a, min(b, v));")
        M.append(f" public static {t} abs({t} v) => {comp(b,n,lambda x: f'abs(v.{x})')};")
        M.append(f" public static {t} sign({t} v) => {comp(b,n,lambda x: f'sign(v.{x})')};")
        M.append(f" public static {b} dot({t} a, {t} b) => ({b})({' + '.join(f'a.{x}*b.{x}' for x in c)});")
        M.append(f" public static {b} csum({t} a) => ({b})({' + '.join(f'a.{x}' for x in c)});")
        M.append(f" public static {b} cmax({t} a) => {'max('*(n-1)}a.x{''.join(f', a.{x})' for x in c[1:])};")
        M.append(f" public static {b} cmin({t} a) => {'min('*(n-1)}a.x{''.join(f', a.{x})' for x in c[1:])};")
        if b in ("float","double"):
            for f in ["floor","ceil","round","saturate","sqrt","rsqrt","sin","cos","tan","exp","log","log2","frac","trunc","radians","degrees","exp2","atan","asin","acos","rcp"]:
                M.append(f" public static {t} {f}({t} v) => {comp(b,n,lambda x,f=f: f'{f}(v.{x})')};")
            for f in ["pow","fmod","atan2","step"]:
                M.append(f" public static {t} {f}({t} a, {t} b) => {comp(b,n,lambda x,f=f: f'{f}(a.{x}, b.{x})')};")
            M.append(f" public static {t} lerp({t} a, {t} b, {b} t) => a + t * (b - a);")
            M.append(f" public static {t} lerp({t} a, {t} b, {t} t) => a + t * (b - a);")
            M.append(f" public static {t} smoothstep({t} a, {t} b, {t} x) => {comp(b,n,lambda x: f'smoothstep(a.{x}, b.{x}, x.{x})')};")
            M.append(f" public static {t} unlerp({t} a, {t} b, {t} x) => (x - a) / (b - a);")
            M.append(f" public static {t} remap({t} a, {t} b, {t} c, {t} d, {t} x) => lerp(c, d, unlerp(a, b, x));")
            M.append(f" public static {b} lengthsq({t} v) => dot(v, v); public static {b} length({t} v) => sqrt(dot(v, v));")
            M.append(f" public static {b} distance({t} a, {t} b) => length(b - a); public static {b} distancesq({t} a, {t} b) => lengthsq(b - a);")
            M.append(f" public static {t} normalize({t} v) => v * rsqrt(dot(v, v));")
            M.append(f" public static {t} normalizesafe({t} v, {t} d = default) {{ var l = dot(v, v); return l > ({b})1e-30 ? v * rsqrt(l) : d; }}")
    if b in ("int","uint"):
        M.append(f" public static {b} rol({b} v, int n) => ({b})(((uint)v << n) | ((uint)v >> (32 - n)));")
        M.append(f" public static {b} ror({b} v, int n) => ({b})(((uint)v >> n) | ((uint)v << (32 - n)));")
        M.append(f" public static {b} ceilpow2({b} v) {{ uint x = (uint)v; x -= 1; x |= x >> 1; x |= x >> 2; x |= x >> 4; x |= x >> 8; x |= x >> 16; return ({b})(x + 1); }}")
        M.append(f" public static int countbits({b} v) {{ uint x=(uint)v; int c=0; while(x!=0){{ c+=(int)(x&1); x>>=1; }} return c; }}")
        M.append(f" public static int lzcnt({b} v) {{ uint x=(uint)v; if(x==0) return 32; int n=0; while((x & 0x80000000u)==0){{ n++; x<<=1; }} return n; }}")
        M.append(f" public static int tzcnt({b} v) {{ uint x=(uint)v; if(x==0) return 32; int n=0; while((x&1)==0){{ n++; x>>=1; }} return n; }}")
        M.append(f" public static bool ispow2({b} v) => v > 0 && (v & (v - 1)) == 0;")
for n in (2,3,4):
    c=comps[:n]
    M.append(f" public static bool any(bool{n} v) => {' || '.join('v.'+x for x in c)}; public static bool all(bool{n} v) => {' && '.join('v.'+x for x in c)};")
    M.append(f" public static uint hash(uint{n} v) {{ uint h = 0x9E3779B9u; {' '.join(f'h = (h ^ v.{x}) * 0x85EBCA6Bu; h ^= h >> 13;' for x in c)} return h; }}")
    M.append(f" public static uint hash(int{n} v) => hash((uint{n})v); public static uint hash(float{n} v) => hash(new uint{n}({', '.join('asuint(v.'+x+')' for x in c)}));")
    M.append(f" public static uint hash(double{n} v) => hash((float{n})v);")
    M.append(f" public static uint hash(bool{n} v) => hash(new uint{n}({', '.join('v.'+x+'?1u:0u' for x in c)}));")
    for b in ["float","int","uint","double"]:
        M.append(f" public static {b}{n} select({b}{n} a, {b}{n} b, bool{n} s) => new {b}{n}({', '.join(f's.{x}?b.{x}:a.{x}' for x in c)});")
        M.append(f" public static {b}{n} select({b}{n} a, {b}{n} b, bool s) => s ? b : a;")
    for b in bases:
        args=", ".join(f"{b} {x}" for x in c)
        M.append(f" public static {b}{n} {b}{n}({args}) => new {b}{n}({', '.join(c)});")
        M.append(f" public static {b}{n} {b}{n}({b} v) => new {b}{n}(v);")
        if n==3: M.append(f" public static {b}{n} {b}{n}({b}2 xy, {b} z) => new {b}{n}(xy, z);")
        if n==4: M.append(f" public static {b}{n} {b}{n}({b}3 xyz, {b} w) => new {b}{n}(xyz, w); public static {b}{n} {b}{n}({b}2 xy, {b}2 zw) => new {b}{n}(xy, zw); public static {b}{n} {b}{n}({b}2 xy, {b} z, {b} w) => new {b}{n}(xy, z, w);")
    for b in ["float","int","uint","double"]:
        for o in ["float","int","uint","double"]:
            if o!=b: M.append(f" public static {b}{n} {b}{n}({o}{n} v) => ({b}{n})v;")
for b in ["float","int","uint","double"]:
    M.append(f" public static {b} select({b} a, {b} b, bool s) => s ? b : a;")
M.append(" public static uint hash(uint v) { v ^= v >> 16; v *= 0x7feb352dU; v ^= v >> 15; v *= 0x846ca68bU; v ^= v >> 16; return v; }")
M.append(" public static unsafe uint asuint(int v) => (uint)v; public static unsafe uint asuint(float v) => *(uint*)&v; public static int asint(uint v) => (int)v; public static unsafe int asint(float v) => *(int*)&v; public static unsafe float asfloat(uint v) => *(float*)&v; public static unsafe float asfloat(int v) => *(float*)&v;")
M.append(" public static unsafe uint hash(void* p, int bytes, uint seed = 0) { byte* b = (byte*)p; uint h = 2166136261u ^ seed; for (int i = 0; i < bytes; i++) h = (h ^ b[i]) * 16777619u; return h; }")
M.append("}")
out+=M
out.append(r'''
public static class noise {
  // Classic 2D simplex noise (Gustavson) - same range [-1,1] as Unity.Mathematics.noise.snoise.
  static readonly int[] perm = BuildPerm();
  static int[] BuildPerm(){ var p=new int[512]; var src=new int[256]; for(int i=0;i<256;i++) src[i]=i; uint s=1234567u; for(int i=255;i>0;i--){ s=s*1664525u+1013904223u; int j=(int)(s%(uint)(i+1)); int t=src[i]; src[i]=src[j]; src[j]=t; } for(int i=0;i<512;i++) p[i]=src[i&255]; return p; }
  static readonly float[] gx={1,-1,1,-1,1,-1,0,0}, gy={1,1,-1,-1,0,0,1,-1};
  public static float snoise(float2 v){ const float F2=0.3660254f, G2=0.21132487f; float xin=v.x, yin=v.y; float s=(xin+yin)*F2; int i=(int)MathF.Floor(xin+s), j=(int)MathF.Floor(yin+s); float t=(i+j)*G2; float x0=xin-(i-t), y0=yin-(j-t); int i1=x0>y0?1:0, j1=x0>y0?0:1; float x1=x0-i1+G2, y1=y0-j1+G2, x2=x0-1+2*G2, y2=y0-1+2*G2; int ii=i&255, jj=j&255; float n=0; float t0=0.5f-x0*x0-y0*y0; if(t0>0){ int g=perm[ii+perm[jj]]&7; t0*=t0; n+=t0*t0*(gx[g]*x0+gy[g]*y0);} float t1=0.5f-x1*x1-y1*y1; if(t1>0){ int g=perm[ii+i1+perm[jj+j1]]&7; t1*=t1; n+=t1*t1*(gx[g]*x1+gy[g]*y1);} float t2=0.5f-x2*x2-y2*y2; if(t2>0){ int g=perm[ii+1+perm[jj+1]]&7; t2*=t2; n+=t2*t2*(gx[g]*x2+gy[g]*y2);} return 70f*n; }
  public static float snoise(float3 v) => snoise(new float2(v.x + v.z * 0.7f, v.y - v.z * 0.3f));
  public static float cnoise(float2 v) => snoise(v);
}
public struct Random {
  public uint state;
  public Random(uint seed){ state=seed; NextState(); }
  public static Random CreateFromIndex(uint i) => new Random(math.hash(i) | 1);
  uint NextState(){ uint t=state; state^=state<<13; state^=state>>17; state^=state<<5; return t; }
  public void InitState(uint seed = 0x6E624EB7u){ state=seed; NextState(); }
  public bool NextBool() => (NextState() & 1) == 1;
  public int NextInt() => (int)NextState() ^ -2147483648;
  public int NextInt(int max) => max <= 0 ? 0 : (int)((NextState() * (ulong)max) >> 32);
  public int NextInt(int min, int max) => min + NextInt(max - min);
  public uint NextUInt() => NextState() - 1u;
  public uint NextUInt(uint max) => (uint)((NextState() * (ulong)max) >> 32);
  public float NextFloat() => math.asfloat(0x3f800000 | (NextState() >> 9)) - 1.0f;
  public float NextFloat(float max) => NextFloat() * max;
  public float NextFloat(float min, float max) => NextFloat() * (max - min) + min;
  public float2 NextFloat2() => new float2(NextFloat(), NextFloat());
  public float2 NextFloat2(float2 min, float2 max) => NextFloat2() * (max - min) + min;
  public int2 NextInt2(int2 min, int2 max) => new int2(NextInt(min.x, max.x), NextInt(min.y, max.y));
}
public struct float4x4 { public float4 c0,c1,c2,c3; public static readonly float4x4 identity = new float4x4{c0=new float4(1,0,0,0),c1=new float4(0,1,0,0),c2=new float4(0,0,1,0),c3=new float4(0,0,0,1)}; }
public struct quaternion { public float4 value; public static readonly quaternion identity = new quaternion{value=new float4(0,0,0,1)}; }
}''')
import sys; open(sys.argv[1],"w").write("\n".join(out))
