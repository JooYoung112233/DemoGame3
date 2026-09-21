import{fresh,start,step}from'./core.mjs';
const $=id=>document.getElementById(id);let s=fresh(),held=false,key=false,pointer=false,paused=false,raf=0,last=0,sound=false,ctx,osc,gain,lastPhase='ready';
function audio(){if(!sound)return;try{ctx??=new AudioContext();ctx.resume();if(!osc){osc=ctx.createOscillator();gain=ctx.createGain();osc.type='triangle';gain.gain.value=0;osc.connect(gain).connect(ctx.destination);osc.start()}}catch{}}
function silence(){if(gain)gain.gain.setTargetAtTime(0,ctx.currentTime,.03)}
function release(){key=false;pointer=false;held=false;$('fan').classList.remove('pressed');silence()}
function render(){
 const playing=s.phase==='playing',done=['success','failed'].includes(s.phase);document.body.classList.toggle('paused',paused);
 $('heat-text').textContent=Math.round(s.heat);$('heat-needle').style.left=s.heat+'%';$('heat-safe').style.left=(s.easy?40:45)+'%';$('heat-safe').style.width=(s.easy?38:27)+'%';
 for(const id of ['air','smoke','progress']){$(id).style.width=s[id]+'%';$(id+'-text').textContent=Math.floor(s[id])+'%'}
 $('clock').textContent=playing?`${Math.ceil((s.easy?40:30)-s.time)}초 남음`:done?`${s.time.toFixed(1)}초 진행`:'준비';
 $('glow').style.transform=`scale(${.5+s.heat/110+(s.phase==='success'?.3:0)})`;$('flames').style.transform=`scale(${s.phase==='success'?1:Math.max(.05,s.heat/150)})`;$('smoke-art').style.opacity=s.smoke/130;$('wind-art').style.opacity=held&&!paused?.7:0;
 $('start').hidden=s.phase!=='ready';$('easy-label').hidden=s.phase!=='ready';$('fan').hidden=!playing;$('fan').disabled=paused;$('again').hidden=!done;$('pause').disabled=!playing;$('pause').textContent=paused?'이어서 하기':'잠깐 멈추기';$('fan').classList.toggle('pressed',held&&!paused);
 if(s.phase==='ready'){$('heading').textContent='세게보다, 알맞게.';$('hint').innerHTML='누르면 부채질하고, 놓으면 쉽니다.<br>바람은 조금 남아 있으니 미리 손을 떼어 보세요.';$('scene-status').textContent='마른 잔가지 아래에 작은 불씨가 남아 있습니다.'}
 else if(playing){const msg=paused?'잠시 멈췄습니다. 이어서 하기를 눌러 주세요.':s.smoke>55?'불씨가 흩어집니다. 손을 떼고 잠깐 쉬세요.':s.heat>72?'충분히 뜨겁습니다. 바람을 줄여 주세요.':s.heat<45?'불씨가 약합니다. 바람을 조금 보내 주세요.':'잘 옮겨붙고 있습니다. 이 열기를 유지하세요.';$('heading').textContent=paused?'잠깐 쉬어가기':held?'후우, 바람을 보냅니다.':'불씨를 지켜봅니다.';if($('hint').textContent!==msg)$('hint').textContent=msg;$('scene-status').textContent=paused?'일시 정지':s.progress>0?'잔가지에 불이 조금씩 옮겨붙습니다.':'작은 불씨가 바람에 반응합니다.'}
 else if(s.phase==='success'){$('heading').textContent='불이 붙었습니다.';$('hint').textContent='잔가지가 스스로 타기 시작합니다. 이제 부채질을 멈춰도 됩니다.';$('scene-status').textContent='작은 불이 자리를 잡았습니다.'}
 else{$('heading').textContent=s.reason==='cold'?'불씨가 꺼졌습니다.':s.reason==='smoke'?'불씨가 흩어졌습니다.':'잔가지에 불이 붙지 않았습니다.';$('hint').textContent=s.reason==='cold'?'열기가 떨어지기 전에 바람을 보내 보세요.':s.reason==='smoke'?'오래 누르기보다, 불씨가 밝아지면 미리 손을 떼어 보세요.':'표시된 구간의 열기를 조금 더 오래 유지해 보세요.';$('scene-status').textContent='소모되는 자원 없이 다시 시도할 수 있습니다.'}
 if(sound&&gain&&playing&&!paused){osc.frequency.setTargetAtTime(held?105+s.air:75,ctx.currentTime,.08);gain.gain.setTargetAtTime(held?.013:.003,ctx.currentTime,.04)}else silence();
 if(lastPhase!==s.phase&&done){release();lastPhase=s.phase}else lastPhase=s.phase;
}
function tick(now){if(s.phase!=='playing')return;const delta=Math.min((now-last)/1000,.15);last=now;if(!paused&&!document.hidden){let remain=delta;while(remain>0){const d=Math.min(.05,remain);s=step(s,held,d);remain-=d}}render();if(s.phase==='playing')raf=requestAnimationFrame(tick)}
function launch(){cancelAnimationFrame(raf);release();paused=false;s=start($('easy').checked);last=performance.now();audio();render();raf=requestAnimationFrame(tick)}
function reset(){cancelAnimationFrame(raf);release();paused=false;s=fresh($('easy').checked);render()}
function sync(){held=(key||pointer)&&s.phase==='playing'&&!paused;audio();render()}
$('start').onclick=launch;$('again').onclick=launch;$('reset').onclick=reset;$('easy').onchange=()=>{s=fresh($('easy').checked);render()};
$('fan').addEventListener('pointerdown',e=>{if(e.button!==0||s.phase!=='playing'||paused)return;e.preventDefault();$('fan').setPointerCapture(e.pointerId);pointer=true;sync()});
for(const event of ['pointerup','pointercancel','lostpointercapture'])$('fan').addEventListener(event,()=>{pointer=false;sync()});
document.addEventListener('keydown',e=>{if(e.code==='Space'&&s.phase==='playing'&&!paused){e.preventDefault();if(!e.repeat){key=true;sync()}}});document.addEventListener('keyup',e=>{if(e.code==='Space'){key=false;sync()}});
function pause(){if(s.phase!=='playing')return;paused=true;release();render()}
$('pause').onclick=()=>{if(paused){paused=false;last=performance.now();render()}else pause()};window.addEventListener('blur',pause);document.addEventListener('visibilitychange',()=>{if(document.hidden)pause()});
$('audio').onclick=()=>{sound=!sound;$('audio').textContent=sound?'소리 끄기':'소리 켜기';$('audio').setAttribute('aria-pressed',String(sound));if(sound)audio();else silence()};render();
