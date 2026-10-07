using Demo6.Core.Save;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 설정 값 한 벌(기획/저장-처음화면-멈춤창-1차.md 6장, 시험 12-5의 1~4): 기본값 일곱, 설정 글 왕복(첫 줄 'settings v1'·LF만),
    /// 모르는 키·빠진 키·첫 줄이 다르거나 빈 글, 소리 크기 0.05·화면 흔들림 0.1 단위 맞추기.
    /// </summary>
    public sealed class GameSettingsTests
    {
        const float Eps = 1e-5f;

        static void AssertDefaults(GameSettings s, string why)
        {
            Assert.IsTrue(s.Sound, why + ": 소리 켬");
            Assert.AreEqual(0.6f, s.Volume, Eps, why + ": 소리 크기 60%");
            Assert.AreEqual(1f, s.Shake, Eps, why + ": 화면 흔들림 100%");
            Assert.IsTrue(s.DamageNumbers, why + ": 피해 숫자 켬");
            Assert.IsTrue(s.HitStop, why + ": 타격 멈춤 켬");
            Assert.IsTrue(s.Footprints, why + ": 피 발자국 켬");
            Assert.IsFalse(s.InstantText, why + ": 대사 글 바로 보이기 끔");
        }

        // ── 12-5의 1: 기본값 ──

        [Test]
        public void DefaultsMatchCurrentCode()
        {
            AssertDefaults(new GameSettings(), "새 설정");
            Assert.AreEqual("settings v1", GameSettings.Header);
        }

        // ── 12-5의 2: 왕복 ──

        [Test]
        public void DefaultTextMatchesDocShape()
        {
            string expected = "settings v1\nsound=1\nvolume=0.60\nshake=1.00\ndamagenumbers=1\nhitstop=1\nfootprints=1\ninstanttext=0\n";
            Assert.AreEqual(expected, new GameSettings().ToText(), "6-3 모양 그대로");
        }

        [Test]
        public void ToTextParseRoundTrip()
        {
            var s = new GameSettings
            {
                Sound = false,
                Volume = 0.35f,
                Shake = 0.2f,
                DamageNumbers = false,
                HitStop = false,
                Footprints = false,
                InstantText = true,
            };
            string text = s.ToText();
            StringAssert.StartsWith("settings v1\n", text, "첫 줄 'settings v1'");
            Assert.IsFalse(text.Contains("\r"), "줄바꿈은 LF만");
            Assert.IsTrue(text.EndsWith("\n") && !text.EndsWith("\n\n"), "끝에 줄바꿈 하나");
            StringAssert.Contains("volume=0.35\n", text, "소수는 0.00");
            StringAssert.Contains("shake=0.20\n", text);

            var back = GameSettings.Parse(text);
            Assert.IsFalse(back.Sound);
            Assert.AreEqual(0.35f, back.Volume, Eps);
            Assert.AreEqual(0.2f, back.Shake, Eps);
            Assert.IsFalse(back.DamageNumbers);
            Assert.IsFalse(back.HitStop);
            Assert.IsFalse(back.Footprints);
            Assert.IsTrue(back.InstantText);
            Assert.IsTrue(back.SameAs(s));
            Assert.AreEqual(text, back.ToText(), "다시 쓴 글도 같다");
        }

        [Test]
        public void ParseAcceptsCrlfAndSpaces()
        {
            var s = GameSettings.Parse("settings v1\r\n sound = 0 \r\nvolume= 0.25\r\ninstanttext=true\r\n");
            Assert.IsFalse(s.Sound, "키·값 앞뒤 빈칸은 무시");
            Assert.AreEqual(0.25f, s.Volume, Eps);
            Assert.IsTrue(s.InstantText, "true도 켬으로 읽음");
        }

        // ── 12-5의 3: 모르는 키·빠진 키·머리가 다름 ──

        [Test]
        public void UnknownKeysIgnoredMissingKeysDefault()
        {
            var s = GameSettings.Parse("settings v1\nhitstop=0\nfov=90\nkeys=wasd\nnoequals\n=1\n");
            Assert.IsFalse(s.HitStop, "아는 키는 읽음");
            Assert.IsTrue(s.Sound, "빠진 키는 기본");
            Assert.AreEqual(0.6f, s.Volume, Eps);
            Assert.AreEqual(1f, s.Shake, Eps);
            Assert.IsTrue(s.DamageNumbers);
            Assert.IsTrue(s.Footprints);
            Assert.IsFalse(s.InstantText);
        }

        [Test]
        public void UnreadableValuesKeepDefault()
        {
            var s = GameSettings.Parse("settings v1\nsound=maybe\nvolume=abc\nshake=NaN\nfootprints=2\n");
            AssertDefaults(s, "읽지 못한 값");
        }

        [Test]
        public void WrongHeaderOrEmptyGivesDefaults()
        {
            AssertDefaults(GameSettings.Parse(null), "null");
            AssertDefaults(GameSettings.Parse(""), "빈 글");
            AssertDefaults(GameSettings.Parse("\n\n"), "빈 줄만");
            AssertDefaults(GameSettings.Parse("settings v2\nsound=0\nvolume=0.10\n"), "판본이 다름");
            AssertDefaults(GameSettings.Parse("testpreset v1\nsound=0\n"), "다른 글");
            AssertDefaults(GameSettings.Parse("sound=0\nvolume=0.10\n"), "머리 없음");
            Assert.IsFalse(GameSettings.Parse("  settings v1  \nsound=0\n").Sound, "첫 줄은 앞뒤 빈칸을 빼고 본다");
        }

        // ── 12-5의 4: 자르기와 단위 ──

        [Test]
        public void NormalizeClampsToZeroOne()
        {
            var s = new GameSettings { Volume = 1.7f, Shake = -0.4f }.Normalize();
            Assert.AreEqual(1f, s.Volume, Eps, "크기는 1까지");
            Assert.AreEqual(0f, s.Shake, Eps, "흔들림은 0부터");
            s = new GameSettings { Volume = -3f, Shake = 3f }.Normalize();
            Assert.AreEqual(0f, s.Volume, Eps);
            Assert.AreEqual(1f, s.Shake, Eps, "시험 패널 손잡이 3배도 설정은 1까지");
            s = new GameSettings { Volume = float.NaN, Shake = float.NaN }.Normalize();
            Assert.AreEqual(0.6f, s.Volume, Eps, "숫자가 아니면 기본");
            Assert.AreEqual(1f, s.Shake, Eps);
        }

        [Test]
        public void NormalizeSnapsVolumeToFivePercent()
        {
            Assert.AreEqual(0.05f, new GameSettings { Volume = 0.07f }.Normalize().Volume, Eps);
            Assert.AreEqual(0.1f, new GameSettings { Volume = 0.08f }.Normalize().Volume, Eps);
            Assert.AreEqual(0.6f, new GameSettings { Volume = 0.6f }.Normalize().Volume, Eps, "이미 맞으면 그대로");
            Assert.AreEqual(0.65f, new GameSettings { Volume = 0.649f }.Normalize().Volume, Eps);
            Assert.AreEqual(0f, new GameSettings { Volume = 0.02f }.Normalize().Volume, Eps);
            Assert.AreEqual(1f, new GameSettings { Volume = 0.99f }.Normalize().Volume, Eps);
        }

        [Test]
        public void NormalizeSnapsShakeToTenPercent()
        {
            Assert.AreEqual(0.3f, new GameSettings { Shake = 0.34f }.Normalize().Shake, Eps);
            Assert.AreEqual(0.4f, new GameSettings { Shake = 0.36f }.Normalize().Shake, Eps);
            // 0.25·0.75는 float로 딱 떨어지는 한가운데 값이다(0.65f는 0.6499…라 아래로 감).
            Assert.AreEqual(0.3f, new GameSettings { Shake = 0.25f }.Normalize().Shake, Eps, "한가운데는 위로");
            Assert.AreEqual(0.8f, new GameSettings { Shake = 0.75f }.Normalize().Shake, Eps);
            Assert.AreEqual(0f, new GameSettings { Shake = 0.04f }.Normalize().Shake, Eps);
        }

        [Test]
        public void ParseNormalizesValues()
        {
            var s = GameSettings.Parse("settings v1\nvolume=0.62\nshake=2.50\n");
            Assert.AreEqual(0.6f, s.Volume, Eps, "읽은 값도 5% 단위");
            Assert.AreEqual(1f, s.Shake, Eps, "읽은 값도 1까지");
        }

        [Test]
        public void CloneAndSameAs()
        {
            var a = new GameSettings { Volume = 0.3f, InstantText = true };
            var b = a.Clone();
            Assert.AreNotSame(a, b);
            Assert.IsTrue(a.SameAs(b));
            b.Footprints = false;
            Assert.IsTrue(a.Footprints, "복사본을 고쳐도 원래 것은 그대로");
            Assert.IsFalse(a.SameAs(b));
            Assert.IsFalse(a.SameAs(null));
            Assert.IsFalse(new GameSettings().SameAs(new GameSettings { Volume = 0.65f }));
        }
    }
}
