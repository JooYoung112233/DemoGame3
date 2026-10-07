using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Progression;

namespace Demo6.Core.Town
{
    /// <summary>
    /// 의뢰 상태(기획/마을-의뢰-첫판.md 4-4). 꾸러미 Counters에 정수로 담는다(TownSave 'q.st:{id}').
    /// Locked → Offered(주민 '!') → Active(받음) → Achieved(달성, 주민 '…') → Rewarded(보고해 받음, 끝).
    /// </summary>
    public enum QuestState
    {
        Locked = 0,
        Offered = 1,
        Active = 2,
        Achieved = 3,
        Rewarded = 4,
    }

    /// <summary>의뢰 한 단계의 목표 종류(4-3 표).</summary>
    public enum GoalKind
    {
        /// <summary>층 들어섬(최소 층 이상). 시험 패널 다시 짓기는 세지 않는다.</summary>
        EnterFloor,
        /// <summary>말뚝에서 바구니로 올라감.</summary>
        Ascend,
        /// <summary>그 스킬로(또는 도움 2초) 그 종류를 처치. 보상 없는 적은 빼고, 받은 뒤부터 센다.</summary>
        KillWithSkill,
        /// <summary>보스 처치(처치 사건에서만 센다).</summary>
        KillBoss,
        /// <summary>그 층 계단 앞 말뚝에 불 켜기.</summary>
        LightStairsStake,
        /// <summary>그 종류 적을 벽에 박기(CombatEvents.EnemyWallSlam, 받은 뒤부터, 묶음 3 나-10).</summary>
        WallSlam,
        /// <summary>보스 돌진을 기둥·벽에 박아 무너뜨리기(오우거 '기둥' 무너짐, 묶음 3 나-10).</summary>
        BossPillarBreak,
        /// <summary>광맥 캐기(DungeonEvents.Discovered 광맥, 받은 뒤부터).</summary>
        MineOre,
        /// <summary>광업소 금고 열기(DungeonEvents.Discovered 금고).</summary>
        OpenSafe,
        /// <summary>받은 측량 장 합계(꾸러미 SurveySheets — 사건이 아니라 상태로 센다, 받기 전 몫도 인정).</summary>
        SurveyTotal,
    }

    /// <summary>
    /// 처치 출처(4-4 가안 + Other). None = 출처를 따지지 않는 처치 사건(CombatEvents.EnemyKilled, 보스 목표만 셈).
    /// Whirlwind·SwordWave·Other = 피해 사건(CombatEvents.PlayerDealtDamage)의 처치 타격 출처. Other는 평타·전설 효과 등 스킬이 아닌 피해.
    /// 스킬 목표는 피해 사건에서만, 보스 목표는 처치 사건에서만 세서 한 번 처치가 같은 목표를 두 번 채우지 않는다(11장 위험 7).
    /// </summary>
    public enum KillSource
    {
        None,
        Whirlwind,
        SwordWave,
        Other,
    }

    /// <summary>
    /// 의뢰 한 단계(4-4 QuestGoal). Hud = 목표 HUD 진행 중 글('{n}' 센 수, '{t}' 목표), Notice = 던전 진행 알림(없으면 null).
    /// 글에 주민 이름을 쓰지 않는다(3-2). LightStairsStake는 MinFloor 층과 같은 층의 계단 앞 말뚝만 센다.
    /// </summary>
    public sealed class QuestGoal
    {
        public GoalKind Kind;
        public int Target = 1;
        public int MinFloor = 1;
        public MonsterKind Monster;
        public KillSource Skill;
        public string Hud = "";
        public string Notice;

        /// <summary>센 수를 넣은 HUD 글.</summary>
        public string HudText(int count) => Format(Hud, count);

        /// <summary>센 수를 넣은 진행 알림(없으면 null).</summary>
        public string NoticeText(int count) => Notice == null ? null : Format(Notice, count);

        string Format(string template, int count) =>
            (template ?? "").Replace("{n}", Math.Max(0, Math.Min(Target, count)).ToString()).Replace("{t}", Target.ToString());

        /// <summary>센 수가 HUD·목록에 보이는 목표인가(목표 2 이상).</summary>
        public bool Counted => Target > 1;
    }

