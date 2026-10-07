using System.Collections.Generic;

namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 문틈 엿듣기로 센 건너편 칸의 기척(기획/1-2층-탐험-맛-1차.md 4-4). 건너편 칸 안에 있는 살아 있는 적을 센다(지나가던 순찰도 든다).
    /// 종류(Rats·Boars·Archers·Elites·Nests)는 한 마리를 한 곳에만 센다: 정예는 Elites에만 넣고 Boars·Archers·Rats에는 넣지 않는다.
    /// 상태(Eating·Patrolling·Awake)는 종류와 겹쳐 센다(먹는 굴쥐 2 = Rats 2 + Eating 2).
    /// Awake는 알아채고 깨어 있는 적(큰 소리에 깨어 문 안에서 기다리거나 싸우던 적)이다. 먹는 중·순찰은 Awake로 세지 않는다.
    /// </summary>
    public struct ListenTally
    {
        public int Rats;
        public int Boars;
        public int Archers;
        public int Elites;
        public int Nests;
        public int Eating;
        public int Patrolling;
        public int Awake;

        /// <summary>센 것이 하나도 없다(글은 '조용하다').</summary>
        public bool IsEmpty =>
            Rats <= 0 && Boars <= 0 && Archers <= 0 && Elites <= 0 && Nests <= 0 && Eating <= 0 && Patrolling <= 0 && Awake <= 0;
    }

    /// <summary>
    /// 문틈 엿듣기(1-2층 탐험 맛 1차 4-4, 12장 질문 2 = 가). 웅크린 채 문 가운데에서 2.4유닛 안에 1초 가만히(걸음 초당 0.3 아래) 있으면
    /// 건너편 칸의 기척을 화면 아래 알림 줄에 글 한 줄로 보여 준다. 맞거나 움직이면 처음부터, 같은 문은 15초 뒤에 다시 들을 수 있다.
    /// 막힌 문(판자벽·금 간 벽·자물쇠 문)도 들린다. 엿듣기는 소리를 내지 않고 새 키도 없다(웅크리기와 가만히 있기뿐).
    /// 글은 수를 세지 않는다(숫자를 쓰지 않음). 가장 위험한 것부터 두 마디까지:
    /// 정예 → 깨어 있음 → 순찰 → 궁수 → 돌충이 → 굴쥐(3 이상 '여럿') → 둥지 → 먹는 중. 글은 ExploreText.
    /// </summary>
    public static class DoorListenRules
    {
        /// <summary>웅크린 채 가만히 있어야 하는 시간(초).</summary>
        public const float HoldSeconds = 1.0f;
        /// <summary>문 가운데(DungeonEdge.DoorCenter)에서 이 거리 안(유닛).</summary>
        public const float Range = 2.4f;
        /// <summary>같은 문을 다시 들을 수 있을 때까지(초).</summary>
        public const float Cooldown = 15f;
        /// <summary>가만히 = 걸음이 초당 이 값 아래.</summary>
        public const float MaxSpeed = 0.3f;
        /// <summary>글 한 줄의 마디 수 상한.</summary>
        public const int MaxPhrases = 2;
        /// <summary>굴쥐가 이 수 이상이면 '발톱 소리 여럿'.</summary>
        public const int ManyRats = 3;

        /// <summary>위험한 차례로 고른 마디(두 마디까지). 비었으면 빈 목록.</summary>
        public static IReadOnlyList<string> Phrases(ListenTally t)
        {
            var list = new List<string>(8);
            if (t.Elites > 0) list.Add(ExploreText.PhraseElite);
            if (t.Awake > 0) list.Add(ExploreText.PhraseAwake);
            if (t.Patrolling > 0) list.Add(ExploreText.PhrasePatrol);
            if (t.Archers > 0) list.Add(ExploreText.PhraseArcher);
            if (t.Boars > 0) list.Add(ExploreText.PhraseBoar);
            if (t.Rats > 0) list.Add(t.Rats >= ManyRats ? ExploreText.PhraseManyRats : ExploreText.PhraseFewRats);
            if (t.Nests > 0) list.Add(ExploreText.PhraseNest);
            if (t.Eating > 0) list.Add(ExploreText.PhraseEating);
            if (list.Count > MaxPhrases) list.RemoveRange(MaxPhrases, list.Count - MaxPhrases);
            return list;
        }

        /// <summary>알림 한 줄: 비면 '문틈 너머 — 조용하다', 아니면 '문틈 너머 — {마디}, {마디}'.</summary>
        public static string Line(ListenTally t)
        {
            var phrases = Phrases(t);
            if (phrases.Count == 0) return ExploreText.ListenQuiet;
            return ExploreText.ListenHead + string.Join(ExploreText.ListenSeparator, phrases);
        }
    }
}
