import {loadPlanning146,setupPlanning146} from './planning146.mjs';
import {readGzip} from './recordings.mjs';
export const contingency151Root='../../docs/experiments/contingency-recourse/';
export async function loadContingency151(fetcher=fetch,name=null,root=contingency151Root){
 const data=await loadPlanning146(fetcher,name,root);data.studyNumber=151;return data;
}
export function compactTree151(tree){
 return {time:tree.Time,reason:tree.Reason,conditional_certificate:tree.ConditionalCertificate,independent_preflight:tree.Independent,irrevocable_until:tree.IrrevocableUntil,
  nodes:tree.Tree.map(n=>({id:n.Id,parent:n.Parent,observation_sign:n.Sign,time:n.Observed.Input.Time,command:n.Action,activation:n.Observed.Input.Time+.24,execution_legal:n.Execution.Legal,terminal:n.TerminalPlan})),
  terminal_reserves:tree.Terminals.map(t=>({id:t.Id,node:t.Node,legal:t.Tape.Safety.Legal,end_reference:t.Tape.States.at(-1).Input.Rider.TotalReferenceMeters,arrival:t.Tape.Arrival})),cpu:tree.Profile};
}
export function setupContingency151(element,data,...callbacks){
 setupPlanning146(element,data,...callbacks);
 const note=document.createElement('p');note.style.fontWeight='bold';note.textContent=data.index.Note+' Certyfikat obejmuje skończone scenariusze; wykonanie należy do natywnego Box2D. Przyszła korekta aktywuje się dopiero po0,24s.';element.prepend(note);
 const audit=data.analysis146.plan_audits[data.run.Name];if(!audit)return;
 const details=document.createElement('details'),summary=document.createElement('summary');summary.textContent='Warunkowe drzewo obserwacji, komendy i manewry rezerwowe';details.append(summary);
 const select=document.createElement('select'),button=document.createElement('button'),output=document.createElement('pre');output.style.whiteSpace='pre-wrap';
 for(const row of audit.rows){const option=document.createElement('option');option.value=row.tree_file;option.textContent=row.time_s.toFixed(2)+'s · '+row.nodes+' węzłów / '+row.leaves+' rezerw';select.append(option);}
 button.textContent='Pokaż zachowane drzewo';button.addEventListener('click',async()=>{try{const r=await fetch(data.root+select.value);if(!r.ok)throw Error('Brak drzewa');const tree=JSON.parse(await readGzip(r));output.textContent=JSON.stringify(compactTree151(tree),null,2);}catch(error){output.textContent=String(error);}});
 details.append(select,button,output);element.append(details);
}
