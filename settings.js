const $=selector=>document.querySelector(selector);
let platform='win';
$('#extension-id').textContent=chrome.runtime.id;
async function refresh(){
 const config=await chrome.storage.local.get(['tabThumbnails','desktopApps']);
 $('#previews').textContent=config.tabThumbnails===false?'Enable last-seen tab thumbnails':'Disable last-seen tab thumbnails';
 $('#desktop').textContent=(config.desktopApps===false?'Show ':'Hide ')+(platform==='mac'?'Mac app windows':'Windows apps');
}
async function check(){
 $('#check').disabled=true;
 try {
  const result=await chrome.runtime.sendMessage({type:'COMPANION_STATUS'});
  if(!result.ok){$('#desktop-status').textContent='Companion not connected. Complete the setup below. '+(result.error||'');return;}
  const name=result.platform==='mac'?'Mac':'Windows';
  $('#desktop-status').textContent=`${name} companion connected · ${result.version}`;
  if(result.platform==='mac'&&result.permissions?.screenRecording===false)
   $('#desktop-status').textContent+=' · Allow Screen Recording to finish Mac setup and include desktop app windows.';
 }catch(error){$('#desktop-status').textContent='Could not check the companion: '+error.message;}
 finally{$('#check').disabled=false;}
}
$('#copy-id').onclick=async()=>{try{await navigator.clipboard.writeText(chrome.runtime.id);$('#desktop-status').textContent='Extension ID copied.';}catch{$('#desktop-status').textContent='Select and copy the extension ID shown above.';}};
$('#previews').onclick=async()=>{const config=await chrome.storage.local.get('tabThumbnails');await chrome.storage.local.set({tabThumbnails:config.tabThumbnails===false});$('#preview-status').textContent='Updated. A thumbnail is optional; clicking always attempts a fresh screenshot.';await refresh();};
$('#desktop').onclick=async()=>{const config=await chrome.storage.local.get('desktopApps');await chrome.storage.local.set({desktopApps:config.desktopApps===false});$('#desktop-status').textContent='Updated. Reopen the rope to apply.';await refresh();};
$('#check').onclick=check;
$('#setup-mac').onclick=async()=>{try{const result=await chrome.runtime.sendMessage({type:'COMPANION_SETUP'});if(!result.ok)$('#desktop-status').textContent='Install and open the Mac companion first. '+result.error;}catch(error){$('#desktop-status').textContent=error.message;}};
$('#download-mac').onclick=()=>chrome.runtime.sendMessage({type:'MAC_SETUP'});
$('#shortcuts').onclick=()=>chrome.tabs.create({url:'chrome://extensions/shortcuts'});
$('#clear').onclick=async()=>{const result=await chrome.runtime.sendMessage({type:'CLEAR_PREVIEWS'});$('#status').textContent=result.ok?'Last-seen thumbnails cleared.':result.error;};
(async()=>{
 const info=await chrome.runtime.getPlatformInfo();platform=info.os;
 const mac=platform==='mac';
 $('#developer-connection').hidden=mac;
 $('#download-mac').textContent='Finish Mac setup';
 $('#mac-setup').hidden=!mac;$('#windows-setup').hidden=mac;$('#setup-mac').hidden=!mac;$('#download-mac').hidden=!mac;
 $('#companion-heading').textContent=mac?'Your Mac companion.':'Your Windows companion.';
 $('#shortcut-label').textContent=mac?'Option + S':'Alt + S';$('#paste-label').textContent=mac?'Command + V':'Ctrl + V';
 $('#app-preview-info').textContent=mac?'Mac app previews are small, on-demand snapshots while the rope is open. Screen Recording permission is needed for desktop windows.':'App previews use Windows’ built-in compositor while the rope is open. They are released on close.';
 await refresh();await check();
})().catch(error=>{$('#desktop-status').textContent=error.message;});
