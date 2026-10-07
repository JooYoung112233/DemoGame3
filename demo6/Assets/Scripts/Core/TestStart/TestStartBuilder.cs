using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Progression;
using Demo6.Core.Random;
using Demo6.Core.Town;
// Loot.KillSource(처치 보상 출처)와 이름이 겹친다. 의뢰 처치 사건은 Town 쪽(회오리·검풍·출처 없음)을 쓴다.
using TownKillSource = Demo6.Core.Town.KillSource;

namespace Demo6.Core.TestStart
{
    /// <summary>시험 메뉴가 들어갈 장면.</summary>
    public enum TestStartScene
    {
        Town,
        Dungeon,
        CombatTest,
    }

    /// <summary>
    /// 시험 메뉴 시작 계획(TestStartBuilder.Build 결과). Game의 TestLaunchSession이 이 값으로 꾸러미를 깔고(ProfileCarry.Install)
    /// 정식 장면 전환(SceneTravel.Load + TripPlan 또는 TownArrivalNote)으로 들어간다.
    /// </summary>
    public sealed class TestStartPlan
    {
        /// <summary>쓴 설정(Normalize한 복사본).</summary>
        public TestStartPreset Preset;
        /// <summary>정식 저장 형식 꾸러미(ToText → FromText 왕복이 같음).</summary>
        public CarryData Carry;
        public TestStartScene Scene;
        /// <summary>던전 층(1·2, 굴은 OgreDen.Floor). 마을·전투 시험장은 0.</summary>
        public int Floor;
        /// <summary>오우거 굴 장면(TripPlan.Den).</summary>
        public bool Den;
        /// <summary>굴 안 바로 싸움(TripPlan.DenFight: 보스방 문 안쪽에서 시작, 들어선 뒤 오우거를 깨움).</summary>
        public bool DenFight;
        /// <summary>던전 도착 종류: 원정 1이면 FirstStart, 아니면 Basket(마을 TownRoot.Depart와 같은 규칙).</summary>
        public bool FirstStart;
        /// <summary>시작 층 지도 씨앗(TripPlan.ForcedSeed). 씨앗 고정이고 굴이 아닐 때만, 그 밖은 null.</summary>
        public ulong? ForcedSeed;
        /// <summary>마을: 새 플레이로 들어감(도착 쪽지 없음, 남쪽 길 끝·오프닝). false면 바구니 도착 쪽지(TownArrivalNote Basket, 요약 없음)를 둔다.</summary>
        public bool TownNewPlay;
        /// <summary>전투 시험장 프리셋 이름(CombatTestRoot.Preset)과 층.</summary>
        public string CombatPreset;
        public int CombatFloor = 1;

        /// <summary>
        /// 콘솔 한 줄(예: "시험 시작: 던전 2층 · 오우거 받음 · 레벨 5 · 희귀 한 벌 iLv2 · 도끼 · 무적").
        /// 전투 시험장은 의뢰 단계·레벨·장비 묶음을 쓰지 않으므로 빼고, 켠 전설(시험장 손잡이)을 적는다(예: "시험 시작: 전투 시험장 Boss 2층 · 전설 연쇄 번개 · 도끼").
        /// </summary>
        public string Summary()
        {
            var p = Preset ?? new TestStartPreset().Normalize();
            var parts = new List<string>();
            string where = TestStartBuilder.StartLabel(p.StartAt);
            if (Scene == TestStartScene.CombatTest) where += " " + (CombatPreset ?? p.CombatPreset) + " " + CombatFloor + "층";
            parts.Add(where);
            string legends = LegendNames(p);
            if (Scene == TestStartScene.CombatTest)
            {
                if (legends.Length > 0) parts.Add("전설 " + legends);
            }
            else
            {
                parts.Add(TestStartBuilder.StageLabel(p.EffectiveStage));
                parts.Add("레벨 " + (Carry != null ? Carry.Level : Math.Max(1, p.Level)));
                if (p.GearSet == TestGearSet.Starting) parts.Add(TestStartBuilder.GearSetLabel(p.GearSet));
                else
                {
                    parts.Add(TestStartBuilder.GearSetLabel(p.GearSet) + " iLv" + p.GearLevel);
                    if (legends.Length > 0) parts.Add("전설 " + legends);
                }
            }
            parts.Add(GearBaseTable.Get(p.WeaponId)?.Name ?? p.WeaponId);
            if (p.Invincible) parts.Add("무적");
            if (p.NoEnemies) parts.Add("적 없음");
            if (p.DarknessOff) parts.Add("어둠 끔");
            if (p.VisionOff) parts.Add("시야 끔");
            if (p.FixSeed) parts.Add("씨앗 " + p.Seed);
            if (p.AddStones > 0) parts.Add("강화석 +" + p.AddStones);
            if (p.AddGold > 0) parts.Add("골드 +" + p.AddGold);
            if (p.AddRunes > 0) parts.Add(RuneTable.SuperArmor.Name + " +" + p.AddRunes);
            if (p.StartWeaponRune) parts.Add("무기에 " + RuneTable.SuperArmor.Name);
            return "시험 시작: " + string.Join(" · ", parts);
        }

