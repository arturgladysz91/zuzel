import {loadPlanning146,setupPlanning146} from './planning146.mjs';
export const speed148Root='../../docs/experiments/planner-speed-root-cause/';
export async function loadSpeed148(fetcher=fetch,name=null,root=speed148Root){const d=await loadPlanning146(fetcher,name,root);d.studyNumber=148;return d;}
export function setupSpeed148(element,data,...callbacks){setupPlanning146(element,data,...callbacks);const p=document.createElement('p');p.textContent='Dokładny wariant #148 zachowuje całą trajektorię #147 i zmniejsza koszt obliczeń. Impulsy gazu i warianty geometrii są osobnymi diagnostykami; nie stanowią autonomicznej poprawy sportowej.';element.append(p);}
