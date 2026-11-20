namespace SharedWorldSpeedway;

/// <summary>Experimental, unmeasured engine loss hypotheses; not a JAWA motoring map.</summary>
public sealed record EngineLossH26(double MechanicalNewtonMeterSeconds=.003,
    double PumpingNewtonMeterSeconds=.005, double Scale=1)
{
    public void Validate()
    {
        if(new[]{MechanicalNewtonMeterSeconds,PumpingNewtonMeterSeconds,Scale}.Any(x=>!double.IsFinite(x)||x<0)
            ||Scale>3) throw new ArgumentException("Nonnegative explicit H26 loss parameters required.");
    }
    public double Mechanical(double omega)=>Scale*MechanicalNewtonMeterSeconds*omega;
    public double Pumping(double omega,double throttle)=>Scale*PumpingNewtonMeterSeconds*Math.Pow(1-throttle,2)*omega;
    public double NetViscosity(double throttle)=>Scale*((1-throttle)*MechanicalNewtonMeterSeconds+
        Math.Pow(1-throttle,2)*PumpingNewtonMeterSeconds);
}

public sealed record H26Configuration(bool DynamicLoads, EngineLossH26? EngineLoss=null,
    double ComHeightMeters=.55, double AccelerationToleranceMetersPerSecondSquared=2e-5)
{
    public static H26Configuration For(string name)=>name switch {
        "H26-L"=>new(true),"H26-E"=>new(false,new()),"H26-LE"=>new(true,new()),
        _=>throw new ArgumentException("Explicit H26-L, H26-E or H26-LE configuration required.")};
    public void Validate()
    {
        EngineLoss?.Validate();
        if(!double.IsFinite(ComHeightMeters)||ComHeightMeters is <.3 or >.9 ||
            !double.IsFinite(AccelerationToleranceMetersPerSecondSquared)||
            AccelerationToleranceMetersPerSecondSquared is <1e-6 or >1e-3)
            throw new ArgumentException("Explicit H26 height and solver tolerance required.");
    }
}

public sealed record EngineLedgerH26(double GrossCombustionTorqueNewtonMeters,double MechanicalTorqueNewtonMeters,
    double PumpingTorqueNewtonMeters,double GrossCombustionWorkJoules,double MechanicalWorkJoules,
    double PumpingWorkJoules,double ShaftEnergyChangeJoules,double ClutchInputWorkJoules,
    double WheelEnergyChangeJoules,double BodyLongitudinalWorkJoules,double ResidualJoules);