        /// <summary>켠 전설 효과 이름(LegendaryTable 차례, '·'로 이음). 없으면 빈 글.</summary>
        static string LegendNames(TestStartPreset p)
        {
            var legends = new List<string>();
            foreach (var def in LegendaryTable.All)
                if ((int)def.Effect < p.LegendOn.Length && p.LegendOn[(int)def.Effect]) legends.Add(def.Name);
            return string.Join("·", legends);
        }
    }

    /// <summary>
    /// 시험 메뉴 '시작 상태 만들기'(순수 함수, UnityEngine 없음). 게임 규칙을 바꾸지 않고, 정식 흐름이 만드는 것과 같은 꾸러미를 만든다:
    /// ① 새 프로필(소금 = 씨앗 고정이면 Seed, 아니면 salt) → ② 의뢰 단계(ApplyStage: 정식 함수 TownArrivalRules·TalkDirector·QuestBook·TownNight·BossLedger로
    /// 정식 흐름을 그대로 다시 밟음) → ③ 시작 위치에 필요한 최소 진행(EnsureStartProgress) → ④ 레벨·스킬(ApplyLevel) → ⑤ 장비(BuildGear, 가방은 비움)
    /// → ⑥ 강화석·골드 넣기 → ⑦ 룬(ApplyRunes: 주머니 +n, 시작 무기에 버팀 룬). 의뢰 문맥은 QuestContext.Live(2층 계단 아래 굴이 있어 오우거 의뢰가 열림).
    /// </summary>
    public static class TestStartBuilder
    {
        /// <summary>장비 옵션 굴림 Pcg32Random 흐름 번호.</summary>
        public const ulong GearStream = 0x7E57UL;
        /// <summary>씨앗 고정이 아닐 때 장비 옵션 굴림 씨앗(같은 설정이면 같은 장비).</summary>
        public const ulong DefaultGearSeed = 0x7E57_0001UL;

        /// <summary>계획 만들기(문맥 QuestContext.Live).</summary>
        public static TestStartPlan Build(TestStartPreset preset, ulong salt) => Build(preset, salt, QuestContext.Live);

        /// <summary>
        /// 계획 만들기. preset은 바꾸지 않는다(Normalize한 복사본을 Plan.Preset에 둠). 같은 설정·같은 salt면 같은 꾸러미 글이다.
        /// </summary>
        public static TestStartPlan Build(TestStartPreset preset, ulong salt, QuestContext ctx)
        {
            var p = (preset ?? new TestStartPreset()).Clone().Normalize();
            var stage = p.EffectiveStage;

            // ① 새 프로필 → ② 의뢰 단계 → ③ 시작 위치 최소 진행 → ④ 레벨·스킬
            var carry = CarryData.NewProfile(p.FixSeed ? p.Seed : salt);
            ApplyStage(carry, stage, ctx);
            EnsureStartProgress(carry, p.StartAt, stage);
            ApplyLevel(carry, p.Level, p.AutoSkills);

            // ⑤ 장비(가방은 비움) → ⑥ 강화석·골드 → ⑦ 룬(주머니 +n, 시작 무기에 버팀 룬)
            var gear = BuildGear(p);
            for (int i = 0; i < carry.Equipment.Length && i < gear.Length; i++) carry.Equipment[i] = gear[i];
            carry.Bag.Clear();
            carry.Stones += p.AddStones;
            carry.Gold += p.AddGold;
            ApplyRunes(carry, p);

            var plan = new TestStartPlan
            {
                Preset = p,
                Carry = carry,
                FirstStart = carry.Expedition <= 1,
                CombatPreset = p.CombatPreset,
                CombatFloor = p.CombatFloor,
            };
            switch (p.StartAt)
            {
                case TestStartAt.TownFresh:
                case TestStartAt.Town:
                    plan.Scene = TestStartScene.Town;
                    plan.TownNewPlay = stage == TestQuestStage.Fresh;
                    break;
                case TestStartAt.Floor1:
                    plan.Scene = TestStartScene.Dungeon;
                    plan.Floor = 1;
                    break;
                case TestStartAt.Floor2:
                    plan.Scene = TestStartScene.Dungeon;
                    plan.Floor = 2;
                    break;
                case TestStartAt.DenFront:
                case TestStartAt.DenFight:
                    plan.Scene = TestStartScene.Dungeon;
                    plan.Floor = OgreDen.Floor;
                    plan.Den = true;
                    plan.DenFight = p.StartAt == TestStartAt.DenFight;
                    break;
                default:
                    plan.Scene = TestStartScene.CombatTest;
                    break;
            }
            plan.ForcedSeed = p.FixSeed && plan.Scene == TestStartScene.Dungeon && !plan.Den ? p.Seed : (ulong?)null;
            return plan;
        }