    /// <summary>
    /// 의뢰 정의(4-1 표, 4-4 QuestDef). 보고 대상은 주는 사람(GiverNpcId)이다.
    /// Requires = 모두 Rewarded여야 열리는 선행 의뢰. NeedsOgreDen = 던전에 오우거 굴이 있어야 열림(QuestContext.OgreDenReady).
    /// CountsBeforeAccept = 받기 전 기록(Milestone 'ms:{이정표}', 또는 BossId 보스의 꾸러미 처치 수 1 이상)을 인정해 받는 자리에서 Achieved
    /// (QuestBook.DoneBeforeAccept, 시스템-컨텐츠-다듬기-검토-1차.md Q2).
    /// </summary>
    public sealed class QuestDef
    {
        public string Id = "";
        public string Title = "";
        public string GiverNpcId = "";
        public QuestGoal[] Steps = Array.Empty<QuestGoal>();
        /// <summary>보상 기준 층과 크기 배수(QuestRewards.Small·Medium·Large).</summary>
        public int BaseFloor = 1;
        public int SizeMul = QuestRewards.Small;
        public string[] Requires = Array.Empty<string>();
        public bool NeedsOgreDen;
        /// <summary>광업소 사무실 열쇠가 있어야 열림(꾸러미 HasKey, 묶음 3 나-9·10).</summary>
        public bool NeedsKey;
        /// <summary>
        /// 이 보스에게 한 번 이상 져야 열림(BossLedger.Losses, 묶음 3 나-10 '기둥 앞에 서라'). 없으면 null.
        /// BossId와 따로 둔다(BossId는 받기 장면 경우·보스방 줄 덧글이 쓰는 '보스 의뢰' 표시).
        /// </summary>
        public string NeedsLossTo;
        /// <summary>이 의뢰들을 받은 채(진행 중·달성, 아직 지급 전)여야 열림. 지급까지 끝났으면 열지 않는다.</summary>
        public string[] RequiresInProgress = Array.Empty<string>();
        public bool CountsBeforeAccept;
        /// <summary>받기 전 기록 이정표 이름(TownSave 'ms:' 뒤). CountsBeforeAccept일 때만 쓴다.</summary>
        public string Milestone;
        /// <summary>
        /// 이 의뢰가 가리키는 보스(꾸러미 보스 기록 BossLedger 열쇠, 예: OgreDen.BossId). 없으면 null.
        /// CountsBeforeAccept면 그 보스 처치 수(BossLedger.Kills) 1 이상을 받기 전 기록으로 인정하고('이미 잡음'),
        /// 받기 장면 고르기에서 그 보스에게 진 적(남은 패배 반응 또는 BossLedger.Losses)을 '진 적 있음'으로 본다(검토 1차 Q2).
        /// </summary>
        public string BossId;

        /// <summary>보상(4-2 식).</summary>
        public QuestReward Reward => QuestRewards.For(BaseFloor, SizeMul);

        /// <summary>보상 크기 글(작음·중간·큼).</summary>
        public string SizeLabel => QuestRewards.SizeLabel(SizeMul);

        public int StepCount => Steps.Length;

        /// <summary>단계 목표(범위 밖이면 마지막 단계, 단계가 없으면 null).</summary>
        public QuestGoal Goal(int step) => Steps.Length == 0 ? null : Steps[Math.Max(0, Math.Min(Steps.Length - 1, step))];

        public override string ToString() => Id + " " + Title;
    }

    /// <summary>
    /// 의뢰 보상(강화석·골드·경험치·의뢰 상자). 표의 보상은 레벨 칸이 0이고, QuestBook.Report가 돌려준 값에는 지급 전후 레벨과 얻은 스킬 점수가 들어 있다.
    /// </summary>
    public readonly struct QuestReward
    {
        public readonly int Stones;
        public readonly int Gold;
        public readonly int Xp;
        public readonly int Chests;
        /// <summary>지급 결과만: 지급 전후 레벨, 얻은 스킬 점수.</summary>
        public readonly int LevelBefore;
        public readonly int LevelAfter;
        public readonly int SkillPointsGained;

        public QuestReward(int stones, int gold, int xp, int chests = 0, int levelBefore = 0, int levelAfter = 0, int skillPointsGained = 0)
        {
            Stones = stones;
            Gold = gold;
            Xp = xp;
            Chests = chests;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
            SkillPointsGained = skillPointsGained;
        }

        public bool LeveledUp => LevelAfter > LevelBefore;

        /// <summary>"강화석 3 · 골드 30 · 경험치 40"(5-9).</summary>
        public string AmountText => $"강화석 {Stones} · 골드 {Gold} · 경험치 {Xp}";

        public QuestReward WithLevels(int levelBefore, int levelAfter, int skillPointsGained) =>
            new QuestReward(Stones, Gold, Xp, Chests, levelBefore, levelAfter, skillPointsGained);

        public override string ToString() => AmountText;
    }

