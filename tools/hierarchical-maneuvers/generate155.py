"""Reuse the frozen13/9 protocol; replace only whole-maneuver proposal completion."""
from pathlib import Path
import sys
R=Path(__file__).resolve().parents[2];D=Path(__file__).parent
s=(R/'tools/safe-fast-contingency/Planner153.Generated.cs').read_text(encoding='utf-8')
s=s.replace('    int repairCount;','').replace('repairCount = 0;','')
s=s.replace('namespace Safety153;','namespace Hierarchical155;\nusing Safety153;').replace('Planner153','Planner155').replace('STUDY153','STUDY155')
s=s.replace('    bool optionalRanking;long optionalStartK1;', '    bool optionalRanking;long optionalStartK1;\n    public Generator155 Generator {get;}\n    readonly Dictionary<string,Tape150[]> generated=new();int generationBatches;')
s=s.replace('RuntimeOptions153? performance = null)', 'RuntimeOptions153? performance = null,bool forwardCheck=false)')
s=s.replace('        Runtime = new(spec, separated, performance);','        Runtime = new(spec, separated, performance);Generator=new(Runtime,Step,Guard,forwardCheck);')
s=s.replace('currentPolicies.Clear();nodes.Clear();', 'currentPolicies.Clear();Generator.Begin();generated.Clear();generationBatches=0;nodes.Clear();')
s=s.replace(': Seed(state);', ': throw new InvalidOperationException("Uncovered tail is not a certificate; use maneuver generator");')
a=s.index('    Tape150? Complete(');b=s.index('    public static FreeRacingAction[] RepairActions',a)
s=s[:a]+'''    Tape150? Complete(State146 state,Tape150? anchor,string phase,FreeRacingAction? forcedRoot=null){
        if(anchor is not null){
            if(forcedRoot is null&&anchor.SafetyGoal>=safetyGoal-1e-7){var feedback=Rollout(state,anchor,null,"endogenous primitive feedback; full physical replay",phase);if(feedback.Safety.Legal)return feedback;}
            var offset=Math.Max(0,(int)Math.Round((state.Input.Time-anchor.CreatedAt)/.2));
            var remaining=anchor.Actions.Skip(offset).ToArray();
            if(remaining.Length>0&&anchor.SafetyGoal>=safetyGoal-1e-7){
                if(forcedRoot is {} request)remaining[0]=request;
                var held=Rollout(state,null,remaining,"primitive retained controls; full physical revalidation",phase);
                if(held.Safety.Legal)return held;
            }
        }
        var key=ExactKey147.State(state)+"/"+goal.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"/"+safetyGoal.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
        if(!generated.TryGetValue(key,out var candidates)){
            if(generationBatches++>=4)return null;
            using var cost=profile.Phase("hierarchical_generation");
            candidates=Generator.Generate(state,goal,safetyGoal,"endogenous phased primitive beam");generated[key]=candidates;
        }
        foreach(var candidate in candidates)if(forcedRoot is null||candidate.Actions[0]==forcedRoot.Value)return candidate;
        return null;
    }
''' +s[b:]
# No K1 proposal fallback exists in the new architecture.
a=s.index('    FreeRacingAction Seed(');b=s.index('    FreeRacingAction Feedback(',a);s=s[:a]+s[b:]
s=s.replace('    readonly Dictionary<string, FreeRacingAction> safeSeeds = new();','')
s=s.replace('long additionalForecasts, additionalContacts, failedK1Batches;', 'readonly long additionalForecasts=0, additionalContacts=0, failedK1Batches=0;')
# A finite certified tape must stop before any missing action; there is no K1 fallback.
s=s.replace('for (int k = 0; k < 130; k++) {','for (int k = 0; k < 130; k++) {\n            if(fixedActions is not null&&k>=fixedActions.Length||anchor is not null&&(int)Math.Round((state.Input.Time-anchor.CreatedAt)/.2)>=anchor.Actions.Length)break;')
s=s.replace('Physical = Runtime.Counters(), AdditionalCompletedForecasts', 'Physical = Runtime.Counters(), Hierarchy=new{Generator.PrimitiveCandidates,Generator.PhysicalSteps,Generator.PrefixRejections,Generator.DiversityPruned,Generator.FullPlans,Generator.LocalForceProbes,Generator.ForwardStageChecks,Generator.ForwardStageDeadEnds,Generator.ForwardStageWitnesses,Generator.DistinctLegalStates,GenerationBatches=generationBatches},AdditionalCompletedForecasts')
s=s.replace('        if (selected is null) throw new InvalidOperationException', '        if(Runtime.K1Calls!=0)throw new InvalidOperationException("Hierarchical generator must never invoke K1");\n        if (selected is null) throw new InvalidOperationException')
s='// Generated from frozen153 finite certificate protocol by generate155.py.\n'+s
p=D/'Planner155.Generated.cs'
if '--check' in sys.argv:assert p.read_text(encoding='utf-8')==s
else:p.write_text(s,encoding='utf-8',newline='\n')
print('155 protocol generation parity PASS')