        /// <summary>
        /// 의뢰 단계까지 정식 흐름을 다시 밟는다(새 프로필 꾸러미에 부름, 단계 차례대로 이어 붙임). 차례(계약):
        /// 처음 = 아무것도 안 함.
        /// 첫 귀환 뒤 = TownArrivalRules.Apply(NewPlay) → TalkDirector.Skip(TownScript.Opening, 0) → TownNight.Depart → EnterFloor(1) + Handle(FloorEntered 1)
        ///   → Handle(Ascended 1) → TownNight.AdvanceForAscend → TownArrivalRules.Apply(Basket).
        /// 궁수까지 끝 = (첫 귀환 뒤) → 춘삼 대화(TalkDirector.Build(Gate) 장면마다 Skip: 내려가기 보고 → 버팀목 길 받기) → 무진 대화(궁수 받기)
        ///   → TownNight.Depart → EnterFloor(1) + FloorEntered 1 → 굴쥐 회오리 처치 8 → EnterFloor(2) + FloorEntered 2 → 궁수 검풍 처치 3
        ///   → StakeLit(2, 계단 앞) → Ascended 2 → AdvanceForAscend → TownArrivalRules.Apply(Basket).
        /// 오우거 받음 = (궁수까지 끝) → 춘삼(버팀목 길 보고) → 옥금(굴쥐 보고) → 무진(궁수 보고·이름 공개 → 같은 대화에서 '굴의 큰 놈' 받기). 대화는 Build → 장면마다 Skip.
        /// 오우거 처치 뒤 = (오우거 받음) → TownNight.Depart → EnterFloor(2) + FloorEntered 2 → EnterFloor(2, 굴) + FloorEntered 2
        ///   → BossLedger.LightStake(굴 앞 말뚝) + StakeLit(2, 계단 앞 아님) → BossLedger.RecordKill(ogre, 지금 원정) + Handle(Killed Ogre, 출처 없음, 보스)
        ///   → TownSave.NoteBossKilled → Ascended 2 → AdvanceForAscend → TownArrivalRules.Apply(Basket).
        /// 사건은 모두 new QuestBook(carry).Handle(QuestEvent …), 대화·도착에는 ctx를 넘긴다.
        /// 처치·발견 경험치는 넣지 않는다(보고 보상만, 정식 흐름에서 생기는 그 밖의 경험치는 플레이마다 다름). 그래서 밟은 층의 발견 주머니는 적되
        /// 받은 몫(DiscoveryXpGiven)은 비워 둔다(첫 방문에 아무것도 줍지 않은 것과 같음).
        /// </summary>
        public static void ApplyStage(CarryData carry, TestQuestStage stage, QuestContext ctx)
        {
            if (carry == null || stage <= TestQuestStage.Fresh) return;

            // ── 첫 귀환 뒤: 새 플레이 도착 → 오프닝 → 원정 1(1층 내려섬 → 바구니로 올라옴) → 바구니 도착 ──
            TownArrivalRules.Apply(carry, TownArrivalKind.NewPlay, ctx);
            TalkDirector.Skip(TownScript.Opening, 0, carry, ctx);
            TownNight.Depart(carry);
            Enter(carry, 1);
            Handle(carry, QuestEvent.Ascended(1));
            Ascend(carry, ctx);
            if (stage == TestQuestStage.AfterFirstReturn) return;

            // ── 궁수까지 끝: 춘삼(내려가기 보고 → 버팀목 길 받기) → 무진(궁수 받기) → 원정 2 → 바구니 도착 ──
            Talk(carry, NpcTable.Gate, ctx);
            Talk(carry, NpcTable.Trainer, ctx);
            TownNight.Depart(carry);
            Enter(carry, 1);
            for (int i = 0; i < 8; i++) Handle(carry, QuestEvent.Killed(MonsterKind.Rat, TownKillSource.Whirlwind, floor: 1));
            Enter(carry, 2);
            for (int i = 0; i < 3; i++) Handle(carry, QuestEvent.Killed(MonsterKind.Archer, TownKillSource.SwordWave, floor: 2));
            Handle(carry, QuestEvent.StakeLit(2, true));
            Handle(carry, QuestEvent.Ascended(2));
            Ascend(carry, ctx);
            if (stage == TestQuestStage.ArcherDone) return;

            // ── 오우거 받음: 춘삼(버팀목 길 보고) → 옥금(굴쥐 보고) → 무진(궁수 보고·이름 공개 → '굴의 큰 놈' 받기) ──
            Talk(carry, NpcTable.Gate, ctx);
            Talk(carry, NpcTable.Smith, ctx);
            Talk(carry, NpcTable.Trainer, ctx);
            if (stage == TestQuestStage.OgreAccepted) return;

            // ── 오우거 처치 뒤: 원정 3(2층 → 굴 앞 말뚝 → 오우거 첫 처치) → 바구니 도착 ──
            TownNight.Depart(carry);
            Enter(carry, 2);
            Enter(carry, OgreDen.Floor, true);
            BossLedger.LightStake(carry, OgreDen.FrontStakeId);
            Handle(carry, QuestEvent.StakeLit(OgreDen.Floor, false));
            BossLedger.RecordKill(carry, OgreDen.BossId, carry.Expedition);
            Handle(carry, QuestEvent.Killed(MonsterKind.Ogre, TownKillSource.None, boss: true, floor: OgreDen.Floor));
            TownSave.NoteBossKilled(carry);
            Handle(carry, QuestEvent.Ascended(OgreDen.Floor));
            Ascend(carry, ctx);
        }