    /// <summary>
    /// 의뢰 보상 식(4-2, 2차 10-5): 기준값 C = 2 + 기준 층. 작음 = C × 1, 중간 = C × 2, 큼 = C × 3 + 의뢰 상자 1개. 골드 = 강화석 × 10.
    /// 경험치 = 4U(기준 층) × 크기 배수(작음 1, 중간 2, 큼 3). 의뢰 하나의 강화석은 1~4층에서 11석 이하.
    /// </summary>
    public static class QuestRewards
    {
        public const int Small = 1;
        public const int Medium = 2;
        public const int Large = 3;
        /// <summary>1~4층 의뢰 하나의 강화석 상한(2차 10-5).</summary>
        public const int MaxStonesEarly = 11;
        public const int GoldPerStone = 10;
        public const int XpUnits = 4;

        public static int BaseValue(int baseFloor) => 2 + Math.Max(1, baseFloor);

        public static QuestReward For(int baseFloor, int sizeMul)
        {
            int size = Math.Max(Small, Math.Min(Large, sizeMul));
            int c = BaseValue(baseFloor);
            int stones = c * size;
            int chests = size >= Large ? 1 : 0;
            int xp = XpUnits * XpRules.Unit(baseFloor) * size;
            return new QuestReward(stones, stones * GoldPerStone, xp, chests);
        }

        public static string SizeLabel(int sizeMul)
        {
            if (sizeMul >= Large) return "큼";
            return sizeMul == Medium ? "중간" : "작음";
        }

        /// <summary>
        /// 보상을 꾸러미에 넣는다(4-2). 경험치를 더한 뒤 레벨과 스킬 점수를 PlayerProgress.AddXp처럼 같이 셈한다
        /// (PlayerProgress.ImportFrom은 스킬 점수를 꾸러미 값 그대로 쓰므로 여기서 올려 둔다). 돌려준 값에 지급 전후 레벨이 들어 있다.
        /// </summary>
        public static QuestReward Grant(CarryData carry, QuestReward r)
        {
            if (carry == null) return r;
            int before = carry.Level;
            carry.Stones += r.Stones;
            carry.Gold += r.Gold;
            carry.TotalXp += r.Xp;
            int lv = LevelTable.LevelFor(carry.TotalXp);
            int gained = 0;
            if (lv > carry.Level)
            {
                gained = lv - carry.Level;
                carry.SkillPoints += gained;
                carry.Level = lv;
            }
            return r.WithLevels(before, carry.Level, gained);
        }
    }

    /// <summary>던전 사건 종류(4-4).</summary>
    public enum QuestEventKind
    {
        FloorEntered,
        Ascended,
        Killed,
        StakeLit,
        /// <summary>적을 벽에 박음(Monster).</summary>
        WallSlam,
        /// <summary>보스가 기둥·벽에 박혀 무너짐(Monster = 보스 종류).</summary>
        PillarBreak,
        /// <summary>한 번 받는 것·광맥·금고 등을 찾음(Found).</summary>
        Found,
    }

