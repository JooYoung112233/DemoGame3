using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 문틈 엿듣기(기획/1-2층-탐험-맛-1차.md 4-4, 12장 질문 2 = 가). 웅크린 채(PlayerController.Crouching) 가만히(걸음 초당 DoorListenRules.MaxSpeed 아래,
    /// 지난 프레임 위치로 잼) 지금 칸(DungeonRoot.CurrentCell)의 어느 문 가운데(DungeonEdge.DoorCenter — 비튼 실제 자리)에서 Range 안에
    /// HoldSeconds(1초) 있으면, 그 문 건너편 칸의 기척(Tally)을 글 한 줄(DoorListenRules.Line)로 화면 아래 알림 줄에 띄운다(DungeonEvents.Say).
    /// 맞거나 움직이거나 일어서면 처음부터. 같은 문은 Cooldown(15초) 뒤에 다시 들을 수 있다. 막힌 문(판자벽·금 간 벽·자물쇠 문)도 들린다.
    /// 창·멈춤·쓰러짐 동안은 세지 않는다. 엿듣기는 소리를 내지 않고 새 키도 없다(웅크리기와 가만히 있기뿐). 시간은 게임 시간이다.
    /// CellEncounters가 같은 물체에 붙인다(1-2층 탐험 맛 1차 11장, 물결 3 B1). 글은 ExploreText에서만 가져온다(숫자 없음).
    /// </summary>
    public sealed class DoorListen : MonoBehaviour
    {
        /// <summary>마지막으로 엿들은 문(확인용, 없으면 null).</summary>
        public DungeonEdge LastEdge { get; private set; }
        /// <summary>마지막으로 띄운 글(확인용, 없으면 null).</summary>
        public string LastLine { get; private set; }
        /// <summary>이 장면에서 엿들은 횟수(확인용).</summary>
        public int Heard { get; private set; }
        /// <summary>마지막으로 센 기척(확인용).</summary>
        public ListenTally LastTally { get; private set; }

        /// <summary>지금 귀를 대고 있는 문(가까운 문, 쉬는 시간이 끝난 것만). 없으면 null.</summary>
        DungeonEdge _edge;
        /// <summary>그 문 앞에서 웅크린 채 가만히 있은 시간(초).</summary>
        float _held;
        /// <summary>문마다 다시 들을 수 있는 게임 시각(Time.time).</summary>
        readonly Dictionary<DungeonEdge, float> _readyAt = new Dictionary<DungeonEdge, float>();
        Vector2 _lastPos;
        bool _hasLastPos;
        int _hpSeen = -1;

        void Update()
        {
            var root = DungeonRoot.Instance;
            var player = PlayerController.Instance;
            if (!root || !player)
            {
                Restart();
                _hasLastPos = false;
                return;
            }

            // 몸 걸음(지난 프레임 위치로 잼). 멈춤 동안(시간 0)은 재지 않고 세지도 않는다.
            Vector2 pos = player.Position;
            float dt = Time.deltaTime;
            float speed = 0f;
            if (_hasLastPos && dt > 0f) speed = (pos - _lastPos).magnitude / dt;
            _lastPos = pos;
            _hasLastPos = true;

            // 맞으면 처음부터(체력이 줄었는가, InteractionSystem 길게 누르기와 같은 셈).
            int hp = player.Health ? player.Health.Current : 0;
            bool gotHit = _hpSeen >= 0 && hp < _hpSeen;
            _hpSeen = hp;

            if (dt <= 0f || TimeScaleService.Paused) return;
            if (!root.Playing || DungeonUi.ModalOpen || PlayerInputReader.Blocked || player.IsDown || !player.Crouching
                || gotHit || speed >= DoorListenRules.MaxSpeed)
            {
                Restart();
                return;
            }

            var edge = NearestDoor(root.CurrentCell, pos, Time.time);
            if (edge == null || edge != _edge)
            {
                // 다른 문으로 옮겼거나 문 앞을 벗어남: 처음부터.
                _edge = edge;
                _held = 0f;
                if (edge == null) return;
            }

            _held += dt;
            if (_held >= DoorListenRules.HoldSeconds) Listen(root.CurrentCell, edge);
        }

        void Restart()
        {
            _edge = null;
            _held = 0f;
        }

        /// <summary>지금 칸의 문 가운데 Range 안에서 가장 가까운 문(다시 들을 수 있는 것만). 없으면 null.</summary>
        DungeonEdge NearestDoor(DungeonCell cell, Vector2 pos, float now)
        {
            if (cell == null) return null;
            DungeonEdge best = null;
            float bestSq = DoorListenRules.Range * DoorListenRules.Range;
            foreach (var e in cell.Edges)
            {
                if (e == null || e.Other(cell) == null) continue;
                if (_readyAt.TryGetValue(e, out float at) && now < at) continue;
                float sq = (e.DoorCenter - pos).sqrMagnitude;
                if (sq > bestSq) continue;
                best = e;
                bestSq = sq;
            }
            return best;
        }

        /// <summary>건너편 칸을 세어 글 한 줄을 띄우고, 그 문은 Cooldown 동안 쉰다.</summary>
        void Listen(DungeonCell cell, DungeonEdge edge)
        {
            var other = edge.Other(cell);
            var tally = Tally(other);
            string line = DoorListenRules.Line(tally);
            _readyAt[edge] = Time.time + DoorListenRules.Cooldown;
            LastEdge = edge;
            LastLine = line;
            LastTally = tally;
            Heard++;
            Restart();
            DungeonEvents.Say(line);
        }

        /// <summary>
        /// 그 칸 안(Bounds)의 살아 있는 적을 센다(허수아비만 빼고, 보이는지와 상관없이 — 소리 정보, 지나가던 순찰도 든다).
        /// 종류는 한 마리를 한 곳에만: 둥지 → Nests, 정예·보스 → Elites, 나머지는 Rats·Boars·Archers. 상태는 종류와 겹쳐 센다(둥지는 빼고):
        /// 보스(오우거 굴)도 센다(물결 3 고침): 빼면 보스방 문 앞에서 돌 씹는 소리가 들리는데도 '조용하다'가 떠 화면 글이 실제와 반대로 말한다.
        /// 살아 있는 보스는 '거친 숨소리(큰 놈)', 먹는 중이면 '씹는 소리'도 더한다(기획 4-4).
        /// 깸(Aware 또는 IsGuarding) → Awake, 아니면 순찰(IsPatrolling) → Patrolling, 아니면 먹는 중(IsEating) → Eating.
        /// 깨어 싸우거나 돌아가는 순찰·먹던 적은 Awake로만 센다(ListenTally 주석: 먹는 중·순찰은 Awake로 세지 않는다).
        /// </summary>
        public static ListenTally Tally(DungeonCell cell)
        {
            var t = new ListenTally();
            if (cell == null) return t;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!e || e.Dead || e.IsDummy) continue;
                if (!cell.Bounds.Contains(e.Position)) continue;
                if (e.Kind == MonsterKind.Nest)
                {
                    t.Nests++;
                    continue;
                }
                if (e.IsElite || e.IsBoss) t.Elites++;
                else
                {
                    switch (e.Kind)
                    {
                        case MonsterKind.Rat: t.Rats++; break;
                        case MonsterKind.Boar: t.Boars++; break;
                        case MonsterKind.Archer: t.Archers++; break;
                        default: continue;
                    }
                }
                if (e.Aware || e.IsGuarding) t.Awake++;
                else if (e.IsPatrolling) t.Patrolling++;
                else if (e.IsEating) t.Eating++;
            }
            return t;
        }
    }
}
