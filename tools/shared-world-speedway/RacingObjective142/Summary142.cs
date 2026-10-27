using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;

public static class Summary142
{
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object o)=>JsonSerializer.SerializeToNode(o,Json)!;
    public static JsonObject Native(NativeCaptureResult142 capture,NativeCase139 c,string mode){
        var e=capture.Episode;var s=e.Replay.Samples;var m=e.Replay.Metrics;var frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);
        var ledger=Node(NativeSummary140.Build(new(e,capture.HistoricalCompleted,capture.HistoricalGateSeconds,capture.NativeContacts,capture.BoundaryContacts),c,mode,false,null,null,false));
        object? At(double reference){
            var index=Array.FindIndex(s,x=>x.Input.Rider.TotalReferenceMeters>=reference);if(index<0)return null;
            var b=s[index];var a=s[Math.Max(0,index-1)];var dr=b.Input.Rider.TotalReferenceMeters-a.Input.Rider.TotalReferenceMeters;
            var fraction=dr>0?Math.Clamp((reference-a.Input.Rider.TotalReferenceMeters)/dr,0,1):1;
            double L(double x,double y)=>x+(y-x)*fraction;
            return new{TargetReferenceMeters=reference,SpeedKph=L(a.Input.Rider.Speed,b.Input.Rider.Speed)*3.6,TimeSeconds=L(a.Input.Time,b.Input.Time),
                PhysicalDistanceMeters=L(a.Input.Rider.DistanceMeters,b.Input.Rider.DistanceMeters),
                Before=new{Time=a.Input.Time,Reference=a.Input.Rider.TotalReferenceMeters,SpeedKph=a.Input.Rider.Speed*3.6},
                After=new{Time=b.Input.Time,Reference=b.Input.Rider.TotalReferenceMeters,SpeedKph=b.Input.Rider.Speed*3.6},
                Meaning="Scalar metric interpolation between adjacent accepted native observations at the SAME reference crossing; no new state, forecast or policy input."};
        }
        var exit=SpeedThrottleAudit136.Exit(frame,c.Corner);var finish=Node(At(capture.StopReference)??new{Unavailable=true});
        var controllerStop=m.Outcome.StartsWith("controller forecast",StringComparison.Ordinal);var budget=m.Outcome.Contains("CPU",StringComparison.Ordinal);
        var dnf=m.Outcome is not "completed" and not "probe"&&!controllerStop&&!budget;
        var next=s.Where(x=>x.Input.Rider.TotalReferenceMeters>=exit+62&&x.Input.Rider.TotalReferenceMeters<=capture.NextCornerEndReference).ToArray();
        var straight=s.Zip(s.Skip(1),(a,b)=>new{a,b}).Where(x=>x.a.Input.Rider.TotalReferenceMeters>=exit&&x.a.Input.Rider.TotalReferenceMeters<exit+62).ToArray();
        var duration=straight.Sum(x=>x.b.Input.Time-x.a.Input.Time);
        var numerical=ledger["EnergyLedgerPassed"]!.GetValue<bool>()&&ledger["MaximumYawUpdateErrorRadiansPerSecond"]!.GetValue<double>()<=2e-5
            &&ledger["MaximumReactionErrorSeconds"]!.GetValue<double>()<1e-8&&ledger["MaximumReviewIntervalErrorSeconds"]!.GetValue<double>()<1e-8
            &&m.Outcome is not "energy invariant" and not "actuator invariant";
        return new JsonObject{
            ["Name"]=e.Replay.Name,["Case"]=Node(c),["Mode"]=mode,["Outcome"]=m.Outcome,["Completed"]=m.Completed,["PhysicalDnf"]=dnf,
            ["FailureClassification"]=m.Outcome switch{"uncertified interval"=>"native swept-boundary certificate failed; not a confirmed point-footprint crossing","confirmed crossing"=>"native footprint boundary crossing","control envelope"=>"frozen experimental beta45 envelope; not a calibrated real-motorcycle limit",_=>m.Outcome},
            ["ControllerForecastStop"]=controllerStop,["BudgetStop"]=budget,["Initial"]=Node(s[0].Input),
            ["ActualConstructorQueue"]=Node(new{Pending=Array.Empty<FreeRacingCommand>(),Active=new FreeRacingAction(0,1),Meaning="Recorded sample0 includes pending decision0 annotation."}),
            ["NativeMetrics"]=Node(m),["RobustMetrics"]=Node(e.Metrics),["HistoricalCompleted"]=capture.HistoricalCompleted,["HistoricalGateSeconds"]=capture.HistoricalGateSeconds,
            ["Scope"]=Node(new{StudiedExitReference=exit,NextBendStartReference=exit+62,capture.NextCornerEndReference,capture.StopReference,ContinuousNativeWorld=true}),
            ["Exit"]=At(exit) is{} x0?Node(x0):null,["Plus20"]=At(exit+20) is{} x1?Node(x1):null,["Plus40"]=At(exit+40) is{} x2?Node(x2):null,["Plus62"]=At(exit+62) is{} x3?Node(x3):null,
            ["FullTimeSeconds"]=m.Completed?finish["TimeSeconds"]!.DeepClone():null,["NativeEndTimeSeconds"]=m.DurationSeconds,
            ["ExtraPhysicalDistanceOverReferenceMeters"]=s[^1].Input.Rider.DistanceMeters-s[0].Input.Rider.DistanceMeters-(s[^1].Input.Rider.TotalReferenceMeters-s[0].Input.Rider.TotalReferenceMeters),
            ["FullPhysicalDistanceMeters"]=m.Completed?finish["PhysicalDistanceMeters"]!.DeepClone():null,
            ["StudiedStraight"]=Node(new{ObservedSeconds=duration,FullGasFraction=duration>0?straight.Where(x=>x.a.Input.Active.Drive>=.99).Sum(x=>x.b.Input.Time-x.a.Input.Time)/duration:(double?)null,MeanGas=duration>0?straight.Sum(x=>x.a.Input.Active.Drive*(x.b.Input.Time-x.a.Input.Time))/duration:(double?)null,Complete=s[^1].Input.Rider.TotalReferenceMeters>=exit+62}),
            ["NextBend"]=Node(new{Entered=next.Length>0,Completed=s[^1].Input.Rider.TotalReferenceMeters>=capture.NextCornerEndReference,MinimumSpeedKph=next.Length>0?next.Min(x=>x.Input.Rider.Speed)*3.6:(double?)null,ObservedSeconds=next.Length>1?next[^1].Input.Time-next[0].Input.Time:0}),
            ["LastAccepted"]=Node(new{Time=s[^1].Input.Time,Reference=s[^1].Input.Rider.TotalReferenceMeters,SpeedKph=s[^1].Input.Rider.Speed*3.6,BetaDegrees=s[^1].Input.Rider.SlideRadians*180/Math.PI,Yaw=s[^1].Input.Rider.AngularVelocity,s[^1].Inner,s[^1].Outer,s[^1].Input.Active,s[^1].Input.Pending}),
            ["NumericalClockGatesPassed"]=numerical,["EnergyLedgerMaximumResidualJoules"]=ledger["MaximumEnergyLedgerResidualJoules"]!.DeepClone(),
            ["EnergyLedgerMaximumToleranceRatio"]=ledger["MaximumEnergyLedgerToleranceRatio"]!.DeepClone(),["YawUpdateMaximumErrorRadiansPerSecond"]=ledger["MaximumYawUpdateErrorRadiansPerSecond"]!.DeepClone(),
            ["ReactionMaximumErrorSeconds"]=ledger["MaximumReactionErrorSeconds"]!.DeepClone(),["ReviewMaximumIntervalErrorSeconds"]=ledger["MaximumReviewIntervalErrorSeconds"]!.DeepClone()
        };
    }
    public static JsonNode Compare(JsonObject r){
        var native=(JsonArray)r["Native"]!;JsonNode? Find(string name)=>native.FirstOrDefault(x=>x!["Name"]!.GetValue<string>()==name);
        double? Speed(JsonNode? row,string point)=>row?[point]?["SpeedKph"]?.GetValue<double>();
        var pairs=new List<object>();var matrix=new List<object>();
        foreach(var c in PhysicsExperiment139.Cases()){
            var a=Find(c.Name+"-A");foreach(var mode in new[]{"A","B","C"}){
                var x=Find(c.Name+"-"+mode);matrix.Add(new{c.Name,Mode=mode,Status=x?["Outcome"]?.GetValue<string>()??"not run",Completed=x?["Completed"]?.GetValue<bool>(),
                    NativeDnf=x?["PhysicalDnf"]?.GetValue<bool>(),ControllerForecastStop=x?["ControllerForecastStop"]?.GetValue<bool>(),TimeSeconds=x?["FullTimeSeconds"]?.GetValue<double>(),
                    ExitKph=Speed(x,"Exit"),Plus20Kph=Speed(x,"Plus20"),Plus40Kph=Speed(x,"Plus40"),Plus62Kph=Speed(x,"Plus62"),
                    MeanGas=x?["NativeMetrics"]?["MeanDrive"]?.GetValue<double>(),FullGasFraction=x?["NativeMetrics"]?["FullDriveFraction"]?.GetValue<double>(),
                    MaxBeta=x?["NativeMetrics"]?["MaximumBetaDegrees"]?.GetValue<double>(),MinClearance=x?["NativeMetrics"]?["MinimumClearanceMeters"]?.GetValue<double>()});
                if(mode=="A")continue;
                var both=a?["Completed"]?.GetValue<bool>()==true&&x?["Completed"]?.GetValue<bool>()==true;
                pairs.Add(new{c.Name,Mode=mode,BothComplete=both,FullTimeGainSeconds=both?a!["FullTimeSeconds"]!.GetValue<double>()-x!["FullTimeSeconds"]!.GetValue<double>():(double?)null,
                    Plus40GainKph=Speed(x,"Plus40")-Speed(a,"Plus40"),PhysicalDistanceDifferenceMeters=both?x!["FullPhysicalDistanceMeters"]!.GetValue<double>()-a!["FullPhysicalDistanceMeters"]!.GetValue<double>():(double?)null});
            }
        }
        var failures=new List<object>();foreach(var x in native.Where(x=>x!["PhysicalDnf"]!.GetValue<bool>()||x["ControllerForecastStop"]!.GetValue<bool>())){
            if(x!["ReusedTerminalPreflight"] is not null)continue;var ds=(JsonArray)x["Decisions"]!;var end=x["NativeEndTimeSeconds"]!.GetValue<double>();
            var noHard=ds.FirstOrDefault(d=>d!["HardFeasibleCount"]!.GetValue<int>()==0);var last=ds.LastOrDefault();
            failures.Add(new{Name=x["Name"]!.GetValue<string>(),Outcome=x["Outcome"]!.GetValue<string>(),Classification=x["FailureClassification"]!.GetValue<string>(),
                EndTimeSeconds=end,FirstNoHardFeasibleTimeSeconds=noHard?["Time"]?.GetValue<double>(),
                TimeFromNoHardForecastToNativeStopSeconds=noHard is null?(double?)null:end-noHard["Time"]!.GetValue<double>(),
                LastRequestActivationAfterStop=last is not null&&last["Time"]!.GetValue<double>()+.24>end,
                LastDecisions=ds.TakeLast(5).Select(d=>d!.DeepClone()).ToArray(),
                Meaning="All-infeasible H09 means no admissible recovery in this finite held-action set, not global impossibility. Late-switch native rows test limited causal alternatives; future native observations never select actions. Uncertain alone is never called DNF."});
        }
        return Node(new{Matrix=matrix,Pairs=pairs,Failures=failures,NativeDnfWorlds=native.Count(x=>x!["PhysicalDnf"]!.GetValue<bool>()&&x["ReusedTerminalPreflight"] is null),
            ControllerForecastStops=native.Count(x=>x!["ControllerForecastStop"]!.GetValue<bool>()&&x["ReusedTerminalPreflight"] is null),
            Interpretation="Each physical family has one A/B/C scope. Reused terminal preflights and original branch counterparts are aliases, not independent trials. Same-reference scalar crossings, not equal-time speed comparison. No speed before later failure is a successful legal time."});
    }
}
