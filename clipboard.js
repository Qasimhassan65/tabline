(async()=>{
  const {dataUrl}=await chrome.runtime.sendMessage({type:'CLIPBOARD_READY'});
  if(!/^data:image\/png;base64,/.test(dataUrl))throw new Error('Invalid screenshot.');
  const deadline=Date.now()+2500;
  while(!document.hasFocus()&&Date.now()<deadline)await new Promise(resolve=>setTimeout(resolve,30));
  const blob=await (await fetch(dataUrl)).blob();
  await navigator.clipboard.write([new ClipboardItem({'image/png':blob})]);
  await chrome.runtime.sendMessage({type:'CLIPBOARD_DONE',ok:true});
})().catch(error=>chrome.runtime.sendMessage({type:'CLIPBOARD_DONE',ok:false,error:error.message}));
