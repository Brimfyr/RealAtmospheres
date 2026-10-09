using HarmonyLib;

namespace RealAtmospheres;

/// <summary>
/// The player's settings, changed in the game through ModMenu (Menu.cs) and kept beside the game's own settings, in
/// RealAtmospheres/settings.toml under its documents folder (which Borea gives each instance its own of). Plain TOML,
/// safe to edit while the game is closed. A missing or unreadable file is the defaults.
/// </summary>
internal static class Settings
{
    /// <summary>
    /// Whether the haze of Pluto, Triton and Titan is cut into its drifting bands (PlutoHazeBandFactor and the
    /// per-pixel sky march that draws them). Off, their haze is the smooth one underneath, and no pixel is marched.
    /// The shaders are told through the haze clock: see Patches.FillAtmosphereDataPrefix.
    /// </summary>
    internal const bool DefaultHazeBands = true;

    internal static bool HazeBands { get; private set; } = DefaultHazeBands;

    private const string HazeBandsKey = "haze_bands";

    private static string? _path;
    private static bool _loaded;

    /// <summary>The file, under the game's documents folder (KSA.Constants.DocumentsFolderPath).</summary>
    internal static string? FilePath
    {
        get
        {
            if (_path != null) return _path;
            try
            {
                Type? constants = AccessTools.TypeByName("KSA.Constants");
                if (AccessTools.Property(constants, "DocumentsFolderPath")?.GetValue(null) is string documents)
                    _path = Path.Combine(documents, "RealAtmospheres", "settings.toml");
            }
            catch (Exception ex)
            {
                ShadowBuilder.Log("WARN: the game's documents folder was not found; settings are not kept: " + ex.Message);
            }
            return _path;
        }
    }

    /// <summary>Reads the file once; the defaults stand for anything it does not say.</summary>
    internal static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        string? path = FilePath;
        if (path == null || !File.Exists(path))
        {
            ShadowBuilder.Log($"settings: the defaults ({Summary()})");
            return;
        }
        try
        {
            HazeBands = Read(File.ReadAllLines(path));
            ShadowBuilder.Log($"settings from {path}: {Summary()}");
        }
        catch (Exception ex)
        {
            ShadowBuilder.Log("WARN: the settings file could not be read, the defaults stand: " + ex.Message);
        }
    }

    /// <summary>The haze bands' switch as a settings file has it; the default if it does not say.</summary>
    internal static bool Read(IEnumerable<string> lines)
    {
        bool bands = DefaultHazeBands;
        foreach (string line in lines)
            if (Parse(line) is (string key, string value) && key == HazeBandsKey && bool.TryParse(value, out bool on))
                bands = on;
        return bands;
    }

    /// <summary>A line's key and value, or null for a comment, a blank or anything else.</summary>
    internal static (string Key, string Value)? Parse(string line)
    {
        string s = line.Trim();
        if (s.Length == 0 || s[0] == '#') return null;
        int eq = s.IndexOf('=');
        if (eq <= 0) return null;
        string value = s[(eq + 1)..];
        int hash = value.IndexOf('#');
        if (hash >= 0) value = value[..hash];
        return (s[..eq].Trim(), value.Trim());
    }

    private static string Summary() => "haze bands " + (HazeBands ? "on" : "off");

    /// <summary>Sets the switch and writes the file: a click, not a drag, so at once.</summary>
    internal static void SetHazeBands(bool bands)
    {
        Load();
        if (bands == HazeBands) return;
        HazeBands = bands;
        string? path = FilePath;
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Text());
        }
        catch (Exception ex)
        {
            ShadowBuilder.Log("WARN: the settings file could not be written: " + ex.Message);
        }
    }

    internal static string Text() => $"""
        # Real Atmospheres settings, written by its page in the game's Mods menu (ModMenu). Safe to edit with the game closed.

        # The drifting bands in the haze of Pluto, Triton and Titan: true or false. False leaves their haze smooth.
        {HazeBandsKey} = {(HazeBands ? "true" : "false")}

        """;
}
