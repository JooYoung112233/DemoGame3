using System;
using System.Text.RegularExpressions;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 시야와 문 1차(기획/시야와-문-1차.md 3·4·5장, 11-1 시험 9~17): 광선이 칸을 나가는 거리, 문턱 0.3, 새 칸 드러남 0.25초,
    /// 적 몸 각도, 부채꼴 들어옴·나감 6°, 보스방 예외, 살핌 75%, 등 뒤 기척 간격, 상수 관계와 화면 글.
    /// </summary>
    public sealed class SightRulesTests
    {
        const float Eps = 1e-4f;

        // 칸 28×16을 ClipPad 0.5만큼 넓힌 사각형(가운데 0, 0).
        const float XMin = -14.5f, YMin = -8.5f, XMax = 14.5f, YMax = 8.5f;

        // ── 9. ExitDistance ──

        [Test]
        public void ExitDistanceLeavesRectangle()
        {
            Assert.AreEqual(14.5f, Exit(0f, 0f, 1f, 0f), Eps, "가운데에서 오른쪽");
            Assert.AreEqual(8.5f, Exit(0f, 0f, 0f, 1f), Eps, "가운데에서 위");
            Assert.AreEqual(14.5f, Exit(0f, 0f, -1f, 0f), Eps, "왼쪽");
            Assert.AreEqual(8.5f, Exit(0f, 0f, 0f, -1f), Eps, "아래");
            float diag = (float)Math.Sqrt(0.5);
            Assert.AreEqual(8.5f * (float)Math.Sqrt(2.0), Exit(0f, 0f, diag, diag), 1e-3f, "대각선은 위 경계에서 먼저 나간다");
            Assert.AreEqual(2.5f * (float)Math.Sqrt(2.0), Exit(10f, 6f, diag, diag), 1e-3f, "가운데 밖에서 대각선");
            Assert.AreEqual(4.5f * (float)Math.Sqrt(2.0), Exit(10f, -2f, diag, -diag), 1e-3f, "오른쪽 아래로(오른쪽 경계에서 먼저 나간다)");
            Assert.AreEqual(4.5f, Exit(10f, 0f, 1f, 0f), Eps, "축과 나란함(y 성분 0)");
            Assert.AreEqual(11.5f, Exit(0f, 3f, 0f, -1f), Eps, "축과 나란함(x 성분 0)");
            Assert.AreEqual(14.5f, Exit(0f, 0f, 2f, 0f), Eps, "방향 길이와 관계없이 거리");
            Assert.AreEqual(0f, Exit(14.5f, 0f, -1f, 0f), Eps, "경계 위에서 시작");
            Assert.AreEqual(0f, Exit(0f, -8.5f, 0f, 1f), Eps, "아래 경계 위에서 시작");
            Assert.AreEqual(0f, Exit(20f, 0f, -1f, 0f), Eps, "밖에서 시작");
            Assert.AreEqual(0f, Exit(0f, 9f, 0f, -1f), Eps, "위 밖에서 시작");
            Assert.AreEqual(0f, Exit(0f, 0f, 0f, 0f), Eps, "방향 0");
        }

        // ── 10. KeepViewCell ──

        [Test]
        public void KeepViewCellUntilPastSwitchPad()
        {
            const float x0 = -14f, y0 = -8f, x1 = 14f, y1 = 8f;
            Assert.IsTrue(SightRules.KeepViewCell(0f, 0f, x0, y0, x1, y1), "안");
            Assert.IsTrue(SightRules.KeepViewCell(14.29f, 0f, x0, y0, x1, y1), "오른쪽 경계 밖 0.29");
            Assert.IsFalse(SightRules.KeepViewCell(14.31f, 0f, x0, y0, x1, y1), "오른쪽 경계 밖 0.31");
            Assert.IsTrue(SightRules.KeepViewCell(-14.29f, 0f, x0, y0, x1, y1), "왼쪽 0.29");
            Assert.IsFalse(SightRules.KeepViewCell(-14.31f, 0f, x0, y0, x1, y1), "왼쪽 0.31");
            Assert.IsTrue(SightRules.KeepViewCell(0f, 8.29f, x0, y0, x1, y1), "위 0.29");
            Assert.IsFalse(SightRules.KeepViewCell(0f, 8.31f, x0, y0, x1, y1), "위 0.31");
            Assert.IsTrue(SightRules.KeepViewCell(0f, -8.29f, x0, y0, x1, y1), "아래 0.29");
            Assert.IsFalse(SightRules.KeepViewCell(0f, -8.31f, x0, y0, x1, y1), "아래 0.31");
        }

        // ── 11. RevealRadius ──

        [Test]
        public void RevealRadiusGrowsOverQuarterSecond()
        {
            Assert.AreEqual(0.25f, SightRules.RevealSeconds, Eps);
            Assert.AreEqual(2.5f, SightRules.RevealRadius(0f, 2.5f, 14f), Eps, "0초 = 시작 반경");
            Assert.AreEqual(2.5f, SightRules.RevealRadius(-1f, 2.5f, 14f), Eps, "음수 시간 = 시작 반경");
            Assert.AreEqual(14f, SightRules.RevealRadius(0.25f, 2.5f, 14f), Eps, "0.25초 = 전체");
            Assert.AreEqual(14f, SightRules.RevealRadius(3f, 2.5f, 14f), Eps, "그 뒤 = 전체");
            Assert.AreEqual(10f, SightRules.RevealRadius(1f, 2.5f, 10f), Eps, "웅크리면 10까지");
            Assert.AreEqual(8.25f, SightRules.RevealRadius(0.125f, 2.5f, 14f), 1e-3f, "가운데 시간은 가운데 반경(smoothstep)");
            float last = float.NegativeInfinity;
            for (int i = 0; i <= 40; i++)
            {
                float r = SightRules.RevealRadius(i * 0.01f, 2.5f, 14f);
                Assert.GreaterOrEqual(r, last - Eps, $"{i * 0.01f}초: 줄어듦");
                Assert.That(r, Is.InRange(2.5f - Eps, 14f + Eps));
                last = r;
            }
        }

        // ── 12. AngularRadius ──

        [Test]
        public void AngularRadiusOfEnemyBody()
        {
            Assert.AreEqual(5.739f, SightRules.AngularRadius(0.5f, 5f), 0.01f, "반경 0.5·거리 5");
            Assert.AreEqual(180f, SightRules.AngularRadius(0.5f, 0.5f), Eps, "거리 = 반경");
            Assert.AreEqual(180f, SightRules.AngularRadius(0.5f, 0.2f), Eps, "거리 < 반경");
            Assert.AreEqual(0f, SightRules.AngularRadius(0f, 5f), Eps, "반경 0");
            Assert.Less(SightRules.AngularRadius(0.5f, 10f), SightRules.AngularRadius(0.5f, 5f), "멀수록 작다");
        }

        // ── 13. ConeSees ──

        [Test]
        public void ConeEntersAtPadAndLeavesSixDegreesLater()
        {
            const float half = 65f, body = 5.74f;
            float enter = half + 5f + body; // 75.74
            Assert.IsTrue(SightRules.ConeSees(false, enter - 0.05f, half, body), "들어옴 경계 안");
            Assert.IsFalse(SightRules.ConeSees(false, enter + 0.05f, half, body), "들어옴 경계 밖");
            Assert.IsTrue(SightRules.ConeSees(false, -(enter - 0.05f), half, body), "반대쪽도 같다");
            Assert.IsTrue(SightRules.ConeSees(true, enter + 0.05f, half, body), "보이던 적은 아직 보인다");
            Assert.IsTrue(SightRules.ConeSees(true, enter + 5.95f, half, body), "6° 안");
            Assert.IsFalse(SightRules.ConeSees(true, enter + 6.05f, half, body), "6° 넘게 벗어나면 숨는다");
            Assert.IsFalse(SightRules.ConeSees(true, 180f, half, 0f), "등 뒤");
            // 웅크리면 반각 40°.
            Assert.IsTrue(SightRules.ConeSees(false, 44.9f, 40f, 0f));
            Assert.IsFalse(SightRules.ConeSees(false, 45.1f, 40f, 0f));
            // 몸에 닿을 만큼 가까우면(몸 각도 180) 어느 쪽이든 부채꼴 안.
            Assert.IsTrue(SightRules.ConeSees(false, 180f, 40f, SightRules.AngularRadius(0.5f, 0.4f)));
        }

        // ── 14. 보스방 예외 ──

        [Test]
        public void OnlyBossRoomSkipsConeAndSweep()
        {
            foreach (PieceKind piece in Enum.GetValues(typeof(PieceKind)))
            {
                bool want = piece != PieceKind.BossRoom;
                Assert.AreEqual(want, SightRules.ConeApplies(piece), "부채꼴 " + piece);
                Assert.AreEqual(want, SightRules.SweepApplies(piece), "살핌 " + piece);
            }
            Assert.IsFalse(SightRules.ConeApplies(PieceKind.BossRoom));
            Assert.IsTrue(SightRules.ConeApplies(PieceKind.BossFront), "보스방 앞 쉼터는 보통 칸");
            Assert.IsFalse(SightRules.SweepApplies(PieceKind.BossRoom));
        }

        // ── 15. 살핌 ──

        [Test]
        public void SweepNeedsThreeQuarters()
        {
            Assert.AreEqual(0.75f, SightRules.SweepFraction, Eps);
            Assert.AreEqual(75, SightRules.SweepNeeded(100));
            Assert.IsFalse(SightRules.SweepDone(74, 100));
            Assert.IsTrue(SightRules.SweepDone(75, 100));
            Assert.IsTrue(SightRules.SweepDone(100, 100));
            Assert.AreEqual(0, SightRules.SweepNeeded(0));
            Assert.AreEqual(0, SightRules.SweepNeeded(-3));
            Assert.IsTrue(SightRules.SweepDone(0, 0), "걸을 수 있는 칸 0이면 늘 참");
            Assert.IsTrue(SightRules.SweepDone(-1, 0));
            Assert.AreEqual(1, SightRules.SweepNeeded(1));
            Assert.AreEqual(3, SightRules.SweepNeeded(3));
            Assert.AreEqual(3, SightRules.SweepNeeded(4));
            for (int w = 1; w <= 5000; w++)
            {
                int need = SightRules.SweepNeeded(w);
                Assert.GreaterOrEqual(need, w * 0.75 - 1e-6, $"{w}칸");
                Assert.Less(need - 1, w * 0.75, $"{w}칸: 올림");
                Assert.LessOrEqual(need, w);
            }
        }

        // ── 16. 등 뒤 기척 간격 ──

        [Test]
        public void BehindGapsByKind()
        {
            SightRules.BehindGap(MonsterKind.Rat, false, out float ratMin, out float ratMax);
            SightRules.BehindGap(MonsterKind.Boar, false, out float boarMin, out float boarMax);
            SightRules.BehindGap(MonsterKind.Archer, false, out float archerMin, out float archerMax);
            Assert.AreEqual(1.2f, ratMin, Eps, "굴쥐");
            Assert.AreEqual(1.8f, ratMax, Eps);
            Assert.AreEqual(1.6f, boarMin, Eps, "돌충이");
            Assert.AreEqual(2.4f, boarMax, Eps);
            Assert.AreEqual(2.0f, archerMin, Eps, "궁수");
            Assert.AreEqual(3.0f, archerMax, Eps);
            Assert.Less(ratMin, boarMin, "굴쥐 < 돌충이");
            Assert.Less(boarMin, archerMin, "돌충이 < 궁수");
            Assert.Less(ratMax, boarMax);
            Assert.Less(boarMax, archerMax);

            foreach (MonsterKind kind in Enum.GetValues(typeof(MonsterKind)))
            {
                SightRules.BehindGap(kind, true, out float eliteMin, out float eliteMax);
                Assert.AreEqual(1.4f, eliteMin, Eps, "정예 " + kind);
                Assert.AreEqual(2.0f, eliteMax, Eps, "정예 " + kind);
                SightRules.BehindGap(kind, false, out float min, out float max);
                Assert.Less(min, max, kind.ToString());
                Assert.Greater(min, 0f, kind.ToString());
            }

            Assert.AreEqual(0.15f, SightRules.BehindFirstMin, Eps, "새로 든 적 첫 소리");
            Assert.AreEqual(0.35f, SightRules.BehindFirstMax, Eps);
            Assert.Less(SightRules.BehindFirstMin, SightRules.BehindFirstMax);
            Assert.Less(SightRules.BehindFirstMax, ratMin, "첫 소리는 다음 소리보다 빠르다");
            Assert.AreEqual(2, SightRules.BehindVoices);
            Assert.AreEqual(0.2f, SightRules.BehindScanSeconds, Eps);
            Assert.AreEqual(0.3f, SightRules.WakeCueSpacing, Eps);
        }

        // ── 17. 상수 ──

        [Test]
        public void ConstantsKeepTheirRelations()
        {
            Assert.Less(SightRules.SwitchPad, SightRules.ClipPad, "문턱은 보이는 곳 안에서 바뀐다");
            Assert.LessOrEqual(SightRules.ClipPad, 0.5f, "벽 두께 1의 절반 이하");
            Assert.AreEqual(0f, SightRules.NearEnemyRadius, Eps, "몸 둘레 원은 적을 보여 주지 않는다");
            Assert.Greater(SightRules.RevealOnHurtSeconds, SightRules.RevealOnHitSeconds, "맞으면 0.8 > 치면 0.5");
            Assert.AreEqual(0.8f, SightRules.RevealOnHurtSeconds, Eps);
            Assert.AreEqual(0.5f, SightRules.RevealOnHitSeconds, Eps);
            // 물결 2 고침: 근접 공격만 찾는다(화살·덫 자리 가까이 있던 다른 적이 드러나지 않게).
            Assert.AreEqual(0.3f, SightRules.HurtRevealReach, Eps);
            Assert.AreEqual(5f, SightRules.ConeEnterPad, Eps);
            Assert.AreEqual(6f, SightRules.ConeExitExtra, Eps);
            Assert.Less(SightRules.BehindRange, SightRules.WakeCueRange);
            Assert.Less(SightRules.WakeCueRange, SightRules.TelegraphCueRange);
            Assert.AreEqual(0.65f, SightRules.BeetlePitch, Eps);
            Assert.AreEqual(1.3f, SightRules.WakeCueLevel, Eps);
            Assert.AreEqual(1.2f, SightRules.TelegraphCueLevel, Eps);

            Assert.AreEqual("살폈다", SightRules.SweptWord);
            Assert.AreEqual("등 뒤는 보이지 않는다. 긁는 소리가 나면 돌아봐라.", SightRules.AmbushHint);
            foreach (var text in new[] { SightRules.SweptWord, SightRules.AmbushHint })
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(text));
                Assert.IsFalse(Regex.IsMatch(text, "[A-Za-z]"), "영어 글자: " + text);
            }
        }

        static float Exit(float ox, float oy, float dx, float dy) =>
            SightRules.ExitDistance(ox, oy, dx, dy, XMin, YMin, XMax, YMax);
    }
}
