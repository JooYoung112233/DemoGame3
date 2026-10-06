using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 07 창 · 읽기 전용 (말 놓기 · 기획/탐험-말놓기-조작-재설계.md 결정 6, design §4): on both visits the window only shows the object — its
    // noise, what it may drop, its progress and who is placed on it this turn, all from the planner's own check (FieldTurnPlanner.Current,
    // the same one '턴 진행' applies) — and never assigns nor passes a turn. It opens from a right press on the object
    // (FieldPawnBoard.RightPress → ExpeditionArrivalPanel.Inspect); a left press places the held pawn. The member cards, the role buttons
    // and the confirm are hidden in ExpeditionSearchPanel.prefab by AgentScripts/BuildRetireOldAssign.cs (runtime code sets no layout);
    // the old '담당자' heading line shows who is placed (Placed). 돌아가기 stays bottom left.
    public sealed partial class ExpeditionSearchPanel
    {
        [Header("읽기 전용 (말 놓기 · 사물 우클릭으로 엶)")]
        [Tooltip("켜면 두 방문 모두 이 창은 보기만 합니다 (배정 · 턴 진행 없음). 끄면 말 놓기 전의 배정 창 (쓰지 않음)")] public bool ReadOnlyWindow = true;
        [Tooltip("놓인 대원 줄 (옛 '담당자' 제목 자리 · AgentScripts/BuildRetireOldAssign.cs가 연결). 비우면 수색도 줄 아래에 씁니다")] public Text Placed;
        [Tooltip("놓인 대원 ({0}: 대원과 할 일)")] public string ReadPlaced = "놓인 대원 · {0}";
        [Tooltip("아무도 놓이지 않았을 때")] public string ReadNobody = "놓인 대원 없음 · 대원 말을 이 사물에 놓으세요";
        [Tooltip("담당 ({0}: 대원)")] public string ReadLead = "{0} 수색";
        [Tooltip("협동 ({0}: 대원, {1}: 역할)")] public string ReadHelper = "{0} {1}";
        [Tooltip("협동 역할 이름 (함께 · 망보기 · 조명)")] public string ReadTogether = "함께", ReadWatch = "망보기", ReadLight = "조명";
        [Tooltip("대원 사이")] public string ReadSeparator = " · ";
        [Tooltip("멈춘 수색 ({0}: 대원, {1}: 까닭)")] public string ReadPaused = "{0} · 멈춤 ({1})";
        [Tooltip("수색도 줄 · 이번 턴 진행 ({0}: 지금, {1}: 필요 턴, {2}: 이번 턴 뒤, {3}: 이번 턴 소음)")] public string ReadCostPlanned = "수색도 {0} / {1}턴 → {2} / {1}  ·  이번 턴 소음 +{3}";
        [Tooltip("수색도 줄 · 놓인 대원 없음 ({0}: 지금, {1}: 필요 턴, {2}: 누적 소음)")] public string ReadCostIdle = "수색도 {0} / {1}턴  ·  누적 소음 {2}";
        [Tooltip("도구 줄 ({0}: 도구) · 덮개를 연 뒤 / 담당 가방에 있음 / 없음")] public string ReadToolOpened = "덮개 개방 완료 · 도구 없이 재개 가능", ReadToolHeld = "{0} · 담당 가방에 있음 · 소모 없음", ReadToolNeeded = "{0} 필요 · 가진 대원을 놓으세요";
        [Tooltip("도구가 필요 없는 사물")] public string ReadNoTool = "도구 없이 수색 가능";
        [Tooltip("아래 안내")] [TextArea(2, 3)] public string ReadNotice = "읽기 전용 · 수색은 대원 말을 이 사물에 놓아 맡깁니다.\n시간은 '턴 진행'으로만 흐릅니다.";

        // The window only shows (both visits, while members are placed on the room: FieldTurnPlanner.Placing).
        public bool ReadOnly => ReadOnlyWindow && arrival && Planner && Planner.Placing;
        // The line under the object that says who is placed (tests read it).
        public string PlacedLine { get; private set; } = "";

        void ReadOnlyRefresh()
        {
            var pl = Planner; var k = pl.Current ?? pl.Plan.Check(pl.Facts());
            var run = k.RunFor(site); var pause = k.PauseFor(site); var order = pl.Plan.Find(site);
            arrival.Loot.Peek(site, out var s); int progress = s != null ? s.Progress : 0;
            ShowObjectNoise();
            int required = run != null ? run.Required : s != null && s.Progress > 0 ? s.Required : arrival.Loot.SiteTurns(site);
            int pace = run != null ? run.Pace : s != null && s.Progress > 0 ? s.Pace : 1, duty = run != null ? run.Duty : s != null && s.Progress > 0 ? s.Duty : 0;
            int bonus = run != null ? run.Bonus : s != null && s.Progress > 0 ? s.Bonus : 0, noise = run != null ? run.Noise : arrival.Loot.SiteNoise(site);
            if (DropPreview) DropPreview.Refresh(arrival, site, pace, duty, bonus, noise, required);
            // Who stands here this turn (the lead first, then the helper and its role), or why their search stops.
            var names = new List<string>(); int lead = run != null ? run.Lead : order != null ? order.Lead : -1;
            if (run != null)
            {
                names.Add(string.Format(ReadLead, Name(run.Lead)));
                if (run.Support >= 0) names.Add(string.Format(ReadHelper, Name(run.Support), run.Role == FieldAction.Watch ? ReadWatch : run.Role == FieldAction.Light ? ReadLight : ReadTogether));
            }
            else if (order != null && pause != FieldPause.None) names.Add(string.Format(ReadPaused, Name(order.Lead), pl.PauseShort(site, pause)));
            PlacedLine = names.Count > 0 ? string.Format(ReadPlaced, string.Join(ReadSeparator, names)) : ReadNobody;
            Worker = lead >= 0 && lead < arrival.Participants.Count ? arrival.Participants[lead] : null;
            // The tool: opened already, in the lead's bag, or needed.
            string tool = arrival.Loot.Sites[site].RequiredTool; var toolItem = arrival.Inventory.Items.FirstOrDefault(i => i.Id == tool); bool needsTool = !string.IsNullOrEmpty(tool);
            if (ToolIcon) { ToolIcon.gameObject.SetActive(needsTool); ToolIcon.sprite = toolItem?.Icon; }
            string toolName = toolItem?.Name ?? tool;
            Equipment.text = !needsTool ? ReadNoTool : s != null && s.Opened ? ReadToolOpened : Worker != null && arrival.Inventory.CountFor(Worker, tool) > 0 ? string.Format(ReadToolHeld, toolName) : string.Format(ReadToolNeeded, toolName);
            string cost = run != null ? string.Format(ReadCostPlanned, run.Before, run.Required, run.After, run.Noise) : string.Format(ReadCostIdle, progress, required, arrival.Rooms.Noise);
            Cost.text = Placed ? cost : cost + "\n" + PlacedLine;
            if (Placed) Placed.text = PlacedLine;
            Notice.text = ReadNotice;
            // Nothing here assigns (the builder hides these; if it has not run they stay but do nothing).
            if (Choose) Choose.interactable = false;
            if (Duties != null) foreach (var d in Duties) if (d) d.interactable = false;
        }
        string Name(int m) => m >= 0 && m < arrival.Participants.Count && arrival.Participants[m] != null ? arrival.Participants[m].Name : "";
    }
}
