using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ContactResult206(string Status,HandlingStep? Step,ClutchStep144? Power,string? Diagnostic)
    {
        internal LoadStepH26? Load=>Step?.LoadH26;
    }

    private static ContactResult206 Solve206(Case204 state,HandlingSettings settings)
    {
        try
        {
            var (step,power)=(state with {Probe=state.Probe with {Settings=settings}}).Prepare(false);
            return new("Converged",step,power,null);
        }
        catch(InvalidOperationException failure)
        {
            return new(Status204(failure),null,null,failure.Message);
        }
        catch(ArgumentException failure)
        {
            return new("InvalidPhysicalState",null,null,failure.Message);
        }
    }

    private sealed class ContactTotals206
    {
        public int Accepted {get;private set;}
        public long Residuals {get;private set;}
        public long ClutchSolves=>Residuals;
        public long WheelQueries {get;private set;}
        public long FullBoxes {get;private set;}
        public long VisitedBoxes {get;private set;}
        public long EarlyExclusions {get;private set;}
        public long ForceTicks {get;private set;}
        public long SolverTicks {get;private set;}
        public long PowerTicks {get;private set;}
        public long IntervalTicks {get;private set;}
        public long CheapTicks {get;private set;}
        public long IsolationTicks {get;private set;}
        public long RefinementTicks {get;private set;}
        public long BoundaryTicks {get;private set;}
        public double MaxAppliedResidual {get;private set;}
        public double MaxWheelImpulse {get;private set;}
        public double MaxCrankEnergy {get;private set;}
        public double MaxRearUtilization {get;private set;}
        public double MinFrontNormal {get;private set;}=double.PositiveInfinity;
        public double MinRearNormal {get;private set;}=double.PositiveInfinity;
        public Dictionary<string,int> Classifications {get;}=[];

        internal void Add(ContactResult206 x,float dt)
        {
            Classifications[x.Status]=Classifications.GetValueOrDefault(x.Status)+1;
            if(x.Load is not {} load||x.Power is not {} power||x.Step is not {} step||
                load.Precision is not {} precision||precision.Combined is not {} combined) return;
            if(load.FrontNormalNewtons<=0||load.RearNormalNewtons<=0||
                Math.Abs(load.FrontNormalNewtons+load.RearNormalNewtons-Mass*9.81)>1e-9||
                Math.Abs(precision.AppliedResidual)>2e-5||Math.Abs(load.Residual)>2e-5||
                power.Wheel.AppliedLongitudinalNewtons!=precision.AppliedRearLongitudinalNewtons||
                power.Wheel.RearUtilization>1.000001||power.Wheel.LongitudinalLossJoules<-.001||
                combined.LateralDissipationJoules<-.001||power.ClutchHeatJoules<-.001||
                Math.Abs(precision.WheelImpulseResidual)>1e-10||Math.Abs(power.EnergyResidualJoules)>1e-6||
                !FreeLearningExperiment.SteeringStepIsPhysical(step.Forces,dt))
                throw new InvalidOperationException("Physical invariant failed for an accepted paired checkpoint.");
            Accepted++;
            Residuals+=load.Iterations;
            WheelQueries+=combined.WheelQueries;
            FullBoxes+=combined.ProofBoxes;
            ForceTicks+=precision.ForceTicks;
            SolverTicks+=precision.SolverTicks;
            PowerTicks+=combined.PowerTicks;
            IntervalTicks+=combined.ProofTicks;
            if(precision.Search206 is {} profile)
            {
                VisitedBoxes+=profile.VisitedBoxes;
                EarlyExclusions+=profile.EarlyExclusions;
                CheapTicks+=profile.CheapTicks;
                IsolationTicks+=profile.IsolationTicks;
                RefinementTicks+=profile.RefinementTicks;
                BoundaryTicks+=profile.BoundaryTicks;
            }
            MaxAppliedResidual=Math.Max(MaxAppliedResidual,Math.Abs(precision.AppliedResidual));
            MaxWheelImpulse=Math.Max(MaxWheelImpulse,Math.Abs(precision.WheelImpulseResidual));
            MaxCrankEnergy=Math.Max(MaxCrankEnergy,Math.Abs(power.EnergyResidualJoules));
            MaxRearUtilization=Math.Max(MaxRearUtilization,power.Wheel.RearUtilization);
            MinFrontNormal=Math.Min(MinFrontNormal,load.FrontNormalNewtons);
            MinRearNormal=Math.Min(MinRearNormal,load.RearNormalNewtons);
        }
    }

    private static bool SamePhysical206(ContactResult206 a,ContactResult206 b) => a.Status==b.Status&&
        a.Step?.Forces==b.Step?.Forces&&a.Step?.PostureH27==b.Step?.PostureH27&&
        a.Step?.LeanAfter==b.Step?.LeanAfter&&a.Power==b.Power&&
        a.Load?.Acceleration==b.Load?.Acceleration&&a.Load?.Residual==b.Load?.Residual;

    private static void Parity206(string repository,string output)
    {
        string Data(int study)=>Path.Combine(repository,"data","experiments","speedway"+study);
        var paths=Directory.GetFiles(Data(202),"*.json.gz")
            .Where(p=>Path.GetFileName(p).Contains("-same.")||Path.GetFileName(p).Contains("-learned-"))
            .Concat(Enumerable.Range(203,3).SelectMany(id=>Directory.GetFiles(Path.Combine(Data(id),"native"),"*.json.gz")))
            .Order(StringComparer.Ordinal).ToArray();
        var old=Settings202("H28-C") with {ProfileCertificate=true};
        var fast=Settings202("H28-F") with {ProfileCertificate=true};
        var rows=new List<object>();
        var timer=Stopwatch.StartNew();
        foreach(string path in paths)
        {
            var archive=Read199<Archive204>(path);
            var rider=archive.Run.Riders.Single();
            var c=new ContactTotals206();
            var f=new ContactTotals206();
            int exact=0,determinism=0,physicalDifference=0,classificationsChanged=0,frozenExact=0;
            double maxAx=0,maxForce=0,maxMoment=0,maxWheel=0,maxCrank=0;
            object? first=null;
            for(int i=1;i<rider.Samples.Length;i++)
            {
                var state=Accepted204(archive,i);
                var accepted=rider.Samples[i];
                var physicalReplay=Solve206(state,archive.Handling);
                if(physicalReplay.Step?.Forces.FrontForce==accepted.Forces?.FrontForce&&
                    physicalReplay.Step?.Forces.RearForce==accepted.Forces?.RearForce&&
                    physicalReplay.Step?.Forces.SteeringAfter==accepted.Input.Steering&&
                    physicalReplay.Power?.AfterOmega==accepted.CrankOmega&&
                    physicalReplay.Power?.Wheel.AfterOmega==accepted.Input.Omega)
                    frozenExact++;
                var reference=Solve206(state,old);
                var optimized=Solve206(state,fast);
                c.Add(reference,state.Probe.Dt);
                f.Add(optimized,state.Probe.Dt);
                if(i%31==0)
                {
                    if(!SamePhysical206(optimized,Solve206(state,fast))) throw new InvalidOperationException("Repeated-state determinism failed.");
                    determinism++;
                }
                if(SamePhysical206(reference,optimized)) {exact++;continue;}
                first??=new {Index=i,state.Probe.State,state.Probe.Action,state.Probe.Dt,
                    H28C=new {reference.Load,reference.Diagnostic,reference.Step?.Forces,reference.Power},
                    H28F=new {optimized.Load,optimized.Diagnostic,optimized.Step?.Forces,optimized.Power}};
                if(reference.Status!=optimized.Status) classificationsChanged++;
                if(reference.Step is {} a&&optimized.Step is {} b)
                {
                    physicalDifference++;
                    maxAx=Math.Max(maxAx,Math.Abs(reference.Load!.Acceleration-optimized.Load!.Acceleration));
                    maxForce=Math.Max(maxForce,Math.Max(Vector2.Distance(a.Forces.FrontForce,b.Forces.FrontForce),
                        Vector2.Distance(a.Forces.RearForce,b.Forces.RearForce)));
                    maxMoment=Math.Max(maxMoment,Math.Abs(a.Forces.NetTorque-b.Forces.NetTorque));
                    maxWheel=Math.Max(maxWheel,Math.Abs(reference.Power!.Wheel.AfterOmega-optimized.Power!.Wheel.AfterOmega));
                    maxCrank=Math.Max(maxCrank,Math.Abs(reference.Power.AfterOmega-optimized.Power.AfterOmega));
                }
            }
            var name=Path.GetRelativePath(repository,path).Replace('\\','/');
            var total=rider.Samples.Length-1;
            rows.Add(new {Name=name,Total=total,FrozenExact=frozenExact,PairExact=exact,Determinism=determinism,
                PhysicalDifferences=physicalDifference,ClassificationsChanged=classificationsChanged,First=first,
                MaxAx=maxAx,MaxForce=maxForce,MaxMoment=maxMoment,MaxWheel=maxWheel,MaxCrank=maxCrank,H28C=c,H28F=f});
            Console.WriteLine($"{Path.GetFileName(path)} {exact}/{total} C/F exact; {frozenExact} archived; " +
                $"residuals {c.Residuals}->{f.Residuals}, full boxes {c.FullBoxes}->{f.FullBoxes}");
        }
        var referenceArchive=Read199<Archive204>(Path.Combine(repository,NativeSource205));
        var focused=new List<object>();
        var shots=Shots205(repository,referenceArchive);
        foreach(var shot in shots)
        {
            var a=Measure205(shot,"H28-C");
            var b=Measure205(shot,"H28-F");
            focused.Add(new {shot.Name,shot.Origin,H28C=a,H28F=b});
        }
        var grid=CompareSynthetic206(shots.First(x=>x.Name=="bend-1-apex"));
        Save(Path.Combine(output,"parity.json"),new {Archives=rows,Focused=focused,SyntheticGrid=grid,
            Seconds=timer.Elapsed.TotalSeconds,Frequency=Stopwatch.Frequency,NativeWorlds=0});
    }

    private static object CompareSynthetic206(Shot205 basis)
    {
        int total=0,exact=0,accepted=0;
        object? first=null;
        foreach(var x in Synthetic205(basis))
        {
            var old=Solve("H28-C");
            var fast=Solve("H28-F");
            total++;
            if(fast.Step is not null) accepted++;
            if(SamePhysical206(old,fast)) exact++;
            else first??=new {x.Shot,x.Kappa,x.Beta,x.Yaw,x.SpeedKph,x.FrontNormal,x.Gas,H28C=old,H28F=fast};

            ContactResult206 Solve(string variant)
            {
                try
                {
                    var q=Query205(x.Shot,variant,x.FrontNormal);
                    var step=q.Query.Solve(q.Lo,q.Hi,q.Posture);
                    if(step.LoadH26 is not {} load||load.Precision is not {} precision||
                        Math.Abs(precision.AppliedResidual)>2e-5||q.Shaft.Prepared.Wheel.RearUtilization>1.000001||
                        !FreeLearningExperiment.SteeringStepIsPhysical(step.Forces,x.Shot.Dt))
                        throw new InvalidOperationException("Synthetic applied invariant failed.");
                    return new("Converged",step,q.Shaft.Prepared,null);
                }
                catch(ContactRootException error){return new(error.Status.ToString(),null,null,error.Message);}
            }
        }
        Console.WriteLine($"Synthetic fixed loads: {exact}/{total} exact, {accepted} accepted.");
        return new {Total=total,Exact=exact,Accepted=accepted,First=first};
    }
}
