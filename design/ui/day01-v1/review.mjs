const root='../../../';
const ui=Object.fromEntries(['scenes','stage','frame','hits','back','next','begin','notes','branches','files','branch','error'].map(id=>[id,document.getElementById(id)]));
try {
const manifest=await fetch('./manifest.json').then(r=>{if(!r.ok)throw Error('manifest '+r.status);return r.json()});
const flow=await fetch(root+'design/chapter01/day01-flow-v1.json').then(r=>r.json());
let index=0,selectedNode='fridge';const screens=manifest.screens;const sequence=screens.slice(0,15);
for(const s of screens){const o=new Option(s.id+' · '+s.label,s.id);ui.scenes.add(o)}
function hit(box,label,fn){const b=document.createElement('button');b.className='hit';b.setAttribute('aria-label',label);const span=document.createElement('span');span.textContent=label;b.append(span);const [x,y,w,h]=box;b.style.cssText=`left:${x/19.2}%;top:${y/10.8}%;width:${w/19.2}%;height:${h/10.8}%`;b.onclick=fn;ui.hits.append(b)}
function linkButton(label,fn){const b=document.createElement('button');b.textContent=label;b.onclick=fn;ui.branches.append(b)}
function show(id){index=screens.findIndex(s=>s.id===id);if(index<0)index=0;const s=screens[index];ui.scenes.value=s.id;ui.frame.src=root+s.preview;ui.frame.alt=s.label;ui.notes.textContent=s.review_note;ui.branch.textContent='';ui.hits.replaceChildren();ui.branches.replaceChildren();ui.files.replaceChildren();ui.back.disabled=index===0;ui.next.disabled=index===screens.length-1;const a=document.createElement('a');a.href=root+s.psd;a.textContent='이 화면 UI PSD';ui.files.append(a);history.replaceState(null,'','#'+s.id);
 for(const next of s.next){const t=screens.find(x=>x.id===next);linkButton(t.label,()=>show(next))}
 if(s.id==='D1-02-map'){hit([860,300,140,130],'편의점 · 이동 20분',()=>{ui.branch.textContent='편의점 선택됨 · 캠핑카로 이동 20분 (수치 시안)'});hit([1270,630,450,86],'편의점으로 출발하는 장면 보기',()=>show('D1-03-arrival'))}
 if(s.id==='D1-04-store'){
   for(const n of flow.search_nodes)hit(n.rect.map((v,i)=>v*(i%2===0?1920/1672:1080/941)),n.label+' 탐색 목록 보기',()=>{selectedNode=n.id;show(n.id==='fridge'?'D1-05-search':'D1-05-'+n.id)});
   for(const n of flow.story_hotspots)hit(n.rect.map((v,i)=>v*(i%2===0?1920/1672:1080/941)),n.label,()=>show(n.id==='notice'?'D1-08-note':'D1-09-return'));
 }
 if(s.id.startsWith('D1-05-')){selectedNode=s.id==='D1-05-search'?'fridge':s.id.slice(6);hit([410,745,530,92],'빠른 탐색 결과 예시',()=>show(selectedNode==='fridge'?'D1-07-result':'D1-07-'+selectedNode));hit([970,745,530,92],'집중 탐색 미니게임 화면 시안',()=>show('D1-06-minigame'))}
 if(s.id==='D1-06-minigame'){ui.branches.replaceChildren();const result=()=>show(selectedNode==='fridge'?'D1-07-result':'D1-07-'+selectedNode);hit([710,741,500,76],'결과 예시 보기',result);linkButton('선택한 지점의 결과 예시',result)}
 if(s.id==='D1-07-result')hit([610,674,690,90],'편의점 화면으로',()=>show('D1-04-store'));
 if(s.id.startsWith('D1-07-')&&s.id!=='D1-07-result')hit([610,718,690,90],'편의점 화면으로',()=>show('D1-04-store'));
 if(s.id==='D1-10-ration')hit([615,778,690,88],'분배 이후 장면 시안',()=>show('D1-11-kitchen'));
 if(s.id==='D1-12-soi')hit([1080,300,135,225],'소이의 부탁 확인',()=>show('D1-13-quest'));
 if(s.id==='C0-03c-choice'){hit([660,395,600,88],'다른 데 가 보고 싶어?',()=>show('C0-03c-book'));hit([660,510,600,88],'어디로 가고 싶은데?',()=>show('C0-03c-book'))}
 if(s.id==='D1-13-quest')linkButton('색연필을 이미 가진 경우의 연결',()=>{ui.branch.textContent='수혁: 마침 챙겨 온 게 있어. → 소이에게 색연필 건네기. 기존 책 사용, 재탐색 목표 없음.'});
 ui.next.disabled=s.id==='D1-13-quest';
}
ui.scenes.onchange=()=>show(ui.scenes.value);ui.back.onclick=()=>show(index>=15?'D1-04-store':screens[Math.max(0,index-1)].id);ui.next.onclick=()=>show(index>=15?screens[index].next[0]:sequence[Math.min(sequence.length-1,index+1)].id);ui.begin.onclick=()=>show('D1-01-need');
ui.frame.onerror=()=>{ui.error.textContent='이미지 로드 실패. 로컬 검토 서버를 통해 열어 주세요.'};
show(location.hash.slice(1)||'D1-01-need');
}catch(e){ui.error.textContent='검토 파일을 읽지 못했습니다. 로컬 서버를 통해 열어 주세요. '+e.message}
