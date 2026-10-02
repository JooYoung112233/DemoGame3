/* Drawn-frame playback. No limb scaling, joint deformation, or image crossfades. */
(function(global){
'use strict';
const moving=s=>s==='walk'||s==='run';
class Player{
 constructor(clips){this.clips=clips;this.state='idle';this.frame=0;this.time=0;this.pending=null;this.afterStart='walk';}
 enter(state,frame=0){this.state=state;this.frame=frame;this.time=0;}
 request(state){
  if(!['idle','walk','run','attack'].includes(state))return;
  if(this.state==='attack'){this.pending=state==='attack'?'idle':state;return;}
  if(this.state==='start'||this.state==='stop'){this.pending=state;return;}
  if(state===this.state){this.pending=null;return;}
  if(this.state==='idle'){
   if(moving(state)){this.afterStart=state;this.enter('start');}
   else if(state==='attack')this.enter('attack');
   return;
  }
  this.pending=state;
 }
 advance(){
  if(this.state==='start'){
   if(++this.frame>=this.clips.start.frames.length){const target=this.pending||this.afterStart;this.pending=null;this.enter(target==='run'?'walk':target);if(target==='run')this.pending='run';}return;
  }
  if(this.state==='stop'){
   if(++this.frame>=this.clips.stop.frames.length){const target=this.pending||'idle';this.pending=null;this.enter('idle');if(target!=='idle')this.request(target);}return;
  }
  const next=(this.frame+1)%this.clips[this.state].frames.length;
  if(moving(this.state)&&this.pending){
   if(moving(this.pending)&&(next===2||next===6)){const target=this.pending;this.pending=null;this.enter(target,next);return;}
   // Finish the stride that places the left foot forward before the shared settle poses.
   if(!moving(this.pending)&&next===0){const target=this.pending;this.pending=target==='idle'?null:target;this.enter('stop');return;}
  }
  if(this.state==='attack'&&next===0){const target=this.pending||'idle';this.pending=null;this.enter('idle');if(target!=='idle')this.request(target);return;}
  this.frame=next;
 }
 step(dt){this.time+=dt*1000;let safety=0;while(this.time>=this.clips[this.state].durations[this.frame]&&safety++<30){const left=this.time-this.clips[this.state].durations[this.frame];this.advance();this.time=left;}return this.current();}
 current(){const c=this.clips[this.state];return {state:this.state,frame:this.frame,id:c.frames[this.frame],bob:c.bob[this.frame],pending:this.pending};}
}
function draw(ctx,images,anchors,pose,{width=640,height=640,background='#293139',shadow=false}={}){
 ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,width,height);if(background){ctx.fillStyle=background;ctx.fillRect(0,0,width,height);}
 const unit=Math.min(width,height)/1536,im=images[pose.id],a=anchors[pose.id];if(!im||!a)return;
 ctx.save();ctx.scale(unit,unit);ctx.translate((width/unit-1536)/2,(height/unit-1536)/2);
 if(shadow){ctx.fillStyle='#00000028';ctx.beginPath();ctx.ellipse(765,1280,220,36,0,0,Math.PI*2);ctx.fill();}
 // A single uniform registration transform per full drawing. Source PNG remains untouched.
 const s=.80*a.scale;ctx.drawImage(im,768-a.x*s,200+(pose.bob||0)*.8-a.y*s,im.width*s,im.height*s);ctx.restore();
}
global.CelMotion={Player,draw};
})(typeof window!=='undefined'?window:globalThis);
