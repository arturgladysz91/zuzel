using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Safety153;
using Speed148;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Profile198(long Steps,long ForceTicks,long IntegrateTicks,long GeometryTicks,long GateTicks,long ClockTicks);

internal sealed class Predictor198(TrackFrame frame,TrackStateSnapshot surface,WorldParameters physics,
    HandlingSettings handling,Schedule188 timing,RiderSpec spec,float mass,float inertia)
{
    private readonly RacingLineGeometry boundary=new(frame);
    private readonly float positionFactor=(physics.SolverSubsteps+1f)/(2f*physics.SolverSubsteps);
    private readonly double riderReaction=RiderController.ReactionSeconds(spec);
    private readonly TrackSurfaceState grip=surface.SampleSurface(0,2);
    internal const double Clearance=.50;
    internal const double Review=.05;

    internal Cursor198 Begin(Start197 start, FreeRacingAction[] controls,int from,double goal,double margin,bool profile=false,
        Func<RiderView,double,string?>? constraint=null,double stepSeconds=.01) =>
        new(this,start,controls,from,goal,margin,profile,constraint,stepSeconds);

    internal sealed class Cursor198
    {
        private readonly Predictor198 model;
        private readonly FreeRacingAction[] controls;
        private readonly Queue<FreeRacingCommand> pending;
        private readonly double goal,margin,originTime;
        private readonly bool profile;
        private readonly Func<RiderView,double,string?>? constraint;
        private readonly double stepSeconds;
        private readonly CrankClutch144 shaft;
        private RearWheelState wheel;
        private HandlingForecast state;
        private FreeRacingAction active;
        private double nextReview;
        private int index;
        private ClearancePair gap;
        private long forceTicks,integrateTicks,geometryTicks,gateTicks,clockTicks;

        internal Cursor198(Predictor198 model,Start197 start,FreeRacingAction[] controls,int from,
            double goal,double margin,bool profile,Func<RiderView,double,string?>? constraint,double stepSeconds)
        {
            this.model=model;
            this.controls=controls;
            this.goal=goal;
            this.margin=margin;
            this.profile=profile;
            this.constraint=constraint;
            this.stepSeconds=stepSeconds;
            originTime=start.State.Time;
            active=start.Active;
            pending=new(start.Pending);
            nextReview=start.Review;
            index=from;
            state=start.State;
            shaft=new(LaunchParameters144.Default,state.Shaft);
            wheel=ForecastFork148.Wheel(state.Wheel,model.Physics.RearWheel);
            var first=model.boundary.Clearance(state.Rider);
            gap=new(first.Inner,first.Outer);
        }

        internal string? Outcome { get; private set; }
        internal int Steps { get; private set; }
        internal bool Reached=>Outcome=="goal";
        internal double GoalTime { get; private set; }
        internal double Sweep { get; private set; }=100;
        internal double MaxBeta { get; private set; }
        internal double PeakForce { get; private set; }
        internal double LastSweep { get; private set; }
        internal int NextIndex=>index;
        internal HeadingSample198 Snapshot=>new(state.Time,state.Rider.TotalReferenceMeters,state.Rider.Position.X,
            state.Rider.Position.Y,state.Rider.Speed,state.Rider.Heading,state.Rider.AngularVelocity,state.Rider.SlideRadians,
            state.Wheel,state.Shaft,state.Steer,state.Lean,LastSweep,gap.Outer,gap.Inner,active);
        internal Profile198 Counters=>new(Steps,forceTicks,integrateTicks,geometryTicks,gateTicks,clockTicks);

        private void ApplyDue()
        {
            while(pending.TryPeek(out var next) && next.ActivationAt<=state.Time+1e-9)
                active=pending.Dequeue().Action;
        }

        internal Start197 Checkpoint()
        {
            ApplyDue();
            return new(state with {Rider=state.Rider with {Drive=active.Drive}},active,pending.ToArray(),nextReview);
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
                if(index>=controls.Length) {Outcome="commands-ended";return false;}
                var action=controls[index++];
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
            var contact=HandlingContact.Prepare(model.Handling,model.grip,before,model.Spec,model.Physics,t,
                model.Mass,model.Inertia,wheel,state.Steer,state.Lean,(float)dt,active.SteeringRadians,shaft,model.riderReaction);
            var power=shaft.Prepared;
            var forces=contact.Forces;
            if(profile) forceTicks+=Mark()-start;

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
            if(!double.IsFinite(LastSweep) || !float.IsFinite(v.Speed) || power.Wheel.RearUtilization>1.000001 ||
                power.ClutchHeatJoules<-.001 || power.Wheel.LongitudinalLossJoules<-.001) Outcome="force-or-energy";
            else if(LastSweep<margin) Outcome="swept";
            else if(MaxBeta>Math.PI/4) Outcome="beta";
            else if(Vector2.Dot(v.Velocity,v.Road.Tangent)<-.001 || v.Speed<.2 && limit-originTime>2)
                Outcome="progress";
            else if(constraint?.Invoke(v,edge.Outer) is { } stop) Outcome=stop;
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

