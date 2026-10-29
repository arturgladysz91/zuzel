using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard","core")]
public class EfficientPlanning147Tests{
    static string Root=>PhysicalSteeringAudit.PreviousPath("../efficient-autonomous-planner");
    static Run144 Saved(){using var z=new GZipStream(File.OpenRead(Path.Combine(Root,"../autonomous-corner-planning/native/autonomous146-viable.json.gz")),CompressionMode.Decompress);return JsonSerializer.Deserialize<Run144>(z,FreeLearningExperiment.Json)!;}
    static State146 State(Run144 run,double at){var s=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!;return new(s.Input with{Time=at,NextReview=at,Pending=s.Input.Pending.Where(c=>c.ObservedAt<at-1e-8).ToArray()},s.CrankOmega);}
    [Fact] public void NativeVariantIsExplicitlyDisabledByDefault(){
        var folder=Path.Combine(Root,"../../../work/test147-off");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"off.json");File.WriteAllText(path,"{\"Name\":\"off\"}");
        try{Assert.Throws<InvalidOperationException>(()=>Native147.Run(folder,path));}finally{File.Delete(path);Directory.Delete(folder);}
    }
    [Fact] public void CachedGeometryRetainsEveryPredictionValueAndRejectsAnotherFrame(){
        var run=Saved();var s=State(run,22.6);var f=StandingWorld144.Frame();var spec=run.Riders[0].Spec;var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();var a=new FreeRacingAction(.01f,.31820688f);
        var first=Predictor144.Predict(s.Input,spec,f,surface,p,147,52.72097f,.9,a,powertrain:new CrankClutch144(omega:s.CrankOmega));
        var same=CachedPredictor147.Predict(s.Input,spec,f,surface,p,147,52.72097f,.9,a,powertrain:new CrankClutch144(omega:s.CrankOmega),boundaryCache:new(f));
        Assert.Equal(JsonSerializer.Serialize(first,FreeLearningExperiment.Json),JsonSerializer.Serialize(same,FreeLearningExperiment.Json));
        Assert.Throws<ArgumentException>(()=>CachedPredictor147.Predict(s.Input,spec,f,surface,p,147,52.72097f,.2,a,boundaryCache:new(StandingWorld144.Frame())));
    }
    [Fact] public void ExactKeyIncludesWheelCrankActuatorQueueAndIeeeBits(){
        var s=State(Saved(),22.6);var key=ExactKey147.State(s);
        Assert.NotEqual(key,ExactKey147.State(s with{CrankOmega=Math.BitIncrement(s.CrankOmega)}));
        Assert.NotEqual(key,ExactKey147.State(s with{Input=s.Input with{Omega=Math.BitIncrement(s.Input.Omega)}}));
        Assert.NotEqual(key,ExactKey147.State(s with{Input=s.Input with{Steering=MathF.BitIncrement(s.Input.Steering)}}));
        Assert.NotEqual(key,ExactKey147.State(s with{Input=s.Input with{Pending=s.Input.Pending.Select(c=>c with{ActivationAt=Math.BitIncrement(c.ActivationAt)}).ToArray()}}));
        var zero=s with{Input=s.Input with{Steering=0f}};Assert.NotEqual(ExactKey147.State(zero),ExactKey147.State(zero with{Input=zero.Input with{Steering=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000))}}));
    }
    [Fact] public void DelayedPrefixSharesOnlyPhysicsAndRetainsEachDifferentQueue(){
        var r=Saved();var s=State(r,22.6);var f=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(f);var spec=r.Riders[0].Spec;
        var original=new Model146(spec,f,surface,p);var runtime=new ExactRuntime147(spec,f,surface,p);runtime.Begin(s.Input.Time);
        var a=new FreeRacingAction(-.08726647f,0);var b=new FreeRacingAction(.08726647f,1);
        var first=runtime.Step(s,a,.2);var second=runtime.Step(s,b,.2);
        Assert.Same(first.Prediction,second.Prediction);Assert.NotEqual(ExactKey147.State(first.End),ExactKey147.State(second.End));
        Assert.Equal(ExactKey147.State(original.Step(s,a).End),ExactKey147.State(first.End));Assert.Equal(ExactKey147.State(original.Step(s,b).End),ExactKey147.State(second.End));Assert.Equal(1,runtime.ActualForecasts);
        runtime.Step(s with{CrankOmega=Math.BitIncrement(s.CrankOmega)},a,.2);Assert.Equal(2,runtime.ActualForecasts);
    }
    [Fact] public void ExactSeedMemoDoesNotMutateInputAndPreservesFrozenK1Ranking(){
        var r=Saved();var s=State(r,22.6);var f=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(f);var p=FreeLearningExperiment.Parameters();var spec=r.Riders[0].Spec;var before=ExactKey147.State(s);
        var expected=Policy144.Choose(s.Input,new CrankClutch144(omega:s.CrankOmega),spec,f,surface,p,147,52.72097f,"K1");var runtime=new ExactRuntime147(spec,f,surface,p);runtime.Begin(s.Input.Time);
        var actual=runtime.Seed(s);var count=runtime.ActualForecasts;runtime.Seed(s);
        Assert.Equal(expected.Choice.Action,actual.Choice.Action);Assert.Equal(JsonSerializer.Serialize(expected.Choice.Candidates),JsonSerializer.Serialize(actual.Choice.Candidates));Assert.Equal(count,runtime.ActualForecasts);Assert.Equal(1,runtime.SeedHits);Assert.Equal(before,ExactKey147.State(s));
    }
    [Fact] public void IndependentPreflightBypassesTheForecastCache(){
        var r=Saved();var s=State(r,22.6);var f=StandingWorld144.Frame();var rt=new ExactRuntime147(r.Riders[0].Spec,f,MultiActionExperiment135.Surface(f),FreeLearningExperiment.Parameters());rt.Begin(s.Input.Time);var a=new FreeRacingAction(0,0);
        rt.Step(s,a,.2);rt.Step(s,a,.2);Assert.Equal(1,rt.ActualForecasts);
        using(rt.Operation("independent_preflight"))rt.Step(s,a,.2);Assert.Equal(2,rt.ActualForecasts);
    }
    [Fact] public void DurableJournalRetainsCompleteHashCheckedRecordsAfterPartialTail(){
        var folder=Path.Combine(Root,"../../../work/test147-journal");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"decisions.jsonl");
        try{using(var journal=new Journal147(path)){journal.Append(new{Event="decision",Time=0,Selected=new FreeRacingAction(0,1)});journal.Append(new{Event="decision",Time=.2,Selected=new FreeRacingAction(0,0)});Assert.Equal(2,Journal147.ReadComplete(path).Length);}
            File.AppendAllText(path,"{\"Index\":2,\"Payload\":");Assert.Equal(2,Journal147.ReadComplete(path).Length);
            var text=File.ReadAllText(path).Replace("\"Time\":0,","\"Time\":1,");File.WriteAllText(path,text);Assert.Throws<IOException>(()=>Journal147.ReadComplete(path));
        }finally{File.Delete(path);Directory.Delete(folder);}
    }
    [Fact] public void ExactPlannerBenchmarkMatchesStateWhileStrategyChangeIsExplicit(){
        using var exact=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"comparison/exact-22-6.json")));var a=exact.RootElement;
        Assert.True(a.GetProperty("CommandMatchesBaseline").GetBoolean());Assert.True(a.GetProperty("EndStateBitExact").GetBoolean());Assert.True(a.GetProperty("LogicalForecastsMatch").GetBoolean());Assert.False(a.GetProperty("StrategyChanged").GetBoolean());
        using var changed=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"comparison/shortlist-22-6.json")));Assert.True(changed.RootElement.GetProperty("StrategyChanged").GetBoolean());Assert.Equal("certified decision",changed.RootElement.GetProperty("Outcome").GetString());
    }
    [Fact] public void OptionalWorkExhaustionRetainsAndIndependentlyChecksLegalIncumbent(){
        var r=Saved();var state=State(r,14.4);var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var p=FreeLearningExperiment.Parameters();
        using var z=new GZipStream(File.OpenRead(Path.Combine(Root,"memory-14-2.json.gz")),CompressionMode.Decompress);using var data=JsonDocument.Parse(z);
        var previous=data.RootElement.GetProperty("LastReview").Deserialize<Review146>(FreeLearningExperiment.Json)!;var expected=new Model146(r.Riders[0].Spec,frame,surface,p).Step(new(previous.Input,previous.CrankOmega),previous.Selected!.Actions[0]).End;
        var runtime=new ExactRuntime147(r.Riders[0].Spec,frame,surface,p,new(WorkBudget:true,OptionalSearchContacts:1,MaximumActualForecasts:8192,MaximumContacts:700000));
        var hooks=runtime.Hooks() with{ScheduledSearch=(_,_,_)=>true};var planner=new Planner146(r.Riders[0].Spec,frame,surface,p,new(Enabled:true),hooks:hooks);
        planner.RestoreMemoryForDiagnostics(new(previous.Selected,expected,data.RootElement.GetProperty("NextSearch").GetDouble()));
        var chosen=planner.Choose(state.Input,new CrankClutch144(omega:state.CrankOmega));var review=planner.Reviews.Single();
        Assert.Equal(FreeActionSafety.SafeInHorizon,chosen.Choice.Safety);Assert.True(review.Selected!.Safety.Legal);Assert.True(review.IndependentSafety!.Legal);Assert.Equal(1,runtime.BudgetFallbacks);
    }

}
