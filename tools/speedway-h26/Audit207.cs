using System.Diagnostics;
using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Evidence206="data/experiments/speedway206";
    private const double StudyClearance207=.50;
    private sealed record AuditPoint207(Start197 Start,FreeRacingAction Applied,FreeRacingAction? Requested,
        double Outer,double? Swept,double Course,double? Ax,double? Ay,double? BetaRate,double? YawRate,
        double? FxRear,double? FyRear,double? FxFront,double? FyFront,double? MzRear,double? MzFront,double? Mz,
        double? NFront,double? NRear,double? Kappa,double? ShaftRpm,double? WheelRpm,bool? SteeringRateLimited,
        bool? SteeringAngleLimited,double? SteeringUsed,double? RearUtilization,Losses207? Losses);
    private sealed record Losses207(double CrankDrag,double Clutch,double Transmission,double RearSlip,double Bearing);
    private sealed record Source207(string Label,string Fixture,FreeRacingAction[] Controls,AuditPoint207[] Trace,
        RacingFailure206? Corridor,RacingFailure206? Style,string? Stop,
        string? PhysicalFailure=null,double? Finish=null,int? Contacts=null);
    private sealed record Witness207(string Name,Source207 Source,double Time,double S,string Predicate,bool StrictPrefix);

    private static AuditPoint207 Point207(Start197 start,Start197 before,double outer,double? swept,
        TwoContactForces? contact,ClutchStep144? power,FreeRacingAction applied,FreeRacingAction? requested)
    {
        var body=start.State.Rider;
        var f=contact;
        double dt=start.State.Time-before.State.Time;
        double? betaRate=dt>1e-8?BikeAngles.Wrap(body.SlideRadians-before.State.Rider.SlideRadians)/dt:null;
        var course=before.State.Rider.Velocity;
        var tangent=course.LengthSquared()>.01f?Vector2.Normalize(course):before.State.Rider.Road.Tangent;
        var normal=new Vector2(-tangent.Y,tangent.X);
        return new(start,applied,requested,outer,swept,Corridor199.CourseError(body),
            f.HasValue?Vector2.Dot(f.Value.NetForce,tangent)/Mass:null,
            f.HasValue?Vector2.Dot(f.Value.NetForce,normal)/Mass:null,betaRate,f?.NetTorque/Inertia,
            power?.Wheel.AppliedLongitudinalNewtons??f?.RearLongitudinal,f?.RearLateral,
            f.HasValue?Vector2.Dot(f.Value.FrontForce,f.Value.BodyForward):null,
            f.HasValue?Vector2.Dot(f.Value.FrontForce,f.Value.BodyLeft):null,
            f?.RearTorque,f?.FrontTorque,f?.NetTorque,f?.TyreLoads?.FrontNormalNewtons,f?.TyreLoads?.RearNormalNewtons,
            power?.Wheel.InstantSlipRatio,power?.Engine.EngineRpm,start.State.Wheel*30/Math.PI,
            f.HasValue?f.Value.SteeringAfter!=f.Value.SteeringRequest:null,
            f.HasValue?Math.Abs(f.Value.SteeringRequest)>=TwoContactBikeParameters.Default.MaximumSteeringRadians-1e-6:null,
            f?.SteeringUsed,power?.Wheel.RearUtilization,
            power is null?null:new(power.CrankDragWorkJoules,power.ClutchHeatJoules,power.TransmissionLossJoules,
                power.Wheel.LongitudinalLossJoules,power.Wheel.BearingLossJoules));
    }

    private static Source207 Archived207(string path,string label)
    {
        var archive=Read199<Archive204>(path);
        var driver=archive.Run.Riders.Single();
        var strict=new RacingProfile206(Map199).Begin();
        var rows=new List<AuditPoint207>();
        RacingFailure206? direction=null,technique=null;
        string? physical=null;
        for(int i=0;i<driver.Samples.Length;i++)
        {
            var s=driver.Samples[i];
            var previous=i>0?rows[^1].Start:Sample199(s,archive.Lean[0][0]);
            var seen=strict.InspectStyle(s.Input.Rider,s.Input.Time,s.Outer,s.Certificate??100,.5,
                s.Input.Rider.TotalReferenceMeters>=3*Frame.LapMeters);
            direction??=seen.Corridor;
            technique??=seen.Technique;
            if(i>0&&s.Forces is {} contact&&s.Powertrain is {} power)
                physical??=RacingProfile206.Physical(s.Input.Rider,s.Input.Time,(float)power.Seconds,contact,power);
            var start=Sample199(s,archive.Lean[0][i]) with {TechniqueHistory=strict.Capture(),
                RiderHeightOffsetMeters=i>0&&archive.Posture is {Length:>0}?archive.Posture[i-1].Posture.RiderHeightOffsetAfterMeters:0};
            rows.Add(Point207(start,previous,s.Outer,s.Certificate,s.Forces,s.Powertrain,s.Input.Active,
                new FreeRacingAction(s.RequestedSteering,s.RequestedDrive)));
        }
        return new(label,"C",driver.Decisions.Select(d=>d.Choice.Action).ToArray(),rows.ToArray(),
            direction,technique,archive.Run.Outcome,physical,driver.FinishSeconds,archive.Run.Contacts.Length);
    }

    private static Source207 Simulated207(string root,string label,string fixture,FreeRacingAction[] commands,bool strict)
    {
        var (seed,_,spec)=Fixture206(root,fixture);
        var gate=new RacingProfile206(Map199);
        var observer=gate.Begin();
        var model=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia,strict?gate:null);
        var relaxed=new Corridor199(Map199,double.PositiveInfinity);
        var cursor=model.Begin(seed.Start,commands,0,seed.Goal,.5,profile:true,constraint:strict?null:relaxed.Check);
        RacingFailure206? direction=null,technique=null;
        var rows=new List<AuditPoint207>();
        int recorded=0;
        var start=cursor.Checkpoint();
        observer.ObserveInitial(start.State.Rider,start.State.Time);
        rows.Add(Point207(start with {TechniqueHistory=observer.Capture()},start,cursor.Snapshot.Outer,null,null,null,start.Active,null));
        while(cursor.Outcome is null&&cursor.Steps<10000)
        {
            var before=cursor.Checkpoint();
            cursor.Advance(1);
            var after=cursor.Checkpoint();
            if(after.State.Time<=before.State.Time+1e-9) break;
            var seen=observer.InspectStyle(after.State.Rider,after.State.Time,cursor.Snapshot.Outer,cursor.LastSweep,.5);
            direction??=seen.Corridor;
            technique??=seen.Technique;
            FreeRacingAction? request=cursor.NextIndex>recorded?commands[cursor.NextIndex-1]:null;
            recorded=cursor.NextIndex;
            rows.Add(Point207(after with {TechniqueHistory=observer.Capture()},before,cursor.Snapshot.Outer,
                cursor.LastSweep,cursor.LastContact?.Forces,cursor.LastPower,before.Active,request));
        }
        return new(label,fixture,commands,rows.ToArray(),direction,technique,cursor.Outcome);
    }

    private static void Audit207(string root,string output)
    {
        var timer=Stopwatch.StartNew();
        var archives=new[]
        {
            Archived207(Path.Combine(root,"data/experiments/speedway202/H25-same.json.gz"),"H25"),
            Archived207(Path.Combine(root,NativeSource205),"H27-N"),
            Archived207(NativeFile206(root,"C"),"H28-C")
        };
        var prior=Read199<ValidationSet206>(Path.Combine(root,Evidence206,"controlled-search.json.gz"));
        var recipe=prior.Offsets.Single(x=>x.Candidate.Id==434).Controls;
        var frozen=Pad200(Policy205(root).Controls!);
        var deepest=Simulated207(root,"#434 strict","C",recipe,true);
        var holds=new[]{"C-hold-plus","C-alt-plus","C-alt"}.Select(x=>
            Simulated207(root,"#205 relaxed causal diagnostic",x,frozen,false)).ToArray();
        var entry=Simulated207(root,"#434 strict","C-alt-plus",recipe,true);
        var last=deepest.Trace[^1].Start.State;
        var lateEntry=entry.Trace[^1].Start.State;
        var old=archives[^1];
        var witnesses=new List<Witness207>
        {
            new("first-yaw",old,old.Corridor!.Time,old.Corridor.S,"wide-direction",true),
            new("correction",old,old.Style!.Time,old.Style.S,"technique-correction",false),
            new("zero-throttle-yaw",deepest,last.Time,last.Rider.TotalReferenceMeters,"wide-direction",true),
            new("plus12-entry",entry,lateEntry.Time,lateEntry.Rider.TotalReferenceMeters,"wide-entry",true)
        };
        foreach(var src in holds)
        {
            var end=src.Trace[^1].Start.State;
            witnesses.Add(new(src.Fixture+"-late",src,end.Time,end.Rider.TotalReferenceMeters,src.Stop!,false));
        }

        var map=Map199;
        var windows=new Corridor199(map).Windows;
        var phases=new List<object>();
        for(int lap=0;lap<3;lap++)
        for(int corner=0;corner<map.Count;corner++)
        {
            int visit=lap*map.Count+corner;
            var bend=map.At(lap,corner);
            var window=windows[visit];
            var find=old.Trace;
            AuditPoint207 At(double s)=>find.MinBy(row=>Math.Abs(row.Start.State.Rider.TotalReferenceMeters-s))!;
            var core=At(window.Start+54);
            var entering=At(bend.Entry);
            var exit=At(bend.Exit);
            var settleStart=At(bend.Exit+12);
            var nextWindow=visit+1<windows.Length?windows[visit+1]:null;
            var begin=At(window.Start+12);
            var yawDeadline=At(bend.Entry+8);
            var slideDeadline=At(bend.Entry+70);
            var active=entering.Start.Active;
            var (seed,_,spec)=Fixture206(root,"C");
            var shot=new Shot205("phase-authority",entering.Start,spec,.01f,"native accepted state");
            var query=Query205(shot,"H28-F");
            var solved=query.Query.Solve(query.Lo,query.Hi,query.Posture);
            var loads=solved.Forces.TyreLoads!;
            var realisticFront=query.Query.FrontMomentSweep(loads.FrontCapacityNewtons);
            var bike=TwoContactBikeParameters.Default;
            double torqueLimit=Math.Max(bike.FrontAxleFromComMeters,bike.RearAxleFromComMeters)*
                loads.EffectiveFrictionCoefficient*Mass*9.81;
            double minimumYawSeconds=Technique200.RequiredSeconds+
                (Technique200.TurnYawThreshold-Corridor199.YawLimit)*Inertia/torqueLimit;
            var overlapStart=Math.Max(window.Start+12,bend.Entry-30);
            var overlapEnd=Math.Min(window.Start+54,bend.Entry+8);
            // Past archived violations can enter later snapshots here. Only the
            // local continuation is certified and its source history is retained.
            var approach=ProbePhase(old,Math.Max(window.Start+10,0),bend.Entry+10);
            var exitTrial=ProbePhase(old,bend.Exit-13,Math.Min(bend.Exit+18,seed.Goal));
            phases.Add(new {Visit=visit+1,Bend=bend,Phase="straight-to-bend",Window=window,
                CoreFrom=window.Start+12,CoreTo=window.Start+54,TurnFrom=bend.Entry-30,TurnTo=bend.Entry+8,
                SlideFrom=bend.Entry-22,SlideTo=bend.Entry+70,OverlapMeters=Math.Max(0,overlapEnd-overlapStart),
                ExclusiveTurnMeters=bend.Entry+8-(window.Start+54),
                ArchivedExclusiveTurnSeconds=yawDeadline.Start.State.Time-core.Start.State.Time,
                SlideWindowSeconds=slideDeadline.Start.State.Time-At(bend.Entry-22).Start.State.Time,
                TorqueUpperBound=torqueLimit,MinimumIdealTurnSeconds=minimumYawSeconds,ReactionSeconds=RiderController.ReactionSeconds(spec),
                SearchDelayAtApproach=core.Start.Pending.LastOrDefault()?.ActivationAt-core.Start.State.Time,
                FrontMomentAtEntry=new {realisticFront.Minimum,realisticFront.Maximum},
                RearMomentAtEntry=solved.Forces.RearTorque,RequiredYawChange=Technique200.TurnYawThreshold-core.Start.State.Rider.AngularVelocity,
                Delta=Change(core,entering),Historical=new {CoreEnd=core,Entry=entering,TurnDeadline=yawDeadline},
                Control=active,Conditional=approach});
            phases.Add(new {Visit=visit+1,Bend=bend,Phase="bend-to-straight",Next=nextWindow,
                SettleWindowBegins=bend.Exit+12,CorrectionEnds=nextWindow?.End-12,
                ExitToCoreSeconds=settleStart.Start.State.Time-exit.Start.State.Time,
                MaxMoment=torqueLimit,MinIdealSettleSeconds=Technique200.RequiredSeconds+
                    Math.Max(0,Math.Abs(exit.Start.State.Rider.AngularVelocity)-Technique200.StableYawLimit)*Inertia/torqueLimit,
                Delta=Change(exit,settleStart),Historical=new {Exit=exit,CoreStart=settleStart,PriorCoreStart=begin},Conditional=exitTrial});
        }
        var details=witnesses.Select(w=>
        {
            var branches=Contrast207(root,w);
            var start=w.Source.Trace.Where(x=>x.Start.State.Time>=w.Time-3.25&&x.Start.State.Time<=w.Time+.15).ToArray();
            return new {w.Name,w.Source.Label,w.Source.Fixture,w.Time,w.S,w.Predicate,w.StrictPrefix,Trace=start,Branches=branches};
        }).ToArray();
        var baselines=archives.Select(x=>
        {
            double? min=x.Trace.Skip(1).Min(point=>point.Swept);
            bool complete=x.Stop=="completed";
            bool legal=x.Corridor is null&&x.Style is null&&x.PhysicalFailure is null;
            return new {x.Label,x.Corridor,x.Style,x.PhysicalFailure,Complete=complete,x.Finish,x.Contacts,MinSwept=min,
                StrictStyle=x.Corridor is null&&x.Style is null,
                StrictS3=complete&&legal&&min>=.5&&x.Contacts==0&&x.Trace.Skip(1).All(p=>p.Swept.HasValue)};
        }).ToArray();
        Save(Path.Combine(output,"causal-and-phases.json.gz"),new {Baselines=baselines,Phases=phases,Witnesses=details,
            Budget=new {MaximumBranches=7*8*13,MaxBeforeSeconds=3.20,MaxAfterSeconds=.70},ElapsedSeconds=timer.Elapsed.TotalSeconds,NativeWorlds=0},true);
        Console.WriteLine($"Audit: {phases.Count} phase budgets, {details.Length} causal traces.");

        object ProbePhase(Source207 source,double start,double target)
        {
            var knot=source.Trace.First(x=>x.Start.State.Rider.TotalReferenceMeters>=start).Start;
            var (seed,_,spec)=Fixture206(root,source.Fixture);
            var model=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia,new RacingProfile206(map));
            var cursor=model.Begin(knot,source.Controls,Index207(knot.Review),seed.Goal,.5);
            for(int i=0;i<450&&cursor.Outcome is null&&cursor.Snapshot.S<target;i++) cursor.Advance(1);
            return new {From=start,Target=target,Passed=cursor.Snapshot.S>=target&&cursor.Outcome is null or "goal",
                cursor.Outcome,cursor.Sweep,End=cursor.Snapshot,Failure=cursor.StrictFailure206,
                HistoricLineageStrict=source.Corridor is null||source.Corridor.Time>=knot.State.Time};
        }
        static object Change(AuditPoint207 a,AuditPoint207 b)
        {
            var x=a.Start.State.Rider;
            var y=b.Start.State.Rider;
            return new {Yaw=(double)y.AngularVelocity-x.AngularVelocity,
                Heading=BikeAngles.Wrap(y.Heading-x.Heading),
                Direction=BikeAngles.Wrap(Math.Atan2(y.Velocity.Y,y.Velocity.X)-Math.Atan2(x.Velocity.Y,x.Velocity.X)),
                Beta=BikeAngles.Wrap(y.SlideRadians-x.SlideRadians)};
        }
    }

    private static int Index207(double time)=>(int)Math.Round(time/Predictor198.Review);

    private static object[] Contrast207(string root,Witness207 witness)
    {
        var (seed,_,spec)=Fixture206(root,witness.Source.Fixture);
        var edits=new (double Gas,double Steer,int Radius)[]
        {
            (0,0,6),(0,-.012,6),(0,.012,6),(-.18,0,6),(.18,0,6),(-.18,-.012,6),(-.18,.012,6),
            (.18,-.012,6),(-.22,-.016,13),(0,-.025,13),(.16,-.018,13),(-.18,.020,13),(.14,.025,13)
        };
        var gates=new RacingProfile206(Map199);
        var model=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia,
            witness.StrictPrefix?gates:null);
        var relaxed=new Corridor199(Map199,double.PositiveInfinity);
        var rows=new List<object>();
        foreach(double before in new[]{3.20,2.30,1.75,1.50,1.0,.55,.26,.12})
        {
            int index=Index207(witness.Time-before);
            double review=index*Predictor198.Review;
            var row=witness.Source.Trace.Last(x=>x.Start.State.Time<=review+1e-8);
            var origin=row.Start with {Review=review,Pending=row.Start.Pending.Where(x=>x.ObservedAt<review-1e-9).ToArray()};
            foreach(var (gas,steer,radius) in edits)
            {
                var actions=Edit197.Apply(witness.Source.Controls,[new(index+radius-1,radius,steer,gas)]);
                var cursor=model.Begin(origin,actions,index,seed.Goal,.5,
                    constraint:!witness.StrictPrefix&&witness.Predicate!="technique-correction"?relaxed.Check:null);
                var style=gates.Begin(origin);
                RacingFailure206? correction=null;
                bool crosses=false;
                for(int j=0;j<330&&cursor.Outcome is null&&cursor.Snapshot.Time<witness.Time+.70;j++)
                {
                    cursor.Advance(1);
                    if(!witness.StrictPrefix&&witness.Predicate=="technique-correction")
                    {
                        var here=cursor.Checkpoint();
                        correction=style.InspectStyle(here.State.Rider,here.State.Time,cursor.Snapshot.Outer,cursor.LastSweep,.5).Technique;
                        if(correction is not null) break;
                    }
                    crosses|=cursor.Snapshot.S>=witness.S+.10&&cursor.Outcome is null;
                }
                rows.Add(new {EditedReview=review,Gas=gas,Steering=steer,SupportSeconds=2*radius*Predictor198.Review,
                    Origin=origin.State,Remainder=origin.Pending,CrossesFault=crosses,Escapes=cursor.Snapshot.S>=witness.S+2&&cursor.Outcome is null&&correction is null,
                    End=cursor.Snapshot,cursor.Outcome,Failure=cursor.StrictFailure206??correction});
            }
        }
        return rows.ToArray();
    }
}
