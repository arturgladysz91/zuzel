import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import zlib from 'node:zlib';
import {loadContingency151,compactTree151} from './contingency151.mjs';
const root=new URL('../../docs/experiments/contingency-recourse/',import.meta.url);
const json=name=>JSON.parse(fs.readFileSync(new URL(name,root)));
const gz=name=>JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL(name,root))));
const fetcher=async url=>new Response(fs.readFileSync(new URL(url.replace('local/',''),root)));
test('151 default replay is actual continuous native execution from rest',async()=>{
 const data=await loadContingency151(fetcher,null,'local/');assert.equal(data.studyNumber,151);assert.equal(data.run.Name,json('index.json').DefaultRun);assert.equal(data.run.Riders[0].Samples[0].Input.Rider.Speed,0);assert.equal(data.replay.state(1,0,false).speed,0);
 assert.match(data.index.Note,/trzech legalnych natywnych/);if(data.run.Riders[0].CompletedLaps<3)assert.ok(!data.run.Riders[0].FinishSeconds||data.run.Options.RequiredLaps<3);
});
test('151 finite tree preserves all nine reserves and command activation times',()=>{
 const compact=compactTree151(gz('gate1-v4.json.gz').Review);assert.equal(compact.nodes.length,13);assert.equal(compact.terminal_reserves.length,9);assert.ok(compact.independent_preflight);assert.ok(compact.terminal_reserves.every(x=>x.legal));
 for(const node of compact.nodes)assert.ok(Math.abs(node.activation-node.time-.24)<1e-8);
 assert.ok(new Set(compact.nodes.filter(n=>n.parent===1).map(n=>n.command.Drive)).size>1);
});
test('151 native partial gates remain separately labeled',()=>{
 const result=json('native/contingency151-gate2-A-v1-result.json');assert.equal(result.GateReached,false);assert.match(result.Outcome,/ABSTAIN/);assert.equal(result.CompletedLaps,0);
 const later=json('native/contingency151-gate3-A-v1-result.json');assert.equal(later.GateReached,true);assert.equal(later.CompletedLaps,1);
});
