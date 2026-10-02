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
        /// <summary>화면 아래 짧은 알림 한 줄.</summary>
        public static event Action<string> Message;
        /// <summary>곡괭이·광맥·금고 같은 큰 소리(자리, 반경 12). 가장 가까운 무리 하나가 깨어 칸 입구에서 기다린다.</summary>
        public static event Action<Vector2, float> Noise;
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
        /// <summary>말뚝에서 '원정 다시 시작': 적을 다시 놓고 원정마다 생기는 것(광맥)을 되살린다.</summary>
        public static event Action ExpeditionRestarted;
        /// <summary>계단을 씀(M0b는 1층까지라 기록을 보여 준다).</summary>
        public static event Action StairsUsed;
        /// <summary>막힌 길이 열림(판자벽 부숨, 금 간 벽 깸).</summary>
        public static event Action<DungeonEdge> EdgeOpened;

        public static void ResetStatics()
        {
            CellEntered = null;
            Discovered = null;
            Message = null;
            Noise = null;
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
        }

        public static void RaiseCellEntered(DungeonCell cell, bool first) => CellEntered?.Invoke(cell, first);
        public static void RaiseDiscovered(DiscoveryKind kind, Vector2 pos, string label) => Discovered?.Invoke(kind, pos, label);
        public static void Say(string text) => Message?.Invoke(text);
        public static void RaiseNoise(Vector2 pos, float radius) => Noise?.Invoke(pos, radius);
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
    /// 이번 시험 동안 남는 것(3차 초안 2-7 '영구로 남는 것'): 지도, 켠 말뚝·등잔, 연 곳, 능력, 재화. 디스크 저장은 M0b에서 뺀다.
    /// </summary>
    public sealed class DungeonState
    {
        public bool HasPickaxe;
        public bool HasKey;
        public int Stones;
        public int Gold;
        public int Expedition = 1;
        /// <summary>이번 실행의 무작위 소금. 궤짝 결과가 실행마다 달라지게 한다(디스크 저장이 없어 매번 원정 1부터 시작).</summary>
        public ulong RunSalt;
        public float ExpeditionStartTime;

        public readonly HashSet<string> VisitedCells = new HashSet<string>();
        public readonly Dictionary<string, OneTimeEntry> OneTime = new Dictionary<string, OneTimeEntry>();
        /// <summary>켠 말뚝 id(켠 순서).</summary>
        public readonly List<string> ActiveStakes = new List<string>();
        /// <summary>쓰러지면 다시 설 말뚝.</summary>
        public string LastStakeId;

        public OneTimeEntry Register(string id, DiscoveryKind kind, DungeonCell cell, Vector2 pos, string label)
        {
            if (!OneTime.TryGetValue(id, out var e))
            {
                e = new OneTimeEntry { Id = id, Kind = kind, Cell = cell, Position = pos, Label = label };
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
