// Pure recorded-data interpolation; no predictor, body step or extrapolation.
export function endTime156(runs){return Math.ceil(Math.max(1,...runs.map(r=>r.samples.at(-1).t))*100)/100;}
export function sample156(samples,time){
 if(!samples.length||!Number.isFinite(time))throw new Error('Recorded samples and finite time required');
 const first=samples[0],last=samples.at(-1);
 if(time<=first.t)return {...first,ended:false};
 if(time>=last.t)return {...last,ended:true};
 let lo=0,hi=samples.length-1;
 while(hi-lo>1){const mid=(lo+hi)>>1;if(samples[mid].t<=time)lo=mid;else hi=mid;}
 const a=samples[lo],b=samples[hi],u=(time-a.t)/(b.t-a.t);
 const out={...a,t:time,ended:false};
 for(const key of ['x','y','speed','s','gap','beta'])out[key]=a[key]+(b[key]-a[key])*u;
 out.heading=a.heading+Math.atan2(Math.sin(b.heading-a.heading),Math.cos(b.heading-a.heading))*u;
 return out;
}
