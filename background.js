importScripts('native-bridge.js');
importScripts('browser-capture.js');
// Keep only tiny last-seen previews. Full-size captures exist only while copying.
const THUMB_LIMIT = 4 * 1024 * 1024;
const thumbnails = new Map(), jobs = new Map();
let thumbBytes = 0, ropeOpen = false, sourceTab = null, sourceViewport = null;
let captureQueue = Promise.resolve(), nextCaptureAt = 0, persistTimer, copyBusy = false, generation = 0;
const ready = chrome.storage.session.get('quietThumbnails').then(({quietThumbnails=[]}) => {
  for (const item of quietThumbnails) putThumbnail(item);
});
function reportError(error) {
  chrome.action.setTitle({title: error.message || String(error)});
  browserPlatform.then(mac=>mac ? browserNotice(false) : nativeRequest('notice', {success:false, text:error.message || 'Please try again.'})).catch(()=>{});
}
function putThumbnail(item) {
  thumbnails.delete(item.id); thumbnails.set(item.id,item);
  thumbBytes = [...thumbnails.values()].reduce((sum,value)=>sum+value.image.length*2+512,0);
  while ((thumbBytes>THUMB_LIMIT || thumbnails.size>64) && thumbnails.size) {
    const key=thumbnails.keys().next().value;
    thumbBytes-=thumbnails.get(key).image.length*2+512; thumbnails.delete(key);
  }
}
function persist() {
  clearTimeout(persistTimer);
  persistTimer=setTimeout(()=>chrome.storage.session.set({quietThumbnails:[...thumbnails.values()]}).catch(()=>{}),800);
}
async function blobDataURL(blob) {
  const bytes=new Uint8Array(await blob.arrayBuffer());
  let string=''; for(let i=0;i<bytes.length;i+=8192) string+=String.fromCharCode(...bytes.subarray(i,i+8192));
  return `data:${blob.type};base64,${btoa(string)}`;
}
async function tinyPreview(blob) {
  const bitmap=await createImageBitmap(blob);
  try {
    const scale=Math.min(360/bitmap.width,220/bitmap.height,1);
    const canvas=new OffscreenCanvas(Math.max(1,Math.round(bitmap.width*scale)),Math.max(1,Math.round(bitmap.height*scale)));
    canvas.getContext('2d',{alpha:false}).drawImage(bitmap,0,0,canvas.width,canvas.height);
    return blobDataURL(await canvas.convertToBlob({type:'image/jpeg',quality:.62}));
  } finally {bitmap.close();}
}
function sameDocument(a,b) {
  return a&&b&&a.id===b.id&&a.url===b.url&&b.active&&!b.discarded&&b.status==='complete';
}
async function captureActive(tab,save=true) {
  const epoch=generation;
  const task=captureQueue.catch(()=>{}).then(async()=>{
    await new Promise(resolve=>setTimeout(resolve,Math.max(0,nextCaptureAt-Date.now())));
    const current=await chrome.tabs.get(tab.id);
    if(!sameDocument(tab,current)) throw new Error('The tab changed before capture. Try again.');
    nextCaptureAt=Date.now()+550;
    const data=await chrome.tabs.captureVisibleTab(tab.windowId,{format:'png'});
    if(!sameDocument(tab,await chrome.tabs.get(tab.id))) throw new Error('The tab changed during capture. Try again.');
    if(!save)return {data};
    const blob=await(await fetch(data)).blob();
    if(save && epoch===generation) {
      const image=await tinyPreview(blob);
      if(epoch===generation) {putThumbnail({id:tab.id,url:tab.url,image,at:Date.now()});persist();}
    }
    return blob;
  });
  captureQueue=task.then(()=>{},()=>{}); return task;
}
// One thumbnail after a tab is visited/loaded. No typing, scroll, mutation or app tracking.
function schedule(tabId,delay=650) {
  clearTimeout(jobs.get(tabId));
  jobs.set(tabId,setTimeout(async()=>{
    jobs.delete(tabId);
    try {
      const config=await chrome.storage.local.get('tabThumbnails');
      if(config.tabThumbnails===false || copyBusy || ropeOpen) return;
      const tab=await chrome.tabs.get(tabId);
      if(!tab.active || tab.incognito || tab.discarded || tab.status!=='complete') return;
      const recent=thumbnails.get(tabId);
      if(recent?.url===tab.url && Date.now()-recent.at<2000) return;
      await captureActive(tab);
    } catch {} // A preview is optional; clicking always attempts a fresh capture.
  },delay));
}
async function tabIcon(id) {
  try {
    const url=new URL(chrome.runtime.getURL('/_favicon/'));
    url.searchParams.set('pageUrl', (await chrome.tabs.get(id)).url || '');
    url.searchParams.set('size','32');
    return await blobDataURL(await (await fetch(url)).blob());
  } catch {return null;}
}
async function tabCards() {
  await ready;
  return Promise.all((await chrome.tabs.query({}))
    .sort((a,b)=>Number(b.id===sourceTab?.id)-Number(a.id===sourceTab?.id)||a.windowId-b.windowId||a.index-b.index)
    .filter(t=>!t.incognito&&!t.url?.startsWith(chrome.runtime.getURL('')))
    .map(async t=>{
      const preview=thumbnails.get(t.id);
      return {id:String(t.id),title:t.title||'Untitled tab',app:'Chrome',
        image:preview?.url===t.url?preview.image:null, icon:await tabIcon(t.id), domain:(()=>{try{return new URL(t.url).hostname || new URL(t.url).protocol.replace(':','');}catch{return 'Chrome';}})(),
        note:'Click to switch to this tab and copy a fresh screenshot.',current:t.id===sourceTab?.id};
    }));
}
async function pageViewport(tab) {
  try {
    const [result]=await chrome.scripting.executeScript({target:{tabId:tab.id},func:()=>({width:innerWidth,height:innerHeight,scale:devicePixelRatio})});
    return result.result;
  } catch {return null;}
}
async function open(tab) {
  if(!tab || tab.incognito) return;
  if(ropeOpen) {await nativeRequest('hide');ropeOpen=false;return;}
  sourceTab=tab;sourceViewport=await pageViewport(tab);
  chrome.scripting.executeScript({target:{tabId:tab.id},func:()=>document.getElementById('tabline-extension')?.remove()}).catch(()=>{});
  const config=await chrome.storage.local.get('desktopApps'),window=await chrome.windows.get(tab.windowId);
  await nativeRequest('show',{tabs:await tabCards(),desktop:config.desktopApps!==false,viewport:sourceViewport,
    bounds:{left:window.left,top:window.top,width:window.width,height:window.height},title:tab.title});
  ropeOpen=true;
}
async function waitUntilRendered(id) {
  const deadline=Date.now()+8000;
  let tab;
  while(Date.now()<deadline) {
    tab=await chrome.tabs.get(id);
    if(!tab.active) throw new Error('Another tab was selected before capture.');
    if(!tab.discarded && tab.status==='complete') break;
    await new Promise(resolve=>setTimeout(resolve,80));
  }
  if(tab.discarded || tab.status!=='complete') throw new Error('This page is still loading. Try again when it finishes.');
  // Let the selected renderer paint. A timeout also handles pages that suspend animation frames.
  try {
    await chrome.scripting.executeScript({target:{tabId:id},func:()=>new Promise(resolve=>{
      const timer=setTimeout(resolve,160);
      requestAnimationFrame(()=>requestAnimationFrame(()=>{clearTimeout(timer);resolve();}));
    })});
  } catch {await new Promise(resolve=>setTimeout(resolve,65));}
  return chrome.tabs.get(id);
}
async function copyTab(id, operation='copy') {
  if(await browserPlatform) return copyBrowserTab(id,operation);
  if(copyBusy) throw new Error('A screenshot is already being copied.');
  copyBusy=true;ropeOpen=false;
  for(const timer of jobs.values())clearTimeout(timer);jobs.clear();
  try {
    await nativeRequest('hide');
    const original=await chrome.tabs.get(Number(id));
    if(original.incognito) throw new Error('Private tabs are excluded.');
    await chrome.windows.update(original.windowId,{focused:true});
    if(!original.active || original.discarded) await chrome.tabs.update(original.id,{active:true});
    const tab=original.active&&!original.discarded&&original.status==='complete'?original:await waitUntilRendered(original.id);
    let capture;
    try {capture=await captureActive(tab,false);}
    catch(error) {
      if(!/^(chrome:|chrome-extension:|devtools:)/.test(tab.url||'')) throw error;
      const viewport=await pageViewport(tab) || (sourceTab?.windowId===tab.windowId?sourceViewport:null);
      const bounds=await chrome.windows.get(tab.windowId);
      const completed=await nativeRequest('browser-copy',{title:tab.title,viewport,operation,bounds:{left:bounds.left,top:bounds.top,width:bounds.width,height:bounds.height}},operation==='save'?120000:10000);
      if(completed.cancelled)return {ok:true,cancelled:true};
      return {ok:true,mode:'fresh',switched:!original.active};
    }
    const completed=await nativeRequest('clipboard',{dataUrl:capture.data,operation},operation==='save'?120000:10000);
    if(completed.cancelled)return {ok:true,cancelled:true};
    chrome.action.setTitle({title:operation==='save'?'Screenshot saved':'Screenshot copied'});
    // Compress the preview after the successful copy; never retain the full frame.
    const epoch=generation; fetch(capture.data).then(response=>response.blob()).then(tinyPreview).then(image=>{if(epoch!==generation)return;putThumbnail({id:tab.id,url:tab.url,image,at:Date.now()});persist();}).catch(()=>{});
    return {ok:true,mode:'fresh',switched:!original.active};
  } finally {copyBusy=false;}
}
async function handleNativeEvent(message) {
  if(message.event==='closed') {ropeOpen=false;return;}
  if(message.event==='copy-tab') {try{await copyTab(message.id,message.operation==='save'?'save':'copy');}catch(error){reportError(error);}}
  if(message.event==='capture-error') chrome.action.setTitle({title:message.error||'Screenshot failed'});
}
async function openWithSetup(tab) {
  if(await browserPlatform) return openBrowserRope(tab);
  try {await open(tab);}
  catch(error) {
    reportError(error);
    if(/native messaging host|native host|companion disconnected|specified native|not found/i.test(error.message||''))
      await chrome.runtime.openOptionsPage();
  }
}
async function initialize() {
  await ready;
  if(!await browserPlatform) {try{await nativeRequest('ping');}catch{}}
  for(const tab of await chrome.tabs.query({active:true})) schedule(tab.id,900);
}
chrome.commands.onCommand.addListener(async command=>{
  if(command==='toggle-tabline') {try{const [tab]=await chrome.tabs.query({active:true,lastFocusedWindow:true});await openWithSetup(tab);}catch(error){reportError(error);}}
});
chrome.action.onClicked.addListener(tab=>openWithSetup(tab).catch(reportError));
chrome.tabs.onActivated.addListener(({tabId})=>schedule(tabId));
chrome.tabs.onUpdated.addListener((id,change)=>{if(change.url){thumbnails.delete(id);persist();}if(change.status==='complete')schedule(id);});
chrome.tabs.onRemoved.addListener(id=>{clearTimeout(jobs.get(id));jobs.delete(id);thumbnails.delete(id);persist();});
chrome.runtime.onStartup.addListener(initialize);
chrome.runtime.onInstalled.addListener(() => {
  initialize().catch(()=>{});
});
chrome.runtime.onMessage.addListener((message,sender,reply)=>{
  if(sender.id!==chrome.runtime.id || message.target==='offscreen') return;
  (async()=>{
    if(message.type==='CLIPBOARD_READY' || message.type==='CLIPBOARD_DONE') {
      if(sender.url!==chrome.runtime.getURL('clipboard.html') || !clipboardJob)throw new Error('No clipboard operation pending.');
      if(message.type==='CLIPBOARD_READY')return {dataUrl:clipboardJob.dataUrl};
      message.ok?clipboardJob.resolve():clipboardJob.reject(new Error(message.error||'Clipboard write failed.'));
      return {ok:true};
    }
    if(message.type==='BROWSER_CARDS') {
      if(!await browserPlatform) throw new Error('The browser picker is available on Mac.');
      return {ok:true,cards:await browserCards()};
    }
    if(message.type==='BROWSER_CLOSED') {ropeOpen=false;return {ok:true};}
    if(message.type==='BROWSER_CAPTURE') {
      if(!await browserPlatform) throw new Error('Use the Windows rope to capture.');
      return copyBrowserTab(message.tabId,message.operation==='save'?'save':'copy');
    }
    if(await browserPlatform && ['COMPANION_STATUS','COMPANION_SETUP','MAC_SETUP'].includes(message.type))
      return {ok:true,platform:'mac',mode:'browser',version:chrome.runtime.getManifest().version};
    if(message.type==='COMPANION_STATUS') return nativeRequest('ping', {}, message.setup ? 1500 : 10000);
    if(message.type==='COMPANION_SETUP') return nativeRequest('setup');
    if(message.type==='CLEAR_PREVIEWS') {
      generation++;for(const timer of jobs.values())clearTimeout(timer);jobs.clear();clearTimeout(persistTimer);
      await captureQueue;thumbnails.clear();thumbBytes=0;await chrome.storage.session.remove('quietThumbnails');if(!await browserPlatform)await nativeRequest('clear');return {ok:true};
    }
    if(message.type==='COPY') return copyTab(message.tabId,message.operation==='save'?'save':'copy');
    if(message.type==='STATS') return {ok:true,thumbBytes,frameBytes:0,thumbnails:thumbnails.size,ropeOpen};
    return {ok:true};
  })().then(reply,error=>{if(message.type!=='COMPANION_STATUS')reportError(error);reply({ok:false,error:error.message});});return true;
});
initialize().catch(()=>{});

