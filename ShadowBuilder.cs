using System.Globalization;
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

    /// <summary>The Mars dust phase GLSL block to inject: the Level-2 LUT (from
    /// assets) if present, else the embedded Level-1 HG fit.</summary>
    private static string _phaseBlock = Payloads.PatchedPhaseBlock;

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
        string lut = Path.Combine(assetsDir, "MarsDustPhaseLut.glsl");
        _phaseBlock = File.Exists(lut) ? ReadLf(lut).Trim('\n') : Payloads.PatchedPhaseBlock;
        if (File.Exists(lut)) Log("Mars phase: LEVEL 2 (exact bhmie LUT)");

        int shaderPatches = 0;
        shaderPatches += PatchFile(
            Path.Combine(dstShaders, "Atmosphere", "AtmosphereFunctions.glsl"),
            PatchAtmosphereFunctions) ? 1 : 0;
        shaderPatches += PatchFile(
            Path.Combine(dstShaders, "Clouds", "2DCloud.comp"),
            PatchCloudTerminator) ? 1 : 0;
        PatchMarsDensity(dstShaders);
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
            sys = PatchAtmosphereValues(sys, Payloads.NeptunePairs, "Neptune");
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
        xml = PatchAtmosphereValues(xml, Payloads.JupiterPairs, "Jupiter");
        xml = PatchAtmosphereValues(xml, Payloads.SaturnPairs, "Saturn");
        xml = PatchAtmosphereValues(xml, Payloads.UranusPairs, "Uranus");
        xml = PatchJupiterClouds(xml);
        xml = SwapDiffuse(xml, "Saturn", assetsDir);
        xml = SwapDiffuse(xml, "Uranus", assetsDir);
        xml = InsertGiantClouds(xml, "Saturn", assetsDir, 95000);
        xml = InsertGiantClouds(xml, "Uranus", assetsDir, 70000);
        xml = PatchCirrusLayer(xml, assetsDir);
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
                        Payloads.MarsDensityHelper.Trim('\n') + "\n\n" + Payloads.AtmosphereDataEndif);
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
