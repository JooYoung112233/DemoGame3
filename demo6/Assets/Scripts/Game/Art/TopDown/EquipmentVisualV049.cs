using System;
using System.Collections.Generic;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    // Pure appearance mapping over existing gear IDs. Source pixels, combat and save data stay unchanged.
    public sealed class EquipmentVisualV049
    {
        [Serializable] public sealed class Weapon {public string key,baseId,texture,icon;public float ppu,pivotX,pivotY,tip,tail;[NonSerialized]public Sprite sprite;}
        [Serializable] public sealed class Armor {public string baseId,part,texture;public float pivotX,pivotY,scaleX,scaleY;[NonSerialized]public Sprite sprite;}
        [Serializable] public sealed class Catalog {public Weapon[] weapons;public Armor[] armor;}
        static Catalog _catalog;
        static readonly Dictionary<string,Texture2D> Icons=new Dictionary<string,Texture2D>();
        readonly PlayerController _player;
        readonly WeaponEdgeV052[] _edges=new WeaponEdgeV052[2];
        readonly MaterialPropertyBlock _block=new MaterialPropertyBlock();
        public int Suppressed {get;private set;}
        public int ExtraVisible {get;private set;}
        public string DesignKey {get;private set;}
        static Catalog Data {get {if(_catalog==null){var text=Resources.Load<TextAsset>("EquipmentV049/calibration");if(text)_catalog=JsonUtility.FromJson<Catalog>(text.text);}return _catalog;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){_catalog=null;Icons.Clear();}
        static Weapon Choose(string id,Grade grade)
        {
            if(Data?.weapons==null)return null;int tier=Mathf.Clamp((int)grade/2,0,2),found=0;
            foreach(var w in Data.weapons)if(w.baseId==id){if(found++==tier)return w;}return null;
        }
        public static Texture2D Icon(string id,Grade grade=Grade.Common)
        {
            var w=Choose(id,grade);if(w==null)return null;
            if(!Icons.TryGetValue(w.icon,out var tex))Icons[w.icon]=tex=Resources.Load<Texture2D>(w.icon);return tex;
        }
        public static bool DrawIcon(Rect area,string id,Grade grade,Color? tint=null)
        {
            var texture=Icon(id,grade);if(!texture)return false;
            var before=GUI.color;GUI.color=tint??new Color(1,1,1,before.a);
            GUI.DrawTexture(area,texture,ScaleMode.ScaleToFit,true);GUI.color=before;return true;
        }
        public EquipmentVisualV049(PlayerController player,Transform parent)
        {
            _player=player;
            for(int h=0;h<2;h++){var go=new GameObject("EquipmentV052_ExtrudedEdge_"+h);go.layer=player.gameObject.layer;go.transform.SetParent(parent,false);_edges[h]=go.AddComponent<WeaponEdgeV052>();}
        }
        public static bool Suppresses(string part)=>part.Contains("rigid-")&&!part.EndsWith("blade-L")&&!part.EndsWith("blade-R")&&!part.EndsWith("rigid-sword")&&!part.EndsWith("rigid-weapons");
        public void CountSuppressed(){Suppressed++;}
        public void Begin(){Suppressed=0;ExtraVisible=0;foreach(var edge in _edges)if(edge)edge.Hide();}

        Grade EquippedGrade(string id){var inv=Inventory.Instance;var gear=inv?inv.Equipment[GearSlot.Weapon]:null;return gear!=null&&gear.BaseId==id?gear.Grade:Grade.Common;}
        public void ApplyWeapon(SpriteRenderer sr,string part,float[] pose,string weaponId,float extraTurn,Vector2 extraOffset,string clipId="",float clock=0,float duration=0,float[] hits=null,bool action=false)
        {
            if(!part.Contains("rigid-"))return;
            var w=Choose(weaponId,EquippedGrade(weaponId));if(w==null)return;
            bool left=part.EndsWith("blade-L");
            bool primary=left||part.EndsWith("blade-R")||part.EndsWith("rigid-sword")||part.EndsWith("rigid-weapons");
            if(!primary){sr.enabled=false;Suppressed++;return;}
            if(!w.sprite){var tex=Resources.Load<Texture2D>(w.texture);if(!tex)return;w.sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(w.pivotX,w.pivotY),w.ppu,0,SpriteMeshType.FullRect);}
            DesignKey=w.key;sr.sprite=w.sprite;sr.flipY=left;
            var at=TopDownCanvas.Rotate(new Vector2(pose[0],pose[1]),extraTurn)+extraOffset;
            float roll=FaceRoll(pose.Length>5?pose[5]:0,weaponId,clipId,clock,duration,hits,action,left);
            var rotation=Quaternion.Euler(0,0,pose[3]+extraTurn)*Quaternion.Euler(0,-pose[4],0)*Quaternion.Euler(roll,0,0);
            sr.transform.localPosition=at;sr.transform.localRotation=rotation;sr.transform.localScale=Vector3.one;
            sr.GetPropertyBlock(_block);_block.SetFloat("_BladeRelief",0);sr.SetPropertyBlock(_block);
            float depth=weaponId=="wpn_greatsword"?.07f:weaponId=="wpn_twinblades"?.025f:.035f;
            _edges[left?1:0].Draw(sr,w.key,at,rotation,depth,left);ExtraVisible++;
        }
        static float FaceRoll(float rest,string weapon,string clip,float t,float duration,float[] hits,bool action,bool left)
        {
            // Keep approved idle/guard stances. Roll only attacks with existing hit clocks.
            if(!action||hits==null||hits.Length==0||clip.Contains("guard")||(clip.Contains("return")&&!clip.Contains("first-")))return rest;
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

        public void ApplyArmor(SpriteRenderer sr,string name,FirstAttackArtV042.Frame f,float extraTurn,Vector2 extraOffset)
        {
            string role=name.EndsWith("approved-torso")?"torso":name.EndsWith("approved-head")?"head":name.EndsWith("approved-shoulder-L")?"leftShoulder":name.EndsWith("approved-shoulder-R")?"shoulder":null;
            if(role==null||Data?.armor==null)return;string id=role=="head"?_player.Look.HelmId:_player.Look.ArmorId;Armor art=null;
            foreach(var a in Data.armor)if(a.baseId==id&&a.part==role){art=a;break;}if(art==null)return;
            if(!art.sprite){var tex=Resources.Load<Texture2D>(art.texture);if(!tex)return;art.sprite=Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(art.pivotX,art.pivotY),160,0,SpriteMeshType.FullRect);}
            Vector2 origin;float turn;
            if(role=="torso"){origin=XY(f.bodyOrigin,new Vector2(f.lean,0));turn=f.twist;}
            else if(role=="head"){origin=XY(f.headOrigin,TopDownCanvas.Rotate(new Vector2(-.16875f,0),f.twist)+new Vector2(f.lean,0));turn=f.headTurn;}
            else {bool left=role=="leftShoulder";origin=XY(left?f.capL:f.capR,TopDownCanvas.Rotate(left?new Vector2(-.01875f,.36875f):new Vector2(.0125f,-.34375f),f.twist)+new Vector2(f.lean,0));turn=left?f.shoulderLeftAngle:f.shoulderRightAngle;}
            sr.sprite=art.sprite;sr.flipY=false;sr.transform.localPosition=TopDownCanvas.Rotate(origin,extraTurn)+extraOffset;
            sr.transform.localRotation=Quaternion.Euler(0,0,turn+extraTurn);
            var poseScale=role=="torso"?f.bodyScale:role=="head"?f.headScale:f.shoulderScale;
            sr.transform.localScale=new Vector3(art.scaleX*(poseScale!=null&&poseScale.Length>=2?poseScale[0]:1f),art.scaleY*(poseScale!=null&&poseScale.Length>=2?poseScale[1]:1f),1);
        }
        static Vector2 XY(float[] a,Vector2 fallback)=>a!=null&&a.Length>=2?new Vector2(a[0],a[1]):fallback;
    }
}
