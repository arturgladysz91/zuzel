import {loadPlanning146,setupPlanning146} from './planning146.mjs';
import {readGzip} from './recordings.mjs';
export const efficient152Root='../../docs/experiments/efficient-contingency/';
export async function loadEfficient152(fetcher=fetch,name=null,root=efficient152Root){const data=await loadPlanning146(fetcher,name,root);data.studyNumber=152;return data;}
export function compactPolicy152(tree){
 const tape=id=>tree.Tapes[id];
 return {format:tree.Format,time:tree.Time,reason:tree.Reason,conditional_certificate:tree.ConditionalCertificate,independent_preflight:tree.Independent,physical_hash:tree.PhysicalHash,
 nodes:tree.Tree.map(n=>({id:n.Id,parent:n.Parent,sign:n.Sign,depth:n.Depth,time:n.Observed.Input.Time,command:n.Action,activation:n.Observed.Input.Time+.24,legal:n.Execution.Legal})),
 terminal_reserves:tree.Terminals.map(t=>({id:t.Id,node:t.Node,tape:t.Tape,legal:tape(t.Tape).Safety.Legal,start:tape(t.Tape).Start,actions:tape(t.Tape).Actions,goal:tape(t.Tape).Goal,safety_goal:tape(t.Tape).SafetyGoal,arrival:tape(t.Tape).Arrival})),shared_tapes:Object.keys(tree.Tapes).length,cpu:tree.Profile};
}
export function setupEfficient152(element,data,...callbacks){
 setupPlanning146(element,data,...callbacks);const note=document.createElement('p');note.textContent=data.index.Note;element.prepend(note);const audit=data.analysis146.plan_audits[data.run.Name];if(!audit)return;
 const details=document.createElement('details'),summary=document.createElement('summary');summary.textContent='Wspólne polityki, dziewięć rezerw i niezależna certyfikacja';details.append(summary);const select=document.createElement('select'),button=document.createElement('button'),output=document.createElement('pre');output.style.whiteSpace='pre-wrap';
 for(const row of audit.rows){const option=document.createElement('option');option.value=row.tree_file;option.textContent=row.time_s.toFixed(2)+'s · '+row.nodes+' / '+row.leaves;select.append(option);}button.textContent='Pokaż zapis polityki';button.addEventListener('click',async()=>{try{output.textContent=JSON.stringify(compactPolicy152(JSON.parse(await readGzip(await fetch(data.root+select.value)))),null,2);}catch(error){output.textContent=String(error);}});details.append(select,button,output);element.append(details);
}
