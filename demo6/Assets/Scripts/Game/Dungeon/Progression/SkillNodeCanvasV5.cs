using System;
using System.Collections.Generic;
using UnityEngine;
namespace Demo6.Game
{
    /// <summary>
    /// 스킬 트리 칸 그리기(데이터를 받아 그리기만). 칸은 틀(action-frame) + 아이콘/기호, 가지는 부모 오른쪽 → 자식 왼쪽으로 꺾어 잇는다
    /// (기획/스킬-자원-트리-1차.md 4장, 사용자 말 2026-10-07 '나무 형태 가지로, 삼국지 토탈워 개혁 느낌, 이미지는 틀만').
    /// </summary>
    public static class SkillNodeCanvasV5
    {
        public enum NodeState { Locked, Available, NeedsTrainer, Taken, Maxed, Placeholder }

        public struct Node
        {
            public string Id,Title,Icon,Caption;
            public int Glyph;
            public Rect Bounds;
            public bool Selected;
            public NodeState State;
            public int Rank,MaxRank;
        }
        public struct Link { public int From,To; public Color Color; public bool Dashed; }

        public static readonly Color LinkTaken = new Color32(210,181,124,255);
        public static readonly Color LinkOpen = new Color32(196,186,160,200);
        public static readonly Color LinkLocked = new Color32(84,77,66,200);
        static readonly Color Ready = new Color32(167,193,132,255);
        static readonly Color TrainerTag = new Color32(160,149,127,255);

        public static void Draw(IReadOnlyList<Node> nodes,IReadOnlyList<Link> links,Action<int> select)
        {
            if(links!=null)foreach(var edge in links)
            {
                if(edge.From<0||edge.To<0||edge.From>=nodes.Count||edge.To>=nodes.Count)continue;
                Branch(nodes[edge.From].Bounds,nodes[edge.To].Bounds,edge.Color,edge.Dashed);
            }
            float pulse=.55f+.45f*Mathf.Sin(Time.unscaledTime*4f);
            for(int i=0;i<nodes.Count;i++)
            {
                var node=nodes[i];var r=node.Bounds;
                bool placeholder=node.State==NodeState.Placeholder;
                bool clicked=!placeholder&&GUI.Button(r,new GUIContent("",node.Title),GUIStyle.none);
                bool lit=node.State==NodeState.Taken||node.State==NodeState.Maxed;
                float shade=placeholder?.3f:node.State==NodeState.Locked?.45f:lit?1f:.78f;
                ApprovedUiV5.Image(r,"action-frame",new Color(shade,shade,shade,1f));
                var inner=new Rect(r.x+r.width*.2f,r.y+r.height*.18f,r.width*.6f,r.height*.6f);
                var tint=new Color(shade,shade,shade,1f);
                if(placeholder)ApprovedUiV5.Label(r,"?",30,new Color(.45f,.42f,.36f,1f),TextAnchor.MiddleCenter,true);
                else if(node.Icon!=null)ApprovedUiV5.Icon(inner,node.Icon,tint);
                else SkillGlyph.Draw(inner,node.Glyph,ApprovedUiV5.Gold*tint);
                if(lit)DungeonUi.Outline(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),LinkTaken,2);
                if(node.State==NodeState.Available)
                {
                    DungeonUi.Outline(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),new Color(Ready.r,Ready.g,Ready.b,pulse),2);
                    ApprovedUiV5.Round(new Rect(r.xMax-14,r.y+4,10,10),Ready,5);
                }
                if(node.State==NodeState.NeedsTrainer)
                {
                    var tag=new Rect(r.x+4,r.yMax-20,r.width-8,16);
                    DungeonUi.Fill(tag,new Color(0,0,0,.55f));
                    ApprovedUiV5.Label(tag,"마을에서",11,TrainerTag,TextAnchor.MiddleCenter,true);
                }
                if(node.MaxRank>1&&!placeholder)Pips(new Rect(r.x+8,r.y+5,r.width-16,6),node.Rank,node.MaxRank);
                if(node.Selected)
                {
                    DungeonUi.Outline(new Rect(r.x-6,r.y-6,r.width+12,r.height+12),ApprovedUiV5.Gold,2);
                    DungeonUi.Outline(new Rect(r.x-9,r.y-9,r.width+18,r.height+18),new Color(ApprovedUiV5.Gold.r,ApprovedUiV5.Gold.g,ApprovedUiV5.Gold.b,.35f),1);
                }
                var titleColor=placeholder?new Color(.5f,.47f,.41f,1f):node.State==NodeState.Locked?ApprovedUiV5.Muted:ApprovedUiV5.Light;
                ApprovedUiV5.Label(new Rect(r.x-50,r.yMax+6,r.width+100,24),node.Title,16,titleColor,TextAnchor.MiddleCenter,true);
                ApprovedUiV5.Label(new Rect(r.x-50,r.yMax+28,r.width+100,20),node.Caption,13,placeholder?new Color(.45f,.42f,.36f,1f):ApprovedUiV5.Gold,TextAnchor.MiddleCenter);
                if(clicked)select?.Invoke(i);
            }
        }

        /// <summary>랭크 눈금(채운 칸 = 지금 랭크).</summary>
        static void Pips(Rect r,int rank,int max)
        {
            float gap=3f,w=(r.width-gap*(max-1))/max;
            for(int i=0;i<max;i++)DungeonUi.Fill(new Rect(r.x+i*(w+gap),r.y,w,r.height),i<rank?LinkTaken:new Color(0,0,0,.6f));
        }

        /// <summary>가지: 부모 오른쪽 가운데 → 가운데 열에서 꺾어 → 자식 왼쪽 가운데.</summary>
        static void Branch(Rect from,Rect to,Color c,bool dashed)
        {
            var a=new Vector2(from.xMax+2,from.center.y);var b=new Vector2(to.xMin-2,to.center.y);
            float midX=Mathf.Lerp(a.x,b.x,.45f);
            Seg(a,new Vector2(midX,a.y),c,dashed);Seg(new Vector2(midX,a.y),new Vector2(midX,b.y),c,dashed);Seg(new Vector2(midX,b.y),b,c,dashed);
            DungeonUi.Fill(new Rect(b.x-5,b.y-4,5,8),c);
        }

        static void Seg(Vector2 a,Vector2 b,Color c,bool dashed)
        {
            const float T=3f;
            if(!dashed){DungeonUi.Fill(new Rect(Mathf.Min(a.x,b.x)-T*.5f,Mathf.Min(a.y,b.y)-T*.5f,Mathf.Abs(a.x-b.x)+T,Mathf.Abs(a.y-b.y)+T),c);return;}
            float len=Vector2.Distance(a,b);var dir=len>0.01f?(b-a)/len:Vector2.zero;
            for(float t=0;t<len;t+=12f)
            {
                var p=a+dir*t;var q=a+dir*Mathf.Min(len,t+6f);
                DungeonUi.Fill(new Rect(Mathf.Min(p.x,q.x)-1,Mathf.Min(p.y,q.y)-1,Mathf.Abs(p.x-q.x)+2,Mathf.Abs(p.y-q.y)+2),c);
            }
        }
    }
}
