using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;
public static class Experiment144
{
    public const string BaseHead="a6b301c6bf8a2854a6963a849b3944b435734f60";
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static Dictionary<string,string> Sources()=>Directory.GetFiles("tools/shared-world-speedway","*.cs",SearchOption.AllDirectories)
        .Concat(Directory.GetFiles("tools/standing-start-three-laps").Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".csproj",StringComparison.Ordinal)||p.EndsWith("generate.py",StringComparison.Ordinal)))
        .Where(p=>!p.Replace('\\','/').Contains("/obj/")&&!p.Replace('\\','/').Contains("/bin/")).Order().ToDictionary(p=>p.Replace('\\','/'),p=>Offline142.Hash(File.ReadAllText(p)));
    public static void Run(string directory,string variant){
        var path=Path.Combine(directory,"study.json");Directory.CreateDirectory(directory);
        using var lease=new FileStream(Path.Combine(directory,"study.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        JsonObject study;
        if(File.Exists(path))study=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        else{
            study=JsonSerializer.SerializeToNode(new{Protocol=new{BaseHead,EnabledByDefault=false,RequiredLaps=3,MainVariants=new[]{"A","B","F","K1"},Gate="B",InitialBodySpeed=0,InitialWheelOmega=0,
                Launch=LaunchParameters144.Default,Reaction=.24,Review=.2,Horizon=.9,Geometry="progressive-b; existing35m before first bend and27m after second; not a verified homologation survey",
                Constraints="Original143 action grid/weights/tyres/engine torque curve, gear ratio9 and efficiency.94. Additional finite crank inertia, drag and passive controlled clutch remain continuous for the whole race; no physical equivalence to old rigid rolling drivetrain is claimed.",
                Certification="Original legacy sweep first; where negative, existing refined collision-free held-wrench proof may certify a SOLO interval within its original bounded assumptions. Both certificates retained. Negative proof stops as uncertified, separate from confirmed instantaneous footprint crossing.",
                Finish="Start reference0 is not counted as a crossing. L,2L,3L are bracketed by consecutive native states; interpolate scalar finish time only. No resets of any physical/control state at a crossing.",
                Budget="At most8 primary native worlds: A first, B/F/K1, one identical A repeat, optionally one shared four-bike world after solo foundation. Preserve all incomplete attempts. No CEM or per-decision CPU cutoff.",
                FourRiders="Only after single-rider foundation is assessed. Existing ContactAdapter supports multiple physical bodies, but old FreeLearning/two-contact WorldRunner guards prevent direct reuse. Shared-clock per-rider state is required; no old HeatSimulator substitution.",Sources=Sources()},Reservations=Array.Empty<object>(),Runs=Array.Empty<object>()},Json)!.AsObject();
            Save(path,study);
        }
        foreach(var s in study["Protocol"]!["Sources"]!.AsObject())if(Offline142.Hash(File.ReadAllText(s.Key))!=s.Value!.GetValue<string>())throw new IOException("Frozen144 source changed: "+s.Key);
        var reservations=(JsonArray)study["Reservations"]!;var rows=(JsonArray)study["Runs"]!;
        if(reservations.Any(q=>!q!["Complete"]!.GetValue<bool>()))throw new IOException("Incomplete native attempt retained; no silent retry.");
        if(reservations.Any(q=>q!["Name"]!.GetValue<string>()==variant))throw new IOException("Duplicate native attempt.");
        if(reservations.Count>=8)throw new IOException("Native study bound reached.");
        if(variant!="A"&&!rows.Any(q=>q!["Name"]!.GetValue<string>()=="A"))throw new IOException("Run baseline A first.");
        var mode=variant=="A-repeat"?"A":variant;Policy144.Variant(mode);
        var reservation=new JsonObject{["Name"]=variant,["Complete"]=false};reservations.Add(reservation);Save(path,study);
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;Run144 run;
        using(var world=new StandingWorld144(new(Enabled:true)))run=world.Run(variant,mode);
        var file=Path.Combine(directory,variant+".json.gz");using(var z=new GZipStream(File.Create(file),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,run,Json);
        reservation["Complete"]=true;reservation["CpuSeconds"]=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
        rows.Add(JsonSerializer.SerializeToNode(new{run.Name,run.Variant,run.Outcome,run.EndSeconds,Laps=run.Riders.Select(r=>r.CompletedLaps),run.Fingerprint,
            run.TotalCpuSeconds,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,run.NativeSteps,run.NativeContactQueries,run.NativeClutchWheelQueries,run.Forecasts,run.ForecastContactQueries,
            RawSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),RawBytes=new FileInfo(file).Length},Json));Save(path,study);
        Console.WriteLine(rows[^1]!.ToJsonString(Json));
    }
    static void Save(string path,JsonObject value){File.WriteAllText(path+".tmp",value.ToJsonString(Json));File.Move(path+".tmp",path,true);}
}