        /// <summary>층에 들어섬: 꾸러미 몫(EnterFloor) + 의뢰 사건 FloorEntered(DungeonRoot.BeginPlay → QuestTracker와 같은 차례).</summary>
        static void Enter(CarryData carry, int floor, bool den = false)
        {
            EnterFloor(carry, floor, den);
            Handle(carry, QuestEvent.FloorEntered(floor));
        }

        /// <summary>바구니로 올라옴(올라간 뒤 원정 번호·밤 사건) → 마을 바구니 도착 처리.</summary>
        static void Ascend(CarryData carry, QuestContext ctx)
        {
            TownNight.AdvanceForAscend(carry);
            TownArrivalRules.Apply(carry, TownArrivalKind.Basket, ctx);
        }

        static void Handle(CarryData carry, QuestEvent e) => new QuestBook(carry).Handle(e);

        /// <summary>
        /// 주민에게 한 번 말을 걺: TalkDirector.Build로 장면 목록을 받고 장면마다 Skip(처음 줄부터 = 끝까지 본 것과 같은 효과).
        /// 할 말(반응·보고·받기)이 없어 반복 대사뿐이면 말을 걸지 않는다(반복 차례 rp:가 오르지 않게).
        /// </summary>
        static void Talk(CarryData carry, string npcId, QuestContext ctx)
        {
            var plan = TalkDirector.Build(npcId, carry, ctx);
            foreach (var scene in plan.Scenes)
            {
                if (scene == null || scene.Kind == TalkSceneKind.Repeat) continue;
                TalkDirector.Skip(scene, 0, carry, ctx);
            }
        }

        /// <summary>
        /// 층에 들어섬의 꾸러미 몫(DungeonRoot.BuildMap 정식 갈래 + BeginPlay와 같음): 밟은 층 +floor. 굴이 아니면 그 원정에 장면이 지었을 지도(VisitMap)의
        /// 글자를 LastGlyphs[floor]에 남기고(다음 원정 흔적 비교), 처음 밟는 층이면 그 지도의 발견 주머니(DiscoveryPouchOf)를 DiscoveryPouch[floor]에 적는다
        /// (이미 있으면 그대로). 그 뒤 가장 깊은 층·켠 승강장(+floor)·줄 깊이(max). 주머니가 없으면 다시 밟을 때도 장면이 적지 않아 발견 경험치 상한이 걸리지 않는다.
        /// </summary>
        public static void EnterFloor(CarryData carry, int floor, bool den = false)
        {
            if (carry == null || floor < 1) return;
            if (!den && FloorRecipe.Exists(floor))
            {
                // 장면은 꾸러미 값을 고치기 전(Awake)에 지도를 짓는다: 처음 밟는가·가장 깊은 층은 들어서기 전 값.
                bool first = !carry.VisitedFloors.Contains(floor);
                var gen = VisitMap(carry, floor);
                carry.LastGlyphs[floor] = gen.Glyphs ?? "";
                if (first && !carry.DiscoveryPouch.ContainsKey(floor)) carry.DiscoveryPouch[floor] = DiscoveryPouchOf(gen.Build(), floor);
            }
            carry.VisitedFloors.Add(floor);
            if (den) return;
            if (floor > carry.DeepestFloor) carry.DeepestFloor = floor;
            carry.LitLandings.Add(floor);
            carry.RopeDepth = Math.Max(carry.RopeDepth, floor);
        }

