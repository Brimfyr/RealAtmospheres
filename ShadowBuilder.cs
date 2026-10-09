using System.Globalization;
using System.Text.RegularExpressions;
using RealModShared;

namespace RealAtmospheres;

/// <summary>
/// Builds the shadow content tree inside the mod folder at every launch:
///   ShadowContent\Core\Astronomicals.xml   (Mars atmosphere v4 + MarsCirrus layer)
///   ShadowContent\Core\Shaders\**          (full copy; atmosphere and cloud shaders
///                                           patched, see Build)
/// The Harmony prefixes then redirect the game's reads into this tree. The full
/// Shaders copy matters: shaderc includes resolve relative to the requesting
/// file's directory, so once a top-level shader path is redirected here, all of
/// its #includes (../../Atmosphere/AtmosphereFunctions.glsl etc.) resolve here
/// too. The folder name deliberately contains "Content" so the game's
/// ShaderReloader.GetModPath (IndexOf("Content")) keeps producing valid mod
/// paths for dependency tracking.
///
/// Every transform is fail-soft and idempotent: a missing anchor (changed by a
/// game update) or an already-applied marker (hand-patched install) logs and
/// skips that transform; the game then simply runs with whatever that file
/// already contains.
/// </summary>
internal static class ShadowBuilder
{
    public static string? ShadowAstronomicals;
    public static string? ShadowShadersRoot;

    /// <summary>The phase GLSL block to inject: the Mars dust phase - the Level-2 LUT
    /// (from assets) if present, else the embedded Level-1 HG fit - then our one
    /// MiePhaseFunction (Payloads.MiePhaseDispatch).</summary>
    private static string _phaseBlock = Payloads.PatchedPhaseBlock + "\n\n" + Payloads.MiePhaseDispatch;

    /// <summary>Core system-template file name -> patched shadow absolute path
    /// (SolSystem.xml / SolSystemDense.xml, carrying the Titan refinement).</summary>
    public static readonly Dictionary<string, string> ShadowSystemFiles = new(StringComparer.OrdinalIgnoreCase);

    public static void Build(string coreDir, string modDir)
    {
        string shadowCore = Path.Combine(modDir, "ShadowContent", "Core");
        string assetsDir = Path.Combine(modDir, "assets");

        // ---- Shaders tree ----
        string srcShaders = Path.Combine(coreDir, "Shaders");
        string dstShaders = Path.Combine(shadowCore, "Shaders");
        if (Directory.Exists(dstShaders))
            Directory.Delete(dstShaders, recursive: true);
        CopyTree(srcShaders, dstShaders);

        // Level-2 phase LUT: if the generated LUT is present, it replaces the
        // embedded Level-1 HG-fit phase block (delete the file to fall back to L1).
        // Either supplies the Martian dust phase only: the file ends with its own
        // copy of MiePhaseFunction, which is cut off for ours, so every body's
        // branch (Pluto's two-part haze) is the same whichever level is in use.
        string lut = Path.Combine(assetsDir, "MarsDustPhaseLut.glsl");
        string mars = File.Exists(lut) ? ReadLf(lut).Trim('\n') : Payloads.PatchedPhaseBlock;
        int ownDispatch = mars.IndexOf(Payloads.StockPhaseStart, StringComparison.Ordinal);
        if (ownDispatch >= 0)
            mars = mars[..ownDispatch].TrimEnd('\n');
        _phaseBlock = mars + "\n\n" + Payloads.MiePhaseDispatch;
        if (File.Exists(lut)) Log("Mars phase: LEVEL 2 (exact bhmie LUT)");

        int shaderPatches = 0;
        shaderPatches += PatchFile(
            Path.Combine(dstShaders, "Atmosphere", "AtmosphereFunctions.glsl"),
            PatchAtmosphereFunctions) ? 1 : 0;
        shaderPatches += PatchFile(
            Path.Combine(dstShaders, "Clouds", "2DCloud.comp"),
            s => PatchVenusShell(PatchCloudGain(PatchCloudSkylight(PatchCloudTerminator(s))))) ? 1 : 0;
        PatchMarsDensity(dstShaders);
        PatchRayleighColour(dstShaders);
        PatchHazeBandMarch(dstShaders);
        PatchCloudRingShadows(dstShaders);
        PatchTerrainShadow(dstShaders);
        PatchCoverageLift(dstShaders);
        PatchMapFlow(dstShaders);
        PatchFaceLight(dstShaders);
        PatchThinShadow(dstShaders);
        ShadowShadersRoot = dstShaders;
        Log($"shader tree ready ({shaderPatches}/2 patches applied) -> {dstShaders}");

        // ---- Astronomicals.xml + System templates: ALL via the shared shadow ----
        // Astronomicals.xml now goes through the shared shadow too (it used to be RA's
        // private copy) so "Real Surfaces" can add its Venus surface edits to the same
        // file - Venus is a LoadFromLibrary body, editable only in the library. RA owns
        // the atmosphere/cloud edits (Astronomicals + inline system bodies); RS owns the
        // surfaces (Pluto/Triton/Titan inline + Venus dunes/clutter in the library).
        // The callback dispatches on filename; the result is split into the two redirects.
        ShadowSystemFiles.Clear();
        var shared = SharedShadow.Build(coreDir, modDir, (name, sys) =>
        {
            if (name.Equals("Astronomicals.xml", StringComparison.OrdinalIgnoreCase))
                return ApplyAstronomicalsEdits(sys, assetsDir);

            // Titan atmosphere = STOCK (forward-scattering structurally blocked; see
            // Payloads.TitanPairs). Only transitions + haze laminae are added.
            sys = PatchAtmosphereValues(sys, Payloads.TitanTransitionPairs, "Titan transitions");
            sys = PatchAtmosphereValues(sys, Payloads.TitanShroudPairs, "Titan shroud");
            sys = PatchAtmosphereValues(sys, Payloads.NeptunePairs, "Neptune");
            sys = PatchTitanClouds(sys, assetsDir);
            sys = PatchTitanHaze(sys, assetsDir);
            sys = UpgradeBody(sys, "Pluto", Payloads.PlutoBodyOpen, "</PlanetaryBody>",
                extraKit: null, Payloads.PlutoAtmosphere);

            // Triton: upgrade to an AtmosphericBody with just the mesh + colour kit.
            // The temporary preview surface (Diffuse/Normal/Height) is Real Surfaces',
            // which inserts it before this <Color> when present.
            string kit = Payloads.TritonBodyKit.Trim('\n');
            string tritonInsert = Payloads.TritonAtmosphere.Trim('\n');
            if (File.Exists(Path.Combine(assetsDir, "TritonCloudsMaskVolumetric.dds"))
                && File.Exists(Path.Combine(assetsDir, "TritonCloudsMask2D.png")))
                tritonInsert += "\n        " + Payloads.TritonClouds.Trim('\n').Replace("{ASSETS}", assetsDir);
            else
                Log("WARN: Triton cloud masks missing, atmosphere only");
            sys = UpgradeBody(sys, "Triton", Payloads.TritonBodyOpen, "</MinorBody>",
                extraKit: kit, tritonInsert);

            sys = SwapDiffuse(sys, "Neptune", assetsDir);
            sys = InsertGiantClouds(sys, "Neptune", assetsDir, 5000, 70000);
            sys = PatchCloudLighting(sys, name);
            sys = PatchPhysicalAtmospheres(sys, name);
            return sys;
        }, Log);
        foreach (var kv in shared)
        {
            if (kv.Key.Equals("Astronomicals.xml", StringComparison.OrdinalIgnoreCase))
                ShadowAstronomicals = kv.Value;   // -> Mod.LoadAssetBundles redirect
            else
                ShadowSystemFiles[kv.Key] = kv.Value;   // -> Mod.LoadSystems redirect
        }
        Log($"shared shadow ready: Astronomicals {(ShadowAstronomicals != null ? "on" : "MISSING")}, " +
            $"{ShadowSystemFiles.Count}/2 system templates");
    }

