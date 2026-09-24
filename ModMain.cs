using System.Reflection;
using HarmonyLib;
using StarMap.API;

namespace RealAtmospheres;

/// <summary>
/// RealAtmospheres - the Mars atmosphere realism overhaul as a deployable StarMap
/// mod. At [StarMapBeforeMain] (invoked by StarMap before the game's Main, i.e.
/// before any content loads) it builds a patched shadow copy of the relevant
/// Core content inside the mod folder, then installs two Harmony prefixes that
/// redirect the game's file reads into it:
///
///   KSA.Mod.LoadAssetBundles (+EditorTags) - swaps Core's Astronomicals.xml entry
///   RenderCore.ShaderModuleUtils.FromFile  - remaps Content\Core\Shaders\* paths
///
/// No game files are modified, no elevation is needed, and a game update simply
/// changes the source the shadow is rebuilt from at next launch. All KSA/engine
/// access is via reflection so the build only references StarMap.API + Harmony.
/// </summary>
[StarMapMod]
public class ModMain
{
    [StarMapBeforeMain]
    public void BeforeMain()
    {
        try
        {
            Install();
        }
        catch (Exception ex)
        {
            ShadowBuilder.Log("FAILED to install (game will run stock): " + ex);
        }
    }

    private static void Install()
    {
        string modDir = Path.GetDirectoryName(typeof(ModMain).Assembly.Location)!;

        Type modType = AccessTools.TypeByName("KSA.Mod")
            ?? throw new InvalidOperationException("KSA.Mod not found");

        string installRoot = Path.GetDirectoryName(modType.Assembly.Location)!;

        // Planet.Render.Core loads lazily and is not in the process yet at
        // BeforeMain: pre-load it into KSA's own load context so the instance
        // we patch is the one the game will use.
        Type? shaderUtils = AccessTools.TypeByName("RenderCore.ShaderModuleUtils");
        if (shaderUtils == null)
        {
            var alc = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(modType.Assembly)
                ?? System.Runtime.Loader.AssemblyLoadContext.Default;
            var prc = alc.LoadFromAssemblyPath(Path.Combine(installRoot, "Planet.Render.Core.dll"));
            shaderUtils = prc.GetType("RenderCore.ShaderModuleUtils")
                ?? throw new InvalidOperationException("RenderCore.ShaderModuleUtils not found");
        }
        string coreDir = Path.Combine(installRoot, "Content", "Core");
        if (!Directory.Exists(coreDir))
            throw new DirectoryNotFoundException("Core content not found at " + coreDir);

        ShadowBuilder.Build(coreDir, modDir);

        var harmony = new Harmony("gunshy.realatmospheres");

        // XML seam: prefix the two non-generic consumers of Mod.Assets and swap
        // Core's Astronomicals entry for the shadow's absolute path. Never patch
        // the generic XmlLoader.Load<T> itself: shared generic code means a
        // patched Load<AssetBundle> hijacks EVERY reference-type instantiation
        // with T=AssetBundle and breaks all other deserialization (learned the
        // hard way - it crashed the game).
        var assetsPrefix = new HarmonyMethod(typeof(Patches), nameof(Patches.ModAssetsPrefix));
        foreach (string name in new[] { "LoadAssetBundles", "LoadEditorTagDefinitions" })
        {
            MethodInfo m = AccessTools.Method(modType, name)
                ?? throw new InvalidOperationException($"Mod.{name} not found");
            harmony.Patch(m, prefix: assetsPrefix);
        }

        // System templates (Titan lives inline in SolSystem/SolSystemDense.xml):
        // same permanent-swap trick on Mod.SystemTemplates. PrepareSystems sets
        // SystemInfo.XmlName = the entry string, so swapping BEFORE both
        // consumers keeps XmlName comparisons self-consistent everywhere.
        var systemsPrefix = new HarmonyMethod(typeof(Patches), nameof(Patches.ModSystemsPrefix));
        foreach (string name in new[] { "LoadSystems", "PrepareSystems" })
        {
            MethodInfo m = AccessTools.Method(modType, name)
                ?? throw new InvalidOperationException($"Mod.{name} not found");
            harmony.Patch(m, prefix: systemsPrefix);
        }

        MethodInfo fromFile = shaderUtils
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "FromFile"
                && m.GetParameters().Length == 4
                && m.GetParameters()[1].ParameterType == typeof(string))
            ?? throw new InvalidOperationException("ShaderModuleUtils.FromFile(4) not found");
        harmony.Patch(fromFile,
            prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.ShaderFromFilePrefix)));

        // Sim-linked haze animation: while the renderer builds a haze body's atmosphere data, its
        // ozone extent reads as elapsed SIM seconds, which the haze march takes as its clock
        // (freezes on pause, scales with time warp). Fail-soft: without the method the shader
        // runs on the body's own extent or wall-clock time.
        Type? atmoRenderer = AccessTools.TypeByName("KSA.AtmosphereRenderer");
        MethodInfo? fillAtmo = atmoRenderer == null ? null : AccessTools.Method(atmoRenderer, "FillPlanetAtmosphereData");
        if (fillAtmo != null && Patches.CanInjectSimTime(fillAtmo))
        {
            try
            {
                harmony.Patch(fillAtmo,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.FillAtmosphereDataPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.FillAtmosphereDataPostfix)));
            }
            catch (Exception e)
            {
                ShadowBuilder.Log($"WARN: could not hook FillPlanetAtmosphereData ({e.GetType().Name}); haze animation is not sim-linked");
            }
        }
        else
            ShadowBuilder.Log("WARN: AtmosphereRenderer.FillPlanetAtmosphereData(body, atmoRef) not found; haze animation is not sim-linked");

        ShadowBuilder.Log("installed (XML + shader redirects active)");
    }
}

