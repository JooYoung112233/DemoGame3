using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 걸음과 어둠(기획/전투-보스-무기-다듬기-1차.md 1-2·1-3, 결정 ① = B)과 웅크리기(결정 ③) 값.
    /// 1-3 '공정 확인' 표 ①~⑧을 지킨다: 느려지고 어두워져도 굴쥐를 걸어서 떼어낼 수 있고, 잠든 적·궁수·근접 사거리보다 빛이 넉넉하다.
    /// 어둠은 2026-10-04 사용자 결정("주변 등 2번째처럼 유지")으로 등잔이 늘 같은 반경이고(같은 날 "등잔 크기 좀더키워줘"로 6.0 / 9.0 → 7.5 / 11.0),
    /// 싸움 판정이 오가도 빛이 출렁이지 않는다(⑥·⑧).
    /// </summary>
    public sealed class ExplorePaceTests
    {
        const float Eps = 1e-5f;

        // ── 1-2 걸음 ──

        [Test]
        public void DefaultsAreOptionB()
        {
            Assert.AreEqual(0.80f, ExplorePace.DefaultPlayerScale, Eps, "결정 ①: 나 0.80");
            Assert.AreEqual(0.93f, ExplorePace.DefaultEnemyScale, Eps, "결정 ①: 적 0.93 = 0.80 ÷ 0.86");
            Assert.AreEqual(ExplorePace.PlayerScaleB, ExplorePace.DefaultPlayerScale, Eps);
            Assert.AreEqual(ExplorePace.EnemyScaleB, ExplorePace.DefaultEnemyScale, Eps);
        }

        [Test]
        public void OptionsFollowCompareTable()
        {
            var o = ExplorePace.Options;
            Assert.AreEqual(5, o.Length, "M0a·지금·A·B·C");
            Assert.AreEqual("M0a 값", o[0].Name);
            Assert.AreEqual("지금", o[1].Name);
            Assert.AreEqual("A", o[2].Name);
            Assert.AreEqual("B", o[3].Name);
            Assert.AreEqual("C", o[4].Name);
            Assert.AreEqual(0.86f, o[1].PlayerScale, Eps);
            Assert.AreEqual(1f, o[1].EnemyScale, Eps);
            Assert.IsTrue(o[1].FastEverywhere, "'지금'은 처음 가는 칸에서도 빠른 걸음(예전 탐험 걸음)");
            Assert.IsFalse(o[2].FastEverywhere, "A는 빠른 걸음을 아는 길에서만");
            Assert.IsFalse(o[3].FastEverywhere);
            Assert.AreEqual(0.74f, o[4].PlayerScale, Eps);
            Assert.AreEqual(0.86f, o[4].EnemyScale, Eps);
        }

        [Test]
        public void WalkSpeedsMatchDocTable()
        {
            // 1-2 표: 처음 가는 칸·싸움 걸음 4.24(5.3 × 0.80), 아는 길 5.20(6.5 × 0.80), 굴쥐 3.91(4.2 × 0.93).
            Assert.AreEqual(4.24, ExplorePace.EquipWalk * (double)ExplorePace.DefaultPlayerScale, 0.005);
            Assert.AreEqual(5.20, ExplorePace.KnownPathScaled(ExplorePace.DefaultPlayerScale), 0.005);
            Assert.AreEqual(3.91, ExplorePace.RatSpeed * (double)ExplorePace.DefaultEnemyScale, 0.01);
            Assert.AreEqual(12f, ExplorePace.EnemyRange, Eps);
            Assert.AreEqual(3f, ExplorePace.QuietSeconds, Eps);
        }

        /// <summary>① 모든 안에서 나 ÷ 굴쥐 ≥ 1.05(굴쥐를 걸어서 떼어낼 수 있다). B는 지금과 같은 1.085.</summary>
        [Test]
        public void PlayerOutpacesRatInEveryOption()
        {
            foreach (var o in ExplorePace.Options)
                Assert.GreaterOrEqual(ExplorePace.PlayerOverRat(o.PlayerScale, o.EnemyScale), ExplorePace.MinPlayerOverRat, o.Name);
            Assert.AreEqual(1.085, ExplorePace.PlayerOverRat(ExplorePace.PlayerScaleB, ExplorePace.EnemyScaleB), 0.005);
            Assert.AreEqual(1.085, ExplorePace.PlayerOverRat(ExplorePace.DefaultPlayerScale, ExplorePace.DefaultEnemyScale), 0.005);
        }

        // ── 1-3 어둠 ──

        /// <summary>1-3 값(2026-10-04 사용자 결정으로 바뀜): 등잔은 늘 7.5 / 11.0, 어둠은 빛 밖 0.06·덮개 0.70·비네트 0.42·잡티 0.20(어두운 쪽 값 고정).</summary>
        [Test]
        public void DarknessValuesMatchDoc()
        {
            Assert.AreEqual(0.06f, ExplorePace.AmbientDark, Eps);
            Assert.AreEqual(0.70f, ExplorePace.VeilAlpha, Eps);
            Assert.AreEqual(7.5f, ExplorePace.LampBright, Eps);
            Assert.AreEqual(11.0f, ExplorePace.LampDim, Eps);
            Assert.AreEqual(7.5f, ExplorePace.BrightRadius(0), Eps);
            Assert.AreEqual(11.0f, ExplorePace.DimRadius(0), Eps);
            Assert.AreEqual(0.42f, ExplorePace.VignetteIntensity, Eps);
            Assert.AreEqual(0.20f, ExplorePace.GrainIntensity, Eps);
        }

        /// <summary>② 흐린 반경 ≥ 잠든 적 앞 감지 6 + 여유 0.5(몸을 보기 전에 먼저 들키지 않게). 11.0이라 여유 5.0.</summary>
        [Test]
        public void ExploreDimCoversSleeperDetect()
        {
            Assert.GreaterOrEqual(ExplorePace.DimRadius(0), ExplorePace.SleepDetectFront + ExplorePace.DetectMargin);
        }

        /// <summary>③ 흐린 ≥ 9.0, 밝은 ≥ 6.0(궁수 거리 두기 5~7, 멧돼지 돌진 거리 3~7보다 2 이상 큼). 이제 탐험 중에도 같다.</summary>
        [Test]
        public void CombatLightCoversArcherAndBoar()
        {
            Assert.GreaterOrEqual(ExplorePace.DimRadius(0), 9.0f);
            Assert.GreaterOrEqual(ExplorePace.BrightRadius(0), 6.0f);
            Assert.LessOrEqual(ExplorePace.DimRadius(ExplorePace.WickLevels - 1), ExplorePace.ViewRadius, "심지를 다 올려도 등잔은 서 있는 시야 반경 안");
        }

        /// <summary>
        /// ④ 서 있을 때 시야 반경 14 ≥ 눈 두 점 12, 화살 사거리 12.
        /// 웅크림 시야 10은 플레이어가 스스로 고른 좁은 시야라 이 규칙의 예외로 둔다(대신 느리고 조용해 잠든 무리를 덜 깨운다, 결정 ③).
        /// </summary>
        [Test]
        public void StandingViewCoversEyesAndArrows()
        {
            Assert.AreEqual(14f, ExplorePace.ViewRadius, Eps);
            Assert.AreEqual(2.5f, ExplorePace.NearRadius, Eps);
            Assert.AreEqual(65f, ExplorePace.ConeHalfAngle, Eps);
            Assert.GreaterOrEqual(ExplorePace.ViewRadius, ExplorePace.EyesRange);
            Assert.GreaterOrEqual(ExplorePace.ViewRadius, ExplorePace.ArrowRange);
        }

        /// <summary>⑤ 밝은 반경 7.5 &gt; 근접 최대 사거리 3.5(장검 찌르기 3.4, 대검 내려찍기 1.3 + 2.2).</summary>
        [Test]
        public void ExploreBrightExceedsMeleeReach()
        {
            Assert.Greater(ExplorePace.BrightRadius(0), ExplorePace.MeleeMaxReach);
        }

        /// <summary>
        /// ⑥ 등잔은 처음부터 싸움 반경이라 적이 깨는 0.5초·궁수 조준 0.8초 전에 빛이 커지기를 기다리지 않는다.
        /// 싸움 섞기(이름표만 옅게)는 켜질 때 0.25초, 꺼질 때 약 1.4초.
        /// </summary>
        [Test]
        public void CombatLightGrowsBeforeWakeAndAim()
        {
            Assert.GreaterOrEqual(ExplorePace.DimRadius(0), 9.0f, "탐험 중에도 싸움 반경");
            Assert.AreEqual(0.25f, ExplorePace.CombatBlendInSeconds, Eps);
            Assert.Less(ExplorePace.CombatBlendInSeconds, ExplorePace.WakeDelay);
            Assert.Less(ExplorePace.CombatBlendInSeconds, ExplorePace.ArcherAim);
            Assert.AreEqual(1.43f, ExplorePace.CombatBlendOutSeconds, 0.01f);

            // 한 걸음 식: 0.25초면 다 켜지고, 0.24초에는 아직 덜 켜진다. 끝나면 1.4초 뒤에도 조금 남고 1.43초에 다 꺼진다.
            Assert.AreEqual(1f, Steps(0f, true, 0.25f), 1e-4f);
            Assert.Less(Steps(0f, true, 0.24f), 1f);
            Assert.Greater(Steps(1f, false, 1.40f), 0f);
            Assert.AreEqual(0f, Steps(1f, false, 1.43f), 1e-4f);
            Assert.AreEqual(1f, ExplorePace.StepCombatBlend(1f, true, 0.5f), Eps, "1을 넘지 않는다");
            Assert.AreEqual(0f, ExplorePace.StepCombatBlend(0f, false, 0.5f), Eps, "0 아래로 내려가지 않는다");
            Assert.AreEqual(0.3f, ExplorePace.StepCombatBlend(0.3f, true, 0f), Eps, "멈춘 프레임(dt 0)은 그대로");
        }

        /// <summary>⑦ 심지 단계 0 → 1 → 2에서 밝은·흐린 반경이 늘어난다(등잔 7.5 / 11.0에 +1.0/+2.0 · +1.5/+3.0).</summary>
        [Test]
        public void WickLevelsGrowRadiusInOrder()
        {
            for (int w = 1; w < ExplorePace.WickLevels; w++)
            {
                Assert.Greater(ExplorePace.BrightRadius(w), ExplorePace.BrightRadius(w - 1), $"밝은 심지 {w}");
                Assert.Greater(ExplorePace.DimRadius(w), ExplorePace.DimRadius(w - 1), $"흐린 심지 {w}");
                Assert.Greater(ExplorePace.DimRadius(w), ExplorePace.BrightRadius(w), "흐린 반경이 밝은 반경보다 크다");
            }
            Assert.AreEqual(3, ExplorePace.WickLevels);
            Assert.AreEqual(8.5f, ExplorePace.BrightRadius(1), Eps);
            Assert.AreEqual(12.5f, ExplorePace.DimRadius(1), Eps);
            Assert.AreEqual(9.5f, ExplorePace.BrightRadius(2), Eps);
            Assert.AreEqual(14.0f, ExplorePace.DimRadius(2), Eps);
            // 범위 밖 심지 단계는 0~2로 자른다.
            Assert.AreEqual(ExplorePace.DimRadius(2), ExplorePace.DimRadius(9), Eps);
            Assert.AreEqual(ExplorePace.DimRadius(0), ExplorePace.DimRadius(-1), Eps);
        }

        /// <summary>
        /// ⑧ 싸움 판정은 짧게 오가도 켜진 채로 있다(2026-10-04 깜빡임 수정): 켜는 거리 12 / 끄는 거리 14,
        /// 보이는 깬 적이 없어진 뒤 3초 동안 켜 둔다. 회피 귀환·한 방 처치·무리가 0.8초 늦게 깨는 사이에 꺼졌다 켜지지 않는다.
        /// </summary>
        [Test]
        public void CombatFlagHoldsThroughShortGaps()
        {
            Assert.AreEqual(12f, ExplorePace.CombatRange(false), Eps, "꺼져 있으면 12 안에서 켬");
            Assert.AreEqual(14f, ExplorePace.CombatRange(true), Eps, "켜져 있으면 14 밖으로 나가야 끔");
            Assert.Greater(ExplorePace.CombatExitRange, ExplorePace.EnemyRange);
            Assert.AreEqual(3f, ExplorePace.CombatHoldSeconds, Eps);

            float hold = -1f;
            Assert.IsFalse(ExplorePace.StepCombatHold(false, 0f, ref hold), "처음엔 꺼짐(시각 0에서도)");
            Assert.IsTrue(ExplorePace.StepCombatHold(true, 1f, ref hold));
            Assert.AreEqual(4f, hold, Eps);
            Assert.IsTrue(ExplorePace.StepCombatHold(false, 3.99f, ref hold), "3초 안은 켜 둠");
            Assert.IsFalse(ExplorePace.StepCombatHold(false, 4f, ref hold), "3초가 지나면 끔");

            // 0.5초마다 보였다 안 보였다 해도(회피 귀환 ↔ 다시 알림, 처치 ↔ 무리 늦게 깸) 10초 내내 켜진 채다.
            hold = -1f;
            bool everOff = false;
            for (int i = 0; i < 1000; i++)
            {
                float t = i * 0.01f;
                bool seen = ((int)(t / 0.5f)) % 2 == 0;
                if (!ExplorePace.StepCombatHold(seen, t, ref hold)) everOff = true;
            }
            Assert.IsFalse(everOff, "짧게 오가는 판정에 꺼지지 않는다");
        }

        /// <summary>
        /// 바라보는 쪽(시야 부채꼴): 커서가 몸 0.6 안으로 들어오면 방향을 붙잡고, 1.0 밖으로 나가야 다시 커서를 따른다.
        /// 0.6 둘레에서 손이 떨려도 방향이 뒤집히지 않는다(2026-10-04 깜빡임 수정).
        /// </summary>
        [Test]
        public void CursorNearHasHysteresis()
        {
            Assert.AreEqual(0.6f, ExplorePace.CursorHoldEnter, Eps);
            Assert.AreEqual(1.0f, ExplorePace.CursorHoldExit, Eps);
            Assert.IsFalse(ExplorePace.CursorNear(false, 0.8f), "멀리서 0.8까지 와도 아직 커서를 따름");
            Assert.IsTrue(ExplorePace.CursorNear(false, 0.59f), "0.6 안이면 붙잡음");
            Assert.IsTrue(ExplorePace.CursorNear(true, 0.8f), "붙잡은 뒤에는 0.8에서도 붙잡은 채");
            Assert.IsTrue(ExplorePace.CursorNear(true, 1.0f));
            Assert.IsFalse(ExplorePace.CursorNear(true, 1.01f), "1.0 밖이면 놓음");
            Assert.IsTrue(ExplorePace.CursorNear(true, float.NaN), "NaN이면 그대로");
            Assert.IsFalse(ExplorePace.CursorNear(false, float.NaN));

            // 0.55~0.85 사이를 떨면 처음 한 번만 바뀐다.
            bool near = false;
            int flips = 0;
            for (int i = 0; i < 200; i++)
            {
                float d = 0.7f + 0.15f * (float)System.Math.Sin(i * 0.7);
                bool next = ExplorePace.CursorNear(near, d);
                if (next != near) flips++;
                near = next;
            }
            Assert.AreEqual(1, flips);
        }

        /// <summary>
        /// 구르기·내딛기·넉백에 카메라가 늦게 따라와도 화면 가운데에 둔 커서는 붙잡은 방향을 놓지 않는다(2026-10-04 검토).
        /// 몸에서 잰 거리만 보면 구르기 끝(카메라 지연 약 1.2)에 1.0을 넘어 놓쳤고, 그 순간 부채꼴이 뒤로 쓸렸다.
        /// </summary>
        [Test]
        public void CursorNearIgnoresCameraLag()
        {
            Assert.AreEqual(0.4f, ExplorePace.CursorNearDistance(0.4f, 1.5f), Eps);
            Assert.AreEqual(0.1f, ExplorePace.CursorNearDistance(1.3f, 0.1f), Eps);
            Assert.AreEqual(0.7f, ExplorePace.CursorNearDistance(float.NaN, 0.7f), Eps);
            Assert.AreEqual(0.7f, ExplorePace.CursorNearDistance(0.7f, float.NaN), Eps);
            Assert.IsTrue(float.IsNaN(ExplorePace.CursorNearDistance(float.NaN, float.NaN)));

            // 구르기 3.5유닛 / 0.22초를 카메라(따라오기 12, 지수)가 따라간다. 커서는 화면 가운데(카메라 중심)에 그대로 둔다.
            const float dt = 1f / 120f;
            float body = 0f, cam = 0f, maxLag = 0f;
            bool near = true, bodyOnly = true, bodyOnlyLost = false;
            for (int i = 0; i < 120; i++)
            {
                if (i * dt < 0.22f) body += 3.5f / 0.22f * dt;
                cam += (body - cam) * (1f - (float)System.Math.Exp(-12f * dt));
                float lag = body - cam;
                maxLag = System.Math.Max(maxLag, lag);
                near = ExplorePace.CursorNear(near, ExplorePace.CursorNearDistance(lag, 0f));
                bodyOnly = ExplorePace.CursorNear(bodyOnly, lag);
                Assert.IsTrue(near, "화면 가운데 커서는 구르는 내내 붙잡은 채");
                if (!bodyOnly) bodyOnlyLost = true;
            }
            Assert.Greater(maxLag, ExplorePace.CursorHoldExit, "구르기 끝 카메라 지연은 1.0을 넘는다");
            Assert.IsTrue(bodyOnlyLost, "몸에서 잰 거리만 보면 놓친다(고치기 전)");
        }

        // ── 웅크리기(결정 ③) ──

        [Test]
        public void CrouchValuesMatchDecision()
        {
            Assert.AreEqual(0.55f, CrouchRules.MoveScale, Eps);
            Assert.AreEqual(0.3f, CrouchRules.NoiseScale, Eps);
            Assert.AreEqual(40f, CrouchRules.ConeHalfAngle, Eps);
            Assert.AreEqual(10f, CrouchRules.ViewRadius, Eps);
            Assert.AreEqual(2.0f, CrouchRules.NearRadius, Eps);
            Assert.AreEqual(0.15f, CrouchRules.BlendSeconds, Eps);
            Assert.AreEqual("웅크림", CrouchRules.HudLabel);
            // 웅크리면 시야가 서 있을 때보다 좁다(반각·반경·몸 둘레 모두).
            Assert.Less(CrouchRules.ConeHalfAngle, ExplorePace.ConeHalfAngle);
            Assert.Less(CrouchRules.ViewRadius, ExplorePace.ViewRadius);
            Assert.Less(CrouchRules.NearRadius, ExplorePace.NearRadius);
        }

        [Test]
        public void CrouchQuietsHearing()
        {
            Assert.AreEqual(0.9f, CrouchRules.HearRadius(3f, true), Eps, "반경으로 재는 소리 3 → 0.9(등 뒤 감지는 몸 사이 거리로 잼, 아래 시험)");
            Assert.AreEqual(3f, CrouchRules.HearRadius(3f, false), Eps);
            Assert.AreEqual(3.6f, CrouchRules.HearRadius(12f, true), Eps, "큰 소리 반경 12 → 3.6");
            Assert.AreEqual(1f, CrouchRules.NoiseFactor(false), Eps);
            Assert.AreEqual(0.5f, CrouchRules.NoiseFactor(true, 0.5f), Eps, "시험 패널 배율");
        }

        /// <summary>
        /// 웅크림 등 뒤 감지는 몸 사이 거리에 소음 배율을 곱한다: '덜 깸'이지 '절대 안 깸'이 아니다(마무리 검토 ②).
        /// 몸이 닿는 거리(두 반지름 합, 플레이어 0.4)는 언제나 감지 안이고, 서 있으면 바탕 3 그대로다.
        /// </summary>
        [Test]
        public void CrouchHearingKeepsBodyContact()
        {
            const float player = 0.4f;
            // 굴쥐 0.25·궁수 0.3·멧돼지 0.55·오우거 1.2(MonsterRules 지름의 절반).
            float[] radius = { 0.25f, 0.3f, 0.55f, 1.2f };
            float[] expect = { 1.355f, 1.39f, 1.565f, 2.02f };
            for (int i = 0; i < radius.Length; i++)
            {
                float contact = radius[i] + player;
                float hear = CrouchRules.HearCenterDistance(3f, contact, CrouchRules.NoiseScale);
                Assert.AreEqual(expect[i], hear, 1e-4f, $"반지름 {radius[i]}");
                Assert.Greater(hear, contact, "몸이 닿으면 웅크려도 듣는다");
                Assert.Less(hear, 3f, "웅크리면 서 있을 때보다 덜 듣는다");
                Assert.AreEqual(3f, CrouchRules.HearCenterDistance(3f, contact, 1f), Eps, "서 있으면 바탕 그대로");
                Assert.AreEqual(contact, CrouchRules.HearCenterDistance(3f, contact, 0f), Eps, "배율 0이면 몸이 닿을 때만");
            }
            // 문서 4-1 '숨죽인 걸음'(등 뒤 3 → 1.5)과 비슷한 범위다.
            Assert.AreEqual(1.5f, CrouchRules.HearCenterDistance(3f, 0.65f, 0.3f), 0.2f);
            Assert.AreEqual(1.0f, CrouchRules.HearCenterDistance(1.0f, 1.6f, 0.3f), Eps, "바탕이 몸보다 작으면 바탕 그대로");
        }

        [Test]
        public void CrouchBlendTakesBlendSeconds()
        {
            float b = 0f;
            for (int i = 0; i < 15; i++) b = CrouchRules.StepBlend(b, true, 0.01f);
            Assert.AreEqual(1f, b, 1e-4f, "0.15초에 다 웅크림");
            Assert.Less(CrouchRules.StepBlend(0f, true, 0.1f), 1f);
            Assert.AreEqual(0f, CrouchRules.StepBlend(1f, false, 0.2f), Eps);
            Assert.AreEqual(0.4f, CrouchRules.StepBlend(0.4f, true, 0f), Eps);
            Assert.AreEqual(14f, CrouchRules.Blend(ExplorePace.ViewRadius, CrouchRules.ViewRadius, 0f), Eps);
            Assert.AreEqual(10f, CrouchRules.Blend(ExplorePace.ViewRadius, CrouchRules.ViewRadius, 1f), Eps);
            Assert.AreEqual(52.5f, CrouchRules.Blend(ExplorePace.ConeHalfAngle, CrouchRules.ConeHalfAngle, 0.5f), Eps);
        }

        static float Steps(float blend, bool combat, float seconds)
        {
            const float dt = 0.01f;
            int n = (int)System.Math.Round(seconds / dt);
            for (int i = 0; i < n; i++) blend = ExplorePace.StepCombatBlend(blend, combat, dt);
            return blend;
        }
    }
}
