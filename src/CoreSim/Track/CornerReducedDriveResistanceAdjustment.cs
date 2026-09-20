namespace CoreSim;

/// <summary>
/// Calibration-only exposure of the existing longitudinal resistance while
/// continuous-corner drive availability is reduced. The production default is
/// absent; an exposure of zero is the exact reviewed production equation.
/// </summary>
internal readonly record struct CornerReducedDriveResistanceAdjustment
{
    internal float Exposure { get; }
    internal bool IsProductionBaseline => Exposure == 0f;

    internal CornerReducedDriveResistanceAdjustment(float exposure)
    {
        if (!float.IsFinite(exposure) || exposure < 0f || exposure > 1f)
            throw new ArgumentOutOfRangeException(nameof(exposure),
                "Reduced-drive resistance exposure must be finite and in [0,1].");

        Exposure = exposure;
    }
}
