using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo8
{
    [Serializable] public class Province
    {
        public int id, owner, terrain, farm, market, barracks, road, walls, focus, garrison = 65, order = 85;
        public int builtTurn = -1, pacifiedTurn = -1;
        public string name;
    }
    [Serializable] public class Faction
    {
        public string name;
        public int gold = 800, food = 500;
        public int capital, tax = 1, agriculture, commerce, logistics, researchedTurn = -1, ceasefireUntil;
    }
    [Serializable] public class Army
    {
        public int id, owner, province, infantry = 160, archers = 80, cavalry = 25, actions = 2, stance;
        public int Count => infantry + archers + cavalry;
    }
    public struct Forecast
    {
        public int attack, defense, losses;
        public bool victory;
    }
    [Serializable] public class Campaign
    {
        public const int Width = 8, Height = 6, RegionCount = Width * Height, VictoryRegions = 12;
        public int version = 2, turn = 1, heldTurns, nextArmyId = 3;
        public string outcome = "", lastReport = "청하의 군대를 선택하고 인접한 지역으로 진군하십시오.";
        public List<Province> provinces = new List<Province>();
        public List<Faction> factions = new List<Faction>();
        public List<Army> armies = new List<Army>();
        public List<string> journal = new List<string>();
        public bool Finished => outcome.Length > 0;
        public static Campaign New()
        {
            var c = new Campaign();
            c.factions.Add(new Faction { name = "청하 연맹", capital = 0, gold = 1200, food = 800 });
            c.factions.Add(new Faction { name = "적산 군벌", capital = 7 });
            c.factions.Add(new Faction { name = "금원 공국", capital = 46 });
            string[] names = { "청하", "풍림", "백령", "장안", "북천", "용문", "연산", "적산", "남곡", "운하", "석문", "낙양", "강릉", "설원", "동주", "동진", "녹야", "하진", "무릉", "백운", "상곡", "태원", "북원", "창성", "서림", "수양", "서주", "진양", "안평", "곡성", "양성", "운몽", "옥천", "남원", "영천", "신야", "오릉", "장수", "한릉", "해안", "서해", "남해", "단양", "수춘", "평원", "금릉", "금원", "해릉" };
            for (int i = 0; i < RegionCount; i++) c.provinces.Add(new Province {
                id = i, name = names[i], owner = i == 0 || i == 8 ? 0 : i == 7 || i == 15 ? 1 : i == 46 || i == 47 ? 2 : -1,
                terrain = i % 8 == 3 || i % 8 == 4 && i / 8 > 2 ? 2 : (i * 7 + i / 8) % 3 == 1 ? 1 : 0,
                farm = i == 8 ? 1 : 0, market = i == 0 ? 1 : 0, barracks = i == 0 || i == 7 || i == 46 ? 1 : 0 });
            c.armies.Add(new Army { id = 0, owner = 0, province = 0 });
            c.armies.Add(new Army { id = 1, owner = 1, province = 7, infantry = 130, archers = 40, cavalry = 10 });
            c.armies.Add(new Army { id = 2, owner = 2, province = 46, infantry = 130, archers = 40, cavalry = 10 });
            c.Record("48개 지역의 세계 · 12개 지역을 확보하고 3턴 유지하십시오.");
            return c;
        }
        public static bool Adjacent(int a, int b)
        {
            int q = a % Width - b % Width, r = a / Width - b / Width;
            return a >= 0 && b >= 0 && a < RegionCount && b < RegionCount && a != b && Math.Max(Math.Abs(q), Math.Max(Math.Abs(r), Math.Abs(q + r))) == 1;
        }
        public Army ArmyOf(int owner) => armies.Find(a => a.owner == owner);
        public Army FindArmy(int owner, int id = -1) => id < 0 ? ArmyOf(owner) : armies.Find(a => a.owner == owner && a.id == id);
        public Army At(int province) => armies.Find(a => a.province == province);
        public int Territory(int owner) => provinces.Count(p => p.owner == owner);
        public string OwnerName(int owner) => owner < 0 ? "독립 지역" : factions[owner].name;
        public static string TerrainName(int terrain) => new[] { "평야", "숲", "산지" }[terrain];
        public int GoldIncome(Province p)
        {
            var f = p.owner < 0 ? new Faction() : factions[p.owner];
            return (80 + p.market * 45 + p.road * 10) * (50 + p.order) / 150 * (80 + f.tax * 20 + f.commerce * 10 + (p.focus == 2 ? 30 : 0)) / 100;
        }
        public int FoodIncome(Province p) => (55 + p.farm * 40 + (p.terrain == 0 ? 20 : 0)) * (100 + (p.owner < 0 ? 0 : factions[p.owner].agriculture * 15) + (p.focus == 1 ? 35 : 0)) / 100;
        public HashSet<int> Supplied(int owner)
        {
            var result = new HashSet<int>(); var queue = new Queue<int>(); int capital = factions[owner].capital;
            if (provinces[capital].owner != owner) return result;
            result.Add(capital); queue.Enqueue(capital);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (var p in provinces.Where(p => p.owner == owner && Adjacent(current, p.id) && !result.Contains(p.id))) { result.Add(p.id); queue.Enqueue(p.id); }
            }
            return result;
        }
        public int MoveCost(int target) => provinces[target].terrain == 2 && provinces[target].road == 0 ? 2 : 1;
        public string SetTax(int tax)
        {
            if (Finished || tax < 0 || tax > 2) return "정책을 바꿀 수 없습니다.";
            factions[0].tax = tax; return Record("세율 변경 · " + new[] { "경감 (치안 +5)", "보통", "중과 (치안 -8)" }[tax]);
        }
        public string SetFocus(int id, int focus)
        {
            if (Finished || provinces[id].owner != 0 || focus < 0 || focus > 3) return "도시 방침을 변경할 수 없습니다.";
            provinces[id].focus = focus; return Record(provinces[id].name + " · " + new[] { "균형", "농업 +35%", "상업 +30%, 치안 -3", "군정: 치안 +5, 수비 성장" }[focus]);
        }
        public string Research(int type)
        {
            var f = factions[0]; if (Finished || type < 0 || type > 2) return "연구할 수 없습니다.";
            int level = type == 0 ? f.agriculture : type == 1 ? f.commerce : f.logistics;
            if (level >= 3 || f.researchedTurn == turn) return "연구는 턴당 하나이며 최대 3단계입니다.";
            int cost = 300 + 150 * level; if (f.gold < cost) return "연구 자금이 부족합니다.";
            f.gold -= cost; f.researchedTurn = turn;
            if (type == 0) f.agriculture++; else if (type == 1) f.commerce++; else f.logistics++;
            return Record(new[] { "농법", "상업", "병참" }[type] + " 연구 완료.");
        }
        public string Truce(int enemy)
        {
            if (Finished || enemy < 1 || enemy > 2 || Territory(enemy) == 0) return "교섭할 대상이 없습니다.";
            if (factions[enemy].ceasefireUntil >= turn) return "이미 휴전 중입니다.";
            if (factions[0].gold < 250) return "휴전 교섭에는 금 250이 필요합니다.";
            factions[0].gold -= 250; factions[enemy].ceasefireUntil = turn + 2;
            return Record(OwnerName(enemy) + " · 3턴 휴전 체결. 서로 공격할 수 없습니다.");
        }
        public string SetStance(int armyId, int stance)
        {
            var a = FindArmy(0, armyId); if (Finished || a == null || stance < 0 || stance > 2) return "태세를 바꿀 수 없습니다.";
            if (a.actions == 0) return "다음 턴에 태세를 변경할 수 있습니다.";
            a.stance = stance; if (stance == 1) a.actions = 0;
            return Record("제" + (a.id + 1) + "군 · " + new[] { "기동", "진지 구축: 방어 +30%", "강습: 공격 +15%, 손실 증가" }[stance]);
        }
        public void Economy(int owner, out int gold, out int food)
        {
            gold = food = 0;
            foreach (var p in provinces.Where(p => p.owner == owner)) { gold += GoldIncome(p) - p.garrison / 12; food += FoodIncome(p) - p.garrison / 15; }
            foreach (var a in armies.Where(a => a.owner == owner)) { gold -= a.infantry / 5 + a.archers / 4 + a.cavalry / 2; food -= a.Count / 4; }
        }
        public string Build(int id, int type)
        {
            if (Finished) return "캠페인이 종료되었습니다.";
            var p = provinces[id];
            if (p.owner != 0) return "우리 지역에서만 건설할 수 있습니다.";
            if (type < 0 || type > 4) return "알 수 없는 시설입니다.";
            int level = new[] { p.farm, p.market, p.barracks, p.road, p.walls }[type];
            if (level >= 3) return "최대 단계입니다.";
            if (p.builtTurn == turn) return "이 지역은 이번 턴에 이미 건설했습니다.";
            int cost = 140 + level * 100;
            if (factions[0].gold < cost) return "금이 부족합니다.";
            factions[0].gold -= cost;
            if (type == 0) p.farm++; else if (type == 1) p.market++; else if (type == 2) p.barracks++; else if (type == 3) p.road++; else p.walls++;
            p.builtTurn = turn;
            return Record(p.name + " · " + new[] { "농지", "시장", "병영", "도로", "성벽" }[type] + " 개선 완료.");
        }
        public string Recruit(int id, int type)
        {
            if (Finished) return "캠페인이 종료되었습니다.";
            var p = provinces[id]; var a = At(id);
            if (p.owner != 0) return "우리 지역에서만 모집할 수 있습니다.";
            if (a == null && armies.Count(x => x.owner == 0) >= 3) return "동시에 세 군대까지 운용할 수 있습니다.";
            if (a == null && p.barracks == 0) return "새 군단 창설에는 병영이 필요합니다.";
            if (type < 0 || type > 2) return "알 수 없는 병종입니다.";
            if (type > 0 && p.barracks < 1) return "궁병·기병 모집에는 병영 1단계가 필요합니다.";
            int cost = new[] { 100, 130, 180 }[type] + (a == null ? 150 : 0), count = type == 2 ? 20 : 40;
            if (a != null && a.Count + count > 800) return "군대 정원은 800명입니다.";
            if (factions[0].gold < cost || factions[0].food < 40) return "금 또는 식량이 부족합니다.";
            factions[0].gold -= cost; factions[0].food -= 40;
            if (a == null) { a = new Army { id = nextArmyId++, owner = 0, province = id, infantry = 0, archers = 0, cavalry = 0, actions = 0 }; armies.Add(a); }
            if (type == 0) a.infantry += count; else if (type == 1) a.archers += count; else a.cavalry += count;
            return Record(p.name + " · " + new[] { "보병", "궁병", "기병" }[type] + " " + count + "명 모집.");
        }
        public string Pacify(int id)
        {
            var p = provinces[id];
            if (Finished || p.owner != 0) return "우리 지역에서만 실행할 수 있습니다.";
            if (p.order >= 100 || p.pacifiedTurn == turn) return "이미 안정되어 있거나 이번 턴 구휼을 마쳤습니다.";
            if (factions[0].gold < 60) return "금 60이 필요합니다.";
            factions[0].gold -= 60; p.order = Math.Min(100, p.order + 25); p.pacifiedTurn = turn;
            return Record(p.name + " · 구휼로 치안 +25.");
        }
        public string Reinforce(int id)
        {
            var p = provinces[id];
            if (Finished || p.owner != 0) return "우리 지역에서만 실행할 수 있습니다.";
            if (p.garrison >= 180) return "수비대가 최대 규모입니다.";
            if (factions[0].gold < 75 || factions[0].food < 25) return "금 75와 식량 25가 필요합니다.";
            factions[0].gold -= 75; factions[0].food -= 25; p.garrison = Math.Min(180, p.garrison + 30);
            return Record(p.name + " · 수비대 보강.");
        }
        public string MoveError(int owner, int target, int armyId = -1)
        {
            var a = FindArmy(owner, armyId);
            if (Finished) return "캠페인이 종료되었습니다.";
            if (a == null) return "군대가 없습니다. 우리 도시에서 병력을 모집하십시오.";
            if (target < 0 || target >= provinces.Count) return "지역이 없습니다.";
            if (a.province == target) return "현재 주둔 지역입니다.";
            if (!Adjacent(a.province, target)) return "인접한 지역으로만 이동할 수 있습니다.";
            if (a.stance == 1) return "진지 구축 상태입니다. 기동 태세로 바꾸십시오.";
            if (a.actions < MoveCost(target)) return "행동력 부족: 평지 1, 도로 없는 산지 2가 필요합니다.";
            if (At(target)?.owner == owner) return "다른 아군 군대가 주둔 중입니다.";
            int enemy = owner == 0 ? provinces[target].owner : provinces[target].owner == 0 ? owner : -1;
            if (enemy > 0 && factions[enemy].ceasefireUntil >= turn) return "휴전 중인 세력은 공격할 수 없습니다.";
            return "";
        }
        static int Power(Army a, int terrain) => a == null ? 0 : a.infantry + a.archers * (terrain == 1 ? 16 : 13) / 10 + a.cavalry * (terrain == 0 ? 23 : 12) / 10;
        public Forecast Preview(int owner, int target, int armyId = -1)
        {
            var a = FindArmy(owner, armyId); var p = provinces[target]; var defender = At(target);
            int attack = Power(a, p.terrain) * (a != null && a.stance == 2 ? 115 : 100) / 100;
            if (a != null && !Supplied(owner).Contains(a.province)) attack = attack * 70 / 100;
            int defense = p.garrison * (p.terrain == 2 ? 15 : 12) / 10 * (70 + p.order) / 170 * (100 + p.walls * 20) / 100 + p.barracks * 18 + Power(defender, p.terrain) * (defender != null && defender.stance == 1 ? 130 : 100) / 100;
            bool win = attack > defense;
            double fraction = win ? Math.Min(.65, .12 + .45 * defense / Math.Max(1.0, attack)) : Math.Min(1, .50 + .35 * defense / Math.Max(1.0, attack));
            if (a != null && a.stance == 2) fraction = Math.Min(1, fraction * 1.2);
            return new Forecast { attack = attack, defense = defense, victory = win, losses = a == null ? 0 : Math.Min(a.Count, (int)Math.Ceiling(a.Count * fraction)) };
        }
        static void Lose(Army a, int loss)
        {
            int total = a.Count;
            if (total <= 0) return;
            int keep = Math.Max(0, total - loss);
            int inf = a.infantry * keep / total, arch = a.archers * keep / total;
            int cav = Math.Min(a.cavalry, keep - inf - arch);
            a.infantry = inf + keep - inf - arch - cav; a.archers = arch; a.cavalry = cav;
        }
        public string Move(int owner, int target, int armyId = -1)
        {
            string error = MoveError(owner, target, armyId); if (error.Length > 0) return error;
            var a = FindArmy(owner, armyId); var p = provinces[target]; a.actions -= MoveCost(target);
            if (p.owner == owner) { a.province = target; return Record(OwnerName(owner) + " · " + p.name + " 이동."); }
            var f = Preview(owner, target, armyId); var defender = At(target); a.actions = 0;
            int before = a.Count; Lose(a, f.losses);
            string result;
            if (f.victory)
            {
                if (defender != null) armies.Remove(defender);
                p.owner = owner; p.order = 35; p.garrison = 25; p.builtTurn = turn; a.province = target;
                result = OwnerName(owner) + " · " + p.name + " 점령! 손실 " + f.losses + "/" + before + "명. 치안 35.";
            }
            else
            {
                p.garrison = Math.Max(10, p.garrison * 65 / 100);
                if (defender != null) Lose(defender, Math.Max(1, defender.Count / 4));
                result = OwnerName(owner) + " · " + p.name + " 공격 실패. 손실 " + f.losses + "/" + before + "명, 생존자는 출발지로 퇴각.";
            }
            armies.RemoveAll(x => x.Count == 0);
            CheckDefeat();
            return Record(result);
        }
        void CheckDefeat()
        {
            for (int i = 0; i < factions.Count; i++) if (Territory(i) == 0) armies.RemoveAll(a => a.owner == i);
            if (Territory(0) == 0) outcome = "패배 · 모든 도시를 잃었습니다.";
        }
        void AI(int owner)
        {
            var land = provinces.Where(p => p.owner == owner).ToList(); if (land.Count == 0) return;
            var faction = factions[owner]; var a = ArmyOf(owner);
            if (a == null && faction.gold >= 200 && faction.food >= 80)
            {
                a = new Army { id = nextArmyId++, owner = owner, province = land[0].id, infantry = 80, archers = 20, cavalry = 0, actions = 0 };
                faction.gold -= 200; faction.food -= 80; armies.Add(a);
            }
            if (a == null) return;
            if (faction.gold >= 100 && faction.food >= 40 && a.Count < 400)
            { faction.gold -= 100; faction.food -= 40; a.infantry += 30; }
            // Give the player two turns to establish the economy.
            if (turn < 3 || a.actions == 0) return;
            var enemies = provinces.Where(p => p.owner != owner && MoveError(owner, p.id).Length == 0).OrderBy(p => Preview(owner, p.id).defense).ToList();
            if (enemies.Count > 0 && Preview(owner, enemies[0].id).attack > Preview(owner, enemies[0].id).defense * 1.2)
                Move(owner, enemies[0].id);
            else
            {
                // Breadth-first route through friendly territory to any exposed border.
                var queue = new Queue<int>(); var previous = new Dictionary<int, int>();
                queue.Enqueue(a.province); previous[a.province] = -1;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    if (current != a.province && provinces.Any(p => p.owner != owner && Adjacent(current, p.id)))
                    { while (previous[current] != a.province) current = previous[current]; Move(owner, current); break; }
                    foreach (var p in land.Where(p => Adjacent(current, p.id) && !previous.ContainsKey(p.id)))
                    { previous[p.id] = current; queue.Enqueue(p.id); }
                }
            }
        }
        public string EndTurn()
        {
            if (Finished) return outcome;
            for (int i = 1; i < factions.Count && !Finished; i++) AI(i);
            if (Finished) return Record(outcome);
            for (int i = 0; i < factions.Count; i++)
            {
                Economy(i, out int gold, out int food); var f = factions[i];
                bool shortage = f.gold + gold < 0 || f.food + food < 0;
                f.gold = Math.Max(0, f.gold + gold); f.food = Math.Max(0, f.food + food);
                foreach (var p in provinces.Where(p => p.owner == i).ToList())
                {
                    bool stationed = At(p.id) != null;
                    int policy = (f.tax == 0 ? 5 : f.tax == 2 ? -8 : 0) + (p.focus == 3 ? 5 : p.focus == 2 ? -3 : 0);
                    p.order = Math.Max(0, Math.Min(100, p.order + (stationed ? 15 : p.garrison >= 55 ? 5 : -8) + policy - (shortage ? 20 : 0)));
                    if (p.focus == 3) p.garrison = Math.Min(180, p.garrison + 8);
                    if (p.order == 0) { p.owner = -1; p.order = 50; p.garrison = 50; Record(p.name + " · 반란으로 독립했습니다."); }
                }
                var supplied = Supplied(i);
                foreach (var army in armies.Where(a => a.owner == i))
                {
                    if (shortage) { Lose(army, (army.Count + 4) / 5); Record(OwnerName(i) + " · 자원 부족으로 병력 20% 이탈."); }
                    else if (!supplied.Contains(army.province)) { Lose(army, Math.Max(1, army.Count * (10 - f.logistics * 2) / 100)); Record(OwnerName(i) + " · 수도 보급로 단절로 병력 이탈."); }
                }
                if (i == 0) Record("결산 · 금 " + Signed(gold) + " / 식량 " + Signed(food));
            }
            foreach (var a in armies.Where(a => provinces[a.province].owner != a.owner)) Record(OwnerName(a.owner) + " · 주둔지 상실로 원정군이 해산했습니다.");
            armies.RemoveAll(a => a.Count == 0 || provinces[a.province].owner != a.owner);
            CheckDefeat();
            if (!Finished)
            {
                heldTurns = Territory(0) >= VictoryRegions ? heldTurns + 1 : 0;
                if (heldTurns >= 3) outcome = "승리 · 열두 지역을 지켜 새로운 연맹을 세웠습니다!";
            }
            turn++; foreach (var a in armies) a.actions = 2 + (factions[a.owner].logistics >= 2 ? 1 : 0);
            return Record(Finished ? outcome : turn + "턴 시작 · 군대 행동력이 회복되었습니다.");
        }
        public static string Signed(int n) => n >= 0 ? "+" + n : n.ToString();
        public string Record(string message)
        {
            lastReport = message; journal.Add("[" + turn + "턴] " + message);
            if (journal.Count > 60) journal.RemoveAt(0);
            return message;
        }
        public bool Valid()
        {
            if (version != 2 || turn < 1 || heldTurns < 0 || heldTurns > 3 || outcome == null || lastReport == null || journal == null || provinces == null || provinces.Count != RegionCount || factions == null || factions.Count != 3 || armies == null) return false;
            if (journal.Any(s => s == null) || factions.Any(f => f == null || f.name == null || f.gold < 0 || f.food < 0 || f.capital < 0 || f.capital >= RegionCount || f.tax < 0 || f.tax > 2 || f.agriculture < 0 || f.agriculture > 3 || f.commerce < 0 || f.commerce > 3 || f.logistics < 0 || f.logistics > 3)) return false;
            for (int i = 0; i < RegionCount; i++)
            {
                var p = provinces[i];
                if (p == null || p.id != i || p.name == null || p.owner < -1 || p.owner > 2 || p.terrain < 0 || p.terrain > 2 || p.order < 0 || p.order > 100 || p.garrison < 0 || p.garrison > 180 || p.farm < 0 || p.farm > 3 || p.market < 0 || p.market > 3 || p.barracks < 0 || p.barracks > 3) return false;
                if (p.road < 0 || p.road > 3 || p.walls < 0 || p.walls > 3 || p.focus < 0 || p.focus > 3) return false;
            }
            return armies.All(a => a != null && a.id >= 0 && a.id < nextArmyId && a.owner >= 0 && a.owner < 3 && a.province >= 0 && a.province < RegionCount && provinces[a.province].owner == a.owner && a.infantry >= 0 && a.archers >= 0 && a.cavalry >= 0 && a.Count > 0 && a.Count <= 800 && a.actions >= 0 && a.actions <= 3 && a.stance >= 0 && a.stance <= 2) && armies.Select(a => a.id).Distinct().Count() == armies.Count && armies.Select(a => a.province).Distinct().Count() == armies.Count && armies.Count(a => a.owner == 0) <= 3;
        }
    }
}
