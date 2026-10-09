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
            // The haze's turning about the body's own pole goes out in the data the method returns, when
            // that still has the slots for it: see Patches.FillAtmosphereDataTurnPostfix.
            bool turn = Patches.CanInjectHazeTurn(fillAtmo);
            if (!turn)
                ShadowBuilder.Log("WARN: the atmosphere data has no free ring slots; the haze turns about fixed axes");
            try
            {
                harmony.Patch(fillAtmo,
                    prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.FillAtmosphereDataPrefix)),
                    postfix: new HarmonyMethod(typeof(Patches),
                        turn ? nameof(Patches.FillAtmosphereDataTurnPostfix) : nameof(Patches.FillAtmosphereDataPostfix)));
            }
            catch (Exception e)
            {
                ShadowBuilder.Log($"WARN: could not hook FillPlanetAtmosphereData ({e.GetType().Name}); haze animation is not sim-linked");
            }
            // The renderer itself, for the tables it keeps: they are worked out again when the haze bands are
            // switched (Patches.RendererSeenPrefix). A hook of its own, so that nothing above depends on it.
            if (!fillAtmo.IsStatic && Patches.CanForgetTables(atmoRenderer!))
            {
                try
                {
                    harmony.Patch(fillAtmo, prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.RendererSeenPrefix)));
                }
                catch (Exception e)
                {
                    ShadowBuilder.Log($"WARN: the atmosphere renderer was not reached ({e.GetType().Name}); switching the haze bands shows fully after the next change of body");
                }
            }
            else
                ShadowBuilder.Log("WARN: the atmosphere renderer's kept tables were not found; switching the haze bands shows fully after the next change of body");
        }
        else
            ShadowBuilder.Log("WARN: AtmosphereRenderer.FillPlanetAtmosphereData(body, atmoRef) not found; haze animation is not sim-linked");

        FlightAtmospheres.Install(harmony);
        NearbyBody.Install(harmony);

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
            double? seconds = HazeClock();
            if (seconds == null) return;
            object? extent = OzoneExtent(atmoRef);
            DistanceSwap? swap = extent == null ? null : SetDistanceMetres(extent, HazeClockFor(seconds.Value, HazeBandsNow()));
            __state = new HazeState(swap, meanRadius, seconds.Value);
        }
        catch { __state = null; }
    }

    /// <summary>
    /// The haze clock as the shader takes it: elapsed seconds, a thousand on so that it is past the shader's 0.5
    /// "is this a clock" line from the start of a game, and run backwards while the player has the haze bands
    /// switched off (the shader's RaHazeBandsOn). The ozone extent it stands in for is harmless at any size on these
    /// bodies, and at either sign: the ozone's own density is clamped.
    /// </summary>
    public static float HazeClockFor(double simSeconds, bool bands) =>
        (float)(bands ? simSeconds + 1000.0 : -(simSeconds + 1000.0));

    private static object? _renderer;
    private static FieldInfo? _tablesValid;
    private static bool _bandsSeen = Settings.DefaultHazeBands, _bandsRead;

    /// <summary>The player's switch, and the renderer's kept tables thrown out when it has changed: Pluto's and
    /// Triton's bands are in the haze's density, which the transmittance, multiple scattering and ambient tables
    /// are worked out from once for each body and kept.</summary>
    private static bool HazeBandsNow()
    {
        if (!_bandsRead)
        {
            _bandsRead = true;
            Settings.Load();
            _bandsSeen = Settings.HazeBands;
        }
        bool bands = Settings.HazeBands;
        if (bands != _bandsSeen)
        {
            _bandsSeen = bands;
            ForgetTables();
            ShadowBuilder.Log("haze bands switched " + (bands ? "on" : "off"));
        }
        return bands;
    }

    /// <summary>Whether the renderer still keeps its tables' validity where this clears it.</summary>
    public static bool CanForgetTables(Type renderer)
    {
        _tablesValid = renderer.GetField("_planetLutsValid", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return _tablesValid?.FieldType == typeof(bool[]);
    }

    /// <summary>Prefix on the same method, for the renderer it is called on.</summary>
    public static void RendererSeenPrefix(object __instance) => _renderer = __instance;

    /// <summary>Marks every body's kept tables as stale, as the engine does when a body's atmosphere is edited.</summary>
    public static bool ForgetTables()
    {
        try
        {
            if (_renderer == null || _tablesValid?.GetValue(_renderer) is not bool[] valid) return false;
            Array.Clear(valid);
            return true;
        }
        catch { return false; }
    }

    public static void FillAtmosphereDataPostfix(object? __state)
    {
        if (__state is HazeState { Swap: DistanceSwap swap })
            _distanceValue?.SetValue(swap.Reference, swap.Metres);
    }

    /// <summary>
    /// The same postfix, which also sends the haze's turning to the shader in the atmosphere data the
    /// method returns: the body's rotation axis as the game has it, and the haze's angle about it.
    ///
    /// The haze march used to turn its pattern about an axis written into the shader for each body,
    /// at one rate for all. Titan's was not its pole: the haze went over the poles instead of round
    /// the planet (user, 2026-10-06). And the pattern turned against the ground at twice its rate,
    /// not with it. Now it turns about the pole the ground turns about, at the ground's rate plus the
    /// wind that carries the real haze (HazeWind), so from the ground it drifts slowly.
    ///
    /// Harmony boxes the returned struct for a "ref object" result and copies it back afterwards.
    /// </summary>
    public static void FillAtmosphereDataTurnPostfix(object body, ref object __result, object? __state)
    {
        FillAtmosphereDataPostfix(__state);
        if (__state is not HazeState state) return;
        try { WriteHazeTurn(__result, body, state.MeanRadius, state.Seconds); }
        catch { }
    }

    private sealed record HazeState(DistanceSwap? Swap, double MeanRadius, double Seconds);

    // The haze's turning (cached reflection).
    private static Type? _turnDataType, _turnBodyType;
    private static FieldInfo? _useRings, _ringNormal, _ringOuter;
    private static FieldInfo?[]? _ringNormalParts;
    private static MethodInfo? _rotationAxis, _angularVelocity;

    /// <summary>What the shader takes as "the haze's turning is in the ring slots": an outer radius no ring has.</summary>
    public const float HazeTurnMarker = -1f;

    /// <summary>Whether the data FillPlanetAtmosphereData returns still has the ring slots the haze's
    /// turning is sent in, and the method the body argument. Checked before patching, like the clock.</summary>
    public static bool CanInjectHazeTurn(MethodInfo fill) =>
        fill.GetParameters().Any(p => p.Name == "body") && TurnSlots(fill.ReturnType);

    private static bool TurnSlots(Type data)
    {
        if (_turnDataType != data)
        {
            _turnDataType = data;
            _useRings = data.GetField("UseRingShadows");
            _ringNormal = data.GetField("RingNormalAndInnerRadius");
            _ringOuter = data.GetField("RingOuterRadius");
            _ringNormalParts = _ringNormal == null ? null
                : new[] { "X", "Y", "Z", "W" }.Select(n => _ringNormal.FieldType.GetField(n)).ToArray();
        }
        return data.IsValueType && _useRings?.FieldType == typeof(int) && _ringOuter?.FieldType == typeof(float)
            && _ringNormalParts != null && _ringNormalParts.All(f => f?.FieldType == typeof(float));
    }

    /// <summary>Writes the haze's turning into a haze body's atmosphere data (boxed): its rotation
    /// axis in the ring normal, the haze's angle about it in the inner radius, and the marker in the
    /// outer radius. A ringed body's slots are its rings' and are left alone. The shader reads them
    /// in RaymarchAtmosphereHorizontal.</summary>
    public static bool WriteHazeTurn(object data, object body, double meanRadius, double simSeconds)
    {
        if (!TurnSlots(data.GetType()) || _useRings!.GetValue(data) is not int rings || rings != 0) return false;
        if (HazeTurn(body, meanRadius, simSeconds) is not (double x, double y, double z, double angle)) return false;
        object normal = _ringNormal!.GetValue(data)!;
        _ringNormalParts![0]!.SetValue(normal, (float)x);
        _ringNormalParts[1]!.SetValue(normal, (float)y);
        _ringNormalParts[2]!.SetValue(normal, (float)z);
        _ringNormalParts[3]!.SetValue(normal, (float)angle);
        _ringNormal.SetValue(data, normal);
        _ringOuter!.SetValue(data, HazeTurnMarker);
        return true;
    }

    /// <summary>A haze body's rotation axis in the frame the shaders work in (the game's
    /// GetRotationAxisCce, the frame it hands ring normals over in), and how far its haze has turned
    /// about it, right-handed, between -pi and pi: the ground's own rate (GetAngularVelocity, negative
    /// for a body turning backwards) plus the haze's wind over its distance from the axis. Wrapped in
    /// double precision, so the angle stays exact however long the save. Null if the body no longer
    /// offers the two.</summary>
    public static (double X, double Y, double Z, double Angle)? HazeTurn(object body, double meanRadius, double simSeconds)
    {
        Type bt = body.GetType();
        if (_turnBodyType != bt)
        {
            _turnBodyType = bt;
            _rotationAxis = AccessTools.Method(bt, "GetRotationAxisCce", Type.EmptyTypes);
            _angularVelocity = AccessTools.Method(bt, "GetAngularVelocity", Type.EmptyTypes);
        }
        if (_rotationAxis?.Invoke(body, null) is not object axis || _angularVelocity?.Invoke(body, null) is not double spin
            || !double.IsFinite(spin))
            return null;
        double x = Component(axis, "X"), y = Component(axis, "Y"), z = Component(axis, "Z");
        double length = Math.Sqrt(x * x + y * y + z * z);
        if (!(length > 0.5)) return null;
        (double wind, double altitude) = HazeWind(meanRadius);
        double rate = spin + (spin < 0.0 ? -wind : wind) / (meanRadius + altitude);
        return (x / length, y / length, z / length, Math.IEEERemainder(rate * simSeconds, 2.0 * Math.PI));
    }

    private static double Component(object vector, string name) =>
        vector.GetType().GetField(name)?.GetValue(vector) switch { double d => d, float f => f, _ => double.NaN };

    /// <summary>
    /// The wind that carries each haze body's haze round against the ground, in m/s, positive the way
    /// the ground turns, and the height above the mean radius it is taken at, in m.
    ///
    /// Titan: its stratosphere turns the way the ground does and far faster. 100-200 m/s at 200-300 km
    /// (Cassini CIRS; Huygens' Doppler wind experiment found ~100 m/s near 130 km), against the
    /// ground's 13 m/s there: the haze laps the ground every two days.
    /// Pluto: no wind was measured. The general circulation models (Forget et al. 2017; Bertrand et
    /// al. 2020) have the air above 4 km turning against the ground at up to ~10 m/s.
    /// Triton: Voyager saw its plumes blown west above 8 km, which is the way its ground turns (it
    /// turns backwards), as the wind over a cold pole should (Ingersoll 1990); some 10 m/s.
    /// </summary>
    public static (double Wind, double Altitude) HazeWind(double meanRadius) =>
        Math.Abs(meanRadius - 2575500.0) < 5000.0 ? (100.0, 300000.0)      // Titan
        : Math.Abs(meanRadius - 1188300.0) < 5000.0 ? (-8.0, 50000.0)      // Pluto
        : Math.Abs(meanRadius - 1353400.0) < 5000.0 ? (10.0, 10000.0)      // Triton
        : (0.0, 0.0);

    /// <summary>The haze bodies, as RaBodyHasHaze in AtmosphereData.glsl picks them out.</summary>
    public static bool IsHazeBody(double meanRadius) =>
        Math.Abs(meanRadius - 1188300.0) < 5000.0      // Pluto
        || Math.Abs(meanRadius - 1353400.0) < 5000.0   // Triton
        || Math.Abs(meanRadius - 2575500.0) < 5000.0;  // Titan

    /// <summary>Elapsed simulation seconds for the haze clock, or null without a clock. Not
    /// wrapped: wrapping made the haze jump once per period. A float carries the raw seconds
    /// smoothly for any realistic save, and the 1000 s offset keeps it above the shader's 0.5
    /// "injected?" threshold at the very start of a game.</summary>
    public static float? HazeClockSeconds() => HazeClock() is double seconds ? (float)(seconds + 1000.0) : null;

    /// <summary>Elapsed simulation seconds, in full precision and without the offset, or null
    /// without a clock.</summary>
    public static double? HazeClock()
    {
        object? simTime = SimClock()?.Invoke(null, null);
        if (simTime == null) return null;
        _simTimeMinutes ??= simTime.GetType().GetProperty("Minutes");
        if (_simTimeMinutes?.GetValue(simTime) is not double minutes) return null;
        return minutes * 60.0;
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
    public static DistanceSwap? SetDistanceMetres(object distance, double metres)
    {
        _distanceValue ??= AccessTools.Field(distance.GetType(), "_value");
        if (_distanceValue?.GetValue(distance) is not double held) return null;
        _distanceValue.SetValue(distance, metres);
        return new DistanceSwap(distance, held);
    }

    public sealed record DistanceSwap(object Reference, double Metres);

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
