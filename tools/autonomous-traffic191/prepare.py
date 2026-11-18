"""Generate the four-rider adapter from the frozen local #190 harness."""
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE.parent / "global-line-planner190"


def replace(source, before, after):
    if source.count(before) != 1:
        raise ValueError(before)
    return source.replace(before, after)


world = (BASE / "HandlingWorld190.Generated.cs").read_text(encoding="utf-8")
for before, after in [
    ("internal sealed class HandlingWorld : IDisposable", "internal sealed class TrafficWorld191 : IDisposable"),
    ("    readonly HandlingRig? handling;", """    readonly HandlingRig[] handling;
    readonly double[] recoveryUntil;
    readonly List<ContactPointObservation> contactPoints = new();
    readonly List<ConsequenceObservation> consequences = new();
    readonly WorldDiagnostics diagnostics;
    public IReadOnlyList<ContactPointObservation> ContactPoints => contactPoints;
    public IReadOnlyList<ConsequenceObservation> Consequences => consequences;
    public double? MinimumBikeGap => double.IsFinite(contactWork.MinimumGapMeters) ? contactWork.MinimumGapMeters : null;
    public long ContactManifolds => contactWork.ContactManifolds;
    public long SolvedPoints => contactWork.SolvedPoints;"""),
    ("public HandlingWorld(StandingOptions144? options=null,IEnumerable<StartingGate>? grid=null,LaunchParameters144? launch=null,TrackFrame? diagnosticFrame=null,RiderSpec? diagnosticRider=null,bool separated=false,bool observeBeyondBeta=false,bool safetyHalfMeter=false,double safetyFrom=0,double? stopReference=null,double postSeconds=0, HandlingRig? handling=null, Schedule188? timing188=null){", """public TrafficWorld191(StandingOptions144 options, TrackFrame diagnosticFrame, RiderSpec[] riders,
        HandlingRig[] handling, Schedule188 timing188, StartingGate[]? startingGates=null){
        const bool safetyHalfMeter=true, observeBeyondBeta=false;
        const double safetyFrom=0, postSeconds=0;
        double? stopReference=null;
        LaunchParameters144? launch=null;
        StartingGate[] grid=startingGates??[StartingGate.A,StartingGate.B,StartingGate.C,StartingGate.D];"""),
    ("    readonly double safetyFrom,stopReference,postSeconds;readonly bool safetyHalfMeter;readonly bool separated,observeBeyondBeta;",
     "    readonly double safetyFrom,stopReference,postSeconds;readonly bool safetyHalfMeter,observeBeyondBeta;"),
    (";this.separated=separated;", ";"),
    ("        this.handling=handling;", """        this.handling=handling;
        diagnostics=new() { ContactPoint=contactPoints.Add };
        recoveryUntil=new double[riders.Length];"""),
    ("        if(handling is not null && (gates.Length!=1 || separated || observeBeyondBeta || !safetyHalfMeter))\n            throw new ArgumentException(\"SpeedwayHandling is a single-rider experiment with the full safety envelope.\");",
     """        if(riders.Length is not (1 or 2 or 4) || handling.Length!=riders.Length ||
            !grid.SequenceEqual(gates) || gates.Length!=riders.Length ||
            !riders.Select(r=>r.Profile.Id).SequenceEqual(Enumerable.Range(1,riders.Length)))
            throw new ArgumentException("One, two or four independent H25 states in physical gate order are required.");"""),
    ("(grid??new[]{this.options.SoloGate}).OrderBy", "grid.OrderBy"),
    ("specs=gates.Select((g,i)=>diagnosticRider is null?Rider(g,i+1):diagnosticRider with{Profile=diagnosticRider.Profile with{Id=i+1}}).ToArray();",
     "specs=riders;"),
    ("""        =>handling is not null
            ?handling.Prepare(v,spec,frame,surface,p,time,mass,inertia!.Value,wheel,state,seconds,
                steeringOverride!.Value,powertrain!,releaseAt)
            :separated?Contact149.Forces(v,spec,frame,surface,p,time,mass,wheel,state,seconds,steeringOverride:steeringOverride,inertia:inertia,powertrain:powertrain,releaseAt:releaseAt):Contact144.Forces(v,spec,frame,surface,p,time,mass,wheel,state,seconds,steeringOverride:steeringOverride,inertia:inertia,powertrain:powertrain,releaseAt:releaseAt);""",
     """        =>handling[v.Id-1].Prepare(v,spec,frame,surface,p,time,mass,inertia!.Value,wheel,state,seconds,
            steeringOverride!.Value,powertrain!,releaseAt);"""),
    ("Func<FreeRacingInput,CrankClutch144,RiderSpec,TrackFrame,TrackStateSnapshot,WorldParameters,float,float,string,Decision144>? chooser=null,Action<double,double>? observeCpu=null",
     "Func<FreeRacingInput,CrankClutch144,RiderSpec,WorldSnapshot,Decision144> chooser"),
    ("                views[i]=views[i] with{Drive=active[i].Drive};", """                views[i]=views[i] with{Drive=active[i].Drive};
                if(views[i].Activity==RiderActivity.Disturbed && time>=recoveryUntil[i]-1e-10)
                    views[i]=views[i] with{Activity=finish[i] is null?RiderActivity.Racing:RiderActivity.Finished,Impairment=0};"""),
    ("            // Every chooser sees the same detached accepted rigid-body snapshot. No chooser writes a body.",
     """            // Observations expose accepted kinematics, never another rider's future commands or targets.
            var snapshot = new WorldSnapshot(time,Array.AsReadOnly(views.Select(v=>
                v with{Drive=0,TargetOffsetMeters=(float)v.Road.OffsetMeters}).ToArray()));"""),
    ("for(int i=0;i<specs.Length;i++)if((finish[i] is null||postSeconds>0)&&time>=nextReview[i]-1e-10)",
     "for(int i=0;i<specs.Length;i++)if(time>=nextReview[i]-1e-10)"),
    ("var choice=chooser is null?Policy144.Choose(Input(i),shafts[i],specs[i],frame,surface,parameters,physics.Masses[i],physics.Inertias[i],variant):chooser(Input(i),shafts[i].Copy(),specs[i],frame,surface,parameters,physics.Masses[i],physics.Inertias[i],variant);",
     "var choice=chooser(Input(i),shafts[i].Copy(),specs[i],snapshot);"),
    ("if((finish[i] is null||postSeconds>0)&&nextReview[i]>time+1e-10)", "if(nextReview[i]>time+1e-10)"),
    ("            // Original curvature-deadline estimate", """            // Same close-pair travel rule as WorldRunner. Only repartitions the native step.
            dt=ContactStep191.Limit(views,dt,parameters.NearbyMaximumTravelMeters);
            // Original curvature-deadline estimate"""),
    ("""                if(handling is null){wheels[i].Commit();steering[i].Commit();}
                else{
                    wheels[i]=Speed148.ForecastFork148.Wheel(powers[i].Wheel.AfterOmega,wheels[i].Parameters);
                    steering[i]=new TwoContactBikeState(forces[i].SteeringAfter);
                    handling.Commit(time+dt);
                }""", """                wheels[i]=Speed148.ForecastFork148.Wheel(powers[i].Wheel.AfterOmega,wheels[i].Parameters);
                steering[i]=new TwoContactBikeState(forces[i].SteeringAfter);
                handling[i].Commit(time+dt);"""),
    ("""            physics.ReadContacts(views,contactWork,(a,b,impulse,closing)=>contacts.Add(new(views[a].Id,views[b].Id,time,"begin",impulse,0,0,closing)),
                (a,b)=>contacts.Add(new(views[a].Id,views[b].Id,time,"end",0,0,0)));""",
     """            physics.ReadContacts(views,contactWork,BeginContact,
                (a,b)=>contacts.Add(new(views[a].Id,views[b].Id,time,"end",0,0,0)),diagnostics,before,time,(float)dt);"""),
    ("                observeCpu?.Invoke(time,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds);\n", ""),
    ('                if(reason is not null&&failure is null)', '                if(views[i].Activity==RiderActivity.Crashed)reason="solver contact: crash threshold";\n                if(reason is not null&&failure is null)'),
    ("    static string Fingerprint(RiderRun144[] riders){", """    void BeginContact(int a,int b,double impulse,double closing)
    {
        double Observe(int i,int j)
        {
            var before=views[i];
            var impact=ContactStep191.Consequence(before,views[j],specs[i],physics.Masses[i],physics.Masses[j],
                surface,time,impulse,closing);
            consequences.Add(impact.Observation);
            views[i]=impact.Rider;
            if(impact.Rider.Activity==RiderActivity.Disturbed)
                recoveryUntil[i]=time+parameters.RecoverySeconds;
            return impact.Observation.SeverityRatio;
        }
        double ratioA=Observe(a,b),ratioB=Observe(b,a);
        contacts.Add(new(views[a].Id,views[b].Id,time,"begin",impulse,ratioA,ratioB,closing));
    }
    static string Fingerprint(RiderRun144[] riders){"""),
]:
    world = replace(world, before, after)