    /// <summary>RA's Astronomicals.xml edits (atmosphere values + giant diffuse/cloud swaps +
    /// cirrus), applied to the shared shadow copy. Venus's SURFACE (dunes/clutter) is Real
    /// Surfaces' job now and is applied to the same shared file by that mod.</summary>
    private static string ApplyAstronomicalsEdits(string xml, string assetsDir)
    {
        xml = PatchAtmosphereValues(xml, Payloads.AtmospherePairs, "Mars");
        xml = PatchAtmosphereValues(xml, Payloads.VenusPairs, "Venus");
        xml = PatchVenusClouds(xml, assetsDir);
        xml = PatchVenusCloudTurn(xml);
        xml = PatchAtmosphereValues(xml, Payloads.JupiterPairs, "Jupiter");
        xml = PatchAtmosphereValues(xml, Payloads.SaturnPairs, "Saturn");
        xml = PatchAtmosphereValues(xml, Payloads.UranusPairs, "Uranus");
        xml = PatchJupiterClouds(xml);
        xml = SwapJupiterMask(xml, assetsDir);
        xml = SwapDiffuse(xml, "Saturn", assetsDir);
        xml = SwapDiffuse(xml, "Uranus", assetsDir);
        xml = InsertGiantClouds(xml, "Saturn", assetsDir, SaturnDeckBaseM, 95000);
        xml = InsertGiantClouds(xml, "Uranus", assetsDir, 5000, 70000);
        xml = PatchCirrusLayer(xml, assetsDir);
        xml = PatchAtmosphereValues(xml, Payloads.EarthPairs, "Earth");
        xml = PatchCloudLighting(xml, "Astronomicals.xml");
        xml = PatchPhysicalAtmospheres(xml, "Astronomicals.xml");
        return xml;
    }

    /// <summary>Insert the detached-haze layer into Titan's EXISTING Clouds
    /// block (which sits OUTSIDE the Atmosphere element in the system files -
    /// a second in-Atmosphere Clouds block is silently ignored), right after
    /// the stock TitanPlaceholderClouds layer.</summary>
    /// <summary>Swap a giant's stock Diffuse cubemap path for the mod's higher-res
    /// build (assets/&lt;Body&gt;_Diffuse.ktx2) when it exists - absolute path passes
    /// through Mod.GetPath. Delete the .ktx2 to revert. Works in whichever file
    /// (Astronomicals or system) holds the reference.</summary>
    /// <summary>The base of Saturn's deck (5 km on Uranus and Neptune), so that it reaches 172.5 km, 5 mbar: far
    /// above Saturn's measured haze top (~50-100 mbar), deliberately, as Uranus's and Neptune's decks are above their
    /// clouds. The planet's map is a photograph, which already holds whatever the gas and haze above the clouds do to
    /// them, so the game should add little more: here as little shows above the deck as above Neptune's (green
    /// optical depth ~0.4 along the limb). At 5-100 km the measured gas and haze above laid a blue veil over the
    /// outer disc from afar (user, 2026-10-03). The whole deck moves, so its shape and towers stay as tuned.</summary>
    private const int SaturnDeckBaseM = 77500;

    /// <summary>Insert a Jupiter-style volumetric Clouds block after the body's
    /// &lt;/Atmosphere&gt; (Clouds is a sibling of Atmosphere). Gated on the coverage
    /// mask existing (delete it to revert); idempotent on the layer Id.</summary>
    private static string InsertGiantClouds(string content, string body, string assetsDir, int baseM, int towerHeightM)
    {
        if (content.Contains($"Id=\"{body}Clouds\""))
            return content; // already present

        string mask = Path.Combine(assetsDir, $"{body}CloudsMask.dds");
        if (!File.Exists(mask))
        {
            Log($"WARN: {body} cloud mask missing, skipping clouds");
            return content;
        }
        int i = content.IndexOf($"Id=\"{body}\"", StringComparison.Ordinal);
        int ae = i < 0 ? -1 : content.IndexOf("</Atmosphere>", i, StringComparison.Ordinal);
        if (ae < 0)
        {
            Log($"WARN: {body} </Atmosphere> not found, skipping clouds");
            return content;
        }
        ae += "</Atmosphere>".Length;
        bool hasFlow = File.Exists(Path.Combine(assetsDir, $"{body}Flowmap.dds"));
        // Velocity = flow peak x Displacement / LoopDuration, so the pair can move
        // together at constant wind. What matters visually is displacement against the
        // cloud noise scale: Jupiter advects ~770 km with 73 km noise, about 10x, while
        // ours had run to 146x on Uranus, which decorrelates the two advection phases
        // and combs the deck. Displacement is pinned near that ratio and the loop
        // carries each planet's real peak jet instead (Saturn and Neptune ~450 m/s,
        // Uranus ~200). The flowmaps are normalised to +-0.45, doubled by the shader.
        const double flowPeak = 0.9;
        const double dispKm = 700.0;
        double peakJetMs = body switch
        {
            "Saturn"  => 450.0,
            "Uranus"  => 200.0,
            "Neptune" => 450.0,
            _         => 300.0
        };
        double loopHours = flowPeak * dispKm * 1000.0 / peakJetMs / 3600.0;
        // The 2D map is not noise: it may travel much further before its loop restarts, and has to for its motion
        // to show (Payloads.Giant2DFlowDisplacementKm). Same wind, a longer loop.
        double loop2dHours = flowPeak * Payloads.Giant2DFlowDisplacementKm * 1000.0 / peakJetMs / 3600.0;
        // Every number here goes into the XML, which the game reads in the invariant format.
        // Under a decimal-comma culture (German, French, Spanish and most of Europe) the loop
        // came out as 0,389, and the file holding it failed to load whole: Astronomicals.xml
        // took the Sol template with it, and the game could not start.
        var xml = CultureInfo.InvariantCulture;
        string disp = dispKm.ToString("0", xml);
        string loop = loopHours.ToString("0.###", xml);

        // Volumetric-to-2D fade band and flicker reduction: Payloads.CloudLighting, as for every cloudy body.
        var band = Payloads.CloudLighting.Single(c => c.Body == body);
        int transStart = band.StartKm, transEnd = band.EndKm;

        var deck = _faceLight ? Payloads.GiantDeckDense : Payloads.GiantDeckSoft;
        string block = Payloads.GiantCloudsTemplate
            .Replace("{FLOWMAP}", hasFlow ? Payloads.GiantFlowMapTemplate : "")
            .Replace("{VOLFLOWTEX}", hasFlow ? Payloads.GiantVolFlowTexPerPlanet : Payloads.GiantVolFlowTexJupiter)
            .Replace("{DISP2D}", Payloads.Giant2DFlowDisplacementKm.ToString("0", xml))
            .Replace("{LOOP2D}", loop2dHours.ToString("0.###", xml))
            .Replace("{DISP}", disp)
            .Replace("{LOOP}", loop)
            .Replace("{TRANSSTART}", transStart.ToString(xml))
            .Replace("{TRANSEND}", transEnd.ToString(xml))
            .Replace("{FLICKER}", (band.FlickerKm ?? transEnd * 11 / 10).ToString(xml))
            .Replace("{BODY}", body).Replace("{ASSETS}", assetsDir)
            // dense deck types only where the raymarch lights them at the face (Payloads.GiantDeckDense)
            .Replace("{DENSITY}", deck.Density).Replace("{DENSITY_EDGE}", deck.DensityEdge)
            .Replace("{SHARP}", deck.Sharpness).Replace("{SHARP_EDGE}", deck.SharpnessEdge)
            .Replace("{BRIGHT}", deck.Brightness)
            .Replace("{BASE}", baseM.ToString(xml))
            .Replace("{HEIGHT}", towerHeightM.ToString(xml))
            // the storm types rise above the deck; heights scale with the per-planet tower
            // Shorter than the deck, so the belt edges read as depressions in it rather
            // than towers above it. Anything taller would raise the layer top, and the
            // 2D billboard hangs from that.
            .Replace("{HEIGHT_EDGE}", (towerHeightM * 9 / 20).ToString(xml));
;
        if (!_faceLight) Log($"{body}'s deck stays soft: the raymarch does not light thick steps at the face");
        Log($"{body} volumetric clouds added{(hasFlow ? $" + per-planet flowmap ({peakJetMs:0} m/s jets): the map {Payloads.Giant2DFlowDisplacementKm.ToString("0", xml)} km over {loop2dHours.ToString("0.###", xml)} h, the noise {disp} km over {loop} h" : "")}");
        return content[..ae] + block + content[ae..];
    }

