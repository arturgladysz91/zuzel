using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ProbePoint208(double Seconds,double S,double Outward,double Course,double Yaw,
        double Inward,double Outer,double Swept,double FrontNormal,double RearFraction,double Drive,double Steer);
    private sealed record Probe208(double DriveDelta,double SteerDelta,ProbePoint208[] Points,
        string Stop,double LegalS,double LegalSweep,RacingFailure206? Failure,ContactFailure203? Contact);
    private sealed record MotionPoint208(AuditPoint207 Observed,ExitMotion208 Velocity,double? Inward,
        double Curvature,double? SweptRoom,double MechanicalKinetic);

    private static void Audit208(string root,string output)
    {
        var source=ReadBase208(root);
        var bank=source.Policy;
        var bend=bank.Bend(3);
        var road=bank.ExitRoad(bend);
        var before=Geometry207(root);
        var phases=before.Phases();
        var law=Decide207(before,phases,source.Plan);
        var marks=new (double S,string Name)[] {
            (bend.Entry-82,"before previous exit"),(bend.Entry-62,"previous exit"),(bend.Entry-50,"previous core start"),
            (bend.Entry-32,"preceding straight"),(bend.Entry-8,"previous core end"),(bend.Entry,"fourth entry"),
            (bend.Entry+bend.Length*.22,"slide"),(bend.Entry+bend.Length*.5,"apex"),
            (bend.Exit-31,"late slide"),(bend.Exit-17,"exit preparation"),(bend.Exit,"fourth exit"),
            (bend.Exit+4.5,"post-exit")};
        var (seed,_,spec)=Fixture206(root,"C");
        var model=Model208(spec);
        var nominal=source.Frozen.Trace!;
        var auditRows=Official208.Select(fixture=>
        {
            var r=fixture=="C"?source.Frozen:VerifyRun207(root,fixture,before,phases,source.Plan,capture:true);
            return new {r.Fixture,r.Complete,r.Stop,r.LastLegalS,r.LegalSweep,r.End,r.Failure,
                Trace=Motion208(r.Trace!.Where(x=>x.Start.State.Rider.TotalReferenceMeters>=bend.Entry-85).ToArray(),bank,bend)};
        }).ToArray();
        var watch=Stopwatch.StartNew();
        using var process=Process.GetCurrentProcess();
        var cpu=process.TotalProcessorTime;
        var samples=marks.Select(mark=>
        {
            var knot=nominal.First(p=>p.Start.State.Rider.TotalReferenceMeters>=mark.S);
            var start=knot.Start;
            var motion=ExitMotion208.From(start.State.Rider,road,Mass);
            var command=law(start);
            var queueDelay=Math.Max(0,Timing.Due(start.Review,RiderController.ReactionSeconds(spec),start.Pending.LastOrDefault())-start.State.Time);
            var interventions=(from drive in new[]{-.45,-.18,0,.22,.45} from steer in new[]{-.065,-.026,-.01,0,.01,.026,.065}
                select Probe(start,drive,steer)).ToArray();
            var accepted=interventions.SelectMany(r=>r.Points).ToArray();
            double? inward=accepted.Length==0?null:accepted.Max(p=>p.Inward);
            var zero=accepted.Where(p=>p.Outward<=0).Select(p=>p.Seconds).DefaultIfEmpty(double.PositiveInfinity).Min();
            var p0=new Shot205("exit-viability",start,spec,.01f,"H29-R accepted observation");
            var gate=Query205(p0,"H28-F");
            var force=gate.Query.Solve(gate.Lo,gate.Hi,gate.Posture).Forces;
            var limits=force.TyreLoads;
            var front=limits is null?null:(object)gate.Query.FrontMomentSweep(limits.FrontCapacityNewtons);
            double? optimistic=limits is null?null:limits.EffectiveFrictionCoefficient*9.81+
                Math.Abs(Vector2.Dot(force.Resistance,road.Outward))/Mass;
            return new {mark.Name,Anchor=mark.S,Start=start,FirstProposed=command,Motion=motion,
                AvailableSweep=knot.Swept,Room=motion.Room(knot.Outer,knot.Swept??knot.Outer,.5),
                QueueDelay=queueDelay,SampledPeakInward=inward,RequiredStoppingDistance=inward.HasValue?motion.StopDistance(inward.Value):null,
                IllustrativeGripAcceleration=optimistic,IllustrativeGripStop=optimistic.HasValue?motion.StopDistance(optimistic.Value):null,
                MinTimeToStopOutward=double.IsFinite(zero)?zero:(double?)null,
                SampledOutwardReduction=accepted.Length==0?null:(double?)(motion.Outward-accepted.Min(p=>p.Outward)),
                SampledYawChange=accepted.Length==0?null:new[]{accepted.Min(p=>p.Yaw)-motion.Yaw,accepted.Max(p=>p.Yaw)-motion.Yaw},
                front,RearMoment=force.RearTorque,AxleLimits=limits,
                Counts=new {Probed=interventions.Length,SafeHorizon=interventions.Count(p=>p.Stop=="horizon")},
                Interventions=interventions};
        }).ToArray();
        Save(Path.Combine(output,"fourth-exit-audit.json.gz"),new {Baseline=source.Plan.Branch,
            Bend=bend,NextStraight=road,NativeWorlds=0,Fixtures=Official208,Audit=auditRows,Authority=samples,
            Bounds=new {Probes=12*35,HorizonSeconds=.95,StateSamples=12,MaxSteps=120,Arm="diagnostic fixed deltas"},
            CpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,WallSeconds=watch.Elapsed.TotalSeconds},true);

        Probe208 Probe(Start197 state,double drive,double steer)
        {
            var cursor=model.BeginFeedback207(state,at=>
            {
                var action=law(at);
                return ExitPolicy208.Bound(action.SteeringRadians+steer,action.Drive+drive);
            },seed.Goal,.5,profile:true);
            var rows=new List<ProbePoint208>();
            double until=state.State.Time+.95;
            for(int i=0;i<120&&cursor.Snapshot.Time<until-1e-8&&cursor.Outcome is null;i++)
            {
                cursor.Advance(1,until);
                if(cursor.Outcome is not (null or "goal")||cursor.LastContact is not {} contact)break;
                var now=cursor.Checkpoint();
                var motion=ExitMotion208.From(now.State.Rider,road,Mass);
                var f=contact.Forces;
                rows.Add(new(now.State.Time-state.State.Time,now.State.Rider.TotalReferenceMeters,motion.Outward,
                    motion.Course,motion.Yaw,ExitMotion208.Inward(f.NetForce,road,Mass),cursor.Snapshot.Outer,
                    cursor.LastSweep,f.TyreLoads?.FrontNormalNewtons??0,cursor.LastPower?.Wheel.RearUtilization??0,
                    now.Active.Drive,now.State.Steer));
            }
            return new(drive,steer,rows.ToArray(),cursor.Outcome??(cursor.Snapshot.Time>=until-1e-8?"horizon":"step cap"),
                cursor.LastLegalS207,cursor.LegalSweep207,cursor.StrictFailure206,cursor.ContactFailure);
        }
    }

    private static MotionPoint208[] Motion208(AuditPoint207[] trace,ExitPolicy208 bank,BendVisit bend)
    {
        var straight=bank.ExitRoad(bend);
        var rows=new MotionPoint208[trace.Length];
        for(int i=0;i<rows.Length;i++)
        {
            var point=trace[i];
            var state=point.Start.State;
            var motion=ExitMotion208.From(state.Rider,straight,Mass);
            double? inward=null;
            if(i>0&&point.Ax is {} ax&&point.Ay is {} ay)
            {
                var prior=trace[i-1].Start.State.Rider;
                var forward=prior.Velocity.LengthSquared()>.01f?Vector2.Normalize(prior.Velocity):prior.Road.Tangent;
                var left=new Vector2(-forward.Y,forward.X);
                inward=-Vector2.Dot(forward*(float)ax+left*(float)ay,straight.Outward);
            }
            double s=state.Rider.TotalReferenceMeters;
            double k=BikeAngles.Wrap(bank.Road(s+.25).Heading-bank.Road(s-.25).Heading)/.5;
            double energy=(Mass*state.Rider.Velocity.LengthSquared()+Inertia*state.Rider.AngularVelocity*state.Rider.AngularVelocity+
                (Physics.RearWheel??RearWheelParameters.Default).WheelInertiaKgMetersSquared*state.Wheel*state.Wheel+
                LaunchParameters144.Default.CrankInertiaKgMetersSquared*state.Shaft*state.Shaft)*.5;
            rows[i]=new(point,motion,inward,k,point.Swept is {} sweep?motion.Room(point.Outer,sweep,.5):null,energy);
        }
        return rows;
    }

    private static void Lock208(string root,string output,string policyPath)
    {
        var source=ReadBase208(root);
        var plan=ReadPlan208(policyPath);
        if(plan.Base.Branch!=source.Plan.Branch) throw new InvalidOperationException("Mismatched baseline plan.");
        var bank=source.Policy;
        var set=plan.Maneuvers.ToDictionary(m=>m.Visit);
        var law=(Start197 state)=>bank.Action(state,set);
        var bend=bank.Bend(3);
        var runs=Official208.Select(fixture=>
        {
            var (seed,_,spec)=Fixture206(root,fixture);
            var model=Model208(spec);
            var start=new StartCase208(fixture,model,seed.Start,seed.Goal);
            return Follow208(start,bank,bend,law,seed.Goal,10000,full:true,record:true);
        }).ToArray();
        var result=new Locked208(plan,runs);
        Save(Path.Combine(output,"official.json.gz"),new {result,
            Offsets=Official208.Zip(Delta208).ToDictionary(x=>x.First,x=>x.Second),
            NativeWorlds=0,FourthExitPassed=runs.Count(r=>r.Stabilization),FullFinishes=runs.Count(r=>r.Finish)},true);
        foreach(var r in runs)
        {
            var frozen=Trace208(root,source,plan,r.Key);
            Save(Path.Combine(output,r.Key+".json.gz"),new {Frozen=plan,result=r,Trace=Motion208(frozen,bank,bend)},true);
            Console.WriteLine($"{r.Key}: legal S={r.LegalS:F3} {r.Stop}; fourth stabilization={r.Stabilization}; finish={r.Finish}");
        }
    }

    private static AuditPoint207[] Trace208(string root,Base208 source,CandidatePlan208 plan,string fixture)
    {
        var (seed,_,spec)=Fixture206(root,fixture);
        var set=plan.Maneuvers.ToDictionary(x=>x.Visit);
        var law=(Start197 at)=>source.Policy.Action(at,set);
        var cursor=Model208(spec).BeginFeedback207(seed.Start,law,seed.Goal,.5,profile:true);
        var initial=cursor.Checkpoint();
        var tape=new List<AuditPoint207>{Point207(initial,initial,cursor.Snapshot.Outer,null,null,null,initial.Active,null)};
        for(int i=0;i<10000&&cursor.Outcome is null;i++)
        {
            var before=cursor.Checkpoint();
            var requested=Math.Abs(before.Review-before.State.Time)<1e-8?(FreeRacingAction?)law(before):null;
            cursor.Advance(1);
            var after=cursor.Checkpoint();
            if(after.State.Time<=before.State.Time+1e-9)break;
            tape.Add(Point207(after,before,cursor.Snapshot.Outer,cursor.LastSweep,cursor.LastContact?.Forces,
                cursor.LastPower,before.Active,requested));
        }
        return tape.ToArray();
    }

    private static void Time208(string root,string output,string policyPath)
    {

        var source=ReadBase208(root);
        var plan=ReadPlan208(policyPath);
        var set=plan.Maneuvers.ToDictionary(m=>m.Visit);
        var (seed,_,spec)=Fixture206(root,"C");
        var model=Model208(spec,timing:true);
        var law=(Start197 state)=>source.Policy.Action(state,set);
        var trace=Trace208(root,source,plan,"C");
        var elapsed=trace[^1].Start.State.Time;
        var reviews=trace.Select(x=>x.Start).Where(x=>x.State.Time<elapsed-1e-8&&
            Math.Abs(x.Review-x.State.Time)<1e-8).ToArray();
        var runs=new List<object>();
        for(int i=0;i<3;i++)
        {
            var wall=Stopwatch.StartNew();
            using var process=Process.GetCurrentProcess();
            var cpu=process.TotalProcessorTime;
            var details=reviews.Select(at=>
            {
                long now=Stopwatch.GetTimestamp();
                var action=law(at);
                var guard=model.BeginFeedback207(at,law,seed.Goal,.5,profile:true);
                guard.Advance(40,at.State.Time+.25);
                double duration=Stopwatch.GetElapsedTime(now).TotalMilliseconds;
                bool accepted=guard.Reached||guard.Outcome is null&&guard.Snapshot.Time>=at.State.Time+.25-1e-8;
                return new {at.State.Time,S=at.State.Rider.TotalReferenceMeters,action,Milliseconds=duration,
                    Accepted=accepted,guard.Outcome,guard.Sweep,guard.Steps,guard.Counters,guard.StrictFailure206};
            }).ToArray();
            runs.Add(new {Repeat=i+1,Timing=DecisionTiming206.Measure(details.Select(x=>x.Milliseconds)),
                Rejected=details.Count(x=>!x.Accepted),CpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,
                WallSeconds=wall.Elapsed.TotalSeconds,Decisions=details});
        }
        Save(Path.Combine(output,"model-timing.json.gz"),new {Source="MODEL",NativeWorlds=0,plan,Runs=runs},true);
    }
}
