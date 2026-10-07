using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>
    /// 화자 이름 공개(기획/마을-의뢰-첫판.md 3-2, 저장소 루트 AGENTS.md 화자 규칙, LIVE49 SPEAKER-IDENTITY-RULES).
    /// 이름과 인물이 이어지기 전까지 이름표는 '?'. 자기소개뿐 아니라 호명·다른 인물의 대사·설명으로 드러나도 공개한다.
    /// 공개 계기는 대사 줄의 Reveals 하나뿐이고(문장 속 이름을 자동으로 찾지 않음), 공개 줄을 넘기는 순간 이름 인지를 꾸러미에 적는다.
    /// 다시 '?'로 되돌리지 않는다. <b>이름이 화면에 보이는 곳은 모두 이 파일을 거친다</b>: 대화창 이름표(Label), F 안내(Hint),
    /// 목표 HUD·도착 카드·던전 알림의 '알릴 곳'(ReportTarget), 의뢰 목록의 주는 사람(GiverLabel).
    /// 던전 알림(DungeonEvents.Say)으로 사람을 가리킬 때도 ReportTarget을 거친다.
    /// 이름을 쓰지 않는 곳: 의뢰 제목, HUD 틀, 바크, 머리 위 표시, 시설 창 제목, 물체 글, 명패 글, 공개 전 장면의 건너뛰기 요약.
    /// 머리 위 이름표는 띄우지 않는다(머리 위 '?'가 의뢰 표시처럼 읽히지 않게).
    /// </summary>
    public static class SpeakerIdentity
    {
        /// <summary>이름을 모르는 화자의 이름표.</summary>
        public const string Unknown = "?";

        /// <summary>이 주민 이름을 아는가.</summary>
        public static bool Knows(CarryData carry, string npcId) => TownSave.KnowsName(carry, npcId);

        /// <summary>이름을 알게 됨(공개 줄을 넘길 때). 표에 없는 id는 무시. 새로 알았으면 true. 여러 번 불러도 같다.</summary>
        public static bool Reveal(CarryData carry, string npcId)
        {
            if (!NpcTable.Exists(npcId)) return false;
            return TownSave.SetNameKnown(carry, npcId);
        }

        /// <summary>대화창 이름표: 알면 표시 이름, 모르면 '?'. 나레이션(빈 id)은 빈 글(이름표 없음).</summary>
        public static string Label(CarryData carry, string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return "";
            var npc = NpcTable.Get(npcId);
            if (npc == null) return Unknown;
            return Knows(carry, npcId) ? npc.DisplayName : Unknown;
        }

        /// <summary>'알릴 곳'(목표 HUD·도착 카드·알림): 알면 '무진에게', 모르면 '경비 초소에'.</summary>
        public static string ReportTarget(CarryData carry, string npcId)
        {
            var npc = NpcTable.Get(npcId);
            if (npc == null) return "";
            return Knows(carry, npcId) ? npc.DisplayName + "에게" : npc.Place + "에";
        }

        /// <summary>
        /// 기술을 배우는 곳(기획/스킬-자원-트리-1차.md 2장): 이름을 알면 '무진에게', 모르면 '경비 초소에서'. 알림·스킬 창에서 '마을 {이 말} 배운다'로 쓴다.
        /// </summary>
        public static string TeachAt(CarryData carry)
        {
            var npc = NpcTable.Get(NpcTable.Trainer);
            if (npc == null) return "";
            return carry != null && Knows(carry, NpcTable.Trainer) ? npc.DisplayName + "에게" : npc.Place + "에서";
        }

        /// <summary>의뢰 목록의 주는 사람: 알면 '무진', 모르면 '? (경비 초소)'.</summary>
        public static string GiverLabel(CarryData carry, string npcId)
        {
            var npc = NpcTable.Get(npcId);
            if (npc == null) return Unknown;
            return Knows(carry, npcId) ? npc.DisplayName : Unknown + " (" + npc.Place + ")";
        }

        /// <summary>
        /// F 안내(6-3): 'F {이름 | 말 걸기}' 뒤에 보고할 것이 있으면 ' · 보고', 받을 것이 있으면 ' · 의뢰'.
        /// 예: 'F 춘삼 · 보고', 'F 말 걸기 · 의뢰'(이름 공개 전 무진), 'F 무진'.
        /// </summary>
        public static string Hint(CarryData carry, string npcId, QuestMarker marker)
        {
            string who = Knows(carry, npcId) && NpcTable.Get(npcId) != null ? NpcTable.Get(npcId).DisplayName : "말 걸기";
            switch (marker)
            {
                case QuestMarker.Report: return "F " + who + " · 보고";
                case QuestMarker.Offer: return "F " + who + " · 의뢰";
                default: return "F " + who;
            }
        }
    }
}
