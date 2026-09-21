using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo5.NightRun
{
    public enum RunPhase { Briefing, Expedition, Debrief }
    public enum Order { Move, Search, Melee, Shoot, Lure, Guard, Extract }

    [Serializable]
    public sealed class Piece
    {
        public string Name;
        public int X, Y, Health = 5, Actions = 2;
        public int MaxHealth = 5, AimBonus;
        public string Role = "모험가";
        public bool Guarding, Extracted;
        public bool Active => Health > 0 && !Extracted;
        public Piece(string name, int x, int y) { Name = name; X = x; Y = y; }
    }

    // Engine-independent rules: the view never writes gameplay state.
    public sealed class RunState
    {
        public const int Size = 4;
        public RunPhase Phase { get; private set; } = RunPhase.Briefing;
        public List<Piece> Squad { get; } = new List<Piece>();
        public List<Piece> Enemies { get; } = new List<Piece>();
        public List<string> Journal { get; } = new List<string>();
        public int Round { get; private set; } = 1;
        public int Noise { get; private set; }
        public int Light { get; private set; } = 100;
        public int Supplies { get; private set; }
        public int Ammo { get; private set; } = 4;
        public int Waves { get; private set; }
        public bool Won { get; private set; }
        public bool[] Searched { get; } = new bool[2];
        public int LureX { get; private set; } = -1;
        public int LureY { get; private set; } = -1;
        public string Result { get; private set; } = "";
        public string LastMessage { get; private set; } = "";
        readonly Func<double> roll;
        public static readonly int[,] Caches = { { 1, 1 }, { 2, 2 } };
        public static readonly int[,] CoverProps = { { 0, 0 }, { 2, 3 } };

        public RunState(Func<double> random = null, Adventurer[] party = null, int ammo = 4)
        {
            roll = random ?? new Random().NextDouble;
            Squad.Add(new Piece("민서", 0, 3));
            Squad.Add(new Piece("도윤", 1, 3));
            if (party != null)
                for (int i = 0; i < 2; i++)
                {
                    Squad[i].Name = party[i].Name; Squad[i].Role = party[i].Role;
                    Squad[i].Health = party[i].Health; Squad[i].MaxHealth = party[i].MaxHealth; Squad[i].AimBonus = party[i].Aim;
                }
            Ammo = Math.Max(0, ammo);
            Enemies.Add(new Piece("감염자 A", 3, 0) { Health = 3 });
            Enemies.Add(new Piece("감염자 B", 3, 1) { Health = 3 });
        }

        public void Start()
        {
            if (Phase != RunPhase.Briefing) return;
            Phase = RunPhase.Expedition;
            Note("폐상가 진입. 선반 두 곳을 탐색하고 청록색 출구로 돌아오자.");
        }
        internal void CompleteVisit(int loot,int ammo)
        {
            if(Phase==RunPhase.Debrief)return;
            Supplies=Math.Max(0,loot);Ammo=Math.Max(0,ammo);
            foreach(var p in Squad)p.Extracted=p.Health>0;
            CheckEnd();
        }
        public static bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public static int Distance(int x, int y, int tx, int ty) => Math.Abs(x - tx) + Math.Abs(y - ty);
        public static int CacheAt(int x, int y)
        {
            for (int i = 0; i < 2; i++) if (Caches[i, 0] == x && Caches[i, 1] == y) return i;
            return -1;
        }
        public static bool ExitAt(int x, int y) => y == 3 && x <= 1;
        public static int PropAt(int x, int y)
        {
            for (int i = 0; i < 2; i++) if (CoverProps[i, 0] == x && CoverProps[i, 1] == y) return i;
            return -1;
        }
        public static bool ObstacleAt(int x, int y) => CacheAt(x, y) >= 0 || PropAt(x, y) >= 0;
        public Piece EnemyAt(int x, int y) => Enemies.Find(p => p.Active && p.X == x && p.Y == y);
        public bool Occupied(int x, int y) => Squad.Any(p => p.Active && p.X == x && p.Y == y) || EnemyAt(x, y) != null;
        public bool ClearLine(Piece a, int x, int y)
        {
            if (a.X == x && a.Y == y) return false;
            if (a.X != x && a.Y != y) return false;
            int dx = Math.Sign(x - a.X), dy = Math.Sign(y - a.Y);
            int cx = a.X + dx, cy = a.Y + dy;
            while (cx != x || cy != y)
            {
                if (ObstacleAt(cx, cy) || Occupied(cx, cy)) return false;
                cx += dx; cy += dy;
            }
            return true;
        }
        public static bool InCover(Piece p)
        {
            for (int i = 0; i < 2; i++) if (Distance(p.X, p.Y, CoverProps[i, 0], CoverProps[i, 1]) == 1) return true;
            return false;
        }
        public int HitChance(Piece attacker, Piece target, bool ranged, bool enemyAttack = false)
        {
            int chance = enemyAttack ? 80 : ranged ? 90 : 85;
            if (ranged)
            {
                chance += attacker.AimBonus;
                chance -= Math.Max(0, Distance(attacker.X, attacker.Y, target.X, target.Y) - 1) * 10;
                if (InCover(target)) chance -= 20;
            }
            if (Light == 0 && !enemyAttack) chance -= 15;
            return Math.Max(20, Math.Min(95, chance));
        }
        void Attack(Piece attacker, Piece target, bool ranged, bool enemyAttack = false)
        {
            int chance = HitChance(attacker, target, ranged, enemyAttack);
            if (roll() * 100 >= chance)
            {
                Note(attacker.Name + " → " + target.Name + " 빗나감 (명중률 " + chance + "%).");
                return;
            }
            int damage = ranged ? 3 : 2;
            if (target.Guarding) damage = Math.Max(0, damage - 1);
            target.Health = Math.Max(0, target.Health - damage);
            Note(attacker.Name + " → " + target.Name + " 명중 " + chance + "% · 피해 " + damage + (target.Health == 0 ? " · 전투 불능." : "."));
        }
        public string Validate(int index, Order order, int x, int y)
        {
            if (Phase != RunPhase.Expedition) return "원정 중에만 행동할 수 있다.";
            if (index < 0 || index >= Squad.Count || !Squad[index].Active) return "행동할 대원을 선택하자.";
            var p = Squad[index];
            int cost = order == Order.Shoot ? 2 : 1;
            if (p.Actions < cost) return "행동력이 부족하다. 다른 대원을 선택하거나 턴을 종료하자.";
            if (!Inside(x, y)) return "보드 안의 칸을 선택하자.";
            int d = Distance(p.X, p.Y, x, y);
            switch (order)
            {
                case Order.Move:
                    if (d != 1 || ObstacleAt(x, y) || Occupied(x, y)) return "상하좌우의 빈 칸으로 한 칸 이동할 수 있다.";
                    break;
                case Order.Search:
                    int c = CacheAt(x, y);
                    if (d != 1 || c < 0) return "선반 옆으로 이동한 뒤 선반을 선택하자.";
                    if (Searched[c]) return "이미 수색한 선반이다.";
                    if (Supplies >= 3) return "가방이 가득 찼다.";
                    break;
                case Order.Melee:
                    if (d != 1 || EnemyAt(x, y) == null) return "바로 옆 감염자를 선택하자.";
                    break;
                case Order.Shoot:
                    if (Ammo <= 0) return "탄약이 없다.";
                    if (EnemyAt(x, y) == null || !ClearLine(p, x, y)) return "같은 행·열의 감염자를 조준하자. 선반과 대원은 사격을 막는다.";
                    break;
                case Order.Lure:
                    if (d < 1 || d > 3 || ObstacleAt(x, y) || Occupied(x, y)) return "3칸 이내 빈 바닥에 미끼를 던지자.";
                    break;
                case Order.Extract:
                    if (!ExitAt(p.X, p.Y)) return "출구 칸으로 이동해야 철수할 수 있다.";
                    break;
            }
            return "";
        }
        public bool Act(int index, Order order, int x, int y)
        {
            string reason = Validate(index, order, x, y);
            if (reason.Length > 0) { LastMessage = reason; return false; }
            var p = Squad[index];
            p.Actions -= order == Order.Shoot ? 2 : 1;
            switch (order)
            {
                case Order.Move: p.X = x; p.Y = y; Note(p.Name + " 이동."); break;
                case Order.Search:
                    Searched[CacheAt(x, y)] = true; Supplies++; Noise++;
                    Note(p.Name + " 보급품 확보. 가방 " + Supplies + "/3, 소음 +1."); break;
                case Order.Melee:
                    Noise++; Attack(p, EnemyAt(x, y), false); break;
                case Order.Shoot:
                    Ammo--; Noise += 3; Attack(p, EnemyAt(x, y), true); break;
                case Order.Lure:
                    LureX = x; LureY = y; Noise++;
                    Note(p.Name + " 미끼 투척. 이번 적 턴에는 감염자가 소리를 쫓는다."); break;
                case Order.Guard: p.Guarding = true; Note(p.Name + " 방어: 다음 적 턴까지 받는 피해 -1."); break;
                case Order.Extract: p.Extracted = true; Note(p.Name + " 철수 완료."); CheckEnd(); break;
            }
            return true;
        }
        public string Intent(Piece enemy)
        {
            if (!enemy.Active) return "제압됨";
            if (LureX >= 0) return "미끼 추적";
            var target = Nearest(enemy);
            if (target == null) return "배회";
            bool adjacent = Distance(enemy.X, enemy.Y, target.X, target.Y) == 1;
            return (adjacent ? "공격 → " : "추적 → ") + target.Name + (adjacent ? " (" + HitChance(enemy, target, false, true) + "%)" : "");
        }
        Piece Nearest(Piece e) => Squad.Where(p => p.Active).OrderBy(p => Distance(e.X, e.Y, p.X, p.Y)).FirstOrDefault();

        public void EndTurn()
        {
            if (Phase != RunPhase.Expedition) return;
            foreach (var e in Enemies.Where(p => p.Active).ToArray())
            {
                var target = Nearest(e);
                if (target == null) break;
                if (LureX < 0 && Distance(e.X, e.Y, target.X, target.Y) == 1)
                {
                    Attack(e, target, false, true);
                }
                else
                {
                    int tx = LureX >= 0 ? LureX : target.X, ty = LureX >= 0 ? LureY : target.Y;
                    for (int n = 0; n < (Light == 0 ? 2 : 1); n++) StepEnemy(e, tx, ty);
                }
            }
            LureX = LureY = -1;
            if (Noise >= 6 && Waves < 2 && !Occupied(3, 0))
            {
                Waves++; Noise -= 6;
                Enemies.Add(new Piece("증원 " + Waves, 3, 0) { Health = 3 });
                Note("소음을 듣고 북동쪽에서 감염자가 들어왔다! 증원은 다음 적 턴부터 행동한다.");
            }
            Round++; Light = Math.Max(0, Light - 15);
            foreach (var p in Squad) { p.Actions = p.Active ? 2 : 0; p.Guarding = false; }
            CheckEnd();
            if (Phase == RunPhase.Expedition) Note(Round + "턴 시작. " + (Light == 0 ? "빛 소진: 적 이동 2칸." : "행동력 회복, 조명 -15."));
        }
        void StepEnemy(Piece e, int tx, int ty)
        {
            // BFS finds the shortest free route around shelves and other tokens.
            var queue = new Queue<int>(); var parent = new Dictionary<int, int>();
            int start = e.Y * Size + e.X, goal = ty * Size + tx;
            queue.Enqueue(start); parent[start] = -1;
            int best = start, bestDistance = Distance(e.X, e.Y, tx, ty);
            int[,] directions = { { 0, 1 }, { -1, 0 }, { 1, 0 }, { 0, -1 } };
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(), cx = cell % Size, cy = cell / Size;
                int d = Distance(cx, cy, tx, ty);
                if (d < bestDistance) { best = cell; bestDistance = d; }
                if (cell == goal || (LureX < 0 && d == 1)) { best = cell; break; }
                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + directions[i, 0], ny = cy + directions[i, 1], next = ny * Size + nx;
                    if (!Inside(nx, ny) || ObstacleAt(nx, ny) || Occupied(nx, ny) || parent.ContainsKey(next)) continue;
                    parent[next] = cell; queue.Enqueue(next);
                }
            }
            if (best == start) return;
            while (parent[best] != start && parent[best] >= 0) best = parent[best];
            e.X = best % Size; e.Y = best / Size;
        }
        void CheckEnd()
        {
            if (Squad.Any(p => p.Active)) return;
            Phase = RunPhase.Debrief;
            int returned = Squad.Count(p => p.Extracted);
            Won = returned == 2 && Supplies >= 2;
            if (returned == 0) Supplies = 0;
            Result = Won ? "보급품 회수 완료" : returned > 0 ? "긴급 철수" : "원정 실패";
            Note(Result + ". 귀환 " + returned + "/2, 회수 보급품 " + Supplies + ".");
        }
        void Note(string message)
        {
            LastMessage = message; Journal.Add(message);
            if (Journal.Count > 60) Journal.RemoveAt(0);
        }
    }
}
