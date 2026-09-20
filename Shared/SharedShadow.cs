using System.Diagnostics;

namespace RealModShared;

/// <summary>
/// Cooperative shared shadow for the Core content files that BOTH "Real Atmospheres"
/// and "Real Surfaces" patch: the SYSTEM templates (SolSystem.xml, SolSystemDense.xml)
/// AND Astronomicals.xml (the library where Venus - a LoadFromLibrary body - is defined,
/// so it's the only place RS can reach Venus's surface).
///
/// StarMap's redirect swaps the Core mod's entry to point at ONE shadow copy, so two
/// mods can't each keep a private shadow of these files. Instead both mods write into a
/// single shared shadow at &lt;mods&gt;\_RealSharedShadow\Core, and both redirect there
/// (systems via Mod.LoadSystems/PrepareSystems, Astronomicals via Mod.LoadAssetBundles).
/// RA owns the atmosphere/cloud edits; RS owns the surfaces (incl. Venus dunes/clutter).
/// Coordination:
///   - a global named Mutex serializes the two mods' read-modify-write,
///   - a per-launch token (the game process start time) makes the fresh stock copy
///     happen exactly ONCE per launch (whichever mod runs first), so the second mod
///     patches ON TOP of the first's edits instead of clobbering them with fresh stock.
/// Each mod's edits are marker-guarded (idempotent), so the two apply cleanly in
/// either order for disjoint edits; where one mod's edit anchors on the other's output
/// (Triton), the edit is fail-soft and the mods load in name order (RealAtmospheres
/// before RealSurfaces), so the structural mod runs first.
///
/// Whichever subset of the two mods is installed, each still produces a valid shared
/// shadow (stock base + its own edits) and redirects to it.
/// </summary>
internal static class SharedShadow
{
    /// <summary>Core files both mods may patch (kept in the shared shadow). Astronomicals.xml
    /// is redirected via Mod.LoadAssetBundles; the SolSystem* templates via Mod.LoadSystems.
    /// Callers split the returned map by filename to drive the right redirect.</summary>
    public static readonly string[] SharedFiles = { "Astronomicals.xml", "SolSystem.xml", "SolSystemDense.xml" };

    /// <summary>&lt;mods&gt;\_RealSharedShadow\Core — derived from either mod's own folder
    /// (its parent is the mods dir), so both mods compute the identical path.</summary>
    public static string CoreDir(string modDir)
        => Path.Combine(Path.GetDirectoryName(modDir.TrimEnd('\\', '/'))!, "_RealSharedShadow", "Core");

    /// <summary>Under the global mutex: ensure a fresh stock copy of the system files
    /// exists for THIS launch (once), then invoke <paramref name="applyEdits"/> on each
    /// so the caller patches it in place (idempotently). Returns filename -&gt; shared
    /// absolute path for the caller's redirect. Fail-soft: any IO/lock error logs and
    /// returns whatever was produced so far (the mod then runs stock for those files).</summary>
    public static Dictionary<string, string> Build(string coreDir, string modDir,
        Func<string, string, string> applyEdits, Action<string> log)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string sharedCore = CoreDir(modDir);
        string token = LaunchToken();

        Mutex? mutex = null;
        bool held = false;
        try
        {
            mutex = new Mutex(false, @"Global\RealSharedShadow_v1");
            try { held = mutex.WaitOne(TimeSpan.FromSeconds(30)); }
            catch (AbandonedMutexException) { held = true; } // prior holder crashed; the state we rebuild is self-healing

            Directory.CreateDirectory(sharedCore);
            string tokenFile = Path.Combine(sharedCore, ".launch_token");
            bool fresh = ReadAllTextSafe(tokenFile) != token;   // first mod this launch rebuilds the base

            foreach (string name in SharedFiles)
            {
                string src = Path.Combine(coreDir, name);
                if (!File.Exists(src)) continue;
                string dst = Path.Combine(sharedCore, name);
                if (fresh || !File.Exists(dst))
                    // fresh stock base, once per launch; LF-normalized so both mods'
                    // (LF) anchors match regardless of the stock file's line endings
                    File.WriteAllText(dst, File.ReadAllText(src).Replace("\r\n", "\n").Replace("\r", "\n"));
                string content = File.ReadAllText(dst);
                string patched = applyEdits(name, content);      // this mod's edits (idempotent)
                if (!ReferenceEquals(patched, content) && patched != content)
                    File.WriteAllText(dst, patched);
                result[name] = dst;
            }

            if (fresh)
            {
                File.WriteAllText(tokenFile, token);
                log("shared shadow: fresh stock base for this launch");
            }
        }
        catch (Exception ex)
        {
            log("WARN: shared shadow build failed (system files run stock): " + ex.Message);
        }
        finally
        {
            if (held) { try { mutex!.ReleaseMutex(); } catch { } }
            mutex?.Dispose();
        }
        return result;
    }

    private static string LaunchToken()
    {
        try { return Process.GetCurrentProcess().StartTime.ToBinary().ToString(); }
        catch { return "0"; }
    }

    private static string ReadAllTextSafe(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
        catch { return ""; }
    }
}
