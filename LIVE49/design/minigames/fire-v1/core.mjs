export const fresh=(easy=false)=>({phase:'ready',easy,time:0,heat:34,air:0,smoke:0,progress:0,reason:null});
export function start(easy=false){return {...fresh(easy),phase:'playing'}}
export function step(s,held,dt){
 if(s.phase!=='playing'||!Number.isFinite(dt)||dt<=0)return s;
 const n={...s},d=Math.min(dt,.1),lo=s.easy?40:45,hi=s.easy?78:72;
 n.time+=d;n.air=Math.max(0,Math.min(100,s.air+(held?30:-42)*d));
 n.heat=Math.max(0,Math.min(100,s.heat+(n.air*.24-6)*d));
 const harsh=n.air>82||n.heat>82;
 n.smoke=Math.max(0,Math.min(100,s.smoke+(harsh?(s.easy?12:22):-7)*d));
 if(n.heat>=lo&&n.heat<=hi)n.progress=Math.min(100,s.progress+10*d);
 if(n.smoke>=100){n.phase='failed';n.reason='smoke'}
 else if(n.heat<=1){n.phase='failed';n.reason='cold'}
 else if(n.progress>=100){n.phase='success';n.reason='lit'}
 else if(n.time>=(s.easy?40:30)){n.phase='failed';n.reason='timeout'}
 return n;
}
