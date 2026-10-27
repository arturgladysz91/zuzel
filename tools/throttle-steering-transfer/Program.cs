using System.Diagnostics;
using System.Text.Json;
using SharedWorldSpeedway;
using static SharedWorldSpeedway.ThrottleSteering138;

if(args.Length!=4||args[0]!="--enable-transfer138")throw new ArgumentException("Default OFF: --enable-transfer138 <evidence> <raw> freeze|run");
var d=args[1];var raw=args[2];var stage=args[3];var policy=Policy;
var original=RobustLearningExperiment.ReadEpisode(Old+"/native","transfer-A");
var definition=new FactorCase138("T0","transfer",new("T0","egg-asymmetric",original.Replay.Scenario,true),1,20,Old+"/native","transfer-A");
if(stage=="freeze"){
    if(File.Exists(Path.Combine(d,"transfer-protocol.json")))throw new IOException("Do not overwrite transfer freeze.");
    Save(d,"transfer-state.json",State(definition));
    Save(d,"transfer-protocol.json",new{FrozenBeforeAnyNewTransferWorld=true,OptionalAllocation="Original protocol allows one egg quartet when prototype gate is closed; existing V1 #137 transfer-A prefix. This transfer does not select a policy.",
        Definition=definition,ProtocolHash=Hash(Path.Combine(d,"protocol.json")),SelectionLockHash=Hash(Path.Combine(d,"selection-lock.json")),
        SourceHash=Hash("tools/throttle-steering-transfer/Program.cs"),MaxTransferWorlds=4,TotalWorldLimit=48,TotalCpuLimit=650});return;
}
if(stage!="run")throw new ArgumentException("freeze|run");
if(ThrottleSteering138.Read<JsonElement>(d,"selection-lock.json").GetProperty("PrototypeGate").GetBoolean())throw new InvalidOperationException("Allocation reserved for prototype.");
var transferProtocol=ThrottleSteering138.Read<JsonElement>(d,"transfer-protocol.json");
if(transferProtocol.GetProperty("SourceHash").GetString()!=Hash("tools/throttle-steering-transfer/Program.cs"))throw new IOException("Frozen source mismatch.");
var state=ThrottleSteering138.Read<FactorState138>(d,"transfer-state.json");
using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",48},{"offline",80}},48,650,20000000);
if(budget.Entries.Any(x=>!x.Complete))throw new IOException("Incomplete reservation; do not repeat.");
var predictionPath=Path.Combine(d,"forecast-T0.json.gz");FactorLeaf138[] leaves;
if(File.Exists(predictionPath))leaves=Unzip<FactorLeaf138[]>(predictionPath);
else {var n=budget.Reserve("forecast-T0","offline",false);var start=Process.GetCurrentProcess().TotalProcessorTime;
    leaves=Plans(state.Input).SelectMany(plan=>new[]{Forecast(state,plan,1),Forecast(state,plan,(float)policy.Drive)}).ToArray();
    budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds,leaves.Sum(x=>(long)x.Prediction.ForceEvaluations),leaves.Length);Zip(predictionPath,leaves);}
var selected=Rank(leaves.Where(x=>x.Drive==1)).First();Save(d,"choice-T0.json",new{Chosen=selected.Plan,ChosenBeforeNative=true,Ranking="Original frozen H09 score; transfer only"});
foreach(var arm in new[]{"A","B","C","D"}){
    var name="T0-"+arm;if(File.Exists(Path.Combine(d,"result-"+name+".json")))continue;
    var plan=arm is "A" or "C"?state.Hold:selected.Plan;var drive=arm is "A" or "B"?1:(float)policy.Drive;
    var c=definition.Case;var frame=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=MultiActionExperiment135.Surface(frame);var spec=FreeLearningExperiment.Rider(c.Scenario);
    bool exact=false;
    FreeLearningDecision Choose(FreeRacingInput i){
        var old=MultiActionPlanner135.Baseline(i,policy,spec,frame,surface,FreeLearningExperiment.Parameters());
        if(Math.Abs(i.Time-state.Input.Time)<1e-8)exact=i.Rider==state.Input.Rider&&i.Omega==state.Input.Omega&&i.Steering==state.Input.Steering&&i.Active==state.Input.Active&&i.Pending.SequenceEqual(state.Input.Pending);
        return i.Time<state.Input.Time-1e-8||i.Time>=state.Input.Time+RequestWindow-1e-8?old:old with{Action=Action(state.Input,plan,drive,(int)Math.Round((i.Time-state.Input.Time)/.2))};
    }
    var run=CausalYawNative137.Execute(d,raw,budget,"native",name,16015,policy,c.Scenario,twoBends:true,horizonSeconds:.9,normalizeProgress:true,fullThrottleCandidate:true,
        trackShape:TrackAwarenessExperiment134.Variant(c.Geometry),chooser:Choose,extendAfterSecondStraight:true);
    var prefix=MultiActionExperiment135.Prefix(original,run,state.Input.Time,.24);
    var forecast=leaves.Single(x=>x.Plan==plan&&x.Drive==drive);var accuracy=MultiActionExperiment135.Accuracy(forecast.Prediction,run,out var pass);
    Save(d,"result-"+name+".json",new FactorResult138(name,arm,"transfer",state,plan,drive,run.Metrics,prefix,exact,accuracy));
    if(!prefix||!exact||run.Replay.Samples[^1].Input.Time>=state.Input.Time+.9-1e-8&&!pass)throw new InvalidOperationException("Transfer prefix/observation/prediction parity failed.");
}
Save(d,"transfer-complete.json",new{Worlds=budget.Entries.Sum(x=>x.NativeWorlds),CpuSeconds=budget.Entries.Sum(x=>x.CpuSeconds),Contacts=budget.Entries.Sum(x=>x.Evaluations),Forecasts=budget.Entries.Sum(x=>x.Forecasts),PolicySelectionFromTransfer=false});