    /// <summary>
    /// 던전 사건 하나(4-3·4-4). QuestTracker(Game)가 DungeonEvents·CombatEvents를 이 모양으로 바꿔 QuestBook.Handle에 넣는다.
    /// 처치: 피해 사건이면 Source = 처치 타격 출처(Whirlwind·SwordWave·Other), Assist = 2초 안에 마지막으로 맞힌 스킬(SkillAssist).
    /// 처치 사건(EnemyKilled)이면 Source = None. 허수아비·보상 없는 쥐는 NoReward = true로 보내거나 보내지 않는다.
    /// </summary>
    public readonly struct QuestEvent
    {
        public readonly QuestEventKind Kind;
        public readonly int Floor;
        public readonly MonsterKind Monster;
        public readonly KillSource Source;
        public readonly KillSource Assist;
        public readonly bool NoReward;
        public readonly bool Boss;
        public readonly bool StairsFront;
        /// <summary>층 들어섬이 시험 패널 다시 짓기(ArrivalKind.Rebuild)였다(세지 않음).</summary>
        public readonly bool Rebuild;
        /// <summary>Found 사건의 찾은 것 종류.</summary>
        public readonly DiscoveryKind Discovery;

        QuestEvent(QuestEventKind kind, int floor, MonsterKind monster, KillSource source, KillSource assist, bool noReward, bool boss, bool stairsFront, bool rebuild,
            DiscoveryKind discovery = DiscoveryKind.NewCell)
        {
            Discovery = discovery;
            Kind = kind;
            Floor = floor;
            Monster = monster;
            Source = source;
            Assist = assist;
            NoReward = noReward;
            Boss = boss;
            StairsFront = stairsFront;
            Rebuild = rebuild;
        }

        /// <summary>DungeonEvents.FloorEntered(층, 첫 방문, 도착). rebuild = 도착이 Rebuild.</summary>
        public static QuestEvent FloorEntered(int floor, bool rebuild = false) =>
            new QuestEvent(QuestEventKind.FloorEntered, floor, default, KillSource.None, KillSource.None, false, false, false, rebuild);

        /// <summary>DungeonEvents.ExpeditionEnding(바구니로 올라감).</summary>
        public static QuestEvent Ascended(int floor = 0) =>
            new QuestEvent(QuestEventKind.Ascended, floor, default, KillSource.None, KillSource.None, false, false, false, false);

        /// <summary>처치. 피해 사건이면 source = 처치 타격 출처, assist = SkillAssist.AssistFor. 처치 사건이면 source = None.</summary>
        public static QuestEvent Killed(MonsterKind monster, KillSource source, bool noReward = false, bool boss = false, KillSource assist = KillSource.None, int floor = 0) =>
            new QuestEvent(QuestEventKind.Killed, floor, monster, source, assist, noReward, boss, false, false);

        /// <summary>새 사건 DungeonEvents.StakeLit(층, 계단 앞인가).</summary>
        public static QuestEvent StakeLit(int floor, bool stairsFront) =>
            new QuestEvent(QuestEventKind.StakeLit, floor, default, KillSource.None, KillSource.None, false, false, stairsFront, false);

        /// <summary>CombatEvents.EnemyWallSlam(그 적 종류).</summary>
        public static QuestEvent WallSlammed(MonsterKind monster, int floor = 0) =>
            new QuestEvent(QuestEventKind.WallSlam, floor, monster, KillSource.None, KillSource.None, false, false, false, false);

        /// <summary>보스가 기둥·벽에 박혀 무너짐(OgreBrain.PillarBroken).</summary>
        public static QuestEvent PillarBroken(MonsterKind boss, int floor = 0) =>
            new QuestEvent(QuestEventKind.PillarBreak, floor, boss, KillSource.None, KillSource.None, false, true, false, false);

        /// <summary>DungeonEvents.Discovered(찾은 것 종류).</summary>
        public static QuestEvent Found(DiscoveryKind kind, int floor = 0) =>
            new QuestEvent(QuestEventKind.Found, floor, default, KillSource.None, KillSource.None, false, false, false, false, kind);

        public override string ToString() => $"{Kind} f{Floor} {Monster} {Source}/{Assist}{(NoReward ? " 보상없음" : "")}{(Boss ? " 보스" : "")}{(StairsFront ? " 계단앞" : "")}";
    }

    /// <summary>
    /// 도움 2초(4-3): 처치 타격이 그 스킬이 아니어도 죽기 2.0초 안에 그 스킬에 맞았으면 인정한다.
    /// 적 → (마지막으로 맞힌 스킬, 시각). 적 열쇠는 Game의 인스턴스 id 같은 정수. 장면이 사라질 때 Clear.
    /// </summary>
    public sealed class SkillAssist
    {
        public const double Window = 2.0;

        readonly Dictionary<int, (KillSource Skill, double Time)> _last = new Dictionary<int, (KillSource, double)>();

        public int Count => _last.Count;

        /// <summary>적이 맞음. 스킬(회오리·검풍)만 적는다.</summary>
        public void Hit(int enemyId, KillSource source, double time)
        {
            if (source != KillSource.Whirlwind && source != KillSource.SwordWave) return;
            _last[enemyId] = (source, time);
        }

        /// <summary>이 적이 time에 죽을 때 도움으로 인정할 스킬(2초 밖이거나 없으면 None).</summary>
        public KillSource AssistFor(int enemyId, double time)
        {
            if (!_last.TryGetValue(enemyId, out var hit)) return KillSource.None;
            double dt = time - hit.Time;
            return dt >= 0 && dt <= Window ? hit.Skill : KillSource.None;
        }

