namespace RealAtmospheres;

/// <summary>One body's measured flight atmosphere (AtmosphereTables.Generated.cs, from
/// make_flight_atmospheres.py): density and pressure against altitude above the mean radius,
/// interpolated linearly in their logarithms. Past either end the nearest segment continues.</summary>
internal sealed class FlightTable
{
    public readonly string Body;
    public readonly double BoundaryM;
    public readonly string SeaLevelPressureAtm, SeaLevelDensity, ScaleHeightKm;
    private readonly double[] _altitude, _lnDensity, _lnPressure;

    public FlightTable(string body, double boundaryM, string seaLevelPressureAtm, string seaLevelDensity,
        string scaleHeightKm, double[] altitude, double[] lnDensity, double[] lnPressure)
    {
        Body = body;
        BoundaryM = boundaryM;
        SeaLevelPressureAtm = seaLevelPressureAtm;
        SeaLevelDensity = seaLevelDensity;
        ScaleHeightKm = scaleHeightKm;
        _altitude = altitude;
        _lnDensity = lnDensity;
        _lnPressure = lnPressure;
    }

    public double Density(double altitudeM) => Math.Exp(Interpolate(_lnDensity, altitudeM));

    public double Pressure(double altitudeM) => Math.Exp(Interpolate(_lnPressure, altitudeM));

    private double Interpolate(double[] values, double altitudeM)
    {
        int i = Array.BinarySearch(_altitude, altitudeM);
        if (i >= 0)
            return values[i];
        int a = Math.Clamp(~i - 1, 0, _altitude.Length - 2);
        double f = (altitudeM - _altitude[a]) / (_altitude[a + 1] - _altitude[a]);
        return values[a] + f * (values[a + 1] - values[a]);
    }
}

internal static partial class AtmosphereTables
{
    public static FlightTable? ForBody(string? id)
    {
        foreach (FlightTable t in Flight)
            if (string.Equals(t.Body, id, StringComparison.Ordinal))
                return t;
        return null;
    }
}
