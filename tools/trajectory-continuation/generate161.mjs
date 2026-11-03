import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const d=path.dirname(fileURLToPath(import.meta.url));let s=fs.readFileSync(path.join(d,'../dynamic-slip-preview/Planner160.Generated.cs'),'utf8').replace('namespace Dynamic160;','namespace Continuation161;');
for(const n of ['Planner','Generator','Settings','Feedback'])s=s.replaceAll(n+'160',n+'161');s=s.replaceAll('STUDY160','STUDY161');if(fs.readFileSync(path.join(d,'Planner161.Generated.cs'),'utf8')!==s)throw Error('161 certificate drift');console.log('161 unchanged Runtime158 / finite13/9 PASS');
