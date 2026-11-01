import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(dir,'../..');
let s=fs.readFileSync(path.join(root,'tools/state-maneuver-transfer/Planner157.Generated.cs'),'utf8').replaceAll('namespace Transfer157;','namespace ThreeLap158;').replaceAll('Planner157','Planner158').replaceAll('Generator157','Generator158').replaceAll('STUDY157','STUDY158');
s=s.replace('bool forwardCheck=false)','Settings158? settings=null)').replace('Generator=new(Runtime,Step,Guard,forwardCheck)','Generator=new(Runtime,Step,Guard,settings)');
// Match the ACTUAL hook with whitespace; study157's no-space substitution missed it.
s=s.replace(/Feedback153\.Action\(Runtime,\s*state,\s*anchor\)/g,'Feedback158.Action(Runtime,state,anchor,Generator.Control)');
if(s.includes('Feedback153.Action('))throw Error('Unreplaced feedback hook');
s=s.replace('k < 130','k < Generator158.MaximumReviews').replace('MaximumReviewForecasts = 8192','MaximumReviewForecasts = 32768').replace('TotalSeconds >= 12','TotalSeconds >= 60').replace('or30 CPU-s','or60 CPU-s');
let a=s.indexOf('    public void InitializeDiagnosticTape('),b=s.indexOf('    public static bool AxisAlignedInvariant',a);s=s.slice(0,a)+s.slice(b);
s=s.replace('||anchor is not null&&(int)Math.Round((state.Input.Time-anchor.CreatedAt)/.2)>=anchor.Actions.Length','');
s=s.replace('throw new InvalidOperationException("Uncovered tail is not a certificate; use maneuver generator")','Generator.Control.Action(state)');
s=s.replace('forcedRoot is null&&anchor.SafetyGoal>=safetyGoal-1e-7','forcedRoot is null');
// Extending an endogenous plan still requires full fresh13/9 and independent replay.
s=s.split('\n').filter(l=>!l.includes('var overlap=(int)Math.Ceiling(Speed148.FastPredictor148.MaximumHorizonSeconds')).join('\n');
s=s.replace('safetyGoal = Planner146.Goal(Runtime.Frame, goal + .005, Options.Laps);','safetyGoal = Planner146.Goal(Runtime.Frame, goal + .005, Options.Laps);\n        if(goal>=Options.Laps*Runtime.Frame.LapMeters-.001)safetyGoal=Math.Max(safetyGoal,Options.Laps*Runtime.Frame.LapMeters+Runtime.Frame.SegmentStartMeters(1)-3);');
s=s.replace('public Runtime153 Runtime','public Runtime158 Runtime');
s=s.replace('observation-conditioned learned proposals','causal physical state-feedback reserve');
s='// Study158: same finite13/9; causal joint feedback and FULL reserve extension.\n'+s;
const target=path.join(dir,'Planner158.Generated.cs');if(process.argv.includes('--check')){if(fs.readFileSync(target,'utf8')!==s)throw Error('158 protocol drift')}else fs.writeFileSync(target,s);console.log('158 finite certificate derivation PASS');

let runtime=fs.readFileSync(path.join(root,'tools/safe-fast-contingency/Runtime153.Generated.cs'),'utf8');
runtime=runtime.replace(/public sealed record RuntimeOptions153[\s\S]*?;\n/,'').replace(/public sealed class Work152[^\n]*\n/,'').replace('namespace Safety153;','namespace ThreeLap158;\nusing Safety153;').replaceAll('Runtime153','Runtime158');
runtime=runtime.replace(' public Decision144 Seed(',` public long PrimedPrefixes {get;private set;}
 public void PrimeUnavoidablePrefix(State146 state,FreeRacingPrediction prediction,double crank){
  if(independent)throw new InvalidOperationException("No proposal priming during independent validation");
  if(prediction.Points.Length<2||prediction.Points[0].Time!=state.Input.Time||Math.Abs(prediction.Points[^1].Time-state.Input.Time-.2)>1e-9)throw new ArgumentException("Exact0.2s physical prefix required");
  var key=Key(state);if(search.ContainsKey(key))return;search[key]=new(new(state.Input,crank),prediction);PrimedPrefixes++;
 }
 public Decision144 Seed(`);
runtime=runtime.replace('IndependentComparisons,Verification=','IndependentComparisons,PrimedPrefixes,Verification=');
const rp=path.join(dir,'Runtime158.Generated.cs');if(process.argv.includes('--check')){if(fs.readFileSync(rp,'utf8')!==runtime)throw Error('158 runtime drift')}else fs.writeFileSync(rp,runtime);

let joint=fs.readFileSync(path.join(root,'tools/robust-autonomous-racing/Joint150.cs'),'utf8').replace('namespace Robust150;','namespace ThreeLap158;\nusing Robust150;').replace(/public sealed record Response150[^\n]*\n/,'').replace('class Joint150','class Joint158').replace('double desiredYaw,double tau)','double desiredYaw,double tau,Control158 memo)').replaceAll('rt.Response(', 'memo.JointResponse(rt,');
const jp=path.join(dir,'Joint158.Generated.cs');if(process.argv.includes('--check')){if(fs.readFileSync(jp,'utf8')!==joint)throw Error('158 joint derivation drift')}else fs.writeFileSync(jp,joint);
