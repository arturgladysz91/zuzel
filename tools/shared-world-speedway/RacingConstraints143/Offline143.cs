using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;
public static class Offline143
{
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object x)=>JsonSerializer.SerializeToNode(x,Json)!;
    public static JsonObject Run(){
        var old=JsonNode.Parse(File.ReadAllText("docs/experiments/racing-objective-ablation/results.json"))!;
        var states=new JsonArray();var counts=new JsonObject();var histories=new Dictionary<string,string>();
        foreach(var path in new[]{"docs/experiments/track-awareness-anticipatory-riding/README.md","docs/experiments/speed-throttle-root-cause/README.md",
            "docs/experiments/causal-yaw-throttle/README.md","docs/experiments/throttle-steering-decoupling/README.md","docs/experiments/motorcycle-physics-validation/README.md",
            "docs/experiments/tyre-law-regularization/README.md","docs/experiments/autonomous-acceleration/README.md","docs/experiments/racing-objective-ablation/results.json"})
            histories[path]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        foreach(var state in (JsonArray)old["Audit"]!["States"]!){
            if(state!["DuplicateOfEarlier"]!.GetValue<bool>())continue;var input=state["Input"]!.Deserialize<FreeRacingInput>(Json)!;
            var leaves=((JsonArray)state["Candidates"]!).Select(row=>{
                var candidate=new FreeLearningCandidate(row!["Action"]!.Deserialize<FreeRacingAction>(Json),row["Safety"]!.Deserialize<FreeActionSafety>(Json),
                    row["Score"]!.GetValue<double>(),row["Progress"]!.GetValue<double>(),row["Clearance"]!.GetValue<double>(),row["FutureClearance"]!.GetValue<double>(),
                    row["SpeedKph"]!.GetValue<double>()/3.6,row["EndBetaRadians"]?.GetValue<double>()??0,0);
                return new Leaf142(candidate,row["Terms"]!.Deserialize<Terms142>(Json)!,row["HardViolation"]?.GetValue<string>(),row["MaximumBetaDegrees"]!.GetValue<double>(),row["MaximumYawRate"]!.GetValue<double>(),row["EndYawRate"]?.GetValue<double>());
            }).ToArray();var choices=new JsonObject();
            foreach(var variant in Experiment143.Isolated.Where(v=>v.Id is not "H" and not "R")){
                var eligible=leaves.Where(l=>!variant.NominalHierarchy||l.HardViolation is null).Select(l=>new{Leaf=l,Score=Controller143.Score(l,variant)});
                var choice=eligible.OrderBy(l=>variant.NominalHierarchy?0:(int)l.Score.Safety).ThenBy(l=>l.Score.Score)
                    .ThenBy(l=>Math.Abs(l.Leaf.Candidate.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(l=>l.Leaf.Candidate.Action.SteeringRadians).FirstOrDefault();
                choices[variant.Id]=choice is null?null:Node(new{choice.Leaf.Candidate.Action,choice.Score.Score,choice.Score.Safety,choice.Leaf.Candidate.Progress,choice.Leaf.MaximumBetaDegrees,choice.Leaf.EndYawRate,choice.Leaf.Candidate.Clearance,choice.Leaf.HardViolation});
            }
            states.Add(new JsonObject{["Name"]=state["Name"]!.DeepClone(),["Identity"]=state["Identity"]!.DeepClone(),["Source"]=state["Source"]!.DeepClone(),["Choices"]=choices});
        }
        foreach(var variant in Experiment143.Isolated.Where(v=>v.Id is not "H" and not "R")){
            var selected=states.Select(s=>s!["Choices"]![variant.Id]).ToArray();
            counts[variant.Id]=Node(new{States=states.Count,Changed=states.Count(s=>s!["Choices"]![variant.Id]?["Action"]?.ToJsonString()!=s["Choices"]!["A"]!["Action"]!.ToJsonString()),
                Gas0=selected.Count(s=>s?["Action"]?["Drive"]?.GetValue<double>()==0),Partial=selected.Count(s=>s?["Action"]?["Drive"]?.GetValue<double>() is >0 and <1),Gas1=selected.Count(s=>s?["Action"]?["Drive"]?.GetValue<double>()==1),
                NoNominalCandidate=selected.Count(s=>s is null)});
        }
        var awareness=new JsonArray();
        foreach(var file in Directory.GetFiles("docs/experiments/causal-yaw-throttle","offline-*.json.gz").Order()){
            using var z=new GZipStream(File.OpenRead(file),CompressionMode.Decompress);var cache=JsonSerializer.Deserialize<Cache142>(z,Json)!;
            var c=new NativeCase139(cache.State.Case.Name,"offline",cache.State.Case,1,"","");var controller=new Controller143(c);var original=controller.Cached(cache.State.Input,Experiment143.Isolated[0],cache.Search.Held);
            var h=controller.Cached(cache.State.Input,Experiment143.Isolated.Single(v=>v.Id=="H"),cache.Search.Held);
            Leaf143 Pick(Leaf143[] l)=>l.OrderBy(l=>l.Safety).ThenBy(l=>l.Score).ThenBy(l=>Math.Abs(l.Original.Candidate.Action.SteeringRadians-cache.State.Input.Active.SteeringRadians)).ThenBy(l=>l.Original.Candidate.Action.SteeringRadians).First();
            var a=Pick(original);var b=Pick(h);awareness.Add(Node(new{cache.State.Name,Original=a.Original.Candidate.Action,Changed=b.Original.Candidate.Action!=a.Original.Candidate.Action,
                Choice=b.Original.Candidate.Action,b.Score,b.PreparationCost,OriginalGeometry=b.Original.Terms.Geometry,Meaning="Same cached H09 forecasts, no new prediction or native future; future geometry estimate is soft and uncertified."}));
        }
        return new JsonObject{["UniqueStates"]=states.Count,["PhysicalFamilies"]=2,["NewWorlds"]=0,["NewForecasts"]=0,["Counts"]=counts,["States"]=states,["GeometryH"]=awareness,
            ["HistorySha256"]=Node(histories),["Original142Context"]=old["Analysis"]!.DeepClone(),
            ["Limits"]="28 cached unique observations are not28 independent riders.136 endpoint beta/yaw/finite validation may be unavailable. R requires extra physical recovery forecasts and is tested separately in full native runs, not inferred from cached ranking."};
    }
}
