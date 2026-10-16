const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '..', 'settings.js'), 'utf8');
async function settings(os, status) {
  const elements = new Map();
  const messages = [];
  const context = {
    document: {querySelector(selector) {
      if (!elements.has(selector)) elements.set(selector, {textContent:'', hidden:false, disabled:false});
      return elements.get(selector);
    }},
    chrome: {
      runtime: {id:'a'.repeat(32), getPlatformInfo:async()=>({os}), sendMessage:async(message)=>{messages.push(message);return status;}},
      storage: {local: {get:async()=>({}), set:async()=>{}}},
      tabs: {create:async()=>{}},
    }, navigator: {clipboard: {writeText:async()=>{}}},
  };
  vm.runInNewContext(source, context);
  await new Promise(resolve=>setImmediate(resolve));
  return {get:selector=>elements.get(selector), messages};
}
test('Mac settings are ready without a companion or native connection check', async()=>{
  const ui=await settings('mac',{ok:true, platform:'mac', version:'2.3.0', permissions:{screenRecording:false}});
  assert.equal(ui.get('#windows-setup').hidden,true);
  assert.equal(ui.get('#mac-setup').hidden,true);
  assert.equal(ui.get('#shortcut-label').textContent,'Option + S');
  assert.match(ui.get('#desktop-status').textContent,/Chrome tabs only/);
  assert.equal(ui.get('#developer-connection').hidden,true);
  assert.equal(ui.get('#check').disabled,false);
  assert.equal(ui.get('#setup-mac').hidden,true);
  assert.equal(ui.get('#download-mac').hidden,true);
  assert.equal(ui.get('#desktop').hidden,true);
  assert.equal(ui.messages.length,0);
});
test('Existing Windows host without a platform field retains Windows setup',async()=>{
  const ui=await settings('win',{ok:true,version:'2.2.0'});
  assert.equal(ui.get('#windows-setup').hidden,false);
  assert.equal(ui.get('#setup-mac').hidden,true);
  assert.equal(ui.get('#download-mac').hidden,true);
  assert.match(ui.get('#desktop-status').textContent,/Windows companion connected/);
  assert.equal(ui.get('#paste-label').textContent,'Ctrl + V');
});
test('Missing Mac host does not block the Chrome-only edition',async()=>{
  const ui=await settings('mac',{ok:false,error:'Native host not found'});
  assert.match(ui.get('#desktop-status').textContent,/No companion/);
  assert.equal(ui.get('#check').disabled,false);
});