        /// <summary>
        /// 이번 원정에 장면이 이 층에 지을 지도(DungeonRoot.BuildMap의 정식 갈래와 같음, 시험 패널 씨앗·다시 짓기 없음):
        /// 씨앗 = ExpeditionSeeds.Choose(처음 밟는 층이면 고른 씨앗, 아니면 원정 번호 씨앗), 다시 연 층이면 지난 글자(LastGlyphs)와 견줘 GenerateUnlike.
        /// </summary>
        public static GeneratedFloor VisitMap(CarryData carry, int floor)
        {
            var recipe = FloorRecipe.For(floor);
            bool first = carry == null || !carry.VisitedFloors.Contains(floor);
            string prev = null;
            if (!first) carry.LastGlyphs.TryGetValue(floor, out prev);
            var input = new GeneratorInput
            {
                Floor = floor,
                Seed = ExpeditionSeeds.Choose(recipe, first, carry != null ? carry.ProfileSalt : 0UL, carry != null ? carry.Expedition : 1),
                FirstVisit = first,
                DeepestFloor = carry != null ? carry.DeepestFloor : floor,
                HasPickaxe = carry != null && carry.HasPickaxe,
                HasKey = carry != null && carry.HasKey,
                OnceDone = carry != null ? new HashSet<string>(carry.OnceDone) : new HashSet<string>(),
                Night = carry != null ? carry.Night : NightEvent.None,
            };
            return string.IsNullOrEmpty(prev) ? FloorGenerator.Generate(input) : FloorGenerator.GenerateUnlike(input, prev);
        }

        /// <summary>
        /// 지도의 발견 주머니(2-6, PlayerProgress.ComputeDiscoveryPouch와 같은 셈): 칸 수 × 새 칸 경험치 + 장면이 등록하는 주머니 종류 경험치 합
        /// (자리 표시 나무 궤짝·쇠 궤짝·벽 등잔·말뚝 = DungeonContent, 판자벽 문틈 = 숨은 방(PlankWall), 금 간 벽 문틈 = 지름길(CrackedWall)).
        /// 같은 id 자리 표시는 한 번만 센다(DungeonState.Register와 같음).
        /// </summary>
        public static int DiscoveryPouchOf(FloorMap map, int floor)
        {
            if (map == null) return 0;
            int total = map.Cells.Count * XpRules.ForDiscovery(DiscoveryKind.NewCell, floor);
            var seen = new HashSet<string>();
            foreach (var cell in map.Cells)
                foreach (var f in cell.Features)
                {
                    var kind = PouchKindOf(f.Kind);
                    if (kind.HasValue && seen.Add(f.Id ?? "")) total += XpRules.ForDiscovery(kind.Value, floor);
                }
            foreach (var e in map.Edges)
            {
                if (e.Kind == EdgeKind.Plank) total += XpRules.ForDiscovery(DiscoveryKind.HiddenRoom, floor);
                else if (e.Kind == EdgeKind.Cracked) total += XpRules.ForDiscovery(DiscoveryKind.Shortcut, floor);
            }
            return total;
        }

        /// <summary>자리 표시가 장면에서 등록되는 주머니 종류(궤짝·등잔·말뚝). 그 밖(사건·이야기·능력·금고·무리 등)은 null.</summary>
        static DiscoveryKind? PouchKindOf(FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.WoodChest: return DiscoveryKind.WoodChest;
                case FeatureKind.IronChest: return DiscoveryKind.IronChest;
                case FeatureKind.WallLamp: return DiscoveryKind.WallLamp;
                case FeatureKind.Stake: return DiscoveryKind.Stake;
                default: return null;
            }
        }

        /// <summary>
        /// 시작 위치에 필요한 최소 진행(단계 뒤에 부름). 던전 시작(1층·2층·굴 앞·굴 안)이고 단계가 '처음'이 아니면 권양기 출발(TownNight.Depart, 출발 수 +1)을 한 번 적는다
        /// ('처음'은 DungeonTest 바로 Play와 같게 마을 키를 만들지 않음). 2층·굴: 아직 밟지 않았으면 1층에 들어섬(EnterFloor: 밟은 층·지도 글자·발견 주머니·
        /// 켠 승강장·가장 깊은 층), 줄 깊이 ≥ 2. 굴: 2층도 같은 몫, 굴 앞 말뚝 켬(BossLedger.LightStake). 의뢰 사건은 넣지 않는다. 마을·전투 시험장은 아무것도 안 함.
        /// </summary>
        public static void EnsureStartProgress(CarryData carry, TestStartAt at, TestQuestStage stage)
        {
            if (carry == null || !IsDungeonStart(at)) return;
            if (stage != TestQuestStage.Fresh) TownNight.Depart(carry);
            if (at == TestStartAt.Floor1) return;

            if (!carry.VisitedFloors.Contains(1)) EnterFloor(carry, 1);
            carry.RopeDepth = Math.Max(carry.RopeDepth, 2);
            carry.LitLandings.Add(1);
            carry.DeepestFloor = Math.Max(carry.DeepestFloor, 1);
            if (at == TestStartAt.Floor2) return;

            if (!carry.VisitedFloors.Contains(OgreDen.Floor)) EnterFloor(carry, OgreDen.Floor);
            carry.LitLandings.Add(OgreDen.Floor);
            carry.DeepestFloor = Math.Max(carry.DeepestFloor, OgreDen.Floor);
            BossLedger.LightStake(carry, OgreDen.FrontStakeId);
        }

