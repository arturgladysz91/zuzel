using System.IO.Compression;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using WholeCorner171;
namespace SpeedwayHandling;

internal sealed record Start197(HandlingForecast State,FreeRacingAction Active,FreeRacingCommand[] Pending,double Review)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double RiderHeightOffsetMeters { get; init; }
}
internal readonly record struct Edit197(int Center,int Radius,double Steer,double Gas)
{
    internal static FreeRacingAction[] Apply(FreeRacingAction[] current,Edit197[] edits)
    {
        var proposal=new FreeRacingAction[current.Length];
        double bound=TwoContactBikeParameters.Default.MaximumSteeringRadians;
        for(int i=0;i<current.Length;i++){
            double steering=current[i].SteeringRadians,gas=current[i].Drive;
            foreach(var edit in edits){
                double u=Math.Abs(i-edit.Center)/(double)edit.Radius;
                if(u>=1)continue;
                double weight=.5+.5*Math.Cos(Math.PI*u);
                steering+=edit.Steer*weight;gas+=edit.Gas*weight;
            }
            proposal[i]=new((float)Math.Clamp(steering,-bound,bound),(float)Math.Clamp(gas,0,1));
        }
        return proposal;
    }
}
internal static partial class Study191
{
    private static string? researchRepository202;
    private static BendMap Map199=>new(Frame);
    private static T Read199<T>(string path)
    {
        if(!File.Exists(path)&&researchRepository202 is {} repo){
            var normalized=Path.GetFullPath(path).Replace('\\','/');
            const string marker="/outputs/badanie200/";
            if(normalized.Contains(marker))path=Path.Combine(repo,"data/experiments/speedway200",normalized[(normalized.IndexOf(marker)+marker.Length)..]);
            else if(normalized.EndsWith("/outputs/badanie194/natywne/solo-wide.json.gz"))
                path=Path.Combine(repo,"data/experiments/speedway200/C-native.json.gz");
        }
        using var file=File.OpenRead(path);
        using var compressed=new GZipStream(file,CompressionMode.Decompress);
        return JsonSerializer.Deserialize<T>(compressed,Json)??throw new InvalidOperationException("Missing archive "+path);
    }
    private static Start197 Sample199(StartSample144 sample,LeanSample lean)
    {
        var x=sample.Input;
        return new(new(x.Rider,x.Time,x.Omega,x.Steering,lean.ActualRadians,sample.CrankOmega),x.Active,x.Pending,x.NextReview);
    }
    private static bool Equal199<T>(T a,T b)=>JsonSerializer.SerializeToUtf8Bytes(a,Json).AsSpan()
        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(b,Json));
    private static FreeRacingAction[] Pad200(FreeRacingAction[] controls)=>
        controls.Concat(Enumerable.Repeat(controls[^1],14)).ToArray();
    private static FreeRacingAction[] Tape199(RiderRun144 rider,double after)
    {
        var commands=rider.Activations.Where(x=>x.ObservedAt>=after-1e-8).Select(x=>x.Action).ToArray();
        return commands.Concat(Enumerable.Repeat(commands[^1],45)).ToArray();
    }
    private static (Seed198 Seed,StartingGate Gate,RiderSpec Spec) Fixture200(string workspace,string arm)
    {
        var history=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie194/natywne/solo-wide.json.gz"));
        var r=history.Run.Riders.Single();
        var root=new Seed198(arm,history,0,Sample199(r.Samples[0],history.Lean[0][0]),3*Frame.LapMeters,Tape199(r,0));
        var (gates,specs)=Grid194("solo-wide");
        var spec=specs[0];
        if(arm=="C-gear")spec=Spec193(gates[0],1,8.4);
        float offset = arm switch { "C-alt" => -.12f, "C-hold-minus" => -.02f, "C-hold-plus" => .02f,
            "C-alt-plus" => .12f, _ => 0 };
        if(offset != 0){
            spec=spec with {OffsetMeters=spec.OffsetMeters+offset,TargetOffsetMeters=spec.TargetOffsetMeters+offset};
            var where=Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
            var rider=root.Start.State.Rider with {Position=where.Position,Road=Frame.Project(where.Position),
                Heading=(float)where.Road.Heading,TargetOffsetMeters=(float)spec.TargetOffsetMeters};
            root=root with {Start=root.Start with {State=root.Start.State with {Rider=rider}}};
        }
        return(root,gates[0],spec);
    }
    private static (Archive198 Archive,Start197 Start,int Index,double Goal) MatchedRoot201(string workspace)
    {
        using var preflight=JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace,"outputs/badanie196/preflight.json")));
        double when=preflight.RootElement.GetProperty("BranchTime").GetDouble();
        double goal=preflight.RootElement.GetProperty("Goal").GetDouble();
        var archive=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie196/natywny.json.gz"));
        int i=Array.FindIndex(archive.Run.Riders.Single().Samples,s=>Math.Abs(s.Input.Time-when)<1e-9);
        return(archive,Sample199(archive.Run.Riders.Single().Samples[i],archive.Lean[0][i]),i,goal);
    }
}
