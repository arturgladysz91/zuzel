using System.Diagnostics;
using System.Numerics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record EquilibriumProbe205(double[] Residual,Shot205 Shot,HandlingStep Contact,ClutchStep144 Power,
        double Tangential,double Normal,double Kappa);

    private static void Equilibria205(string repository,string output)
    {
        var archive=Read199<Archive204>(Path.Combine(repository,NativeSource205));
        var run=archive.Run.Riders.Single();
        var first=Map199.At(0,0);
        int sample=Enumerable.Range(0,run.Samples.Length).MinBy(i=>Math.Abs(run.Samples[i].Input.Rider.TotalReferenceMeters-
            (first.Entry+first.Exit)/2));
        var seed=NativeShot205(archive,sample,"quasi-steady nominal apex");
        var rows=new List<object>();
        var stopwatch=Stopwatch.StartNew();
        foreach(string variant in Physics205)
        foreach(double radius in new[]{32d,38d,44d})
        foreach(double speedKph in new[]{40d,60d,80d,100d,120d})
        {
            double v=speedKph/3.6;
            double curvatureAcceleration=v*v/radius;
            var surface=new Probe203(Settings202(variant),seed.Spec,seed.Start.State,seed.Start.Active,seed.Dt).Grip;
            double ceiling=LoadBasedTyreGrip.Budget(Mass,surface,TwoContactBikeParameters.Default,Physics.LoadBasedTyres).
                EffectiveFrictionCoefficient*Settings202(variant).GripScale*9.81;
            if(curvatureAcceleration>ceiling)
            {
                rows.Add(new {Variant=variant,Radius=radius,SpeedKph=speedKph,Status="ExcludedByTotalLateralBudget",
                    NormalAcceleration=curvatureAcceleration,Ceiling=ceiling});
                continue;
            }
            double[]? continuation=null;
            foreach(float gas in new[]{0f,.25f,.5f,.75f,1f})
            {
                EquilibriumProbe205? At(double[] x)
                {
                    var bike=TwoContactBikeParameters.Default;
                    double limit=bike.MaximumSteeringRadians;
                    if(x.Any(y=>!double.IsFinite(y))||Math.Abs(x[0])>=.65||Math.Abs(x[1])>limit||Math.Abs(x[2])>limit||
                        x[3]<=0||x[4]<=0) return null;
                    double course=seed.Start.State.Rider.Road.Heading;
                    double heading=course+x[0], wheel=x[3]*v/.33, crank=x[4]*8*v/.33;
                    double yaw=v/radius;
                    var p=seed.Start.State;
                    var velocity=new Vector2((float)(v*Math.Cos(course)),(float)(v*Math.Sin(course)));
                    var state=p with {Rider=p.Rider with {Velocity=velocity,Heading=(float)heading,AngularVelocity=(float)yaw},
                        Wheel=wheel,Shaft=crank,Steer=(float)x[2]};
                    var shot=seed with {Dt=.01f,Start=seed.Start with {State=state,Active=new((float)x[1],gas),Pending=[],
                        Review=state.Time}};
                    try
                    {
                        var q=Query205(shot,variant);
                        var step=q.Query.Solve(q.Lo,q.Hi,q.Posture);
                        var drive=q.Shaft.Prepared;
                        var along=Vector2.Normalize(velocity);
                        double aT=Vector2.Dot(step.Forces.NetForce,along)/Mass;
                        double aN=Vector2.Dot(step.Forces.NetForce,new(-along.Y,along.X))/Mass;
                        double u=v*Math.Cos(x[0]);
                        double signedSlip=drive.Wheel.InstantSlipRatio;
                        double wheelRate=aT*Math.Cos(x[0])*(1+signedSlip*u/Math.Sqrt(u*u+.25))/.33;
                        double shaftRate=drive.Engine.OverallRatio*wheelRate;
                        var residual=new[] {(aN-curvatureAcceleration)/10,(step.Forces.NetTorque/Inertia-aT/radius)/10,
                            step.Forces.SteeringTargetBeforeRounding!.Value-state.Steer,
                            ((drive.Wheel.AfterOmega-wheel)/shot.Dt-wheelRate)/100,
                            ((drive.AfterOmega-crank)/shot.Dt-shaftRate)/500};
                        return new(residual,shot,step,drive,aT,aN,signedSlip);
                    }
                    catch(InvalidOperationException){return null;}
                    catch(ArgumentException){return null;}
                }
                double request=Math.Atan(1.3/radius);
                var starts=new List<double[]> {new[] {.05,request,.05,1.02,1.02},new[] {.15,request,-.03,1.13,1.13},
                    new[] {-.03,request,.09,.98,.98}};
                if(continuation is not null) starts.Insert(0,continuation);
                EquilibriumProbe205? best=null;
                double bestNorm=double.PositiveInfinity;
                const double accept=.00025;
                foreach(var initial in starts)
                {
                    double[] x=initial.ToArray();
                    for(int iteration=0;iteration<28;iteration++)
                    {
                        var p=At(x);
                        if(p is null) break;
                        double error=Norm205(p.Residual);
                        if(error<bestNorm){best=p;bestNorm=error;continuation=x;}
                        if(error<accept) break;
                        var derivative=new double[5,5];
                        bool valid=true;
                        const double epsilon=.0002;
                        for(int column=0;column<5;column++)
                        {
                            var plus=x.ToArray();plus[column]+=epsilon;
                            var minus=x.ToArray();minus[column]-=epsilon;
                            var upper=At(plus);var lower=At(minus);
                            if(upper is null||lower is null){valid=false;break;}
                            for(int row=0;row<5;row++) derivative[row,column]=(upper.Residual[row]-lower.Residual[row])/(2*epsilon);
                        }
                        if(!valid||Direction205(derivative,p.Residual) is not {} direction) break;
                        bool improved=false;
                        for(int trial=0;trial<9;trial++)
                        {
                            double scale=Math.Pow(.5,trial);
                            var candidate=x.Select((y,j)=>y+scale*direction[j]).ToArray();
                            if(At(candidate) is not {} next||Norm205(next.Residual)>=error) continue;
                            x=candidate;
                            improved=true;
                            break;
                        }
                        if(!improved) break;
                    }
                    if(bestNorm<accept) break;
                }
                if(best is null)
                {
                    rows.Add(new {Variant=variant,Radius=radius,SpeedKph=speedKph,Gas=gas,Status="NoCandidateFound"});
                    continue;
                }
                bool converged=bestNorm<accept;
                object? subsequent=null;
                if(converged)
                {
                    // Free integration, with the same actuator, held request and delayed review.
                    var predictor=new Predictor198(Frame,Surface,Physics,Settings202(variant),Timing,best.Shot.Spec,Mass,Inertia);
                    var checkpoint=best.Shot.Start;
                    var cursor=predictor.Begin(checkpoint,Enumerable.Repeat(checkpoint.Active,12).ToArray(),0,double.PositiveInfinity,.5);
                    cursor.Advance(100,checkpoint.State.Time+.20);
                    subsequent=new {cursor.Outcome,End=cursor.Snapshot,cursor.Sweep};
                }
                var f=best.Contact.Forces;
                var state=best.Shot.Start.State;
                rows.Add(new {Variant=variant,Radius=radius,SpeedKph=speedKph,Gas=gas,
                    Status=converged?"QuasiSteadyCandidate":"ResidualAboveDiagnosticTolerance",
                    ScaledNorm=bestNorm,best.Residual,state.Rider.Heading,Direction=state.Rider.Road.Heading,
                    Beta=state.Rider.SlideRadians,Yaw=state.Rider.AngularVelocity,Steer=state.Steer,
                    SteeringRequest=best.Shot.Start.Active.SteeringRadians,state.Wheel,state.Shaft,
                    best.Power.Engine.EngineRpm,best.Kappa,best.Tangential,best.Normal,NetYaw=f.NetTorque,
                    f.FrontLateral,f.RearLongitudinal,f.RearLateral,f.TyreLoads,
                    best.Power.Wheel.LongitudinalLossJoules,best.Contact.LoadH26!.Precision!.Combined,Subsequent=subsequent});
            }
        }
        Save(Path.Combine(output,"equilibria-model.json"),new {Cases=rows,ElapsedSeconds=stopwatch.Elapsed.TotalSeconds,
            NativeWorlds=0});
    }

    private static double Norm205(double[] values)=>Math.Sqrt(values.Sum(x=>x*x));

    private static double[]? Direction205(double[,] a,double[] residual)
    {
        int n=residual.Length;
        var rhs=residual.Select(x=>-x).ToArray();
        for(int j=0;j<n;j++)
        {
            int p=j;
            for(int i=j+1;i<n;i++) if(Math.Abs(a[i,j])>Math.Abs(a[p,j])) p=i;
            if(Math.Abs(a[p,j])<1e-9) return null;
            for(int i=j;i<n;i++) (a[p,i],a[j,i])=(a[j,i],a[p,i]);
            (rhs[p],rhs[j])=(rhs[j],rhs[p]);
            for(int i=j+1;i<n;i++)
            {
                double factor=a[i,j]/a[j,j];
                for(int k=j+1;k<n;k++) a[i,k]-=factor*a[j,k];
                rhs[i]-=factor*rhs[j];
            }
        }
        for(int i=n-1;i>=0;i--)
        {
            for(int j=i+1;j<n;j++) rhs[i]-=a[i,j]*rhs[j];
            rhs[i]/=a[i,i];
        }
        return rhs;
    }
}
