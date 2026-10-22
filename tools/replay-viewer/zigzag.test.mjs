import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import { nativeAt, loadZigzag, zigzagFile } from './zigzag.mjs';
const root=new URL('../../docs/experiments/',import.meta.url);
const data=JSON.parse(gunzipSync(readFileSync(new URL('first-bend-zigzag/native-telemetry.json.gz',root))));
function rows(name){const text=gunzipSync(readFileSync(new URL(`predictive-rider-control/${name}.csv.gz`,root))).toString();const [head,...lines]=text.trim().split(/\r?\n/),keys=head.split(',');return lines.map(line=>Object.fromEntries(line.split(',').map((v,i)=>[keys[i],Number(v)])));}
test('zigzag telemetry keeps every native pose/control and the project beta sign without forecast substitution',()=>{
  assert.equal(data.BetaConvention,'psi-chi');
  for(const [name,source] of [['baseline','baseline'],['candidate','controlled/candidate']]){
    const rr=rows(source),pp=rows(source+'-plan'),series=data.Series[name];assert.equal(rr.length,series.length);
    for(let i=0;i<rr.length;i++){
      const r=rr[i],n=series[i];for(const k of ['t','dt','s','x','y'])assert.equal(n[k],r[k]);
      assert.equal(n.drive,r.drive*100);assert.equal(n.steering,r.steer_before*180/Math.PI);assert.equal(n.request,r.steer_request*180/Math.PI);
      assert.equal(n.beta,r.beta*180/Math.PI);assert.equal(n.error,pp[i].normal_error);assert.equal(n.speed,r.speed*3.6);
      assert.ok(Math.abs((n.psi-n.chi)-n.beta)<.00005);assert.equal(n.yaw,r.body_omega);
      assert.equal(n.front_force,r.front_lateral);assert.equal(n.rear_capacity,pp[i].rear_capacity);
    }
  }
});
test('zigzag seeks step native snapshots and retains their own sample time at shared cursor',()=>{
  const rows=data.Series.candidate;assert.equal(nativeAt(rows,-1),rows[0]);assert.equal(nativeAt(rows,99),rows.at(-1));
  for(const t of [1.44,1.84,4.15,5.44,5.54,6.43,10.15]){const r=nativeAt(rows,t);assert.ok(r.t<=t+1e-9);assert.ok(t-r.t<.010001);}
  const r=nativeAt(rows,5.445);assert.equal(r,nativeAt(rows,5.44));assert.notEqual(r,nativeAt(rows,5.45));
});
test('zigzag loader requests only labelled native telemetry and preserves all actual activation times',async()=>{
  const calls=[];const loaded=await loadZigzag(async url=>{calls.push(url);return new Response(readFileSync(new URL('first-bend-zigzag/native-telemetry.json.gz',root)));});
  assert.deepEqual(calls,[zigzagFile]);assert.deepEqual(loaded.Activations.map(a=>Number(a.ActualActivation.toFixed(2))),[1.44,1.84,5.44,5.84,6.24,6.64,7.04,7.44,10.64,11.04]);
  await assert.rejects(()=>loadZigzag(async()=>new Response('',{status:404})),/telemetrii/);
});
