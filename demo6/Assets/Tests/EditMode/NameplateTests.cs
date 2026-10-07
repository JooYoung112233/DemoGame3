using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>명패 늘리기(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 나-8).</summary>
    public class NameplateTests
    {
        [Test]
        public void ThreeNameplatesInTheTrial()
        {
            Assert.AreEqual(FeatureKind.Nameplate, FloorRecipe.FindOnceItem("f1.T.nameplate").Kind);
            Assert.AreEqual(FeatureKind.Nameplate, FloorRecipe.FindOnceItem("f2.nameplate").Kind, "2층 명패");
            Assert.AreSame(FloorRecipe.DenNameplate, FloorRecipe.FindOnceItem(FloorRecipe.DenNameplate.Id), "보스 자리 명패");
            Assert.IsTrue(FloorRecipe.IsOnceItem(FloorRecipe.DenNameplate.Id));
            int n = 0;
            foreach (var o in FloorRecipe.AllOnceItems())
                if (o.Kind == FeatureKind.Nameplate)
                {
                    n++;
                    Assert.IsNotNull(FloorRecipe.NameplateName(o.Id), o.Id + " 새겨진 이름");
                }
            Assert.AreEqual(3, n);
        }

        [Test]
        public void InscriptionParticleFollowsLastSyllable()
        {
            Assert.AreEqual("명패에 '갑돌'이라 새겨져 있다.", TownScript.NameplateInscription("갑돌"));
            Assert.AreEqual("명패에 '덕배'라 새겨져 있다.", TownScript.NameplateInscription("덕배"));
            Assert.IsNull(TownScript.NameplateInscription(null));
        }

        [Test]
        public void SecondAndThirdTagsGetGateReactionsAndMissingLine()
        {
            var c = CarryData.NewProfile(0x55UL);
            TownSave.MarkSceneSeen(c, TownScript.OpeningId);
            c.OnceDone.Add("f1.T.nameplate");
            TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxTagF1));
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxTag2));

            c.OnceDone.Add("f2.nameplate");
            c.Expedition++;
            var r = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(1, r.NewTags);
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxTag2), "둘째 명패 반응");
            CollectionAssert.Contains(TownArrivalRules.CardLines(c, r), "돌아오지 못한 사람 12 — 소식 2");

            c.OnceDone.Add(FloorRecipe.DenNameplate.Id);
            c.Expedition++;
            TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxTag3), "셋째 명패 반응");
            Assert.AreEqual(3, TownSave.Tags(c));
        }
    }
}
