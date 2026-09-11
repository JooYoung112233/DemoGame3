import{initialState,resolve,step,availableChoices,sceneFor,restore,reviewCheckpoint}from'./chapter0-story-core.mjs';
const $=id=>document.getElementById(id),key='live49-ch00-continuation-v1',sessionKey='live49-ch00-review-session-v1';
let data,state,chars=[],shown=0,typingTimer=null,locked=false,renderToken=0,saved=null,lastFrame=performance.now(),activeMs=0,events=[];
try{saved=localStorage.getItem(key)}catch{}
const segmenter=new Intl.Segmenter('ko',{granularity:'grapheme'});
function record(type,detail={}){events.push({type,node:state.node,line:state.line,active_ms:Math.round(activeMs),...detail})}
function save(){try{localStorage.setItem(key,JSON.stringify(state));localStorage.setItem(sessionKey,JSON.stringify({activeMs,events}))}catch{}}
function measure(now){if(!document.hidden&&document.hasFocus())activeMs+=Math.min(1000,now-lastFrame);lastFrame=now;requestAnimationFrame(measure)}requestAnimationFrame(measure);
function reveal(){clearInterval(typingTimer);shown=chars.length;$('text').textContent=chars.join('');$('cue').hidden=false}
function startTyping(line){clearInterval(typingTimer);chars=[...segmenter.segment(line)].map(x=>x.segment);shown=0;$('text').textContent='';$('cue').hidden=true;
 if($('instant').checked)return reveal();
 typingTimer=setInterval(()=>{if(document.hidden||!document.hasFocus())return;$('text').textContent=chars.slice(0,++shown).join('');if(shown>=chars.length)reveal()},data.timing.seconds_per_character*1000)
}
async function paint(previousScene=null){
 const token=++renderToken;locked=true;$('story-stage').dataset.ready='false';clearInterval(typingTimer);const n=data.nodes[state.node],path='/'+sceneFor(data,state);const changed=$('scene').getAttribute('src')!==path;
 $('title').textContent=n.title;$('purpose').textContent=n.purpose;$('asset').textContent='원화: '+path;$('sound').textContent='소리 제작 cue: '+(n.cue||'환경음 유지')+' (이 검토판은 무음)';
 const visualNote=n.visual_note_condition&&state.flags[n.visual_note_condition]?n.visual_note_if_seen:n.visual_note;
 $('visual-note').hidden=!visualNote;$('visual-note').textContent=visualNote||'';
 const reduced=$('reduce').checked;document.body.classList.toggle('reduced',reduced);
 const transitionMs=(n.id==='outage'?.55:n.id==='night'?.8:.35)*1000;
 if(changed){
  const nextImage=new Image();nextImage.src=path;await nextImage.decode();if(token!==renderToken)return;
  if(previousScene&&!reduced){await $('fade').animate([{opacity:0},{opacity:1}],{duration:transitionMs/2,fill:'forwards'}).finished;if(token!==renderToken)return}
  $('scene').src=path;
 }
 const [zoom,x,y]=n.camera;$('scene').style.transformOrigin=`${x*100}% ${y*100}%`;$('scene').style.transform=`scale(${reduced?1:zoom})`;
 const line=n.lines[state.line];$('panel').hidden=!line;$('choices').replaceChildren();$('end').hidden=true;
 for(const who of ['suhyeok','soi'])$(who).hidden=true;
 const dark=['outage','investigation','secured','night'].includes(n.scene);
 const objectFocus=n.id==='book'||n.id==='book_look';
 $('scene').style.filter=line?.speaker&&!objectFocus?`blur(${$('story-stage').clientWidth/1920*5}px)`:'none';
 if(line){
  $('name').textContent=line.speaker;$('name').hidden=!line.speaker;
  if(line.speaker&&!objectFocus){for(const who of ['suhyeok','soi']){const active=line.speaker===(who==='suhyeok'?'수혁':'소이');$(who).hidden=false;$(who).style.filter=dark?`brightness(${active?.58:.48}) saturate(.65)`:`brightness(${active?1:.85})`;$(who).style.opacity='1'}}
 }else{
  if(n.id==='reply'){const context=document.createElement('p');context.textContent='소이: “내일도 여기 있어?”';context.style.cssText='font:1.7cqw Live49,serif;color:#f1e4c7;background:#27231ddd;padding:12px 18px;margin:0';$('choices').append(context)}
  const options=availableChoices(data,state);
  options.forEach((c,i)=>{const b=document.createElement('button');b.textContent=c.label;b.onclick=e=>{e.stopPropagation();if(locked)return;record('choice',{label:c.label});const prev=state.node;state=step(data,state,i);save();paint(prev)};$('choices').append(b)});
  if(!options.length&&!n.next){$('end').hidden=false;record('chapter_complete');save()}
 }
 if(changed&&previousScene&&!reduced){await $('fade').animate([{opacity:1},{opacity:0}],{duration:transitionMs/2,fill:'forwards'}).finished;if(token!==renderToken)return}
 $('fade').getAnimations().forEach(a=>a.cancel());$('fade').style.opacity=0;locked=false;
 Object.assign($('story-stage').dataset,{ready:'true',node:state.node,line:String(state.line)});
 if(line)startTyping(line.text.replace('{journal_first_line}',state.flags.journal_first_line||''));
 $('status').textContent=`${n.id} · ${line?`대사 ${state.line+1}/${n.lines.length}`:n.choices.length?'행동 선택':'구간 끝'} · 검토 북마크 자동 저장`;
 record('view');
}
function advance(){if(!data||locked||!data.nodes[state.node].lines[state.line])return;if(shown<chars.length){reveal();return}record('line_confirm');const prev=state.node;state=step(data,state);save();paint(prev)}
$('story-stage').addEventListener('click',e=>{if(!e.target.closest('button,a'))advance()});
$('story-stage').addEventListener('keydown',e=>{if(e.code==='Space'&&!e.target.closest('button,a')){e.preventDefault();if(!e.repeat)advance()}});
$('instant').onchange=()=>{if($('instant').checked)reveal()};$('reduce').onchange=()=>paint();
$('restart').onclick=()=>{if(!data)return;state=initialState();events=[];activeMs=0;record('restart');save();paint();$('story-stage').focus()};
$('resume').onclick=()=>{try{state=restore(data,saved);const prior=JSON.parse(localStorage.getItem(sessionKey)||'null');if(prior){activeMs=prior.activeMs||0;events=prior.events||[]}record('resume');paint()}catch{$('status').textContent='이전 검토 북마크를 읽을 수 없습니다. 첫 응답부터 다시 시작해 주세요.'}};
$('export-review').onclick=()=>{if(!data)return;const payload={scope:'Chapter 0 continuation only; opening time excluded. Active preview time, not first-player measurement.',active_ms:Math.round(activeMs),state,events};const url=URL.createObjectURL(new Blob([JSON.stringify(payload,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='Live49-ch00-continuation-review.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),5000)};
try{const response=await fetch('/design/chapter00/continuation-v1/story.json');if(!response.ok)throw Error('story data missing');data=await response.json();const checkpoint=new URLSearchParams(location.search).get('review');state=['photo-wish','camp-radio'].includes(checkpoint)?reviewCheckpoint(data,checkpoint==='photo-wish'?'photo_wish':'camp_radio'):resolve(data,initialState());if(['photo-wish','camp-radio'].includes(checkpoint))record('review_checkpoint',{note:'Earlier state simulated along default review path; not user playtime.'});$('resume').disabled=!saved;await Promise.all([$('suhyeok').decode(),$('soi').decode(),document.fonts.load('38px Live49','수혁 소이')]);await paint();$('story-stage').focus()}catch(e){$('status').textContent='검토 파일을 불러오지 못했습니다: '+e.message;locked=true}
