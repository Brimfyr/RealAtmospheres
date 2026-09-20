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

        // Sim-linked haze animation: inject wrapped elapsed SIM seconds into the atmosphere
        // push constant's OzoneExtent for Pluto (which doesn't use ozone), so the haze reads a
        // clock that freezes on pause and scales with time-warp. Fail-soft: if the method isn't
        // found or the postfix throws, the shader falls back to wall-clock time.
        Type? atmoRenderer = AccessTools.TypeByName("KSA.AtmosphereRenderer");
        MethodInfo? prepAtmo = atmoRenderer == null ? null : AccessTools.Method(atmoRenderer, "PrepareAtmosphereData");
        if (prepAtmo != null)
            harmony.Patch(prepAtmo, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.PrepareAtmospherePostfix)));
        else
            ShadowBuilder.Log("WARN: AtmosphereRenderer.PrepareAtmosphereData not found; haze animation uses wall-clock time");

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
    private static FieldInfo? _pushConstantField, _ozoneExtentField;
    private static MethodInfo? _getElapsedSimTime;
    private static PropertyInfo? _simTimeMinutes;
    private static Type? _universeType;

    /// <summary>Postfix on AtmosphereRenderer.PrepareAtmosphereData: for Pluto (identified by
    /// mean radius, the same key the shader uses), overwrite the push constant's OzoneExtent
    /// with wrapped elapsed SIM seconds, so the haze animation is sim-linked (freezes on pause,
    /// scales with time-warp). Pluto doesn't use ozone; every other body is left untouched.
    /// Fail-soft: any missing member or exception leaves OzoneExtent alone and the shader uses
    /// wall-clock time.</summary>
    public static void PrepareAtmospherePostfix(object __instance, object planetInstance)
    {
        try
        {
            object? body = planetInstance.GetType().GetProperty("AtmosphericBody")?.GetValue(planetInstance)
                        ?? planetInstance.GetType().GetField("AtmosphericBody")?.GetValue(planetInstance);
            if (body == null) return;
            object? mr = body.GetType().GetProperty("MeanRadius")?.GetValue(body)
                      ?? body.GetType().GetField("MeanRadius")?.GetValue(body);
            if (mr is not double meanRadius) return;
            // Inject for every haze body (must match RaBodyHasHaze in the shader). Overwriting
            // OzoneExtent is safe on these: Pluto/Triton have no ozone, Titan's ozone is ~0.
            bool isHaze = Math.Abs(meanRadius - 1188300.0) < 5000.0    // Pluto
                       || Math.Abs(meanRadius - 2575500.0) < 5000.0    // Titan
                       || Math.Abs(meanRadius - 1353400.0) < 5000.0;   // Triton
            if (!isHaze) return;

            _universeType ??= AccessTools.TypeByName("KSA.Universe");
            _getElapsedSimTime ??= _universeType == null ? null : AccessTools.Method(_universeType, "GetElapsedSimTime");
            object? simTime = _getElapsedSimTime?.Invoke(null, null);
            if (simTime == null) return;
            _simTimeMinutes ??= simTime.GetType().GetProperty("Minutes");
            if (_simTimeMinutes?.GetValue(simTime) is not double minutes) return;
            double seconds = minutes * 60.0;
            // NO wrap: wrapping caused a periodic jump (every wrap-period/simSpeed seconds). float32
            // carries the raw elapsed seconds smoothly for any realistic sim duration; +1000 just
            // keeps it > 0.5 so the shader's "injected?" fallback check passes at sim start.
            float animT = (float)(seconds + 1000.0);

            _pushConstantField ??= AccessTools.Field(__instance.GetType(), "_atmospherePushConstant");
            object? pc = _pushConstantField?.GetValue(__instance);
            if (pc == null) return;
            _ozoneExtentField ??= AccessTools.Field(pc.GetType(), "OzoneExtent");
            if (_ozoneExtentField == null) return;
            _ozoneExtentField.SetValue(pc, animT);          // pc is a boxed struct copy
            _pushConstantField!.SetValue(__instance, pc);   // write the modified struct back
        }
        catch { /* fail-soft: shader falls back to wall-clock time */ }
    }

    /// <summary>Full path with consistent separators; relative paths resolve
    /// against the CWD, which the game requires to be its install root.</summary>
    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path.Replace('/', '\\')); }
        catch { return path; }
    }
}
