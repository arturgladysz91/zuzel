// Display interpolation only. A DNF freezes at its last actual native point.
export function sample158(samples,time){
 if(!samples.length||!Number.isFinite(time))throw Error('Finite time and recorded samples required');
 if(time>=samples.at(-1).t-1e-9)return {...samples.at(-1),ended:true};
 if(time<=samples[0].t)return {...samples[0],ended:false};
 let lo=0,hi=samples.length-1;while(hi-lo>1){let m=(lo+hi)>>1;if(samples[m].t<=time)lo=m;else hi=m;}
 const a=samples[lo],b=samples[hi],u=(time-a.t)/(b.t-a.t),out={...a,t:time,ended:false};
 for(const key of ['x','y','s','speed','beta','gap'])out[key]=a[key]+u*(b[key]-a[key]);
 out.heading=a.heading+Math.atan2(Math.sin(b.heading-a.heading),Math.cos(b.heading-a.heading))*u;
 return out;
}
export function endTime158(runs){return Math.ceil(Math.max(1,...runs.map(r=>r.samples.at(-1).t))*100-1e-9)/100;}
