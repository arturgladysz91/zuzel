"""Observation-state forks of frozen #172; no controller or physics formula edits."""
from pathlib import Path
import hashlib

ROOT = Path(__file__).resolve().parents[2]
DEST = Path(__file__).parent

def insert(text, marker, addition):
    assert text.count(marker) == 1, marker
    return text.replace(marker, addition + '\n' + marker)

def sources():
    old = ROOT / 'tools/three-lap-global-planning'
    texts = {name: (old / name).read_text(encoding='utf-8') for name in
             ['Policy172.Generated.cs', 'CyclicExit172.Generated.cs', 'Tracker172.Generated.cs', 'CyclicController172.cs']}
    texts['Policy172.Generated.cs'] = insert(texts['Policy172.Generated.cs'], '    private ProfilePoint Learned(double s)', '''    internal Policy172 Fork174() => new(a,b,w,rider,frame,surface,parameters,mass,inertia,targets,pilot);
''')
    texts['CyclicExit172.Generated.cs'] = insert(texts['CyclicExit172.Generated.cs'], '    public void ObserveApplied', '''    internal CyclicExit172 Fork174(Policy172 policy) {
        var copy=new CyclicExit172(policy,a,b,weights,tuning,spec,frame,surface,parameters,mass,inertia,targets);
        copy.plannedRelease=plannedRelease;copy.releaseReview=releaseReview;copy.lastProposed=lastProposed;return copy;
    }
''')
    texts['Tracker172.Generated.cs'] = insert(texts['Tracker172.Generated.cs'], '    public Tracking172 Choose', '''    internal Tracker172 Fork174(CyclicExit172 policy) {
        var copy=new Tracker172(policy,planner,map,strategy,tempo,spec,frame,surface,parameters,mass,inertia);
        copy.visit=visit;copy.chosen=chosen;copy.nominal=nominal;copy.replans=replans;copy.lastPlanReference=lastPlanReference;
        copy.Plans.AddRange(Plans);return copy;
    }
''')
    texts['CyclicController172.cs'] = insert(texts['CyclicController172.cs'], '    public CyclicDecision172 Choose', '''    private CyclicController172(CyclicController172 source) {
        spec=source.spec;frame=source.frame;surface=source.surface;parameters=source.parameters;mass=source.mass;inertia=source.inertia;
        targets=source.targets;policy=source.policy.Fork174();stabilizer=source.stabilizer.Fork174(policy);tracker=source.tracker.Fork174(stabilizer);
        boundary=new(frame);cache=new(frame);
    }
    internal CyclicController172 Fork174() => new(this);
    internal void Applied174(FreeRacingAction action) => stabilizer.ObserveApplied(action);
''')
    return texts

if __name__ == '__main__':
    import sys
    check = '--check' in sys.argv
    for name, text in sources().items():
        target=DEST/('Fork174.'+name)
        if check: assert target.read_text(encoding='utf-8')==text, target
        else: target.write_text(text,encoding='utf-8',newline='\n')
    print('Four #172 derivatives differ only by explicit state-fork helpers; originals untouched.')
