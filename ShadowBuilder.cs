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
            PatchCloudTerminator) ? 1 : 0;
        PatchMarsDensity(dstShaders);
        PatchRayleighColour(dstShaders);
        PatchHazeBandMarch(dstShaders);
        PatchCloudRingShadows(dstShaders);
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
            sys = InsertGiantClouds(sys, "Neptune", assetsDir, 70000);
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
        xml = PatchAtmosphereValues(xml, Payloads.JupiterPairs, "Jupiter");
        xml = PatchAtmosphereValues(xml, Payloads.SaturnPairs, "Saturn");
        xml = PatchAtmosphereValues(xml, Payloads.UranusPairs, "Uranus");
        xml = PatchJupiterClouds(xml);
        xml = SwapDiffuse(xml, "Saturn", assetsDir);
        xml = SwapDiffuse(xml, "Uranus", assetsDir);
        xml = InsertGiantClouds(xml, "Saturn", assetsDir, 95000);
        xml = InsertGiantClouds(xml, "Uranus", assetsDir, 70000);
        xml = PatchCirrusLayer(xml, assetsDir);
        xml = PatchAtmosphereValues(xml, Payloads.EarthPairs, "Earth");
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
    /// <summary>Insert a Jupiter-style volumetric Clouds block after the body's
    /// &lt;/Atmosphere&gt; (Clouds is a sibling of Atmosphere). Gated on the coverage
    /// mask existing (delete it to revert); idempotent on the layer Id.</summary>
    private static string InsertGiantClouds(string content, string body, string assetsDir, int towerHeightM)
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
        // Every number here goes into the XML, which the game reads in the invariant format.
        // Under a decimal-comma culture (German, French, Spanish and most of Europe) the loop
        // came out as 0,389, and the file holding it failed to load whole: Astronomicals.xml
        // took the Sol template with it, and the game could not start.
        var xml = CultureInfo.InvariantCulture;
        string disp = dispKm.ToString("0", xml);
        string loop = loopHours.ToString("0.###", xml);

        // Volumetric-to-2D fade band, as fractions of each planet's radius matching
        // stock Jupiter's (7.2% and 12.9%). Fading closer in makes the swap obvious.
        var (transStart, transEnd) = body switch
        {
            "Saturn"  => (4200, 7500),
            "Uranus"  => (1800, 3300),
            "Neptune" => (1800, 3200),
            _         => (1800, 3300)
        };

        string block = Payloads.GiantCloudsTemplate
            .Replace("{FLOWMAP}", hasFlow ? Payloads.GiantFlowMapTemplate : "")
            .Replace("{VOLFLOWTEX}", hasFlow ? Payloads.GiantVolFlowTexPerPlanet : Payloads.GiantVolFlowTexJupiter)
            .Replace("{DISP}", disp)
            .Replace("{LOOP}", loop)
            .Replace("{TRANSSTART}", transStart.ToString(xml))
            .Replace("{TRANSEND}", transEnd.ToString(xml))
            .Replace("{FLICKER}", (transEnd * 11 / 10).ToString(xml))
            .Replace("{BODY}", body).Replace("{ASSETS}", assetsDir)
            .Replace("{HEIGHT}", towerHeightM.ToString(xml))
            // the storm types rise above the deck; heights scale with the per-planet tower
            // Shorter than the deck, so the belt edges read as depressions in it rather
            // than towers above it. Anything taller would raise the layer top, and the
            // 2D billboard hangs from that.
            .Replace("{HEIGHT_EDGE}", (towerHeightM * 9 / 20).ToString(xml));
;
        Log($"{body} volumetric clouds added{(hasFlow ? $" + per-planet flowmap (2D + volumetric), disp {disp}km, loop {loop}h ({peakJetMs:0} m/s jets)" : "")}");
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

        venus = venus[..t0] + Payloads.VenusCloudDeckTypes.Trim('\n') + venus[(t1 + typeEnd.Length)..];
        if (!File.Exists(Path.Combine(assetsDir, "VenusCloudDetail.png")))
            Log("WARN: VenusCloudDetail.png missing, Venus's deck keeps the stock tile's holes");
        else if (CountOf(venus, Payloads.VenusStockDetailTexture) != 1)
            Log("WARN: Venus's stock detail tile not found, the deck keeps its holes");
        else
            venus = venus.Replace(Payloads.VenusStockDetailTexture, Payloads.VenusDetailTexture.Replace("{ASSETS}", assetsDir));
        Log("Venus cloud deck: one solid deck 48-72 km, stock variety in short crowns");
        return s[..body] + venus + s[end..];
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
