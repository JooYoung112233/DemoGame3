const fs=require('fs'),path=require('path');const old='아트/캐릭터-연결보정-v052',root='아트/기본방패-가독성-v053';fs.mkdirSync(root,{recursive:true});for(const dir of ['scripts','source'])fs.cpSync(old+'/'+dir,root+'/'+dir,{recursive:true,force:false,errorOnExist:true});
let s=fs.readFileSync(root+'/scripts/build-motion.cjs','utf8');
const polygon='[[-.30,0],[-.26,-.14],[-.15,-.23],[0,-.26],[.15,-.23],[.26,-.14],[.30,0],[.26,.14],[.15,.23],[0,.26],[-.15,.23],[-.26,.14]]';
s=s.replace('const kinds=',`function shieldPlate(w){const poly=${polygon}.map(p=>p.map(v=>v*1.15));const rim=[[103,106,102,255],[136,138,128,255],[169,170,155,255]];const faces=extrude(poly,.085,w,rim,'shield');const center=xform([0,0,.049],w);faces.push(...extrude(poly.map(p=>p.map(v=>v*.82)),.008,[...center,...w.slice(3)],[[64,66,62,255],[86,87,79,255],[116,116,102,255]],'shield-face'));return faces.sort((a,b)=>a.depth-b.depth);}\nconst kinds=`);
s=s.replace('extrude('+polygon+",.035,w,steel,'shield').sort((a,b)=>a.depth-b.depth)",'shieldPlate(w)');
s=s.replace('extrude('+polygon+",.035,[...center,...p.left.slice(3)],steel,'shield').sort((a,b)=>a.depth-b.depth)",'shieldPlate([...center,...p.left.slice(3)])');
fs.writeFileSync(root+'/scripts/build-motion.cjs',s);require('../'+root+'/scripts/build-motion.cjs').buildAll();
const lib=JSON.parse(fs.readFileSync('Assets/Resources/CharacterMotionV045/clips.json'));
let lua=fs.readFileSync(old+'/scripts/render-final.lua','utf8').split('local function drawPose')[0].replace(old,root);
lua+=`\nlocal report=io.open(out..'aseprite-shield-only-output.txt','w')\n`;
for(const c of lib.clips.filter(c=>c.weapon==='wpn_longsword')){
 const from=fs.existsSync(old+'/'+c.id+'.aseprite')?old+'/'+c.id+'.aseprite':'아트/캐릭터-가드준비-수정-v050/'+c.id+'.aseprite';
 if(!fs.existsSync(from))throw Error('Missing approved native '+from);
 lua+=`do local s=app.open('E:/personalProject/Demo3/demo6/${from}');local clip=nil;for _,c in ipairs(data.clips)do if c.id=='${c.id}' then clip=c end end;assert(#s.frames==#clip.frames,'Native frame mismatch ${c.id}');local l=nil;for _,layer in ipairs(s.layers)do if layer.name:find('shield%-edge')then l=layer end end;assert(l);for i,p in ipairs(clip.frames)do local cel=l:cel(i);if cel then s:deleteCel(cel)end;s:newCel(l,i,plate(p.shieldFaces),Point(0,0))end;s:saveAs(out..'${c.id}.aseprite');local im=Image(s.spec);im:drawSprite(s,1);im:saveAs(out..'${c.id}-native-frame-00.png');s:close();report:write('${c.id}\\t'..#clip.frames..'\\n');report:flush()end\n`;
}
lua+='report:close()\n';fs.writeFileSync(root+'/scripts/render-shield-only.lua',lua);console.log('Prepared 21 original layered masters; only basic shield cels change, size +15%, depth .035 -> .085.');
