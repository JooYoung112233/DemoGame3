using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>이번 장면에 어떻게 왔나(층 이름 카드·승강장 도착 글·흔적이 본다).</summary>
    public enum ArrivalKind
    {
        /// <summary>플레이를 새로 시작함(1층 첫 원정).</summary>
        FirstStart,
        /// <summary>바구니로 내려옴(밤을 지나 새 원정, 고른 승강장).</summary>
        Basket,
        /// <summary>계단으로 내려옴(같은 원정, 아래층 승강장).</summary>
        Stairs,
        /// <summary>시험 패널 '이 씨앗으로 다시'·'씨앗 +1'(같은 원정, 같은 층을 다시 지음).</summary>
        Rebuild,
    }

    /// <summary>
    /// 장면을 다시 불러온 뒤 새 DungeonRoot가 할 일(장면 사이 쪽지). ProfileCarry.Trip에 두고 새 장면 Awake가 읽는다.
    /// </summary>
    public sealed class TripPlan
    {
        /// <summary>지을 층.</summary>
        public int Floor = 1;
        public ArrivalKind Arrival = ArrivalKind.FirstStart;
        /// <summary>시험 패널이 정한 씨앗(없으면 ExpeditionSeeds.Choose).</summary>
        public ulong? ForcedSeed;
        /// <summary>밤 카드를 이어 보여 줄 남은 때(Time.realtimeSinceStartup 기준, 0이면 없음)와 글.</summary>
        public float NightUntil;
        public string[] NightLines = Array.Empty<string>();
        /// <summary>새 장면에서 승강장 고르기를 띄운다(밤을 지난 바구니 원정만).</summary>
        public bool PickLanding;
        /// <summary>옛 장면이 장면 불러오기를 부른 실제 시각(Time.realtimeSinceStartup). 새 장면이 다시 불러오는 시간을 잰다(6장 위험 6).</summary>
        public float RequestedRealtime;

        // ── 시험 패널 다시 짓기(Rebuild)만: 같은 원정·같은 층을 옛 장면과 같은 조건으로 다시 짓는다 ──
        /// <summary>옛 장면의 '이 층을 처음 밟는 원정인가'(같은 층 예산·감쇠·층 이름 카드를 쓰게). null이면 꾸러미의 밟은 층으로 정한다.</summary>
        public bool? FirstVisit;
        /// <summary>옛 장면이 흔적을 견준 지난 원정 지도(같은 원정 안에서 막 지은 지도와 견주지 않게). null이면 흔적 없음.</summary>
        public string TraceBase;
        /// <summary>이번 원정에 이 층 측량을 이미 받았는가(한 원정에 한 장).</summary>
        public bool SurveyDone;
    }

    /// <summary>바구니로 올라갈 때 결과 창 숫자(매판 새 탐험 1차 2-3 M0b: "3번째 원정 끝 — 밝힌 칸 9/10 · 궤짝 4 · 쓰러뜨린 것 13 · 레벨 3").</summary>
    public sealed class ExpeditionSummary
    {
        public int Expedition;
        public int CellsVisited;
        public int CellsTotal;
        public int Chests;
        public int Kills;
        public int Level;
        public int DeepestFloorThisTrip;
        public int StonesGained;
        public int GoldGained;

        /// <summary>결과 창 한 줄.</summary>
        public string Line() => $"{Expedition}번째 원정 끝 — 밝힌 칸 {CellsVisited}/{CellsTotal} · 궤짝 {Chests} · 쓰러뜨린 것 {Kills} · 레벨 {Level}";
    }

    /// <summary>승강장 고르기 판 한 줄(2-3 단계 3: 층, 명패 찾음, 측량 장 수, 권장 레벨. 이름난 정예는 첫 시험판에 없음).</summary>
    public readonly struct LandingOption
    {
        public readonly int Floor;
        public readonly string Name;
        public readonly int RecommendedLevel;
        /// <summary>그 층 명패를 찾았는가(명패가 없는 층은 null).</summary>
        public readonly bool? NameplateFound;
        public readonly int SurveySheets;
        /// <summary>가장 깊은 승강장('줄 끝', 기본 선택).</summary>
        public readonly bool RopeEnd;
        /// <summary>아직 밟지 않은 층(고른 지도로 처음 내려감).</summary>
        public readonly bool FirstVisit;

        public LandingOption(int floor, string name, int recommendedLevel, bool? nameplateFound, int surveySheets, bool ropeEnd, bool firstVisit)
        {
            Floor = floor;
            Name = name;
            RecommendedLevel = recommendedLevel;
            NameplateFound = nameplateFound;
            SurveySheets = surveySheets;
            RopeEnd = ropeEnd;
            FirstVisit = firstVisit;
        }
    }

    /// <summary>
    /// 꾸러미 보관소(매판 새 탐험 1차 3-3). 같은 플레이 안에서 장면을 다시 불러와도 살아남고, 플레이를 새로 시작하면 비워진다.
    /// 도메인 다시 불러오기가 꺼져 있어 SubsystemRegistration에서 직접 비운다. DungeonRoot.ResetStatics는 이것을 비우지 않는다.
    /// 담기(Capture): DungeonState·PlayerProgress·Inventory·플레이어 → CarryData. 풀기(Apply): 그 반대(새 장면 Awake에서).
    /// </summary>
    public static class ProfileCarry
    {
        /// <summary>들고 다니는 꾸러미. null이면 아직 첫 장면(새 프로필).</summary>
        public static CarryData Data { get; private set; }
        /// <summary>다음 장면이 할 일. null이면 플레이를 새로 시작한 것.</summary>
        public static TripPlan Trip { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Data = null;
            Trip = null;
        }

        /// <summary>꾸러미를 꺼낸다. 없으면 새 프로필을 만든다(프로필 소금 = 지금 시각).</summary>
        public static CarryData Ensure()
        {
            if (Data == null) Data = CarryData.NewProfile((ulong)DateTime.UtcNow.Ticks);
            return Data;
        }

        /// <summary>장면을 다시 불러오기 직전에 다음 장면이 할 일을 둔다.</summary>
        public static void SetTrip(TripPlan trip) => Trip = trip;

        /// <summary>
        /// 담기: 지금 장면의 프로필 몫(과 keepLeg면 원정 몫)을 Data에 적는다. 바구니로 올라가기·계단·시험 패널 다시 짓기가 장면을 다시 불러오기 직전에 부른다.
        /// 프로필 몫: DungeonState(강화석·골드·곡괭이·열쇠, 끝낸 '한 번 받는 것' id → OnceDone), PlayerProgress(레벨·경험치·스킬, 발견 주머니에서 받은 몫·사건 경험치 층),
        /// Inventory(장착 8자리·가방: 옵션·전설·강화 포함, CarryData 판본 2). 원정 몫(keepLeg, 계단으로 내려갈 때): 지금까지의 원정 기록 + 이번 층(밝힌 칸·처음 갈 수 있는 칸·처치·궤짝·재화) + 체력·물약.
        /// keepLeg가 아니면(바구니로 올라감) 원정 몫을 버린다. 같은 장면에서 두 번 불러도 같은 결과다(원정 몫은 매번 새로 셈).
        /// rebuild(시험 패널 다시 짓기)면 이 층의 밝힌 칸·처음 갈 수 있는 칸은 담지 않는다(다시 지은 장면이 이 층을 다시 센다).
        /// </summary>
        public static void Capture(DungeonRoot root, bool keepLeg, bool rebuild = false)
        {
            var data = Ensure();
            if (!root) return;
            var state = root.State;
            if (state != null)
            {
                data.Stones = state.Stones;
                data.Gold = state.Gold;
                data.HasPickaxe = state.HasPickaxe;
                data.HasKey = state.HasKey;
                foreach (var e in state.OneTime.Values)
                    if (e.Done && FloorRecipe.IsOnceItem(e.Id)) data.OnceDone.Add(e.Id);
            }
            if (root.Progress) root.Progress.ExportTo(data);
            if (root.Inventory) root.Inventory.ExportTo(data);

            if (!keepLeg)
            {
                data.Leg = null;
                return;
            }
            var leg = root.LegWithThisFloor(!rebuild);
            var player = root.Player;
            if (player && player.Health)
            {
                leg.Hp = player.IsDown ? -1 : player.Health.Current;
                leg.Potions = player.Potions;
            }
            data.Leg = leg;
        }

        /// <summary>
        /// 풀기: Data를 새 장면의 DungeonState·PlayerProgress·Inventory·플레이어에 넣는다(새 장면 Awake에서, Inventory·Progress Init 뒤).
        /// 차례는 Progress(레벨·질긴 몸) → Inventory(장착 8자리·가방, RecomputeStats로 최대 체력 확정) → RestoreVitals(체력을 이음)다.
        /// 상태 몫(ApplyState)은 자리 표시 Spawn 전에 이미 한 번 넣었고 여기서 다시 넣어도 같다.
        /// 계단(과 시험 패널 다시 짓기)으로 왔으면 원정 몫의 체력·물약을 잇는다. 바구니로 왔으면 가득 찬 채 시작한다.
        /// </summary>
        public static void Apply(DungeonRoot root)
        {
            if (!root) return;
            var data = Ensure();
            ApplyState(root);
            if (root.Progress) root.Progress.ImportFrom(data);
            if (root.Inventory) root.Inventory.ImportFrom(data);
            bool sameExpedition = root.Arrival == ArrivalKind.Stairs || root.Arrival == ArrivalKind.Rebuild;
            if (sameExpedition && data.Leg != null && root.Player) root.Player.RestoreVitals(data.Leg.Hp, data.Leg.Potions);
        }

        /// <summary>
        /// 풀기의 상태 몫: DungeonState(강화석·골드·곡괭이·열쇠·원정 번호·소금)와 ProfileDone(받은 '한 번 받는 것' + 이 층 승강장을 켰으면 그 말뚝).
        /// DungeonRoot가 지도를 지은 뒤·세계와 자리 표시를 만들기 전에 부른다(Register가 ProfileDone을 보고 처음부터 끝낸 것으로 적음).
        /// 이미 등록된 자리도 끝낸 것으로 고치므로 늦게 불러도 결과는 같다.
        /// </summary>
        public static void ApplyState(DungeonRoot root)
        {
            if (!root || root.State == null) return;
            var data = Ensure();
            var state = root.State;
            state.Stones = data.Stones;
            state.Gold = data.Gold;
            state.HasPickaxe = data.HasPickaxe;
            state.HasKey = data.HasKey;
            state.Expedition = Math.Max(1, data.Expedition);
            state.RunSalt = data.ProfileSalt;
            foreach (var id in data.OnceDone) state.ProfileDone.Add(id);
            if (data.LitLandings.Contains(root.Floor) && !string.IsNullOrEmpty(root.LandingStakeId)) state.ProfileDone.Add(root.LandingStakeId);
            foreach (var e in state.OneTime.Values)
                if (state.ProfileDone.Contains(e.Id)) e.Done = true;
        }

        /// <summary>시험·검사용: 꾸러미와 쪽지를 비운다(다음 장면은 새 프로필).</summary>
        public static void Clear()
        {
            Data = null;
            Trip = null;
        }

        /// <summary>
        /// 승강장 고르기 판 목록(2-3 단계 3): 1층부터 줄 끝까지(시험판 상한 FloorRecipe.MaxTestFloor). 층 이름, 권장 레벨,
        /// 명패 찾음(그 층에 명패가 없으면 null), 측량 장 수, 줄 끝(기본 선택), 아직 밟지 않은 층.
        /// </summary>
        public static List<LandingOption> LandingOptions()
        {
            var data = Ensure();
            var list = new List<LandingOption>();
            int ropeEnd = data.RopeEnd(FloorRecipe.MaxTestFloor);
            foreach (int floor in data.LandingFloors(FloorRecipe.MaxTestFloor))
            {
                var recipe = FloorRecipe.For(floor);
                string name = recipe != null && !string.IsNullOrEmpty(recipe.Name) ? recipe.Name : floor + "층";
                string nameplate = NameplateId(recipe);
                bool? found = nameplate != null ? data.OnceDone.Contains(nameplate) : (bool?)null;
                data.SurveySheets.TryGetValue(floor, out int sheets);
                list.Add(new LandingOption(floor, name, DungeonHud.RecommendedLevel(floor), found, sheets, floor == ropeEnd, !data.VisitedFloors.Contains(floor)));
            }
            return list;
        }

        /// <summary>그 층 명패의 '한 번 받는 것' id(1층 f1.T.nameplate). 없으면 null.</summary>
        static string NameplateId(FloorRecipe recipe)
        {
            if (recipe == null) return null;
            foreach (var o in recipe.OnceItems)
                if (o.Kind == FeatureKind.Nameplate) return o.Id;
            return null;
        }
    }
}
