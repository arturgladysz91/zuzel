using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>#121 three predeclared launch configurations; stop the whole bike before the bend.</summary>
public static class EngineClutchStartAudit
{
    public const string BaseHead="46129d74cf725c33d67040663262062ac9067281";
    public const double GateWidthMeters=3,GateNumber=2;
    public static readonly (string Name,double ReleaseSeconds)[] Cases={ ("gentle",.8),("intermediate",.4),("aggressive",.12) };
    public static WorldParameters Parameters(double release=.4)=>CornerEntryAudit.Parameters() with{
        EnableEngineClutchStart=true,EngineClutch=new(){ReleaseSeconds=release}};
    public static RiderSpec Rider()
    {
        var geometry=RacingLineRootCauseAudit.Frame().Track.Geometry;
        var field=new StartingGateGeometry(geometry.StraightWidthMeters).Field((StartingGate)((int)GateNumber-1));
        var offset=field.CenterPhysicalOffsetMeters-TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters;
        return CornerEntryAudit.Rider() with{
            ReferenceMeters=-(TwoContactBikeParameters.Default.FrontAxleFromComMeters+RearWheelParameters.Default.WheelRadiusMeters),
            OffsetMeters=offset,TargetOffsetMeters=offset,SpeedMetersPerSecond=0};
    }
    public static double StopReference=>RacingLineRootCauseAudit.Frame().SegmentStartMeters(1)-SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters/2-.5;
    public static TwoContactBikeAudit.Capture Execute(double release=.4,double maximumSeconds=12,double step=.01,double tapeRelease=0,int requiredLaps=0)
    {
        var spec=Rider();var p=Parameters(release);p=p with{EngineClutch=p.EngineClutch! with{TapeReleaseSeconds=tapeRelease}};
        var frame=RacingLineRootCauseAudit.Frame();var ticks=new List<TwoContactBikeAudit.Tick>();
        TwoContactForces forces=default;RearWheelStep wheel=default;RiderView before=default;double previous=0;string? stop=null;
        using var world=new WorldRunner(new[]{spec},parameters:p,trackShape:RacingLineRootCauseAudit.Variant(),
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),twoContactInitial:new(),
            steeringCommands:new[]{new TwoContactSteeringCommand(spec.Profile.Id,0,0)},
            diagnostics:new(){Force=f=>before=f.Rider,TwoContact=f=>forces=f.Forces,RearWheel=w=>wheel=w.Step});
        var initial=world.Snapshot;var mechanical=world.MechanicalProperties(spec.Profile.Id);var geometry=new RacingLineGeometry(world.Frame);
        var run=world.Run("clutch-start",step,maximumSeconds,requiredLaps,acceptedObserver:s=>{
            var after=s.Riders[0];var gap=geometry.Clearance(after);
            ticks.Add(new(previous,(float)wheel.Seconds,before,after,forces,wheel,gap.Inner,gap.Outer));previous=s.TimeSeconds;
        },diagnosticStop:s=>{
            if(ticks[^1].Inner<=0||ticks[^1].Outer<=0)stop="boundary crossing";
            if(Math.Abs(s.Riders[0].SlideRadians)>.01)stop="straight attitude assumption exceeded";
            return stop is not null||s.Riders[0].TotalReferenceMeters>=StopReference;
        });
        return new(initial,run,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,0,stop);
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"protocol.json",new{BaseHead,Start=Rider(),Parameters=Parameters(),Cases=Cases.Select(c=>new{c.Name,c.ReleaseSeconds}),
                TapeReactionSeconds=RiderController.TapeReactionSeconds(Rider()),StopReference,GateWidthMeters,GateNumber,StartFinishLineReferenceMeters=0,
                Rule="Three release durations declared before runs: gentle0.8/intermediate0.4/aggressive0.12s; all6000RPM, crank inertia0.02kgm2, capacity90Nm, original engine map/gearing/tyres/mu/Egg. Existing tape reaction, throttle1 afterwards. Hold physical steering0 on straight; no controller change. Front wheel initially at common line; COM behind. End before whole footprint reaches unverified bend. No full laps, no70/60m extrapolation, no tuning by outcome."});return;
        }
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(frozen.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen launch parameters changed.");
        var chosen=Cases.Single(c=>c.Name==stage);
        if(frozen.RootElement.GetProperty("Start").GetRawText()!=JsonSerializer.Serialize(Rider(),InverseFeasibilityAudit.Json)
            ||frozen.RootElement.GetProperty("Cases").EnumerateArray().Single(c=>c.GetProperty("Name").GetString()==stage).GetProperty("ReleaseSeconds").GetDouble()!=chosen.ReleaseSeconds)
            throw new InvalidOperationException("Frozen starting position or release configuration changed.");
        if(File.Exists(Path.Combine(dir,stage+".json")))throw new InvalidOperationException("Retain original trial; no overwrite.");
        var c=Execute(chosen.ReleaseSeconds);TwoContactBikeAudit.Export(dir,stage,c);
        using var writer=InverseFeasibilityAudit.Csv(dir,stage+"-drivetrain.csv","t,s,engine_before,engine_after,engine_rpm,wheel_rpm,engagement,throttle,engine_torque,clutch_torque,wheel_torque,capacity,end_slip,locked,combustion_work,clutch_loss,gear_loss,engine_change,wheel_input_work,contact_queries,accel");
        foreach(var t in c.Ticks){var d=t.Forces.Drivetrain!;InverseFeasibilityAudit.Row(writer,t.Time,t.Before.TotalReferenceMeters,d.BeforeOmega,d.AfterOmega,
            d.BeforeOmega*30/Math.PI,t.Wheel.BeforeOmega*30/Math.PI,d.Engagement,d.Throttle,d.EngineTorqueNewtonMeters,d.ClutchTorqueNewtonMeters,d.WheelTorqueNewtonMeters,
            d.CapacityNewtonMeters,d.EndSlipRadPerSecond,d.Locked?1:0,d.CombustionWorkJoules,d.ClutchLossJoules,d.TransmissionLossJoules,d.EngineEnergyChangeJoules,
            d.Wheel.EngineWorkJoules,d.ContactQueries,(t.After.Velocity-t.Before.Velocity).Length()/t.Seconds);}
        Console.WriteLine($"{stage}: end {c.Result.TimeSeconds}s, V={c.Ticks[^1].After.Speed*3.6}km/h, stop={c.StopReason??"straight end"}");
    }
}
