// Reproducible native-resolution authoring; the runtime fallback source stays untouched.
const fs=require('fs');
const source=fs.readFileSync('Assets/Scripts/Game/Art/TopDown/TopDownSprites.cs','utf8').replace(/\r\n/g,'\n');
function method(name){const start=source.indexOf('        static '+name);if(start<0)throw Error(name);let a=source.indexOf('{',start),n=1,b=a+1;for(;n;b++){if(source[b]==='{')n++;if(source[b]==='}')n--;}return source.slice(start,b);}
let methods=['float Surface','float FrontArc','Sprite BuildPlayer','Sprite BuildHelm','Sprite BuildFist','Sprite BuildBoot'].map(method).join('\n');
methods=methods.replace('new C(-0.66f, 0.66f, -0.66f, 0.66f, 110f)','new Painter(640,640,.864f,"body_"+Names[kind])')
.replace('new C(-0.42f, 0.3f, -0.3f, 0.3f, 110f)','new Painter(384,384,.864f,"helm_"+Names[kind])')
.replace('new C(-0.085f, 0.075f, -0.075f, 0.075f, 256f)','new Painter(96,80,1f,"fist_"+Names[kind]+(closed?"_closed":"_open"))')
.replace('new C(-0.1f, 0.1f, -0.065f, 0.065f, 256f)','new Painter(112,72,1f,"boot_"+Names[kind])')
.replace('new Color(0.17f, 0.19f, 0.27f)','new Color32(47,44,40,255)')
.replace('new Color(0.23f, 0.25f, 0.34f)','new Color32(151,139,114,255)')
.replace('0.92f + 0.16f * C.Fbm(p, 0.06f, seed)','(0.78f + 0.30f * C.Fbm(p, 0.036f, seed)) * (0.96f + 0.08f * C.Noise(p,0.0035f,seed+2))')
.replaceAll('0.025f, Outline','0.014f, Outline').replaceAll('0.02f, Outline','0.012f, Outline');
const insert=(marker,code)=>{if(!methods.includes(marker))throw Error(marker);methods=methods.replace(marker,code+'\n            '+marker);};
insert('// 웃옷(어깨 앞쪽)','cv.Layer("01-undercoat");');
insert('// 망토:','cv.Layer("02-charcoal-cloak");');
insert('// 어깨:','cv.Layer("03-seams-and-wear");\n            CloakDetail(cv);\n            cv.Layer("04-armor-shoulders");');
insert('if (kind == 0)\n            {\n                // 가죽 끈:', 'cv.Layer("05-leather-straps-and-steel");\n            ArmorDetail(cv,kind,armor);');
insert('// 목 뒤 옷깃','cv.Layer("06-worn-linen-collar");');
insert('// 이마·코끝','cv.Layer("07-head-and-hair");');
methods=methods.replace('var head = new Vector2(0.02f, 0f);','var head = new Vector2(0.02f, 0f);\n            cv.Layer("01-helm-material");');
methods=methods.replace('return cv.ToSprite("정수리 투구','cv.Layer("02-helm-stitches-wear"); HelmDetail(cv,kind);\n            return cv.ToSprite("정수리 투구');
methods=methods.replace('if (closed)\n            {','cv.Layer("02-knuckles-and-thumb");\n            if (closed)\n            {');
methods=methods.replace('if (kind == 0)\n            {\n                var lace','cv.Layer("02-laces-and-toecap");\n            if (kind == 0)\n            {\n                var lace');
const scaffold=fs.readFileSync('AgentScripts/HeroV3Scaffold.cs.txt','utf8');
fs.writeFileSync('AgentScripts/HeroV3Build.cs',scaffold.replace('// GENERATED_METHODS',methods));
console.log('HeroV3Build.cs prepared');
