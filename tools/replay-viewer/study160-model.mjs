// Only saved states. A DNF remains at the last actual sample.
export function sample160(run,time){
 if(!run.samples?.length||!Number.isFinite(time))throw Error('Recorded samples and finite time required');
 const ss=run.samples;let lo=0,hi=ss.length-1;
 if(time<=ss[0].t)return {sample:ss[0],ended:time>=ss[hi].t};
 if(time>=ss[hi].t)return {sample:ss[hi],ended:true};
 while(hi-lo>1){const mid=(hi+lo)>>1;if(ss[mid].t<=time)lo=mid;else hi=mid;}
 return {sample:ss[lo],ended:false};
}
export function clock160(run,time,advance){
 if(!Number.isFinite(time)||!Number.isFinite(advance)||advance<0)throw Error('Finite nonnegative advance required');
 return Math.min(run.samples.at(-1).t,Math.max(run.samples[0].t,time+advance));
}
