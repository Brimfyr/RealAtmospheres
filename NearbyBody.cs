using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RealAtmospheres;

/// <summary>Which body a camera is near. The game draws one body in detail for each camera (terrain, volumetric
/// clouds, the full atmosphere) and every other as a distant one, and picks it with
/// CelestialSystem.GetNearbyAstronomical. That walks the bodies and takes each whose SURFACE is nearer than the last
/// pick's CENTRE. Above a planet with a small moon, the moon so takes the planet's place whenever it comes nearer
/// than the planet's centre: from 8,400 km of Mars's centre Phobos does once an orbit, and for that part of it Mars
/// is drawn as a distant body, without terrain or clouds, with a thicker-looking atmosphere and a higher framerate.
/// Under time warp that is a flicker (user, 2026-10-04: every 2 s for 0.5 s at x14400, where Phobos's 7.65 h orbit
/// takes 1.9 s). Deimos, Pan, Miranda and Charon do the same to their planets.
///
/// Comparing surface with surface would not end it: Phobos passes nearer than Mars's surface as well. So a body
/// counts first once the camera is within NearRadii of its own radii, and among bodies that count alike the nearest
/// surface wins. Above Mars that leaves Mars; within 282 km of Phobos, where it spans 4.6 degrees, Phobos takes
/// over. The game's own limit (a surface within nearbyDistance) stays. The comparison is replaced in place by a
/// transpiler, as the method runs for every camera each frame; if its code has changed, the game's is left alone.
/// </summary>
internal static class NearbyBody
{
    /// <summary>A body counts ahead of the others within this many of its own radii, where it spans 4.6 degrees.</summary>
    public const double NearRadii = 25.0;

    public static bool Patched { get; private set; }

    public static void Install(Harmony harmony)
    {
        Type? system = AccessTools.TypeByName("KSA.CelestialSystem");
        MethodInfo? nearby = system == null ? null : AccessTools.Method(system, "GetNearbyAstronomical");
        if (nearby == null || nearby.IsStatic || RangeArgument(nearby) < 0)
        {
            ShadowBuilder.Log("WARN: CelestialSystem.GetNearbyAstronomical(.., nearbyDistance) not found; a small moon can still take its planet's place as the nearby body");
            return;
        }
        try
        {
            harmony.Patch(nearby, transpiler: new HarmonyMethod(typeof(NearbyBody), nameof(Transpiler)));
        }
        catch (Exception e)
        {
            ShadowBuilder.Log($"WARN: could not patch the nearby body's choice ({e.GetType().Name}); a small moon can still take its planet's place");
            return;
        }
        ShadowBuilder.Log(Patched
            ? $"nearby body: a body within {NearRadii:0} of its radii counts first, then the nearest surface"
            : "WARN: the nearby body's comparison has changed; a small moon can still take its planet's place");
    }

    /// <summary>Whether a body takes over as the nearby one. <paramref name="best"/> holds the rank of the pick so
    /// far, lower being better: the distance to its surface, plus the range for a body the camera is not within
    /// NearRadii of, which puts every such body behind every one it is. <paramref name="none"/>: nothing picked
    /// yet, when best still holds the game's starting value.</summary>
    public static bool Take(double centreDistance, double radius, ref double best, double range, bool none)
    {
        double surface = centreDistance - radius;
        if (!(surface < range))
            return false;
        double rank = centreDistance <= NearRadii * radius ? surface : surface + range;
        if (!none && !(rank < best))
            return false;
        best = rank;
        return true;
    }

    /// <summary>
    /// The game's test and bookkeeping, with dist the body's centre distance and best the pick's:
    ///     if (dist - body.MeanRadius &lt; best) { result = body; best = dist; }
    /// becomes
    ///     if (Take(dist, body.MeanRadius, ref best, nearbyDistance, result == null)) { result = body; }
    /// </summary>
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        List<CodeInstruction> code = instructions.ToList();
        int range = RangeArgument(original);
        int[] radius = code.Select((c, i) => IsMeanRadius(c) ? i : -1).Where(i => i >= 0).ToArray();
        if (range < 0 || radius.Length != 1)
            return code;
        int at = radius[0];
        // dist, body, MeanRadius, sub, best, clt
        if (at < 2 || at + 3 >= code.Count || code[at + 1].opcode != OpCodes.Sub || code[at + 3].opcode != OpCodes.Clt)
            return code;
        int dist = LoadedLocal(code[at - 2]), body = LoadedLocal(code[at - 1]), best = LoadedLocal(code[at + 2]);
        if (dist < 0 || body < 0 || best < 0)
            return code;
        // result = body; best = dist;
        int keep = -1;
        for (int i = at + 4; i + 3 < code.Count && keep < 0; i++)
            if (LoadedLocal(code[i]) == body && StoredLocal(code[i + 1]) >= 0
                && LoadedLocal(code[i + 2]) == dist && StoredLocal(code[i + 3]) == best)
                keep = i;
        if (keep < 0)
            return code;
        int result = StoredLocal(code[keep + 1]);
        // nothing may jump into what is replaced
        if (new[] { at + 1, at + 2, at + 3, keep + 2, keep + 3 }.Any(i => code[i].labels.Count > 0 || code[i].blocks.Count > 0))
            return code;

        code[keep + 2] = new CodeInstruction(OpCodes.Nop);
        code[keep + 3] = new CodeInstruction(OpCodes.Nop);
        code.RemoveRange(at + 1, 3);
        code.InsertRange(at + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldloca_S, (byte)best),
            new CodeInstruction(OpCodes.Ldarg_S, (byte)range),
            new CodeInstruction(OpCodes.Ldloc_S, (byte)result),
            new CodeInstruction(OpCodes.Ldnull),
            new CodeInstruction(OpCodes.Ceq),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NearbyBody), nameof(Take))),
        });
        Patched = true;
        return code;
    }

    /// <summary>The argument slot of the method's nearbyDistance, or -1 if it has none.</summary>
    private static int RangeArgument(MethodBase method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        int at = Array.FindIndex(parameters, p => p.Name == "nearbyDistance" && p.ParameterType == typeof(double));
        return at < 0 ? -1 : at + (method.IsStatic ? 0 : 1);
    }

    private static bool IsMeanRadius(CodeInstruction c) =>
        (c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call) && c.operand is MethodInfo m
        && m.Name == "get_MeanRadius" && m.ReturnType == typeof(double);

    private static int LoadedLocal(CodeInstruction c) =>
        c.opcode == OpCodes.Ldloc_0 ? 0 : c.opcode == OpCodes.Ldloc_1 ? 1 : c.opcode == OpCodes.Ldloc_2 ? 2 : c.opcode == OpCodes.Ldloc_3 ? 3
        : c.opcode == OpCodes.Ldloc_S || c.opcode == OpCodes.Ldloc ? Index(c.operand) : -1;

    private static int StoredLocal(CodeInstruction c) =>
        c.opcode == OpCodes.Stloc_0 ? 0 : c.opcode == OpCodes.Stloc_1 ? 1 : c.opcode == OpCodes.Stloc_2 ? 2 : c.opcode == OpCodes.Stloc_3 ? 3
        : c.opcode == OpCodes.Stloc_S || c.opcode == OpCodes.Stloc ? Index(c.operand) : -1;

    /// <summary>A local's slot, if it fits the short forms the replacement is written in.</summary>
    private static int Index(object? operand)
    {
        int index = operand is LocalVariableInfo local ? local.LocalIndex : operand is IConvertible number ? Convert.ToInt32(number) : -1;
        return index is >= 0 and <= byte.MaxValue ? index : -1;
    }
}
