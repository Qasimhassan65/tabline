(async()=>{
  const result=await chrome.runtime.sendMessage({type:'BROWSER_CARDS'});
  if(result.ok) globalThis.TablineRope.show(result.cards,true);
  else document.body.textContent=result.error||'Could not load tabs. Reopen Tabline.';
})().catch(error=>{document.body.textContent=error.message;});