    private static string SwapDiffuse(string content, string body, string assetsDir)
    {
        string ktx = Path.Combine(assetsDir, $"{body}Diffuse.ktx2");
        string stock = $"Path=\"Textures/{body}_Diffuse.ktx2\"";
        if (File.Exists(ktx) && content.Contains(stock))
        {
            content = content.Replace(stock, $"Path=\"{ktx}\"");
            Log($"{body} diffuse -> mod 2048/face map");
        }
        return content;
    }

    /// <summary>Titan's methane clouds: swap the stock layer's coverage (Venus's cloud map, in the
    /// volumetric texture, its detail selector and the 2D texture) for the masks built from the
    /// user's SpaceEngine map, and Venus's colours for white. The stock types and detail tile stay.
    /// Missing masks or a layer that no longer matches stock leave it as it is.</summary>
    private static string PatchTitanClouds(string s, string assetsDir)
    {
        if (s.Contains(Payloads.TitanCloudsMarker))
            return s; // already applied

        int i = s.IndexOf(Payloads.TitanCloudsAnchor, StringComparison.Ordinal);
        int j = i < 0 ? -1 : s.IndexOf("</Layer>", i, StringComparison.Ordinal);
        if (j < 0)
        {
            Log("WARN: TitanPlaceholderClouds anchor not found, keeping Titan's stock clouds");
            return s;
        }
        foreach (string tex in new[] { "TitanCloudsMaskVolumetric.dds", "TitanCloudsMask2D.png" })
        {
            if (!File.Exists(Path.Combine(assetsDir, tex)))
            {
                Log($"WARN: missing asset {tex}, keeping Titan's stock clouds");
                return s;
            }
        }
        string layer = s[i..j];
        string stock = Payloads.TitanCloudsStockTexture;
        if (CountOf(layer, stock) != 2 || CountOf(layer, Payloads.TitanCloudsStockSelector) != 1
            || CountOf(layer, Payloads.TitanCloudsStockColour) != 1 || CountOf(layer, Payloads.TitanClouds2DStockColour) != 1)
        {
            Log("WARN: Titan's cloud layer no longer matches stock, keeping its clouds");
            return s;
        }
        // the volumetric map comes first, the 2D one inside TwoDimensionalCloud last; replace the
        // later one first so the earlier index stays valid
        int vol = layer.IndexOf(stock, StringComparison.Ordinal);
        int flat = layer.IndexOf(stock, vol + stock.Length, StringComparison.Ordinal);
        layer = layer[..flat] + Payloads.TitanClouds2DTexture.Replace("{ASSETS}", assetsDir) + layer[(flat + stock.Length)..];
        layer = layer[..vol] + Payloads.TitanCloudsVolumetricTexture.Trim('\n').Replace("{ASSETS}", assetsDir)
            + layer[(vol + stock.Length)..];
        layer = layer.Replace(Payloads.TitanCloudsStockSelector, Payloads.TitanCloudsSelector)
            .Replace(Payloads.TitanCloudsStockColour, Payloads.TitanCloudsColour)
            .Replace(Payloads.TitanClouds2DStockColour, Payloads.TitanCloudsColour);
        Log("Titan methane clouds: two polar caps from the SpaceEngine map, white");
        return s[..i] + layer + s[j..];
    }

    private static string PatchTitanHaze(string s, string assetsDir)
    {
        if (s.Contains(Payloads.TitanHazeMarker))
            return s; // already present

        int i = s.IndexOf(Payloads.TitanCloudsAnchor, StringComparison.Ordinal);
        int j = i < 0 ? -1 : s.IndexOf("</Layer>", i, StringComparison.Ordinal);
        if (j < 0)
        {
            Log("WARN: TitanPlaceholderClouds anchor not found, skipping detached haze");
            return s;
        }
        j += "</Layer>".Length;

        foreach (string tex in new[] { "TitanUpperHazeMaskVolumetric.dds", "TitanUpperHazeMask2D.png",
                                       "TitanLowerHazeMaskVolumetric.dds", "TitanLowerHazeMask2D.png" })
        {
            if (!File.Exists(Path.Combine(assetsDir, tex)))
            {
                Log($"WARN: missing asset {tex}, skipping detached haze");
                return s;
            }
        }
        string layer = Payloads.TitanHazeLayerTemplate.Replace("{ASSETS}", assetsDir);
        return s[..j] + layer + s[j..];
    }

    // ---------------- XML transforms ----------------

    private static string PatchAtmosphereValues(
        string s, (string Stock, string Corrected, string Marker)[] pairs, string label)
    {
        foreach (var (stock, corrected, marker) in pairs)
        {
            if (s.Contains(marker))
                continue; // already applied (e.g. hand-patched install)
            int n = CountOf(s, stock);
            if (n != 1)
            {
                Log($"WARN: {label} atmosphere anchor found {n}x (expected 1), skipping: {Truncate(stock)}");
                continue;
            }
            s = s.Replace(stock, corrected);
        }
        return s;
    }

