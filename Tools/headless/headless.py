#!/usr/bin/env python3
"""Offline build + headless run harness for PixelGenesis (no Unity install needed).

  python3 Tools/headless/headless.py check          # type-check every runtime asmdef against UnityEngine 2021.3 reference
                                                    # assemblies + signature stubs for Burst/Collections/InputSystem
  python3 Tools/headless/headless.py run <cmd> ...  # run the simulation layers headless on managed shims
      cmd: determinism [ticks] | headless [years] | soak | units | feed | tests | civ [years] ...

Limits: 'check' cannot see Unity 6-only APIs (a stub may be missing) and does not compile Editor/Test assemblies.
'run' timings come from the .NET JIT, not Burst: use them only to compare before/after. Unity stays the source of truth.
Needs: dotnet SDK 8 and network access to api.nuget.org (first run only).
"""
import glob, json, os, subprocess, sys, urllib.request, zipfile
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ROOT = os.path.join(REPO, "Assets", "_Project")
BUILD = os.path.join(HERE, ".build")
PKGS = {"unityengine.modules": "2021.3.33", "newtonsoft.json": "13.0.3"}
SKIP_CHECK = {"PG.Editor", "PG.Tests.EditMode", "PG.Tests.PlayMode"}
NOWARN = "CS0169;CS0414;CS0649;CS1591;CS0067;CS8632;CS2008"

def fetch():
    for pid, ver in PKGS.items():
        d = os.path.join(BUILD, "pkgs", pid)
        if os.path.isdir(d): continue
        os.makedirs(d, exist_ok=True)
        nupkg = d + ".nupkg"
        urllib.request.urlretrieve(f"https://api.nuget.org/v3-flatcontainer/{pid}/{ver}/{pid}.{ver}.nupkg", nupkg)
        zipfile.ZipFile(nupkg).extractall(d)

def unity_dlls():
    core = glob.glob(os.path.join(BUILD, "pkgs", "unityengine.modules", "**", "UnityEngine.CoreModule.dll"), recursive=True)[0]
    return glob.glob(os.path.join(os.path.dirname(core), "*.dll"))

def newtonsoft():
    return glob.glob(os.path.join(BUILD, "pkgs", "newtonsoft.json", "lib", "netstandard2.0", "Newtonsoft.Json.dll"))[0]

def math_stub():
    out = os.path.join(BUILD, "gen", "Mathematics.cs")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    subprocess.check_call([sys.executable, os.path.join(HERE, "gen_math.py"), out])
    return out

def ref(path):
    n = os.path.splitext(os.path.basename(path))[0]
    return f'<Reference Include="{n}"><HintPath>{path}</HintPath><Private>false</Private></Reference>'

def props(extra=""):
    return (f"<PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><LangVersion>9.0</LangVersion><AllowUnsafeBlocks>true</AllowUnsafeBlocks>"
            f"<EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable><NoWarn>{NOWARN}</NoWarn>"
            f"<DefineConstants>UNITY_2021_3_OR_NEWER;UNITY_6000_0_OR_NEWER;ENABLE_INPUT_SYSTEM;UNITY_STANDALONE</DefineConstants>"
            f"<GenerateAssemblyInfo>false</GenerateAssemblyInfo>{extra}</PropertyGroup>")

def asmdefs():
    res = {}
    for f in glob.glob(os.path.join(ROOT, "**", "*.asmdef"), recursive=True):
        d = json.load(open(f)); res[d["name"]] = (os.path.dirname(f), d)
    return res

def files_of(d, dirs):
    out = []
    for p in glob.glob(os.path.join(d, "**", "*.cs"), recursive=True):
        pd = os.path.dirname(p)
        while pd not in dirs and pd.startswith(ROOT): pd = os.path.dirname(pd)
        if pd == d: out.append(p)
    return sorted(out)

