using System;
using System.Collections.Generic;
using Demo6.Core.Random;

namespace Demo6.Core.Dungeon
{
    /// <summary>무리 후보 표 한 줄(기획/1-2층-탐험-맛-1차.md 4-2): 화면 무리 이름, 구성, 상태, 뽑힐 몫.</summary>
    public sealed class PackOption
    {
        /// <summary>표 안의 줄 이름(시험·기록용, 화면에 보이지 않음).</summary>
        public string Id = "";
        /// <summary>화면 무리 이름(9장, 글은 ExploreText).</summary>
        public string Label = "";
        /// <summary>
        /// 순찰을 못 할 때(이웃 빈 칸도 열린 문 둘도 없음, 4-3) '먹는 중'으로 바꾸며 쓰는 이름. 순찰이 아닌 줄은 Label과 같다.
        /// </summary>
        public string FallbackLabel = "";
        public int Boars;
        public int Archers;
        public int Rats;
        /// <summary>뽑힐 몫(표의 '뽑힐 몫'). 정예 줄은 뽑지 않고 자리에 고정하므로 0.</summary>
        public int Weight;
        public GroupState State = GroupState.Sleep;
        /// <summary>정예 무리('단단한 정예 돌충이').</summary>
        public bool Elite;
    }

    /// <summary>
    /// 층 상한(4-2, 첫 공터 무리 포함). 1층 돌충이 2~4·굴쥐 9~13, 2층 궁수 3~5·돌충이 1~3·강한 적(돌충이 + 궁수) 7까지·굴쥐 4~8,
    /// 둘 다 순찰 1까지·같은 이름 2까지. 정예는 돌충이 1로 센다.
    /// </summary>
    public readonly struct PackCaps
    {
        public readonly int MinBoars;
        public readonly int MaxBoars;
        public readonly int MinArchers;
        public readonly int MaxArchers;
        /// <summary>강한 적(돌충이 + 궁수) 상한.</summary>
        public readonly int MaxStrong;
        public readonly int MinRats;
        public readonly int MaxRats;
        public readonly int MaxPatrols;
        /// <summary>같은 이름 무리 상한(첫 공터 무리는 세지 않음).</summary>
        public readonly int MaxSame;

        public PackCaps(int minBoars, int maxBoars, int minArchers, int maxArchers, int maxStrong, int minRats, int maxRats, int maxPatrols, int maxSame)
        {
            MinBoars = minBoars;
            MaxBoars = maxBoars;
            MinArchers = minArchers;
            MaxArchers = maxArchers;
            MaxStrong = maxStrong;
            MinRats = minRats;
            MaxRats = maxRats;
            MaxPatrols = maxPatrols;
            MaxSame = maxSame;
        }
    }

    /// <summary>
    /// 다시 연 층 무리 후보 표(기획/1-2층-탐험-맛-1차.md 4-2). 첫 공터를 뺀 무리마다 이 표에서 구성과 상태(잠·먹는 중·순찰)를 고른다.
    /// 무리 수와 놓이는 공터는 생성기가 정한 그대로이고, 첫 공터 무리는 층 예산 그대로다(FloorSpice가 부른다).
    /// 정예 무리('단단한 정예 돌충이 + 굴쥐 2', 잠)는 뽑지 않고 정예 자리(eliteSlot)에 고정한다.
    /// 표는 부를 때마다 새로 만든다(정적 값을 두지 않아 도메인 다시 불러오기를 꺼도 고친 값이 남지 않음).
    /// </summary>
    public static class PackTable
    {
        /// <summary>한 자리를 다시 뽑는 최대 횟수(4-2 '고르기는 최대 20번 다시 뽑는다').</summary>
        public const int MaxRedraws = 20;

        /// <summary>
        /// 그 층의 후보 표(표 차례 고정). 1층은 첫 공터를 뺀 3무리, 2층은 2무리에 쓴다. 2층보다 깊은 층은 2층 표를 쓴다.
        /// </summary>
        public static IReadOnlyList<PackOption> Options(int floor) => floor <= 1 ? FloorOne() : FloorTwo();

        /// <summary>정예 무리 줄: 단단한 정예 돌충이 1 + 굴쥐 2(잠). 1·2층 같다.</summary>
        public static PackOption EliteOption(int floor) => new PackOption
        {
            Id = "elite-boar",
            Label = ExploreText.PackElite,
            FallbackLabel = ExploreText.PackElite,
            Boars = 1,
            Rats = 2,
            Weight = 0,
            State = GroupState.Sleep,
            Elite = true,
        };

