using System;
using System.Collections.Generic;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>전설 고유 효과 3종(장비 문서 6장). 차례는 StatSheet.LegendaryRollPermille 칸 번호다(바꾸지 않음).</summary>
    public enum LegendaryEffect
    {
        /// <summary>연쇄 번개: 기본공격 동작마다 한 번 굴림.</summary>
        ChainLightning,
        /// <summary>불꽃 발자국: 전투 중 1.2유닛 걸을 때마다 불길.</summary>
        FlameSteps,
        /// <summary>연쇄 폭발: 일반 몬스터 처치 시 폭발.</summary>
        ChainBlast,
    }

    /// <summary>효과가 나오는 부위 하나와 묶음 안 비율(‰).</summary>
    public readonly struct LegendaryPartShare
    {
        public readonly GearPart Part;
        public readonly int Permille;

        public LegendaryPartShare(GearPart part, int permille)
        {
            Part = part;
            Permille = permille;
        }
    }

    /// <summary>전설 효과 정의: id, 이름(카드 첫 옵션 줄), 나오는 부위 묶음. 수치는 Core/Combat/LegendRules(꾸러미 ⑥)가 가진다.</summary>
    public sealed class LegendaryDef
    {
        public readonly string Id;
        public readonly LegendaryEffect Effect;
        /// <summary>효과 이름(연쇄 번개·불꽃 발자국·연쇄 폭발). 칭호는 쓰지 않는다(5-4 결정).</summary>
        public readonly string Name;
        public readonly LegendaryPartShare[] Parts;

        public LegendaryDef(string id, LegendaryEffect effect, string name, params LegendaryPartShare[] parts)
        {
            Id = id;
            Effect = effect;
            Name = name;
            Parts = parts ?? Array.Empty<LegendaryPartShare>();
        }
    }

    /// <summary>
    /// 전설 효과 3종과 부위 묶음(장비 문서 6장): 번개 = 무기 700·장갑 300, 발자국 = 장화 600·갑옷 400, 폭발 = 목걸이 500·투구 300·반지 200.
    /// 정해지는 순서: 등급 → (전설이면) 효과 1/3 → 그 효과의 부위 묶음 안 비율 → 종류 1/3 → 굴림 → 옵션 3줄.
    /// 전설 하나가 나올 부위(전설 기준): 무기 23%, 장화 20%, 목걸이 17%, 갑옷 13%, 장갑 10%, 투구 10%, 반지 7%.
    /// 겹침: 같은 효과가 두 장비에 있으면 굴림이 높은 하나만 작동한다(같으면 먼저 낀 자리). 다른 효과끼리는 함께 작동한다.
    /// id·효과 차례·Active 결과 모양은 꾸러미 글·StatSheet가 쓰므로 바꾸지 않는다.
    /// </summary>
    public static class LegendaryTable
    {
        public const int Count = 3;
        public const string ChainLightningId = "leg_chain_lightning";
        public const string FlameStepsId = "leg_flame_steps";
        public const string ChainBlastId = "leg_chain_blast";

        static LegendaryPartShare S(GearPart part, int permille) => new LegendaryPartShare(part, permille);

        static readonly LegendaryDef[] Defs =
        {
            new LegendaryDef(ChainLightningId, LegendaryEffect.ChainLightning, "연쇄 번개", S(GearPart.Weapon, 700), S(GearPart.Gloves, 300)),
            new LegendaryDef(FlameStepsId, LegendaryEffect.FlameSteps, "불꽃 발자국", S(GearPart.Boots, 600), S(GearPart.Armor, 400)),
            new LegendaryDef(ChainBlastId, LegendaryEffect.ChainBlast, "연쇄 폭발", S(GearPart.Amulet, 500), S(GearPart.Helm, 300), S(GearPart.Ring, 200)),
        };

        public static IReadOnlyList<LegendaryDef> All => Defs;

        public static LegendaryDef Get(LegendaryEffect effect) => Defs[Math.Max(0, Math.Min(Count - 1, (int)effect))];

        /// <summary>id로 찾는다. 없으면 null.</summary>
        public static LegendaryDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var d in Defs)
                if (d.Id == id) return d;
            return null;
        }

        /// <summary>그 효과가 이 부위에서 나올 수 있는가.</summary>
        public static bool CanAppearOn(LegendaryEffect effect, GearPart part)
        {
            foreach (var s in Get(effect).Parts)
                if (s.Part == part) return true;
            return false;
        }

        /// <summary>효과 1/3(난수 1번).</summary>
        public static LegendaryEffect RollEffect(IRandom rng) => (LegendaryEffect)rng.NextInt(0, Count);

        /// <summary>그 부위에서 나올 수 있는 효과(효과 차례, 새 목록). 일곱 부위 모두 1개 이상이다(6장 에셋 검사 50번).</summary>
        public static List<LegendaryEffect> EffectsOn(GearPart part)
        {
            var list = new List<LegendaryEffect>(Count);
            foreach (var d in Defs)
                if (CanAppearOn(d.Effect, part)) list.Add(d.Effect);
            return list;
        }

        /// <summary>
        /// 부위 제한이 있을 때의 효과(난수 1번): onlyParts 가운데 한 곳에라도 나올 수 있는 효과 k개 가운데 1/k(8-1 빈 자리 채우기·3층 보장).
        /// onlyParts가 비었거나 null이거나 맞는 효과가 없으면 RollEffect와 같다.
        /// </summary>
        public static LegendaryEffect RollEffect(IReadOnlyList<GearPart> onlyParts, IRandom rng)
        {
            if (onlyParts == null || onlyParts.Count == 0) return RollEffect(rng);
            var candidates = new List<LegendaryEffect>(Count);
            foreach (var d in Defs)
                foreach (var p in onlyParts)
                    if (CanAppearOn(d.Effect, p))
                    {
                        candidates.Add(d.Effect);
                        break;
                    }
            if (candidates.Count == 0) return RollEffect(rng);
            return candidates[rng.NextInt(0, candidates.Count)];
        }

        /// <summary>그 효과의 부위 묶음 안 비율로 부위 하나(난수 1번).</summary>
        public static GearPart RollPart(LegendaryEffect effect, IRandom rng) => RollPart(effect, null, rng);

        /// <summary>
        /// 그 효과의 부위 묶음 안 비율로 부위 하나(난수 1번). onlyParts가 있으면 그 안의 부위만 남긴 비율로 고른다.
        /// 남는 부위가 없으면 제한 없이 고른다.
        /// </summary>
        public static GearPart RollPart(LegendaryEffect effect, IReadOnlyList<GearPart> onlyParts, IRandom rng)
        {
            var parts = Get(effect).Parts;
            bool limited = onlyParts != null && onlyParts.Count > 0;
            int total = 0;
            foreach (var s in parts)
                if (!limited || Contains(onlyParts, s.Part)) total += s.Permille;
            if (limited && total <= 0)
            {
                limited = false;
                foreach (var s in parts) total += s.Permille;
            }
            int r = rng.NextInt(0, Math.Max(1, total));
            GearPart last = parts.Length > 0 ? parts[parts.Length - 1].Part : GearPart.Weapon;
            foreach (var s in parts)
            {
                if (limited && !Contains(onlyParts, s.Part)) continue;
                last = s.Part;
                r -= s.Permille;
                if (r < 0) return s.Part;
            }
            return last;
        }

        static bool Contains(IReadOnlyList<GearPart> parts, GearPart part)
        {
            foreach (var p in parts)
                if (p == part) return true;
            return false;
        }

        /// <summary>효과 세기 굴림(‰, 0~1000 = 수치 범위 안 위치). 이미 가진 효과면 위쪽 절반(500~1000, 2차 그대로).</summary>
        public static int RollStrength(bool alreadyOwned, IRandom rng) => alreadyOwned ? rng.NextInt(500, 1001) : rng.NextInt(0, 1001);

        /// <summary>
        /// 작동하는 효과(겹침 규칙 적용). 결과는 길이 3, 효과 차례 칸에 굴림‰, 없으면 -1.
        /// </summary>
        public static int[] Active(IEnumerable<GearItem> equipped)
        {
            var result = new int[Count];
            for (int i = 0; i < Count; i++) result[i] = -1;
            if (equipped == null) return result;
            foreach (var item in equipped)
            {
                if (item == null || !item.IsLegendary) continue;
                var def = Get(item.LegendaryId);
                if (def == null) continue;
                int e = (int)def.Effect;
                if (item.LegendaryRollPermille > result[e]) result[e] = item.LegendaryRollPermille;
            }
            return result;
        }

        /// <summary>
        /// 이 장비의 효과가 겹침으로 꺼져 있는가(카드 회색 '겹침 — 작동 안 함'). 같은 효과를 가진 다른 낀 장비의 굴림이 더 높으면 true.
        /// 같은 굴림이면 equipped 차례에서 먼저 나온 것이 작동한다.
        /// </summary>
        public static bool IsSuppressed(GearItem item, IEnumerable<GearItem> equipped)
        {
            if (item == null || !item.IsLegendary || equipped == null) return false;
            bool seenSelf = false;
            foreach (var other in equipped)
            {
                if (other == null) continue;
                if (ReferenceEquals(other, item))
                {
                    seenSelf = true;
                    continue;
                }
                if (other.LegendaryId != item.LegendaryId) continue;
                if (other.LegendaryRollPermille > item.LegendaryRollPermille) return true;
                if (other.LegendaryRollPermille == item.LegendaryRollPermille && !seenSelf) return true;
            }
            return false;
        }
    }
}
