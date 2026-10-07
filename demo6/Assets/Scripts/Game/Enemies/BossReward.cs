using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>보스 보상 한 묶음(3-8). 장비 수·가장 낮은 등급 보장·강화석·골드. 경험치(20U)는 처치 경험치 규칙(XpRules.KillUnits)이 따로 준다.</summary>
    public readonly struct BossRewardPlan
    {
        public readonly int Gear;
        /// <summary>희귀 이상 보장 수(첫 처치 1).</summary>
        public readonly int RareOrBetter;
        /// <summary>고급 이상만(다시 잡을 때).</summary>
        public readonly bool UncommonOrBetter;
        public readonly int Stones;
        public readonly int Gold;
        /// <summary>아이템 레벨 = 층 + 1.</summary>
        public readonly int ItemLevel;

        public BossRewardPlan(int gear, int rareOrBetter, bool uncommonOrBetter, int stones, int gold, int itemLevel)
        {
            Gear = gear;
            RareOrBetter = rareOrBetter;
            UncommonOrBetter = uncommonOrBetter;
            Stones = stones;
            Gold = gold;
            ItemLevel = itemLevel;
        }
    }

    /// <summary>
    /// 보스 보상 자리(기획/전투-보스-무기-다듬기-1차.md 3-8). 시험판 첫 처치 = 장비 3(희귀 이상 1 보장, iLv = 층 + 1)·강화석 10·골드 40·20U,
    /// 다시 잡을 때 = 장비 1(고급 이상)·강화석 4·골드 15(BossRules.Trial* 값, 묶음은 BossLoot가 정함).
    /// 전투 시험장에서는 보스에 NoReward를 켜고 기록만 남긴다(한 판 기록은 BossFightLog).
    /// 던전(묶음 7 오우거 굴): 꾸러미 보스 기록(BossLedger.RecordKill — 첫 처치 판단의 정답)을 적고, 프로필·원정·보스 id 씨앗으로 BossLoot를 굴려
    /// 쓰러진 자리에서 문 쪽으로 흩뿌린 뒤 DungeonEvents.BossDefeated를 알린다(BossArena가 문 열기·등잔·줄 끝 말뚝을 맡음).
    /// 보스의 일반 처치 보상은 Inventory가 빼므로 보스 보상은 이 한 곳에서만 나온다. 의뢰 셈은 CombatEvents.EnemyKilled 쪽이다.
    /// 룬(기획/세-무기-우클릭-소켓-1차.md 6-3): 첫 처치 확정(1000‰), 다시 처치 300‰. 같은 보스 씨앗에 따로 흐르는 난수(RuneRules.BossStream)라 장비 굴림은 그대로다.
    /// </summary>
    public static class BossReward
    {
        /// <summary>보상 굴림 흐름(다른 난수와 순서를 나눔: 처치 23, 궤짝 41, 치명 31, 전설 61).</summary>
        const ulong LootStream = 43;
        /// <summary>쓰러지고 보상이 튀어나오기까지(처치 느린 화면 동안, 게임 시간).</summary>
        const float ScatterDelay = 0.6f;

        static readonly HashSet<int> Cleared = new HashSet<int>();

        /// <summary>마지막으로 정한 보상 묶음(계측·시험 패널). 시험장(보상 없음)은 바꾸지 않는다.</summary>
        public static BossRewardPlan? LastPlan { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Cleared.Clear();
            LastPlan = null;
        }

        /// <summary>
        /// 이 층 보스를 이미 잡았는가(첫 처치 판단). 이번 플레이에서 잡은 층(정적), 또는 던전이고 2층(오우거 굴 층)이면 꾸러미 보스 기록(BossLedger).
        /// 던전의 실제 첫 처치 여부는 OnBossKilled가 BossLedger.RecordKill로 다시 정한다.
        /// </summary>
        public static bool HasCleared(int floor)
        {
            int f = FloorScaling.Clamp(floor);
            if (Cleared.Contains(f)) return true;
            return DungeonRoot.Instance && f == OgreDen.Floor && BossLedger.Cleared(ProfileCarry.Data, OgreDen.BossId);
        }

        /// <summary>시험판 보상 묶음(5·10층은 2차 7-3·3차 4-6 값으로 던전 단계에서 더한다).</summary>
        public static BossRewardPlan Plan(int floor, bool firstClear)
        {
            int iLv = FloorScaling.Clamp(floor) + 1;
            return firstClear
                ? new BossRewardPlan(BossRules.TrialFirstGear, 1, false, BossRules.TrialFirstStones, BossRules.TrialFirstGold, iLv)
                : new BossRewardPlan(BossRules.TrialRepeatGear, 0, true, BossRules.TrialRepeatStones, BossRules.TrialRepeatGold, iLv);
        }

        /// <summary>
        /// 보스가 쓰러졌다(firstClear = 정적 기록으로 본 첫 처치). 전투 시험장(NoReward)은 기록만.
        /// 던전이면 꾸러미에 처치를 적고(그 결과가 첫 처치 여부) 보상 묶음을 흩뿌린 뒤 BossDefeated를 알린다.
        /// </summary>
        public static void OnBossKilled(Enemy boss, bool firstClear)
        {
            if (!boss) return;
            Cleared.Add(FloorScaling.Clamp(boss.Floor));
            if (boss.NoReward) return;
            var root = DungeonRoot.Instance;
            if (!root)
            {
                // 던전 밖(보상 없음을 켜지 않은 시험)은 묶음만 정해 둔다.
                LastPlan = Plan(boss.Floor, firstClear);
                return;
            }
            var arena = BossArena.Instance;
            string bossId = arena ? arena.BossId : OgreDen.BossId;
            var carry = ProfileCarry.Ensure();
            bool first = BossLedger.RecordKill(carry, bossId, root.Expedition);
            LastPlan = Plan(boss.Floor, first);
            var rng = new Pcg32Random(BossLoot.Seed(carry.ProfileSalt, root.Expedition, bossId), LootStream);
            var inventory = Inventory.Instance;
            var bundle = BossLoot.Roll(boss.Floor, first, rng, inventory ? inventory.RollContext() : null);
            var runeRng = new Pcg32Random(BossLoot.Seed(carry.ProfileSalt, root.Expedition, bossId), RuneRules.BossStream);
            RuneRules.AddTo(bundle, first ? RuneSource.BossFirst : RuneSource.BossRepeat, runeRng);
            Vector2 pos = boss.Position;
            // 문 쪽으로 흩는다(보스방 안쪽 벽에 몰리지 않고 나가는 길에 놓이게). 방이 없으면 플레이어 쪽.
            Vector2 toward = arena ? arena.DoorPoint - pos : Vector2.zero;
            if (toward.sqrMagnitude < 0.0001f)
            {
                var player = PlayerController.Instance;
                toward = player ? player.Position - pos : Vector2.left;
            }
            LootSpawner.Spawn(bundle, pos, toward, ScatterDelay);
            SpawnDenNameplate(pos, carry);
            // 광업소 사무실 열쇠(묶음 3 나-9, 결정 D3 '나'): 아직 열쇠가 없으면 유해 곁에.
            KeyPickup.Place(pos + new Vector2(0.9f, -0.6f));
            DungeonEvents.RaiseBossDefeated(boss, first);
        }

        /// <summary>
        /// 보스를 쓰러뜨린 자리에 광부 명패(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 나-8, FloorRecipe.DenNameplate). 아직 받지 않았으면 쓰러뜨릴 때마다 놓는다
        /// (첫 처치 때 줍지 않고 떠나도 다음 처치에 다시 놓임). 받은 기록은 1층 명패와 같이 DungeonState → 꾸러미 OnceDone으로 간다.
        /// </summary>
        static void SpawnDenNameplate(Vector2 pos, CarryData carry)
        {
            var item = FloorRecipe.DenNameplate;
            if (carry == null || carry.OnceDone.Contains(item.Id)) return;
            var root = DungeonRoot.Instance;
            var state = root ? root.State : null;
            if (state != null && state.IsDone(item.Id)) return;
            var cell = root && root.World != null ? root.World.CellAt(pos) : null;
            var feature = new CellFeature { Kind = FeatureKind.Nameplate, Id = item.Id, Label = item.Label };
            state?.Register(item.Id, DiscoveryKind.Story, cell, pos, item.Label);
            StoryItem.Create(cell, feature, pos + new Vector2(0f, -0.8f));
        }
    }
}
