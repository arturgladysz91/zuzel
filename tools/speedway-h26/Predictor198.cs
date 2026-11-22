using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Safety153;
using Speed148;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Profile198(long Steps,long ForceTicks,long IntegrateTicks,long GeometryTicks,long GateTicks,long ClockTicks)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long ResidualForceTicks { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long LoadSolverTicks { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long CombinedPowerTicks { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public long CombinedProofTicks { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public ForecastWork206 Work206 { get; init; }
}
internal readonly record struct ForecastWork206(long Steps,long ResidualEvaluations,long ClutchSolves,long WheelQueries,
    long WheelIterations,long FullBoxes,long VisitedBoxes,long EarlyExclusions,long CheapTicks,long IntervalTicks,
    long IsolationTicks,long RefinementTicks,long BoundaryTicks,long ForceTicks,long PowerTicks,long GeometryTicks,
    long StrictGateTicks,long RateLimited,long NearLoadBoundary)
{
    public static ForecastWork206 operator +(ForecastWork206 a,ForecastWork206 b)=>new(a.Steps+b.Steps,
        a.ResidualEvaluations+b.ResidualEvaluations,a.ClutchSolves+b.ClutchSolves,a.WheelQueries+b.WheelQueries,
        a.WheelIterations+b.WheelIterations,a.FullBoxes+b.FullBoxes,a.VisitedBoxes+b.VisitedBoxes,a.EarlyExclusions+b.EarlyExclusions,
        a.CheapTicks+b.CheapTicks,a.IntervalTicks+b.IntervalTicks,a.IsolationTicks+b.IsolationTicks,
        a.RefinementTicks+b.RefinementTicks,a.BoundaryTicks+b.BoundaryTicks,a.ForceTicks+b.ForceTicks,
        a.PowerTicks+b.PowerTicks,a.GeometryTicks+b.GeometryTicks,a.StrictGateTicks+b.StrictGateTicks,
        a.RateLimited+b.RateLimited,a.NearLoadBoundary+b.NearLoadBoundary);

    public static ForecastWork206 operator -(ForecastWork206 a,ForecastWork206 b)=>new(a.Steps-b.Steps,
        a.ResidualEvaluations-b.ResidualEvaluations,a.ClutchSolves-b.ClutchSolves,a.WheelQueries-b.WheelQueries,
        a.WheelIterations-b.WheelIterations,a.FullBoxes-b.FullBoxes,a.VisitedBoxes-b.VisitedBoxes,a.EarlyExclusions-b.EarlyExclusions,
        a.CheapTicks-b.CheapTicks,a.IntervalTicks-b.IntervalTicks,a.IsolationTicks-b.IsolationTicks,
        a.RefinementTicks-b.RefinementTicks,a.BoundaryTicks-b.BoundaryTicks,a.ForceTicks-b.ForceTicks,
        a.PowerTicks-b.PowerTicks,a.GeometryTicks-b.GeometryTicks,a.StrictGateTicks-b.StrictGateTicks,
        a.RateLimited-b.RateLimited,a.NearLoadBoundary-b.NearLoadBoundary);
}
internal sealed record ContactFailure203(HandlingForecast State, FreeRacingAction Action,
    double RiderHeightOffsetMeters, float Dt, string Error);

internal sealed class Predictor198(TrackFrame frame,TrackStateSnapshot surface,WorldParameters physics,
    HandlingSettings handling,Schedule188 timing,RiderSpec spec,float mass,float inertia,RacingProfile206? strict=null)
{
    private readonly RacingLineGeometry boundary=new(frame);
    private RacingProfile206? StrictProfile=>strict;
    private readonly float positionFactor=(physics.SolverSubsteps+1f)/(2f*physics.SolverSubsteps);
    private readonly double riderReaction=RiderController.ReactionSeconds(spec);
    private readonly TrackSurfaceState grip=surface.SampleSurface(0,2);
    internal const double Clearance=.50;
    internal const double Review=.05;

    internal Cursor198 Begin(Start197 start, FreeRacingAction[] controls,int from,double goal,double margin,bool profile=false,
        Func<RiderView,double,string?>? constraint=null,double stepSeconds=.01,
        Func<RiderView,double,double,string?>? motionConstraint=null) =>
        new(this,start,controls,from,goal,margin,profile,constraint,stepSeconds,motionConstraint);

    internal Cursor198 BeginFeedback207(Start197 start,Func<Start197,FreeRacingAction> feedback,double goal,
        double margin,bool profile=false) => new(this,start,[],0,goal,margin,profile,null,.01,null,feedback);

    internal sealed class Cursor198
    {
        private readonly Predictor198 model;
        private readonly FreeRacingAction[] controls;
        private readonly Func<Start197,FreeRacingAction>? feedback207;
        private readonly Queue<FreeRacingCommand> pending;
        private readonly double goal,margin,originTime;
        private readonly bool profile;
        private readonly Func<RiderView,double,string?>? constraint;
        private readonly Func<RiderView,double,double,string?>? motionConstraint;
        private readonly double stepSeconds;
        private readonly CrankClutch144 shaft;
        private readonly RacingProfile206.Cursor? strict;
        private RearWheelState wheel;
        private HandlingForecast state;
        private double riderHeightOffsetMeters;
        private FreeRacingAction active;
        private double nextReview;
        private int index;
        private ClearancePair gap;
        private long forceTicks,integrateTicks,geometryTicks,gateTicks,clockTicks;
        private long residualForceTicks,loadSolverTicks,combinedPowerTicks,combinedProofTicks;
        private ForecastWork206 work206;

        internal Cursor198(Predictor198 model,Start197 start,FreeRacingAction[] controls,int from,
            double goal,double margin,bool profile,Func<RiderView,double,string?>? constraint,double stepSeconds,
            Func<RiderView,double,double,string?>? motionConstraint,Func<Start197,FreeRacingAction>? feedback207=null)
        {
            this.model=model;
            this.controls=controls;
            this.feedback207=feedback207;
            this.goal=goal;
            this.margin=margin;
            this.profile=profile;
            this.constraint=constraint;
            this.motionConstraint=motionConstraint;
            strict=model.StrictProfile?.Begin(start);
            this.stepSeconds=stepSeconds;
            originTime=start.State.Time;
            active=start.Active;
            pending=new(start.Pending);
            nextReview=start.Review;
            index=from;
            state=start.State;
            LastLegalS207=state.Rider.TotalReferenceMeters;
            riderHeightOffsetMeters = start.RiderHeightOffsetMeters;
            shaft=new(LaunchParameters144.Default,state.Shaft,model.Handling.H26?.EngineLoss);
            wheel=ForecastFork148.Wheel(state.Wheel,model.Physics.RearWheel);
            var first=model.boundary.Clearance(state.Rider);
            gap=new(first.Inner,first.Outer);
        }

        internal string? Outcome { get; private set; }
        internal ContactFailure203? ContactFailure { get; private set; }
        internal RacingFailure206? StrictFailure206 { get; private set; }
        internal int Steps { get; private set; }
        internal bool Reached=>Outcome=="goal";
        internal double GoalTime { get; private set; }
        internal double Sweep { get; private set; }=100;
        internal double MaxBeta { get; private set; }
        internal double PeakForce { get; private set; }
        internal double LastLegalS207 { get; private set; }
        internal double LegalSweep207 { get; private set; }=100;
        internal double LastSweep { get; private set; }
        internal int NextIndex=>index;
        internal HeadingSample198 Snapshot=>new(state.Time,state.Rider.TotalReferenceMeters,state.Rider.Position.X,
            state.Rider.Position.Y,state.Rider.Speed,state.Rider.Heading,state.Rider.AngularVelocity,state.Rider.SlideRadians,
            state.Wheel,state.Shaft,state.Steer,state.Lean,LastSweep,gap.Outer,gap.Inner,active);
        internal Profile198 Counters=>new(Steps,forceTicks,integrateTicks,geometryTicks,gateTicks,clockTicks)
            {ResidualForceTicks=residualForceTicks,LoadSolverTicks=loadSolverTicks,
                CombinedPowerTicks=combinedPowerTicks,CombinedProofTicks=combinedProofTicks,Work206=model.Handling.ProfileCertificate?Work206:default};
        internal ForecastWork206 Work206=>work206 with {Steps=Steps,ForceTicks=forceTicks,
            PowerTicks=combinedPowerTicks,IntervalTicks=combinedProofTicks,GeometryTicks=geometryTicks,StrictGateTicks=gateTicks};
        internal HandlingStep? LastContact { get; private set; }
        internal RiderView LastContactInput { get; private set; }
        internal ClutchStep144? LastPower { get; private set; }

        private void ApplyDue()
        {
            while(pending.TryPeek(out var next) && next.ActivationAt<=state.Time+1e-9)
                active=pending.Dequeue().Action;
        }

        internal Start197 Checkpoint()
        {
            ApplyDue();
            return new(state with {Rider=state.Rider with {Drive=active.Drive}},active,pending.ToArray(),nextReview)
                { RiderHeightOffsetMeters = riderHeightOffsetMeters,TechniqueHistory=strict?.Capture() };
        }

        internal void Advance(int stepBudget,double timeLimit=double.PositiveInfinity)
        {
            if(Outcome is not null) return;
            try
            {
                for(int n=0;n<stepBudget && state.Time<timeLimit-1e-9;n++)
                {
                    if(state.Rider.TotalReferenceMeters>=goal)
                    {
                        if(strict is not null)
                        {
                            StrictFailure206=strict.Check(state.Rider,state.Time,gap.Outer,Math.Min(gap.Inner,gap.Outer),
                                margin,null,true);
                            if(StrictFailure206 is not null)
                            {
                                Outcome=StrictFailure206.Condition;
                                return;
                            }
                        }
                        Outcome="goal";GoalTime=state.Time;
                        return;
                    }
                    if(state.Time-originTime>85){Outcome="deadline";return;}
                    if(!Step(timeLimit)) return;
                }
            }
            catch(InvalidOperationException error)
            {
                Outcome="physics: "+error.Message;
            }
        }

        private long Mark()=>profile?Stopwatch.GetTimestamp():0;
        private bool Step(double timeLimit)
        {
            long start=Mark();
            double t=state.Time;
            ApplyDue();
            if(t>=nextReview-1e-9)
            {
                if(feedback207 is null&&index>=controls.Length) {Outcome="commands-ended";return false;}
                var action=feedback207 is null?controls[index]:feedback207(Checkpoint());
                index++;
                double due=model.Timing.Due(nextReview,model.riderReaction,pending.LastOrDefault());
                pending.Enqueue(new(nextReview,due,action));
                nextReview+=model.Timing.Period(nextReview);
            }
            double limit=Math.Min(timeLimit,(Math.Floor((t+1e-8)/stepSeconds)+1)*stepSeconds);
            limit=Math.Min(limit,nextReview);
            if(pending.TryPeek(out var onDeck)) limit=Math.Min(limit,onDeck.ActivationAt);
            double dt=limit-t;
            if(dt<=1e-9) {Outcome="clock";return false;}
            var before=state.Rider with {Drive=active.Drive};
            if(profile) clockTicks+=Mark()-start;

            start=Mark();
            HandlingStep contact;
            try
            {
                contact=HandlingContact.Prepare(model.Handling,model.grip,before,model.Spec,model.Physics,t,
                    model.Mass,model.Inertia,wheel,state.Steer,state.Lean,(float)dt,active.SteeringRadians,shaft,model.riderReaction,
                    riderHeightOffsetMeters);
            }
            catch (InvalidOperationException failure)
            {
                ContactFailure = new(state, active, riderHeightOffsetMeters, (float)dt, failure.Message);
                if(strict is not null)
                    StrictFailure206=new("physical",failure.Message,state.Time,state.Rider.TotalReferenceMeters,state.Rider,gap.Outer,LastSweep,margin);
                throw;
            }
            var power=shaft.Prepared;
            var forces=contact.Forces;
            if(profile)
            {
                LastContact=contact;
                LastContactInput=before;
                LastPower=power;
                forceTicks+=Mark()-start;
                if (contact.LoadH26?.Precision is { } precision)
                {
                    residualForceTicks+=precision.ForceTicks;
                    loadSolverTicks+=precision.SolverTicks;
                    if(precision.Combined is {} combined)
                    {
                        combinedPowerTicks+=combined.PowerTicks;
                        combinedProofTicks+=combined.ProofTicks;
                        if(precision.Search206 is {} search&&contact.LoadH26 is {} load)
                        {
                            work206+=new ForecastWork206(0,load.Iterations,load.Iterations,combined.WheelQueries,combined.WheelIterations,
                                combined.ProofBoxes,search.VisitedBoxes,search.EarlyExclusions,search.CheapTicks,0,
                                search.IsolationTicks,search.RefinementTicks,search.BoundaryTicks,0,0,0,0,
                                forces.SteeringRequest!=forces.SteeringAfter?1:0,
                                Math.Min(load.FrontNormalNewtons,load.RearNormalNewtons)<50?1:0);
                        }
                    }
                }
            }

            start=Mark();
            float step=(float)dt;
            var a=forces.NetForce/model.Mass;
            float angular=forces.NetTorque/model.Inertia;
            var pos=before.Position+before.Velocity*step+model.positionFactor*step*step*a;
            var road=model.Frame.Project(pos);
            double s=before.TotalReferenceMeters+model.Frame.UnwrapDelta(before.Road.ReferenceMeters,road.ReferenceMeters);
            var v=before with {Position=pos,Velocity=before.Velocity+step*a,Heading=(float)BikeAngles.Wrap(
                before.Heading+before.AngularVelocity*step+model.positionFactor*step*step*angular),
                AngularVelocity=before.AngularVelocity+step*angular,Road=road,TotalReferenceMeters=s,
                DistanceMeters=before.DistanceMeters+Vector2.Distance(before.Position,pos),Context=model.Frame.Context(road,s)};
            wheel=ForecastFork148.Wheel(power.Wheel.AfterOmega,wheel.Parameters);
            shaft.Commit();
            riderHeightOffsetMeters = contact.PostureH27?.RiderHeightOffsetAfterMeters ?? 0;
            state=new(v,limit,wheel.AngularVelocityRadPerSecond,forces.SteeringAfter,contact.LeanAfter,shaft.Omega);
            if(profile) integrateTicks+=Mark()-start;

            start=Mark();
            var edge=model.boundary.Clearance(v);
            LastSweep=Clearance153.SweepLowerBound(before,v,dt,(gap.Inner,gap.Outer),edge);
            gap=new(edge.Inner,edge.Outer);
            if(profile) geometryTicks+=Mark()-start;

            start=Mark();
            Steps++;
            Sweep=Math.Min(Sweep,LastSweep);
            MaxBeta=Math.Max(MaxBeta,Math.Abs(v.SlideRadians));
            if(strict is not null)
            {
                StrictFailure206=strict.Check(v,limit,edge.Outer,LastSweep,margin,
                    RacingProfile206.Physical(v,limit,step,forces,power),s>=goal);
                Outcome=StrictFailure206?.Condition;
                if(Outcome is null&&feedback207 is not null)
                {
                    LastLegalS207=s;
                    LegalSweep207=Math.Min(LegalSweep207,LastSweep);
                }
                if(Outcome is null&&s>=goal)
                {
                    Outcome="goal";
                    GoalTime=limit-dt*Math.Clamp((s-goal)/(s-before.TotalReferenceMeters),0,1);
                }
                if(profile) gateTicks+=Mark()-start;
                return Outcome is null;
            }
            if(!double.IsFinite(LastSweep) || !float.IsFinite(v.Speed) || power.Wheel.RearUtilization>1.000001 ||
                power.ClutchHeatJoules<-.001 || power.Wheel.LongitudinalLossJoules<-.001) Outcome="force-or-energy";
            else if(LastSweep<margin) Outcome="swept";
            else if(MaxBeta>Math.PI/4) Outcome="beta";
            else if(Vector2.Dot(v.Velocity,v.Road.Tangent)<-.001 || v.Speed<.2 && limit-originTime>2)
                Outcome="progress";
            else if(forces.SteeringTargetBeforeRounding is not null&&!FreeLearningExperiment.SteeringStepIsPhysical(forces,step))
                Outcome="actuator invariant";
            else if(constraint?.Invoke(v,edge.Outer) is { } stop) Outcome=stop;
            else if(motionConstraint?.Invoke(v,edge.Outer,state.Time) is {} motionStop) Outcome=motionStop;
            else if(s>=goal)
            {
                Outcome="goal";
                GoalTime=limit-dt*Math.Clamp((s-goal)/(s-before.TotalReferenceMeters),0,1);
            }
            if(profile) gateTicks+=Mark()-start;
            return Outcome is null;
        }
    }

    private TrackFrame Frame=>frame;
    private WorldParameters Physics=>physics;
    private HandlingSettings Handling=>handling;
    private Schedule188 Timing=>timing;
    private RiderSpec Spec=>spec;
    private float Mass=>mass;
    private float Inertia=>inertia;
    private readonly record struct ClearancePair(double Inner,double Outer);
}

internal sealed record HeadingSample198(double Time,double S,float X,float Y,float Speed,float Heading,
    float Yaw,float Beta,double Wheel,double Shaft,float Steer,double Lean,double Sweep,double Outer,double Inner,
    FreeRacingAction Active);
