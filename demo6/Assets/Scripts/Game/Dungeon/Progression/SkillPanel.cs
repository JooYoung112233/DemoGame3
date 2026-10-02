using Demo6.Core.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 스킬 창(K, 계약서 창 이름 "skills"): 1줄 4칸(3차 초안 4-5)의 이름, 랭크 n/4, 지금·다음 효과, [+].
    /// [+]는 점수 1점 이상, Lv 2 이상, 4랭크 미만일 때만 누를 수 있다. 여는 동안 시간이 멈춘다(DungeonUi.TryOpen).
    /// 판정에 필요한 정보만 단순하게 그린다. 정식 화면은 Unity 개발 단계에서 다시 만든다.
    /// </summary>
    public sealed class SkillPanel : MonoBehaviour
    {
        public const string ModalName = "skills";

        const float PanelWidth = 640f;
        const float RowHeight = 78f;
        const float HeaderHeight = 92f;
        const float FooterHeight = 40f;

        PlayerProgress _progress;

        void Awake() => _progress = GetComponent<PlayerProgress>();

        void Update()
        {
            // 창 여닫기 키는 시간이 멈춘 동안에도 들어야 하므로 키보드를 직접 읽는다(계약서 10).
            var kb = Keyboard.current;
            if (kb == null) return;
            bool open = DungeonUi.Modal == ModalName;
            if (kb.kKey.wasPressedThisFrame)
            {
                if (open) DungeonUi.Close(ModalName);
                else DungeonUi.TryOpen(ModalName);
            }
            else if (open && kb.escapeKey.wasPressedThisFrame)
            {
                DungeonUi.Close(ModalName);
            }
        }

        void OnDisable()
        {
            if (DungeonUi.Modal == ModalName) DungeonUi.Close(ModalName);
        }

        void OnGUI()
        {
            if (DungeonUi.Modal != ModalName) return;
            var p = _progress ? _progress : PlayerProgress.Instance;
            if (!p) return;

            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            bool prevEnabled = GUI.enabled;
            DungeonUi.Begin();
            GUI.depth = -20;

            float height = HeaderHeight + RowHeight * SkillTree.Count + FooterHeight;
            var r = new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - height) * 0.5f, PanelWidth, height);
            DungeonUi.Box(r, 0.93f);

            GUI.Label(new Rect(r.x + 20f, r.y + 12f, PanelWidth - 40f, 30f), "스킬", DungeonUi.Title);
            GUI.Label(new Rect(r.x + 20f, r.y + 44f, PanelWidth - 40f, 22f),
                $"레벨 {p.Level}    스킬 점수 {p.SkillPoints}", DungeonUi.Bold);
            if (p.Level < SkillDef.RequiredLevel)
                GUI.Label(new Rect(r.x + 20f, r.y + 66f, PanelWidth - 40f, 20f), "레벨 2부터 점수를 쓸 수 있다", DungeonUi.Small);
            else if (p.SkillPoints <= 0)
                GUI.Label(new Rect(r.x + 20f, r.y + 66f, PanelWidth - 40f, 20f), "남은 점수가 없다 — 레벨이 오르면 1점", DungeonUi.Small);

            float y = r.y + HeaderHeight;
            foreach (var def in SkillTree.All)
            {
                DrawRow(p, def, new Rect(r.x + 14f, y, PanelWidth - 28f, RowHeight - 8f));
                y += RowHeight;
            }

            GUI.color = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(r.x + 20f, r.yMax - FooterHeight + 8f, PanelWidth - 40f, 22f), "K · Esc 닫기", DungeonUi.Small);

            GUI.enabled = prevEnabled;
            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        static void DrawRow(PlayerProgress p, SkillDef def, Rect row)
        {
            int rank = p.Rank(def.Id);
            DungeonUi.Fill(row, new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Color.white;
            GUI.Label(new Rect(row.x + 10f, row.y + 6f, 260f, 24f), $"{def.Name}  ·  {def.Branch}", DungeonUi.Bold);

            // 랭크 칸 4개.
            float pipX = row.x + 280f;
            for (int i = 0; i < SkillDef.MaxRank; i++)
            {
                var pip = new Rect(pipX + i * 22f, row.y + 11f, 16f, 14f);
                DungeonUi.Fill(pip, i < rank ? new Color(1f, 0.92f, 0.7f, 0.95f) : new Color(1f, 1f, 1f, 0.15f));
            }
            GUI.Label(new Rect(pipX + SkillDef.MaxRank * 22f + 6f, row.y + 6f, 90f, 24f), $"{rank}/{SkillDef.MaxRank}", DungeonUi.Label);

            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(row.x + 10f, row.y + 36f, 250f, 22f), "지금: " + def.EffectText(rank), DungeonUi.Small);
            string next = rank < SkillDef.MaxRank ? "다음: " + def.EffectText(rank + 1) : "최대 랭크";
            GUI.Label(new Rect(row.x + 270f, row.y + 36f, 250f, 22f), next, DungeonUi.Small);
            GUI.color = Color.white;

            bool can = p.CanRankUp(def.Id);
            bool prev = GUI.enabled;
            GUI.enabled = can;
            if (GUI.Button(new Rect(row.xMax - 62f, row.y + 14f, 50f, 40f), "+")) p.TryRankUp(def.Id);
            GUI.enabled = prev;
        }
    }
}
