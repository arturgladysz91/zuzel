using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreSim;
namespace SharedWorldSpeedway;
public sealed record Cache142(YawState137 State,YawSearch137 Search);
public static class Offline142
{
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object o)=>JsonSerializer.SerializeToNode(o,Json)!;
    public static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.Replace("\r\n","\n"))));
    static T Read<T>(string path,Dictionary<string,string> sources){
        var bytes=File.ReadAllBytes(path);sources[path]=Convert.ToHexString(SHA256.HashData(bytes));
        if(path.EndsWith(".gz",StringComparison.Ordinal)){using var s=new GZipStream(new MemoryStream(bytes),CompressionMode.Decompress);return JsonSerializer.Deserialize<T>(s,Json)!;}
        return JsonSerializer.Deserialize<T>(bytes,Json)!;
    }
    public static object View(Leaf142? l)=>l is null?new{HasCandidate=false}:(object)new{HasCandidate=true,NominalHardAdmissible=l.HardViolation is null,l.Candidate.Action,l.Candidate.Safety,l.Candidate.Score,
        l.Candidate.Progress,SpeedKph=l.Candidate.ExitSpeed*3.6,EndBetaRadians=l.EndYawRate is null?(double?)null:l.Candidate.ExitBeta,
        l.EndYawRate,l.MaximumBetaDegrees,l.MaximumYawRate,l.Candidate.Clearance,l.Candidate.FutureClearance,l.HardViolation,l.Terms};
    public static JsonObject Run(){
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var sources=new Dictionary<string,string>();var states=new JsonArray();var seen=new HashSet<string>();
        var branchOptions=new List<(YawState137 State,string Mode,Leaf142 Original,Leaf142 Alternative)>();
        void Add(string source,string name,AwarenessCase134 c,FreeRacingInput input,Leaf142[] leaves,object? awareness=null){
            var identity=Hash(c.Geometry+JsonSerializer.Serialize(input,Json));var duplicate=!seen.Add(identity);var original=Objective142.Select(leaves,input,"A")!;
            var choices=new JsonObject();foreach(var mode in new[]{"A","B","C"}.Concat(Objective142.Single)){
                var selected=Objective142.Select(leaves,input,mode);var view=Node(View(selected));view["Changed"]=selected?.Candidate.Action!=original.Candidate.Action;
                view["HigherGas"]=selected is not null&&selected.Candidate.Action.Drive>original.Candidate.Action.Drive+1e-6;
                view["RemovedTerm"]=mode.StartsWith("no-",StringComparison.Ordinal)?mode:null;choices[mode]=view;
            }
            states.Add(Node(new{Source=source,Name=name,Case=c,Input=input,Identity=identity,DuplicateOfEarlier=duplicate,
                HistoricalInference="Same cached physical forecasts; no new predictor/world. No native-time causality inferred from reranking.",Choices=choices,
                Candidates=leaves.Select(View).ToArray(),TrackAwarenessCounterfactual=awareness}));
        }
        foreach(var file in Directory.GetFiles("docs/experiments/causal-yaw-throttle","offline-*.json.gz").Order()){
            var cache=Read<Cache142>(file,sources);var s=cache.State;var f=TrackAwarenessExperiment134.Frame(s.Case.Geometry);var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();
            var leaves=cache.Search.Held.Select(x=>Objective142.FromPrediction(s.Input,PhysicsExperiment139.Policy,f,surface,x.Prediction,x.Actions[0])).ToArray();
            for(int j=0;j<leaves.Length;j++)if(Math.Abs(leaves[j].Candidate.Score-cache.Search.Held[j].Evaluation.Score)>1e-8)throw new InvalidOperationException("Cached original score differs.");
            var ta=new List<Leaf142>();
            for(int j=0;j<leaves.Length;j++){
                var leaf=leaves[j];var pred=cache.Search.Held[j].Prediction;var v=pred.Points[^1].Rider;
                var coast=cache.Search.Held.First(x=>x.Actions[0].Drive==0&&x.Actions[0].SteeringRadians==leaf.Candidate.Action.SteeringRadians).Prediction;
                var lane=Math.Clamp((float)v.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(f.Track.Segments[v.Road.Segment].Type,f.Track.Geometry)*4,0,4);
                var capacity=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(v.Road.Segment,lane),TwoContactBikeParameters.Default).TotalCapacityNewtons/147;
                var estimate=TrackAwareness134.Evaluate(s.Input,PhysicsExperiment139.Policy,f,p,leaf.Candidate,pred,coast,capacity,leaf.Terms.Geometry);
                ta.Add(leaf with{Candidate=leaf.Candidate with{Score=leaf.Candidate.Score+estimate.ScoreDelta},Terms=leaf.Terms with{TrackAwareness=estimate.ScoreDelta}});
            }
            var old=Objective142.Select(leaves,s.Input,"A")!;var withTa=Objective142.Select(ta,s.Input,"A")!;
            var withoutTa=Objective142.Select(ta,s.Input,"no-track-awareness")!;
            var awareness=new{EnabledInOriginalA=false,EnabledChoice=View(withTa),RemovedChoice=View(withoutTa),
                Changed=withTa.Candidate.Action!=withoutTa.Candidate.Action,Meaning="134 Track Awareness REPLACES geometry cost by a profile cost when applied; ScoreDelta can be negative. This counterfactual uses cached same-angle coast forecasts, no new dynamics."};
            Add("137-full-held",s.Name,s.Case,s.Input,leaves,awareness);
            foreach(var mode in Objective142.Single.Where(x=>x is not "no-track-awareness" and not "no-clearance" and not "no-unsafe-score")){
                var alt=Objective142.Select(leaves,s.Input,mode)!;
                if(alt.Candidate.Action!=old.Candidate.Action&&alt.Candidate.Action.Drive>old.Candidate.Action.Drive+1e-6
                    &&alt.Candidate.Safety==old.Candidate.Safety&&alt.HardViolation is null)branchOptions.Add((s,mode,old,alt));
            }
        }
        var costRows=Read<JsonElement[]>("docs/experiments/speed-throttle-root-cause/costs.json",sources);
        var oldStates=Read<ThrottleState136[]>("docs/experiments/speed-throttle-root-cause/states.json",sources);
        foreach(var s in oldStates){
            var c=SpeedThrottleAudit136.Diagnostic.Single(x=>x.Name==s.Case);
            var matched=costRows.Where(x=>x.GetProperty("Case").GetString()==s.Case&&x.GetProperty("Corner").GetInt32()==s.Corner&&x.GetProperty("Distance").GetDouble()==s.Distance).ToArray();
            var leaves=matched.Select(x=>{
                double V(string k)=>x.GetProperty(k).GetDouble();var action=x.GetProperty("Action").Deserialize<FreeRacingAction>(Json);
                var safety=Enum.Parse<FreeActionSafety>(x.GetProperty("Safety").GetString()!);
                var candidate=new FreeLearningCandidate(action,safety,V("Score"),V("Progress"),V("Clearance"),V("FutureClearance"),V("SpeedKph")/3.6,0,0);
                var terms=new Terms142(V("ProgressCost"),V("DirectionCost"),V("YawCost"),V("ThrottleCost"),V("SteeringCost"),V("GeometryCost"),V("ClearanceCost"),V("UnsafeCost"));
                if(Math.Abs(terms.Sum-candidate.Score)>1e-8)throw new InvalidOperationException("136 component sum differs.");
                var hard=V("Clearance")<0?"predicted footprint boundary crossing":V("MaxBetaDegrees")>45?"predicted experimental beta45 envelope":null;
                return new Leaf142(candidate,terms,hard,V("MaxBetaDegrees"),V("MaxYawRate"),null);
            }).ToArray();
            var selected=Objective142.Select(leaves,s.Input,"A")!;var saved=matched.Single(x=>x.GetProperty("Chosen").GetBoolean()).GetProperty("Action").Deserialize<FreeRacingAction>(Json);
            if(selected.Candidate.Action!=saved)throw new InvalidOperationException("136 original ranking differs.");
            Add("136-saved-costs",$"{s.Case}-bend{s.Corner}-exit{s.Distance}",c,s.Input,leaves);
        }
        // Two changed single-penalty decisions, one straight and one bend, distinct physical starts.
        var branches=new JsonArray();foreach(var family in new[]{"D-B","D-C"}){
            var options=branchOptions.Where(x=>x.State.Case.Name==family);
            var preferred=options.Where(x=>family=="D-C"?x.State.Phase=="straight+10":x.State.Phase.StartsWith("exit1",StringComparison.Ordinal));
            if(preferred.Any())options=preferred;
            var b=options.OrderByDescending(x=>x.Alternative.Candidate.Progress-x.Original.Candidate.Progress).ThenBy(x=>x.State.Name).ThenBy(x=>x.Mode).First();
            branches.Add(Node(new{b.State,b.Mode,Original=View(b.Original),Alternative=View(b.Alternative),
                Corner=b.State.Input.Rider.TotalReferenceMeters>=TrackAwarenessExperiment134.Frame(b.State.Case.Geometry).SegmentStartMeters(5)?2:1,SelectionRule="Different D-B/D-C physical starts; prefer D-B bend exit (otherwise any available phase), D-C straight; maximum cached progress gain among changed higher-gas same-class admissible single-penalty removals. Fixed before native worlds."}));
        }
        var counts=new JsonObject();foreach(var mode in new[]{"A","B","C"}.Concat(Objective142.Single)){
            var unique=states.Where(x=>!x!["DuplicateOfEarlier"]!.GetValue<bool>()).ToArray();var choices=unique.Select(x=>x!["Choices"]![mode]!).ToArray();
            counts[mode]=Node(new{Observations=unique.Length,Changed=choices.Count(x=>x["Changed"]!.GetValue<bool>()),HigherGas=choices.Count(x=>x["HigherGas"]!.GetValue<bool>()),
                Gas0=choices.Count(x=>x["Action"]?["Drive"]?.GetValue<double>()==0),Partial=choices.Count(x=>x["Action"]?["Drive"]?.GetValue<double>() is >0 and <1),
                Gas1=choices.Count(x=>x["Action"]?["Drive"]?.GetValue<double>()==1),NoAdmissible=choices.Count(x=>!x["HasCandidate"]!.GetValue<bool>())});
        }
        // Context only: these files do not contain complete comparable candidate pools.
        foreach(var path in new[]{"docs/experiments/throttle-steering-decoupling/README.md","docs/experiments/motorcycle-physics-validation/README.md","docs/experiments/tyre-law-regularization/results.json","docs/experiments/autonomous-acceleration/results.json"})sources[path]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return new JsonObject{["Audit"]=Node(new{NativeWorlds=0,NewForecasts=0,Policy=PhysicsExperiment139.Policy,States=states,Counts=counts,BranchStates=branches,
            SourceSha256=sources,UniqueObservations=seen.Count,RawObservations=states.Count,
            Limits="136 cached costs lack endpoint beta/yaw and full finite/reverse validation: unavailable values null, C there uses saved footprint/beta extrema only.137 uses every saved prediction point. Duplicate identities removed from counts. These are observations of two diagnostic physical families, not independent riders.138 direct interventions have no full objective pool;139-140 new material/mode contexts are not reranked as identical physics.141 roots are incomplete, so no causal full-pool gate ablation inferred.",
            CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds}),["Reservations"]=new JsonArray(),["Native"]=new JsonArray()};
    }
}
