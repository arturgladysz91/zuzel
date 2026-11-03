from audit162 import R,D,read,save
import json

def main():
 a=read(D/'profile161.json');results=[]
 for n in a['runs']:
  counts=[]
  for ref in n['input_files']:
   q=read(R/ref['path'])['Review'];physical=[]
   for t in q['Terminals']:
    p=q['Tapes'][t['Tape']];physical.append(json.dumps({k:p[k] for k in ['Start','Actions','Goal','SafetyGoal']},sort_keys=True,separators=(',',':')))
   counts.append(len(set(physical)))
  results.append(dict(label=n['label'],minimum_unique=min(counts),maximum_unique=max(counts),duplicate_full_input_tuples=sum(9-c for c in counts),review_count=len(counts)))
 save(D/'repeat-inputs161.json',dict(scope='Read-only exact serialized full Start + orderedActions + Goal + SafetyGoal within one frozen configuration; excludes arrival/safety metadata from identity. No new forecasts.',runs=results))
if __name__=='__main__':main()
