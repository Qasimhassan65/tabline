const NATIVE_HOST='com.tabline.companion';
let nativePort,nativeSequence=0;
const nativePending=new Map();
function connectCompanion(){
 if(nativePort)return nativePort;
 const port=chrome.runtime.connectNative(NATIVE_HOST);nativePort=port;
 port.onMessage.addListener(message=>{
  if(message.event){handleNativeEvent(message).catch(reportError);return;}
  const pending=nativePending.get(message.requestId);if(!pending)return;
  clearTimeout(pending.timer);nativePending.delete(message.requestId);
  message.ok?pending.resolve(message):pending.reject(new Error(message.error));
 });
 port.onDisconnect.addListener(()=>{
  const detail=chrome.runtime.lastError?.message;
  if(nativePort!==port)return;
  const error=new Error(detail||'Tabline companion disconnected.');
  nativePort=null;
  for(const pending of nativePending.values()){clearTimeout(pending.timer);pending.reject(error);}
  nativePending.clear();ropeOpen=false;
 });
 return port;
}
function nativeRequest(action,values={},timeout=10000){
 if(nativePending.size>=24)return Promise.reject(new Error('Capture queue is busy. Try again.'));
 return new Promise((resolve,reject)=>{
  const requestId=++nativeSequence;
  const timer=setTimeout(()=>{nativePending.delete(requestId);reject(new Error('Screenshot request timed out.'));},timeout);
  nativePending.set(requestId,{resolve,reject,timer});
  try{connectCompanion().postMessage({action,requestId,...values});}
  catch(error){clearTimeout(timer);nativePending.delete(requestId);reject(error);}
 });
}