        /// <summary>
        /// 룬(기획/세-무기-우클릭-소켓-1차.md 6장): 주머니에 버팀 룬 AddRunes개(상한 RuneRules.PouchCap), StartWeaponRune이면 낀 무기의 빈 홈에 버팀 룬을 끼운다
        /// (무기 홈은 늘 1칸 이상이라 시작 장비에도 들어감, 이미 끼워져 있으면 그대로). 전투 시험장은 이 꾸러미 무기를 쓰지 않아 Tuning.TestSuperArmorRune으로 대신한다(TestLaunchSession).
        /// </summary>
        public static void ApplyRunes(CarryData carry, TestStartPreset p)
        {
            if (carry == null || p == null) return;
            if (p.AddRunes > 0) carry.AddRunes(RuneTable.SuperArmorId, p.AddRunes);
            if (!p.StartWeaponRune) return;
            var weapon = carry.Equipment[(int)GearSlot.Weapon];
            if (weapon == null || weapon.HasRune(RuneTable.SuperArmorId)) return;
            if (!RuneRules.CanInsert(weapon.Part, weapon.Grade, weapon.Runes, RuneTable.SuperArmorId)) return;
            var runes = new List<string>(weapon.Runes) { RuneTable.SuperArmorId };
            carry.Equipment[(int)GearSlot.Weapon] = weapon.WithRunes(runes);
        }

        static bool IsDungeonStart(TestStartAt at) =>
            at == TestStartAt.Floor1 || at == TestStartAt.Floor2 || at == TestStartAt.DenFront || at == TestStartAt.DenFight;

        /// <summary>
        /// 레벨 넣기. level ≥ 1이면 레벨 = level(1~MaxLevel), 스킬 점수 = level − 1, 랭크 0(SkillTree.Count칸).
        /// 누계 경험치는 단계가 쌓은 값이 그 레벨 구간(Cumulative(level) 이상, 다음 레벨 누계 미만) 안이면 그대로 두고, 아니면 LevelTable.Cumulative(level)
        /// (예: 오우거 받음 176 + 레벨 1 → 176, 레벨 5 → Cumulative(5)). level 0이면 레벨·경험치·점수·랭크를 그대로 둔다. autoSkills면 그 뒤 AutoAllocateSkills.
        /// </summary>
        public static void ApplyLevel(CarryData carry, int level, bool autoSkills)
        {
            if (carry == null) return;
            if (level >= 1)
            {
                int lv = Math.Min(TestStartPreset.MaxLevel, level);
                carry.Level = lv;
                if (!InLevelBand(carry.TotalXp, lv)) carry.TotalXp = LevelTable.Cumulative(lv);
                carry.SkillPoints = lv - 1;
                carry.SkillRanks = new int[SkillTree.Count];
                carry.SkillNodes.Clear();
            }
            if (autoSkills) AutoAllocateSkills(carry);
        }

        /// <summary>누계 경험치가 그 레벨 구간 안인가(Cumulative(level) 이상, 최대 레벨이 아니면 다음 레벨 누계 미만).</summary>
        static bool InLevelBand(int totalXp, int level) =>
            totalXp >= LevelTable.Cumulative(level) && (level >= LevelTable.MaxLevel || totalXp < LevelTable.Cumulative(level + 1));

        /// <summary>
        /// 남은 스킬 점수를 쓴다(SkillTree.SpendForTest): 회오리·검풍을 먼저 배우고(무진 없이), 그다음 랭크 칸을 차례로 한 점씩 돌려 넣는다.
        /// 레벨 2 미만이면 아무것도 배우지 않는다(처음엔 스킬이 없다, 기획/스킬-자원-트리-1차.md).
        /// </summary>
        public static void AutoAllocateSkills(CarryData carry)
        {
            if (carry == null || carry.Level < SkillDef.RequiredLevel || carry.SkillPoints <= 0) return;
            var state = new SkillState();
            state.Load(carry.Level, carry.SkillPoints, carry.SkillRanks, carry.SkillNodes);
            SkillTree.SpendForTest(state);
            carry.SkillPoints = state.Points;
            carry.SkillRanks = (int[])state.Ranks.Clone();
            carry.SkillNodes.Clear();
            carry.SkillNodes.UnionWith(state.Nodes);
        }

