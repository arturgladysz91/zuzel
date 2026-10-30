"""Mechanical153 adapters. Original physics and153 sources are never modified."""
from pathlib import Path
import sys
R=Path(__file__).resolve().parents[2];D=Path(__file__).parent
def emit(name,s):
 s='// Generated mechanically from frozen study153; see generate154.py.\n'+s
 p=D/name
 if '--check' in sys.argv:assert p.read_text(encoding='utf-8')==s,name
 else:p.write_text(s,encoding='utf-8',newline='\n')
for kind in ('Runtime','Planner','Feedback'):
 s=(R/f'tools/safe-fast-contingency/{kind}153.Generated.cs').read_text(encoding='utf-8')
 s=s.replace('namespace Safety153;','namespace Viable154;\nusing Safety153;')
 for name in ('Runtime','Planner','Feedback'):s=s.replace(name+'153',name+'154')
 s=s.replace('RuntimeOptions153','RuntimeOptions154')
 if kind=='Runtime':
  s=s.replace('ExactRuntimeA153','ExactRuntimeA154').replace('ExactRuntimeB153','ExactRuntimeB154')
  s=s.replace('public object Counters()=>', 'public object CandidateLog()=>a is not null?a.SeedDiagnostics:b!.SeedDiagnostics;\n public object Counters()=>')
  s=s.replace('bool StrictSeeds=false);','bool StrictSeeds=false,int CandidateMode=0);')
  s=s.replace(' public Decision144 Seed(State146 state)=>a is not null?a.Seed(state):b!.Seed(state);',''' public double RequiredReferenceSpeed=15;
 public long ViableSeedCalls,ViableCandidateForecasts,LocalPoseRejected,LocalNonPoseRejected,LocalLegalCandidates;
 public Decision144 OriginalSeed(State146 state)=>a is not null?a.Seed(state):b!.Seed(state);
 public FreeRacingPrediction CandidateForecast(State146 state,FreeRacingAction action)=>a is not null?a.CandidateView(state,action):b!.CandidateView(state,action);
 public int CandidateMode=>options.CandidateMode;
 public Decision144 Seed(State146 state){if(options.CandidateMode==0)return a is not null?a.Seed(state):b!.Seed(state);ViableSeedCalls++;return Candidate154.Choose(this,state);}
''')
  s=s.replace('Verification="Fresh per-review DAG;', 'ViableSeedCalls,ViableCandidateForecasts,LocalPoseRejected,LocalNonPoseRejected,LocalLegalCandidates,Verification="Fresh per-review DAG;')
 if kind=='Planner':
  s=s.replace('STUDY153','STUDY154')
  s=s.replace('    Tape150? Complete(State146 state, Tape150? anchor, string phase, FreeRacingAction? forcedRoot = null) {', '''    public List<object> RolloutDiagnostics {get;}=new();
    Tape150? Complete(State146 state, Tape150? anchor, string phase, FreeRacingAction? forcedRoot = null) {
        if(performance.CandidateMode>=2&&anchor is not null){
            var offset=Math.Max(0,(int)Math.Round((state.Input.Time-anchor.CreatedAt)/.2));var retainedControls=anchor.Actions.Skip(offset).ToArray();
            if(retainedControls.Length>0){if(forcedRoot is {} root)retainedControls[0]=root;var held=Rollout(state,null,retainedControls,"held-retained-priority",phase);if(held.Safety.Legal)return held;}
        }
''')
  s=s.replace('currentPolicies.Clear();nodes.Clear();', 'currentPolicies.Clear();RolloutDiagnostics.Clear();nodes.Clear();')
  s=s.replace('attempts.Add(new(origin, safety.Legal, arrival, safety.Reason,', '''if(!safety.Legal&&RolloutDiagnostics.Count<512)RolloutDiagnostics.Add(new{origin,Start=start,Actions=tape.Actions,Goal=goal,SafetyGoal=safetyGoal,tape.Safety,LastPoints=tape.Points.TakeLast(25).ToArray()});
        attempts.Add(new(origin, safety.Legal, arrival, safety.Reason,''')
  s=s.replace('action = fixedActions is not null', 'Runtime.RequiredReferenceSpeed=Math.Max(15,(safetyGoal-state.Input.Rider.TotalReferenceMeters)/Math.Max(.2,(130-k)*.2));\n                action = fixedActions is not null')
  s=s.replace('if(shareCurrentPolicies&&retained is not null&&retained.SafetyGoal<safetyGoal-1e-7)', 'if(performance.CandidateMode<2&&shareCurrentPolicies&&retained is not null&&retained.SafetyGoal<safetyGoal-1e-7)')
 emit(kind+'154.Generated.cs',s)
