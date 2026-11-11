using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class MergeChecks
{
    internal static void Run(AssemblyDefinition client)
    {
        var types = Flatten(client.MainModule.Types).ToArray();
        var legacy = types.Where(t => t.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")).ToArray();
        if (legacy.Length != 0)
            throw new Exception("Raid lifecycle cannot discover legacy zzap patches: " + string.Join(", ", legacy.Select(t => t.FullName)));
        Console.WriteLine("PASS All zzap patches use the scoped raid lifecycle");
        var lifecycle = types.Single(t => t.Name == "IcebreakerPatchLifecycle");
        var register = lifecycle.Methods.Single(m => m.Name == "RegisterOptionalRaidPatches");
        if (!register.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == "OrbitBrainLayerCompat"))
            throw new Exception("ORBIT compatibility is not registered before raid bot creation");
        Console.WriteLine("PASS ORBIT compatibility registers before raid bot creation");
        var release = types.Single(t => t.Name == "IcebreakerRaidResources").Methods.Single(m => m.Name == "Release" && m.Parameters.Count == 0);
        foreach (var owner in new[] { "Patch_BotStartCorePoint", "IcebreakerWedgeDiag" })
            if (!release.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == owner && m.Name == "ReleaseRaid"))
                throw new Exception("Fork raid cache is not released: " + owner);
        Console.WriteLine("PASS zzap AI-core and wedge diagnostic caches release after the raid");
    }

    private static IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in Flatten(type.NestedTypes)) yield return nested;
        }
    }
}
