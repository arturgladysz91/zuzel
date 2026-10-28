using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
namespace Autonomous146;

public sealed record PlannerOptions146(bool Enabled=false, int RequiredLaps=3,
    double SearchIntervalSeconds=1.2, double MaximumPlanSeconds=26,
    int BeamWidth=1, int MaximumCandidates=80,
    double ClearanceReserveMeters=.10, double SwitchImprovementSeconds=.02, bool RepairUnsafePrefixes=true,bool TerminalContinuation=true,int MaximumForecastsPerReview=25000);
public sealed record Safety146(bool Legal,double MinimumGapMeters,double SweepLowerBoundMeters,
    double MaximumBetaDegrees,double MaximumYawRate,string Reason);
public sealed record Plan146(double CreatedAt,double GoalMeters,double SafetyGoalMeters,FreeRacingAction[] Actions,
    FreeRacingPoint[] Points,State146 End,double? GoalTime,double? CornerExitTime,
    double? CornerExitSpeed,Safety146 Safety,string Origin){
    public double TravelSeconds=>GoalTime is double t?t-CreatedAt:double.PositiveInfinity;
    public RiderView Terminal=>Points.FirstOrDefault(p=>p.Rider.TotalReferenceMeters>=GoalMeters)?.Rider??End.Input.Rider;
    public double AlongExitSpeed=>Vector2.Dot(Terminal.Velocity,Terminal.Road.Tangent);
}
public sealed record Error146(double PositionMeters,double SpeedMps,double BetaRadians,double YawRate,
    double WheelOmega,double CrankOmega,bool ActiveMatches,bool QueueMatches);
public sealed record Candidate146(string Origin,FreeRacingAction[] FirstActions,bool Legal,double? TravelSeconds,
    double MinimumGapMeters,double SweepLowerBoundMeters,double MaximumBetaDegrees,double MaximumYawRate,
    double ExitSpeed,double AlongExitSpeed,string Reason);
public sealed record Review146(double Time,FreeRacingInput Input,double CrankOmega,double GoalMeters,
    Error146? PredictionError,FreeRacingAction? ExpectedAction,bool FollowedExpected,string Reason,
    int Candidates,int Forecasts,long Contacts,double CpuSeconds,Plan146? Selected,
    Safety146? IndependentSafety,Candidate146[] Alternatives);

