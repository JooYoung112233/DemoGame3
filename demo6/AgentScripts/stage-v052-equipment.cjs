const fs=require('fs'),root='아트/캐릭터-연결보정-v052/code-candidate/';
let s=fs.readFileSync('검증/v052-feedback/backup/EquipmentVisualV049.cs','utf8');
s=s.replace('readonly SpriteRenderer[,] _edges=new SpriteRenderer[2,3];','readonly WeaponEdgeV052[] _edges=new WeaponEdgeV052[2];');
const start=s.indexOf('            for(int h=0;h<2;h++)for(int i=0;i<3;i++)');
const end=s.indexOf('\n        Grade EquippedGrade',start);
s=s.slice(0,start)+`            for(int h=0;h<2;h++){var go=new GameObject("EquipmentV052_ExtrudedEdge_"+h);go.layer=player.gameObject.layer;go.transform.SetParent(parent,false);_edges[h]=go.AddComponent<WeaponEdgeV052>();}
        }
        public static bool Suppresses(string part)=>part.Contains("rigid-")&&!part.EndsWith("blade-L")&&!part.EndsWith("blade-R")&&!part.EndsWith("rigid-sword")&&!part.EndsWith("rigid-weapons");
        public void CountSuppressed(){Suppressed++;}
        public void Begin(){Suppressed=0;ExtraVisible=0;foreach(var edge in _edges)if(edge)edge.Hide();}
`+s.slice(end);
s=s.replace('string weaponId,float extraTurn,Vector2 extraOffset)','string weaponId,float extraTurn,Vector2 extraOffset,string clipId="",float clock=0,float duration=0,float[] hits=null,bool action=false)');
s=s.replace('var rotation=Quaternion.Euler(0,0,pose[3]+extraTurn)*Quaternion.Euler(0,-pose[4],0)*Quaternion.Euler(pose.Length>5?pose[5]:0,0,0);','float roll=FaceRoll(pose.Length>5?pose[5]:0,weaponId,clipId,clock,duration,hits,action,left);\n            var rotation=Quaternion.Euler(0,0,pose[3]+extraTurn)*Quaternion.Euler(0,-pose[4],0)*Quaternion.Euler(roll,0,0);');
const a=s.indexOf('            var normal=rotation*Vector3.forward;');const b=s.indexOf('\n        public void ApplyArmor',a);
s=s.slice(0,a)+`            _edges[left?1:0].Draw(sr,w.key,at,rotation,depth,left);ExtraVisible++;
        }
        static float FaceRoll(float rest,string weapon,string clip,float t,float duration,float[] hits,bool action,bool left)
        {
            // Keep approved idle/guard stances. Roll only attacks with existing hit clocks.
            if(!action||hits==null||hits.Length==0||clip.Contains("guard")||clip.Contains("return"))return rest;
            float hit=hits[0];bool vertical=clip=="great-combo-3"||clip=="great-execution";
            if(vertical){
                float raise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,Mathf.Max(.02f,hit*.55f),t));
                float recover=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(hit+.08f,Mathf.Max(hit+.09f,duration),t));
                return Mathf.Lerp(rest,86f,raise*recover);
            }
            float windup=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/Mathf.Max(.02f,hit)));
            if(t>=hit)windup=.5f*Mathf.Sin(Mathf.PI*Mathf.Clamp01((t-hit)/Mathf.Max(.02f,duration-hit)));
            float edge=weapon=="wpn_greatsword"?72f:weapon=="wpn_twinblades"?66f:58f;
            return Mathf.Lerp(rest,left?-edge:edge,windup);
        }
`+s.slice(b);
// twin-first-right-return is an attack despite its historical clip name.
s=s.replace('||clip.Contains("return")','||(clip.Contains("return")&&!clip.Contains("first-"))');
fs.writeFileSync(root+'EquipmentVisualV049.cs',s);
let tele=fs.readFileSync(root+'Telegraph.cs','utf8').replace('const int AboveDarkOrder = OutlineOrder;','const int AboveDarkOrder = -60;');fs.writeFileSync(root+'Telegraph.cs',tele);
console.log('Staged silhouette extrusion and weapon face roll.');
