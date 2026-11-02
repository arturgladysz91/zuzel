// Select an actual saved native sample. Never interpolate a physical state.
export function sample159(samples,time){
 if(!samples.length||!Number.isFinite(time))throw Error('Recorded samples and finite time required');
 let lo=0,hi=samples.length-1;
 if(time<=samples[0].t)return {sample:samples[0],index:0,ended:false};
 if(time>=samples[hi].t)return {sample:samples[hi],index:hi,ended:true};
 while(hi-lo>1){const mid=(lo+hi)>>1;if(samples[mid].t<=time)lo=mid;else hi=mid;}
 return {sample:samples[lo],index:lo,ended:false};
}
