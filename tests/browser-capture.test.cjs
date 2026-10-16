const test=require('node:test'),assert=require('node:assert/strict'),vm=require('node:vm'),fs=require('node:fs');
function fixture(os='mac',restricted=false){
 const calls=[],listeners={};let offscreen=false;
 const event=name=>({addListener(fn){listeners[name]=fn;},removeListener(){}});
 const tab={id:11,windowId:2,url:'https://example.com',title:'Example',active:true,status:'complete',index:0};
 const context={console,URL,Map,Promise,Error,Number,Date,fetch:async()=>{throw Error('no optional preview in this test');},
  setTimeout(fn,ms){return ms>500?{unused:true}:setTimeout(fn,ms);},clearTimeout(timer){if(!timer?.unused)clearTimeout(timer);},
  chrome:{
   runtime:{id:'a'.repeat(32),getURL:path=>'chrome-extension://'+'a'.repeat(32)+'/'+path,getPlatformInfo:async()=>({os}),getManifest:()=>({version:'2.4.0'}),
    getContexts:async()=>offscreen?[{}]:[],sendMessage:async message=>{calls.push(['runtime',message]);return {ok:true};},openOptionsPage:async()=>calls.push(['settings']),
    onStartup:event('startup'),onInstalled:event('installed'),onMessage:event('message')},
   commands:{onCommand:event('command')},action:{onClicked:event('action'),setTitle:async()=>{},setPopup:async details=>calls.push(['popup',details]),openPopup:async()=>calls.push(['open-popup']),setBadgeText:async()=>{},setBadgeBackgroundColor:async()=>{}},
   storage:{local:{get:async()=>({})},session:{get:async()=>({}),set:async()=>{},remove:async()=>{}}},
   tabs:{query:async()=>[tab],get:async()=>tab,update:async()=>tab,sendMessage:async(id,message)=>{calls.push(['tab-message',message]);return {ok:true,wasOpen:false};},captureVisibleTab:async()=>{calls.push(['capture']);return 'data:image/png;base64,AAAA';},onActivated:event('activated'),onUpdated:event('updated'),onRemoved:event('removed')},
   scripting:{executeScript:async details=>{calls.push(['script',details]);if(restricted)throw Error('Cannot access chrome:// URL');return [{result:{width:1000,height:700,scale:1}}];}},
   windows:{get:async()=>({}),update:async()=>{},create:async()=>calls.push(['popup-window'])},
   offscreen:{createDocument:async()=>{offscreen=true;calls.push(['clipboard-open']);},closeDocument:async()=>{offscreen=false;calls.push(['clipboard-close']);}}
  },nativeRequest:async(type,payload)=>{calls.push(['native',type,payload]);return {ok:true};}
 };
 vm.createContext(context);
 context.importScripts=(...files)=>{for(const file of files){if(file==='native-bridge.js')continue;vm.runInContext(fs.readFileSync(file,'utf8'),context);}};
 vm.runInContext(fs.readFileSync('background.js','utf8'),context);
 return {calls,tab,listeners,run:code=>vm.runInContext(code,context)};
}
test('First install and Option+S on Mac never invoke native setup or settings',async()=>{
 const f=fixture();f.listeners.installed();await f.listeners.command('toggle-tabline');
 assert.ok(f.calls.some(([kind,details])=>kind==='tab-message'&&details.type==='ROPE_SHOW'));
 assert.equal(f.calls.filter(([kind])=>kind==='native'||kind==='settings').length,0);
});
test('Protected Mac pages open a picker instead of settings',async()=>{
 const f=fixture('mac',true);await f.listeners.command('toggle-tabline');
 assert.ok(f.calls.some(([kind])=>kind==='open-popup'));
 assert.equal(f.calls.filter(([kind])=>kind==='settings'||kind==='native').length,0);
});
test('Mac copy hides UI, captures and confirms clipboard before reporting success',async()=>{
 const f=fixture();const result=await f.run('copyTab(11)');assert.equal(result.ok,true);
 const capture=f.calls.findIndex(([kind])=>kind==='capture'),write=f.calls.findIndex(([kind,message])=>kind==='tab-message'&&message.type==='ROPE_CLIPBOARD');
 assert.ok(f.calls.slice(0,capture).some(([kind,message])=>kind==='tab-message'&&message.type==='ROPE_HIDE'));
 assert.ok(write>capture);
 assert.equal(f.calls.filter(([kind])=>kind==='native').length,0);
});
test('Windows shortcut retains the native desktop rope path',async()=>{
 const f=fixture('win');await f.listeners.command('toggle-tabline');
 assert.ok(f.calls.some(([kind,type])=>kind==='native'&&type==='show'));
 assert.equal(f.calls.filter(([kind])=>kind==='open-popup').length,0);
});
test('Mac rejects invalid and private capture targets',async()=>{
 const f=fixture();await assert.rejects(f.run("copyTab('invalid')"),/Invalid tab/);
 f.tab.incognito=true;const result=await f.run('copyTab(11)');assert.equal(result.ok,false);
 assert.equal(f.calls.filter(([kind])=>kind==='capture').length,0);
});