        /// <summary>
        /// 장착 8자리(GearSlot 차례, 빈 자리 null). 시작 장비: GearItem.Starting(무기·갑옷·투구·장갑·장화 5칸, 무기는 WithBase(WeaponId)), 반지·목걸이 빔, 전설 무시.
        /// 그 밖: 8칸 모두 — 무기 = WeaponId, 갑옷·투구·장갑·장화 = ArmorWeight 무게(가죽·사슬·판금), 반지 1 = 쇠 반지, 반지 2 = 핏빛 반지, 목걸이 = 이빨 목걸이.
        /// 아이템 레벨 = GearLevel, 굴림 1000‰, 강화 0. 전설 규칙: 전설 한 벌은 자리마다 그 부위에 나올 수 있는 효과(무기·장갑 연쇄 번개, 갑옷·장화 불꽃 발자국,
        /// 투구·반지·목걸이 연쇄 폭발)가 켜져 있으면 전설 등급 + 그 효과, 꺼져 있으면 영웅 등급(효과 없음). 다른 한 벌은 켠 효과마다 대표 자리 하나
        /// (연쇄 번개 = 무기, 불꽃 발자국 = 장화, 연쇄 폭발 = 목걸이)만 전설 등급 + 그 효과. 효과 세기 = LegendRoll.
        /// 옵션 = 자리의 최종 등급으로 OptionTable.RollAll(부위, 등급, GearLevel, rng), rng = Pcg32Random(씨앗 고정이면 Seed 아니면 DefaultGearSeed, GearStream), 자리 차례로.
        /// </summary>
        public static GearItem[] BuildGear(TestStartPreset preset)
        {
            var p = (preset ?? new TestStartPreset()).Clone().Normalize();
            var gear = new GearItem[GearSlots.SlotCount];
            if (p.GearSet == TestGearSet.Starting)
            {
                foreach (var slot in GearSlots.All) gear[(int)slot] = GearItem.Starting(slot);
                gear[(int)GearSlot.Weapon] = gear[(int)GearSlot.Weapon]?.WithBase(p.WeaponId);
                return gear;
            }

            var rng = new Pcg32Random(p.FixSeed ? p.Seed : DefaultGearSeed, GearStream);
            var setGrade = GradeOf(p.GearSet);
            foreach (var slot in GearSlots.All)
            {
                var part = GearSlots.PartOf(slot);
                var effect = LegendFor(p, slot);
                Grade grade;
                if (effect.HasValue) grade = Grade.Legendary;
                else grade = p.GearSet == TestGearSet.Legendary ? Grade.Epic : setGrade;
                var options = OptionTable.RollAll(part, grade, p.GearLevel, rng);
                string legendId = effect.HasValue ? LegendaryTable.Get(effect.Value).Id : null;
                gear[(int)slot] = new GearItem(BaseIdFor(slot, p), grade, p.GearLevel, 1000, 0, options, legendId, effect.HasValue ? p.LegendRoll : 0);
            }
            return gear;
        }

        static Grade GradeOf(TestGearSet set)
        {
            switch (set)
            {
                case TestGearSet.Uncommon: return Grade.Uncommon;
                case TestGearSet.Rare: return Grade.Rare;
                case TestGearSet.Epic: return Grade.Epic;
                case TestGearSet.Legendary: return Grade.Legendary;
                default: return Grade.Common;
            }
        }

        static bool IsLegendOn(TestStartPreset p, LegendaryEffect effect) => (int)effect < p.LegendOn.Length && p.LegendOn[(int)effect];

        /// <summary>그 효과를 한 벌(전설 한 벌이 아닌 것)에 넣을 대표 자리: 연쇄 번개 = 무기, 불꽃 발자국 = 장화, 연쇄 폭발 = 목걸이.</summary>
        static GearSlot RepresentativeSlot(LegendaryEffect effect)
        {
            switch (effect)
            {
                case LegendaryEffect.ChainLightning: return GearSlot.Weapon;
                case LegendaryEffect.FlameSteps: return GearSlot.Boots;
                default: return GearSlot.Amulet;
            }
        }

        /// <summary>이 자리에 넣을 전설 효과(없으면 null).</summary>
        static LegendaryEffect? LegendFor(TestStartPreset p, GearSlot slot)
        {
            if (p.GearSet == TestGearSet.Legendary)
            {
                foreach (var e in LegendaryTable.EffectsOn(GearSlots.PartOf(slot)))
                    if (IsLegendOn(p, e)) return e;
                return null;
            }
            foreach (var def in LegendaryTable.All)
                if (IsLegendOn(p, def.Effect) && RepresentativeSlot(def.Effect) == slot) return def.Effect;
            return null;
        }

