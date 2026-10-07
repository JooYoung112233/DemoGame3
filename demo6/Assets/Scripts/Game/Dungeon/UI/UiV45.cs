using System.Collections.Generic;
using UnityEngine;
namespace Demo6.Game
{
    public static partial class UiV45
    {
        static readonly Dictionary<string,Texture2D> Images=new Dictionary<string,Texture2D>();
        public static Rect OrbRect=>new Rect(DungeonUi.Width*.5f-270,DungeonUi.Height-130,104,104);
        public static Rect ActionRect(int index)=>new Rect(DungeonUi.Width*.5f-156+(index+1)*47,DungeonUi.Height-84,44,44);
        public static Rect UtilityRect(int index)=>new Rect(DungeonUi.Width*.5f+139+index*44,DungeonUi.Height-78,40,40);
        public static Rect CombatRect=>new Rect(OrbRect.x,DungeonUi.Height-140,510,126);
        static Texture2D Image(string id){if(!Images.TryGetValue(id,out var t))Images[id]=t=Resources.Load<Texture2D>("UI/V045/"+id);return t;}
        static void Draw(Rect r,string id){var t=Image(id);if(t)GUI.DrawTexture(r,t,ScaleMode.StretchToFill,true);}
        static void Liquid(Rect r,string id,float fraction)
        {
            fraction=Mathf.Clamp01(fraction);if(fraction<=0)return;
            // Source liquid occupies y45..339 inside the384px editable original.
            float top=r.height*(45+294*(1-fraction))/384f;
            GUI.BeginGroup(new Rect(r.x,r.y+top,r.width,r.height-top));
            Draw(new Rect(0,-top,r.width,r.height),id);GUI.EndGroup();
        }
        public static void DrawOrb(PlayerController player,int level)
        {
            var r=OrbRect;var hp=player.Health;var previous=GUI.color;GUI.color=Color.white;
            Draw(r,"orb-well");Liquid(r,"orb-red",hp?hp.Fraction:0);
            var resource=HudSkillResource.Read(player);
            Liquid(r,"orb-blue",resource.HasValue?resource.Value.Fraction:0);Draw(r,"orb-gloss");Draw(r,"orb-shell");
            DungeonUi.ShadowLabel(new Rect(r.x,r.y-21,r.width,20),"Lv "+level,ApprovedUiV5.Style(13,TextAnchor.MiddleCenter,true),ApprovedUiV5.Gold);
            DungeonUi.ShadowLabel(new Rect(r.x+9,r.y+40,43,19),"HP",ApprovedUiV5.Style(11,TextAnchor.MiddleCenter,true),ApprovedUiV5.Light);
            DungeonUi.ShadowLabel(new Rect(r.x+52,r.y+40,43,19),resource.HasValue?resource.Value.ShortLabel:"자원",ApprovedUiV5.Style(11,TextAnchor.MiddleCenter,true),ApprovedUiV5.Light);
            string text=hp?hp.Current+" / "+hp.Max:"-";
            string resourceText=resource.HasValue?Mathf.FloorToInt(resource.Value.Current+0.0001f)+" / "+Mathf.RoundToInt(resource.Value.Maximum):"—";
            DungeonUi.ShadowLabel(new Rect(r.x-14,r.yMax-8,66,21),text,ApprovedUiV5.Style(10,TextAnchor.MiddleCenter,true),ApprovedUiV5.Light);
            DungeonUi.ShadowLabel(new Rect(r.x+52,r.yMax-8,66,21),resourceText,ApprovedUiV5.Style(10,TextAnchor.MiddleCenter,true),ApprovedUiV5.Light);
            GUI.Label(r,new GUIContent("","체력 "+text+" · "+(resource.HasValue?resource.Value.ShortLabel:"스킬 자원")+" "+resourceText),GUIStyle.none);
            GUI.color=previous;
        }
        public static void DrawBelt()
        {
            var first=ActionRect(-1);var last=UtilityRect(1);
            ApprovedUiV5.Round(new Rect(first.x-6,first.y-7,last.xMax-first.x+12,first.height+28),new Color32(12,12,14,244),7,ApprovedUiV5.Edge,1);
            for(int i=-1;i<5;i++)Frame(ActionRect(i));for(int i=0;i<2;i++)Frame(UtilityRect(i));
        }
        static void Frame(Rect r)=>ApprovedUiV5.Round(r,new Color32(27,27,29,255),5,new Color32(77,67,54,255),1);
    }
}
