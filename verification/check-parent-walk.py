"""Compile the actual cutscene parent-walk method against a minimal Transform shim."""
from pathlib import Path
import subprocess
root = Path(__file__).resolve().parents[1]
source = (root / "icebreaker-client/IcebreakerCombineCutscene.cs").read_text(encoding="utf-8-sig")
start = source.index("            internal bool TryFindNearestSourceAnchor(")
end = source.index("            internal List<GameObject> ConsumeNewHelpers()", start)
method = source[start:end]
stage = root / "verification/obj/parent-walk-check"
stage.mkdir(parents=True, exist_ok=True)
(stage / "check.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>', encoding="utf-8")
prefix = r'''
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
class Transform {
    public string name = "node";
    public Transform parent;
    public static implicit operator bool(Transform value) => value is not null;
}
class Walker {
    internal Dictionary<Transform, Transform> _sourceToActor = new();
    internal Dictionary<string, Transform> _coreByName = new();
'''
suffix = r'''
}
class Program {
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static bool Walk(Walker w, Transform source) => w.TryFindNearestSourceAnchor(source, out _, out _);
    static bool Bounded(Walker w, Transform source) {
        var task = Task.Run(() => Walk(w, source));
        Check(task.Wait(500), "parent walk terminates within 500ms");
        return task.Result;
    }
    static void Main() {
        var w = new Walker();
        Check(!Walk(w, null), "null source has no anchor");
        var anchor = new Transform { name = "anchor" };
        var actor = new Transform(); w._sourceToActor[anchor] = actor;
        Check(Walk(w, new Transform { parent = anchor }), "mapped parent found");
        var named = new Transform { name = "named" }; w._coreByName["named"] = actor;
        Check(Walk(w, named), "named anchor found and cached");
        var self = new Transform(); self.parent = self;
        Check(!Bounded(w, self), "self-cycle safely rejected");
        var a = new Transform(); var b = new Transform(); a.parent = b; b.parent = a;
        Check(!Bounded(w, a), "two-node cycle safely rejected");
        var deep = anchor; for (int i = 0; i < 300; i++) deep = new Transform { parent = deep };
        Check(!Bounded(w, deep), "over-depth hierarchy safely rejected");
        Console.WriteLine("Parent-walk source-linked checks passed.");
    }
}
'''
(stage / "Program.cs").write_text(prefix + method + suffix, encoding="utf-8")
raise SystemExit(subprocess.run(["dotnet", "run", "--project", str(stage / "check.csproj"), "-c", "Release", "--nologo"]).returncode)
