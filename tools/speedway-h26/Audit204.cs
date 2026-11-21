using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Source204 = "data/experiments/speedway203";
    private const string Training204 = Source204 + "/H27-P-training.json.gz";
    private static readonly string[] Fixtures204 = ["C","C-hold-plus","C-hold-minus","C-alt","C-alt-plus"];

    private sealed record Archive204(Run144 Run, HandlingSettings Handling, LeanSample[][] Lean,
        LoadStepH26[][] Loads, TimedPostureH27[]? Posture);

    private sealed record Case204(Probe203 Probe, double RiderOffset)
    {
        internal HandlingSettings Numerical => Probe.Settings with {DoubleContactSolve=true};
        internal CrankClutch144 Crank() => new(omega:Probe.State.Shaft,lossH26:Probe.Settings.H26?.EngineLoss);
        internal RearWheelState Wheel() => Speed148.ForecastFork148.Wheel(Probe.State.Wheel,Physics.RearWheel);

        internal (double Lo,double Hi,double Height,PostureStepH27? Posture) Domain()
        {
            var h26=Numerical.H26!;
            var posture=Numerical.PostureH27?.Prepare(Mass, Probe.Spec.Profile.Gameplay.Physical.MassKg,
                h26.ComHeightMeters,RiderOffset,Probe.Action.Drive,Probe.Dt);
            double height=posture?.CombinedMidpointHeightMeters??h26.ComHeightMeters;
            var bike=Physics.TwoContactBike??TwoContactBikeParameters.Default;
            double g=LoadBasedTyreGrip.GravityMetersPerSecondSquared;
            return (-g*bike.FrontAxleFromComMeters/height+.001,g*bike.RearAxleFromComMeters/height-.001,height,posture);
        }

        internal HandlingContact.ContactQuery64 Query(CrankClutch144 shaft)
        {
            var bike=Physics.TwoContactBike??TwoContactBikeParameters.Default;
            var budget=LoadBasedTyreGrip.Budget(Mass,Probe.Grip,bike,Physics.LoadBasedTyres);
            var state=Probe.State;
            return new(Numerical,state.Rider with {Drive=Probe.Action.Drive},Probe.Spec,bike,budget,Mass,Inertia,
                Domain().Height,state.Time,Probe.Dt,state.Steer,Probe.Action.SteeringRadians,state.Lean,Wheel(),shaft,
                RiderController.ReactionSeconds(Probe.Spec));
        }

        internal (HandlingStep Step,ClutchStep144 Power) Prepare(bool useDouble)
        {
            var state=Probe.State;
            var shaft=Crank();
            var step=HandlingContact.Prepare(useDouble?Numerical:Probe.Settings,Probe.Grip,
                state.Rider with {Drive=Probe.Action.Drive},Probe.Spec,Physics,state.Time,Mass,Inertia,Wheel(),state.Steer,
                state.Lean,Probe.Dt,Probe.Action.SteeringRadians,shaft,RiderController.ReactionSeconds(Probe.Spec),RiderOffset);
            return (step,shaft.Prepared);
        }

        internal Probe203 FixedLegacy => Probe with {Settings=Probe.Settings with
            {H26=Probe.Settings.H26! with {ComHeightMeters=Domain().Height},PostureH27=null}};
    }

    private static void Native204(string repository,string output,string fixture)
    {
        if (!Fixtures204.Contains(fixture)) throw new ArgumentException("Unknown predeclared study fixture.",nameof(fixture));
        RequireLedger203("H27-N-learned-"+fixture);
        NativeLearned202(repository,output,"H27-N",Path.Combine(repository,Training204),fixture);
    }

    private static Case204 Accepted204(Archive204 archive,int i)
    {
        var driver=archive.Run.Riders.Single();
        var previous=driver.Samples[i-1];
        var sample=driver.Samples[i];
        return new(new(archive.Handling,driver.Spec,Sample199(previous,archive.Lean[0][i-1]).State,
            sample.Input.Active,(float)sample.Powertrain!.Seconds),
            i>1&&archive.Posture is {Length:>0}?archive.Posture[i-2].Posture.RiderHeightOffsetAfterMeters:0);
    }

    private static object Attempt204(Case204 probe,bool precise,bool bisect=false)
    {
        try
        {
            var shaft=probe.Crank();
            var domain=probe.Domain();
            var (step,power)=bisect
                ? (probe.Query(shaft).Solve(domain.Lo,domain.Hi,domain.Posture,true),shaft.Prepared)
                : probe.Prepare(precise);
            return new {Status="Converged", step.LoadH26, step.Forces, Power=power,
                Method=bisect?"Bisection":precise?"Hybrid":"Legacy Illinois"};
        }
        catch (InvalidOperationException failure)
        {
            return new {Status=Status204(failure),Failure=failure.Message,
                Method=bisect?"Bisection":precise?"Hybrid":"Legacy Illinois"};
        }
    }

    private static string Status204(InvalidOperationException failure) => failure switch
    {
        ContactRootException root => root.Status.ToString(),
        _ when failure.Message.Contains("no positive-load") => "LegacyEndpointRejection",
        _ when failure.Message.Contains("convergence") => "IterationLimit",
        _ => "InvalidPhysicalState"
    };

    private static void Audit204(string repository,string output)
    {
        var archived=new List<object>();
        var focused=new Dictionary<string,Case204>();
        Case204? rearMinimum=null;
        double minimumRear=double.PositiveInfinity;
        string v202=Path.Combine(repository,"data/experiments/speedway202");
        string v203=Path.Combine(repository,Source204);
        var paths=Archives203.Select(n=>Path.Combine(v202,n+".json.gz"))
            .Concat(Directory.GetFiles(Path.Combine(v203,"native"),"*.json.gz").Order(StringComparer.Ordinal));
        var timer=Stopwatch.StartNew();
        int unresolved=0;
        foreach (string path in paths)
        {
            var archive=Read199<Archive204>(path);
            var driver=archive.Run.Riders.Single();
            int parity=0, exact=0, reverseSlip=0, discontinuousNearRoot=0, nonmonotoneScans=0;
            int wheelCount=0;
            double maxInternal=0,maxApplied=0,maxRound=0,maxMoment=0,maxWheel=0,maxEnergy=0,maxLoss=0,maxJump=0;
            long forceTicks=0,solverTicks=0,evalCount=0;
            var errors=new List<object>();
            for (int i=1;i<driver.Samples.Length;i++)
            {
                var probe=Accepted204(archive,i);
                var expected=driver.Samples[i];
                var (reference,legacyPower)=probe.Prepare(false);
                if (reference.Forces.FrontForce!=expected.Forces!.Value.FrontForce ||
                    reference.Forces.RearForce!=expected.Forces.Value.RearForce ||
                    legacyPower.AfterOmega!=expected.CrankOmega ||
                    legacyPower.Wheel.AfterOmega!=expected.Input.Omega ||
                    reference.Forces.SteeringAfter!=expected.Input.Steering ||
                    reference.LeanAfter!=archive.Lean[0][i].ActualRadians)
                    throw new InvalidOperationException($"Frozen physics parity failed at {path}, step {i}.");
                parity++;
                if (reference.Forces.TyreLoads!.RearNormalNewtons<minimumRear)
                {
                    minimumRear=reference.Forces.TyreLoads.RearNormalNewtons;
                    rearMinimum=probe;
                }
                if (archive.Handling.H26 is not {DynamicLoads:true}) continue;
                if (path.StartsWith(v202,StringComparison.Ordinal) &&
                    Path.GetFileName(path) is "H26-LE-learned-C.json.gz" or "H26-LE-learned-C-alt.json.gz")
                {
                    string key=Path.GetFileName(path).Contains("-alt")?"min-front-alt":"min-front";
                    if (!focused.TryGetValue(key,out var old) ||
                        reference.Forces.TyreLoads.FrontNormalNewtons<old.Prepare(false).Step.Forces.TyreLoads!.FrontNormalNewtons)
                        focused[key]=probe;
                }
                try
                {
                    var (step,power)=probe.Prepare(true);
                    var load=step.LoadH26!;
                    var q=load.Precision!;
                    if (q.Status!="Converged"||load.FrontNormalNewtons<=0||load.RearNormalNewtons<=0||
                        Math.Abs(load.FrontNormalNewtons+load.RearNormalNewtons-Mass*9.81)>1e-9||
                        Math.Abs(q.AppliedResidual)>2e-5||Math.Abs(load.Residual)>2e-5||
                        power.Wheel.AppliedLongitudinalNewtons!=q.AppliedRearLongitudinalNewtons||
                        power.Wheel.RearUtilization>1.000001||power.Wheel.LongitudinalLossJoules<-.001||
                        Math.Abs(q.WheelImpulseResidual)>1e-10)
                        throw new InvalidOperationException("Archived state failed force, impulse, or load invariant.");
                    exact++;
                    maxInternal=Math.Max(maxInternal,Math.Abs(load.Residual));
                    maxApplied=Math.Max(maxApplied,Math.Abs(q.AppliedResidual));
                    maxRound=Math.Max(maxRound,q.ForceRoundingNewtons);
                    maxMoment=Math.Max(maxMoment,Math.Abs(q.MomentRoundingNewtonMeters));
                    maxWheel=Math.Max(maxWheel,Math.Abs(q.WheelImpulseResidual));
                    maxEnergy=Math.Max(maxEnergy,Math.Abs(power.EnergyResidualJoules));
                    maxLoss=Math.Max(maxLoss,Math.Abs(power.Wheel.RootResidualMetersPerSecond));
                    if (power.Wheel.InstantSlipRatio<0) reverseSlip++;
                    forceTicks+=q.ForceTicks;
                    solverTicks+=q.SolverTicks;
                    evalCount+=load.Iterations;
                    wheelCount+=power.WheelQueries;

                    var d=probe.Domain();
                    var query=probe.Query(probe.Crank());
                    double prior=double.NegativeInfinity;
                    bool nonmonotone=false;
                    const int intervals=16;
                    for (int j=0;j<=intervals;j++)
                    {
                        double r=query.Evaluate(d.Lo+(d.Hi-d.Lo)*j/intervals).Residual;
                        nonmonotone|=r<prior;
                        prior=r;
                    }
                    if (nonmonotone) nonmonotoneScans++;
                    if (archive.Handling.PostureH27 is not null)
                    {
                        var fixedLegacy=probe.FixedLegacy;
                        double mid=reference.LoadH26!.Acceleration;
                        double lower=Math.Max(d.Lo,mid-.001),upper=Math.Min(d.Hi,mid+.001);
                        for (int j=0;j<50&&Math.BitIncrement(lower)<upper;j++)
                        {
                            double x=lower+(upper-lower)/2;
                            if (LegacySample204(fixedLegacy,x).Residual<0) lower=x;
                            else upper=x;
                        }
                        var a=LegacySample204(fixedLegacy,lower);
                        var b=LegacySample204(fixedLegacy,upper);
                        if (a.Residual<0&&b.Residual>0)
                        {
                            maxJump=Math.Max(maxJump,b.Residual-a.Residual);
                            if (a.Residual < -2e-5&&b.Residual>2e-5) discontinuousNearRoot++;
                        }
                    }
                }
                catch (InvalidOperationException failure)
                {
                    errors.Add(new {Index=i,probe.Probe.State,probe.Probe.Action,probe.Probe.Dt,probe.RiderOffset,
                        Status=Status204(failure),Error=failure.Message});
                }
            }
            unresolved+=errors.Count;
            archived.Add(new {Name=Path.GetRelativePath(repository,path),Steps=driver.Samples.Length-1,FrozenExact=parity,
                H27NOrDynamic64Converged=exact,ReverseSlip=reverseSlip,PositiveSteps=exact-reverseSlip,DiscontinuousNearRoot=discontinuousNearRoot,
                NonmonotoneScans=nonmonotoneScans,MaxLegacyRootJump=maxJump,MaxInternalResidual=maxInternal,
                MaxAppliedResidual=maxApplied,MaxForceRounding=maxRound,MaxMomentRounding=maxMoment,MaxWheelImpulse=maxWheel,
                MaxCrankEnergy=maxEnergy,MaxWheelSlipResidual=maxLoss,ForceTicks=forceTicks,SolverTicks=solverTicks,
                Evaluations=evalCount,AcceptedCrankQueries=wheelCount,Failures=errors});

            if (path==Path.Combine(v202,"H26-LE-same.json.gz"))
            {
                var last=driver.Samples[^1];
                var state=Sample199(last,archive.Lean[0][^1]);
                focused["start-0.370"]=new(new(archive.Handling,driver.Spec,state.State,state.Active,.01f),0);
            }
        }
        var nominal=Read199<Archive204>(Path.Combine(v203,"native/H27-P-learned-C.json.gz"));
        var spec=nominal.Run.Riders.Single().Spec;
        using var matchDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(v202,"matched-irrevocable-prefix.json")));
        var match=matchDoc.RootElement.GetProperty("Start").Deserialize<Start197>(Json)!;
        focused["196"]=new(new(Settings202("H26-LE"),spec,match.State,match.Active,(float)(25.49-match.State.Time)),0);
        using var previousStudy=JsonDocument.Parse(File.ReadAllText(Path.Combine(v203,"holdout-solver-model.json")));
        var fail=previousStudy.RootElement.GetProperty("Failure").Deserialize<ContactFailure203>(Json)!;
        var p0=new Case204(new(Settings202("H27-P"),spec,fail.State,fail.Action,fail.Dt),fail.RiderHeightOffsetMeters);
        focused["plus-2-cm"]=p0;
        var trace=Trace204(p0,previousStudy.RootElement);
        var p0Result=p0.Prepare(true);
        var oldOutcome=(JsonSerializer.SerializeToElement(Attempt204(p0,false),Json)).GetProperty("Status").GetString();
        if (oldOutcome!="IterationLimit"||p0Result.Step.LoadH26?.Precision?.Status!="Converged")
            throw new InvalidOperationException("P0 regression did not reproduce expected outcomes.");

        foreach (string key in new[] {"start-0.370","196"})
        {
            var status=JsonSerializer.SerializeToElement(Attempt204(focused[key],true),Json).GetProperty("Status").GetString();
            if (status!=nameof(ContactRootStatus.NoPhysicallyAdmissibleRoot))
                throw new InvalidOperationException($"Physical-domain control changed in {key}: {status}.");
        }
        var holdout=previousStudy.RootElement.GetProperty("Checkpoint").Deserialize<Start197>(Json)!;
        var training=Read199<Training199>(Path.Combine(repository,Training204)).Winner.Result;
        var frozen=Pad200(training.Controls!);
        var runtimeCorridor=new Corridor199(Map199,double.PositiveInfinity);
        var prefixChecks=new List<object>();
        var timings=new List<object>();
        var fixtureChecks=new List<object>();
        foreach (string variant in new[] {"H27-P","H27-N"})
        {
            var model=new Predictor198(Frame,Surface,Physics,Settings202(variant),Timing,spec,Mass,Inertia);
            var cursor=model.Begin(holdout,frozen,(int)Math.Round(holdout.Review/.05),Frame.LapMeters*3,.5,
                constraint:runtimeCorridor.Check);
            cursor.Advance(10,holdout.Review+.05);
            cursor.Advance(30,holdout.Review+.25);
            prefixChecks.Add(new {Variant=variant,cursor.Outcome,cursor.ContactFailure,End=cursor.Snapshot});
            var (seed,_,_)=Fixture200(repository,"C");
            var start=seed.Start with {Pending=[],Review=0};
            for (int trial=0;trial<3;trial++)
            {
                var clock=Stopwatch.StartNew();
                var full=model.Begin(start,frozen,0,seed.Goal,.502,profile:true,constraint:runtimeCorridor.Check);
                full.Advance(10000);
                timings.Add(new {Variant=variant,Trial=trial,full.Outcome,full.GoalTime,full.Counters,
                    WallTicks=clock.ElapsedTicks});
            }
            foreach (string fixture in Fixtures204)
            {
                var (configured,_,specification)=Fixture200(repository,fixture);
                var initial=configured.Start with {Pending=[],Review=0};
                var configuredModel=new Predictor198(Frame,Surface,Physics,Settings202(variant),Timing,specification,Mass,Inertia);
                var check=configuredModel.Begin(initial,frozen,0,configured.Goal,.502,constraint:runtimeCorridor.Check);
                check.Advance(10000);
                fixtureChecks.Add(new {Variant=variant,Fixture=fixture,check.Outcome,check.GoalTime,check.Sweep,
                    End=check.Snapshot,check.ContactFailure});
            }
        }
        // This intervention found an iteration-limit case in the first zero-world throttle sweep.
        const double replayReview=4.60;
        var nominalRun=nominal.Run.Riders.Single();
        int vote=Array.FindIndex(nominalRun.Decisions,d=>Math.Abs(d.Choice.Time-replayReview)<1e-8);
        int token=Array.FindIndex(nominalRun.Samples,s=>Math.Abs(s.Input.Time-replayReview)<1e-8);
        double actualReview=nominalRun.Decisions[vote].Choice.Time;
        var fork=Sample199(nominalRun.Samples[token],nominal.Lean[0][token]) with {Review=actualReview,
            RiderHeightOffsetMeters=nominal.Posture![token-1].Posture.RiderHeightOffsetAfterMeters};
        var modified=nominalRun.Decisions.Skip(vote).Select(d=>d.Choice.Action).ToArray();
        for (int i=0;i<24;i++) modified[i]=modified[i] with {Drive=1};
        var throttleModel=new Predictor198(Frame,Surface,Physics,Settings202("H27-N"),Timing,spec,Mass,Inertia);
        var assisted=throttleModel.Begin(fork,modified,0,double.PositiveInfinity,.5,profile:true);
        assisted.Advance(200,actualReview+1.50);
        if (assisted.Outcome is not null)
            throw new InvalidOperationException($"Prior iteration-limit throttle intervention did not recover: {assisted.Outcome}");
        var iterationRegression=new {Review=actualReview,PriorFailureStep=5.38,HoldReviews=24,
            assisted.Outcome,End=assisted.Snapshot,assisted.Counters};

        var nearestRear=rearMinimum!;
        if (nearestRear.Probe.Settings.H26 is not {DynamicLoads:true})
            nearestRear=nearestRear with {Probe=nearestRear.Probe with {Settings=Settings202("H26-LE")}};
        focused["min-rear-archive"]=nearestRear;
        var one=nearestRear.Domain();
        var almostAirborne=nearestRear.Query(nearestRear.Crank()).Evaluate(one.Lo);
        var cases=focused.Select(p=>new {Name=p.Key,State=p.Value.Probe.State,p.Value.Probe.Action,p.Value.Probe.Dt,
            p.Value.RiderOffset,p.Value.Domain().Height,Legacy=Attempt204(p.Value,false),Hybrid=Attempt204(p.Value,true),
            Bisection=Attempt204(p.Value,true,true)}).ToArray();
        var extensions=new List<object>();
        foreach (float dt in new[] {.0025f,.005f,.01f,.02f,focused["196"].Probe.Dt})
        {
            var p=p0 with {Probe=p0.Probe with {Dt=dt}};
            extensions.Add(new {Group="dt",dt,Legacy=Attempt204(p,false),Hybrid=Attempt204(p,true)});
        }
        foreach (double slip in new[] {-.7,-.3,0,.3,.7})
        {
            double velocity=p0.Probe.State.Rider.ForwardSpeed;
            var p=p0 with {Probe=p0.Probe with {State=p0.Probe.State with {Wheel=(1+slip)*velocity/.33}}};
            extensions.Add(new {Group="signed slip",slip,Hybrid=Attempt204(p,true)});
        }
        foreach (double rpm in Engine500.Curve.Select(x=>x.Rpm).Concat(new[] {1500d,4000d}).Where(r=>r>=1500).Distinct())
        foreach (double delta in new[] {-150d,-.01,.01,150d})
        {
            var p=p0 with {Probe=p0.Probe with {Dt=.0025f,State=p0.Probe.State with {Shaft=(rpm+delta)*Math.PI/30}}};
            extensions.Add(new {Group="engine and engagement",rpm,delta,Hybrid=Attempt204(p,true)});
        }
        foreach (float angle in new[] {-.61f,-.40f,0,.40f,.61f})
        {
            var p=p0 with {Probe=p0.Probe with {Action=p0.Probe.Action with {SteeringRadians=angle}}};
            extensions.Add(new {Group="front inverse and actuator",angle,Hybrid=Attempt204(p,true)});
        }
        var crank=p0.Crank();
        var query204=p0.Query(crank);
        var d204=p0.Domain();
        var accepted=query204.Solve(d204.Lo,d204.Hi,d204.Posture);
        var saved=crank.Prepared;
        for(int i=0;i<=32;i++) query204.Evaluate(d204.Lo+(d204.Hi-d204.Lo)*i/32);
        if (!ReferenceEquals(saved,crank.Prepared)||crank.Omega!=p0.Probe.State.Shaft)
            throw new InvalidOperationException("A speculative trial mutated the accepted drivetrain state.");
        var again=query204.Solve(d204.Lo,d204.Hi,d204.Posture);
        if (accepted.Forces!=again.Forces||saved!=crank.Prepared||accepted.LoadH26!.Residual!=again.LoadH26!.Residual)
            throw new InvalidOperationException("Repeated contact solve differs.");
        Save(Path.Combine(output,"p0-residual.json"),trace);
        Save(Path.Combine(output,"targeted-scenarios.json"),new {Cases=cases,Matrix=extensions,RearEndpoint=almostAirborne,
            PrefixChecks=prefixChecks,IterationRegression=iterationRegression,RepeatableAndSpeculationPure=true});
        Save(Path.Combine(output,"predictor-timings.json"),new {Runs=timings,FrozenFixturePreflights=fixtureChecks,Stopwatch.Frequency});
        Save(Path.Combine(output,"archive-model.json"),new {Archives=archived,NativeWorlds=0,
            Stopwatch.Frequency,ElapsedSeconds=timer.Elapsed.TotalSeconds});
        if (unresolved!=0) throw new InvalidOperationException($"{unresolved} unresolved archived states (see archive-model.json).");
    }

    private static (double Residual,HandlingStep Step) LegacySample204(Probe203 probe,double ax)
    {
        var (step,_)=probe.AtAcceleration(ax);
        return (ax-Vector2.Dot(step.Forces.NetForce,step.Forces.BodyForward)/Mass,step);
    }

    private static object Trace204(Case204 probe,JsonElement previous)
    {
        var legacy=probe.FixedLegacy;
        var domain=probe.Domain();
        var query=probe.Query(probe.Crank());
        var bracket=previous.GetProperty("Residual").GetProperty("BisectionBracket");
        double left=bracket.GetProperty("Lower").GetProperty("Ax").GetDouble();
        double right=bracket.GetProperty("Upper").GetProperty("Ax").GetDouble();
        var curve=Enumerable.Range(0,801).Select(i=>
        {
            double ax=left+(i-400)*1e-6;
            return new {Ax=ax,Legacy=LegacySample204(legacy,ax).Residual,Double=query.Evaluate(ax).Residual};
        }).ToArray();
        var points=new[] {left,right}.Select(ax=>
        {
            var (residual,step)=LegacySample204(legacy,ax);
            var f=step.Forces;
            double blend=Math.Abs((step.LeanAfter+probe.Probe.State.Lean)/2)/probe.Probe.Settings.MaximumLeanRadians;
            double forward=Vector2.Dot(f.RearVelocity,f.BodyForward);
            double side=Vector2.Dot(f.RearVelocity,f.BodyLeft);
            double b=side/HandlingContact.Scale(forward,.104*(1-.25*blend),f.Budget/Mass);
            double raw=-f.TyreLoads!.RearCapacityNewtons*b*(1/Math.Sqrt(1+b*b));
            var precise=query.Evaluate(ax);
            return new {Ax=ax,LegacyResidual=residual,DoubleResidual=precise.Residual,f.Budget,
                RearBeforeCast=raw,RearAfterCast=f.RearLateral,RearMoment=f.RearTorque,
                FrontTargetSteer=f.SteeringRequest,FrontFx=Vector2.Dot(f.FrontForce,f.BodyForward),
                PreciseRearFy=precise.Value.RearFy,precise.Value.TargetSteering, f.TyreLoads};
        }).ToArray();
        return new {Review=20.3, Time=probe.Probe.State.Time,probe.Probe.Dt,domain.Height,Points=points,Curve=curve};
    }
}
