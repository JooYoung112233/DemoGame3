using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 시험장의 사건 통로. 물체(궤짝·등잔·말뚝)가 알리고, 경험치·화면·지도·탐험 기록이 듣는다.
    /// 도메인 다시 불러오기가 꺼져 있으므로 DungeonRoot가 플레이 시작 때 구독을 비운다.
    /// </summary>
    public static class DungeonEvents
    {
        /// <summary>칸에 들어감(칸, 처음인가).</summary>
        public static event Action<DungeonCell, bool> CellEntered;
        /// <summary>처음 찾은 것(경험치·조사율·새 것 간격). 이름은 화면 알림에 쓴다.</summary>
        public static event Action<DiscoveryKind, Vector2, string> Discovered;
        /// <summary>
        /// 화면 아래 짧은 알림 한 줄. 주민을 가리킬 때는 이름을 바로 쓰지 않고 SpeakerIdentity.ReportTarget을 거친다
        /// (이름과 인물이 이어지기 전에는 '경비 초소에'처럼 자리 이름, 기획/마을-의뢰-첫판.md 3-2).
        /// </summary>
        public static event Action<string> Message;
        /// <summary>곡괭이·광맥·금고 같은 큰 소리(자리, 반경 12). 가장 가까운 무리 하나가 깨어 칸 입구에서 기다린다. 누가 냈는지는 NoiseFromPlayer.</summary>
        public static event Action<Vector2, float> Noise;
        /// <summary>
        /// 지금 알리는 큰 소리(Noise)를 플레이어 몸이 냈는가: 곡괭이·광맥·금고·금 간 벽·흙 묻은 쇠 궤짝 파내기는 참(기본).
        /// 함정 소리(낙석 떨어짐·가시 덫 '찰칵')는 웅크림과 상관없이 나므로 거짓 — CellEncounters가 이때는 웅크림 소음 배율을 곱하지 않는다
        /// (기획/1-2층-탐험-맛-1차.md 4-5 반경 12·4-6 반경 8 그대로, 웅크림 0.3배는 4-9 파내기 같은 몸 소리에만). Noise를 알리는 동안만 뜻이 있다.
        /// </summary>
        public static bool NoiseFromPlayer { get; private set; } = true;
        /// <summary>무리가 깨어 싸움이 시작됨(무리 id).</summary>
        public static event Action<int> GroupAwake;
        /// <summary>무리를 모두 쓰러뜨림(무리 id, 둥지 무리인가, 자리).</summary>
        public static event Action<int, bool, Vector2> GroupCleared;
        /// <summary>사건 고르기(설명).</summary>
        public static event Action<string> ChoiceMade;
        /// <summary>장비가 떨어짐(등급 이름 포함 설명).</summary>
        public static event Action<string> GearDropped;
        /// <summary>장비를 낌(설명).</summary>
        public static event Action<string> GearEquipped;
        public static event Action<int> LevelUp;
        /// <summary>쓰러진 뒤 말뚝에서 다시 섬. 깨어 있던 무리는 제자리로 돌아가 잔다.</summary>
        public static event Action PlayerRespawned;
        /// <summary>
        /// 옛 '원정 다시 시작'(같은 지도에 적·광맥만 되돌림). 매판 새 탐험 1차부터 원정은 장면을 다시 불러와 새로 지으므로 더 알리지 않는다.
        /// 구독하는 곳(CellEncounters·OreVein·GoreSystem)이 남아 있어 사건은 지우지 않는다.
        /// </summary>
        public static event Action ExpeditionRestarted;
        /// <summary>계단을 씀(장면을 다시 불러오기 직전, 탐험 기록이 층 시간을 적는다).</summary>
        public static event Action StairsUsed;
        /// <summary>막힌 길이 열림(판자벽 부숨, 금 간 벽 깸).</summary>
        public static event Action<DungeonEdge> EdgeOpened;
        /// <summary>
        /// 층에 들어섬(매판 새 탐험 1차): 장면을 짓고 승강장을 고른 뒤 화면이 밝아질 때 한 번. 층, 이 층을 처음 밟는 원정인가, 어떻게 왔나.
        /// 층 이름 카드·승강장 도착 글·탐험 기록이 듣는다.
        /// </summary>
        public static event Action<int, bool, ArrivalKind> FloorEntered;
        /// <summary>바구니로 올라가기 직전(꾸러미를 담기 전): 바닥 골드·강화석 자동 수거, 탐험 기록 마감.</summary>
        public static event Action ExpeditionEnding;
        /// <summary>승강장 말뚝에 처음 불을 켬(층, 영구).</summary>
        public static event Action<int> LandingLit;
        /// <summary>계단 앞 말뚝을 켜 아래층 승강장까지 바구니 줄이 닿음(그 아래층 번호, 영구).</summary>
        public static event Action<int> RopeExtended;
        /// <summary>
        /// 말뚝에 불을 켬(층, 계단 앞 말뚝인가). 이번 장면에서 처음 켤 때 한 번(꾸러미에서 켜진 승강장은 나가지 않음).
        /// 시험판 끝 층 계단 앞 말뚝은 줄이 더 내려가지 않아 RopeExtended가 나가지 않으므로 따로 알린다. QuestTracker가 듣는다(마을과 의뢰 첫 판 4-3).
        /// </summary>
        public static event Action<int, bool> StakeLit;
        /// <summary>
        /// 보스전이 시작됨(묶음 7 오우거 굴): 보스가 깨고 플레이어가 보스방 안에 있어 문이 막힌 순간 한 번. BossArena가 알린다.
        /// 카메라 고정·예고 어둠 위 그리기·이름표가 이 때부터다.
        /// </summary>
        public static event Action<Enemy> BossEngaged;
        /// <summary>
        /// 보스를 쓰러뜨림(보스, 첫 처치인가). BossReward가 꾸러미(BossLedger)에 적고 보상을 흩뿌린 뒤 알린다.
        /// BossArena(문 열기·등잔 다시 켜기·줄 끝 말뚝·카메라 풀기)가 듣는다. 의뢰 셈은 CombatEvents.EnemyKilled에서 한다(두 번 세지 않게).
        /// </summary>
        public static event Action<Enemy, bool> BossDefeated;
        /// <summary>보스전이 처음으로 돌아감(쓰러진 뒤 말뚝에서 다시 섬: 보스 체력 가득·먹는 중, 문 열림, 등잔 꺼짐). BossArena가 알린다.</summary>
        public static event Action<Enemy> BossReset;
        /// <summary>
        /// 칸을 살폈다(시야와 문 1차 5-3): 새 칸 경험치를 미룬 칸에서 걸을 수 있는 바닥의 75%를 눈으로 훑은 순간, 또는 미루기가 꺼지는 순간.
        /// 칸마다 장면에 한 번. VisionSystem이 알리고 PlayerProgress가 새 칸 경험치와 '살폈다'를 준다.
        /// </summary>
        public static event Action<DungeonCell> CellSwept;

        public static void ResetStatics()
        {
            CellEntered = null;
            Discovered = null;
            Message = null;
            Noise = null;
            NoiseFromPlayer = true;
            GroupAwake = null;
            GroupCleared = null;
            ChoiceMade = null;
            GearDropped = null;
            GearEquipped = null;
            LevelUp = null;
            PlayerRespawned = null;
            ExpeditionRestarted = null;
            StairsUsed = null;
            EdgeOpened = null;
            FloorEntered = null;
            ExpeditionEnding = null;
            LandingLit = null;
            RopeExtended = null;
            StakeLit = null;
            BossEngaged = null;
            BossDefeated = null;
            BossReset = null;
            CellSwept = null;
        }

        public static void RaiseCellEntered(DungeonCell cell, bool first) => CellEntered?.Invoke(cell, first);
        public static void RaiseDiscovered(DiscoveryKind kind, Vector2 pos, string label) => Discovered?.Invoke(kind, pos, label);
        public static void Say(string text) => Message?.Invoke(text);
        /// <summary>큰 소리를 알린다. fromPlayer: 플레이어 몸이 낸 소리인가(함정은 false, NoiseFromPlayer 참고).</summary>
        public static void RaiseNoise(Vector2 pos, float radius, bool fromPlayer = true)
        {
            bool before = NoiseFromPlayer;
            NoiseFromPlayer = fromPlayer;
            try { Noise?.Invoke(pos, radius); }
            finally { NoiseFromPlayer = before; }
        }
        public static void RaiseGroupAwake(int group) => GroupAwake?.Invoke(group);
        public static void RaiseGroupCleared(int group, bool nest, Vector2 pos) => GroupCleared?.Invoke(group, nest, pos);
        public static void RaiseChoice(string text) => ChoiceMade?.Invoke(text);
        public static void RaiseGearDropped(string text) => GearDropped?.Invoke(text);
        public static void RaiseGearEquipped(string text) => GearEquipped?.Invoke(text);
        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaiseExpeditionRestarted() => ExpeditionRestarted?.Invoke();
        public static void RaiseStairsUsed() => StairsUsed?.Invoke();
        public static void RaiseEdgeOpened(DungeonEdge edge) => EdgeOpened?.Invoke(edge);
        public static void RaiseFloorEntered(int floor, bool firstVisit, ArrivalKind arrival) => FloorEntered?.Invoke(floor, firstVisit, arrival);
        public static void RaiseExpeditionEnding() => ExpeditionEnding?.Invoke();
        public static void RaiseLandingLit(int floor) => LandingLit?.Invoke(floor);
        public static void RaiseRopeExtended(int floor) => RopeExtended?.Invoke(floor);
        public static void RaiseStakeLit(int floor, bool stairsFront) => StakeLit?.Invoke(floor, stairsFront);
        public static void RaiseBossEngaged(Enemy boss) => BossEngaged?.Invoke(boss);
        public static void RaiseBossDefeated(Enemy boss, bool firstClear) => BossDefeated?.Invoke(boss, firstClear);
        public static void RaiseBossReset(Enemy boss) => BossReset?.Invoke(boss);
        public static void RaiseCellSwept(DungeonCell cell) => CellSwept?.Invoke(cell);
    }

    /// <summary>프로필에 한 번만 받는 것(궤짝·등잔·말뚝·이야기·숨은 방·지름길) 하나. 조사율과 큰 지도 아이콘에 쓴다.</summary>
    public sealed class OneTimeEntry
    {
        public string Id;
        public DiscoveryKind Kind;
        public DungeonCell Cell;
        public Vector2 Position;
        public string Label;
        public bool Done;
    }

    /// <summary>
    /// 이번 장면(원정 몫)의 기록: 켠 말뚝·등잔, 연 곳, 가 본 칸. 매판 새 탐험 1차부터 장면을 다시 불러올 때마다 새로 만들고,
    /// 프로필 몫(능력·재화·한 번 받는 것)은 꾸러미(ProfileCarry)에서 풀어 넣는다. 디스크 저장은 M0b에서 뺀다.
    /// </summary>
    public sealed class DungeonState
    {
        public bool HasPickaxe;
        public bool HasKey;
        public int Stones;
        public int Gold;
        public int Expedition = 1;
        /// <summary>무작위 소금(꾸러미의 프로필 소금). 궤짝 결과가 프로필·원정 번호마다 달라지게 한다.</summary>
        public ulong RunSalt;
        public float ExpeditionStartTime;

        public readonly HashSet<string> VisitedCells = new HashSet<string>();
        public readonly Dictionary<string, OneTimeEntry> OneTime = new Dictionary<string, OneTimeEntry>();
        /// <summary>켠 말뚝 id(켠 순서).</summary>
        public readonly List<string> ActiveStakes = new List<string>();
        /// <summary>쓰러지면 다시 설 말뚝. 말뚝에 닿으면(새로 적히면) 등잔 다시 서는 곳은 지운다.</summary>
        public string LastStakeId
        {
            get => _lastStakeId;
            set
            {
                _lastStakeId = value;
                RespawnLamp = null;
            }
        }
        string _lastStakeId;
        /// <summary>마지막으로 켠 벽 등잔 곁(묶음 5-6 '작은 다시 서는 곳'). 말뚝보다 나중에 켰으면 여기서 다시 선다.</summary>
        public Vector2? RespawnLamp;
        /// <summary>
        /// 꾸러미에서 온 '이미 끝낸' id(한 번 받는 물건, 켠 승강장 말뚝). Register가 이 id를 처음부터 끝낸 것으로 적어
        /// 궤짝·곡괭이·명패·말뚝의 기존 IsDone 검사가 그대로 통한다(경험치를 다시 주지 않음).
        /// </summary>
        public readonly HashSet<string> ProfileDone = new HashSet<string>();

        public OneTimeEntry Register(string id, DiscoveryKind kind, DungeonCell cell, Vector2 pos, string label)
        {
            if (!OneTime.TryGetValue(id, out var e))
            {
                e = new OneTimeEntry { Id = id, Kind = kind, Cell = cell, Position = pos, Label = label, Done = ProfileDone.Contains(id) };
                OneTime[id] = e;
            }
            return e;
        }

        public bool IsDone(string id) => id != null && OneTime.TryGetValue(id, out var e) && e.Done;

        /// <summary>처음이면 완료로 적고 Discovered를 알린다(경험치·기록). 이미 했으면 false.</summary>
        public bool Complete(string id, DiscoveryKind kind, Vector2 pos, string label)
        {
            var e = Register(id, kind, DungeonRoot.Instance ? DungeonRoot.Instance.World.CellAt(pos) : null, pos, label);
            if (e.Done) return false;
            e.Done = true;
            DungeonEvents.RaiseDiscovered(kind, pos, label);
            return true;
        }

        /// <summary>조사율: 한 번만 받는 것 가운데 끝낸 몫(가 본 칸 포함). 3차 초안 2-4 '조사 n%'.</summary>
        public float Survey(int totalCells)
        {
            int total = totalCells;
            int done = VisitedCells.Count;
            foreach (var e in OneTime.Values)
            {
                total++;
                if (e.Done) done++;
            }
            return total > 0 ? (float)done / total : 0f;
        }

        public Vector2? StakePosition(string id) => id != null && OneTime.TryGetValue(id, out var e) ? e.Position : (Vector2?)null;
    }
}
