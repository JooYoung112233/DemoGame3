using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 그림 프레임 고르기. 공격 동작은 '타격 프레임'이 코드의 판정 순간에 정확히 시작하도록,
    /// 판정 전 구간에는 타격 전 프레임을, 판정 뒤 구간에는 타격 프레임부터 끝까지를 고르게 나눈다.
    /// 그래서 그림이 몇 장이든 동작 길이·판정 시점(데이터)이 바뀌어도 맞는다.
    /// </summary>
    public static class ClipTiming
    {
        /// <summary>권장 기준 프레임레이트. 명세의 프레임 수는 이 값으로 계산한다.</summary>
        public const double ReferenceFps = 12.0;

        /// <summary>반복 클립(대기·이동).</summary>
        public static int LoopFrame(double time, double fps, int count)
        {
            if (count <= 0) return -1;
            if (fps <= 0 || time <= 0) return 0;
            long i = (long)Math.Floor(time * fps);
            return (int)(i % count);
        }

        /// <summary>한 번 재생하고 마지막 프레임에 머무는 클립(구르기·쓰러짐).</summary>
        public static int OnceFrame(double time, double fps, int count)
        {
            if (count <= 0) return -1;
            if (fps <= 0 || time <= 0) return 0;
            return (int)Math.Min(count - 1, Math.Floor(time * fps));
        }

        /// <summary>
        /// 동작 길이에 맞춰 늘이고 줄이는 클립. keyFrame이 있으면 hitTime에 정확히 그 프레임이 시작한다.
        /// keyFrame이 없거나 범위를 벗어나면 동작 길이에 고르게 나눈다.
        /// </summary>
        public static int TimedFrame(double t, double duration, double hitTime, int count, int keyFrame)
        {
            if (count <= 0) return -1;
            if (duration <= 0) return count - 1;
            if (t <= 0) return 0;
            if (t >= duration) return count - 1;
            bool aligned = keyFrame >= 0 && keyFrame < count && hitTime > 0 && hitTime < duration;
            if (!aligned) return Clamp((int)Math.Floor(t / duration * count), 0, count - 1);
            if (t < hitTime)
            {
                if (keyFrame == 0) return 0;
                return Clamp((int)Math.Floor(t / hitTime * keyFrame), 0, keyFrame - 1);
            }
            int after = count - keyFrame;
            double u = (t - hitTime) / (duration - hitTime);
            return Clamp(keyFrame + (int)Math.Floor(u * after), keyFrame, count - 1);
        }

        /// <summary>명세용: 기준 프레임레이트에서 권장 전체 프레임 수와 타격 프레임 번호(0부터).</summary>
        public static void Recommend(double duration, double hitTime, out int frames, out int keyFrame)
        {
            int windup = Math.Max(1, (int)Math.Round(hitTime * ReferenceFps));
            frames = Math.Max(windup + 2, (int)Math.Round(duration * ReferenceFps));
            keyFrame = windup;
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
