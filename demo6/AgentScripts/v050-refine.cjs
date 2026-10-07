const fs=require('fs');
const root='아트/캐릭터-가드준비-수정-v050',backup='검증/v049-final/backup';
let p=root+'/scripts/build-motion.cjs',s=fs.readFileSync(p,'utf8');fs.copyFileSync(p,backup+'/build-motion-priority.cjs');
s=s.replace('rests[0].right=[.16,-.58,1.40,-10,15,0];','rests[0].right=[.16,-.64,1.40,-10,15,0];').replace('R(.16,-.58,1.40,-8,55)','R(.16,-.68,1.40,-8,55)');fs.writeFileSync(p,s);
for(const p of ['아트/캐릭터-준비연결-v049/scripts/MotionRuntimeV045.cs','AgentScripts/V049FinalReview.cs']){let s=fs.readFileSync(p,'utf8');s=s.replace('P.SetWeapon(c.weapon==','Set("<Weapon>k__BackingField",c.weapon==').replace('p.SetWeapon(WeaponPresets.Longsword);','Set(p,"<Weapon>k__BackingField",WeaponPresets.Longsword);');fs.writeFileSync(p,s);}
console.log('Tucked grip cleared below shoulder; pose tests no longer fire equipment-change callbacks.');
