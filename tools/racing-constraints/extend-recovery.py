"""Freeze two additional combinations after isolated failures, before any held run.
No scoring, physics, existing configuration or measured result is changed.
"""
import hashlib,json,sys
from pathlib import Path
path=Path(sys.argv[1]);r=json.loads(path.read_text(encoding='utf-8'))
assert r.get('CombinationsComplete') and not r.get('ConfirmationLock')
assert 'CombinationExtension' not in r and all(q['Complete'] for q in r['Reservations'])
old=r['RecoveryCombinationLock'];prior=json.loads(json.dumps(old))
added=[]
for name in ['C','D']:
    rows=[q for q in r['Native'] if q['Variant']['Id']==name and q['Pool']=='selection']
    assert len(rows)==4 and not any(q['Completed'] for q in rows)
    variant={'Id':'KR-'+name,'Changes':[name],'Recovery':True,'NominalHierarchy':False}
    assert not any(q['Id']==variant['Id'] for q in old['Chosen'])
    added.append(variant)
r['CombinationExtension']={
    'FrozenBeforeAdditionalCombinationWorlds':True,'BeforeAnyHeldWorld':True,
    'Reason':'Global direction/yaw removal each failed0/4. Test whether physical continuation can replace some stabilizing role, rather than declaring the old penalties necessary from those failures.',
    'BeforeWorldCount':len(r['Reservations']),'OriginalRecoveryCombinationLock':prior,'Added':added,
    'NoScoringOrPhysicsCodeChanged':True,'MaximumExtraWorlds':8,'HardWorldLimit':160,'HardCpuLimit':1800,
    'ScriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper(),
    'FallbackCaveat':'R may explicitly fall back to originalA when bounded recovery cannot certify a root. This is a combined consequence/fallback controller, not proof that the removed penalty is absent from every actual fallback choice.'}
old['Chosen']+=added
path.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print('Frozen KR-C/KR-D extension; run combinations again, then confirmation. Existing native reservations are cached, never repeated.')
