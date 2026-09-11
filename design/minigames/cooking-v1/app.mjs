import{fresh,start,select,step}from'./core.mjs';
const $=id=>document.getElementById(id);let s=fresh(),paused=false,raf=0,last=0,sound=false,ctx,osc,gain;
const active=()=>['warming','simmering'].includes(s.phase);
function audio(){if(!sound)return;try{ctx??=new AudioContext();ctx.resume();if(!osc){osc=ctx.createOscillator();gain=ctx.createGain();osc.type='triangle';gain.gain.value=0;osc.connect(gain).connect(ctx.destination);osc.start()}}catch{}}
function render(){
 $('resources').innerHTML=`<span>가스 ${s.gas}</span><span>식재료 ${s.ingredients}</span><span>완성 식사 ${s.meals}</span><span>사용한 게임 시간 ${s.minutes}분</span>`;
 $('heat-text').textContent=Math.round(s.heat);$('needle').style.left=s.heat/120*100+'%';$('zone').style.left=(s.phase==='simmering'?62:78)/120*100+'%';$('zone').style.width=(s.phase==='simmering'?16:10)/120*100+'%';$('target').textContent=s.phase==='done'?'조리 완료 · 불 꺼짐':s.phase==='simmering'?'잔잔한 끓음 유지':'먼저 끓여 주세요';
 $('progress').style.width=s.simmer*10+'%';$('progress-text').textContent=Math.min(100,Math.floor(s.simmer*10))+'%';
 $('start').hidden=s.phase!=='ready';$('levels').hidden=!active();$('again').hidden=s.phase!=='done';$('pause').disabled=!active();$('pause').textContent=paused?'이어서 하기':'잠깐 멈추기';document.body.classList.toggle('paused',paused);
 document.querySelectorAll('[data-level]').forEach(b=>{b.setAttribute('aria-pressed',String(+b.dataset.level===s.level));b.disabled=paused});
 $('warm-step').classList.toggle('active',s.phase==='warming');$('simmer-step').classList.toggle('active',s.phase==='simmering');$('done-step').classList.toggle('active',s.phase==='done');
 $('flames').style.opacity=active()?'1':'0';$('flames').style.transform=`scaleY(${s.level/3})`;$('knob').style.transform=`rotate(${(s.level-2)*55}deg)`;
 $('bubbles').style.opacity=active()?Math.max(0,Math.min(1,(s.heat-50)/40)):0;$('steam').style.opacity=active()?Math.max(0,(s.heat-40)/100):s.phase==='done'?.3:0;$('overflow').style.opacity=active()&&s.heat>92?.85:0;
 let title='오늘은 따뜻한 수프.',hint='처음에는 불을 올려 데우고, 끓기 시작하면 낮춰서 잔잔하게 익혀 주세요.',scene='요리할 준비가 됐습니다.';
 if(active()){if(paused){title='잠깐 쉬어가기';hint='불과 조리 시간이 멈췄습니다. 이어서 하기를 눌러 주세요.';scene='일시 정지'}else if(s.phase==='warming'){title=s.boiled?'끓기 시작했어요. 불을 낮춰 주세요.':'먼저 냄비를 데워 주세요.';hint='중불이나 강불로 데워 보세요. 기포가 커지면 미리 낮춰도 됩니다.';scene=s.boiled?'보글보글, 이제 불을 낮출 때입니다.':'냄비가 천천히 데워집니다.'}else{title=s.heat>86?'넘치기 전에 불을 낮춰 주세요.':s.heat<62?'조금 더 따뜻하게.':'잔잔하게 익혀 주세요.';hint='표시된 구간을 유지하세요. 불을 낮춰도 냄비는 천천히 식습니다.';scene=s.heat>86?'끓음이 거세지고 있습니다.':'남은 열기로 재료를 익힙니다.'}}
 if(s.phase==='done'){title=s.quality==='great'?'잘 만든 따뜻한 식사.':'한 끼가 완성됐습니다.';hint=s.quality==='great'?'알맞은 끓음을 유지했어요. 기본 식사 1개 + 품질 보너스 표시.':'남은 조리는 자동으로 마무리했습니다. 기본 식사 1개를 챙깁니다. 추가 가스는 쓰지 않았어요.';scene='불을 끄고, 수프를 담아냅니다.'}
 $('heading').textContent=title;if($('hint').textContent!==hint)$('hint').textContent=hint;$('scene-status').textContent=scene;$('phase').textContent={ready:'준비',warming:'01 · 데우기',simmering:'02 · 익히기',done:'03 · 완성'}[s.phase];$('time').textContent=active()?`${s.time.toFixed(1)}초 경과`:s.phase==='done'?`${s.time.toFixed(1)}초 조작 완료`:'조작 약 15~20초';
 $('feedback').textContent=s.phase==='done'?`적정 열기 ${s.good.toFixed(1)}초 / 거센 끓음 ${s.spill.toFixed(1)}초 · 품질 효과는 본편 미연결`:'선택한 불은 바로 바뀌고, 냄비의 열기는 천천히 따라옵니다.';
 if(gain){osc.frequency.setTargetAtTime(90+s.heat,ctx.currentTime,.1);gain.gain.setTargetAtTime(sound&&active()&&!paused?.009:0,ctx.currentTime,.05)}
}
function tick(now){let d=Math.min(.15,(now-last)/1000);last=now;if(!paused&&!document.hidden)while(d>0){const a=Math.min(.05,d);s=step(s,a);d-=a}render();if(active())raf=requestAnimationFrame(tick)}
function launch(){s=start(s);if(!active())return;paused=false;last=performance.now();audio();render();raf=requestAnimationFrame(tick)}
function reset(){cancelAnimationFrame(raf);s=fresh();paused=false;render()}
function change(n){if(paused)return;s=select(s,n);render()}
$('start').onclick=launch;$('again').onclick=()=>{reset();launch()};$('reset').onclick=reset;document.querySelectorAll('[data-level]').forEach(b=>b.onclick=()=>change(+b.dataset.level));document.addEventListener('keydown',e=>{if(active()&&['1','2','3'].includes(e.key)){e.preventDefault();change(+e.key)}});
function pause(){if(active()){paused=true;render()}}$('pause').onclick=()=>{paused=!paused;last=performance.now();render()};window.addEventListener('blur',pause);document.addEventListener('visibilitychange',()=>{if(document.hidden)pause()});$('sound').onclick=()=>{sound=!sound;$('sound').textContent=sound?'소리 끄기':'소리 켜기';$('sound').setAttribute('aria-pressed',String(sound));audio();render()};render();

