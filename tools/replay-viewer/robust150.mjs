import {loadPlanning146,setupPlanning146} from './planning146.mjs';
export const robust150Root='../../docs/experiments/robust-autonomous-racing/';
export async function loadRobust150(fetcher=fetch,name=null,root=robust150Root){const d=await loadPlanning146(fetcher,name,root);d.studyNumber=150;return d;}
export function setupRobust150(element,data,...callbacks){
 setupPlanning146(element,data,...callbacks);
 const p=document.createElement('p');p.style.fontWeight='bold';
 p.textContent='PARTIAL. '+(data.run.Name.startsWith('fixed-')?'Diagnostyczny zapis stałych komend: potwierdzone przekroczenie obrysu.':data.run.Name.includes('prefix-')?'Natywny prefiks starego sterownika od zera, następnie nowy planner. Wynik nie dowodzi pełnej autonomii nowego sterownika.':data.run.Name.includes('recovery-')||data.run.Name.includes('rolling-')?'Diagnostyka ze startem 90 km/h przy referencji23m, do pierwszego przekroczenia318,779m. To nie są trzy okrążenia.':data.run.Name.includes('full-')?'Pełna autonomiczna próba od0 kończy się odmową planera po0,4s; obrys pozostaje legalny.':'Zamrożony pomiar odniesienia z badania149; nie uruchamiano go ponownie w150.');element.prepend(p);
}
