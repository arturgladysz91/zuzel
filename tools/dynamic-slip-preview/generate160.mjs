import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(dir,'../..');
let s=fs.readFileSync(path.join(root,'tools/robust-three-lap/Planner158.Generated.cs'),'utf8').replace('namespace ThreeLap158;','namespace Dynamic160;\nusing ThreeLap158;');
for(const name of ['Planner','Generator','Settings','Feedback'])s=s.replaceAll(name+'158',name+'160');
s=s.replaceAll('STUDY158','STUDY160');
const target=path.join(dir,'Planner160.Generated.cs');if(process.argv.includes('--check')){if(fs.readFileSync(target,'utf8')!==s)throw Error('160 finite certificate derivation drift')}else fs.writeFileSync(target,s);console.log('160 original Runtime158 and finite13/9 protocol derivation PASS');
