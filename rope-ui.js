// Injected only on request, in Chrome's isolated world. No page CSS or idle loop.
(()=>{
  if(globalThis.TablineRope)return;
  const host=document.createElement('div');host.id='tabline-browser-rope';
  host.style.cssText='all:initial!important;position:fixed!important;inset:0!important;z-index:2147483647!important;pointer-events:none!important;display:block!important;font:14px -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif!important;color:#f1f5f9!important;';
  const shadow=host.attachShadow({mode:'open'});
  const style=document.createElement('style');
  style.textContent=`
  :host{color-scheme:dark;font:14px -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;color:#f1f5f9}
  *{box-sizing:border-box}button{font:inherit;color:inherit;cursor:pointer}button:focus-visible,.card:focus-visible{outline:2px solid #76c5ff;outline-offset:4px}
  .band{position:absolute;top:0;left:0;width:100%;height:min(375px,83vh);overflow:hidden;pointer-events:auto;perspective:1200px;isolation:isolate}
  .band[hidden],.toast[hidden]{display:none}
  .cable{position:absolute;inset:0;width:100%;height:90px;overflow:visible;pointer-events:none;filter:drop-shadow(0 2px 2px #0009)}
  .card{position:absolute;top:68px;left:0;width:var(--cw);height:var(--ch);border-radius:17px;cursor:pointer;outline:none;transform-origin:50% -14px;background:linear-gradient(140deg,#263540d9,#101820df 70%);border:1px solid #b1c8d05c;box-shadow:inset 0 1px 0 #edfcff36,0 12px 28px #0005;backdrop-filter:blur(20px) saturate(125%);will-change:transform;touch-action:pan-y}
  .card.selected,.card:hover,.card:focus-visible{border-color:#6ec8ff;box-shadow:inset 0 0 0 1px #63bfff66,0 0 0 1px #48b8ff,0 10px 30px #0005}
  .clip{position:absolute;left:calc(50% - 6px);top:-29px;width:12px;height:36px;border-radius:2px;background:linear-gradient(90deg,#46525c,#a7b4c0 23%,#f3f7fc 42%,#718191 64%,#b8c5cd 81%,#414c55);box-shadow:0 2px 3px #000b;z-index:3;border:1px solid #6b7882}
  .clip:before{content:"";position:absolute;left:-1px;right:-1px;top:3px;height:6px;background:linear-gradient(#f8ffff,#a4b2be 44%,#394953 75%,#d0dde3);box-shadow:0 1px 1px #0009}
  .clip:after{content:"";position:absolute;left:3px;bottom:4px;width:4px;height:4px;border-radius:50%;background:#586775;box-shadow:inset 0 1px 1px #12202b,0 1px 0 #e1edf1}
  .image{position:absolute;left:10px;right:10px;top:10px;bottom:47px;overflow:hidden;border-radius:9px;background:linear-gradient(140deg,#243b4e99,#0e1822aa)}
  .image>img{width:100%;height:100%;object-fit:cover;display:block;pointer-events:none}
  .identity{height:100%;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px;padding:12px;text-align:center}
  .identity img{width:48px;height:48px;object-fit:contain;filter:drop-shadow(0 4px 8px #0005)}.identity strong{font-size:18px;font-weight:550;max-width:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.identity span{color:#a2b6c9;font-size:13px}.identity small{color:#c0cbd6;font-size:12px}
  .footer{position:absolute;bottom:0;left:0;right:0;height:45px;display:flex;align-items:center;gap:9px;padding:0 14px}.footer img{width:23px;height:23px;object-fit:contain}.title{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:15px;font-weight:500;letter-spacing:-.2px}
  .actions{position:absolute;bottom:9px;left:0;right:0;display:flex;justify-content:center;gap:7px;opacity:0;transform:translateY(3px);transition:opacity .16s,transform .16s;pointer-events:none}
  .card:hover .actions,.card:focus-within .actions{opacity:1;transform:none;pointer-events:auto}
  .glass{background:linear-gradient(#253a43e6,#101f25e6);border:1px solid #b4d2db68;border-radius:999px;box-shadow:inset 0 1px 0 #e6ffff24,0 2px 5px #0006;backdrop-filter:blur(14px)}
  .action{height:37px;padding:0 15px;display:flex;align-items:center;gap:8px;font-size:14px;text-shadow:0 1px 2px #000}.action:hover{background:#375166e6;border-color:#b9e2ff}.action svg{width:19px;height:19px;fill:none;stroke:currentColor;stroke-width:1.7;stroke-linecap:round;stroke-linejoin:round}
  .nav{position:absolute;top:151px;width:39px;height:44px;z-index:8;display:grid;place-items:center;background:#13232de8;color:#f0f7ff;box-shadow:0 2px 12px #0006}.prev{left:9px}.next{right:9px}.nav:disabled{opacity:.25;cursor:default}.nav svg{width:20px;height:20px;fill:none;stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}
  .toast{position:absolute;left:50%;bottom:56px;transform:translateX(-50%);height:52px;max-width:calc(100vw - 24px);padding:0 22px 0 16px;display:flex;align-items:center;gap:14px;border-radius:999px;background:linear-gradient(115deg,#203029ec,#152028eb 42%,#1b252deb);border:1px solid #8ac0ad55;box-shadow:inset 0 1px 1px #d8ffff22,0 8px 28px #0006;backdrop-filter:blur(22px) saturate(135%);font-size:16px;white-space:nowrap;letter-spacing:-.25px;pointer-events:none}
  .toast.fail{border-color:#fb666c77;background:linear-gradient(110deg,#42292ce8,#18232bea 45%)}.status-icon{width:27px;height:27px;background:#76ed68;color:#153c1e;border-radius:50%;display:grid;place-items:center;font-size:20px;font-weight:600;box-shadow:0 0 7px #6fe57744}.fail .status-icon{background:#ff655c;color:#481e22}
  @media(hover:none){.actions{opacity:1;transform:none;pointer-events:auto}}
  @media(prefers-reduced-motion:reduce){.actions{transition:none}}
  `;
  shadow.append(style);
  const band=document.createElement('section');band.className='band';band.hidden=true;band.setAttribute('aria-label','Tabline screenshot picker');
  const svgNS='http://www.w3.org/2000/svg';
  const cable=document.createElementNS(svgNS,'svg');cable.classList.add('cable');cable.setAttribute('aria-hidden','true');
  const strokes=[['#243039',7],['#627789',5],['#e0e8ef',2.7],['#fbffff',.8]].map(([color,width])=>{const p=document.createElementNS(svgNS,'path');p.setAttribute('fill','none');p.setAttribute('stroke',color);p.setAttribute('stroke-width',width);cable.append(p);return p;});
  const create=(tag,cls,text)=>{const el=document.createElement(tag);el.className=cls;if(text)el.textContent=text;return el;};
  const icon=(kind)=>{
    const svg=document.createElementNS(svgNS,'svg');svg.setAttribute('viewBox','0 0 24 24');svg.setAttribute('aria-hidden','true');
    const path=document.createElementNS(svgNS,'path');path.setAttribute('d',kind==='copy'?'M9 5V3h8v4M8 7H5v14h13V7H8zm1-2H3v12':kind==='save'?'M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5':kind==='prev'?'m14 5-7 7 7 7':'m10 5 7 7-7 7');svg.append(path);return svg;
  };
  const prev=create('button','nav prev glass'),next=create('button','nav next glass');prev.append(icon('prev'));next.append(icon('next'));prev.setAttribute('aria-label','Previous tabs');next.setAttribute('aria-label','Next tabs');
  band.append(cable,prev,next);shadow.append(band);
  const toast=create('div','toast');toast.hidden=true;toast.setAttribute('role','status');toast.setAttribute('aria-live','polite');shadow.append(toast);
  let cards=[],elements=[],offset=0,target=0,frame=0,toastTimer,opened=false,popup=false,initial=true,cardWidth=300,step=322,viewport=innerWidth;
  const reduced=matchMedia('(prefers-reduced-motion: reduce)');
  function mount(){if(!host.isConnected)(document.body||document.documentElement).append(host);}
  function limit(value){return Math.max(0,Math.min(Math.max(0,cards.length-1)*step,value));}
  function paint(){
    const center=viewport/2;
    elements.forEach((el,index)=>{
      const x=center+index*step-offset;
      const distance=Math.max(-1,Math.min(1,(x-center)/(Math.max(1,viewport/2))));
      const y=-17*distance*distance,angle=-2.3*distance;
      el.style.transform=`translate3d(${(x-cardWidth/2).toFixed(2)}px,${y.toFixed(2)}px,0) rotate(${angle.toFixed(2)}deg)`;
      el.style.visibility=x < -cardWidth || x > viewport+cardWidth?'hidden':'visible';
    });
    prev.disabled=target<1;next.disabled=target>=Math.max(0,cards.length-1)*step-1;
  }
  function animate(){frame=0;const delta=target-offset;offset=reduced.matches||Math.abs(delta)<.25?target:offset+delta*.19;paint();if(offset!==target)frame=requestAnimationFrame(animate);}
  function unselect(){if(initial){initial=false;elements.forEach(el=>el.classList.remove('selected'));}}
  function scrollBy(amount){unselect();target=limit(target+amount);if(!frame)frame=requestAnimationFrame(animate);}
  function resize(){
    const oldStep=step;viewport=host.getBoundingClientRect().width||innerWidth;
    cardWidth=Math.max(190,Math.min(300,viewport-90,innerHeight*.69));step=cardWidth+24;
    band.style.setProperty('--cw',`${cardWidth}px`);band.style.setProperty('--ch',`${Math.round(cardWidth*.8)}px`);
    target=limit(target/oldStep*step);offset=target;
    cable.setAttribute('viewBox',`0 0 ${viewport} 90`);
    strokes.forEach((path,index)=>path.setAttribute('d',`M -10 ${17-index*.25} Q ${viewport/2} ${78-index*.25} ${viewport+10} ${17-index*.25}`));paint();
  }
  function hide(){
    const wasOpen=opened;opened=false;band.hidden=true;toast.hidden=true;clearTimeout(toastTimer);cancelAnimationFrame(frame);frame=0;
    elements.forEach(el=>el.remove());elements=[];cards=[];
    if(wasOpen)chrome.runtime.sendMessage({type:'BROWSER_CLOSED'}).catch(()=>{});
    return wasOpen;
  }
  async function capture(card,operation){
    if(!opened)return;
    hide();
    const pending=chrome.runtime.sendMessage({type:'BROWSER_CAPTURE',tabId:card.id,operation});
    if(popup){pending.catch(()=>{});window.close();return;}
    const result=await pending.catch(error=>({ok:false,error:error.message}));
    if(!result.ok)notice(false,'Screenshot failed');
  }
  function image(src,cls=''){const img=create('img',cls);img.alt='';img.src=src;img.draggable=false;img.addEventListener('error',()=>img.remove(),{once:true});return img;}
  function show(data,isPopup=false){
    mount();hide();popup=isPopup;cards=data;initial=true;elements.forEach(el=>el.remove());elements=[];
    for(const card of cards){
      const el=create('article','card'+(card.current?' selected':''));el.tabIndex=0;el.setAttribute('role','group');el.setAttribute('aria-label',`${card.title}. Click to capture.`);
      el.append(create('span','clip'));const preview=create('div','image');
      if(card.image)preview.append(image(card.image));else{
        const identity=create('div','identity');
        if(card.icon)identity.append(image(card.icon));
        identity.append(create('strong','',card.title),create('span','',card.domain),create('small','','Capture on click'));preview.append(identity);
      }
      const actions=create('div','actions');
      for(const [operation,label] of [['copy','Copy'],['save','Save']]){
        const button=create('button','action glass',label);button.prepend(icon(operation));button.setAttribute('aria-label',`${label} ${card.title}`);
        button.onclick=event=>{event.stopPropagation();capture(card,operation);};actions.append(button);
      }
      preview.append(actions);const footer=create('div','footer');if(card.icon)footer.append(image(card.icon));footer.append(create('span','title',card.title));
      el.append(preview,footer);el.onclick=()=>capture(card,'copy');el.onpointerenter=unselect;
      el.onkeydown=event=>{if(event.target===el&&(event.key==='Enter'||event.key===' ')){event.preventDefault();capture(card,'copy');}};
      band.append(el);elements.push(el);
    }
    opened=true;band.hidden=false;resize();target=offset=Math.max(0,cards.findIndex(card=>card.current))*step;paint();
  }
  function notice(success,text){mount();toast.replaceChildren(create('span','status-icon',success?'✓':'×'),create('span','',text));toast.className='toast'+(success?'':' fail');toast.hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>{toast.hidden=true;if(!opened)host.remove();},2600);}
  prev.onclick=()=>scrollBy(-step);next.onclick=()=>scrollBy(step);
  band.addEventListener('wheel',event=>{if(event.ctrlKey||!opened)return;event.preventDefault();event.stopPropagation();const raw=Math.abs(event.deltaX)>Math.abs(event.deltaY)?event.deltaX:event.deltaY;scrollBy(raw*(event.deltaMode===1?22:event.deltaMode===2?viewport:1));},{passive:false});
  let pointerX=null;
  band.addEventListener('touchstart',event=>{pointerX=event.touches[0].clientX;},{passive:true});
  band.addEventListener('touchmove',event=>{if(pointerX===null)return;const x=event.touches[0].clientX;scrollBy(pointerX-x);pointerX=x;},{passive:true});
  document.addEventListener('pointerdown',event=>{if(opened&&!event.composedPath().includes(host))hide();},true);
  document.addEventListener('keydown',event=>{if(!opened)return;if(event.key==='Escape'){event.preventDefault();hide();if(popup)window.close();}else if(['ArrowLeft','ArrowRight'].includes(event.key)){event.preventDefault();scrollBy(event.key==='ArrowLeft'?-step:step);}},true);
  window.addEventListener('resize',()=>{if(opened)resize();});
  chrome.runtime.onMessage.addListener((message,sender,reply)=>{
    if(sender.id!==chrome.runtime.id)return;
    if(message.type==='ROPE_SHOW'){show(message.cards);reply({ok:true});}
    if(message.type==='ROPE_HIDE'){
      const wasOpen=hide();let done=false;const finish=()=>{if(done)return;done=true;reply({ok:true,wasOpen});};
      const timer=setTimeout(finish,160);requestAnimationFrame(()=>requestAnimationFrame(()=>{clearTimeout(timer);finish();}));return true;
    }
    if(message.type==='ROPE_CLIPBOARD'){
      (async()=>{
        if(!/^data:image\/png;base64,/.test(message.dataUrl))throw new Error('Invalid screenshot.');
        const blob=await (await fetch(message.dataUrl)).blob();
        await navigator.clipboard.write([new ClipboardItem({'image/png':blob})]);return {ok:true};
      })().then(reply,error=>reply({ok:false,error:error.message}));return true;
    }
    if(message.type==='ROPE_NOTICE'){notice(message.success,message.text);reply({ok:true});}
  });
  globalThis.TablineRope={show,hide,notice};
})();
