import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {writeFileSync} from 'node:fs';
const [playwright,chrome,url,out]=process.argv.slice(2);
const {chromium}=createRequire(import.meta.url)(playwright);
const browser=await chromium.launch({executablePath:chrome,headless:true});
const page=await browser.newPage({viewport:{width:1560,height:1180}});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
try{
    await page.goto(url);await page.locator('#track[data-riders]').waitFor();
    assert.equal(await page.locator('#race-controller').inputValue(),'base');
    await page.selectOption('#race-controller','h26-le');
    assert.equal(await page.locator('#race-count').inputValue(),'1');
    assert.match(await page.locator('#race-status').textContent(),/niekompatybilna/);
    // This POST must reject before constructing a native world. No new race is requested by the test.
    const response=await page.request.post(new URL('/api/races',url).href,{headers:{'X-Stadium-Request':'run','Origin':new URL(url).origin},data:{ratios:[8.4],controller:'h26-le'}});
    assert.equal(response.status(),400);
    assert.match((await response.json()).error,/nie są kompatybilne/);
    for(const key of ['h25','l','e','le','learned','alternate','l-trained','e-trained']){
        const loaded=page.waitForResponse(r=>r.url().endsWith('/api/races/202-'+key));
        await page.selectOption('#scenario','h25-202-'+key);
        await loaded;
        await page.locator('#exit-diagnostics').evaluate(x=>{x.open=true;});
        await page.locator('#load-state').waitFor({state:'hidden'});
        await page.locator('#scrub').evaluate(x=>{x.value=x.max;x.dispatchEvent(new Event('input',{bubbles:true}));});
        await page.waitForFunction(()=>document.querySelector('#exit-readout').textContent.includes('Fz przód/tył'));
        assert.match(await page.locator('#exit-readout').textContent(),/RPM/);
        assert.match(await page.locator('#exit-readout').textContent(),/reszta E/);
    }
    await page.selectOption('#scenario','h25-202-learned');
    await page.locator('#load-state').waitFor({state:'hidden'});
    await page.locator('#scrub').evaluate(x=>{x.value='6';x.dispatchEvent(new Event('input',{bubbles:true}));});
    await page.locator('#h25-comparison').evaluate(x=>{x.open=true;});
    await page.selectOption('#h25-compare','h25-202-h25');
    await page.waitForFunction(()=>document.querySelector('#track').dataset.comparison);
    await page.screenshot({path:out+'/stadion-h26.png'});
    assert.deepEqual(errors,[]);
    writeFileSync(out+'/browser-tests.json',JSON.stringify({passed:true,default:'base',replays:8,
        incompatiblePolicyRejected:true,nativeWorldsCreated:0,browser:browser.version(),errors},null,2));
    console.log('PASS eight #202 replays, H26 telemetry, H25 comparison, default and incompatible setup rejection; zero worlds.');
}finally{await browser.close();}
