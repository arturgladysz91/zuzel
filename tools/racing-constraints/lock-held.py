"""Lock frozen-rule top3 plus the prospectively committed global-B comparator."""
import hashlib,json,statistics,sys
from pathlib import Path
p=Path(sys.argv[1]);r=json.loads(p.read_text(encoding='utf-8'))
assert r.get('CombinationsComplete') and r.get('CombinationExtension') and not r.get('ConfirmationLock')
assert all(q['Complete'] for q in r['Reservations']) and not any(q['Pool']=='held' for q in r['Native'])
commitment=p.parent/'held-B-commitment.json';assert json.loads(commitment.read_bytes())['BeforeAnyHeldWorld']
variants=r['Protocol']['Isolated'][1:]+r['CombinationLock']['Configurations']+r['RecoveryCombinationLock']['Chosen']
variants=list({q['Id']:q for q in variants}.values())
def rows(name):return [q for q in r['Native'] if q['Pool']=='selection' and q['Variant']['Id']==name]
baseline={q['Family']:q for q in rows('A')}
def ranking(v):
    group=rows(v['Id']);ratios=[q['FullTimeSeconds']/baseline[q['Family']]['FullTimeSeconds'] if baseline[q['Family']]['Completed'] else 1 for q in group if q['Completed']]
    return (-sum(q['Completed'] for q in group),statistics.mean(ratios) if ratios else float('inf'),-sum(q['ChangedDecisions'] for q in group),v['Id'])
ranked=sorted(variants,key=ranking);top=ranked[:3];selected=top[:]
if not any(q['Id']=='B' for q in selected):selected.append(next(q for q in variants if q['Id']=='B'))
r['ConfirmationLock']={
    'BeforeAnyHeldWorld':True,'Winners':selected,'OriginalRuleTop3':top,'Held':r['Protocol']['Held'],
    'Rule':'Original frozen143 reliability/paired-time/changed-choice ranking for top3, plus the prospectively registered mandatory global-B comparator. No held result is used.',
    'Ranking':[{'Variant':q,'Completions':-ranking(q)[0],'MeanLegalPairedTimeRatio':None if ranking(q)[1]==float('inf') else ranking(q)[1]} for q in ranked],
    'MandatoryBCommitmentSha256':hashlib.sha256(commitment.read_bytes()).hexdigest().upper(),
    'ScriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper()}
p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print('Held locked: '+', '.join(q['Id'] for q in selected)+'; original top3: '+', '.join(q['Id'] for q in top))