        /// <summary>그 층의 상한(첫 공터 포함). 1층은 궁수가 없어 궁수 0~0, 강한 적 상한은 돌충이 상한과 같다.</summary>
        public static PackCaps Caps(int floor) => floor <= 1
            ? new PackCaps(minBoars: 2, maxBoars: 4, minArchers: 0, maxArchers: 0, maxStrong: 4, minRats: 9, maxRats: 13, maxPatrols: 1, maxSame: 2)
            : new PackCaps(minBoars: 1, maxBoars: 3, minArchers: 3, maxArchers: 5, maxStrong: 7, minRats: 4, maxRats: 8, maxPatrols: 1, maxSame: 2);

        /// <summary>
        /// 첫 공터를 뺀 무리 slots개의 구성을 고른다(차례 = 부르는 쪽의 무리 차례). first는 첫 공터 무리(상한을 셀 때 넣는다).
        /// eliteSlot(0 ≤ eliteSlot &lt; slots)은 정예 줄로 고정하고, 나머지 자리는 차례대로 몫에 따라 뽑는다.
        /// 뽑은 줄로는 남은 자리를 어떻게 채워도 상한을 못 맞추면 그 줄을 빼고 다시 뽑는다(최대 MaxRedraws번, 뺀 줄은 다시 나오지 않으므로
        /// 맞출 수 있는 줄이 하나라도 있으면 늘 찾는다 — 맞는 줄끼리의 비율은 몫 그대로). 끝내 못 맞추면 그 자리는 표의 첫 줄이다.
        /// 같은 난수 순서면 같은 결과다. rng가 null이면 모든 자리가 첫 줄(정예 자리는 정예).
        /// </summary>
        public static PackOption[] Pick(int floor, int slots, GroupMix first, int eliteSlot, IRandom rng)
        {
            if (slots <= 0) return Array.Empty<PackOption>();
            var options = Options(floor);
            var picks = new PackOption[slots];
            bool hasElite = eliteSlot >= 0 && eliteSlot < slots;
            if (hasElite) picks[eliteSlot] = EliteOption(floor);
            for (int i = 0; i < slots; i++)
            {
                if (hasElite && i == eliteSlot) continue;
                PackOption chosen = null;
                if (rng != null)
                {
                    var left = new List<PackOption>(options);
                    for (int draw = 0; draw < MaxRedraws && left.Count > 0 && chosen == null; draw++)
                    {
                        var o = Draw(left, rng);
                        picks[i] = o;
                        if (CanFinish(floor, first, picks, i + 1, options)) chosen = o;
                        else left.Remove(o);
                    }
                }
                picks[i] = chosen ?? options[0];
            }
            return picks;
        }

        /// <summary>
        /// 첫 공터 무리 + 고른 줄들이 층 상한 안인가: 돌충이(정예는 1로 셈)·궁수·강한 적·굴쥐 범위, 순찰 수, 같은 이름 수(첫 공터 무리는 이름을 세지 않음).
        /// 빈 줄(null)은 건너뛴다.
        /// </summary>
        public static bool WithinCaps(int floor, GroupMix first, IReadOnlyList<PackOption> picks)
        {
            var caps = Caps(floor);
            Tally(first, picks, out int boars, out int archers, out int rats, out int patrols, out int same);
            return boars >= caps.MinBoars && boars <= caps.MaxBoars &&
                   archers >= caps.MinArchers && archers <= caps.MaxArchers &&
                   boars + archers <= caps.MaxStrong &&
                   rats >= caps.MinRats && rats <= caps.MaxRats &&
                   patrols <= caps.MaxPatrols && same <= caps.MaxSame;
        }

        // ── 표(4-2, 9장 이름) ──

        static PackOption[] FloorOne() => new[]
        {
            Row("rat-swarm", ExploreText.PackRatSwarm, null, 0, 0, 4, GroupState.Eat, 3),
            Row("boar-sleep", ExploreText.PackSleepingBoar, null, 1, 0, 2, GroupState.Sleep, 3),
            Row("boar-eat", ExploreText.PackEatingBoar, null, 1, 0, 2, GroupState.Eat, 3),
            Row("rat-patrol", ExploreText.PackRatPatrol, ExploreText.PackRatSwarm, 0, 0, 4, GroupState.Patrol, 2),
            Row("two-boars", ExploreText.PackTwoBoars, null, 2, 0, 0, GroupState.Sleep, 1),
            Row("boar-patrol", ExploreText.PackBoarPatrol, ExploreText.PackEatingBoar, 1, 0, 2, GroupState.Patrol, 1),
        };

