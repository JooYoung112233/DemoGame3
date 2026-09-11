import{SPOTS,fresh,begin,check,grade,checkpoint,proceed,finish,acknowledge,decide,restore,noiseChance}from'./core.mjs';
const $=id=>document.getElementById(id),KEY='live49-search-v2';let s=fresh(),selected='fridge',slow=false,sound=false,audio,raf=0,elapsed=0,lastTick=0,cue=-1,feedback='주변을 살피며 물건을 뒤집니다.';
try{s=restore(localStorage.getItem(KEY))}catch{}
const save=()=>{try{localStorage.setItem(KEY,JSON.stringify(s))}catch{$('storage').textContent='저장 불가 · 이번 창에서만 유지됩니다.'}};
const pct=n=>Math.round(n*100)+'%';
function tone(kind){if(!sound)return;try{audio??=new AudioContext();audio.resume();const o=audio.createOscillator(),g=audio.createGain();o.type=kind==='miss'?'triangle':'sine';o.frequency.value=kind==='cue'?700:kind==='miss'?110:420;g.gain.setValueAtTime(.045,audio.currentTime);g.gain.exponentialRampToValueAtTime(.001,audio.currentTime+.3);o.connect(g).connect(audio.destination);o.start();o.stop(audio.currentTime+.3)}catch{}}
function act(fn){s=fn(s);save();render()}
function resultTable(items){return `<table><tr><th>물자</th><th>판정 확률</th><th>확보</th></tr>${items.map(i=>`<tr><td>${i.name}</td><td>${pct(i.probability)}</td><td>${i.got}개</td></tr>`).join('')}</table>`}
function render(){
 if(s.phase==='explore'&&s.searched.includes(selected)&&s.searched.length<4)selected=SPOTS.find(x=>!s.searched.includes(x.id)).id;
 $('time').textContent=`사용한 게임 시간 ${s.minutes}분`;
 $('spots').innerHTML=SPOTS.map(x=>`<button data-spot="${x.id}" class="${x.id===selected?'selected':''}" ${s.phase!=='explore'||s.searched.includes(x.id)?'disabled':''}>${x.name}${s.searched.includes(x.id)?' · 완료':''}</button>`).join('');
 document.querySelectorAll('[data-spot]').forEach(b=>b.onclick=()=>{selected=b.dataset.spot;render()});
 const names=Object.fromEntries(SPOTS.flatMap(x=>x.items).map(x=>[x.id,x.name.replace('추가 ', '')]));
 $('inventory').innerHTML=`<div class="bag">${Object.entries(s.inventory).map(([id,n])=>`${names[id]} × ${n}`).join('<br>')||'아직 확보한 물자가 없습니다.'}</div>`;$('return').disabled=s.phase!=='explore';
 const panel=$('panel');
 if(s.phase==='explore'){
  if(s.searched.length===4){panel.innerHTML='<h2>모든 곳을 확인했습니다.</h2><p>물자를 챙겨 캠핑카로 돌아갑니다.</p>';return}
  const x=SPOTS.find(x=>x.id===selected);panel.innerHTML=`<span class="tag">진행형 탐색 · V2</span><h2>${x.name}</h2><p>${x.description}</p><table><tr><th>물자</th><th>기본 확률</th><th>집중 탐색 확보 시점</th></tr>${x.items.map((i,j)=>`<tr><td>${i.name} ${i.count}개</td><td>${pct(i.p)}</td><td>${j===0?'중간 확인':'끝까지 탐색'}</td></tr>`).join('')}</table><p class="small">일반 성공: 조용히 진행 / 금색 대성공: 이후 획득 확률 +10%p (최대 +20%p)<br>실패: 소음 위험 +25%p. 현재 기본 소음 위험 ${pct(noiseChance(x,s.caution))}. 실패해도 확보한 물자는 유지됩니다.</p><label><input id="slow" type="checkbox" ${slow?'checked':''}> 여유로운 판정 (느린 회전 · 넓은 성공 구간)</label><div class="actions"><button id="quick">빠른 탐색 · 5분</button><button id="focus" class="primary">집중 탐색 · 10분 + 선택 5분</button></div><p class="small">집중 탐색: 조작 약 14초${slow?'~17초':''}, 판정 3회. 중간 선택 동안 시간 정지. Space 또는 판정 버튼 클릭.</p>`;
  $('slow').onchange=e=>{slow=e.target.checked};$('quick').onclick=()=>{s=begin(s,selected,'quick');act(x=>finish(x))};$('focus').onclick=start;
 }else if(s.phase==='playing'){
  panel.innerHTML=`<span class="tag">${s.pending.stage?'추가 물자 탐색':'기본 물자 탐색'}</span><h2>소리를 듣고, 걸리는 순간을 넘겨 주세요.</h2><div class="progress"><div id="fill"></div></div><div class="readouts"><span id="progress-text"></span><span id="risk"></span></div><div class="check-layout"><div class="dial" id="dial"><div class="hand" id="hand"></div><div class="hub"></div><span id="dial-label">탐색 중</span></div><div><p id="feedback" role="status"></p><button id="strike" class="primary">판정 · Space</button><p class="small">초록: 일반 성공 · 금색: 대성공<br>예고 표시 뒤 바늘이 한 바퀴 돕니다.</p></div></div><p class="small">중간 확인까지 10분. 계속 탐색하면 5분 추가. 판정 밖 입력은 무시됩니다.</p>`;
  $('strike').onclick=strike;update();
 }else if(s.phase==='checkpoint'){
  const p=s.pending;panel.innerHTML=`<span class="tag">중간 확인 · 조작 일시 정지</span><h2>${p.secured.some(x=>x.got)?'여기까지는 챙겼습니다.':'첫 물자는 찾지 못했습니다.'}</h2>${resultTable(p.secured)}<p>${p.failures?'물건이 부딪히는 소리가 났습니다.':'아직 큰 소리를 내지 않았습니다.'}<br>현재 소음 위험 ${pct(p.risk)} · 확률 보너스 +${Math.round(p.bonus*100)}%p</p><p>안쪽에는 ${SPOTS.find(x=>x.id===p.id).items.slice(1).map(x=>x.name).join(', ')}도 남아 있을 수 있습니다.</p><div class="actions"><button id="bank">확보한 것만 챙겨 끝내기</button><button id="continue" class="primary">더 뒤지기 · 게임 시간 +5분</button></div>`;
  $('bank').onclick=()=>act(x=>finish(x,'banked'));$('continue').onclick=()=>{s=proceed(s);save();lastTick=performance.now();feedback='안쪽 물건을 조심스럽게 꺼냅니다.';render();raf=requestAnimationFrame(tick)};
 }else if(s.phase==='result'){
  const r=s.last;panel.innerHTML=`<span class="tag">탐색 결과</span><h2>${r.reason==='interrupted'?'중단된 탐색을 정리했습니다.':r.reason==='banked'?'여기서 탐색을 마쳤습니다.':'탐색을 마쳤습니다.'}</h2>${r.items.length?resultTable(r.items):'<p>중단 전에 확보한 물자가 없습니다.</p>'}<p>${r.mode==='focus'?`판정 ${r.checks.length}회 · 실패 ${r.failures}회 · 최종 확률 보너스 +${Math.round(r.bonus*100)}%p`: '빠른 탐색 · 기본 확률 적용'}</p><p class="small">${r.reason==='interrupted'?'새로고침 이전에 확보한 물자만 보존했습니다. 미확보 물자를 다시 추첨하지 않습니다.':'각 물자는 확보 당시의 확률로 한 번만 판정합니다.'}</p><button id="ack" class="primary">${r.noise?'주변 인기척 확인':'다음 탐색 살펴보기'}</button>`;
  $('ack').onclick=()=>{if(s.last.noise)tone('miss');act(acknowledge)};
 }else if(s.phase==='warning'){
  panel.innerHTML='<div class="warning"><span class="tag">소음 반응</span><h2>밖에서 무언가 움직입니다.</h2><p>확보한 물자는 그대로 가지고 있습니다.<br>계속 머물면 다음 탐색의 기본 소음 위험이 10%p 높아집니다.</p><div class="actions"><button id="stay">조금 더 살펴보기</button><button id="leave" class="primary">캠핑카로 귀환</button></div><p class="small">전투 연결 전 시안입니다.</p></div>';$('stay').onclick=()=>act(x=>decide(x,'stay'));$('leave').onclick=()=>act(x=>decide(x,'return'));
 }else{panel.innerHTML=`<h2>캠핑카로 돌아왔습니다.</h2><p>게임 시간 ${s.minutes}분 사용. 확보한 물자는 가방에 유지됩니다.</p><button id="again">새 탐색 시작</button>`;$('again').onclick=reset}
}
const duration=()=>slow?1.9:1.25;
function active(){if(s.phase!=='playing')return -1;return s.pending.schedule.findIndex((t,i)=>!s.pending.checks.some(x=>x.index===i)&&elapsed>=t&&elapsed<=t+.8+duration())}
function start(){s=begin(s,selected,'focus');save();elapsed=0;cue=-1;feedback='주변을 살피며 물건을 뒤집니다.';lastTick=performance.now();render();raf=requestAnimationFrame(tick)}
function update(){if(s.phase!=='playing')return;const p=s.pending,i=active(),t=i<0?0:elapsed-p.schedule[i],rotating=i>=0&&t>=.8,center=p.centers[Math.max(0,i)],width=slow?.16:.11;
 $('fill').style.width=Math.min(100,elapsed/14*100)+'%';$('progress-text').textContent=`탐색 진행 ${Math.min(100,Math.floor(elapsed/14*100))}% · 판정 ${p.checks.length}/3`;$('risk').textContent=`소음 위험 ${pct(p.risk)}`;
 $('dial').style.background=i<0?'#263128':`conic-gradient(from 0deg,#263128 0deg,#263128 ${(center-width)*360}deg,#789265 ${(center-width)*360}deg,#789265 ${(center-.035)*360}deg,#e9bd63 ${(center-.035)*360}deg,#e9bd63 ${(center+.035)*360}deg,#789265 ${(center+.035)*360}deg,#789265 ${(center+width)*360}deg,#263128 ${(center+width)*360}deg)`;
 $('hand').style.transform=`rotate(${rotating?(t-.8)/duration()*360:0}deg)`;$('hand').style.opacity=rotating?'1':'.15';$('dial-label').textContent=i<0?'탐색 중':rotating?'Space':'걸리는 소리!';$('strike').disabled=!rotating;$('feedback').textContent=i>=0&&!rotating?'곧 판정이 시작됩니다.':feedback;
}
function resolve(i,outcome){s=check(s,i,outcome);save();feedback=outcome==='great'?'대성공 · 소리 없이 꺼냈습니다. 획득 확률 +10%p':outcome==='good'?'성공 · 조용히 작업을 이어갑니다.':'덜컹! 물건이 부딪혔습니다. 소음 위험 +25%p';tone(outcome);update()}
function strike(){const i=active();if(i<0)return;const t=elapsed-s.pending.schedule[i]-.8;if(t<0)return;resolve(i,grade(t/duration(),s.pending.centers[i],slow))}
function tick(now){if(s.phase!=='playing')return;if(!document.hidden)elapsed+=(now-lastTick)/1000;lastTick=now;const p=s.pending;
 for(let i=0;i<3;i++){if((p.stage===0&&i===2)||p.checks.some(x=>x.index===i))continue;if(elapsed>=p.schedule[i]&&cue<i){cue=i;tone('cue')}if(elapsed>p.schedule[i]+.8+duration())resolve(i,'miss')}
 if(p.stage===0&&elapsed>=8&&s.pending.checks.length===2){s=checkpoint(s);save();render();return}
 if(p.stage===1&&elapsed>=14&&s.pending.checks.length===3){act(x=>finish(x));return}update();raf=requestAnimationFrame(tick)
}
document.addEventListener('visibilitychange',()=>{lastTick=performance.now()});
document.addEventListener('keydown',e=>{if(e.code==='Space'&&s.phase==='playing'){e.preventDefault();if(!e.repeat)strike()}});
function reset(){cancelAnimationFrame(raf);s=fresh();selected='fridge';save();render()}
$('reset').onclick=()=>{if(confirm('이번 시안의 진행을 초기화할까요?'))reset()};$('return').onclick=()=>act(x=>decide(x,'return'));$('sound').onclick=()=>{sound=!sound;$('sound').textContent=sound?'소리 끄기':'소리 켜기';$('sound').setAttribute('aria-pressed',String(sound));tone('cue')};save();render();
