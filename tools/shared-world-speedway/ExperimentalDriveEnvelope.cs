using CoreSim;
using CoreSim.Setup;

namespace SharedWorldSpeedway;

/// <summary>#106 opt-in engine torque scale and physical final ratio. No speed limiter.</summary>
public sealed record ExperimentalDriveEnvelope(string Name,float TorqueScale,float FinalRatio)
{
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Name)||!float.IsFinite(TorqueScale)||TorqueScale is <=0 or >8
            ||!float.IsFinite(FinalRatio)||FinalRatio is <.25f or >2)throw new ArgumentException("Invalid experimental engine/ratio.");
    }
    public float AvailableForce(float reference,float wheelSpeed,BikeSetup setup)
        =>TorqueScale*FinalRatio*LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(reference,wheelSpeed*FinalRatio,setup);
    public float ReferenceForce(float reference)=>reference*TorqueScale*FinalRatio;
}
