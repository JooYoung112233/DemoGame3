using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    public sealed class ClipTimingTests
    {
        /// <summary>타격 프레임은 판정 순간에 정확히 시작하고, 그 직전에는 타격 전 프레임이다.</summary>
        [Test]
        public void KeyFrameStartsExactlyAtHitTime()
        {
            foreach (var w in WeaponPresets.All)
            foreach (var s in w.combo)
            {
                double hit = s.duration * s.hitMoment;
                ClipTiming.Recommend(s.duration, hit, out int frames, out int key);
                Assert.AreEqual(key, ClipTiming.TimedFrame(hit, s.duration, hit, frames, key), $"{w.displayName} {s.name}");
                Assert.AreEqual(key - 1, ClipTiming.TimedFrame(hit - 1e-6, s.duration, hit, frames, key), $"{w.displayName} {s.name}");
                Assert.AreEqual(frames - 1, ClipTiming.TimedFrame(s.duration - 1e-6, s.duration, hit, frames, key));
            }
        }

        [Test]
        public void TimedFrameWorksForAnyFrameCount()
        {
            for (int frames = 1; frames <= 16; frames++)
            for (int key = 0; key < frames; key++)
            {
                int last = -1;
                for (double t = 0; t <= 0.9; t += 0.005)
                {
                    int f = ClipTiming.TimedFrame(t, 0.9, 0.3, frames, key);
                    Assert.That(f, Is.InRange(0, frames - 1));
                    Assert.GreaterOrEqual(f, last, "프레임은 뒤로 가지 않는다");
                    last = f;
                }
                Assert.AreEqual(key, ClipTiming.TimedFrame(0.3, 0.9, 0.3, frames, key));
            }
        }

        [Test]
        public void LoopAndOnceFrames()
        {
            Assert.AreEqual(0, ClipTiming.LoopFrame(0, 12, 8));
            Assert.AreEqual(3, ClipTiming.LoopFrame(0.25, 12, 8));
            Assert.AreEqual(1, ClipTiming.LoopFrame(0.75, 12, 8));
            Assert.AreEqual(3, ClipTiming.OnceFrame(10, 12, 4));
            Assert.AreEqual(-1, ClipTiming.LoopFrame(1, 12, 0));
        }
    }
}