    /// <summary>Each measured body's flight values (its Physical block) set to its table's 0 km values
    /// (AtmosphereTables.Flight): the game shows them, and they are the exponential flight falls back
    /// on if FlightAtmospheres cannot install. Bodies not in this file are passed over.</summary>
    private static string PatchPhysicalAtmospheres(string s, string label)
    {
        int done = 0;
        foreach (FlightTable t in AtmosphereTables.Flight)
        {
            int body = s.IndexOf($"<AtmosphericBody Id=\"{t.Body}\"", StringComparison.Ordinal);
            if (body < 0)
                continue;
            int end = s.IndexOf("</AtmosphericBody>", body, StringComparison.Ordinal);
            int p0 = end < 0 ? -1 : s.IndexOf("<Physical>", body, end - body, StringComparison.Ordinal);
            int p1 = p0 < 0 ? -1 : s.IndexOf("</Physical>", p0, end - p0, StringComparison.Ordinal);
            if (p1 < 0)
            {
                Log($"WARN: {t.Body} has no flight atmosphere block in {label}");
                continue;
            }
            string block = s[p0..p1];
            block = Regex.Replace(block, "(<SeaLevelPressure\\s+Atm=\")[^\"]*(\")", "${1}" + t.SeaLevelPressureAtm + "${2}");
            block = Regex.Replace(block, "(<SeaLevelDensity\\s+KgPerM3=\")[^\"]*(\")", "${1}" + t.SeaLevelDensity + "${2}");
            block = Regex.Replace(block, "(<ScaleHeight\\s+Km=\")[^\"]*(\")", "${1}" + t.ScaleHeightKm + "${2}");
            s = s[..p0] + block + s[p1..];
            done++;
        }
        if (done > 0)
            Log($"measured flight values set for {done} bodies in {label}");
        return s;
    }

    /// <summary>Venus's stock cloud layer as one deck (see Payloads.VenusCloudDeckTypes): swaps its five
    /// cloud types and its detail tile for the coverage-lifted copy, inside Venus's body only. If the
    /// stock types are not the five expected, the clouds are left as they are; without the tile the
    /// types still apply, but the deck keeps the stock tile's holes.</summary>
    private static string PatchVenusClouds(string s, string assetsDir)
    {
        if (s.Contains(Payloads.VenusDeckMarker))
            return s; // already applied

        int body = s.IndexOf("<AtmosphericBody Id=\"Venus\"", StringComparison.Ordinal);
        int end = body < 0 ? -1 : s.IndexOf("</AtmosphericBody>", body, StringComparison.Ordinal);
        if (end < 0) { Log("WARN: Venus not found, clouds left as they are"); return s; }
        string venus = s[body..end];

        const string firstType = "<CloudType Name=\"Stratus\">", typeEnd = "</CloudType>";
        int t0 = venus.IndexOf(firstType, StringComparison.Ordinal);
        int t1 = venus.LastIndexOf(typeEnd, StringComparison.Ordinal);
        if (CountOf(venus, "<CloudType ") != 5 || t0 < 0 || t1 < t0 || CountOf(venus[..t0], "<CloudType ") != 0
            || !venus[(t1 + typeEnd.Length)..].TrimStart().StartsWith("</VolumetricCloud>", StringComparison.Ordinal))
        {
            Log("WARN: Venus's stock cloud types have changed, clouds left as they are");
            return s;
        }

        // the crown types at the stratus's brightness only where the deck is lit at the face (Payloads.VenusCrownBrightness)
        venus = venus[..t0] + Payloads.VenusCloudDeckTypes.Trim('\n').Replace("{BRIGHT}", _faceLight ? Payloads.VenusCrownBrightness : "1")
            + venus[(t1 + typeEnd.Length)..];
        if (!File.Exists(Path.Combine(assetsDir, "VenusCloudDetail.png")))
            Log("WARN: VenusCloudDetail.png missing, Venus's deck keeps the stock tile's holes");
        else if (CountOf(venus, Payloads.VenusStockDetailTexture) != 1)
            Log("WARN: Venus's stock detail tile not found, the deck keeps its holes");
        else
            venus = venus.Replace(Payloads.VenusStockDetailTexture, Payloads.VenusDetailTexture.Replace("{ASSETS}", assetsDir));
        Log("Venus cloud deck: one solid deck 48-72 km, stock variety in short crowns");
        return s[..body] + venus + s[end..];
    }

