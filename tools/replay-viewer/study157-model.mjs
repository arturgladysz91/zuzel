import {sample156} from './study156-model.mjs';
// A timeline tick can be one floating-point ulp before the recorded final tick.
export function sample157(samples,time){if(samples.length&&time>=samples.at(-1).t-1e-9)return {...samples.at(-1),ended:true};return sample156(samples,time)}
export function endTime157(runs){return Math.ceil(Math.max(1,...runs.map(r=>r.samples.at(-1).t))*100-1e-9)/100}
