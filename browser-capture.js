// Mac uses Chrome APIs only. Windows keeps its existing native implementation.
const browserPlatform=chrome.runtime.getPlatformInfo().then(info=>info.os==='mac');
let browserOverlayTab=null,clipboardJob=null,browserOpening=false;
async function browserCards() {
  if(!sourceTab) [sourceTab]=await chrome.tabs.query({active:true,lastFocusedWindow:true});
  const cards=await tabCards();
  const order=new Map((await chrome.tabs.query({})).sort((a,b)=>a.windowId-b.windowId||a.index-b.index).map((tab,index)=>[String(tab.id),index]));
  return cards.sort((a,b)=>order.get(a.id)-order.get(b.id));
}
async function browserMessage(tabId,message) {
  try {return await chrome.tabs.sendMessage(tabId,message);} catch {return null;}
}
async function injectBrowserUI(tabId) {
  await chrome.scripting.executeScript({target:{tabId},files:['rope-ui.js']});
}
async function openBrowserRope(tab) {
  if(!tab || tab.incognito || browserOpening) return;
  browserOpening=true;
  try {
    if(browserOverlayTab!==null) {
      const closed=await browserMessage(browserOverlayTab,{type:'ROPE_HIDE'});
      browserOverlayTab=null;ropeOpen=false;
      if(closed?.wasOpen) return;
    }
    sourceTab=tab;
    await chrome.action.setBadgeText({text:''});
    const cards=await browserCards();
    try {
      await injectBrowserUI(tab.id);
      const shown=await chrome.tabs.sendMessage(tab.id,{type:'ROPE_SHOW',cards});
      if(!shown?.ok) throw new Error('Picker unavailable');
      browserOverlayTab=tab.id;ropeOpen=true;
    } catch {
      // Chrome forbids overlays on internal pages. Still open the tab picker,
      // never settings or a native-app installer.
      await chrome.action.setPopup({popup:'picker.html'});
      try {await chrome.action.openPopup({windowId:tab.windowId});}
      catch {await chrome.windows.create({url:'picker.html?window=1',type:'popup',width:790,height:430});}
      finally {await chrome.action.setPopup({popup:''});}
    }
  } finally {browserOpening=false;}
}
async function browserNotice(success,text,tabId) {
  if(tabId===undefined) [tabId]=(await chrome.tabs.query({active:true,lastFocusedWindow:true})).map(tab=>tab.id);
  if(tabId===undefined) return;
  try {
    await injectBrowserUI(tabId);
    await chrome.tabs.sendMessage(tabId,{type:'ROPE_NOTICE',success,text:text||(success?'Screenshot copied':'Screenshot failed')});
  } catch {
    // Protected pages cannot host a toast. The toolbar badge remains visible.
    await chrome.action.setBadgeBackgroundColor({color:success?'#46b66c':'#d95353'});
    await chrome.action.setBadgeText({text:success?'✓':'!'});
  }
}
async function browserClipboard(dataUrl,tab) {
  // Clipboard.write needs a focused document. Normal secure pages write directly.
  try {
    await injectBrowserUI(tab.id);
    const copied=await chrome.tabs.sendMessage(tab.id,{type:'ROPE_CLIPBOARD',dataUrl});
    if(copied?.ok)return;
  } catch {}
  // Protected and insecure HTTP pages use a short-lived focused extension window.
  // It opens only after the screenshot has been captured.
  let clipboardWindow,timer;
  const completed=new Promise((resolve,reject)=>{
    clipboardJob={dataUrl,resolve,reject};
    timer=setTimeout(()=>reject(new Error('Clipboard copy timed out.')),8000);
  });
  completed.catch(()=>{});
  try {
    clipboardWindow=await chrome.windows.create({url:'clipboard.html',type:'popup',width:280,height:120,focused:true});
    await completed;
  } finally {
    clearTimeout(timer);clipboardJob=null;
    if(clipboardWindow)await chrome.windows.remove(clipboardWindow.id).catch(()=>{});
    await chrome.windows.update(tab.windowId,{focused:true}).catch(()=>{});
  }
}
async function browserSave(dataUrl) {
  const stamp=new Date().toISOString().replace(/[:.]/g,'-');
  let id;
  try {id=await chrome.downloads.download({url:dataUrl,filename:`Tabline-${stamp}.png`,saveAs:true});}
  catch(error){if(/cancelled|canceled/i.test(error.message))return {cancelled:true};throw error;}
  return new Promise((resolve,reject)=>{
    let timer;
    const finish=(error,cancelled=false)=>{
      clearTimeout(timer);chrome.downloads.onChanged.removeListener(changed);
      error?reject(error):resolve({cancelled});
    };
    const changed=delta=>{
      if(delta.id!==id)return;
      if(delta.state?.current==='complete')finish();
      if(delta.state?.current==='interrupted')finish(delta.error?.current==='USER_CANCELED'?null:new Error('The screenshot could not be saved.'),delta.error?.current==='USER_CANCELED');
    };
    chrome.downloads.onChanged.addListener(changed);
    timer=setTimeout(()=>finish(new Error('Save has not finished. Check Chrome Downloads.')),120000);
    // A tiny PNG can finish before onChanged is registered.
    chrome.downloads.search({id}).then(([item])=>{
      if(item?.state==='complete')finish();
      else if(item?.state==='interrupted')finish(item.error==='USER_CANCELED'?null:new Error('Save failed.'),item.error==='USER_CANCELED');
    }).catch(error=>finish(error));
  });
}
async function copyBrowserTab(id,operation='copy') {
  if(copyBusy) throw new Error('A screenshot is already being captured.');
  const numeric=Number(id);
  if(!Number.isSafeInteger(numeric)||numeric<0)throw new Error('Invalid tab.');
  copyBusy=true;ropeOpen=false;
  for(const timer of jobs.values())clearTimeout(timer);jobs.clear();
  let tab;
  try {
    if(browserOverlayTab!==null)await browserMessage(browserOverlayTab,{type:'ROPE_HIDE'});
    browserOverlayTab=null;
    const original=await chrome.tabs.get(numeric);
    if(original.incognito || original.url?.startsWith(chrome.runtime.getURL('')))throw new Error('This tab cannot be captured.');
    await chrome.windows.update(original.windowId,{focused:true});
    if(!original.active||original.discarded)await chrome.tabs.update(numeric,{active:true});
    // Also remove an earlier toast from the selected page before the frame paints.
    await browserMessage(numeric,{type:'ROPE_HIDE'});
    tab=await waitUntilRendered(numeric);
    const capture=await captureActive(tab,false);
    const completed=operation==='save'?await browserSave(capture.data):await browserClipboard(capture.data,tab);
    if(completed?.cancelled)return {ok:true,cancelled:true};
    const text=operation==='save'?'Screenshot saved':'Screenshot copied';
    await chrome.action.setTitle({title:text});
    await browserNotice(true,text,tab.id);
    const epoch=generation;
    fetch(capture.data).then(response=>response.blob()).then(tinyPreview).then(image=>{if(epoch===generation){putThumbnail({id:tab.id,url:tab.url,image,at:Date.now()});persist();}}).catch(()=>{});
    return {ok:true,mode:'fresh',switched:!original.active};
  } catch(error) {
    await chrome.action.setTitle({title:`Screenshot failed: ${error.message}`});
    await browserNotice(false,'Screenshot failed',tab?.id);
    return {ok:false,error:error.message};
  } finally {copyBusy=false;}
}
