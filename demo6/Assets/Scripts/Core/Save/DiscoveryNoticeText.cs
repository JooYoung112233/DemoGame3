using Demo6.Core.Dungeon;

namespace Demo6.Core.Save
{
    /// <summary>
    /// 지난 알림에 담을 발견 알림 글(기획/저장-처음화면-멈춤창-1차.md 7장, 물결 4 반박 검토). 던전 HUD 아래 알림은 Message 말고도
    /// DungeonEvents.Discovered로 '손에 넣었다 · 곡괭이'·'숨은 방'·'지름길'·궤짝·광석·벽 등잔 글을 띄우므로 지난 알림도 같은 글을 담는다.
    /// 글은 Game/Dungeon/UI/DungeonHud.cs의 DiscoveryText·KindName과 같다(그 파일은 UI 세션 몫이라 고치지 않고 여기 복사본을 둔다 — 한쪽을 바꾸면 다른 쪽도 맞춘다).
    /// 조용한 지역 이름(NewCell)과 층 완전 탐험(FloorComplete, 글은 레벨 모듈이 Message로 띄움)은 담지 않아 null.
    /// </summary>
    public static class DiscoveryNoticeText
    {
        /// <summary>지난 알림 한 줄. 담지 않는 갈래면 null.</summary>
        public static string Line(DiscoveryKind kind, string label)
        {
            if (kind == DiscoveryKind.NewCell || kind == DiscoveryKind.FloorComplete) return null;
            string name = string.IsNullOrEmpty(label) ? KindName(kind) : label;
            switch (kind)
            {
                case DiscoveryKind.WallLamp: return "벽 등잔 — 어둠이 한 걸음 물러난다";
                case DiscoveryKind.Stake: return name + " — 희미한 불이 깃든다";
                case DiscoveryKind.HiddenRoom: return "숨은 방";
                case DiscoveryKind.Shortcut: return "지름길";
                case DiscoveryKind.Ability: return "손에 넣었다 · " + name;
                case DiscoveryKind.Story: return "남겨진 흔적 · " + name;
                case DiscoveryKind.Event: return "사건 · " + name;
                case DiscoveryKind.WoodChest: return name + " — 삐걱이며 열렸다";
                case DiscoveryKind.IronChest: return name + " — 녹슨 경첩이 비명을 지른다";
                case DiscoveryKind.Ore: return name + " — 돌이 부서져 내린다";
                default: return name;
            }
        }

        static string KindName(DiscoveryKind kind)
        {
            switch (kind)
            {
                case DiscoveryKind.WoodChest: return "나무 궤짝";
                case DiscoveryKind.IronChest: return "쇠 궤짝";
                case DiscoveryKind.Stake: return "말뚝";
                case DiscoveryKind.Story: return "이야기 물건";
                case DiscoveryKind.Event: return "사건";
                case DiscoveryKind.Ability: return "능력";
                case DiscoveryKind.Ore: return "광맥";
                case DiscoveryKind.Safe: return "금고";
                default: return "새 것";
            }
        }
    }
}
