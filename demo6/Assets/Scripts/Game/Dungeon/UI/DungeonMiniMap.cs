using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Read-only local view of existing exploration state. Never reads enemies or reveals secret walls.</summary>
    public sealed class DungeonMiniMap : MonoBehaviour
    {
        public static DungeonMiniMap Instance { get; private set; }
        public static bool Visible => ApprovedUiV5.MapsEnabled && Instance && !DungeonUi.ModalOpen && !(ExplorationLog.Instance && ExplorationLog.Instance.PanelVisible);
        public static Rect PanelRect => new Rect(DungeonUi.Width - 238f, 16f, 222f, 180f);
        public static bool ShowsCell(DungeonCell cell) => cell != null && (cell.Visited || BigMap.KnownFrontier(cell));
        void Awake() => Instance = this;
        void OnDestroy() { if(Instance==this)Instance=null; }

        void OnGUI()
        {
            var root=DungeonRoot.Instance;
            if(!Visible || !root || root.World==null || root.CurrentCell==null || !root.Player)return;
            DungeonUi.Begin();GUI.depth=4;
            var r=PanelRect;
            bool open=GUI.Button(r,new GUIContent("","큰 지도 · M"),GUIStyle.none);
            DungeonUi.Box(r,.93f);
            GUI.Label(new Rect(r.x+14,r.y+9,148,24),"제"+root.Floor+"층 · 탐사",DungeonUi.Small);
            DungeonUi.ShadowLabel(new Rect(r.xMax-38,r.y+9,24,24),"M",DungeonUi.KeyLabel,DungeonUi.BoneDim);
            var area=new Rect(r.x+14,r.y+38,r.width-28,r.height-52);
            var current=root.CurrentCell;
            const float stepX=38f,stepY=25f,cellW=30f,cellH=17f;
            Rect Cell(DungeonCell c)=>new Rect(area.center.x+(c.Map.X-current.Map.X)*stepX-cellW*.5f,
                area.center.y-(c.Map.Y-current.Map.Y)*stepY-cellH*.5f,cellW,cellH);
            bool InRange(DungeonCell c)=>Mathf.Abs(c.Map.X-current.Map.X)<=2&&Mathf.Abs(c.Map.Y-current.Map.Y)<=2;
            foreach(var edge in root.World.Edges)
            {
                if(!BigMap.RouteKnown(edge) || (!edge.A.Visited&&!edge.B.Visited) || !InRange(edge.A)||!InRange(edge.B))continue;
                var a=Cell(edge.A).center;var b=Cell(edge.B).center;
                DungeonUi.Fill(new Rect(Mathf.Min(a.x,b.x)-1,Mathf.Min(a.y,b.y)-1,Mathf.Max(2,Mathf.Abs(a.x-b.x)+2),Mathf.Max(2,Mathf.Abs(a.y-b.y)+2)),new Color(.43f,.38f,.29f));
            }
            foreach(var cell in root.World.Cells)
            {
                if(!InRange(cell)||!ShowsCell(cell))continue;
                var at=Cell(cell);
                if(cell.Visited)
                {
                    DungeonUi.Fill(at,new Color(.17f,.15f,.12f));
                    DungeonUi.Outline(at,cell==current?DungeonUi.Ember:new Color(.48f,.44f,.36f));
                    MiniMapDots.Draw(at,cell,root); // 1-2층 탐험 맛 1차 4-10 색 점(계단·켠 말뚝·안 연 궤짝·사건)
                }
                else
                {
                    DungeonUi.Fill(at,new Color(.04f,.035f,.025f));
                    DungeonUi.ShadowLabel(at,"?",DungeonUi.KeyLabel,DungeonUi.BoneDim);
                }
            }
            var room=Cell(current);var bounds=current.Bounds;var pos=root.Player.Position;
            Vector2 here=new Vector2(room.x+Mathf.Clamp01((pos.x-bounds.xMin)/bounds.width)*room.width,
                room.y+(1f-Mathf.Clamp01((pos.y-bounds.yMin)/bounds.height))*room.height);
            DungeonUi.Fill(new Rect(here.x-3,here.y-3,6,6),new Color(.98f,.87f,.58f));
            var direction=root.Player.FacingDirection;var matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(here,Quaternion.Euler(0,0,Mathf.Atan2(-direction.y,direction.x)*Mathf.Rad2Deg),Vector3.one);
            DungeonUi.Fill(new Rect(2,-1,7,2),DungeonUi.Bone);GUI.matrix=matrix;
            if(open)DungeonUi.TryOpen(BigMap.MapModal);
        }
    }
}
