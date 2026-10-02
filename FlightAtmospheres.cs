using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace RealAtmospheres;

/// <summary>Measured flight atmospheres. KSA's flight model gives every body one exponential for
/// density and pressure (PhysicalAtmosphereReference); drag, engine back-pressure, exhaust plumes
/// and the atmosphere's boundary (where drag starts and time warp is limited) all read those two
/// functions and that boundary. For the bodies in AtmosphereTables.Flight they now return the
/// measured profile instead. A body is matched by its template Id as its atmosphere loads, so a
/// same-named body elsewhere gets the table and every other body keeps the stock model. If any of
/// the game's members has moved, nothing is patched and flight stays stock.</summary>
internal static class FlightAtmospheres
{
    private static readonly ConditionalWeakTable<object, FlightTable> Registered = new();
    private static PropertyInfo? _id;
    private static FieldInfo? _atmosphere, _physical, _height;
    private static MethodInfo? _fromMeters;

    public static void Install(Harmony harmony)
    {
        Type? template = AccessTools.TypeByName("KSA.AstronomicalTemplate");
        Type? atmosphere = AccessTools.TypeByName("KSA.AtmosphereReference");
        Type? physical = AccessTools.TypeByName("KSA.PhysicalAtmosphereReference");
        Type? distance = AccessTools.TypeByName("KSA.DistanceReference");
        MethodInfo? loaded = template == null ? null : AccessTools.DeclaredMethod(template, "OnDataLoad");
        MethodInfo? density = physical == null ? null : AccessTools.Method(physical, "GetAtmosphericDensityAtAltitude", new[] { typeof(double) });
        MethodInfo? pressure = physical == null ? null : AccessTools.Method(physical, "GetAtmosphericPressureAtAltitude", new[] { typeof(double) });
        _id = template == null ? null : AccessTools.Property(template, "Id");
        _atmosphere = template == null ? null : AccessTools.Field(template, "AtmosphereReference");
        _physical = atmosphere == null ? null : AccessTools.Field(atmosphere, "Physical");
        _height = physical == null ? null : AccessTools.Field(physical, "_height");
        _fromMeters = distance == null ? null : AccessTools.Method(distance, "FromMeters", new[] { typeof(double) });

        if (loaded == null || density == null || pressure == null || _id == null || _atmosphere == null
            || _physical == null || _height == null || _fromMeters == null
            || density.GetParameters()[0].Name != "altitudeInMeters" || pressure.GetParameters()[0].Name != "altitudeInMeters")
        {
            ShadowBuilder.Log("WARN: the game's flight atmosphere has changed; measured flight atmospheres not installed");
            return;
        }
        try
        {
            harmony.Patch(loaded, postfix: new HarmonyMethod(typeof(FlightAtmospheres), nameof(TemplateLoadedPostfix)));
            harmony.Patch(density, prefix: new HarmonyMethod(typeof(FlightAtmospheres), nameof(DensityPrefix)));
            harmony.Patch(pressure, prefix: new HarmonyMethod(typeof(FlightAtmospheres), nameof(PressurePrefix)));
            ShadowBuilder.Log($"measured flight atmospheres ready for {AtmosphereTables.Flight.Length} bodies");
        }
        catch (Exception e)
        {
            ShadowBuilder.Log($"WARN: could not patch the flight atmosphere ({e.GetType().Name}); flight stays stock");
        }
    }

    /// <summary>After a body's template (and so its atmosphere) loads: attach its table and move
    /// the atmosphere's boundary to where the measured profile crosses KSA's thresholds.</summary>
    public static void TemplateLoadedPostfix(object __instance)
    {
        try
        {
            FlightTable? table = AtmosphereTables.ForBody(_id!.GetValue(__instance) as string);
            object? atmosphere = table == null ? null : _atmosphere!.GetValue(__instance);
            object? physical = atmosphere == null ? null : _physical!.GetValue(atmosphere);
            if (physical == null)
                return;
            Registered.AddOrUpdate(physical, table!);
            _height!.SetValue(physical, _fromMeters!.Invoke(null, new object[] { table!.BoundaryM }));
            ShadowBuilder.Log($"{table.Body}: measured flight atmosphere, boundary {table.BoundaryM / 1000.0:F0} km");
        }
        catch (Exception e)
        {
            ShadowBuilder.Log($"WARN: flight atmosphere not attached ({e.GetType().Name})");
        }
    }

    public static bool DensityPrefix(object __instance, double altitudeInMeters, ref double __result)
    {
        if (!Registered.TryGetValue(__instance, out FlightTable? table))
            return true;
        __result = table.Density(altitudeInMeters);
        return false;
    }

    public static bool PressurePrefix(object __instance, double altitudeInMeters, ref double __result)
    {
        if (!Registered.TryGetValue(__instance, out FlightTable? table))
            return true;
        __result = table.Pressure(altitudeInMeters);
        return false;
    }
}