for model in ('A','B'):
 s=(R/f'tools/safe-fast-contingency/ExactRuntime{model}153.Generated.cs').read_text(encoding='utf-8')
 s=s.replace('namespace Safety153;','namespace Viable154;\nusing Safety153;').replace(f'ExactRuntime{model}153',f'ExactRuntime{model}154')
 s=s.replace('    readonly Dictionary<string,(FreeRacingPrediction', '    string? lastSeedKey;readonly Dictionary<FreeRacingAction,FreeRacingPrediction> lastCandidates=new();public long CandidatePoolHits;\n    readonly Dictionary<string,(FreeRacingPrediction')
 s=s.replace('    readonly bool cheapTail;', '    public List<object> SeedDiagnostics {get;}=new();public long LocalPoseRejected,LocalNonPoseRejected,LocalLegalCandidates;\n    readonly bool cheapTail;')
 s=s.replace('public void Begin(double time){', 'public void Begin(double time){SeedDiagnostics.Clear();')
 s=s.replace('FullK1Executions++;var began=', 'lastSeedKey=ExactKey147.State(state);lastCandidates.Clear();FullK1Executions++;var began=')
 s=s.replace('var physical=new Dictionary<FreeRacingAction,Safety146>();','var details=new List<object>();var physical=new Dictionary<FreeRacingAction,Safety146>();')
 s=s.replace('var prediction=Predict(state,action,.9).Prediction;contacts+=','var prediction=Predict(state,action,.9).Prediction;lastCandidates[action]=prediction;contacts+=')
 s=s.replace('if(safetyHalfMeter)physical[action]=Clearance153.Check(prediction.Points,Regime153.SafetyHalfMeter);', '''if(safetyHalfMeter){var physicalSafety=Clearance153.Check(prediction.Points,Regime153.SafetyHalfMeter);physical[action]=physicalSafety;if(physicalSafety.MinimumGapMeters<.5)LocalPoseRejected++;else if(!physicalSafety.Legal)LocalNonPoseRejected++;else LocalLegalCandidates++;var last=prediction.Points[^1];details.Add(new{Action=action,Safety=physicalSafety,Terminal=last});}''')
 s=s.replace('var result=new Decision144(new(input.Time,chosen.Action,', 'SeedDiagnostics.Add(new{Start=state,Candidates=details,OriginalSelected=chosen.Action});var result=new Decision144(new(input.Time,chosen.Action,')
 s=s.replace('    public FreeRacingPrediction ForecastForDiagnostics', '    public FreeRacingPrediction CandidateView(State146 state,FreeRacingAction action){if(lastSeedKey==ExactKey147.State(state)&&lastCandidates.TryGetValue(action,out var value)){CandidatePoolHits++;return value;}return Predict(state,action,.9).Prediction;}\n    public FreeRacingPrediction ForecastForDiagnostics')
 s=s.replace('ShortlistK1=shortlistK1,Complete27K1Batches=', 'LocalPoseRejected,LocalNonPoseRejected,LocalLegalCandidates,CandidatePoolHits,ShortlistK1=shortlistK1,Complete27K1Batches=')
 emit(f'ExactRuntime{model}154.Generated.cs',s)
print('Study154 generated-source parity PASS' if '--check' in sys.argv else 'Generated154 adapters')
