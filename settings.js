const $=selector=>document.querySelector(selector);
$('#extension-id').textContent=chrome.runtime.id;
async function refresh(){const config=await chrome.storage.local.get(['tabThumbnails','desktopApps']);$('#previews').textContent=config.tabThumbnails===false?'Enable last-seen tab thumbnails':'Disable last-seen tab thumbnails';$('#desktop').textContent=config.desktopApps===false?'Show Windows apps':'Hide Windows apps';}
$('#copy-id').onclick=async()=>{await navigator.clipboard.writeText(chrome.runtime.id);$('#desktop-status').textContent='Extension ID copied.';};
$('#previews').onclick=async()=>{const config=await chrome.storage.local.get('tabThumbnails');await chrome.storage.local.set({tabThumbnails:config.tabThumbnails===false});$('#preview-status').textContent='Updated. A thumbnail is optional; clicking always attempts a fresh screenshot.';await refresh();};
$('#desktop').onclick=async()=>{const config=await chrome.storage.local.get('desktopApps');await chrome.storage.local.set({desktopApps:config.desktopApps===false});$('#desktop-status').textContent='Updated. Reopen the rope to apply.';await refresh();};
$('#check').onclick=async()=>{const result=await chrome.runtime.sendMessage({type:'COMPANION_STATUS'});$('#desktop-status').textContent=result.ok?'Windows companion connected · '+result.version:'Run companion\\Install.cmd with the extension ID above. '+result.error;};
$('#shortcuts').onclick=()=>chrome.tabs.create({url:'chrome://extensions/shortcuts'});
$('#clear').onclick=async()=>{const result=await chrome.runtime.sendMessage({type:'CLEAR_PREVIEWS'});$('#status').textContent=result.ok?'Last-seen thumbnails cleared.':result.error;};
refresh();
