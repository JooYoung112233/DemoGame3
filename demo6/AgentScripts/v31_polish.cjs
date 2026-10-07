const fs=require('fs');const ev='검증/돌갑충-예고-v031';
for(const name of ['Telegraph.cs','BeetlePatternVfx.cs']){
 const path=name==='Telegraph.cs'?'Assets/Scripts/Game/Combat/'+name:'Assets/Scripts/Game/Art/TopDown/'+name;
 fs.copyFileSync(path,ev+'/candidate/'+name+'.before-polish');let s=fs.readFileSync(path,'utf8');
 if(name==='Telegraph.cs'){
 s=s.replace('const float EdgeThick = 0.14f;','const float EdgeThick = 0.055f;');
 s=s.replace('new Color(.98f, .12f, .14f, 1f)','new Color(.70f, .04f, .05f, 1f)');
 s=s.replace('new Color(.82f, .10f, .14f, 1f)','new Color(.48f, .018f, .026f, 1f)');
 s=s.replace('new Color(1f, .18f, .22f, .90f)','new Color(.62f, .028f, .035f, .90f)');
 s=s.replace('new Color(1f, .39f, .32f, .48f + .24f * imminent)','new Color(.92f, .16f, .12f, .48f + .24f * imminent)');
 s=s.replace('new Color(1f, .30f, .27f, .75f + .15f * imminent)','new Color(.82f, .12f, .10f, .62f + .18f * imminent)');
 }else{
 s=s.replace('Emit(point+outward*.08f,.95f,.34f,false,true,outward*.25f);\n            Vector2 tangent=new Vector2(-outward.y,outward.x);','Vector2 tangent=new Vector2(-outward.y,outward.x);\n            Emit(point+outward*.10f+tangent*.37f,.85f,.34f,false,true,outward*.25f+tangent*.5f);\n            Emit(point+outward*.10f-tangent*.37f,.85f,.34f,false,true,outward*.25f-tangent*.5f);');
 s=s.replace('tangent*((i-1.5f)*.72f)','tangent*((i-1.5f)*1.15f)');
 }
 fs.writeFileSync(ev+'/candidate/'+name,s);
}
console.log('Final visual polish prepared; no combat changes');
