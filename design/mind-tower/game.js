/* Pure turn model shared by the browser and Node verification. */
(function(root){
const levels=[
 {title:'손을 놓지 않고',line:'소이는 한 걸음 뒤에서 아빠의 발자국을 따라왔다.',walls:[],hazards:[[17],[23],[16]],exit:5},
 {title:'조금 돌아가도',line:'빨리 가는 것보다, 함께 도착하는 게 중요했다.',walls:[9,10,15,16],hazards:[[8,14],[20,21],[26,27]],exit:5},
 {title:'잠깐, 아빠 뒤에',line:'괜찮다는 말 대신, 수혁은 아이 앞에 섰다.',walls:[8,14,26,27],hazards:[[10,16,22],[21,22,23],[4,10,16]],exit:5},
 {title:'말하지 못한 마음',line:'먼저 도착한 뒤에도 그는 자꾸 뒤를 돌아봤다.',walls:[9,15,21,25],hazards:[[11,17,23],[20,26,32],[3,4,5]],exit:5},
 {title:'함께 켜는 불빛',line:'“아빠, 여기야.” 작은 목소리가 돌아갈 길을 비췄다.',walls:[8,9,21,22,26],hazards:[[10,16,17],[19,20,25],[4,5,11],[28,29,35]],exit:5}
];
const adjacent=(a,b)=>Math.abs(a%6-b%6)+Math.abs(Math.floor(a/6)-Math.floor(b/6))===1;
function start(i=0){return {level:i,dad:30,soi:31,turn:0,health:5,light:3,arrived:false,done:false,failed:false,message:levels[i].line};}
function danger(s){const h=levels[s.level].hazards;return h[s.turn%h.length];}
function act(s,type,target){
 if(s.done||s.failed)return s;
 const n={...s},l=levels[s.level]; let shield=!!s.ward,cleared=[];n.ward=false;
 if(type==='move'){
  if(!Number.isInteger(target)||target<0||target>=36||!adjacent(s.dad,target)||l.walls.includes(target))return s;
  n.dad=target;if(!s.arrived)n.soi=s.dad;
  n.message='한 걸음. 소이는 아빠가 서 있던 자리로 따라옵니다.';
 }else if(type==='guard'){shield=true;n.light=Math.min(3,n.light+1);n.message='곁을 지켰습니다. 이번 위험을 막고 빛을 1 회복합니다.';
 }else if(type==='push'){
  if(s.light<1)return s;n.light--;n.ward=true;shield=true;n.message='그림자를 밀어냈습니다. 이번 턴과 다음 행동을 보호합니다.';
 }else return s;
 if(n.soi===l.exit)n.arrived=true;
 const hit=!shield&&danger(s).some(x=>!cleared.includes(x)&&(x===n.dad||(!n.arrived&&x===n.soi)));
 if(hit){n.health--;n.message='그림자가 마음을 스쳤습니다. 마음 −1. 되돌려 다른 길을 골라도 괜찮아요.';}
 n.turn++;n.done=n.arrived&&n.dad===l.exit;n.failed=n.health<=0&&!n.done;
 if(n.done)n.message='두 사람이 불빛에 도착했습니다.';
 if(n.failed)n.message='잠시 쉬어가도 괜찮아요. 되돌리거나 이 층을 다시 시작하세요.';
 return n;
}
const api={levels,start,danger,act,adjacent};if(typeof module!=='undefined')module.exports=api;else root.Tower=api;
})(typeof window!=='undefined'?window:globalThis);
