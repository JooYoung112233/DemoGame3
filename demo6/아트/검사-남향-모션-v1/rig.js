/* Painted 2D cutout rig. All artwork comes from the preserved ImageGen PNGs.
   Coordinates are shared by every clip; movement is in-place, south-facing. */
(function(global){
'use strict';
const TAU=Math.PI*2, rad=d=>d*Math.PI/180, lerp=(a,b,t)=>a+(b-a)*t;
const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t)};
const clips={idle:{frames:8,duration:1.6,loop:true},walk:{frames:12,duration:1,loop:true},run:{frames:12,duration:.6,loop:true},attack:{frames:12,duration:.8,loop:false}};
function neutral(){return {x:0,y:0,bodyAngle:0,bodyScale:1,headAngle:0,headY:0,
  leftLeg:0,rightLeg:0,leftBend:0,rightBend:0,leftStretch:1,rightStretch:1,
  leftHandX:210,leftHandY:820,rightHandX:790,rightHandY:820,swordAngle:-18,swordScale:1};}
function mix(a,b,t){const p={};for(const k of Object.keys(a))p[k]=lerp(a[k],b[k],t);return p;}
// Attack anticipation, downward contact, follow-through, then exact neutral recovery.
const attackKeys=[
 [0,{}],[.10,{y:8,bodyAngle:-2,rightHandX:900,rightHandY:670,swordAngle:-65,leftHandX:220,leftHandY:800}],
 [.23,{y:14,bodyAngle:-6,headAngle:3,rightHandX:940,rightHandY:490,swordAngle:-156,leftHandX:240,leftHandY:780,leftStretch:.99,rightStretch:.96}],
 [.32,{y:8,bodyAngle:-4,rightHandX:915,rightHandY:430,swordAngle:-177,leftHandX:230,leftHandY:760}],
 [.43,{y:28,bodyAngle:7,headY:10,rightHandX:650,rightHandY:810,swordAngle:-6,leftHandX:180,leftHandY:790,leftStretch:1.05,rightStretch:.98}],
 [.55,{y:22,bodyAngle:9,headY:8,rightHandX:570,rightHandY:825,swordAngle:20,leftHandX:160,leftHandY:800,leftStretch:1.04,rightStretch:.98}],
 [.72,{y:10,bodyAngle:4,rightHandX:700,rightHandY:820,swordAngle:5,leftHandX:185,leftHandY:810}],
 [1,{}]
];
function pose(clip,phase){
 const p=neutral();
 if(clip==='attack'){
  phase=Math.max(0,Math.min(1,phase));let i=0;while(i<attackKeys.length-2&&phase>attackKeys[i+1][0])i++;
  const [t0,k0]=attackKeys[i],[t1,k1]=attackKeys[i+1];return mix({...p,...k0},{...p,...k1},smooth((phase-t0)/(t1-t0)));
 }
 const a=phase*TAU,s=Math.sin(a),c=Math.cos(a);
 if(clip==='idle'){p.y=-5*Math.sin(a);p.headY=2*Math.sin(a-.3);p.leftHandY+=3*s;p.rightHandY+=3*s;p.swordAngle+=.7*s;return p;}
 const run=clip==='run',stride=run?.25:.15;
 p.x=(run?8:4)*s;p.y=-(run?16:7)*(1-Math.cos(2*a));p.bodyScale=run?.965:1;p.headY=run?14:0;
 p.bodyAngle=(run?2:1)*s;p.headAngle=-p.bodyAngle*.6;
 p.leftStretch=1+stride*s;p.rightStretch=1-stride*s;
 p.leftLeg=(run?4:2)*s;p.rightLeg=(run?4:2)*s;
 p.leftBend=(run?9:4)*Math.max(0,-c);p.rightBend=-(run?9:4)*Math.max(0,c);
 p.leftHandY+=(run?100:55)*s-(run?42:0);p.rightHandY-=(run?100:55)*s+(run?42:0);
 p.leftHandX+=(run?22:10)*s;p.rightHandX+=(run?22:10)*s;
 p.swordAngle+=(run?6:3)*s;return p;
}
function rotate(x,y,a){const c=Math.cos(a),s=Math.sin(a);return [c*x-s*y,s*x+c*y];}
function samplePose(clip,phase){const c=clips[clip],steps=c.loop?c.frames:c.frames-1;return pose(clip,Math.floor(Math.max(0,Math.min(c.loop?.999999:1,phase))*steps+1e-8)/steps);}
function triangle(ctx,img,src,dst){
 const [s0,s1,s2]=src,[d0,d1,d2]=dst;
 const a=s1[0]-s0[0],b=s1[1]-s0[1],c=s2[0]-s0[0],d=s2[1]-s0[1],det=a*d-b*c;
 if(Math.abs(det)<1e-8)return;
 const ux=d1[0]-d0[0],uy=d1[1]-d0[1],vx=d2[0]-d0[0],vy=d2[1]-d0[1];
 const A=(ux*d-vx*b)/det,B=(uy*d-vy*b)/det,C=(vx*a-ux*c)/det,D=(vy*a-uy*c)/det;
 ctx.save();ctx.beginPath();
 // Tiny expanded clip prevents antialiased cracks between adjacent textured triangles.
 const mx=(d0[0]+d1[0]+d2[0])/3,my=(d0[1]+d1[1]+d2[1])/3;
 const ds=dst.map(([x,y])=>{const dx=x-mx,dy=y-my,l=Math.hypot(dx,dy);return [x+dx/l*.35,y+dy/l*.35]});
 ctx.moveTo(...ds[0]);ctx.lineTo(...ds[1]);ctx.lineTo(...ds[2]);ctx.closePath();ctx.clip();
 ctx.transform(A,B,C,D,d0[0]-A*s0[0]-C*s0[1],d0[1]-B*s0[0]-D*s0[1]);ctx.drawImage(img,0,0);ctx.restore();
}
class Rig{
 constructor(images,regions){this.images=images;this.regions=regions;}
 part(ctx,id,x,y,w,h,angle=0,pivotX=.5,pivotY=.5){const r=this.regions[id];ctx.save();ctx.translate(x,y);ctx.rotate(rad(angle));ctx.drawImage(this.images[r.source],...r.rect,-w*pivotX,-h*pivotY,w,h);ctx.restore();}
 limb(ctx,id,x,y,w,h,angle,bend,stretch=1,joint=.52){
  const r=this.regions[id],img=this.images[r.source],a=rad(angle),b=rad(bend),j=h*joint;
  const point=(u,v)=>{
   const px=(u-.5)*w,py=v*h;
   const upper=rotate(px,py,a),elbow=rotate(0,j,a),lower=rotate(px,py-j,a+b);
   const blend=smooth((v-joint+.07)/.14), xx=lerp(upper[0],elbow[0]+lower[0],blend),yy=lerp(upper[1],elbow[1]+lower[1],blend);
   // Keep the boot rigid: projected stride changes the thigh, not boot length.
   const shift=(stretch-1)*h*smooth(v/.50);
   return [x+xx,y+yy+shift];
  };
  const rows=20,cols=4,[sx,sy,sw,sh]=r.rect;
  for(let i=0;i<rows;i++)for(let k=0;k<cols;k++){
   const uv=[[k/cols,i/rows],[(k+1)/cols,i/rows],[(k+1)/cols,(i+1)/rows],[k/cols,(i+1)/rows]];
   const src=uv.map(([u,v])=>[sx+u*sw,sy+v*sh]),dst=uv.map(([u,v])=>point(u,v));
   triangle(ctx,img,[src[0],src[1],src[2]],[dst[0],dst[1],dst[2]]);triangle(ctx,img,[src[0],src[2],src[3]],[dst[0],dst[2],dst[3]]);
  }
 }
 arm(ctx,id,shoulder,hand,side){
  const dx=hand[0]-shoulder[0],dy=hand[1]-shoulder[1],reach=Math.hypot(dx,dy),stretch=Math.max(1,reach/369),l1=175*stretch,l2=195*stretch,dist=Math.min(l1+l2-1,reach);
  const base=Math.atan2(-dx,dy),off=Math.acos(Math.max(-1,Math.min(1,(l1*l1+dist*dist-l2*l2)/(2*l1*dist))));
  const a=base+side*off,el=rotate(0,l1,a),b=Math.atan2(-(dx-el[0]),dy-el[1])-a;
  const fullLength=l1+l2+28;
  this.limb(ctx,id,...shoulder,180,fullLength,a*180/Math.PI,b*180/Math.PI,1,l1/fullLength);
 }
 draw(ctx,p,{size=1024,background=null,shadow=false}={}){
  ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,size,size);if(background){ctx.fillStyle=background;ctx.fillRect(0,0,size,size)};
  ctx.save();ctx.scale(size/1536,size/1536);ctx.translate(328,120);ctx.scale(.85,.85);
  if(shadow){ctx.save();ctx.fillStyle='#00000030';ctx.beginPath();ctx.ellipse(512,1300,260,45,0,0,TAU);ctx.fill();ctx.restore()}
  ctx.translate(p.x,p.y);
  this.limb(ctx,'leftLeg',380,855,215,440,p.leftLeg,p.leftBend,p.leftStretch-p.y/440,.40);
  this.limb(ctx,'rightLeg',645,855,215,440,p.rightLeg,p.rightBend,p.rightStretch-p.y/440,.40);
  ctx.save();ctx.translate(512,770);ctx.rotate(rad(p.bodyAngle));ctx.scale(1,p.bodyScale);ctx.translate(-512,-770);
  const left=[p.leftHandX,p.leftHandY],right=[p.rightHandX,p.rightHandY];
  this.arm(ctx,'leftArm',[287,465],left,1);
  this.part(ctx,'torso',512,655,505,606);
  // Sword attaches to wrist before painting the closed glove over its grip.
  this.part(ctx,'sword',right[0],right[1]-8,155,590*p.swordScale,p.swordAngle,.5,.16);
  this.arm(ctx,'rightArm',[737,465],right,-1);
  this.part(ctx,'head',508,310+p.headY,440,440,p.headAngle);
  ctx.restore();ctx.restore();
 }
}
// Pose blending, not image cross-fading: texture identity and foot phase survive transitions.
class Controller{
 constructor(){this.state='idle';this.phase=0;this.current=pose('idle',0);this.from=null;this.elapsed=0;this.blendDuration=.18;}
 set(state){if(!clips[state]||state===this.state)return;this.from={...this.current};const locomotion=['walk','run'];if(!(locomotion.includes(state)&&locomotion.includes(this.state)))this.phase=0;this.state=state;this.elapsed=0;this.blendDuration=state==='attack'?.12:state==='idle'?.22:.18;}
 step(dt){this.phase+=dt/clips[this.state].duration;if(this.state==='attack'&&this.phase>=1){this.current=pose('attack',1);this.set('idle');}else if(clips[this.state].loop)this.phase%=1;
  const target=samplePose(this.state,this.phase);this.elapsed+=dt;this.current=this.from?mix(this.from,target,smooth(this.elapsed/this.blendDuration)):target;if(this.elapsed>=this.blendDuration)this.from=null;return this.current;}
}
global.SouthRig={Rig,Controller,clips,pose,samplePose,mix,neutral};
})(typeof window!=='undefined'?window:globalThis);

