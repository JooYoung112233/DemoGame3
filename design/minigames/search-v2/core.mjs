import {SPOTS,noiseChance} from '../search-v1/core.mjs';
export {SPOTS,noiseChance};
export const fresh=()=>({version:2,phase:'explore',minutes:0,inventory:{},searched:[],caution:0,pending:null,last:null});
const clone=s=>structuredClone(s);
export function begin(s,id,mode,random=Math.random){
 if(s.phase!=='explore'||s.searched.includes(id)||!['quick','focus'].includes(mode))throw Error('Unavailable');
 const spot=SPOTS.find(x=>x.id===id);if(!spot)throw Error('Unknown spot');
 const n=clone(s);n.minutes+=mode==='quick'?5:10;n.phase='playing';
 n.pending={id,mode,rolls:spot.items.map(()=>random()),noiseRoll:random(),risk:noiseChance(spot,s.caution),bonus:0,failures:0,checks:[],secured:[],stage:0,schedule:[1.8+random()*.5,5.4+random()*.5,10+random()*.5],centers:[.5+random()*.22,.5+random()*.22,.5+random()*.22]};return n;
}
export function grade(position,center,slow=false){const d=Math.abs(position-center);return d<=.035?'great':d<=(slow?.16:.11)?'good':'miss'}
export function check(s,index,outcome){
 if(s.phase!=='playing'||s.pending.mode!=='focus'||s.pending.checks.some(x=>x.index===index)||!['good','great','miss'].includes(outcome)||index!==(s.pending.checks.length)||index>2)return s;
 if((s.pending.stage===0&&index>1)||(s.pending.stage===1&&index<2))return s;
 const n=clone(s),p=n.pending;p.checks.push({index,outcome});if(outcome==='great')p.bonus=Math.min(.2,p.bonus+.1);if(outcome==='miss'){p.failures++;p.risk=Math.min(.95,p.risk+.25)}return n;
}
function award(n,indices){const p=n.pending,spot=SPOTS.find(x=>x.id===p.id);for(const index of indices){if(p.secured.some(x=>x.index===index))continue;const item=spot.items[index],probability=Math.min(1,item.p+p.bonus),got=p.rolls[index]<probability?item.count:0;p.secured.push({...item,index,probability,got});if(got)n.inventory[item.id]=(n.inventory[item.id]||0)+got}}
export function checkpoint(s){if(s.phase!=='playing'||s.pending.stage!==0||s.pending.mode!=='focus'||s.pending.checks.length!==2)return s;const n=clone(s);award(n,[0]);n.phase='checkpoint';return n}
export function proceed(s){if(s.phase!=='checkpoint')return s;const n=clone(s);n.minutes+=5;n.pending.stage=1;n.phase='playing';return n}
export function finish(s,reason='complete'){
 if(!['playing','checkpoint'].includes(s.phase)||!s.pending)return s;
 if(reason==='complete'&&s.pending.mode==='focus'&&(s.pending.stage!==1||s.pending.checks.length!==3))return s;
 const n=clone(s),p=n.pending;
 if(reason==='complete')award(n,SPOTS.find(x=>x.id===p.id).items.map((_,i)=>i));
 n.last={...p,items:p.secured,reason,noise:p.noiseRoll<p.risk};n.searched.push(p.id);n.pending=null;n.phase='result';return n;
}
export function acknowledge(s){if(s.phase!=='result')return s;const n=clone(s);n.phase=n.last.noise?'warning':'explore';return n}
export function decide(s,choice){if(!['explore','warning'].includes(s.phase))return s;const n=clone(s);if(choice==='return')n.phase='returned';else if(choice==='stay'&&n.phase==='warning'){n.caution++;n.phase='explore'}return n}
export function restore(raw){try{const s=JSON.parse(raw);if(s?.version!==2||!Array.isArray(s.searched)||!s.inventory||!Number.isFinite(s.minutes)||!['explore','playing','checkpoint','result','warning','returned'].includes(s.phase))return fresh();if(['playing','checkpoint'].includes(s.phase))return finish(s,'interrupted');if(['result','warning'].includes(s.phase)&&!s.last)return fresh();return s}catch{return fresh()}}