        public void Forget(int enemyId) => _last.Remove(enemyId);

        public void Clear() => _last.Clear();
    }

    /// <summary>
    /// 의뢰를 다시 계산할 때의 바깥 조건(QuestBook.Refresh). OgreDenReady = 던전에 오우거 굴이 있음.
    /// default(QuestContext)는 '굴 없음 가정'이다(오우거 의뢰가 잠긴 채, 시험이 쓰는 값). 게임은 Live(지금 던전 상태, 2층 계단 아래 굴이 있어 true)
    /// 또는 TownRoot.QuestCtx(Live 또는 F1 '오우거 굴 있음 가정')를 넘긴다. QuestBook.Refresh·TalkDirector.Build·End·Skip·TownArrivalRules.Apply에
    /// ctx를 빠뜨리면 굴 없음으로 계산하니 게임 코드는 늘 넘긴다. F1 가정은 new QuestContext(true).
    /// </summary>
    public readonly struct QuestContext
    {
        public readonly bool OgreDenReady;

        public QuestContext(bool ogreDenReady) => OgreDenReady = ogreDenReady;

        /// <summary>지금 던전 상태(QuestTable.OgreDenInDungeon = OgreDen.InDungeon).</summary>
        public static QuestContext Live => new QuestContext(QuestTable.OgreDenInDungeon);
    }

    /// <summary>
    /// 사건 하나가 바꾼 의뢰(QuestBook.Handle 결과). Notice = 던전 알림(DungeonEvents.Say로 띄울 한 줄): 달성이면 완료 알림, 아니면 진행 알림.
    /// 사람을 가리키는 글은 SpeakerIdentity.ReportTarget을 거쳤다.
    /// </summary>
    public sealed class QuestUpdate
    {
        public QuestDef Quest;
        /// <summary>바뀐 뒤 단계·센 수·그 단계 목표.</summary>
        public int Step;
        public int Count;
        public int Target;
        public bool StepAdvanced;
        public bool Achieved;
        public string ProgressNotice;
        public string DoneNotice;

        public string Notice => DoneNotice ?? ProgressNotice;

        public override string ToString() => $"{Quest?.Id} 단계 {Step} {Count}/{Target}{(Achieved ? " 달성" : "")}";
    }

    /// <summary>목표 HUD 모드(6-1): 마을은 보고할 것 먼저, 던전은 진행 중 먼저.</summary>
    public enum QuestHudMode
    {
        Town,
        Dungeon,
    }

    /// <summary>
    /// 머리 위 표시(6-2): Offer '!' = 받을 의뢰, Report '…' = 보고할 의뢰(둘 다면 '…'). '?'는 이름표 전용이라 표시로 쓰지 않는다.
    /// </summary>
    public enum QuestMarker
    {
        None,
        Offer,
        Report,
    }

    public static class QuestMarkers
    {
        public const string OfferGlyph = "!";
        public const string ReportGlyph = "…";

        /// <summary>표시 글자(없으면 빈 글).</summary>
        public static string Glyph(QuestMarker marker)
        {
            switch (marker)
            {
                case QuestMarker.Offer: return OfferGlyph;
                case QuestMarker.Report: return ReportGlyph;
                default: return "";
            }
        }
    }

    /// <summary>의뢰 목록 창 한 줄(6-5). GiverLabel은 SpeakerIdentity.GiverLabel을 거쳤다.</summary>
    public sealed class QuestRow
    {
        public QuestDef Quest;
        public QuestState State;
        /// <summary>받을 수 있음 / 진행 n/목표(또는 진행 중) / 알릴 일.</summary>
        public string Status = "";
        public string SizeLabel = "";
        public QuestReward Reward;
        public string GiverLabel = "";
        /// <summary>
        /// 목표 한 줄(묶음 3 가-3): 진행 중 '지금: 회오리(E)로 굴쥐 3/8', 받을 수 있음 '할 일: …', 알릴 일 '알리기: 무진에게'(이름 공개 규칙). 없으면 빈 글.
        /// </summary>
        public string GoalLine = "";

        /// <summary>창 한 줄: "굴쥐 쫓기 · 진행 3/8 · 작음 · 강화석 3 · 골드 30 · 경험치 40 · 옥금".</summary>
        public string Line() => $"{Quest?.Title} · {Status} · {SizeLabel} · {Reward.AmountText} · {GiverLabel}";
    }
}
