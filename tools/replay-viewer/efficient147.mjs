import {loadPlanning146,setupPlanning146} from './planning146.mjs';
export const efficient147Root='../../docs/experiments/efficient-autonomous-planner/';
export async function loadEfficient147(fetcher=fetch,name=null,root=efficient147Root){const data=await loadPlanning146(fetcher,name,root);data.studyNumber=147;return data;}
export function setupEfficient147(element,data,...callbacks){setupPlanning146(element,data,...callbacks);const p=document.createElement('p');p.textContent='Pełny zapis #147: wszystkie decyzje zapisane i synchronizowane na dysk przed przekazaniem komendy. Dokładny cache i odrębne zmiany strategii są porównane w raporcie.';element.append(p);}
