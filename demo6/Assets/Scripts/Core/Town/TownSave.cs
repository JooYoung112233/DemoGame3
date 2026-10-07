using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>
    /// 오우거 패배 반응(rx:ogre_lost)이 가리킬 패턴(전투 문서 3-7 '그 판에 가장 많이 맞은 패턴을 가리킴'). 돌진·내려찍기만 따로 한 줄이 있고,
    /// 그 밖(휩쓸기·포효·맞은 적 없음)은 None = 두 줄 그대로. 꾸러미 값이라 차례(정수)를 바꾸지 않는다.
    /// </summary>
    public enum OgreLossHint
    {
        None = 0,
        Charge = 1,
        Slam = 2,
    }

    /// <summary>
    /// 마을·의뢰 저장(기획/마을-의뢰-첫판.md 7장). 꾸러미 Counters(문자열 → 정수)에 머리말 키로 담는다. 꾸러미 판본(2)은 올리지 않는다.
    /// <b>이 키를 읽고 쓰는 곳은 이 파일 하나뿐이다</b>(QuestBook·SpeakerIdentity·TownArrivalRules·TalkDirector는 이 파일을 거친다).
    /// 판본 3(전투 단계 7 또는 M2 디스크 저장)에서 Quests·KnownNames·SeenScenes를 정식 칸으로 옮길 때 이 파일과 왕복 시험만 고친다(11장 위험 12).
    /// 키: q.st:{의뢰} 상태, q.sp:{의뢰} 단계, q.n:{의뢰} 센 수, q.ac:{의뢰} 받은 차례(목표 HUD '최근에 받은 순'·보고 '오래된 것 먼저'),
    /// nm:{npc} 이름 앎, sc:{장면} 본 장면, ms:{이정표} 받기 전 기록, rx:{반응} 다음 대화 첫 줄 반응(쓰면 지움),
    /// rx:{반응}.hint 그 반응이 고를 줄(지금은 rx:ogre_lost.hint = OgreLossHint, 반응을 지우면 함께 지움), rp:{npc} 반복 대사 차례,
    /// town.visits 마을 도착 수, town.departures 권양기 출발 수, town.tags 맡긴 명패 수, town.arrived 마지막으로 처리한 도착(원정 번호).
    /// 저장하지 않는 것: 마을 안 위치, 시설 해금, 켜진 등불 수(12 − town.tags), 주민 단계·머리 위 표시(의뢰 상태에서 계산).
    /// 기본값(0·없음)이면 키를 지워 꾸러미 글을 짧게 둔다.
    /// </summary>
    public static class TownSave
    {
        public const string QuestStatePrefix = "q.st:";
        public const string QuestStepPrefix = "q.sp:";
        public const string QuestCountPrefix = "q.n:";
        public const string QuestOrderPrefix = "q.ac:";
        public const string NamePrefix = "nm:";
        public const string ScenePrefix = "sc:";
        public const string MilestonePrefix = "ms:";
        public const string ReactionPrefix = "rx:";
        public const string RepeatPrefix = "rp:";
        public const string VisitsKey = "town.visits";
        public const string DeparturesKey = "town.departures";
        public const string TagsKey = "town.tags";
        public const string ArrivedKey = "town.arrived";

        /// <summary>반응 이름(5-5).</summary>
        public const string RxDowned = "downed";
        public const string RxOgreLost = "ogre_lost";
        public const string RxTagF1 = "tag.f1";
        /// <summary>둘째·셋째 명패를 맡김(묶음 3 나-8, 춘삼 반응 한 줄).</summary>
        public const string RxTag2 = "tag.2";
        public const string RxTag3 = "tag.3";
        /// <summary>반응 힌트 키 꼬리('rx:{반응}.hint'). 머리말은 rx:라 의뢰·이름·장면 지우기(ClearStory)에 함께 지워진다.</summary>
        public const string ReactionHintSuffix = ".hint";

        /// <summary>이정표: 2층 계단 앞 말뚝(받기 전 기록, 4-3 '이정표'). = StairsMilestone(2).</summary>
        public const string MilestoneStairsF2 = "stairs.f2";
        /// <summary>광업소 금고를 열었다(묶음 3 나-10 '광업소 금고' 받기 전 몫 인정).</summary>
        public const string MilestoneSafeOpened = "safe.opened";

        /// <summary>그 층 계단 앞 말뚝 이정표 이름('stairs.f{층}').</summary>
        public static string StairsMilestone(int floor) => "stairs.f" + floor;

        /// <summary>이 파일이 쓰는 모든 머리말(기존 키와 겹치지 않는지 시험이 본다).</summary>
        public static readonly string[] Prefixes = { "q.", NamePrefix, ScenePrefix, MilestonePrefix, ReactionPrefix, RepeatPrefix, "town." };

        /// <summary>이 파일이 쓰는 키인가.</summary>
        public static bool IsTownKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (var p in Prefixes)
                if (key.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        // ── 바탕 ──

        static int Get(CarryData c, string key) => c != null ? c.Count(key) : 0;

        static void Set(CarryData c, string key, int value)
        {
            if (c == null || key == null) return;
            if (value == 0) c.Counters.Remove(key);
            else c.Counters[key] = value;
        }

        static bool Flag(CarryData c, string key) => Get(c, key) != 0;

        static void SetFlag(CarryData c, string key, bool on) => Set(c, key, on ? 1 : 0);

        // ── 의뢰 ──

        /// <summary>의뢰 상태(키가 없으면 Locked).</summary>
        public static QuestState GetQuestState(CarryData c, string questId)
        {
            int v = Get(c, QuestStatePrefix + questId);
            return v >= (int)QuestState.Locked && v <= (int)QuestState.Rewarded ? (QuestState)v : QuestState.Locked;
        }

        public static void SetQuestState(CarryData c, string questId, QuestState state) => Set(c, QuestStatePrefix + questId, (int)state);

        public static int GetQuestStep(CarryData c, string questId) => Get(c, QuestStepPrefix + questId);

        public static void SetQuestStep(CarryData c, string questId, int step) => Set(c, QuestStepPrefix + questId, Math.Max(0, step));

        public static int GetQuestCount(CarryData c, string questId) => Get(c, QuestCountPrefix + questId);

        public static void SetQuestCount(CarryData c, string questId, int count) => Set(c, QuestCountPrefix + questId, Math.Max(0, count));

        /// <summary>받은 차례(1부터, 받지 않았으면 0).</summary>
        public static int GetQuestOrder(CarryData c, string questId) => Get(c, QuestOrderPrefix + questId);

        /// <summary>받은 차례를 새로 매긴다(지금까지 가장 큰 차례 + 1).</summary>
        public static int StampQuestOrder(CarryData c, string questId)
        {
            if (c == null) return 0;
            int max = 0;
            foreach (var kv in c.Counters)
                if (kv.Key.StartsWith(QuestOrderPrefix, StringComparison.Ordinal) && kv.Value > max) max = kv.Value;
            Set(c, QuestOrderPrefix + questId, max + 1);
            return max + 1;
        }

        /// <summary>의뢰 하나를 처음으로(상태·단계·센 수·받은 차례를 지움).</summary>
        public static void ClearQuest(CarryData c, string questId)
        {
            Set(c, QuestStatePrefix + questId, 0);
            Set(c, QuestStepPrefix + questId, 0);
            Set(c, QuestCountPrefix + questId, 0);
            Set(c, QuestOrderPrefix + questId, 0);
        }

        // ── 이름·장면·이정표·반응·반복 ──

        public static bool KnowsName(CarryData c, string npcId) => !string.IsNullOrEmpty(npcId) && Flag(c, NamePrefix + npcId);

        /// <summary>이름 앎을 적는다(되돌리지 않는다). 새로 알았으면 true.</summary>
        public static bool SetNameKnown(CarryData c, string npcId)
        {
            if (c == null || string.IsNullOrEmpty(npcId) || KnowsName(c, npcId)) return false;
            SetFlag(c, NamePrefix + npcId, true);
            return true;
        }

        /// <summary>F1 시험 패널 '모르는 상태로(시험)'만 쓴다. 게임 흐름에서는 이름을 다시 '?'로 되돌리지 않는다(3-2).</summary>
        public static void ForgetNameForTest(CarryData c, string npcId) => SetFlag(c, NamePrefix + npcId, false);

        public static bool SeenScene(CarryData c, string sceneId) => !string.IsNullOrEmpty(sceneId) && Flag(c, ScenePrefix + sceneId);

        public static void MarkSceneSeen(CarryData c, string sceneId)
        {
            if (!string.IsNullOrEmpty(sceneId)) SetFlag(c, ScenePrefix + sceneId, true);
        }

        public static bool HasMilestone(CarryData c, string milestone) => !string.IsNullOrEmpty(milestone) && Flag(c, MilestonePrefix + milestone);

        public static void SetMilestone(CarryData c, string milestone)
        {
            if (!string.IsNullOrEmpty(milestone)) SetFlag(c, MilestonePrefix + milestone, true);
        }

        public static bool HasReaction(CarryData c, string reaction) => !string.IsNullOrEmpty(reaction) && Flag(c, ReactionPrefix + reaction);

        public static void SetReaction(CarryData c, string reaction)
        {
            if (!string.IsNullOrEmpty(reaction)) SetFlag(c, ReactionPrefix + reaction, true);
        }

        /// <summary>반응을 지운다(그 반응의 힌트 'rx:{반응}.hint'도 함께).</summary>
        public static void ClearReaction(CarryData c, string reaction)
        {
            if (string.IsNullOrEmpty(reaction)) return;
            SetFlag(c, ReactionPrefix + reaction, false);
            Set(c, ReactionHintKey(reaction), 0);
        }

        /// <summary>반응 힌트 키('rx:{반응}.hint').</summary>
        public static string ReactionHintKey(string reaction) => ReactionPrefix + reaction + ReactionHintSuffix;

        /// <summary>
        /// 던전에서 쓰러짐(4-3 '반응 표시', CombatEvents.PlayerDowned): rx:downed. 그때 살아 있는 보스가 있었으면 rx:ogre_lost와
        /// 그 판에 가장 많이 맞은 패턴(hint, 전투 문서 3-7)도. 보스 앞 패배마다 힌트를 새로 적는다(None이면 지난 힌트를 지워 두 줄로).
        /// 보스 없이 쓰러지면 남아 있던 rx:ogre_lost와 힌트는 그대로 둔다. hint를 빼면(옛 2인자 호출) None이다.
        /// </summary>
        public static void NoteDowned(CarryData c, bool bossAlive, OgreLossHint hint = default)
        {
            SetReaction(c, RxDowned);
            if (!bossAlive) return;
            SetReaction(c, RxOgreLost);
            Set(c, ReactionHintKey(RxOgreLost), (int)hint);
        }

        /// <summary>
        /// 던전에서 보스를 쓰러뜨림(QuestTracker, CombatEvents.EnemyKilled의 보스): 남아 있던 오우거 패배 반응(rx:ogre_lost)과 힌트를 지운다.
        /// 이긴 뒤 첫 대화가 지난 패배의 공략 줄("…굴러.")로 시작해 보고 장면("그놈을 눕혔다고.")과 어긋나지 않게(마을 문서 5-5).
        /// 쓰러짐 반응(rx:downed)은 그대로 둔다. 처치마다 지운다(첫 처치가 아니어도 지난 패배 줄은 낡은 말이다).
        /// </summary>
        public static void NoteBossKilled(CarryData c) => ClearReaction(c, RxOgreLost);

        /// <summary>남아 있는 오우거 패배 반응의 힌트(없거나 읽지 못한 값이면 None).</summary>
        public static OgreLossHint OgreLossHint(CarryData c)
        {
            int v = Get(c, ReactionHintKey(RxOgreLost));
            return Enum.IsDefined(typeof(OgreLossHint), v) ? (OgreLossHint)v : default;
        }

        /// <summary>오우거가 가장 많이 맞힌 패턴(OgreBrain.MostHitPattern) → 반응 힌트. 돌진·내려찍기만 힌트가 있고 그 밖·null은 None.</summary>
        public static OgreLossHint LossHintFor(BossPattern? mostHit)
        {
            // 이 클래스 안에서 OgreLossHint는 읽기 함수 이름이라 값은 Town.OgreLossHint로 가리킨다.
            switch (mostHit)
            {
                case BossPattern.Charge: return Town.OgreLossHint.Charge;
                case BossPattern.Slam: return Town.OgreLossHint.Slam;
                default: return default;
            }
        }

        /// <summary>반복 대사 다음 차례(0부터 늘어나는 수, 돌림은 쓰는 쪽이 나머지로).</summary>
        public static int RepeatIndex(CarryData c, string npcId) => Get(c, RepeatPrefix + npcId);

        public static void BumpRepeat(CarryData c, string npcId)
        {
            if (c != null && !string.IsNullOrEmpty(npcId)) c.Bump(RepeatPrefix + npcId);
        }

        // ── 마을 세기 ──

        public static int Visits(CarryData c) => Get(c, VisitsKey);

        public static int BumpVisits(CarryData c) => c != null ? c.Bump(VisitsKey) : 0;

        public static int Departures(CarryData c) => Get(c, DeparturesKey);

        /// <summary>권양기 출발 +1(1-3 단계 4).</summary>
        public static int BumpDepartures(CarryData c) => c != null ? c.Bump(DeparturesKey) : 0;

        /// <summary>맡긴 명패 수(= 꺼진 등불 수).</summary>
        public static int Tags(CarryData c) => Get(c, TagsKey);

        public static void SetTags(CarryData c, int tags) => Set(c, TagsKey, Math.Max(0, tags));

        /// <summary>마지막으로 처리한 도착(그 도착의 원정 번호, 0이면 없음). TownArrivalRules가 같은 도착을 두 번 세지 않게 본다.</summary>
        public static int LastArrival(CarryData c) => Get(c, ArrivedKey);

        public static void SetLastArrival(CarryData c, int expedition) => Set(c, ArrivedKey, Math.Max(0, expedition));

        // ── 지우기(F1 시험) ──

        /// <summary>의뢰·이름·장면·이정표·반응·반복 차례를 모두 지운다(F1 '의뢰·이름·장면 지우기'). 마을 세기(town.*)는 남긴다.</summary>
        public static void ClearStory(CarryData c)
        {
            if (c == null) return;
            var keys = new List<string>();
            foreach (var k in c.Counters.Keys)
                if (IsTownKey(k) && !k.StartsWith("town.", StringComparison.Ordinal)) keys.Add(k);
            foreach (var k in keys) c.Counters.Remove(k);
        }

        /// <summary>이 파일이 쓰는 키를 모두 지운다.</summary>
        public static void ClearAll(CarryData c)
        {
            if (c == null) return;
            var keys = new List<string>();
            foreach (var k in c.Counters.Keys)
                if (IsTownKey(k)) keys.Add(k);
            foreach (var k in keys) c.Counters.Remove(k);
        }
    }
}
