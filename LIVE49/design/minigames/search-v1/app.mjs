import{SPOTS,fresh,begin,finish,acknowledge,decide,restore,marker,hit,noiseChance}from'./core.mjs';
const $=id=>document.getElementById(id),KEY='live49-search-prototype-v1';let s=fresh(),selected='fridge',slow=false,sound=false,audio,start=0,elapsed=0,hiddenAt=0,raf=0;
try{s=restore(localStorage.getItem(KEY))}catch{$('storage').textContent='저장 공간을 사용할 수 없어 이번 창에서만 진행합니다.'}
const save=()=>{try{localStorage.setItem(KEY,JSON.stringify(s))}catch{$('storage').textContent='자동 저장 불가 · 창을 닫으면 진행이 유실될 수 있습니다.'}};
save();
function beep(type){if(!sound)return;try{audio??=new AudioContext();audio.resume();const o=audio.createOscillator(),g=audio.createGain();o.type='sine';o.frequency.setValueAtTime(type==='noise'?100:type==='success'?510:290,audio.currentTime);o.frequency.exponentialRampToValueAtTime(type==='noise'?55:220,audio.currentTime+.22);g.gain.setValueAtTime(.035,audio.currentTime);g.gain.exponentialRampToValueAtTime(.001,audio.currentTime+.25);o.connect(g).connect(audio.destination);o.start();o.stop(audio.currentTime+.26)}catch{}}
const pct=n=>Math.round(n*100)+'%';
function act(fn){s=fn(s);save();render()}
function render(){
 if(s.phase==='explore'&&s.searched.includes(selected)&&s.searched.length<SPOTS.length)selected=SPOTS.find(x=>!s.searched.includes(x.id)).id;
 $('time').textContent=`탐색에 쓴 시간 ${s.minutes}분`;
 $('spots').innerHTML=SPOTS.map(x=>`<button data-spot="${x.id}" class="${selected===x.id?'selected':''}" ${s.phase!=='explore'||s.searched.includes(x.id)?'disabled':''}>${x.name}${s.searched.includes(x.id)?' · 확인 완료':''}</button>`).join('');
 document.querySelectorAll('[data-spot]').forEach(b=>b.onclick=()=>{selected=b.dataset.spot;render()});
 const names=Object.fromEntries(SPOTS.flatMap(x=>x.items).map(x=>[x.id,x.name.replace('추가 ', '')]));
 $('inventory').innerHTML=`<div class="bag">${Object.entries(s.inventory).map(([id,n])=>`${names[id]} × ${n}`).join('<br>')||'아직 챙긴 물자가 없습니다.'}</div>`;
 $('return').disabled=s.phase!=='explore';
 const panel=$('panel');
 if(s.phase==='explore'){
  if(s.searched.length===SPOTS.length){panel.innerHTML='<span class="tag">탐색 완료</span><h2>살펴볼 곳을 모두 확인했습니다.</h2><p>물자를 가지고 캠핑카로 돌아갈 수 있습니다.</p>';return}
  if(s.searched.includes(selected))selected=SPOTS.find(x=>!s.searched.includes(x.id)).id;
  const x=SPOTS.find(x=>x.id===selected);panel.innerHTML=`<span class="tag">선택한 장소</span><h2>${x.name}</h2><p>${x.description}</p><table><thead><tr><th>물자</th><th>빠른 탐색</th><th>집중 성공</th></tr></thead><tbody>${x.items.map(i=>`<tr><td>${i.name} ${i.count}개${i.p===1?' · 확보 보장':''}</td><td>${pct(i.p)}</td><td>${pct(Math.min(1,i.p+.2))}</td></tr>`).join('')}</tbody></table><p class="small">소음 가능성 ${pct(noiseChance(x,s.caution))} · 성공/실패와 별도 판정${s.caution?' · 인기척 이후 경계 상승':''}<br>실패해도 기본 확률 유지. 같은 지점은 한 번만 탐색합니다.</p><label><input id="slow" type="checkbox" ${slow?'checked':''}> 느린 조작 · 넓은 성공 구간</label><div class="actions"><button id="quick">빠르게 살펴보기 · 5분</button><button class="primary" id="focus">조심스럽게 꺼내기 · 총 15분</button></div>`;
  $('slow').onchange=e=>slow=e.target.checked;$('quick').onclick=()=>launch('quick');$('focus').onclick=()=>launch('focus');
 }else if(s.phase==='playing'){
  panel.innerHTML=`<span class="tag">집중 탐색 · ${SPOTS.find(x=>x.id===s.pending.id).name}</span><h2>초록 구간에서 멈춰 주세요.</h2><p>클릭 또는 Space 한 번으로 물건을 꺼냅니다.</p><div class="meter" aria-hidden="true"><div class="safe" style="left:${slow?32:40}%;width:${slow?36:20}%"></div><div class="needle" id="needle"></div></div><p class="small" id="remaining">8초 안에 멈추기</p><button class="primary" id="stop">지금 꺼내기 · Space</button><p class="small">시작 시 시간이 반영됩니다. 창을 새로 열면 보너스 없이 기본 확률로 마무리됩니다.</p>`;$('stop').onclick=()=>stop(false);
 }else if(s.phase==='result'){
  const r=s.last;panel.innerHTML=`<span class="tag">물자 확보</span><h2>${r.mode==='quick'?'빠른 탐색을 마쳤습니다.':r.success?'조심스럽게 꺼냈습니다.':'기본 확률로 탐색을 마쳤습니다.'}</h2>${r.reason==='interrupted'?'<p>중단된 탐색을 복구했습니다. 시간·보상은 중복 적용하지 않습니다.</p>':''}<table><thead><tr><th>물자</th><th>적용 확률</th><th>획득</th></tr></thead><tbody>${r.items.map(i=>`<tr><td>${i.name}</td><td>${pct(i.probability)}</td><td>${i.got}개</td></tr>`).join('')}</tbody></table><p>획득한 물자는 가방에 넣었습니다.</p><button id="ack" class="primary">${r.noise?'주변 소리 확인':'다음 탐색 살펴보기'}</button>`;$('ack').onclick=()=>{if(s.last.noise)beep('noise');act(acknowledge);};
 }else if(s.phase==='warning'){
  panel.innerHTML='<div class="warning"><span class="tag">소음 · 반응 선택</span><h2>물건이 부딪혔습니다.</h2><p>가게 밖에서 희미한 인기척이 들립니다.<br>무엇인지는 아직 알 수 없습니다.</p><p class="small">챙긴 물자는 유지됩니다. 계속 탐색하면 이후 소음 가능성이 10%p 올라갑니다. 이 시안에는 전투가 없습니다.</p><div class="actions"><button id="stay">조금 더 살펴보기</button><button class="primary" id="leave">캠핑카로 귀환</button></div></div>';$('stay').onclick=()=>act(x=>decide(x,'stay'));$('leave').onclick=()=>act(x=>decide(x,'return'));
 }else{panel.innerHTML=`<span class="tag">귀환 완료</span><h2>캠핑카로 돌아왔습니다.</h2><p>탐색에 ${s.minutes}분을 사용했습니다.<br>왼쪽 가방의 물자를 가지고 다음 분배 장면으로 이어집니다.</p><p class="small">이번 시안은 여기까지입니다. 실제 분배·퀘스트·전투 시스템에는 아직 연결하지 않았습니다.</p><button id="again">새 탐색 시작</button>`;$('again').onclick=reset;}
}
function launch(mode){s=begin(s,selected,mode);save();if(mode==='quick'){s=finish(s);save();beep('done');render();return}start=performance.now();elapsed=0;hiddenAt=0;render();raf=requestAnimationFrame(tick)}
function tick(now){if(s.phase!=='playing')return;if(!document.hidden){elapsed=(now-start)/1000;$('needle').style.left=marker(elapsed,slow)*100+'%';$('remaining').textContent=`${Math.max(0,8-elapsed).toFixed(1)}초 남음`;if(elapsed>=8){stop(true);return}}raf=requestAnimationFrame(tick)}
function stop(timeout){if(s.phase!=='playing')return;cancelAnimationFrame(raf);const seconds=(performance.now()-start)/1000;const success=!timeout&&seconds<8&&hit(marker(seconds,slow),slow);s=finish(s,success,timeout?'timeout':'played');save();beep(success?'success':'done');render()}
document.addEventListener('visibilitychange',()=>{if(s.phase!=='playing')return;if(document.hidden)hiddenAt=performance.now();else if(hiddenAt){start+=performance.now()-hiddenAt;hiddenAt=0}});
document.addEventListener('keydown',e=>{if(e.code==='Space'&&s.phase==='playing'){e.preventDefault();if(!e.repeat)stop(false)}});
function reset(){cancelAnimationFrame(raf);s=fresh();selected='fridge';save();render()}
$('reset').onclick=()=>{if(confirm('이 시안의 시간과 획득 물자를 지우고 새 탐색을 시작할까요?'))reset()};
$('return').onclick=()=>act(x=>decide(x,'return'));
$('sound').onclick=()=>{sound=!sound;$('sound').textContent=sound?'소리 끄기':'소리 켜기';$('sound').setAttribute('aria-pressed',String(sound));if(sound)beep('done')};render();
