const fs=require('fs');
const root='아트/캐릭터-연결보정-v052/code-candidate/';
const backup='검증/v052-feedback/backup/';
for(const [name,rel] of [['OgreArtRig','Art/TopDown/'],['Telegraph','Combat/']]){
 const src='Assets/Scripts/Game/'+rel+name+'.cs';
 if(!fs.existsSync(backup+name+'.cs'))fs.copyFileSync(src,backup+name+'.cs');
 let s=fs.readFileSync(src,'utf8');
 if(name==='OgreArtRig')s=s.replace('if(broken||!ogre.Aware)gait.Reset(ogre.Position);','// Visual cadence follows the interpolated render transform, not 50 Hz physics steps.\n            Vector2 renderPosition=ogre.transform.position;\n            if(broken||!ogre.Aware)gait.Reset(renderPosition);').replace('gait.Drive(ogre.Position,','gait.Drive(renderPosition,');
 else s=s.replace('const int AboveDarkOrder = 14500;','// Floor warnings must remain below actor feet, bodies and heads.\n        const int AboveDarkOrder = OutlineOrder;');
 fs.writeFileSync(root+name+'.cs',s);
}
let s=fs.readFileSync(backup+'FirstAttackArtV042.cs','utf8');
s=s.replace('using System.Collections.Generic;','using System.Collections.Generic;\nusing System.Collections;');
s=s.replace('public bool weaponReleased; public Part[] parts;','public bool weaponReleased; public CharacterMotionV045.Sockets sockets; public Part[] parts;');
s=s.replace('readonly EquipmentVisualV049 _equipment;','readonly EquipmentVisualV049 _equipment;\n        readonly HashSet<string> _warming=new HashSet<string>();');
s=s.replace('// Textures are cached when their cel first appears; no duplicate preload of the whole library.','// Warm only visible cels for the equipped weapon, without a synchronous library-wide load.');
s=s.replace('if(_lastWeapon!=clip.weapon){_wave.Clear();_lastWeapon=clip.weapon;}','if(_lastWeapon!=clip.weapon){_wave.Clear();_lastWeapon=clip.weapon;if(_warming.Add(clip.weapon))_wave.StartCoroutine(WarmWeapon(clip.weapon));}');
s=s.replace('var sr=_parts[part.layer];if(!part.sprite)part.sprite=LoadSprite(part.resource);','var sr=_parts[part.layer];\n                if(EquipmentVisualV049.Suppresses(part.name)){sr.enabled=false;_equipment.CountSuppressed();continue;}\n                bool rigid=part.name.Contains("rigid-");\n                if(!part.sprite)part.sprite=rigid?ShapeSprites.Square:LoadSprite(part.resource);');
s=s.replace('if(part.name.Contains("rigid-"))ApplyBladeRelief(part,sr.sprite,f,clip,_flash);','// Calibrated equipment supplies its own face and physical edge.');
s=s.replace('if(gaitActive&&!foot){turn=gait.BodyTurn;at=TopDownCanvas.Rotate(at,turn)+gait.BodyOffset;}','Vector2 extraOffset=attack&&!extended?accentOffset:Vector2.zero;\n                if(gaitActive&&!foot){GaitTransform(part.name,f,clip.weapon,gait,out turn,out extraOffset);at=TopDownCanvas.Rotate(at,turn)+extraOffset;}');
s=s.replace('if(gaitActive&&foot&&gait.Amount>.001f&&art&&art.toe){','if(gaitActive&&foot&&art&&art.toe){');
s=s.replace('Vector2 extraOffset=attack&&!extended?accentOffset:gaitActive?gait.BodyOffset:Vector2.zero;','');
s=s.replace('clip.weapon,turn,extraOffset);','clip.weapon,turn,extraOffset,clip.id,t,clip.duration,clip.hits,attack||extended);');
s=s.replace('foreach(var anchor in new[]{_baseR,_tipR,_baseL,_tipL})\n                    anchor.localPosition=TopDownCanvas.Rotate(anchor.localPosition,gait.BodyTurn)+gait.BodyOffset;', 'GaitTransform("rigid-blade-R",f,clip.weapon,gait,out var rt,out var ro);\n                GaitTransform("rigid-blade-L",f,clip.weapon,gait,out var lt,out var lo);\n                foreach(var anchor in new[]{_baseR,_tipR})anchor.localPosition=TopDownCanvas.Rotate(anchor.localPosition,rt)+ro;\n                foreach(var anchor in new[]{_baseL,_tipL})anchor.localPosition=TopDownCanvas.Rotate(anchor.localPosition,lt)+lo;');
const methods=`
        IEnumerator WarmWeapon(string weapon)
        {
            var resources=new HashSet<string>();
            foreach(var c in _motionClips.Values)if(c.weapon==weapon)
                foreach(var f in c.frames)foreach(var p in f.parts)
                    if(!p.name.Contains("rigid-"))resources.Add(p.resource);
            foreach(var resource in resources){
                if(Sprites.ContainsKey(resource))continue;
                var request=Resources.LoadAsync<Texture2D>(resource);yield return request;
                if(request.asset is Texture2D texture&&!Sprites.ContainsKey(resource)){
                    var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),160,0,SpriteMeshType.FullRect);
                    sprite.name=resource;Sprites[resource]=sprite;
                }
            }
        }
        static void GaitTransform(string part,Frame f,string weapon,CuteWalkV22 gait,out float turn,out Vector2 offset)
        {
            turn=gait.BodyTurn;offset=gait.BodyOffset;
            float local=0;Vector2 pivot=Vector2.zero;
            if(part.EndsWith("approved-head")){
                local=gait.HeadTurn;pivot=f.headOrigin!=null?new Vector2(f.headOrigin[0],f.headOrigin[1]):Vector2.zero;
                offset+=new Vector2(0,gait.HeadShift);
            } else if(weapon!="wpn_greatsword"&&(part.EndsWith("-L")||part.EndsWith("-R")||part.Contains("rigid-sword")||part.Contains("shield-edge"))){
                bool left=part.EndsWith("-L")||part.Contains("shield-edge");
                var socket=left?f.sockets?.shoulderL:f.sockets?.shoulderR;
                if(socket!=null){pivot=new Vector2(socket[0],socket[1]);local=(left?-1:1)*gait.ShoulderTurn*(weapon=="wpn_twinblades"?.65f:.30f);}
            }
            offset+=TopDownCanvas.Rotate(pivot,turn)-TopDownCanvas.Rotate(pivot,turn+local);turn+=local;
        }
`;
s=s.replace('        void DriveGlow(',methods+'\n        void DriveGlow(');
fs.writeFileSync(root+'FirstAttackArtV042.cs',s);
s=fs.readFileSync(backup+'SlashWaveV044.cs','utf8');
s=s.replaceAll('AddCrescent(center,pulse.angle,tail,head,r,width*(great?1.05f:1f),opacity);','AddCrescent(center,pulse.angle,tail,head,r,width*(great?1.05f:1f),opacity);\n                    if(age<.085f)AddCrescent(center,pulse.angle,Mathf.Lerp(tail,head,.30f),head,r+.075f,great?.035f:twin?.016f:.022f,opacity*.52f*(1-age/.085f));');
fs.writeFileSync(root+'SlashWaveV044.cs',s);
console.log('Staged visual-only gait, floor order, asynchronous cel warmup and brief speed-line changes.');