def check():
    fetch(); ms = math_stub()
    proj = os.path.join(BUILD, "check"); os.makedirs(proj, exist_ok=True)
    refs = "".join(ref(p) for p in unity_dlls())
    open(os.path.join(proj, "Stubs.csproj"), "w").write(
        f'<Project Sdk="Microsoft.NET.Sdk">{props("<AssemblyName>UnityStubs</AssemblyName><LangVersion>latest</LangVersion>")}'
        f'<ItemGroup><Compile Include="{ms}"/><Compile Include="{HERE}/stubs/*.cs"/></ItemGroup><ItemGroup>{refs}</ItemGroup></Project>')
    a = asmdefs(); dirs = {v[0] for v in a.values()}; names = []
    for name, (d, j) in a.items():
        if name in SKIP_CHECK: continue
        names.append(name)
        comp = "".join(f'<Compile Include="{p}"/>' for p in files_of(d, dirs))
        prs = "".join(f'<ProjectReference Include="{r}.csproj"/>' for r in j.get("references", []) if r.startswith("PG.") and r not in SKIP_CHECK)
        open(os.path.join(proj, name + ".csproj"), "w").write(
            f'<Project Sdk="Microsoft.NET.Sdk">{props(f"<AssemblyName>{name}</AssemblyName>")}<ItemGroup>{comp}</ItemGroup>'
            f'<ItemGroup><ProjectReference Include="Stubs.csproj"/>{prs}</ItemGroup><ItemGroup>{refs}{ref(newtonsoft())}</ItemGroup></Project>')
    # a leaf project that references every assembly
    open(os.path.join(proj, "All.csproj"), "w").write(
        f'<Project Sdk="Microsoft.NET.Sdk">{props("<AssemblyName>All</AssemblyName>")}<ItemGroup>'
        + "".join(f'<ProjectReference Include="{n}.csproj"/>' for n in names) + f'</ItemGroup><ItemGroup>{refs}</ItemGroup></Project>')
    return build(os.path.join(proj, "All.csproj"), [])

def build(csproj, extra):
    r = subprocess.run(["dotnet", "build", csproj, "-nologo", "-v", "q", "-clp:NoSummary"] + extra, capture_output=True, text=True)
    lines = sorted({l.replace(ROOT + "/", "").split(" [")[0] for l in (r.stdout + r.stderr).splitlines() if " error " in l or " warning CS" in l})
    for l in lines: print(l)
    ok = r.returncode == 0
    print(f"[headless] build {'OK' if ok else 'FAILED'}: {sum(' error ' in l for l in lines)} error(s), {sum('warning' in l for l in lines)} warning(s)")
    return 0 if ok else 1

RUN_LAYERS = ["Core", "Content", "World", "WorldGen", "Sim", "Powers", "Persistence"]
RUN_EXTRA = ["Boot/Diagnostics.cs", "Editor/NatureSoak.cs", "Editor/UnitAcceptance.cs", "Editor/CivSoak.cs", "Editor/MetaSoak.cs"]

def run(args):
    fetch(); ms = math_stub()
    files = []
    for l in RUN_LAYERS: files += glob.glob(os.path.join(ROOT, l, "**", "*.cs"), recursive=True)
    files += [os.path.join(ROOT, f) for f in RUN_EXTRA if os.path.exists(os.path.join(ROOT, f))]
    files += glob.glob(os.path.join(HERE, "shim", "*.cs")) + glob.glob(os.path.join(HERE, "runner", "*.cs")) + [ms]
    files += glob.glob(os.path.join(ROOT, "Tests", "Headless", "*.cs"))
    files = [f for f in files if not f.endswith("AssemblyInfo.cs")]
    proj = os.path.join(BUILD, "run"); os.makedirs(proj, exist_ok=True)
    comp = "".join(f'<Compile Include="{f}"/>' for f in sorted(files))
    open(os.path.join(proj, "Run.csproj"), "w").write(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><LangVersion>latest</LangVersion>'
        f'<AllowUnsafeBlocks>true</AllowUnsafeBlocks><EnableDefaultCompileItems>false</EnableDefaultCompileItems><Nullable>disable</Nullable><ImplicitUsings>disable</ImplicitUsings>'
        f'<NoWarn>{NOWARN};CS0162;CS0618;CS0436</NoWarn><Optimize>true</Optimize><InvariantGlobalization>true</InvariantGlobalization></PropertyGroup>'
        f'<ItemGroup>{comp}</ItemGroup><ItemGroup>{ref(newtonsoft()).replace("<Private>false</Private>", "")}</ItemGroup></Project>')
    rc = build(os.path.join(proj, "Run.csproj"), ["-c", "Release", "-o", os.path.join(proj, "out")])
    if rc: return rc
    env = dict(os.environ, PG_REPO=REPO, PG_BUILD=BUILD)
    return subprocess.call(["dotnet", os.path.join(proj, "out", "Run.dll")] + args, env=env)

if __name__ == "__main__":
    if len(sys.argv) < 2: print(__doc__); sys.exit(2)
    sys.exit(check() if sys.argv[1] == "check" else run(sys.argv[2:]) if sys.argv[1] == "run" else 2)