        static PackOption[] FloorTwo() => new[]
        {
            Row("boar-archer-eat", ExploreText.PackEatingBoarArcher, null, 1, 1, 2, GroupState.Eat, 3),
            Row("boar-archer-sleep", ExploreText.PackSleepingBoarArcher, null, 1, 1, 2, GroupState.Sleep, 3),
            Row("two-archers", ExploreText.PackTwoArchers, null, 0, 2, 2, GroupState.Eat, 2),
            Row("boar-patrol", ExploreText.PackBoarPatrol, ExploreText.PackEatingBoar, 1, 0, 2, GroupState.Patrol, 2),
            // 순찰을 못 하면 '궁수 1 + 굴쥐 3' 먹는 무리 — 9장 이름 가운데 구성이 맞는 '궁수와 굴쥐'.
            Row("archer-patrol", ExploreText.PackArcherPatrol, ExploreText.PackArcherRat, 0, 1, 3, GroupState.Patrol, 1),
            Row("rat-swarm", ExploreText.PackRatSwarm, null, 0, 0, 5, GroupState.Eat, 1),
        };

        static PackOption Row(string id, string label, string fallback, int boars, int archers, int rats, GroupState state, int weight) => new PackOption
        {
            Id = id,
            Label = label,
            FallbackLabel = fallback ?? label,
            Boars = boars,
            Archers = archers,
            Rats = rats,
            State = state,
            Weight = weight,
        };

        // ── 뽑기 ──

        /// <summary>몫에 따라 한 줄을 뽑는다(난수 하나). 몫이 모두 0이면 첫 줄.</summary>
        static PackOption Draw(List<PackOption> left, IRandom rng)
        {
            int total = 0;
            foreach (var o in left) total += Math.Max(0, o.Weight);
            if (total <= 0) return left[0];
            int roll = rng.NextInt(0, total);
            foreach (var o in left)
            {
                int w = Math.Max(0, o.Weight);
                if (roll < w) return o;
                roll -= w;
            }
            return left[left.Count - 1];
        }

        /// <summary>from부터 비어 있는 자리를 표 줄로 채워 상한을 맞출 수 있는가(picks는 되돌려 놓는다).</summary>
        static bool CanFinish(int floor, GroupMix first, PackOption[] picks, int from, IReadOnlyList<PackOption> options)
        {
            int next = from;
            while (next < picks.Length && picks[next] != null) next++;
            if (next >= picks.Length) return WithinCaps(floor, first, picks);
            if (OverMax(floor, first, picks)) return false;
            foreach (var o in options)
            {
                picks[next] = o;
                bool ok = CanFinish(floor, first, picks, next + 1, options);
                picks[next] = null;
                if (ok) return true;
            }
            return false;
        }

        /// <summary>이미 채운 줄만으로 상한(최댓값)을 넘었는가.</summary>
        static bool OverMax(int floor, GroupMix first, PackOption[] picks)
        {
            var caps = Caps(floor);
            Tally(first, picks, out int boars, out int archers, out int rats, out int patrols, out int same);
            return boars > caps.MaxBoars || archers > caps.MaxArchers || boars + archers > caps.MaxStrong || rats > caps.MaxRats ||
                   patrols > caps.MaxPatrols || same > caps.MaxSame;
        }

        static void Tally(GroupMix first, IReadOnlyList<PackOption> picks, out int boars, out int archers, out int rats, out int patrols, out int same)
        {
            boars = first != null ? first.Boars : 0;
            archers = first != null ? first.Archers : 0;
            rats = first != null ? first.Rats : 0;
            patrols = first != null && first.State == GroupState.Patrol ? 1 : 0;
            same = 0;
            if (picks == null) return;
            var names = new Dictionary<string, int>();
            foreach (var p in picks)
            {
                if (p == null) continue;
                boars += p.Elite ? 1 : p.Boars;
                archers += p.Archers;
                rats += p.Rats;
                if (p.State == GroupState.Patrol) patrols++;
                string key = p.Label ?? "";
                names.TryGetValue(key, out int n);
                names[key] = n + 1;
                same = Math.Max(same, n + 1);
            }
        }
    }
}
