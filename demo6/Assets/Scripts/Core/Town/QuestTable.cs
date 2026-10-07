using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>
    /// 첫 판 의뢰 표(기획/마을-의뢰-첫판.md 4-1). 다섯 개 합: 강화석 22, 골드 220, 경험치 272(레벨 2 누계 340 아래).
    /// 오우거 의뢰(q.trainer_ogre)는 던전 '오우거 굴'과 함께 연다(12장 결정 1 A): 2층 계단 아래 굴(OgreDen)이 있어 지금 던전(QuestContext.Live)에서는
    /// 궁수 의뢰 지급 뒤 열린다. 굴 없음 가정(default(QuestContext))이면 잠긴 채라 첫 판 의뢰는 4개다.
    /// 받기 전 기록 인정(CountsBeforeAccept)은 '버팀목 길 끝까지'(2층 계단 앞 말뚝 이정표)와 '굴의 큰 놈'(꾸러미 오우거 처치 수 1 이상) 둘이다.
    /// 첫 의뢰 '갱도로 내려가기'는 아무 층에 내려섰다가 바구니로 올라오면 끝(12장 결정 2 A).
    /// 목표 수는 실제 배치(FloorRecipe)에 맞춰 줄였다: 1층 굴쥐 11, 2층 궁수 4(목표 3, 검토 1차 Q8)(4-1 '목표 수를 원안보다 줄인 까닭').
    /// 제목·HUD·알림 글에 주민 이름을 쓰지 않는다(3-2).
    /// </summary>
    public static class QuestTable
    {
        public const string GateDescend = "q.gate_descend";
        public const string SmithRats = "q.smith_rats";
        public const string GateFloor2 = "q.gate_floor2";
        public const string TrainerWindblade = "q.trainer_windblade";
        public const string TrainerOgre = "q.trainer_ogre";
        // 묶음 3 나-10(시스템-컨텐츠-다듬기-검토-1차.md): 좋은 조작을 가르치는 의뢰와 탐험 의뢰.
        public const string TrainerSlam = "q.trainer_slam";
        public const string TrainerPillar = "q.trainer_pillar";
        public const string GateSurvey = "q.gate_survey";
        public const string SmithOre = "q.smith_ore";
        public const string SmithSafe = "q.smith_safe";

        /// <summary>
        /// 던전에 오우거 굴이 있는가(QuestContext.Live). 2층 계단 아래 '오우거 굴'(OgreDen.InDungeon = FloorRecipe 2층 DenBelow)이라 true다.
        /// 굴을 빼면 false가 되어 아직 잠긴 오우거 의뢰는 열리지 않는다(이미 열린 의뢰는 그대로, Refresh는 Locked → Offered만).
        /// </summary>
        public static bool OgreDenInDungeon => OgreDen.InDungeon;

        /// <summary>오프닝 장면이 주는 의뢰(5-1 끝).</summary>
        public static readonly string[] OpeningQuests = { GateDescend, SmithRats };

        static readonly QuestDef[] Table =
        {
            new QuestDef
            {
                Id = GateDescend,
                Title = "갱도로 내려가기",
                GiverNpcId = NpcTable.Gate,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.EnterFloor, MinFloor = 1, Hud = "갱도로 내려가기 — 권양기 바구니" },
                    new QuestGoal { Kind = GoalKind.Ascend, Hud = "말뚝에서 바구니로 올라오기" },
                },
                BaseFloor = 1,
                SizeMul = QuestRewards.Small,
            },
            new QuestDef
            {
                Id = SmithRats,
                Title = "굴쥐 쫓기",
                GiverNpcId = NpcTable.Smith,
                Steps = new[]
                {
                    new QuestGoal
                    {
                        // 처음엔 스킬이 없다(기획/스킬-자원-트리-1차.md 2장, 2026-10-07): 첫 의뢰는 어떤 공격으로 잡아도 센다(Skill = None).
                        Kind = GoalKind.KillWithSkill, Target = 8, Monster = MonsterKind.Rat, Skill = KillSource.None,
                        Hud = "굴쥐 {n}/{t}", Notice = "굴쥐 {n}/{t}",
                    },
                },
                BaseFloor = 1,
                SizeMul = QuestRewards.Small,
            },
            new QuestDef
            {
                Id = GateFloor2,
                Title = "버팀목 길 끝까지",
                GiverNpcId = NpcTable.Gate,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.LightStairsStake, MinFloor = 2, Hud = "2층 끝 말뚝에 불 켜기", Notice = "2층 끝 말뚝에 불을 켰다 — 의뢰 끝" },
                },
                BaseFloor = 2,
                SizeMul = QuestRewards.Small,
                Requires = new[] { GateDescend },
                CountsBeforeAccept = true,
                Milestone = TownSave.MilestoneStairsF2,
            },
            new QuestDef
            {
                Id = TrainerWindblade,
                Title = "궁수 떨구기",
                GiverNpcId = NpcTable.Trainer,
                Steps = new[]
                {
                    new QuestGoal
                    {
                        Kind = GoalKind.KillWithSkill, Target = 3, Monster = MonsterKind.Archer, Skill = KillSource.SwordWave,
                        Hud = "검풍으로 궁수 {n}/{t}", Notice = "검풍으로 궁수 {n}/{t}",
                    },
                },
                BaseFloor = 2,
                SizeMul = QuestRewards.Small,
                Requires = new[] { GateDescend },
            },
            new QuestDef
            {
                Id = TrainerOgre,
                Title = "굴의 큰 놈",
                GiverNpcId = NpcTable.Trainer,
                Steps = new[]
                {
                    // 처치 사건(CombatEvents.EnemyKilled, 출처 없음)에서만 센다(QuestTracker). 굴 오우거는 NoReward가 아니라 세고, 전투 시험장 오우거는
                    // NoReward라 세지 않는다. 굴 오우거에 NoReward를 켜면 이 의뢰가 끝나지 않는다. 보스 보상(BossLoot)은 이 의뢰 보상과 따로다(4-2).
                    new QuestGoal { Kind = GoalKind.KillBoss, Monster = MonsterKind.Ogre, Hud = "갱도 오우거 쓰러뜨리기" },
                },
                BaseFloor = 2,
                SizeMul = QuestRewards.Medium,
                Requires = new[] { TrainerWindblade },
                NeedsOgreDen = true,
                // 이미 한 일 인정(시스템-컨텐츠-다듬기-검토-1차.md Q2): 2층 계단은 처음부터 굴로 이어져 의뢰보다 먼저 잡을 수 있다.
                // 꾸러미의 오우거 처치 수(BossLedger.Kills)가 1 이상이면 받는 자리에서 달성하고 '이미 잡음' 장면이 받기와 보고를 한 번에 한다.
                // 의뢰 보상(보고 한 번)과 오우거 첫 처치 보상(BossLoot, 처치 수 0 → 1 한 번)은 따로 한 번씩이다.
                CountsBeforeAccept = true,
                BossId = OgreDen.BossId,
            },
            new QuestDef
            {
                // 돌충이를 벽에 박는 손맛을 가르친다(묶음 3 나-10 '뿔을 벽에'). 처치 조건 없이 벽 박기 수만 센다.
                Id = TrainerSlam,
                Title = "뿔을 벽에",
                GiverNpcId = NpcTable.Trainer,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.WallSlam, Target = 3, Monster = MonsterKind.Boar, Hud = "돌충이를 벽에 박기 {n}/{t}", Notice = "돌충이를 벽에 박았다 {n}/{t}" },
                },
                BaseFloor = 1,
                SizeMul = QuestRewards.Small,
                // 의뢰 다섯을 끝낸 뒤(굴의 큰 놈 지급)에 열린다 — '다 끝내면 할 일이 없다'는 빈자리를 채우고 첫 판 동시 진행 상한(4)을 지킨다.
                Requires = new[] { TrainerOgre },
            },
            new QuestDef
            {
                // 오우거에게 한 번 진 뒤에 열린다(묶음 3 나-10 '기둥 앞에 서라'). 마지막 돌진을 기둥·벽에 박아 무너뜨리면 끝(벽에 박아도 같은 무너짐).
                Id = TrainerPillar,
                Title = "기둥 앞에 서라",
                GiverNpcId = NpcTable.Trainer,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.BossPillarBreak, Monster = MonsterKind.Ogre, Hud = "오우거 돌진을 기둥에 박기", Notice = "오우거가 기둥에 박혀 무너졌다" },
                },
                BaseFloor = 2,
                SizeMul = QuestRewards.Small,
                Requires = new[] { TrainerWindblade },
                // '굴의 큰 놈'을 받은 채 그놈에게 한 번 진 뒤에만 열린다(눕히는 길을 가르치는 의뢰라 지급 뒤에는 열지 않음).
                RequiresInProgress = new[] { TrainerOgre },
                NeedsOgreDen = true,
                NeedsLossTo = OgreDen.BossId,
            },
            new QuestDef
            {
                // 측량 합계(꾸러미, 받기 전 몫도 인정). 한 층 3장, 두 층 6장 가운데 3장.
                Id = GateSurvey,
                Title = "도면 다시 그리기",
                GiverNpcId = NpcTable.Gate,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.SurveyTotal, Target = 3, Hud = "측량 {n}/{t}장" },
                },
                BaseFloor = 1,
                SizeMul = QuestRewards.Small,
                Requires = new[] { GateFloor2, TrainerOgre },
            },
            new QuestDef
            {
                // 광맥 2곳(곡괭이가 있어야 캔다).
                Id = SmithOre,
                Title = "쇠 냄새",
                GiverNpcId = NpcTable.Smith,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.MineOre, Target = 2, Hud = "광맥 캐기 {n}/{t}", Notice = "광맥 {n}/{t}" },
                },
                BaseFloor = 1,
                SizeMul = QuestRewards.Small,
                Requires = new[] { SmithRats, TrainerOgre },
            },
            new QuestDef
            {
                // 광업소 사무실 열쇠(오우거 유해 곁, 결정 D3 '나')가 생긴 뒤에 열린다. 받기 전에 열었으면 받는 자리에서 달성(금고는 한 번만 열린다).
                Id = SmithSafe,
                Title = "광업소 금고",
                GiverNpcId = NpcTable.Smith,
                Steps = new[]
                {
                    new QuestGoal { Kind = GoalKind.OpenSafe, Hud = "1층 광업소 금고 열기", Notice = "광업소 금고를 열었다 — 의뢰 끝" },
                },
                BaseFloor = 1,
                SizeMul = QuestRewards.Small,
                // 옥금 차례: '쇠 냄새' 다음(동시 진행 상한 4). 열쇠가 있어야 열린다.
                Requires = new[] { SmithOre },
                NeedsKey = true,
                CountsBeforeAccept = true,
                Milestone = TownSave.MilestoneSafeOpened,
            },
        };

        /// <summary>모든 의뢰(표 차례).</summary>
        public static IReadOnlyList<QuestDef> All => Table;

        /// <summary>이 id의 의뢰(없으면 null).</summary>
        public static QuestDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var q in Table)
                if (string.Equals(q.Id, id, StringComparison.Ordinal)) return q;
            return null;
        }

        public static bool Exists(string id) => Get(id) != null;

        /// <summary>표 차례(없으면 −1). 같은 차례일 때 정렬 기준.</summary>
        public static int IndexOf(string id)
        {
            for (int i = 0; i < Table.Length; i++)
                if (string.Equals(Table[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>이 주민이 주는 의뢰(표 차례).</summary>
        public static List<QuestDef> ByGiver(string npcId)
        {
            var list = new List<QuestDef>();
            foreach (var q in Table)
                if (string.Equals(q.GiverNpcId, npcId, StringComparison.Ordinal)) list.Add(q);
            return list;
        }
    }
}
