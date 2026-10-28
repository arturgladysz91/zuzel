using System.IO.Compression;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class CornerSpeedLoss145Tests
{
    static string Root=>PhysicalSteeringAudit.PreviousPath("../corner-speed-loss");
    [Fact]
    public void DiagnosticWorldRemainsExplicitlyDisabledByDefault()=>Assert.Throws<InvalidOperationException>(()=>new DiagnosticWorld145());
    [Fact]
    public void NativeCausalRecordHasExactFullReplayAndRetainsLaterFailure(){
        using var replay=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"native/replay-K1-result.json")));
        var r=replay.RootElement;Assert.True(r.GetProperty("PrefixExact").GetBoolean());
        Assert.Equal(r.GetProperty("Fingerprint").GetString(),r.GetProperty("FullOriginalFingerprint").GetString());
        using var failed=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"native/steer-large-31-result.json")));
        Assert.Equal(2,failed.RootElement.GetProperty("CompletedLaps").GetInt32());Assert.Equal("confirmed footprint crossing",failed.RootElement.GetProperty("Outcome").GetString());
    }
    [Fact]
    public void TwoCommandForecastCannotMutateAcceptedCrankQueueOrActionGrid(){
        using var z=new GZipStream(File.OpenRead(Path.Combine(Root,"../standing-start-three-laps/native/K1.json.gz")),CompressionMode.Decompress);
        var run=JsonSerializer.Deserialize<Run144>(z,FreeLearningExperiment.Json)!;var s=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-29))!;
        var input=s.Input with{Time=29,NextReview=29};var pending=JsonSerializer.Serialize(input.Pending);var crank=new CrankClutch144(omega:s.CrankOmega);var before=crank.Omega;
        var frame=StandingWorld144.Frame();var spec=run.Riders[0].Spec;
        var result=Algorithm145.Choose(input,crank,spec,frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters(),147,52.72097f,"K1","two-command");
        Assert.Equal(before,crank.Omega);Assert.Null(crank.Prepared);Assert.Equal(pending,JsonSerializer.Serialize(input.Pending));
        Assert.Contains(result.Choice.Action,FreeLearningController.Actions(input,Controller143.Policy,true));
        Assert.InRange(result.Choice.Forecasts,28,64);Assert.InRange(result.Choice.Candidates.Length,1,36);
        Assert.All(result.Choice.Candidates,c=>Assert.True(double.IsFinite(c.Score)));
    }
}
