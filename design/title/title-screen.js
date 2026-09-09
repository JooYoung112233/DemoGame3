// Reusable title UI. Host owns game saves, scene loading, audio and application quit.
export function mountTitle(root,{background='a',hasSave=false,onStart,onContinue,onQuit,onSettingsChange}={}){
 const backgrounds={a:'background-a-v2.png',b:'background-b-v2.png',c:'background-c-v2.png'};
 const asset=name=>new URL('../../art/title/'+name,import.meta.url).href;
 const defaults={volume:70,textSize:'normal',reducedMotion:matchMedia('(prefers-reduced-motion: reduce)').matches};
 let settings={...defaults};try{const saved=JSON.parse(localStorage.getItem('live49.title.settings.v1'));if(saved&&typeof saved==='object'){if(Number.isFinite(saved.volume))settings.volume=Math.max(0,Math.min(100,saved.volume));if(['normal','large'].includes(saved.textSize))settings.textSize=saved.textSize;if(typeof saved.reducedMotion==='boolean')settings.reducedMotion=saved.reducedMotion;}}catch{}
 const abort=new AbortController(),signal=abort.signal;
 root.classList.add('title-stage');root.setAttribute('aria-label','Live49 시작 화면');
 root.innerHTML=`<img class="title-background" alt=""/><div class="title-shade"></div><div class="title-menu"><img class="title-logo" alt="Live49 마지막 여름"/><nav class="menu-options" aria-label="시작 메뉴"><button class="menu-item" data-action="continue" hidden>이어하기</button><button class="menu-item" data-action="start">시작</button><button class="menu-item" data-action="settings">설정</button><button class="menu-item" data-action="quit">종료</button></nav></div><div class="title-footer">↑ ↓ 선택 &nbsp; · &nbsp; Enter 확인</div><div class="title-screenfade"></div><span class="sr-only" role="status"></span><dialog class="settings-dialog" aria-labelledby="settings-heading"><h2 id="settings-heading">설정</h2><label>전체 음량 <input aria-label="전체 음량" type="range" min="0" max="100" step="5"></label><label>글자 크기 <select aria-label="글자 크기"><option value="normal">기본</option><option value="large">크게</option></select></label><label>움직임 줄이기 <input aria-label="움직임 줄이기" type="checkbox"></label><button class="dialog-close">돌아가기</button></dialog>`;
 const find=s=>root.querySelector(s),dialog=find('dialog'),actions={start:onStart,continue:onContinue,quit:onQuit};let busy=false;
 find('.title-logo').src=asset('logo-v1.svg');
 find('[data-action="continue"]').hidden=!hasSave;
 for(const [key,handler] of Object.entries(actions))find(`[data-action="${key}"]`).disabled=typeof handler!=='function';
 const available=()=>[...root.querySelectorAll('.menu-item:not([hidden]):not(:disabled)')];
 function applySettings(){root.dataset.reducedMotion=String(settings.reducedMotion);root.dataset.largeText=String(settings.textSize==='large');find('input[type=range]').value=settings.volume;find('select').value=settings.textSize;find('input[type=checkbox]').checked=settings.reducedMotion;onSettingsChange?.({...settings});}
 function setBackground(key){background=Object.hasOwn(backgrounds,key)?key:'a';find('.title-background').src=asset(backgrounds[background]);root.dataset.background=background;}
 function focusMenu(){available()[0]?.focus({preventScroll:true});}
 const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
 root.addEventListener('click',async event=>{const button=event.target.closest('[data-action]');if(!button||button.disabled||busy)return;const action=button.dataset.action;if(action==='settings'){dialog.showModal();return;}busy=true;available().forEach(b=>b.disabled=true);root.classList.add('leaving');try{if(!settings.reducedMotion)await delay(600);if(signal.aborted)return;await actions[action]({settings:{...settings}});}catch{find('[role=status]').textContent='요청을 완료하지 못했습니다. 다시 시도해 주세요.';}finally{if(!signal.aborted){root.classList.remove('leaving');for(const item of root.querySelectorAll('.menu-item'))item.disabled=item.dataset.action!=='settings'&&typeof actions[item.dataset.action]!=='function';busy=false;button.focus({preventScroll:true});}}},{signal});
 find('.dialog-close').addEventListener('click',()=>dialog.close(),{signal});
 dialog.addEventListener('close',()=>find('[data-action=settings]').focus({preventScroll:true}),{signal});
 dialog.addEventListener('change',()=>{settings={volume:Number(find('input[type=range]').value),textSize:find('select').value,reducedMotion:find('input[type=checkbox]').checked};applySettings();try{localStorage.setItem('live49.title.settings.v1',JSON.stringify(settings));}catch{}},{signal});
 root.addEventListener('keydown',event=>{if(dialog.open||busy||!['ArrowDown','ArrowUp','Home','End'].includes(event.key))return;const buttons=available();if(!buttons.length)return;event.preventDefault();let index=buttons.indexOf(document.activeElement);if(event.key==='Home')index=0;else if(event.key==='End')index=buttons.length-1;else index=(index+(event.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length;buttons[index].focus({preventScroll:true});},{signal});
 applySettings();setBackground(background);
 return {setBackground,focus:focusMenu,getSettings:()=>({...settings}),destroy(){abort.abort();dialog.close();root.replaceChildren();root.classList.remove('title-stage');}};
}