internal static class Patches
{
    private const string ShadersMarker = @"\Content\Core\Shaders\";

    private static System.Reflection.PropertyInfo? _idProp;
    private static System.Reflection.FieldInfo? _assetsField;
    private static bool _loggedXml;

    /// <summary>Prefix on Mod.LoadAssetBundles / Mod.LoadEditorTagDefinitions:
    /// swap the Core mod's "Astronomicals.xml" assets entry for the shadow
    /// copy's absolute path (Path.Combine passes rooted paths through). The
    /// swap persists in the array, so later consumers see it too; the prefix
    /// stays idempotent either way.</summary>
    public static void ModAssetsPrefix(object __instance)
    {
        string? shadow = ShadowBuilder.ShadowAstronomicals;
        if (shadow == null) return;

        _idProp ??= __instance.GetType().GetProperty("Id");
        if (_idProp?.GetValue(__instance) is not string id
            || !id.Equals("Core", StringComparison.OrdinalIgnoreCase))
            return;

        _assetsField ??= AccessTools.Field(__instance.GetType(), "Assets");
        if (_assetsField?.GetValue(__instance) is not string[] assets) return;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] != shadow
                && Path.GetFileName(assets[i]).Equals("Astronomicals.xml", StringComparison.OrdinalIgnoreCase))
            {
                assets[i] = shadow;
                if (!_loggedXml)
                {
                    ShadowBuilder.Log("redirected Core Astronomicals.xml");
                    _loggedXml = true;
                }
            }
        }
    }

    private static System.Reflection.FieldInfo? _systemTemplatesField;
    private static bool _loggedSystems;

    /// <summary>Prefix on Mod.LoadSystems / Mod.PrepareSystems: swap the Core
    /// mod's SolSystem(.Dense).xml entries for the Titan-patched shadow copies
    /// (absolute paths pass through Path.Combine). Permanent and idempotent;
    /// SystemInfo.XmlName then carries the swapped string consistently.</summary>
    public static void ModSystemsPrefix(object __instance)
    {
        if (ShadowBuilder.ShadowSystemFiles.Count == 0) return;

        _idProp ??= __instance.GetType().GetProperty("Id");
        if (_idProp?.GetValue(__instance) is not string id
            || !id.Equals("Core", StringComparison.OrdinalIgnoreCase))
            return;

        _systemTemplatesField ??= AccessTools.Field(__instance.GetType(), "SystemTemplates");
        if (_systemTemplatesField?.GetValue(__instance) is not string[] systems) return;

        for (int i = 0; i < systems.Length; i++)
        {
            if (ShadowBuilder.ShadowSystemFiles.TryGetValue(Path.GetFileName(systems[i]), out string? shadow)
                && systems[i] != shadow)
            {
                systems[i] = shadow;
                if (!_loggedSystems)
                {
                    ShadowBuilder.Log("redirected Core system templates (Titan)");
                    _loggedSystems = true;
                }
            }
        }
    }

    /// <summary>Prefix on ShaderModuleUtils.FromFile: remap any Core shader path
    /// into the shadow tree. Include directives then resolve inside the shadow
    /// tree automatically, because shaderc includes are resolved relative to the
    /// requesting file's directory. ("ShadowContent" contains no
    /// "\Content\Core\Shaders\" substring, so redirected paths never re-match.)</summary>
    public static void ShaderFromFilePrefix(ref string filePath)
    {
        string? shadowRoot = ShadowBuilder.ShadowShadersRoot;
        if (shadowRoot == null) return;
        string full = Normalize(filePath);
        int i = full.IndexOf(ShadersMarker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return;
        string candidate = Path.Combine(shadowRoot, full[(i + ShadersMarker.Length)..]);
        if (File.Exists(candidate))
        {
            filePath = candidate;
            if (!_logged2DCloud && candidate.EndsWith("2DCloud.comp", StringComparison.OrdinalIgnoreCase))
            {
                ShadowBuilder.Log("redirected Clouds/2DCloud.comp (terminator fade live)");
                _logged2DCloud = true;
            }
        }
    }

    private static bool _logged2DCloud;

    // Sim-time injection (cached reflection).
    private static FieldInfo? _distanceValue;
    private static MemberInfo? _visualMember, _ozoneMember, _extentMember;
    private static MethodInfo? _getElapsedSimTime;
    private static bool _simClockLookedUp;
    private static PropertyInfo? _simTimeMinutes;
    private static Type? _universeType;

    /// <summary>
    /// The simulation clock, whichever name this build gives it.
    ///
    /// It was GetElapsedSimTime when this was written and is GetElapsedTime now. Because the
    /// lookup fails soft, the rename cost the haze its sim link silently: it kept animating on
    /// wall-clock time, so it neither froze on pause nor scaled with time-warp, and nothing
    /// said so. Both names are tried, and a build with neither says so in the log.
    /// </summary>
    private static MethodInfo? SimClock()
    {
        if (_simClockLookedUp) return _getElapsedSimTime;
        _simClockLookedUp = true;

        _universeType ??= AccessTools.TypeByName("KSA.Universe");
        if (_universeType != null)
            foreach (string name in new[] { "GetElapsedTime", "GetElapsedSimTime" })
            {
                _getElapsedSimTime = AccessTools.Method(_universeType, name);
                if (_getElapsedSimTime != null) return _getElapsedSimTime;
            }

        ShadowBuilder.Log("WARN: no simulation clock found; haze animation falls back to wall-clock time");
        return null;
    }

    /// <summary>Whether FillPlanetAtmosphereData still takes the two arguments the prefix reads.
    /// Harmony injects them by name and throws at patch time on a missing one, which would take
    /// the rest of the mod down with it, so this is checked before patching.</summary>
    public static bool CanInjectSimTime(MethodInfo fill)
    {
        string?[] names = fill.GetParameters().Select(p => p.Name).ToArray();
        return names.Contains("body") && names.Contains("atmoRef");
    }

    /// <summary>
    /// Prefix on AtmosphereRenderer.FillPlanetAtmosphereData, which builds the atmosphere's
    /// uniform data from the body's AtmosphereReference. For the haze bodies (by mean radius,
    /// the key RaBodyHasHaze uses in the shader) the ozone extent it copies reads as elapsed SIM
    /// seconds for the length of the call. The haze march takes that as its clock, so the haze
    /// freezes on pause and scales with time warp. The postfix puts the body's own value back.
    ///
    /// This used to write the renderer's _atmospherePushConstant, which went when the atmosphere
    /// data moved into a uniform buffer. That failed as softly as the clock rename did, so the
    /// haze ran on wall-clock time on Pluto and Triton and stood still on Titan, whose stock
    /// extent (20 km) the shader took as a fixed time.
    ///
    /// The swap is harmless on these bodies: Pluto and Triton have no ozone, and Titan's is too
    /// thin to matter at any extent. Nothing else reads the extent while the call runs.
    /// </summary>
    public static void FillAtmosphereDataPrefix(object body, object atmoRef, out object? __state)
    {
        __state = null;
        try
        {
            if (body.GetType().GetProperty("MeanRadius")?.GetValue(body) is not double meanRadius
                || !IsHazeBody(meanRadius))
                return;
            float? clock = HazeClockSeconds();
            if (clock == null) return;
            object? extent = OzoneExtent(atmoRef);
            if (extent == null) return;
            __state = SetDistanceMetres(extent, clock.Value);
        }
        catch { __state = null; }
    }

    public static void FillAtmosphereDataPostfix(object? __state)
    {
        if (__state is DistanceSwap swap)
            _distanceValue?.SetValue(swap.Reference, swap.Metres);
    }

    /// <summary>The haze bodies, as RaBodyHasHaze in AtmosphereData.glsl picks them out.</summary>
    public static bool IsHazeBody(double meanRadius) =>
        Math.Abs(meanRadius - 1188300.0) < 5000.0      // Pluto
        || Math.Abs(meanRadius - 2575500.0) < 5000.0   // Titan
        || Math.Abs(meanRadius - 1353400.0) < 5000.0;  // Triton

    /// <summary>Elapsed simulation seconds for the haze clock, or null without a clock. Not
    /// wrapped: wrapping made the haze jump once per period. A float carries the raw seconds
    /// smoothly for any realistic save, and the 1000 s offset keeps it above the shader's 0.5
    /// "injected?" threshold at the very start of a game.</summary>
    public static float? HazeClockSeconds()
    {
        object? simTime = SimClock()?.Invoke(null, null);
        if (simTime == null) return null;
        _simTimeMinutes ??= simTime.GetType().GetProperty("Minutes");
        if (_simTimeMinutes?.GetValue(simTime) is not double minutes) return null;
        return (float)(minutes * 60.0 + 1000.0);
    }

    /// <summary>atmoRef.Visual.Ozone.Extent, a DistanceReference, or null if the chain moved.</summary>
    public static object? OzoneExtent(object atmoRef)
    {
        object? visual = Member(ref _visualMember, atmoRef, "Visual");
        object? ozone = visual == null ? null : Member(ref _ozoneMember, visual, "Ozone");
        return ozone == null ? null : Member(ref _extentMember, ozone, "Extent");
    }

    /// <summary>Sets a DistanceReference to a value in metres (what InMeters() returns) and
    /// returns what it held, for the postfix to restore. Null if the reference changed shape.</summary>
    public static object? SetDistanceMetres(object distance, double metres)
    {
        _distanceValue ??= AccessTools.Field(distance.GetType(), "_value");
        if (_distanceValue?.GetValue(distance) is not double held) return null;
        _distanceValue.SetValue(distance, metres);
        return new DistanceSwap(distance, held);
    }

    private sealed record DistanceSwap(object Reference, double Metres);

    private static object? Member(ref MemberInfo? cached, object owner, string name)
    {
        cached ??= (MemberInfo?)AccessTools.Field(owner.GetType(), name) ?? AccessTools.Property(owner.GetType(), name);
        return cached switch
        {
            FieldInfo f => f.GetValue(owner),
            PropertyInfo p => p.GetValue(owner),
            _ => null,
        };
    }

    /// <summary>Full path with consistent separators; relative paths resolve
    /// against the CWD, which the game requires to be its install root.</summary>
    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path.Replace('/', '\\')); }
        catch { return path; }
    }
}