        /// <summary>자리의 종류 id: 무기 = 고른 무기, 방어구 = 고른 무게, 반지 1 = 쇠 반지, 반지 2 = 핏빛 반지, 목걸이 = 이빨 목걸이.</summary>
        static string BaseIdFor(GearSlot slot, TestStartPreset p)
        {
            switch (slot)
            {
                case GearSlot.Weapon: return p.WeaponId;
                case GearSlot.Ring1: return GearBaseTable.IronRing;
                case GearSlot.Ring2: return GearBaseTable.BloodRing;
                case GearSlot.Amulet: return GearBaseTable.FangAmulet;
                default:
                    var part = GearSlots.PartOf(slot);
                    foreach (var b in GearBaseTable.ForPart(part))
                        if (b.Weight == p.ArmorWeight) return b.Id;
                    return GearBaseTable.FirstOf(part).Id;
            }
        }

        // ── 화면 글(시험 메뉴는 내부용이라 주민을 '갱도지기(춘삼)'처럼 적어도 된다) ──

        /// <summary>시작 위치 이름(예: '굴 안 바로 싸움').</summary>
        public static string StartLabel(TestStartAt at)
        {
            switch (at)
            {
                case TestStartAt.TownFresh: return "마을 처음";
                case TestStartAt.Town: return "마을";
                case TestStartAt.Floor1: return "던전 1층";
                case TestStartAt.Floor2: return "던전 2층";
                case TestStartAt.DenFront: return "굴 앞 말뚝";
                case TestStartAt.DenFight: return "굴 안 바로 싸움";
                case TestStartAt.CombatTest: return "전투 시험장";
                default: return at.ToString();
            }
        }

        /// <summary>의뢰 단계 이름(예: '궁수까지 끝(보고 전)').</summary>
        public static string StageLabel(TestQuestStage stage)
        {
            switch (stage)
            {
                case TestQuestStage.Fresh: return "처음";
                case TestQuestStage.AfterFirstReturn: return "첫 귀환 뒤";
                case TestQuestStage.ArcherDone: return "궁수까지 끝(보고 전)";
                case TestQuestStage.OgreAccepted: return "오우거 받음";
                case TestQuestStage.OgreKilled: return "오우거 처치 뒤";
                default: return stage.ToString();
            }
        }

        /// <summary>의뢰 단계 설명 한 줄(예: '갱도지기(춘삼)·대장장이(옥금) 이름 앎 · 알릴 일: 갱도로 내려가기').</summary>
        public static string StageNote(TestQuestStage stage)
        {
            switch (stage)
            {
                case TestQuestStage.Fresh: return "새 프로필 · 이름 모두 ?";
                case TestQuestStage.AfterFirstReturn:
                    return Who(NpcTable.Gate) + "·" + Who(NpcTable.Smith) + " 이름 앎 · 알릴 일: " + QuestTitle(QuestTable.GateDescend);
                case TestQuestStage.ArcherDone: return "알릴 일: 버팀목 길·굴쥐·궁수 · " + Who(NpcTable.Trainer) + "은 아직 ?";
                case TestQuestStage.OgreAccepted: return "세 이름 모두 앎 · 진행 중: " + QuestTitle(QuestTable.TrainerOgre);
                case TestQuestStage.OgreKilled: return "오우거 첫 처치 기록 · 알릴 일: " + QuestTitle(QuestTable.TrainerOgre);
                default: return "";
            }
        }

        /// <summary>내부용 주민 표기 '역할(이름)'(예: '갱도지기(춘삼)'). 게임 화면 이름표는 SpeakerIdentity를 거친다.</summary>
        static string Who(string npcId)
        {
            var npc = NpcTable.Get(npcId);
            return npc == null ? npcId : npc.Role + "(" + npc.DisplayName + ")";
        }

        static string QuestTitle(string questId) => QuestTable.Get(questId)?.Title ?? questId;

        /// <summary>장비 묶음 이름(예: '희귀 한 벌').</summary>
        public static string GearSetLabel(TestGearSet set)
        {
            switch (set)
            {
                case TestGearSet.Starting: return "시작 장비";
                case TestGearSet.Common: return "일반 한 벌";
                case TestGearSet.Uncommon: return "고급 한 벌";
                case TestGearSet.Rare: return "희귀 한 벌";
                case TestGearSet.Epic: return "영웅 한 벌";
                case TestGearSet.Legendary: return "전설 한 벌";
                default: return set.ToString();
            }
        }
    }
}
