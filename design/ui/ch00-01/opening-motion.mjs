import{setupPhoto,PHOTO}from'./photo-memory.mjs';
import{sampleFrame,durationOf,configured,graphemes,smoothstep,transitionFrame,transitionDuration}from'./opening-motion-core.mjs';
import{setupDialogue}from'./memory-dialogue.mjs';
const el=id=>document.getElementById(id);
let base,config,preset='base',time=0,running=false,ready=false,started=false,last=null,pressedImage,focusUrl;
let resumeOnVisible=false,rafId=0;
let phase='opening';
let dialogue,photo,lineIndex=0;
const cueDuration=()=>config.entry_tracks.find(t=>t.id==='next-indicator').duration;
const playbackEnd=()=>phase==='opening'?1.35:phase==='photo-entry'?PHOTO.entryEnd:phase==='photo-return'?PHOTO.returnEnd:phase==='present-reveal'?1:phase==='return-fade'?.4:phase==='conversation'?dialogue.duration(lineIndex):phase==='handoff'?dialogue.data.timing.handoff_fade+dialogue.data.timing.handhold_pause:transitionDuration(config);
function stopClock(){cancelAnimationFrame(rafId);rafId=0;running=false;last=null;}
const stage=el('stage'),ui=el('ui-layers'),background=el('background');
const buttons=['jump-present','jump-photo','jump-handhold','replay','pause','reset','export'];
function render(){
 const f=sampleFrame(phase==='opening'?time:durationOf(config),config);
 const transition=phase==='present-reveal'?{blackOpacity:0,showMemory:true}:phase==='return-fade'?{blackOpacity:smoothstep(time/.4),showMemory:true}:phase.startsWith('photo-')?{blackOpacity:0,showMemory:false}:phase==='opening'?{blackOpacity:0,showMemory:false,complete:false}:phase==='conversation'?{blackOpacity:0,showMemory:true}:phase==='handoff'?{blackOpacity:1-smoothstep(time/dialogue.data.timing.handoff_fade),showMemory:true}:transitionFrame(time,config);
 const ended=time>=playbackEnd();
 photo.render(phase==='transition'&&!transition.showMemory?'photo-entry':phase,phase==='transition'?PHOTO.entryEnd:time,running&&phase!=='transition');
 if(phase==='present-reveal'){photo.render('photo-return',PHOTO.returnEnd,false);el('photo-scene').style.opacity=1-smoothstep(time/.65);el('photo-cue').hidden=true;}
 const scaleX=stage.clientWidth/config.resolution[0],scaleY=stage.clientHeight/config.resolution[1];
 background.style.transform=`matrix(${f.scale},0,0,${f.scale},${f.x*scaleX},${f.y*scaleY})`;
 ui.style.opacity=f.menuOpacity;
 el('call-dim').style.opacity=f.dimOpacity;
 el('advance-ui').style.opacity=0;
 el('memory').hidden=!transition.showMemory;
 dialogue.render(lineIndex,phase==='conversation'?time:phase==='return-fade'?dialogue.duration(lineIndex):0,transition.showMemory,phase==='handoff');
 if(phase==='present-reveal')el('memory-dialogue').hidden=true;
 el('transition-black').style.opacity=transition.blackOpacity;
 const sourceVisible=phase==='opening'||phase==='photo-entry';
 for(const item of [background,ui,el('call-dim'),el('call-text')])item.style.visibility=sourceVisible?'visible':'hidden';
 for(const item of [el('call-dim'),el('call-text')])item.style.visibility=phase==='opening'?'visible':'hidden';
 Array.from(el('call-text').children).forEach((span,i)=>{span.style.visibility=i<f.visibleGraphemes?'visible':'hidden';});
 const accessible=phase==='opening'&&f.complete?config.entry_tracks.find(t=>t.id==='call-text').text:'';
 if(el('call-accessible').textContent!==accessible)el('call-accessible').textContent=accessible;
 const surface=started&&f.pressed?new URL('/design/ui/ch00-01/sprites/A/title-button-pressed.png',location.href).href:focusUrl;
 if(pressedImage.src!==surface)pressedImage.src=surface;
 el('start').disabled=!ready||started;
 el('start').style.visibility=started?'hidden':'visible';
 el('scrub').max=playbackEnd()*1000;el('scrub').value=time*1000;
 el('time').textContent=`${phase==='opening'?'부름':'전환'} ${time.toFixed(2)} / ${playbackEnd().toFixed(2)}초`;
 el('pause').textContent=running?'일시정지':'계속 재생';el('pause').disabled=!started||(ended&&!['opening','transition','handoff','return-fade','present-reveal'].includes(phase));
 stage.dataset.status=!ready?'loading':ended?(phase==='opening'?'reading':phase==='conversation'?'dialogue-reading':phase==='handoff'?'handoff':'memory'):running?'playing':started?'paused':'ready';
 stage.dataset.phase=phase;stage.dataset.blackOpacity=transition.blackOpacity;
 stage.dataset.time=time.toFixed(4);stage.dataset.scale=f.scale.toFixed(5);
 stage.dataset.visibleGraphemes=f.visibleGraphemes;
 el('status').textContent=phase!=='opening'?(ended?'회상 첫 장면에 도착했어요. 이번에는 여기서 멈춥니다.':running?'암전 속에서 장면이 바뀌고, 회상이 서서히 드러나요.':'전환을 멈췄어요. 진행 막대로 암전과 등장 순간을 확인할 수 있어요.'):f.complete?'문장을 읽은 뒤 화면 클릭 또는 Space를 누르면 회상으로 넘어갑니다.':running?(f.visibleGraphemes?'“수혁아.”가 한 글자씩 나타나고 있어요.':f.dimOpacity?'카메라가 멈추고, 하단이 은은하게 어두워져요.':'확정한 카메라 움직임으로 시작하고 있어요.'):started?'멈춘 상태예요. 진행 막대를 움직여 각 순간을 확인할 수 있어요.':'화면 안의 ‘시작’을 눌러 주세요. 첫 부름을 읽은 뒤 새 입력으로 회상에 들어갑니다.';
 if(phase==='conversation')el('status').textContent=lineIndex===0?'같은 하단 창에서 나레이션을 읽습니다. 새 입력으로 서연의 대화가 이어집니다.':dialogue.data.lines[lineIndex].kind==='narration'?'수혁의 내면 독백 · 이름표와 초상 없이 같은 창에서 이어집니다.':`${dialogue.data.lines[lineIndex].speaker}의 대화 · 배경은 그대로 유지됩니다. 클릭 또는 Space로 이어가세요.`;
 if(phase==='handoff')el('status').textContent='수혁과 소이가 손을 맞잡았어요. 서연이 건네는 중간 동작은 생략하고 두 손만 보여줍니다.';
 if(phase==='conversation'&&dialogue.data.lines[lineIndex].end_preview)el('status').textContent='소이의 질문을 읽고 새 입력을 누르면 첫 응답 선택부터 취침까지 이어집니다. 후속 대사는 검토 초안입니다.';
 if(phase==='photo-entry')el('status').textContent='캠핑카 사진에서 회상의 부름을 듣습니다. 문장을 읽고 클릭 또는 Space로 이어가세요.';
 if(phase==='photo-return')el('status').textContent=ended?'현재 소이의 “아빠?”를 읽고 클릭 또는 Space로 현재 대화에 이어갑니다.':'같은 사진으로 복귀합니다. 짧게 화면이 끊긴 뒤 얼굴만 불분명하게 남아요.';
 if(phase==='present-reveal')el('status').textContent='사진이 사라지고 현재 캠핑카 식탁으로 시선이 돌아옵니다.';
 el('time').textContent=`${phase==='opening'?'시작':phase==='photo-entry'?'사진 진입':phase==='photo-return'?'사진 복귀':phase==='conversation'?'대사 '+(lineIndex+1):phase==='handoff'?'행동 컷':'전환'} ${time.toFixed(2)} / ${playbackEnd().toFixed(2)}초`;
}
function tick(timestamp){
 if(!running)return;
 if(last!==null)time=Math.min(playbackEnd(),time+(timestamp-last)/1000);
 last=timestamp;
 if(time>=playbackEnd()){
  if(phase==='opening'){phase='photo-entry';time=0;last=timestamp;}
  else if(phase==='return-fade'){phase='photo-return';photo.reset();time=0;last=timestamp;}
  else if(phase==='present-reveal'){phase='conversation';time=0;last=timestamp;}
  else if(phase==='transition'){phase='conversation';lineIndex=0;time=0;last=timestamp;}
  else if(phase==='handoff'){phase='conversation';lineIndex=dialogue.data.lines.findIndex(l=>l.background==='handhold');time=0;last=timestamp;}
  else running=false;
 }
 render();if(running)rafId=requestAnimationFrame(tick);
}
function play(){if(!ready||running)return;started=true;running=true;last=null;render();rafId=requestAnimationFrame(tick);}
function reset(){stopClock();photo.reset();resumeOnVisible=false;phase='opening';lineIndex=0;started=false;time=0;render();}
function start(){if(ready&&!started){photo.unlock();play();stage.focus({preventScroll:true});}}
el('start').addEventListener('click',start);
el('start').addEventListener('keydown',event=>{if(event.repeat)event.preventDefault();});
function advance(){
 if(!ready||!started)return;
 photo.unlock();
 if(phase==='photo-entry'){if(time<PHOTO.callStart)return;stopClock();resumeOnVisible=false;if(time<PHOTO.entryEnd){time=PHOTO.entryEnd;render();return;}phase='transition';time=0;play();return;}
 if(phase==='photo-return'){if(time<3.1)return;stopClock();resumeOnVisible=false;if(time<PHOTO.returnEnd){time=PHOTO.returnEnd;render();return;}lineIndex=dialogue.data.lines.findIndex(l=>l.background==='present');phase='present-reveal';time=0;play();return;}
 if(phase==='conversation'){
  if(time<(lineIndex===0?dialogue.data.timing.arrival_hold+dialogue.data.timing.panel_fade:dialogue.data.timing.portrait_fade))return;
  if(dialogue.data.lines[lineIndex].end_preview&&time>=dialogue.fullTextTime(lineIndex)){stopClock();location.assign('./chapter0-continuation.html');return;}
  stopClock();resumeOnVisible=false;
  if(time<dialogue.fullTextTime(lineIndex)){time=dialogue.fullTextTime(lineIndex);play();return;}
  if(dialogue.data.lines[lineIndex+1]?.background==='present'&&dialogue.data.lines[lineIndex].background!=='present')phase='return-fade';
  else if(dialogue.data.lines[lineIndex+1]?.background==='handhold'&&dialogue.data.lines[lineIndex].background!=='handhold')phase='handoff';
  else if(lineIndex+1<dialogue.data.lines.length)lineIndex++;
  else phase='return-fade';
  time=0;stage.focus({preventScroll:true});play();return;
 }
 if(phase!=='opening')return;
 const call=config.entry_tracks.find(t=>t.id==='call-text');
 if(time<call.start)return;
 stopClock();resumeOnVisible=false;
 if(time<durationOf(config)){time=durationOf(config);play();return;}
 phase='transition';time=0;stage.focus({preventScroll:true});play();
}
stage.addEventListener('click',event=>{if(event.target!==el('start'))advance();});
document.addEventListener('keydown',event=>{
 if(event.code!=='Space')return;
 if(event.target!==document.body&&!stage.contains(event.target))return;
 event.preventDefault();if(event.repeat)return;if(!started)start();else advance();
});
el('replay').addEventListener('click',()=>{photo.unlock();reset();play();});
el('jump-photo').addEventListener('click',()=>{if(!ready)return;stopClock();resumeOnVisible=false;photo.unlock();photo.reset();phase='photo-return';time=0;stage.focus({preventScroll:true});play();});
el('jump-present').addEventListener('click',()=>{if(!ready)return;stopClock();resumeOnVisible=false;lineIndex=dialogue.data.lines.findIndex(l=>l.background==='present');phase='present-reveal';time=0;stage.focus({preventScroll:true});play();});
el('jump-handhold').addEventListener('click',()=>{if(!ready)return;stopClock();resumeOnVisible=false;photo.unlock();phase='handoff';lineIndex=dialogue.data.lines.findIndex(l=>l.background==='handhold')-1;time=0;stage.focus({preventScroll:true});play();});
el('reset').addEventListener('click',()=>{reset();el('start').focus({preventScroll:true});});
el('pause').addEventListener('click',()=>{resumeOnVisible=false;if(running){stopClock();render();}else play();});
el('scrub').addEventListener('input',()=>{stopClock();resumeOnVisible=false;started=true;time=Number(el('scrub').value)/1000;render();});
document.querySelectorAll('[data-preset]').forEach(button=>button.addEventListener('click',()=>{
 if(!ready)return;preset=button.dataset.preset;config=configured(base,preset);
 document.querySelectorAll('[data-preset]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));reset();
}));
function suspend(){resumeOnVisible=resumeOnVisible||running;stopClock();if(ready)render();}
function resume(){if(!document.hidden&&resumeOnVisible){resumeOnVisible=false;play();}}
document.addEventListener('visibilitychange',()=>{if(document.hidden)suspend();else resume();});
window.addEventListener('blur',suspend);window.addEventListener('focus',resume);
new ResizeObserver(()=>{if(ready)render();}).observe(stage);
el('export').addEventListener('click',()=>{
 const value={id:'C0-opening-memory-dialogue-review',status:'flow-approved-layout-wording-and-portraits-for-review',source:'design/unity-handoff/C0-opening-direction-v1.json',resolution:config.resolution,camera:config.camera,entry_tracks:config.entry_tracks,memory_transition:config.memory_transition,dialogue:dialogue.data,photo_timing:PHOTO,photo_sequence_source:'docs/03-콘티/챕터0/C0-PHOTO-MEMORY-IMPLEMENTATION.ko.md',present_sequence_source:'docs/03-콘티/챕터0/C0-PRESENT-DIALOGUE.ko.md',speaker_identity_source:'docs/06-대사/SPEAKER-IDENTITY-RULES.ko.md',scope:'opening, narration, dialogue and handoff visual reference; not Unity implementation'};
 const url=URL.createObjectURL(new Blob([JSON.stringify(value,null,2)+'\n'],{type:'application/json'}));
 const a=document.createElement('a');a.href=url;a.download='Live49-opening-memory-review.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
});
async function init(){
 const responses=await Promise.all([fetch('/design/unity-handoff/C0-opening-direction-v1.json'),fetch('./manifest.json')]);
 if(responses.some(r=>!r.ok))throw new Error('Required data not found');
 const values=await Promise.all(responses.map(r=>r.json()));[base]=values;config=configured(base,preset);
 [dialogue,photo]=await Promise.all([setupDialogue(),setupPhoto()]);
 const screen=values[1].screens['C0-00-A-first-start'];
 for(const char of graphemes(config.entry_tracks.find(t=>t.id==='call-text').text)){
  const span=document.createElement('span');span.textContent=char;el('call-text').append(span);
 }
 const images=[];
 for(const layer of screen.layers){
  const im=new Image();im.src='/'+layer.png;im.alt='';const[x,y,w,h]=layer.rect_logical;
  Object.assign(im.style,{left:x/1920*100+'%',top:y/1080*100+'%',width:w/1920*100+'%',height:h/1080*100+'%'});
  if(layer.name==='10_0_start_surface'){pressedImage=im;focusUrl=im.src;}
  ui.append(im);images.push(im.decode());
 }
 const pressed=new Image();pressed.src='/design/ui/ch00-01/sprites/A/title-button-pressed.png';images.push(pressed.decode(),background.decode());
 images.push(el('call-dim').decode(),el('advance-ui').querySelector('img').decode(),document.fonts.load('38px Live49','수혁아.'));
 await Promise.all(images);ready=true;buttons.forEach(id=>el(id).disabled=false);el('scrub').disabled=false;render();
}
init().catch(error=>{stage.dataset.status='error';el('status').textContent='시안 파일을 불러오지 못했어요. 로컬 서버로 다시 열어 주세요.';console.error(error);});
