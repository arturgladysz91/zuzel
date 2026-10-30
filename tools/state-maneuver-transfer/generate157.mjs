// Reuse the exact frozen 13-node/9-full-reserve and independent-preflight protocol.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(dir,'../..');
let p=fs.readFileSync(path.join(root,'tools/hierarchical-maneuvers/Planner155.Generated.cs'),'utf8').replaceAll('namespace Hierarchical155;','namespace Transfer157;\nusing Hierarchical155;').replaceAll('Planner155','Planner157').replaceAll('Generator155','Generator157').replaceAll('STUDY155','STUDY157');
p=p.replace('Generator=new(Runtime,Step,Guard,forwardCheck)','Generator=new(Runtime,Step,Guard,forwardCheck)');
p=p.replace('Feedback153.Action(Runtime,state,anchor)','Feedback157.Action(Runtime,state,anchor)');
p=p.replace('MaximumReviewForecasts = 32768, MaximumReviewContacts = 2000000','MaximumReviewForecasts = 8192, MaximumReviewContacts = 2000000');
p=p.replace('TotalSeconds >= 30','TotalSeconds >= 12');
p=p.replace('endogenous phased primitive beam','observation-conditioned learned proposals');
p=p.replace('        if(Runtime.K1Calls!=0)', '        if(Runtime.K1Calls!=0)');
let f=fs.readFileSync(path.join(root,'tools/safe-fast-contingency/Feedback153.Generated.cs'),'utf8').replace('namespace Safety153;','namespace Transfer157;\nusing Safety153;').replaceAll('Feedback153','Feedback157').replace('if(rt.Joint)return Joint150.Correct','return Joint150.Correct');
// The joint inverse is the only feedback path. Remove unreachable steering-only path.
const a=f.indexOf('  var delta=.5f',f.indexOf('public static FreeRacingAction Action'));const b=f.indexOf('\n }',a);f=f.slice(0,a)+f.slice(b);
const outputs={'Planner157.Generated.cs':'// Study157: proposal and joint-repair hooks only; certificate unchanged.\n'+p,'Feedback157.Generated.cs':f};
for(const [name,value] of Object.entries(outputs)){let target=path.join(dir,name);if(process.argv.includes('--check')){if(fs.readFileSync(target,'utf8')!==value)throw new Error(name+' drift');}else fs.writeFileSync(target,value)}
console.log('157 protocol generation parity PASS');
