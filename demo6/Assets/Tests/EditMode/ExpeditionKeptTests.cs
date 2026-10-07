using Demo6.Core.Loot;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>도착 카드 '남은 것' 한 줄(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 가-4).</summary>
    public class ExpeditionKeptTests
    {
        [Test]
        public void NothingChangedIsNull()
        {
            var k = new ExpeditionKept { LevelFrom = 3, LevelTo = 3, AttackFrom = 200, AttackTo = 200, HpFrom = 900, HpTo = 900 };
            Assert.IsNull(k.Line());
        }

        [Test]
        public void FullLineInOrder()
        {
            var k = new ExpeditionKept
            {
                LevelFrom = 3, LevelTo = 4,
                AttackFrom = 210, AttackTo = 240,
                HpFrom = 900, HpTo = 950,
                Swapped = 2, NewLandings = 1, Survey = 2, Deaths = 1,
            };
            k.NotePicked(Grade.Common, "일반 장검");
            k.NotePicked(Grade.Common, "일반 두건");
            k.NotePicked(Grade.Uncommon, "고급 반지");
            k.NotePicked(Grade.Rare, "희귀 대검");
            k.OnceNames.Add("곡괭이");
            Assert.AreEqual(4, k.PickedTotal);
            Assert.AreEqual(
                "남은 것 — 장비 4(일반 2 · 고급 1 · 희귀 1: 희귀 대검) · 바꿔 낌 2 · 레벨 3 → 4 · 공격 210 → 240 · 최대 체력 900 → 950 · 새 승강장 1 · 측량 +2 · 쓰러짐 1 · 손에 넣은 것: 곡괭이",
                k.Line());
        }

        [Test]
        public void OnlyRareAndUpAreNamed()
        {
            var k = new ExpeditionKept();
            k.NotePicked(Grade.Uncommon, "고급 장검");
            k.NotePicked(Grade.Epic, "영웅 반지");
            k.NotePicked(Grade.Legendary, "전설 대검");
            Assert.AreEqual("남은 것 — 장비 3(고급 1 · 영웅 1 · 전설 1: 영웅 반지, 전설 대검)", k.Line());
        }

        [Test]
        public void ZeroStartStatsAreNotShown()
        {
            var k = new ExpeditionKept { AttackFrom = 0, AttackTo = 240, HpFrom = 0, HpTo = 900, Deaths = 2 };
            Assert.AreEqual("남은 것 — 쓰러짐 2", k.Line(), "시작 값을 모르면 공격·체력 줄을 쓰지 않음");
        }
    }
}
