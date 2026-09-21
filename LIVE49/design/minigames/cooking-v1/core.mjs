export const fresh=()=>({phase:'ready',gas:3,ingredients:3,meals:0,minutes:0,heat:20,level:2,time:0,simmer:0,good:0,spill:0,boiled:false,quality:null});
export function start(s){if(s.phase!=='ready'||s.gas<1||s.ingredients<1)return s;return {...s,phase:'warming',gas:s.gas-1,ingredients:s.ingredients-1,minutes:s.minutes+25}}
export function select(s,level){return ['warming','simmering'].includes(s.phase)&&[1,2,3].includes(level)?{...s,level}:s}
export function step(s,dt){
 if(!['warming','simmering'].includes(s.phase)||!Number.isFinite(dt)||dt<=0)return s;
 const d=Math.min(.1,dt),n={...s};n.time+=d;n.heat+=([0,55,84,120][s.level]-s.heat)*(1-Math.exp(-d/2.8));
 if(n.heat>=78)n.boiled=true;
 if(s.phase==='warming'&&((n.boiled&&n.time>=5)||n.time>=10)){n.phase='simmering'}
 if(s.phase==='simmering'){n.simmer+=d;if(n.heat>=62&&n.heat<=78)n.good+=d;if(n.heat>86)n.spill+=d;
  if(n.simmer>=10){n.phase='done';n.level=0;n.meals++;n.quality=n.boiled&&n.good>=6&&n.spill<1.2?'great':'normal'}
 }return n;
}
