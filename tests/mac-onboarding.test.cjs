const test=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs');const vm=require('node:vm');
function fixture(os='mac',status={ok:true,platform:'mac',permissions:{screenRecording:true}}){
 const opened=[],focused=[];let tabs=[],error=false;
 const context={chrome:{runtime:{getPlatformInfo:async()=>({os}),getURL:path=>'chrome-extension://'+'a'.repeat(32)+'/'+path},tabs:{query:async()=>tabs,create:async options=>{opened.push(options);return options},update:async(...args)=>{focused.push(args)}},windows:{update:async()=>{}}},nativeRequest:async()=>{if(error)throw Error('Specified native messaging host not found');return status}};
 vm.createContext(context);vm.runInContext(fs.readFileSync('mac-onboarding.js','utf8'),context);
 return {opened,focused,setTabs:value=>tabs=value,fail:()=>error=true,run:code=>vm.runInContext(code,context)};
}
test('Mac installation opens setup automatically when companion is missing',async()=>{const f=fixture();f.fail();await f.run('MacOnboarding.installed()');assert.match(f.opened[0].url,/mac-setup.html\?install=1$/)});
test('Ready Mac and Windows installations do not open an installer',async()=>{for(const os of ['mac','win']){const f=fixture(os);await f.run('MacOnboarding.installed()');assert.equal(f.opened.length,0);assert.equal(await f.run('MacOnboarding.beforeOpen()'),true)}});
test('Mac screen permission is a required setup step before the rope opens',async()=>{const f=fixture('mac',{ok:true,platform:'mac',permissions:{screenRecording:false}});assert.equal(await f.run('MacOnboarding.beforeOpen()'),false);assert.equal(f.opened.length,1)});
test('Concurrent setup requests share one page and later attempts focus it',async()=>{const f=fixture();f.fail();await Promise.all([f.run('MacOnboarding.open(true)'),f.run('MacOnboarding.open(true)')]);assert.equal(f.opened.length,1);f.setTabs([{id:42,windowId:7,url:f.opened[0].url}]);await f.run('MacOnboarding.open(true)');assert.equal(f.opened.length,1);assert.equal(f.focused[0][0],42)});
