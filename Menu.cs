using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RealAtmospheres;

/// <summary>
/// Real Atmospheres' page in the game's Mods menu, which ModMenu (MrJeranimo) adds when it is installed. ModMenu
/// finds the page by its attribute's name and calls it while its menu is open; without ModMenu nothing calls it, and
/// the settings stay as their file has them (Settings).
/// </summary>
public static class Menu
{
    private static bool _broken;

    [ModMenuEntry("Real Atmospheres")]
    public static void Draw()
    {
        if (_broken || !ImGuiCalls.Ready) return;
        try
        {
            Settings.Load();
            if (ImGuiCalls.MenuItem("Procedural haze bands##realatmospheres", Settings.HazeBands))
                Settings.SetHazeBands(!Settings.HazeBands);
            ImGuiCalls.SetItemTooltip(
                "The bands in the haze of Pluto, Triton and Titan, which drift round each body and change with time.\n"
                + "Unticked, their haze is smooth, and the sky there is not worked out pixel by pixel.");
        }
        catch (Exception ex)
        {
            _broken = true;
            ShadowBuilder.Log("WARN: Real Atmospheres' page in the Mods menu stopped: " + ex.GetBaseException().Message);
        }
    }
}

/// <summary>The marker ModMenu looks for, by name (its own source asks a mod to copy it): a page and its title.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class ModMenuEntryAttribute : Attribute
{
    public string MenuName { get; }
    public string? IsModMenuActivePropertyName { get; }

    public ModMenuEntryAttribute(string menuName, string? isModMenuActivePropertyName = null)
    {
        MenuName = menuName;
        IsModMenuActivePropertyName = isModMenuActivePropertyName;
    }
}

/// <summary>
/// The few ImGui calls the page makes, through the game's own ImGui (Brutal.ImGuiApi). Its labels are an ImString,
/// a ref struct that reflection cannot box, so each call is a small method built here in IL that turns our strings
/// into ImStrings on the stack and calls straight through. Fail-soft: anything not found leaves the page empty.
/// </summary>
internal static class ImGuiCalls
{
    internal delegate void TextCall(string text);
    internal delegate bool MenuItemCall(string label, bool selected);

    private static TextCall? _tooltip;
    private static MenuItemCall? _menuItem;
    private static bool _resolved, _ready;

    internal static bool Ready
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                try
                {
                    Resolve();
                    _ready = true;
                }
                catch (Exception ex)
                {
                    ShadowBuilder.Log("WARN: the game's ImGui calls were not found; Real Atmospheres' page in the Mods menu is empty: "
                                      + ex.GetBaseException().Message);
                }
            }
            return _ready;
        }
    }

    // A tooltip is a printf format, so a percent sign is written twice to show once.
    internal static void SetItemTooltip(string text) => _tooltip!(text.Replace("%", "%%"));

    /// <summary>A menu item, ticked when <paramref name="selected"/>; true on the frame it is clicked.</summary>
    internal static bool MenuItem(string label, bool selected) => _menuItem!(label, selected);

    /// <summary>Builds the calls; throws if the game's ImGui is not as expected.</summary>
    internal static void Resolve()
    {
        Type imgui = AccessTools.TypeByName("Brutal.ImGuiApi.ImGui") ?? throw new InvalidOperationException("Brutal.ImGuiApi.ImGui not found");
        Type imString = AccessTools.TypeByName("Brutal.ImGuiApi.ImString") ?? throw new InvalidOperationException("ImString not found");
        MethodInfo fromString = imString.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == imString
                                 && m.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(string))
            ?? throw new InvalidOperationException("ImString from string not found");

        MethodInfo Find(string name, params Type[] types) =>
            imgui.GetMethod(name, BindingFlags.Public | BindingFlags.Static, types)
            ?? throw new InvalidOperationException($"ImGui.{name} not found");

        _tooltip = TextLike(Find("SetItemTooltip", imString), fromString);

        MethodInfo menuItem = imgui.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "MenuItem" && m.GetParameters() is { Length: 4 } p && p[0].ParameterType == imString
                                 && p[1].ParameterType == imString && p[2].ParameterType == typeof(bool)
                                 && p[3].ParameterType == typeof(bool) && m.ReturnType == typeof(bool))
            ?? throw new InvalidOperationException("ImGui.MenuItem(label, shortcut, selected, enabled) not found");
        var itemIl = new DynamicMethod("RealAtmospheres_ImGui_MenuItem", typeof(bool), new[] { typeof(string), typeof(bool) },
                                       typeof(ImGuiCalls).Module, skipVisibility: true);
        ILGenerator il = itemIl.GetILGenerator();
        LocalBuilder none = il.DeclareLocal(imString);         // the shortcut: an empty ImString, as its default is
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, fromString);
        il.Emit(OpCodes.Ldloca_S, none);
        il.Emit(OpCodes.Initobj, imString);
        il.Emit(OpCodes.Ldloc, none);
        il.Emit(OpCodes.Ldarg_1);                              // selected: shown ticked
        il.Emit(OpCodes.Ldc_I4_1);                             // enabled
        il.Emit(OpCodes.Call, menuItem);
        il.Emit(OpCodes.Ret);
        _menuItem = (MenuItemCall)itemIl.CreateDelegate(typeof(MenuItemCall));
    }

    private static TextCall TextLike(MethodInfo call, MethodInfo fromString)
    {
        var method = new DynamicMethod("RealAtmospheres_ImGui_" + call.Name, typeof(void), new[] { typeof(string) },
                                       typeof(ImGuiCalls).Module, skipVisibility: true);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, fromString);
        il.Emit(OpCodes.Call, call);
        il.Emit(OpCodes.Ret);
        return (TextCall)method.CreateDelegate(typeof(TextCall));
    }
}
