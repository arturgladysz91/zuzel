import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {writeFileSync} from 'node:fs';
import {gunzipSync} from 'node:zlib';

const [lib,chrome,url,out,mode]=process.argv.slice(2);
const {chromium}=createRequire(import.meta.url)(lib);
const browser=await chromium.launch({executablePath:chrome,headless:true});
const page=await browser.newPage({viewport:{width:1500,height:1100}});
const errors=[];
page.on('pageerror',e=>errors.push(e.message));
page.on('console',m=>{if(m.type()==='error')console.error(m.text());});
try {
  await page.goto(url);
  await page.locator('#track[data-riders]').waitFor();
  assert.equal(await page.locator('#race-controller').inputValue(),'base');
  await page.selectOption('#race-controller','speedway200');
  assert.equal(await page.locator('#race-count').inputValue(),'1');
  assert.equal(await page.locator('#ratio-1').inputValue(),'8');
  let entry;
  if(mode==='replay') {
    const res=await page.request.get(new URL('/api/races',url).href);
    entry=(await res.json()).find(e=>e.controller==='speedway200' && e.complete);
    assert(entry);
    await page.selectOption('#scenario','h25-'+entry.id);
  } else {
    const response=page.waitForResponse(r=>r.request().method()==='POST' && r.url().endsWith('/api/races'),{timeout:120000});
    await page.locator('#race-run').click();
    const res=await response;
    entry=await res.json();
    assert.equal(res.status(),201,JSON.stringify(entry));
  }
  assert.equal(entry.controller,'speedway200');
  assert.equal(entry.complete,true);
  await page.waitForFunction(id=>document.querySelector('#scenario').value==='h25-'+id,entry.id);
  await page.locator('#load-state').waitFor({state:'hidden'});
  const raw=await page.request.get(new URL('/api/races/'+entry.id,url).href);
  const replay=JSON.parse(gunzipSync(await raw.body()));
  const times=replay.document.Run.Riders[0].Measurement.CrossingSeconds;
  assert.equal(times.length,3);
  assert(Math.abs(times[2]-46.38686288723425)<1e-6,'Integration changed the #200 nominal result');
  await page.locator('#h25-comparison > summary').click();
  await page.selectOption('#h25-compare','h25-198-solo');
  await page.locator('#scrub').evaluate(x=>{x.value='39';x.dispatchEvent(new Event('input',{bubbles:true}));});
  await page.waitForFunction(()=>document.querySelector('#track').dataset.comparison);
  await page.screenshot({path:out+'/integracja-stadion.png'});
  assert.deepEqual(errors,[]);
  writeFileSync(out+'/integracja.json',JSON.stringify({entry,times,browser:browser.version(),compared198:true},null,2));
  console.log('PASS normal stadium POST -> live C #200 -> three laps, nominal parity, comparison #198.');
} finally {await browser.close();}