    /// <summary>Venus's cloud layer taken round with its superrotation (Payloads.VenusTurnMarker): its stock
    /// RotationSpeed swapped, inside Venus's body only, for the speed that laps once in Payloads.VenusCloudHours
    /// against the stars, from the spin, the radius and the cloud base the file itself has.</summary>
    private static string PatchVenusCloudTurn(string s)
    {
        if (s.Contains(Payloads.VenusTurnMarker))
            return s; // already applied

        int body = s.IndexOf("<AtmosphericBody Id=\"Venus\"", StringComparison.Ordinal);
        int end = body < 0 ? -1 : s.IndexOf("</AtmosphericBody>", body, StringComparison.Ordinal);
        if (end < 0) { Log("WARN: Venus not found, its clouds keep the game's drift"); return s; }
        string venus = s[body..end];
        if (CountOf(venus, Payloads.VenusStockTurn) != 1)
        {
            Log("WARN: Venus's stock cloud rotation not found, its clouds keep the game's drift");
            return s;
        }

        double Number(string pattern, double otherwise)
        {
            var m = System.Text.RegularExpressions.Regex.Match(venus, pattern);
            return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                                System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : otherwise;
        }
        double spinDays = Number("<SiderealPeriod Days=\"([0-9.eE+-]+)\"", 243.018484);
        bool retrograde = !venus.Contains("<IsRetrograde Value=\"false\"") && venus.Contains("<IsRetrograde");
        double radiusM = Number("<MeanRadius Km=\"([0-9.eE+-]+)\"", 6051.8) * 1000.0;
        // the layer's base: the lowest of its cloud types, as the engine takes it
        double baseM = double.PositiveInfinity;
        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(venus, "<StartAltitude M=\"([0-9.eE+-]+)\""))
            if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double start))
                baseM = Math.Min(baseM, start);
        if (double.IsInfinity(baseM)) baseM = 48000.0;

        double speed = Payloads.CloudTurnSpeed(Payloads.VenusCloudHours, spinDays, retrograde, radiusM + baseM);
        string turned = "<RotationSpeed X=\"0\" Y=\"0\" Z=\""
                        + speed.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture)
                        + "\" /> <!-- " + Payloads.VenusTurnMarker + " -->";
        Log(FormattableString.Invariant(
            $"Venus's clouds go round once in {Payloads.VenusCloudHours:0} h, the way it spins ({speed:0.0} m/s at the deck's base)"));
        return s[..body] + venus.Replace(Payloads.VenusStockTurn, turned) + s[end..];
    }

    private static string PatchCirrusLayer(string s, string assetsDir)
    {
        if (s.Contains(Payloads.CirrusMarker))
            return s; // already present

        int i = s.IndexOf("Id=\"MarsDustStorms\"", StringComparison.Ordinal);
        if (i < 0)
        {
            Log("WARN: MarsDustStorms layer not found, skipping cirrus layer");
            return s;
        }
        int j = s.IndexOf("</Layer>", i, StringComparison.Ordinal);
        if (j < 0)
        {
            Log("WARN: MarsDustStorms </Layer> not found, skipping cirrus layer");
            return s;
        }
        j += "</Layer>".Length;

        string layer = Payloads.CirrusLayerTemplate.Replace("{ASSETS}", assetsDir);
        foreach (string tex in new[] { "MarsCirrusMaskVolumetric.dds", "MarsCirrusMask2D.png" })
        {
            if (!File.Exists(Path.Combine(assetsDir, tex)))
            {
                Log($"WARN: missing asset {tex}, skipping cirrus layer");
                return s;
            }
        }
        return s[..j] + layer + s[j..];
    }

    /// <summary>Jupiter's main deck reads the mod's mask, the game's with its grey and white swapped
    /// (Payloads.JupiterMaskSwapped): zones high, belts low, vortices as deep as before. Without the asset, or if
    /// the game's entry has changed, the game's mask stays.</summary>
    private static string SwapJupiterMask(string s, string assetsDir)
    {
        if (s.Contains(Payloads.JupiterMaskAsset))
            return s; // already swapped
        if (!File.Exists(Path.Combine(assetsDir, Payloads.JupiterMaskAsset)))
        {
            Log($"WARN: {Payloads.JupiterMaskAsset} missing, Jupiter's deck keeps the game's mask (dark belts tallest)");
            return s;
        }
        int n = CountOf(s, Payloads.JupiterMaskStock);
        if (n != 1)
        {
            Log($"WARN: Jupiter's cloud mask entry found {n}x (expected 1), its deck keeps the game's mask");
            return s;
        }
        Log("Jupiter's main deck: the game's mask with grey and white swapped (zones high, belts low, vortices deep)");
        return s.Replace(Payloads.JupiterMaskStock, Payloads.JupiterMaskSwapped.Replace("{ASSETS}", assetsDir));
    }

    /// <summary>Rescale the two Jupiter cloud layers to realistic altitudes.
    /// All edits are verified against the layer span first and applied
    /// all-or-nothing: heights and densities must move together or opacity
    /// breaks, so a single drifted anchor skips the whole rescale.</summary>
    private static string PatchJupiterClouds(string s)
    {
        if (s.Contains(Payloads.JupiterCloudsMarker))
            return s; // already applied

        const string layerStart = "<Layer Id=\"JupiterLowerClouds\">";
        int a = s.IndexOf(layerStart, StringComparison.Ordinal);
        int upper = a < 0 ? -1 : s.IndexOf("Id=\"JupiterClouds\"", a, StringComparison.Ordinal);
        int b = upper < 0 ? -1 : s.IndexOf("</Layer>", upper, StringComparison.Ordinal);
        if (b < 0)
        {
            Log("WARN: Jupiter cloud layers not found, skipping cloud rescale");
            return s;
        }
        b += "</Layer>".Length;

        string span = s[a..b];
        foreach (var (stock, _, count) in Payloads.JupiterCloudEdits)
        {
            int n = CountOf(span, stock);
            if (n != count)
            {
                Log($"WARN: Jupiter cloud anchor found {n}x (expected {count}), skipping cloud rescale: {Truncate(stock)}");
                return s;
            }
        }
        foreach (var (stock, corrected, _) in Payloads.JupiterCloudEdits)
            span = span.Replace(stock, corrected);

        // dense deck types only where the raymarch lights them at the face; else they stay thin, at the map's brightness
        int deckTypes = CountOf(span, Payloads.JupiterDenseDeckStock);
        if (_faceLight && deckTypes == 2)
            span = span.Replace(Payloads.JupiterDenseDeckStock, Payloads.JupiterDenseDeck);
        else
            Log(_faceLight ? $"WARN: Jupiter deck type anchor found {deckTypes}x (expected 2), its deck stays thin"
                           : "Jupiter's deck stays thin: the raymarch does not light thick steps at the face");

        return s[..a] + Payloads.JupiterCloudsMarkerComment + span + s[b..];
    }

    /// <summary>Upgrade a body element to AtmosphericBody in a system file:
    /// rename the open/close tags, optionally add the minimal PlanetaryBody
    /// kit (mesh + colour), and append the Atmosphere block before the close.
    /// AtmosphericBodyTemplate extends PlanetaryBodyTemplate, so all existing
    /// children stay valid.</summary>
    private static string UpgradeBody(string s, string name, string openTag, string closeTag,
        string? extraKit, string atmosphere)
    {
        if (s.Contains($"<AtmosphericBody Id=\"{name}\""))
            return s; // already upgraded

        int i = s.IndexOf(openTag, StringComparison.Ordinal);
        int j = i < 0 ? -1 : s.IndexOf(closeTag, i, StringComparison.Ordinal);
        if (j < 0)
        {
            Log($"WARN: {name} body anchor not found, skipping atmosphere upgrade");
            return s;
        }

        string newOpen = openTag
            .Replace("<PlanetaryBody ", "<AtmosphericBody ")
            .Replace("<MinorBody ", "<AtmosphericBody ");
        string insert = (extraKit == null ? "" : "\n        " + extraKit.Trim('\n'))
            + "\n        " + atmosphere.Trim('\n') + "\n    ";
        return s[..i] + newOpen + s[(i + openTag.Length)..j] + insert + "</AtmosphericBody>"
            + s[(j + closeTag.Length)..];
    }

    // ---------------- GLSL transforms ----------------

    private static string PatchAtmosphereFunctions(string s)
    {
        if (s.Contains(Payloads.PhaseMarker))
            return s; // already patched

        int a = s.IndexOf(Payloads.StockPhaseStart, StringComparison.Ordinal);
        int sig = a < 0 ? -1 : s.IndexOf(Payloads.PhaseFnSignature, a, StringComparison.Ordinal);
        int b = sig < 0 ? -1 : s.IndexOf("\n}", sig, StringComparison.Ordinal);
        if (b < 0)
        {
            Log("WARN: MiePhaseFunction anchor not found in AtmosphereFunctions.glsl, skipping");
            return s;
        }
        b += "\n}".Length;
        return s[..a] + _phaseBlock + s[b..];
    }

    /// <summary>Double-exponential Mars dust density (Schneegans 2024): inject the
    /// AtmosphereDensityFalloff helper into AtmosphereData.glsl and swap all 6
    /// density sites across 4 files to call it. In-shader sentinel means only Mars
    /// is affected. Fail-soft per file (anchor drift skips that file).</summary>
    private static void PatchMarsDensity(string shadersDir)
    {
        // 1) helper into AtmosphereData.glsl (before the include-guard endif)
        string dataPath = Path.Combine(shadersDir, "Atmosphere", "AtmosphereData.glsl");
        if (File.Exists(dataPath))
        {
            string d = ReadLf(dataPath);
            if (!d.Contains(Payloads.MarsDensityMarker))
            {
                if (d.Contains(Payloads.AtmosphereDataEndif))
                {
                    d = d.Replace(Payloads.AtmosphereDataEndif,
                        Payloads.MarsDensityHelper.Trim('\n')
                            .Replace("// __RA_DENSITY_TABLES__", AtmosphereTables.VisualDensityGlsl.Trim('\n'))
                        + "\n\n" + Payloads.AtmosphereDataEndif);
                    File.WriteAllText(dataPath, d);
                }
                else Log("WARN: AtmosphereData include-guard not found, skipping double-exp density");
            }
        }

        // 2) swap the density sites to call the helper
        int done = 0;
        foreach (var (relFile, stock, patch, count) in Payloads.MarsDensitySites)
        {
            string path = Path.Combine(shadersDir, relFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { Log($"WARN: {relFile} missing, skipping density site"); continue; }
            string s = ReadLf(path);
            if (s.Contains(patch)) { done++; continue; } // already applied
            int n = CountOf(s, stock);
            if (n != count) { Log($"WARN: {relFile} density anchor found {n}x (expected {count}), skipping"); continue; }
            File.WriteAllText(path, s.Replace(stock, patch));
            done++;
        }
        Log($"Mars double-exp density ({done}/{Payloads.MarsDensitySites.Length} files)");
    }

    /// <summary>Rayleigh colour by altitude: swap every stock use of the Rayleigh coefficients for
    /// RaRayleighCoefficients (the XML colour except above Titan's main haze). A file whose sites don't
    /// all match is left stock, so its extinction and scattering can't disagree.</summary>
    private static void PatchRayleighColour(string shadersDir)
    {
        int done = 0, files = 0;
        foreach (var group in Payloads.RayleighColourSites.GroupBy(site => site.File))
        {
            files++;
            string path = Path.Combine(shadersDir, group.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { Log($"WARN: {group.Key} missing, skipping its Rayleigh colour"); continue; }
            string s = ReadLf(path);
            if (s.Contains(Payloads.RayleighColourCall)) { done++; continue; } // already applied
            var off = group.Where(site => CountOf(s, site.Stock) != site.Count).ToList();
            if (off.Count > 0)
            {
                Log($"WARN: {group.Key} Rayleigh anchor '{off[0].Stock}' found {CountOf(s, off[0].Stock)}x (expected {off[0].Count}), leaving its colour stock");
                continue;
            }
            foreach (var site in group)
                s = s.Replace(site.Stock, site.Patch);
            File.WriteAllText(path, s);
            done++;
        }
        Log($"Rayleigh colour by altitude ({done}/{files} files)");
    }

    // Insert the horizontal haze-band march into Atmosphere.comp and branch the sky path so
    // flagged (ozone Altitude < 0) bodies march per-pixel instead of sampling the symmetric LUT.
    private static void PatchHazeBandMarch(string shadersDir)
    {
        string path = Path.Combine(shadersDir, "Atmosphere", "Atmosphere.comp");
        if (!File.Exists(path)) { Log("WARN: Atmosphere.comp missing, skipping haze-band march"); return; }
        string s = ReadLf(path);
        if (s.Contains("RaymarchAtmosphereHorizontal")) { Log("haze-band march already applied"); return; }

        if (CountOf(s, Payloads.HazeBandMarchAnchor) != 1)
        { Log("WARN: Atmosphere.comp main() anchor not found, skipping haze-band march"); return; }
        if (CountOf(s, Payloads.HazeBandSkyStock) != 1)
        { Log("WARN: Atmosphere.comp sky-path anchor not found, skipping haze-band march"); return; }

        s = s.Replace(Payloads.HazeBandMarchAnchor, Payloads.HazeBandMarchFunc.Trim('\n') + "\n\n" + Payloads.HazeBandMarchAnchor);
        s = s.Replace(Payloads.HazeBandSkyStock, Payloads.HazeBandSkyPatch);
        File.WriteAllText(path, s);
        Log("horizontal haze-band march applied (sky)");
    }

    // Give both cloud passes the rings' shadow, which the game (2026.9.22) draws on the atmosphere
    // and the surface only: our opaque Saturn deck covered it. Builds without ring data in the
    // atmosphere UBO have nothing to shadow with, so they are passed over without a warning.
    private static void PatchCloudRingShadows(string shadersDir)
    {
        string uboPath = Path.Combine(shadersDir, "Atmosphere", "AtmosphereDataUbo.glsl");
        if (!File.Exists(Path.Combine(shadersDir, "Common", "RingShadows.glsl"))
            || !File.Exists(uboPath) || !ReadLf(uboPath).Contains("ringTextureId"))
        {
            Log("cloud ring shadows: this game build has none to add to");
            return;
        }

        int applied = 0, byGame = 0;
        foreach (var (file, stock, patched) in Payloads.CloudRingShadowSites)
        {
            string path = Path.Combine(shadersDir, "Clouds", file);
            if (!File.Exists(path)) { Log($"WARN: {file} missing, skipping its ring shadows"); continue; }
            string s = ReadLf(path);
            if (s.Contains(Payloads.CloudRingShadowMarker)) { applied++; continue; }

            // A game build that shadows its clouds itself would get the shadow twice
            if (s.Contains("RingShadow") || s.Contains("useRingShadows")) { byGame++; continue; }

            if (CountOf(s, Payloads.CloudMainAnchor) != 1 || CountOf(s, stock) != 1)
            { Log($"WARN: {file} main() or eclipse anchor not found, skipping its ring shadows"); continue; }

            s = s.Replace(Payloads.CloudMainAnchor,
                Payloads.CloudRingShadowFunc.Trim('\n') + "\n\n" + Payloads.CloudMainAnchor);
            s = s.Replace(stock, patched);
            File.WriteAllText(path, s);
            applied++;
        }
        Log($"cloud ring shadows applied ({applied}/{Payloads.CloudRingShadowSites.Length}"
            + (byGame > 0 ? $", {byGame} already shadowed by the game" : "") + ")");
    }

    /// <summary>Earth's faint clouds kept in the volumetrics (Payloads.CoverageLiftFunc): the map value lifted to the
    /// one whose volumetric opacity matches the 2D layer's. Both edits to CloudFunctions.glsl, or neither.</summary>
    private static void PatchCoverageLift(string shadersDir)
    {
        string path = Path.Combine(shadersDir, "Clouds", "CloudFunctions.glsl");
        if (!File.Exists(path)) { Log("WARN: CloudFunctions.glsl missing, Earth's faint clouds stay the game's"); return; }
        string s = ReadLf(path);
        if (s.Contains(Payloads.CoverageLiftMarker)) { Log("faint volumetric clouds already lifted"); return; }
        int func = CountOf(s, Payloads.CoverageLiftFuncAnchor), call = CountOf(s, Payloads.CoverageLiftCallAnchor);
        if (func != 1 || call != 1)
        {
            Log($"WARN: CloudFunctions.glsl coverage anchors found {func}x and {call}x (expected 1 each), Earth's faint clouds stay the game's");
            return;
        }
        s = s.Replace(Payloads.CoverageLiftFuncAnchor, Payloads.CoverageLiftFunc.TrimStart('\n') + Payloads.CoverageLiftFuncAnchor)
             .Replace(Payloads.CoverageLiftCallAnchor, Payloads.CoverageLiftCall);
        File.WriteAllText(path, s);
        Log("Earth's faint clouds and Mars's faint dust lifted in the volumetrics (CloudFunctions.glsl)");
    }

    /// <summary>The cloud map flows in the volumetric clouds as in the 2D layer (Payloads.MapFlowFunc): the helper
    /// and both lookups in CloudFunctions.glsl, or none of them.</summary>
    private static void PatchMapFlow(string shadersDir)
    {
        string path = Path.Combine(shadersDir, "Clouds", "CloudFunctions.glsl");
        if (!File.Exists(path)) { Log("WARN: CloudFunctions.glsl missing, the volumetric clouds' map stays still"); return; }
        string s = ReadLf(path);
        if (s.Contains(Payloads.MapFlowMarker)) { Log("volumetric cloud map flow already in"); return; }
        int func = CountOf(s, Payloads.CoverageLiftFuncAnchor), lookup = CountOf(s, Payloads.MapFlowLookupAnchor),
            colour = CountOf(s, Payloads.MapFlowColourAnchor);
        if (func != 1 || lookup != 1 || colour != 1)
        {
            Log($"WARN: CloudFunctions.glsl map anchors found {func}x, {lookup}x and {colour}x (expected 1 each), the volumetric clouds' map stays still");
            return;
        }
        s = s.Replace(Payloads.CoverageLiftFuncAnchor, Payloads.MapFlowFunc.TrimStart('\n') + Payloads.CoverageLiftFuncAnchor)
             .Replace(Payloads.MapFlowLookupAnchor, Payloads.MapFlowLookup)
             .Replace(Payloads.MapFlowColourAnchor, Payloads.MapFlowColour);
        File.WriteAllText(path, s);
        Log("the cloud map flows in the volumetric clouds as in the 2D layer (CloudFunctions.glsl)");
    }

    /// <summary>Triton's thin clouds shadow the ground from above the 2D switch no more than from below it
    /// (Payloads.ThinShadowPatched, in CloudShadows.glsl).</summary>
    private static void PatchThinShadow(string shadersDir)
    {
        string path = Path.Combine(shadersDir, "Clouds", "CloudShadows.glsl");
        if (!File.Exists(path)) { Log("WARN: CloudShadows.glsl missing, Triton's 2D clouds keep their shadows"); return; }
        string s = ReadLf(path);
        if (s.Contains(Payloads.ThinShadowMarker)) { Log("Triton's thin clouds already shadow the ground by their column"); return; }
        int n = CountOf(s, Payloads.ThinShadowAnchor);
        if (n != 1)
        {
            Log($"WARN: CloudShadows.glsl anchor found {n}x (expected 1), Triton's 2D clouds keep their shadows");
            return;
        }
        File.WriteAllText(path, s.Replace(Payloads.ThinShadowAnchor, Payloads.ThinShadowPatched));
        Log("Triton's thin clouds shadow the ground from above the 2D switch no more than their column allows (CloudShadows.glsl)");
    }

    /// <summary>True once the raymarch lights an optically thick step at its near face (Payloads.FaceLight).
    /// Jupiter's deck is made dense only then: lit the game's way a dense deck darkens with distance.</summary>
    private static bool _faceLight;

    /// <summary>The giants' decks lit at the near face of an optically thick step, under the cloud's own shadow
    /// there (Payloads.FaceLight, NearLight): all three edits to RaymarchCloud.comp, or none.</summary>
    private static void PatchFaceLight(string shadersDir)
    {
        _faceLight = false;
        string path = Path.Combine(shadersDir, "Clouds", "RaymarchCloud.comp");
        if (!File.Exists(path)) { Log("WARN: RaymarchCloud.comp missing, dense cloud decks stay lit at each step's far end"); return; }
        string s = ReadLf(path);
        if (s.Contains(Payloads.FaceLightMarker)) { _faceLight = true; Log("dense cloud decks already lit at the face"); return; }
        int light = CountOf(s, Payloads.FaceLightAnchor), position = CountOf(s, Payloads.FaceLightPositionAnchor),
            near = CountOf(s, Payloads.NearLightAnchor), func = CountOf(s, Payloads.FaceLightFuncAnchor);
        if (light != 1 || position != 1 || near != 1 || func != 1)
        {
            Log($"WARN: RaymarchCloud.comp lighting anchors found {light}x, {position}x, {near}x and {func}x (expected 1 each), dense cloud decks stay lit at each step's far end");
            return;
        }
        s = s.Replace(Payloads.FaceLightFuncAnchor, Payloads.FaceLightFunc)
             .Replace(Payloads.FaceLightAnchor, Payloads.FaceLight)
             .Replace(Payloads.NearLightAnchor, Payloads.NearLight)
             .Replace(Payloads.FaceLightPositionAnchor, Payloads.FaceLightPosition);
        File.WriteAllText(path, s);
        _faceLight = true;
        Log("the giants' and Venus's cloud decks lit at the near face of an optically thick step (RaymarchCloud.comp)");
    }

    /// <summary>The planet's own shadow on its terrain and rocks, softened by the body's relief
    /// (Payloads.TerrainShadowSites). Each shader is patched whole or not at all.</summary>
    private static void PatchTerrainShadow(string shadersDir)
    {
        int done = 0, files = 0;
        foreach (var group in Payloads.TerrainShadowSites.GroupBy(site => site.File))
        {
            files++;
            string path = Path.Combine(shadersDir, group.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { Log($"WARN: {group.Key} missing, its terminator stays the game's"); continue; }
            string s = ReadLf(path);
            if (s.Contains(Payloads.TerrainShadowMarker)) { done++; continue; } // already applied
            var off = group.Where(site => CountOf(s, site.Stock) != 1).ToList();
            if (off.Count > 0)
            {
                Log($"WARN: {group.Key} anchor found {CountOf(s, off[0].Stock)}x (expected 1), its terminator stays the game's: {Truncate(off[0].Stock)}");
                continue;
            }
            foreach (var site in group)
                s = s.Replace(site.Stock, site.Patch);
            File.WriteAllText(path, s);
            done++;
        }
        Log($"terrain terminator softened by relief ({done}/{files} files)");
    }

    /// <summary>Each cloudy body's 2D/volumetric band and its layers' light march (Payloads.CloudLighting), over
    /// whatever the file holds, the game's or ours. A layer's light distance is the longest path a sunbeam can take
    /// inside it, from its cloud types and the body's radius as loaded, at most Payloads.MaxLightDistanceKm.</summary>
    private static string PatchCloudLighting(string s, string label)
    {
        var inv = CultureInfo.InvariantCulture;
        foreach (var (body, startKm, endKm, shadowsKm, flickerKm, layers) in Payloads.CloudLighting)
        {
            int b = s.IndexOf($"<AtmosphericBody Id=\"{body}\"", StringComparison.Ordinal);
            if (b < 0)
                continue; // not in this file
            int bEnd = s.IndexOf("</AtmosphericBody>", b, StringComparison.Ordinal);
            int c = bEnd < 0 ? -1 : s.IndexOf("<Clouds>", b, bEnd - b, StringComparison.Ordinal);
            int cEnd = c < 0 ? -1 : s.IndexOf("</Clouds>", c, StringComparison.Ordinal);
            var radius = bEnd < 0 ? Match.Empty : Regex.Match(s[b..bEnd], @"<MeanRadius\s+Km=""([^""]+)""");
            if (c < 0 || cEnd < 0 || cEnd > bEnd || !radius.Success)
            {
                Log($"WARN: {label}: {body}'s clouds or radius not found, its cloud lighting stays");
                continue;
            }
            double radiusKm = double.Parse(radius.Groups[1].Value, NumberStyles.Float, inv);

            // the band is set before the first layer, the light march inside each layer
            string clouds = s[c..cEnd];
            int head = clouds.IndexOf("<Layer", StringComparison.Ordinal);
            if (head < 0) head = clouds.Length;
            string bandXml = clouds[..head], rest = clouds[head..];
            int set = SetElement(ref bandXml, "OrbitTransitionStartAltitude", $"Km=\"{startKm.ToString(inv)}\"")
                    + SetElement(ref bandXml, "OrbitTransitionEndAltitude", $"Km=\"{endKm.ToString(inv)}\""), wanted = 2;
            if (shadowsKm is int shadows)
            {
                wanted++;
                set += SetElement(ref bandXml, "MaxShadowsAltitude", $"Km=\"{shadows.ToString(inv)}\"");
            }
            if (flickerKm is int flicker)
            {
                wanted++;
                set += SetElement(ref bandXml, "VolumetricsFlickerReductionDistance", $"Km=\"{flicker.ToString(inv)}\"");
            }
            if (set != wanted)
                Log($"WARN: {label}: {body}'s 2D/volumetric band: {set} of {wanted} values found to set");

            var lit = new List<string>();
            foreach (var (layer, samples) in layers)
            {
                int l = rest.IndexOf($"<Layer Id=\"{layer}\">", StringComparison.Ordinal);
                int lEnd = l < 0 ? -1 : rest.IndexOf("</Layer>", l, StringComparison.Ordinal);
                if (lEnd < 0)
                {
                    Log($"WARN: {label}: {body}'s layer {layer} not found, its light march stays");
                    continue;
                }
                string text = rest[l..lEnd];
                double bottom = double.MaxValue, top = double.MinValue;
                foreach (Match type in Regex.Matches(text, @"<CloudType\b.*?</CloudType>", RegexOptions.Singleline))
                {
                    if (DistanceKm(type.Value, "StartAltitude") is double start && DistanceKm(type.Value, "Height") is double height)
                    {
                        bottom = Math.Min(bottom, start);
                        top = Math.Max(top, start + height);
                    }
                }
                if (bottom > top)
                {
                    Log($"WARN: {label}: {body}'s layer {layer} has no cloud types to measure, its light march stays");
                    continue;
                }
                double path = Math.Sqrt((radiusKm + top) * (radiusKm + top) - (radiusKm + bottom) * (radiusKm + bottom));
                double lightKm = Math.Min(Math.Round(path), Payloads.MaxLightDistanceKm);
                if (SetElement(ref text, "LightDistance", $"Km=\"{lightKm.ToString("0", inv)}\"")
                    + SetElement(ref text, "LightSamples", $"Value=\"{samples.ToString(inv)}\"") != 2)
                {
                    Log($"WARN: {label}: {body}'s layer {layer} has no light march to set");
                    continue;
                }
                foreach (var (typeLayer, typeName, density) in Payloads.CloudTypeDensities)
                {
                    if (typeLayer != layer)
                        continue;
                    int t0 = text.IndexOf($"<CloudType Name=\"{typeName}\">", StringComparison.Ordinal);
                    int t1 = t0 < 0 ? -1 : text.IndexOf("</CloudType>", t0, StringComparison.Ordinal);
                    string type = t1 < 0 ? "" : text[t0..t1];
                    if (t1 < 0 || SetElement(ref type, "Density", $"Value=\"{density.ToString("0.########", inv)}\"") != 1)
                    {
                        Log($"WARN: {label}: {body}'s layer {layer} has no {typeName} density to set");
                        continue;
                    }
                    text = text[..t0] + type + text[t1..];
                }
                string maxStep = "";
                foreach (var (stepLayer, maxStepM) in Payloads.CloudMaxSteps)
                {
                    if (stepLayer != layer)
                        continue;
                    if (SetElement(ref text, "MaxSize", $"M=\"{maxStepM.ToString(inv)}\"") == 1)
                        maxStep = $", steps up to {(maxStepM / 1000.0).ToString("0.#", inv)} km";
                    else
                        Log($"WARN: {label}: {body}'s layer {layer} has no maximum step to set");
                }
                rest = rest[..l] + text + rest[lEnd..];
                lit.Add($"{layer} {lightKm.ToString("0", inv)} km x {samples}{maxStep}");
            }
            s = s[..c] + bandXml + rest + s[cEnd..];
            Log($"{label}: {body}'s clouds 2D/volumetric {startKm}-{endKm} km, light {string.Join(", ", lit)}");
        }
        return s;
    }

    /// <summary>A distance element's value in km, whatever unit it is written in (M or Km).</summary>
    private static double? DistanceKm(string text, string tag)
    {
        var m = Regex.Match(text, $@"<{tag}\s+(M|Km)=""([^""]+)""");
        if (!m.Success)
            return null;
        double v = double.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return m.Groups[1].Value == "M" ? v / 1000.0 : v;
    }

    /// <summary>Replaces the first self-closing element of that name with one carrying the given attribute.</summary>
    /// <returns>1 if it was there</returns>
    private static int SetElement(ref string text, string tag, string attribute)
    {
        var element = new Regex($@"<{tag}\s+\w+=""[^""]*""\s*/>");
        if (!element.IsMatch(text))
            return 0;
        text = element.Replace(text, $"<{tag} {attribute} />", 1);
        return 1;
    }

    private static string PatchCloudTerminator(string s)
    {
        if (s.Contains(Payloads.TerminatorMarker))
            return s; // already patched

        int n = CountOf(s, Payloads.TerminatorAnchor);
        if (n != 1)
        {
            Log($"WARN: 2DCloud.comp anchor found {n}x (expected 1), skipping terminator fade");
            return s;
        }
        return s.Replace(Payloads.TerminatorAnchor, Payloads.TerminatorFade);
    }

    private static string PatchCloudSkylight(string s)
    {
        if (s.Contains(Payloads.SkylightMarker))
            return s; // already patched

        int colour = CountOf(s, Payloads.SkylightColourAnchor), add = CountOf(s, Payloads.SkylightAddAnchor);
        if (colour != 1 || add != 1)
        {
            Log($"WARN: 2DCloud.comp skylight anchors found {colour}x and {add}x (expected 1 each), skipping");
            return s;
        }
        return s.Replace(Payloads.SkylightColourAnchor, Payloads.SkylightColourPatched)
                .Replace(Payloads.SkylightAddAnchor, Payloads.SkylightAddPatched);
    }

    /// <summary>Triton's 2D clouds given the brightness their colour setting cannot reach (Payloads.CloudGainPatched).</summary>
    private static string PatchCloudGain(string s)
    {
        if (s.Contains(Payloads.CloudGainMarker))
            return s; // already patched

        int n = CountOf(s, Payloads.CloudGainAnchor);
        if (n != 1)
        {
            Log($"WARN: 2DCloud.comp colour anchor found {n}x (expected 1), Triton's 2D clouds stay as dim as the game draws them");
            return s;
        }
        return s.Replace(Payloads.CloudGainAnchor, Payloads.CloudGainPatched);
    }

    /// <summary>Venus's 2D cloud shell made opaque, its map shading the cloud (Payloads.VenusShellPatched).</summary>
    private static string PatchVenusShell(string s)
    {
        if (s.Contains(Payloads.VenusShellMarker))
            return s; // already patched

        int n = CountOf(s, Payloads.VenusShellAnchor);
        if (n != 1)
        {
            Log($"WARN: 2DCloud.comp light anchor found {n}x (expected 1), Venus's 2D clouds stay see-through to the gas below");
            return s;
        }
        return s.Replace(Payloads.VenusShellAnchor, Payloads.VenusShellPatched(s.Contains(Payloads.SkylightMarker)));
    }

    // ---------------- helpers ----------------

    /// <returns>true if the transform changed the file's content</returns>
    private static bool PatchFile(string path, Func<string, string> transform)
    {
        if (!File.Exists(path))
        {
            Log($"WARN: shadow file missing: {path}");
            return false;
        }
        string before = ReadLf(path);
        string after = transform(before);
        File.WriteAllText(path, after);
        return !ReferenceEquals(before, after) && before != after;
    }

    /// <summary>Read with newlines normalized to LF so multi-line anchors match
    /// regardless of the file's line endings (same as the Python patchers).</summary>
    private static string ReadLf(string path) =>
        File.ReadAllText(path).Replace("\r\n", "\n");

    private static void CopyTree(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(dst, Path.GetRelativePath(src, dir)));
        foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(dst, Path.GetRelativePath(src, file)), overwrite: true);
    }

    private static int CountOf(string s, string needle)
    {
        int count = 0;
        for (int i = s.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = s.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string Truncate(string s) =>
        s.Length <= 60 ? s : s[..60] + "...";

    internal static void Log(string msg) => Console.WriteLine("RealAtmospheres - " + msg);
}
