using Demo6.Core.Progression;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Demo6.Game
{
    /// <summary>
    /// 스킬 트리 창(기획/스킬-자원-트리-1차.md, 사용자 결정 2026-10-07). K로 어디서나 열고, 랭크 칸은 어디서나 올린다.
    /// 배우기·갈림 칸은 마을 무진과 이야기를 마치면 열리는 '배우기' 창(OpenTeach)에서만 점수를 쓴다. 고르기와 점수 쓰기는 나눈다.
    /// 그리기는 SkillPanel.ApprovedV5.cs(나무 가지 모양, 안쪽 스크롤·끌어 옮기기).
    /// </summary>
    public sealed partial class SkillPanel : MonoBehaviour
    {
        public const string ModalName = "skills";
        PlayerProgress _progress;
        SkillNodeId _selected = SkillNodeId.LearnWhirl;
        bool _teach;
        /// <summary>연 프레임(대화를 Enter로 닫은 그 키가 '배우기'로 먹히지 않게 그 프레임의 키는 읽지 않는다).</summary>
        int _openedFrame = -1;

        public SkillNodeId SelectedNode => _selected;
        /// <summary>무진에게서 배우는 창으로 열렸는가(배우기·갈림 칸에 점수를 쓸 수 있다).</summary>
        public bool TeachMode => _teach && DungeonUi.Modal == ModalName;

        void Awake() => _progress = GetComponent<PlayerProgress>();

        /// <summary>마을 무진과 이야기를 마친 뒤: 배울 칸이 있으면 배우기 창을 연다. 열었으면 true.</summary>
        public static bool OpenTeach()
        {
            var p = PlayerProgress.Instance;
            var panel = p ? p.GetComponent<SkillPanel>() : null;
            return panel && p.AnyTrainerNode && panel.Open(true);
        }

        public bool Open(bool teach)
        {
            if (DungeonUi.Modal == ModalName) DungeonUi.Close(ModalName);
            if (!DungeonUi.TryOpen(ModalName)) return false;
            _teach = teach;
            _selected = Suggested(teach);
            _openedFrame = Time.frameCount;
            return true;
        }

        /// <summary>처음 고를 칸: 지금 점수를 쓸 수 있는 첫 칸(배우기 창이면 배우기·갈림 칸 먼저), 없으면 그대로.</summary>
        SkillNodeId Suggested(bool teach)
        {
            if (!_progress) return _selected;
            foreach (var n in SkillTree.Nodes)
                if (n.Trainer == teach && _progress.Check(n.Id, teach).Ok) return n.Id;
            foreach (var n in SkillTree.Nodes)
                if (_progress.Check(n.Id, teach).Ok) return n.Id;
            return _selected;
        }

        public SkillCheck CheckSelected() => _progress ? _progress.Check(_selected, _teach) : default;

        public bool TakeSelected() => _progress && _progress.TryTake(_selected, _teach);

        void Update()
        {
            var kb = Keyboard.current;
            bool open = DungeonUi.Modal == ModalName;
            if (!open) _teach = false;
            if (kb == null || Time.frameCount <= _openedFrame) return;
            if (kb.kKey.wasPressedThisFrame)
            {
                if (open) DungeonUi.Close(ModalName);
                else if (!PlayerInputReader.Blocked) Open(false);
                return;
            }
            if (!open) return;
            if (kb.escapeKey.wasPressedThisFrame) { DungeonUi.Close(ModalName); return; }
            if (kb.leftArrowKey.wasPressedThisFrame) Move(-1, 0);
            if (kb.rightArrowKey.wasPressedThisFrame) Move(1, 0);
            if (kb.upArrowKey.wasPressedThisFrame) Move(0, -1);
            if (kb.downArrowKey.wasPressedThisFrame) Move(0, 1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) TakeSelected();
        }

        /// <summary>방향키: 그 방향에 있는 가장 가까운 칸(가로는 칸 사이 열, 세로는 줄 차이를 먼저 본다).</summary>
        void Move(int dx, int dy)
        {
            var from = SkillTree.Node(_selected);
            SkillNodeDef best = null;
            float bestScore = float.MaxValue;
            foreach (var n in SkillTree.Nodes)
            {
                if (n.Id == _selected) continue;
                float ddx = n.Col - from.Col, ddy = n.Row - from.Row;
                float along = dx != 0 ? ddx * dx : ddy * dy;
                float across = dx != 0 ? Mathf.Abs(ddy) : Mathf.Abs(ddx);
                if (along <= 0.01f) continue;
                float score = along + across * 2f;
                if (score < bestScore) { bestScore = score; best = n; }
            }
            if (best != null) _selected = best.Id;
        }

        void OnDisable() { if (DungeonUi.Modal == ModalName) DungeonUi.Close(ModalName); }

        void OnGUI() => DrawApprovedSkillNodesV5();
    }

    /// <summary>아이콘은 형태로 구분한다: 회전, 검풍, 내려찍기, 방패, 구르기, 투지(불꽃). 원화 자산을 대체하지 않는 UI 기호.</summary>
    public static class SkillGlyph
    {
        static void Line(Vector2 a,Vector2 b,Color c,float width=3f){var m=GUI.matrix;GUI.matrix=m*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);DungeonUi.Fill(new Rect(0,-width*.5f,Vector2.Distance(a,b),width),c);GUI.matrix=m;}
        public static void Draw(Rect r,int kind,Color c)
        {
            if(kind==0||kind==1||kind==2||kind==4){DungeonHudArt.Draw(r,kind==0?"whirl":kind==1?"wave":kind==2?"attack":"dodge",Color.white);return;}
            Vector2 P(float x,float y)=>new Vector2(r.x+x*r.width,r.y+y*r.height);
            if(kind==5)
            {
                // 투지: 가로 막대를 쌓은 불꽃(스크롤 안에서는 회전 그리기가 잘려서 돌리지 않는다).
                float[] w={.10f,.18f,.26f,.34f,.42f,.48f,.52f,.52f,.48f,.40f,.28f};
                float h=r.height*.8f/w.Length;
                for(int i=0;i<w.Length;i++){float y=r.y+r.height*.1f+i*h;DungeonUi.Fill(new Rect(r.x+r.width*(.5f-w[i]*.5f),y,r.width*w[i],h+.5f),c);}
                var core=new Color(.12f,.08f,.05f,c.a);
                float[] cw={.10f,.16f,.22f,.24f,.20f};
                for(int i=0;i<cw.Length;i++){float y=r.y+r.height*.1f+(i+5)*h;DungeonUi.Fill(new Rect(r.x+r.width*(.5f-cw[i]*.5f),y,r.width*cw[i],h+.5f),core);}
                return;
            }
            Vector2[] v={P(.5f,.1f),P(.85f,.25f),P(.76f,.67f),P(.5f,.91f),P(.24f,.67f),P(.15f,.25f)};for(int i=0;i<v.Length;i++)Line(v[i],v[(i+1)%v.Length],c);Line(P(.5f,.28f),P(.5f,.69f),c);
        }
    }
}
