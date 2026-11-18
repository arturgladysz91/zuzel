using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Node197(double Time,double S,float X,float Y,float Speed,float Yaw,float Beta,
    double Sweep,double Outer,double Drive,double RequestedDrive,double RequestedSteer,double Fx,double Fy,double Heat,double SlipLong,double SlipLat);
internal sealed record Start197(HandlingForecast State,FreeRacingAction Active,FreeRacingCommand[] Pending,double Review);
internal sealed record Roll197(bool Legal,double Seconds,double Progress,double Sweep,double Beta,double Edge,string Stop,
    double ExitSpeed,Node197[]? Trace,int Steps);
internal sealed record Iteration197(int Round,int Evaluation,bool Legal,double Seconds,double Sweep,double Progress,
    double SteerRadius,double GasRadius,string Stop);
internal sealed record ExitConstraint198(double Reference,double MaximumOuter,double MaximumCourseRadians);
internal sealed record Search197(FreeRacingAction[] Controls,Roll197 Result,Iteration197[] Iterations,int Evaluations,double WallSeconds);

internal sealed class Shooting197(TrackFrame frame,TrackStateSnapshot surface,WorldParameters physics,
    HandlingSettings handling,Schedule188 timing,RiderSpec spec,float mass,float inertia)
{
    internal const double ReviewSeconds=.05;
    internal const double NativeClearance=.50;
    internal const double CertifiedClearance=.525;
    internal const double ReducedClearance=.15;
    internal const double SecondsBudget=86;
    private readonly RacingLineGeometry boundary=new(frame);
    private readonly TrackSurfaceState grip=surface.SampleSurface(0,2);

    internal Roll197 Evaluate(Start197 initial,FreeRacingAction[] controls,double goal,double margin,
        bool trace=false,double grid=.01,ExitConstraint198? exit=null)
    {
        var state=initial.State;
        double due=initial.Review;
        var pending=new Queue<FreeRacingCommand>(initial.Pending);
        var active=initial.Active;
        var oldGap=boundary.Clearance(state.Rider);
        var samples=trace?new List<Node197>():null;
        int steps=0,index=0;
        double sweep=100,beta=0,outer=oldGap.Outer;
        var last=state;
        string stop="deadline";
        void Record(double certified,double fx,double fy,double heat,double longLoss,double latLoss)
        {
            if(samples is null) return;
            var v=state.Rider;
            var request=index>0?controls[index-1]:active;
            samples.Add(new(state.Time,v.TotalReferenceMeters,v.Position.X,v.Position.Y,v.Speed,
                v.AngularVelocity,v.SlideRadians,certified,outer,active.Drive,request.Drive,request.SteeringRadians,fx,fy,heat,longLoss,latLoss));
        }
        Record(Math.Min(oldGap.Inner,oldGap.Outer),0,0,0,0,0);
        try
        {
            while(state.Time-initial.State.Time<SecondsBudget && state.Rider.TotalReferenceMeters<goal)
            {
                while(pending.TryPeek(out var nextCommand) && nextCommand.ActivationAt<=state.Time+1e-9)
                    active=pending.Dequeue().Action;
                if(index<controls.Length && due<=state.Time+1e-9)
                {
                    double release=timing.Due(due,RiderController.ReactionSeconds(spec),pending.LastOrDefault());
                    pending.Enqueue(new(due,release,controls[index++]));
                    due+=timing.Period(due);
                }
                var before=state;
                double next=(Math.Floor((state.Time+1e-8)/grid)+1)*grid;
                if(index<controls.Length)next=Math.Min(next,due);
                if(pending.TryPeek(out var incoming))next=Math.Min(next,incoming.ActivationAt);
                double dt=next-state.Time;
                if(dt<=1e-9) throw new InvalidOperationException("Nonadvancing forecast clock.");
                double fx=0,fy=0,heat=0,longLoss=0,latLoss=0;
                state=HandlingPreview.Advance(state,dt,active,[],grip,handling,spec,physics,frame,
                    mass,inertia,LaunchParameters144.Default,(f,p)=>{
                        if(p.Wheel.RearUtilization>1.000001 || p.Wheel.LongitudinalLossJoules<-.001 || p.ClutchHeatJoules<-.001)
                            throw new InvalidOperationException("Contact or energy budget violated.");
                        fx=f.RearLongitudinal;
                        fy=f.FrontLateral+f.RearLateral;
                        heat=p.Wheel.LongitudinalLossJoules+p.ClutchHeatJoules+p.TransmissionLossJoules;
                        if(trace)
                        {
                            longLoss+=p.Wheel.LongitudinalLossJoules;
                            latLoss+=p.Seconds*(-Math.Min(0,Vector2.Dot(f.FrontForce,f.FrontVelocity))-
                                Math.Min(0,f.RearLateral*Vector2.Dot(f.BodyLeft,f.RearVelocity)));
                        }
                    });
                steps++;
                var v=state.Rider;
                var gap=boundary.Clearance(v);
                outer=gap.Outer;
                var certified=Clearance153.SweepLowerBound(before.Rider,v,dt,oldGap,gap);
                sweep=Math.Min(sweep,certified);
                beta=Math.Max(beta,Math.Abs(v.SlideRadians));
                oldGap=gap;
                last=before;
                Record(certified,fx,fy,heat,longLoss,latLoss);
                if(!double.IsFinite(sweep) || !float.IsFinite(v.Speed)) {stop="nonfinite"; break;}
                if(certified<margin) {stop="swept"; break;}
                if(beta>Math.PI/4) {stop="beta"; break;}
                if(Vector2.Dot(v.Velocity,v.Road.Tangent)<-.001 ||
                    v.Speed<.2 && state.Time-initial.State.Time>2) {stop="progress"; break;}
                if(exit is not null && before.Rider.TotalReferenceMeters<exit.Reference && v.TotalReferenceMeters>=exit.Reference)
                {
                    double course=BikeAngles.Wrap(Math.Atan2(v.Velocity.Y,v.Velocity.X)-v.Road.Heading);
                    if(gap.Outer>exit.MaximumOuter || Math.Abs(course)>exit.MaximumCourseRadians)
                    { stop="exit-corridor"; break; }
                }
                if(state.Rider.TotalReferenceMeters>=goal) stop="goal";
            }
        }
        catch(InvalidOperationException) {stop="physical-invariant";}
        double end=state.Time-initial.State.Time;
        if(stop=="goal")
        {
            double s=state.Rider.TotalReferenceMeters,prev=last.Rider.TotalReferenceMeters;
            end-=Math.Clamp((s-goal)/(s-prev),0,1)*(state.Time-last.Time);
        }
        return new(stop=="goal",end,state.Rider.TotalReferenceMeters,sweep,beta,outer,stop,
            state.Rider.Speed,samples?.ToArray(),steps);
    }

    internal static bool Better(Roll197 a,Roll197 b)
    {
        if(a.Legal!=b.Legal) return a.Legal;
        if(a.Legal) return a.Seconds<b.Seconds-1e-8;
        // Infeasible trajectories only guide search toward the feasible set.
        return a.Progress>b.Progress+1e-7 || Math.Abs(a.Progress-b.Progress)<=1e-7 && a.Sweep>b.Sweep;
    }

    internal Search197 Improve(Start197 initial,FreeRacingAction[] seed,double goal,double margin,int rounds,int trials,int rngSeed,
        bool parallel=true,double sensitivity=1,ExitConstraint198? exit=null)
    {
        var timer=Stopwatch.StartNew();
        var incumbent=seed;
        var best=Evaluate(initial,incumbent,goal,margin,exit:exit);
        var history=new List<Iteration197>{new(0,1,best.Legal,best.Seconds,best.Sweep,best.Progress,0,0,best.Stop)};
        var random=new Random(rngSeed);
        int evaluated=1,noGain=0;
        double contraction=1;
        for(int round=0;round<rounds;round++)
        {
            int available=Math.Clamp((int)Math.Ceiling(best.Seconds/ReviewSeconds),12,seed.Length);
            double sweepScale=.004*sensitivity*contraction;
            double gasScale=.16*sensitivity*contraction;
            var policies=new FreeRacingAction[trials][];
            for(int i=0;i<trials;i++)
            {
                var perturbations=new Edit197[1+(i%9==0?2:0)];
                for(int j=0;j<perturbations.Length;j++)
                {
                    int center=random.Next(available);
                    int radius=random.Next(5,Math.Min(38,Math.Max(8,available/3)));
                    double steer=i%5==0?0:(random.NextDouble()*2-1)*sweepScale;
                    double gas=i%5==1?0:(random.NextDouble()*2-1)*gasScale;
                    perturbations[j]=new(center,radius,steer,gas);
                }
                policies[i]=Edit197.Apply(incumbent,perturbations);
            }
            var outcomes=new Roll197[trials];
            void Test(int i) => outcomes[i]=Evaluate(initial,policies[i],goal,margin,exit:exit);
            if(parallel) Parallel.For(0,trials,new ParallelOptions { MaxDegreeOfParallelism=6 },Test);
            else for(int i=0;i<trials;i++)Test(i);
            int winner=-1;
            for(int i=0;i<trials;i++)
                if(Better(outcomes[i],winner<0?best:outcomes[winner])) winner=i;
            if(winner>=0)
            {
                best=outcomes[winner];
                incumbent=policies[winner];
                noGain=0;
            }
            else if(++noGain>=3)
            {
                contraction=Math.Max(.16,contraction*.72);
                noGain=0;
            }
            evaluated+=trials;
            history.Add(new(round+1,evaluated,best.Legal,best.Seconds,best.Sweep,best.Progress,sweepScale,gasScale,best.Stop));
        }
        return new(incumbent,Evaluate(initial,incumbent,goal,margin,true,exit:exit),history.ToArray(),evaluated+1,timer.Elapsed.TotalSeconds);
    }
}

internal readonly record struct Edit197(int Center,int Radius,double Steer,double Gas)
{
    internal static FreeRacingAction[] Apply(FreeRacingAction[] current,Edit197[] edits)
    {
        var proposal=new FreeRacingAction[current.Length];
        double bound=TwoContactBikeParameters.Default.MaximumSteeringRadians;
        for(int i=0;i<current.Length;i++)
        {
            double steering=current[i].SteeringRadians,gas=current[i].Drive;
            foreach(var edit in edits)
            {
                double u=Math.Abs(i-edit.Center)/(double)edit.Radius;
                if(u>=1) continue;
                double weight=.5+.5*Math.Cos(Math.PI*u);
                steering+=edit.Steer*weight;
                gas+=edit.Gas*weight;
            }
            proposal[i]=new((float)Math.Clamp(steering,-bound,bound),(float)Math.Clamp(gas,0,1));
        }
        return proposal;
    }
}