controller = (BASE / "HandlingController190.Generated.cs").read_text(encoding="utf-8")
controller = controller.replace("LapPlan190?", "IMotionReference191?")
controller = replace(controller,
    "        var body = state.Rider;\n        double s = body.TotalReferenceMeters;\n        double speed = body.Speed;\n        var where = map.Locate(s);",
    """        return ChooseAt(input,state,plan);
    }

    internal HandlingDecision ChooseAt(FreeRacingInput input,HandlingForecast state,IMotionReference191? reference)
    {
        var body = state.Rider;
        double s = body.TotalReferenceMeters;
        double speed = body.Speed;
        var where = map.Locate(s);""")
controller = replace(controller, "if (plan is null &&", "if (reference is null &&")
controller = controller.replace("plan?.At(", "reference?.At(").replace("plan?.TargetSpeed(", "reference?.TargetSpeed(")
controller = replace(controller, "plan?.Id ??", "reference?.Id ??")
controller = replace(controller,
    "        var action = new FreeRacingAction((float)steer, (float)Math.Clamp(gas, 0, 1));",
    "        var action = new FreeRacingAction((float)steer, (float)Math.Clamp(gas, 0, reference?.DriveLimit ?? 1));")
controller = controller.replace("current.Offset, lean,", "current.Offset, state.Lean,")
for name, source in (("TrafficWorld191", world), ("Fast50", controller)):
    (HERE / (name + ".Generated.cs")).write_text(source, encoding="utf-8")

