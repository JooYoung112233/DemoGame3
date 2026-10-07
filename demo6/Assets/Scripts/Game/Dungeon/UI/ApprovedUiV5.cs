using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Approved visual assets and layout only. No progression, loot or combat state.</summary>
    public static class ApprovedUiV5
    {
        // Maps remain deferred by the current UI direction.
        public static bool MapsEnabled => false;
        const string Root = "UI/ApprovedV5/";
        public const float HudScale = 900f / 941f;
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<int, GUIStyle> Styles = new Dictionary<int, GUIStyle>();
        static GUIStyle _buttonStyle;
        public sealed class ButtonScope : IDisposable
        {
            readonly GUIStyle _previous;
            readonly Color _background;
            public ButtonScope()
            {
                _previous=GUI.skin.button;_background=GUI.backgroundColor;
                if(_buttonStyle==null)
                {
                    _buttonStyle=new GUIStyle(_previous){fontSize=20,alignment=TextAnchor.MiddleCenter,wordWrap=true,font=UiFontsA.Emphasis,fontStyle=FontStyle.Normal,border=new RectOffset(4,4,4,4),fixedHeight=0};
                    foreach(var state in new[]{_buttonStyle.normal,_buttonStyle.hover,_buttonStyle.active,_buttonStyle.focused}){state.background=Texture("button-mask");state.textColor=Light;}
                }
                GUI.skin.button=_buttonStyle;GUI.backgroundColor=new Color32(75,66,48,255);
            }
            public void Dispose(){GUI.skin.button=_previous;GUI.backgroundColor=_background;}
        }
        public static readonly Color Light = new Color32(242,231,201,255);
        public static readonly Color Muted = new Color32(181,171,151,255);
        public static readonly Color Gold = new Color32(210,181,124,255);
        public static readonly Color Panel = new Color32(27,26,24,255);
        public static readonly Color Edge = new Color32(111,91,58,255);
        public static Texture2D Texture(string id)
        {
            if (!Textures.TryGetValue(id,out var image)) Textures[id]=image=Resources.Load<Texture2D>(Root+id);
            return image;
        }
        public static Rect HudRect(float x,float y,float w,float h) => new Rect(DungeonUi.Width*.5f+(x-836f)*HudScale,DungeonUi.Height+(y-941f)*HudScale,w*HudScale,h*HudScale);
        public static Rect ActionRect(int index) => HudRect(576f+(index+1)*59f,847f,59f,58.2f);
        public static Rect UtilityRect(int index) => HudRect(947f+index*47f,861f,47f,45f);
        public static Rect HudHitRect(Rect r) => new Rect(r.x-2,r.y,r.width+4,r.height+19);
        public static Rect HealthRect => HudRect(607f,820f,315f,29.4f);
        public static void Image(Rect r,string id,Color? tint=null)
        {
            var image=Texture(id);if(!image)return;var c=GUI.color;GUI.color=tint??Color.white;
            GUI.DrawTexture(r,image,ScaleMode.StretchToFill,true);GUI.color=c;
        }
        public static void Icon(Rect r,string id,Color? tint=null,Demo6.Core.Loot.Grade grade=Demo6.Core.Loot.Grade.Common)
        {
            if(EquipmentVisualV049.DrawIcon(r,id,grade,tint))return;
            var image=Texture(id);if(!image){ItemIconArt.Draw(r,id);return;}var c=GUI.color;GUI.color=tint??Color.white;
            GUI.DrawTexture(r,image,ScaleMode.ScaleToFit,true);GUI.color=c;
        }
        public static GUIStyle Style(int size=18,TextAnchor anchor=TextAnchor.UpperLeft,bool bold=false,bool wrap=false)
        {
            int key=size+((int)anchor<<8)+(bold?4096:0)+(wrap?8192:0);
            if(!Styles.TryGetValue(key,out var style))
            {
                style=new GUIStyle(DungeonUi.Label){fontSize=size,alignment=anchor,font=bold?UiFontsA.Emphasis:UiFontsA.Body,fontStyle=FontStyle.Normal,wordWrap=wrap,clipping=TextClipping.Clip};
                style.normal.textColor=Color.white;Styles[key]=style;
            }
            return style;
        }
        public static void Label(Rect r,string text,int size=18,Color? color=null,TextAnchor anchor=TextAnchor.UpperLeft,bool bold=false,bool wrap=false)
        {
            var previous=GUI.color;GUI.color=color??Light;GUI.Label(r,text,Style(size,anchor,bold,wrap));GUI.color=previous;
        }
        public static void TitleLabel(Rect r,string text,int size=28,Color? color=null,TextAnchor anchor=TextAnchor.UpperLeft,bool bold=true,bool wrap=false)
        {
            var previous=GUI.color;GUI.color=color??Light;
            GUI.Label(r,text,UiFontsA.HeadingStyle(Style(size,anchor,true,wrap),text));GUI.color=previous;
        }
        public static void Round(Rect r,Color fill,float radius=12f,Color? edge=null,float border=2f)
        {
            if(r.width<=0||r.height<=0)return;
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            // Native rounded rectangles are one draw each: no nine-slice seams.
            // Fill first, then draw ONLY the border. A translucent grade interior
            // must never be composited over an opaque, full-size grade-colored base.
            if(fill.a>0)GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,fill,0,radius);
            if(edge.HasValue&&border>0)GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,edge.Value,border,radius);
        }
        public static void EquipmentSlot(Rect r,bool selected=false)
        {
            Image(r,"action-frame",new Color(.78f,.78f,.78f,1));
            if(selected)Round(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Color.clear,10,Gold,2);
        }

        public static bool Button(Rect r,string text,bool primary=false)
        {
            bool click=GUI.Button(r,text,GUIStyle.none);var tint=GUI.enabled?1f:.45f;
            Color fill=primary?new Color32(75,66,48,255):new Color32(39,37,32,255);fill*=new Color(tint,tint,tint,1);
            Round(r,fill,12,primary?new Color32(145,121,79,255):Edge,1.5f);
            Label(r,text,18,Light*new Color(tint,tint,tint,1),TextAnchor.MiddleCenter,primary);return click;
        }
        public static bool Close(Rect r)
        {
            bool click=GUI.Button(r,new GUIContent("","닫기 · Esc"),GUIStyle.none);
            Round(r,new Color32(134,75,67,255),12,new Color32(197,132,110,255),2);
            Label(r,"×",28,new Color32(255,234,209,255),TextAnchor.MiddleCenter,true);return click;
        }
        public static void HudBackdrop()
        {
            Image(HudRect(576,847,354,58.2f),"action-dock");
            for(int i=0;i<3;i++)Image(UtilityRect(i),"utility-frame");
        }
        public static void Key(Rect r,string text)
        {
            DungeonUi.ShadowLabel(r,text,Style(12,TextAnchor.MiddleCenter,true),Gold);
        }
        public static void Cooldown(Rect r,string value)=>DungeonUi.ShadowLabel(r,value,Style(20,TextAnchor.MiddleCenter,true),Light);
        public static void Health(Rect r,float fraction)
        {
            Image(r,"health-cradle");
            // Paint the source's baked full fill with actual HP every frame.
            var fill=new Rect(r.x+r.width*.075f,r.y+r.height*.33f,r.width*.85f,r.height*.33f);
            Round(fill,new Color32(37,14,15,255),3);
            if(fraction>0)Round(new Rect(fill.x,fill.y,fill.width*Mathf.Clamp01(fraction),fill.height),new Color32(160,45,37,255),3);
        }
    }
}