/// <summary>Research only. Endogenous K1 seed; bounded deterministic three-coordinate beam;
/// every suffix is propagated again from the accepted state with its irrevocable queue.</summary>
public sealed class Planner146
{
    public PlannerOptions146 Options {get;}
    public List<Review146> Reviews {get;}=new();
    public object? LastProgress {get;private set;}
    public long SeedDecisions {get;private set;}
    readonly Action? budgetGuard;
    readonly RiderSpec spec;readonly TrackFrame frame;readonly TrackStateSnapshot surface;
    readonly WorldParameters parameters;readonly float mass,inertia;
    readonly Model146 model;
    Plan146? previous;State146? expectedState;double nextSearch;
    int policyForecasts,policyContacts;long reviewForecastStart;
    public Planner146(RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters parameters,
        PlannerOptions146? options=null,float mass=147,float inertia=52.72097f,Action? budgetGuard=null){
        Options=options??new();if(!Options.Enabled)throw new InvalidOperationException("Study146 is OFF by default.");
        if(Options.RequiredLaps is <1 or >3||Options.BeamWidth is <1 or >3||Options.MaximumCandidates is <1 or >160
            ||!double.IsFinite(Options.SearchIntervalSeconds)||!double.IsFinite(Options.MaximumPlanSeconds)||!double.IsFinite(Options.ClearanceReserveMeters)||!double.IsFinite(Options.SwitchImprovementSeconds)
            ||Options.SearchIntervalSeconds<.2||Options.MaximumPlanSeconds is <2 or >30
            ||Options.MaximumForecastsPerReview is <32 or >40000
            ||Options.ClearanceReserveMeters<.05||Options.SwitchImprovementSeconds<.01)
            throw new ArgumentException("Bounded, explicit research planner options required.");
        this.spec=spec;this.frame=frame;this.surface=surface;this.parameters=parameters;this.mass=mass;this.inertia=inertia;
        this.budgetGuard=budgetGuard;model=new(spec,frame,surface,parameters,mass,inertia,ForecastGuard);
    }
    // Geometry supplies a common finish of the maneuver, never a lateral target or an action.
    // Stop three metres before the next bend, after the entire current bend and following straight.
    public static double Goal(TrackFrame frame,double reference,int laps){
        const double BeforeNextBendMeters=3;
        var finish=laps*frame.LapMeters;
        for(int lap=0;lap<laps;lap++)foreach(int bendExit in new[]{4,8}){
            var exit=lap*frame.LapMeters+frame.SegmentStartMeters(bendExit);
            if(exit<=reference+.001)continue;
            var nextEntry=bendExit==4?lap*frame.LapMeters+frame.SegmentStartMeters(5):(lap+1)*frame.LapMeters+frame.SegmentStartMeters(1);
            return Math.Min(finish,nextEntry-BeforeNextBendMeters);
        }
        return finish;
    }
    double ExitBefore(double goal){
        double best=0;
        for(int lap=0;lap<Options.RequiredLaps;lap++)foreach(int bendExit in new[]{4,8}){
            var value=lap*frame.LapMeters+frame.SegmentStartMeters(bendExit);if(value<goal&&value>best)best=value;
        }
        return best;
    }
    public static Safety146 Check(FreeRacingPoint[] points,double reserveMeters){
        const double FloatTubeMeters=.0005;
        double minimum=double.PositiveInfinity,certificate=double.PositiveInfinity,beta=0,yaw=0;
        var radius=SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
        foreach(var p in points){minimum=Math.Min(minimum,Math.Min(p.Inner,p.Outer));
            beta=Math.Max(beta,Math.Abs(p.Rider.SlideRadians)*180/Math.PI);yaw=Math.Max(yaw,Math.Abs(p.Rider.AngularVelocity));
            if(!double.IsFinite(p.Rider.Speed)||!double.IsFinite(p.Rider.TotalReferenceMeters)||!double.IsFinite(p.Omega))
                return new(false,minimum,double.NegativeInfinity,beta,yaw,"nonfinite state");
            if(Vector2.Dot(p.Rider.Velocity,p.Rider.Road.Tangent)<-.01)
                return new(false,minimum,double.NegativeInfinity,beta,yaw,"reverse motion");
        }
        for(int j=1;j<points.Length;j++){
            var a=points[j-1];var b=points[j];var dt=b.Time-a.Time;
            // On each unchanged held-wrench step speed/yaw maxima are bounded by endpoints.
            // Any pose is within dt/2 of an endpoint. Footprint distance is Lipschitz in translation/yaw.
            // This is independent of the time objective and uses both native boundary clearances.
            var travelBound=dt*.5*(Math.Max(a.Rider.Speed,b.Rider.Speed)+radius*Math.Max(Math.Abs(a.Rider.AngularVelocity),Math.Abs(b.Rider.AngularVelocity)));
            var lower=Math.Min(Math.Min(a.Inner,a.Outer),Math.Min(b.Inner,b.Outer))-travelBound-FloatTubeMeters;
            certificate=Math.Min(certificate,lower);
        }
        var reason=minimum<reserveMeters?"footprint reserve":certificate<0?"uncertified swept interval":beta>45?"beta45 envelope":"legal complete forecast";
        return new(minimum>=reserveMeters&&certificate>=0&&beta<=45,minimum,certificate,beta,yaw,reason);
    }
    void ForecastGuard(){
        budgetGuard?.Invoke();
        if(model.Forecasts-reviewForecastStart+policyForecasts>=Options.MaximumForecastsPerReview)
            throw new InvalidOperationException("STUDY146_BUDGET: deterministic per-review forecast cap");
    }
    FreeRacingAction SeedAction(State146 state){
        ForecastGuard();
        if(model.Forecasts-reviewForecastStart+policyForecasts+FreeLearningController.Actions(state.Input,Controller143.Policy,true).Length>Options.MaximumForecastsPerReview)
            throw new InvalidOperationException("STUDY146_BUDGET: reserve for complete K1 seed decision");
        SeedDecisions++;var d=Policy144.Choose(state.Input,new CrankClutch144(omega:state.CrankOmega),spec,frame,surface,parameters,mass,inertia,"K1");
        policyForecasts+=d.Choice.Forecasts;policyContacts+=d.Choice.ForceEvaluations;return d.Choice.Action;
    }
    Plan146 Rollout(State146 start,double goal,FreeRacingAction[]? tail,string origin){
        var state=start;var actions=new List<FreeRacingAction>();var points=new List<FreeRacingPoint>();
        var exit=ExitBefore(goal);var safetyGoal=Options.TerminalContinuation?Goal(frame,goal+.005,Options.RequiredLaps):goal;
        double? goalTime=null,exitTime=null,exitSpeed=null;
        int limit=(int)Math.Ceiling(Options.MaximumPlanSeconds/.2);
        for(int k=0;k<limit;k++){
            LastProgress=new{ReviewTime=start.Input.Time,Origin=origin,Step=k,ForecastTime=state.Input.Time,Goal=goal,SafetyGoal=safetyGoal,model.Forecasts,model.Contacts,SeedDecisions,policyForecasts};
            var action=tail is null||Options.TerminalContinuation&&k>=tail.Length?SeedAction(state):tail[Math.Min(k,tail.Length-1)];
            var transition=model.Step(state,action);actions.Add(action);
            foreach(var point in transition.Prediction.Points.Skip(k==0?0:1)){
                if(points.Count>0){var a=points[^1];var ds=point.Rider.TotalReferenceMeters-a.Rider.TotalReferenceMeters;
                    double Fraction(double reference)=>ds<=0?1:Math.Clamp((reference-a.Rider.TotalReferenceMeters)/ds,0,1);
                    if(exitTime is null&&a.Rider.TotalReferenceMeters<=exit&&point.Rider.TotalReferenceMeters>=exit){var f=Fraction(exit);exitTime=a.Time+f*(point.Time-a.Time);exitSpeed=a.Rider.Speed+f*(point.Rider.Speed-a.Rider.Speed);}
                    if(goalTime is null&&point.Rider.TotalReferenceMeters>=goal){var f=Fraction(goal);goalTime=a.Time+f*(point.Time-a.Time);}
                }
                points.Add(point);
            }
            state=transition.End;
            // Reject early after a confirmed violation, but never score a partial trajectory as an arrival.
            if(state.Input.Rider.TotalReferenceMeters>=safetyGoal&&actions.Count>=4||transition.Prediction.Points.Any(p=>Math.Min(p.Inner,p.Outer)<0||Math.Abs(p.Rider.SlideRadians)>Math.PI/4))break;
        }
        var safety=Check(points.ToArray(),Options.ClearanceReserveMeters);
        if(goalTime is null||state.Input.Rider.TotalReferenceMeters<safetyGoal)safety=safety with{Legal=false,Reason=safety.Legal?"terminal continuation not completed":safety.Reason};
        return new(start.Input.Time,goal,safetyGoal,actions.ToArray(),points.ToArray(),state,goalTime,exitTime,exitSpeed,safety,origin);
    }
    static IEnumerable<Plan146> Order(IEnumerable<Plan146> plans)=>plans.OrderBy(p=>!p.Safety.Legal)
        .ThenBy(p=>!p.Safety.Legal&&p.Safety.MaximumBetaDegrees>45)
        .ThenByDescending(p=>p.Safety.Legal?0:p.Safety.MinimumGapMeters)
        .ThenBy(p=>p.TravelSeconds).ThenByDescending(p=>p.AlongExitSpeed).ThenBy(p=>Math.Abs(p.Terminal.SlideRadians))
        .ThenBy(p=>Math.Abs(p.Terminal.AngularVelocity));
    static Candidate146 Summary(Plan146 p)=>new(p.Origin,p.Actions.Take(3).ToArray(),p.Safety.Legal,p.GoalTime is null?null:p.TravelSeconds,
        p.Safety.MinimumGapMeters,p.Safety.SweepLowerBoundMeters,p.Safety.MaximumBetaDegrees,p.Safety.MaximumYawRate,
        p.CornerExitSpeed??p.End.Input.Rider.Speed,p.AlongExitSpeed,p.Safety.Reason);
    static bool SameQueue(FreeRacingCommand[] a,FreeRacingCommand[] b)=>a.Length==b.Length&&a.Zip(b).All(q=>q.First.Action==q.Second.Action&&Math.Abs(q.First.ActivationAt-q.Second.ActivationAt)<1e-7&&Math.Abs(q.First.ObservedAt-q.Second.ObservedAt)<1e-7);
    public Decision144 Choose(FreeRacingInput input,CrankClutch144 crank){
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var forecasts=model.Forecasts;var contacts=model.Contacts;
        policyContacts=policyForecasts=0;reviewForecastStart=model.Forecasts;
        var start=new State146(input with{NextReview=input.Time},crank.Omega);var goal=previous is not null&&previous.GoalMeters>input.Rider.TotalReferenceMeters+.001?previous.GoalMeters:Goal(frame,input.Rider.TotalReferenceMeters,Options.RequiredLaps);
        Error146? error=null;
        if(expectedState is not null){var expected=expectedState.Input;error=new(Vector2.Distance(expected.Rider.Position,input.Rider.Position),
            Math.Abs(expected.Rider.Speed-input.Rider.Speed),Math.Abs(BikeAngles.Wrap(expected.Rider.SlideRadians-input.Rider.SlideRadians)),
            Math.Abs(expected.Rider.AngularVelocity-input.Rider.AngularVelocity),Math.Abs(expected.Omega-input.Omega),
            Math.Abs(expectedState.CrankOmega-crank.Omega),expected.Active==input.Active,SameQueue(expected.Pending,input.Pending));}
        FreeRacingAction? expectedAction=null;Plan146? incumbent=null;
        if(previous is not null&&(Options.TerminalContinuation||Math.Abs(previous.GoalMeters-goal)<1e-7)){
            int shift=(int)Math.Round((input.Time-previous.CreatedAt)/.2);
            if(shift<previous.Actions.Length){var suffix=previous.Actions.Skip(shift).ToArray();expectedAction=suffix[0];incumbent=Rollout(start,goal,suffix,"shifted-incumbent");}
        }
        var candidates=new List<Plan146>();if(incumbent is not null)candidates.Add(incumbent);
        var reason=expectedAction is null?"new endogenous maneuver":"retained legal suffix";
        bool modelMismatch=error is not null&&(error.PositionMeters>.03||error.YawRate>.01||!error.ActiveMatches||!error.QueueMatches);
        bool search=incumbent is null||!incumbent.Safety.Legal||modelMismatch||input.Time>=nextSearch-1e-8;
        if(search){
            if(incumbent is null||!incumbent.Safety.Legal||modelMismatch){var seed=Rollout(start,goal,null,"fresh-K1-seed");candidates.Add(seed);incumbent=Order(candidates.Where(p=>p.Safety.Legal)).FirstOrDefault()??seed;}
            var roots=new[]{incumbent};
            for(int depth=0;depth<3&&candidates.Count<Options.MaximumCandidates;depth++){
                var layer=new List<Plan146>();
                foreach(var root in roots){
                    var state=start;for(int j=0;j<depth;j++)state=model.Step(state,root.Actions[Math.Min(j,root.Actions.Length-1)]).End;
                    var grid=FreeLearningController.Actions(state.Input,Controller143.Policy,true);
                    // Third correction uses neighbouring grid angles and all throttle choices; no coordinates.
                    if(depth==2){var current=root.Actions[Math.Min(depth,root.Actions.Length-1)].SteeringRadians;
                        var angles=grid.Select(a=>a.SteeringRadians).Distinct().OrderBy(a=>Math.Abs(a-current)).Take(3).ToHashSet();grid=grid.Where(a=>angles.Contains(a.SteeringRadians)).ToArray();}
                    foreach(var action in grid){
                        if(candidates.Count>=Options.MaximumCandidates)break;
                        var changed=root.Actions.ToArray();if(changed.Length<=depth)continue;changed[depth]=action;
                        var plan=Rollout(start,goal,changed,$"beam-{depth+1}");layer.Add(plan);candidates.Add(plan);
                    }
                }
                // Intermediate nodes may need a later correction. Only complete legal plans reach execution.
                roots=Order(Options.RepairUnsafePrefixes?layer:layer.Where(p=>p.Safety.Legal)).Take(Options.BeamWidth).ToArray();if(roots.Length==0)break;
            }
            nextSearch=input.Time+Options.SearchIntervalSeconds;
        }
        var best=Order(candidates.Where(p=>p.Safety.Legal)).FirstOrDefault();Plan146? selected=null;
        if(incumbent is not null&&incumbent.Safety.Legal){
            selected=incumbent;
            if(best is not null&&best.TravelSeconds+Options.SwitchImprovementSeconds<incumbent.TravelSeconds){selected=best;reason="common-goal time improvement";}
            else if(modelMismatch)reason="state mismatch; suffix revalidated";
        }else if(best is not null){selected=best;reason=expectedAction is null?"new endogenous maneuver":"unsafe suffix replaced";}
        else reason="abstain: no certified complete continuation";
        Safety146? independent=null;
        if(selected is not null){
            var state=start;var preflight=new List<FreeRacingPoint>();
            // Independent replay of the next four reviews, with current accepted queue, no scoring.
            for(int k=0;k<4;k++){var t=model.Step(state,selected.Actions[Math.Min(k,selected.Actions.Length-1)]);state=t.End;preflight.AddRange(t.Prediction.Points.Skip(k==0?0:1));}
            independent=Check(preflight.ToArray(),Options.ClearanceReserveMeters);
            if(!independent.Legal){reason="abstain: independent preflight "+independent.Reason;selected=null;}
        }
        if(selected is not null){previous=selected;expectedState=model.Step(start,selected.Actions[0]).End;}
        var elapsed=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
        int forecastCount=checked((int)(model.Forecasts-forecasts)+policyForecasts);long contactCount=model.Contacts-contacts+policyContacts;
        // Keep review logs compact while retaining every selected action and predicted native interval.
        Reviews.Add(new(input.Time,input,crank.Omega,goal,error,expectedAction,selected is not null&&expectedAction==selected.Actions[0],reason,
            candidates.Count,forecastCount,contactCount,elapsed,selected,independent,candidates.Select(Summary).ToArray()));
        if(selected is null)throw new InvalidOperationException("STUDY146_ABSTAIN: "+reason);
        var a=selected.Actions[0];
        return new(new(input.Time,a,FreeActionSafety.SafeInHorizon,false,Array.Empty<FreeLearningCandidate>(),checked((int)contactCount),forecastCount),elapsed);
    }
}
