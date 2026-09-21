export const VERSION=1;
export const SPOTS=[
 {id:'fridge',name:'냉장고',description:'문 안쪽의 병을 조심스럽게 꺼냅니다.',noise:.25,items:[{id:'water',name:'생수',count:2,p:1},{id:'water',name:'추가 생수',count:1,p:.45},{id:'drink',name:'밀봉 음료',count:1,p:.3}]},
 {id:'food',name:'식품 선반',description:'남아 있는 포장 식량을 살펴봅니다.',noise:.18,items:[{id:'food',name:'포장 식량',count:1,p:.65},{id:'can',name:'통조림',count:1,p:.4}]},
 {id:'box',name:'생활용품 상자',description:'부품 사이에서 쓸 만한 재료를 찾습니다.',noise:.3,items:[{id:'parts',name:'부품',count:1,p:.55},{id:'cloth',name:'천 조각',count:1,p:.65},{id:'tape',name:'테이프',count:1,p:.35}]},
 {id:'pencils',name:'문구대',description:'선반에 남은 그림 도구를 확인합니다.',noise:.15,items:[{id:'pencils',name:'색연필',count:1,p:.4},{id:'cloth',name:'천 조각',count:1,p:.4}]}
];
export const fresh=()=>({version:VERSION,phase:'explore',minutes:0,inventory:{},searched:[],caution:0,pending:null,last:null});
export const chance=(p,success)=>Math.min(1,p+(success?.2:0));
export const noiseChance=(spot,caution)=>Math.min(.65,spot.noise+caution*.1);
export function marker(seconds,slow=false){const n=(seconds/(slow?4:2.6))%2;return n<=1?n:2-n}
export function hit(position,slow=false){return position>=(slow?.32:.4)&&position<=(slow?.68:.6)}
function copy(s){return structuredClone(s)}
export function begin(s,id,mode,random=Math.random){
 if(s.phase!=='explore'||s.searched.includes(id)||!['quick','focus'].includes(mode))throw Error('Unavailable search');
 const spot=SPOTS.find(x=>x.id===id);if(!spot)throw Error('Unknown spot');
 const n=copy(s);n.phase='playing';n.minutes+=mode==='quick'?5:15;
 n.pending={id,mode,rolls:spot.items.map(()=>random()),noiseRoll:random(),noiseRisk:noiseChance(spot,s.caution)};return n;
}
export function finish(s,success=false,reason='played'){
 if(s.phase!=='playing'||!s.pending)return s;
 const n=copy(s),p=n.pending,spot=SPOTS.find(x=>x.id===p.id);success=success&&p.mode==='focus';
 const items=spot.items.map((item,i)=>({...item,probability:chance(item.p,success),got:p.rolls[i]<chance(item.p,success)?item.count:0}));
 for(const i of items)if(i.got)n.inventory[i.id]=(n.inventory[i.id]||0)+i.got;
 n.searched.push(p.id);n.last={id:p.id,mode:p.mode,success,reason,items,noise:p.noiseRoll<p.noiseRisk,noiseRisk:p.noiseRisk};
 n.pending=null;n.phase='result';return n;
}
export function acknowledge(s){if(s.phase!=='result')return s;const n=copy(s);n.phase=n.last.noise?'warning':'explore';return n}
export function decide(s,choice){
 if(!['warning','explore'].includes(s.phase)||!['stay','return'].includes(choice))return s;
 const n=copy(s);if(choice==='return')n.phase='returned';else if(s.phase==='warning'){n.caution++;n.phase='explore'}return n;
}
export function restore(raw){
 try{const s=JSON.parse(raw);if(!s||s.version!==VERSION||!['explore','playing','result','warning','returned'].includes(s.phase)||!Array.isArray(s.searched)||!s.inventory||!Number.isFinite(s.minutes)||!Number.isFinite(s.caution))return fresh();
 if(s.phase==='playing'){if(!s.pending||!SPOTS.some(x=>x.id===s.pending.id)||!Array.isArray(s.pending.rolls))return fresh();return finish(s,false,'interrupted')}
 if(['result','warning'].includes(s.phase)&&!s.last)return fresh();return s;
 }catch{return fresh()}
}
