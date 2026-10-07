using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 배경음·먼 소리를 코드로 합성한다(기획/다크판타지-분위기-1차.md '소리': 낮게 웅웅거리는 배경음,
    /// 멀리서 물방울·쇠사슬·삐걱임·짐승 소리). 소리 파일 없이 만드는 자리·타이밍 견본이며 리소스 단계에서 녹음으로 바꾼다.
    /// 던전을 열 때 DungeonAudio가 한 번 만들고, 닫힐 때 지운다(매 프레임 할당 없음).
    /// 전투·보스·무기 다듬기 1차(기획/전투-보스-무기-다듬기-1차.md 묶음 4) 목소리: 잠든 무리 기척(굴쥐 긁기·멧돼지 콧김·궁수 시위 삐걱·정예 숨소리),
    /// 겁먹은 굴쥐 찍찍(LurkSounds가 만듦), 낮은 체력 거친 숨·심장, 시체 밟기 질척(DungeonAudio가 만듦).
    /// </summary>
    public static class DungeonAmbienceSynth
    {
        /// <summary>낮은 소리가 중심이라 표본율을 절반(22,050Hz)으로 해서 만드는 시간과 메모리를 줄인다.</summary>
        public const int SampleRate = 22050;
        /// <summary>물방울 소리 갈래 수(높이가 조금씩 다르다).</summary>
        public const int DripVariants = 3;
        /// <summary>굴쥐 긁기 갈래 수(긁는 횟수·높이가 다르다).</summary>
        public const int ScratchVariants = 2;
        /// <summary>찍찍 갈래 수(울음 수·높이가 다르다).</summary>
        public const int SqueakVariants = 2;
        /// <summary>거친 숨 한 바퀴(초). 4-3 '거친 숨(2.2초 주기)'. 처음과 끝이 조용해 반복 클립으로 이음매 없이 돈다.</summary>
        public const float PlayerBreathSeconds = 2.2f;
        /// <summary>심장 '쿵-쿵' 사이(초). 첫 '쿵'은 표본 0에서 시작한다(맥동 박자 맞춤).</summary>
        public const float HeartDubDelay = 0.26f;

        const float TwoPi = 2f * Mathf.PI;
        /// <summary>느린 출렁임을 다시 계산하는 간격(표본, 약 3ms). 수 초 주기라 이만큼 끊어도 들리지 않는다.</summary>
        const int LfoBlock = 64;

        /// <summary>
        /// 배경음(스테레오 반복 클립): 35~60Hz 울림(+작은 스피커에서도 들리게 82·110Hz 배음) + 걸러진 바람 잡음, 느린 출렁임.
        /// 울림·출렁임은 한 바퀴에 정수 번 돌게 주파수를 맞추고, 바람은 끝을 앞에 겹쳐(교차 페이드) 이음매가 들리지 않는다.
        /// 왼쪽·오른쪽 55Hz·110Hz를 조금 어긋나게 해 아주 느린 맥놀이로 넓게 들린다.
        /// </summary>
        public static AudioClip BuildDrone(float seconds)
        {
            int n = Mathf.Max(SampleRate * 2, Mathf.RoundToInt(seconds * SampleRate));
            double loop = n / (double)SampleRate;
            int fade = Mathf.Min(n / 4, SampleRate * 2);
            var data = new float[n * 2];
            var wind = new float[n + fade];
            var rng = new System.Random(20261003);
            var osc = new Phasor[5];
            float kLp = Coef(170f), kLp2 = Coef(900f);

            for (int c = 0; c < 2; c++)
            {
                // 1) 바람: 낮게 거른 잡음 + 천천히 움직이는 공명(갱도를 지나는 바람 소리).
                float lp = 0f, lp2 = 0f, low = 0f, band = 0f, f = 0f;
                for (int i = 0; i < wind.Length; i++)
                {
                    if (i % LfoBlock == 0)
                    {
                        double t = i / (double)SampleRate;
                        float center = 270f + 130f * Sin(t, 3.0 / loop, 1.7 * c);
                        f = 2f * Mathf.Sin(Mathf.PI * center / SampleRate);
                    }
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (white - lp) * kLp;
                    lp2 += (lp - lp2) * kLp2;
                    low += f * band;
                    float high = white - low - 0.45f * band;
                    band += f * high;
                    wind[i] = lp2 * 2f + band * 0.8f;
                }

                // 2) 울림: 한 바퀴(loop초)에 정수 번 도는 낮은 음들.
                osc[0] = new Phasor(Cycles(36.7, loop), 0.0);
                osc[1] = new Phasor(Cycles(41.2, loop), 0.6 + c);
                osc[2] = new Phasor(Cycles(c == 0 ? 55.0 : 55.35, loop), 1.1);
                osc[3] = new Phasor(Cycles(82.4, loop), 0.2 + c);
                osc[4] = new Phasor(Cycles(c == 0 ? 110.0 : 110.4, loop), 2.0);

                float gust = 0f, swell = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (i % LfoBlock == 0)
                    {
                        double t = i / (double)SampleRate;
                        gust = 0.55f + 0.3f * Sin(t, 2.0 / loop, 1.3 + c) + 0.15f * Sin(t, 5.0 / loop, 0.4 + 2.1 * c);
                        swell = (0.72f + 0.28f * Sin(t, 1.0 / loop, 0.9 * c)) * (0.8f + 0.2f * Sin(t, 3.0 / loop, 0.5 + 0.3 * c));
                    }
                    float w = wind[i];
                    if (i < fade)
                    {
                        // 앞부분에 꼬리(n 뒤)를 겹친다: 마지막 표본 → 첫 표본이 이어진 잡음이 된다.
                        float a = i / (float)fade;
                        w = wind[i] * Mathf.Sqrt(a) + wind[n + i] * Mathf.Sqrt(1f - a);
                    }
                    float tones = 0.32f * osc[0].Next() + 0.42f * osc[1].Next() + 0.3f * osc[2].Next()
                                + 0.16f * osc[3].Next() + 0.1f * osc[4].Next();
                    data[i * 2 + c] = tones * swell * 0.55f + w * gust;
                }
            }
            Normalize(data, 0.9f);
            return Make("amb_drone", data, 2);
        }

        /// <summary>멀리서 떨어지는 물방울: 위로 휘는 짧은 '똑' + 갱도 벽 메아리.</summary>
        public static AudioClip BuildDrip(int variant)
        {
            var data = new float[Mathf.RoundToInt(0.9f * SampleRate)];
            var rng = new System.Random(311 + variant * 97);
            float f0 = variant == 0 ? 820f : variant == 1 ? 1010f : 690f;
            float phase = 0f, lp = 0f, k = Coef(2600f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float rise = Mathf.Clamp01(t / 0.03f);
                float f = f0 * (1f + 1.3f * rise * (2f - rise));
                phase += f / SampleRate;
                phase -= Mathf.Floor(phase);
                float plink = Mathf.Sin(phase * TwoPi) * Mathf.Exp(-t * 40f);
                float tick = t < 0.002f ? Noise(rng) * (1f - t / 0.002f) * 0.35f : 0f;
                lp += (plink + tick - lp) * k;
                data[i] = lp;
            }
            Echo(data, 0.13f, 0.32f, 0.29f, 0.18f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("amb_drip" + variant, data, 1);
        }

        /// <summary>멀리서 쇠사슬: 낮은 '철컥' 뒤 고리들이 잦게 부딪치다 잦아든다. 멀게 들리도록 높은 소리를 깎는다.</summary>
        public static AudioClip BuildChain()
        {
            var data = new float[Mathf.RoundToInt(1.4f * SampleRate)];
            var rng = new System.Random(733);
            var start = new[] { -1f, -1f, -1f };
            var freq = new float[3];
            int next = 0;
            float lp = 0f, k = Coef(1800f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                if (rng.NextDouble() < 0.0016 * Mathf.Exp(-t * 2.4f))
                {
                    start[next] = t;
                    freq[next] = 1300f + 1300f * (float)rng.NextDouble();
                    next = (next + 1) % start.Length;
                }
                float n0 = Noise(rng);
                float s = 0f;
                for (int j = 0; j < start.Length; j++)
                {
                    if (start[j] < 0f) continue;
                    float dt = t - start[j];
                    float tick = dt < 0.002f ? n0 * 0.6f : 0f;
                    s += (Mathf.Sin(dt * TwoPi * freq[j]) + 0.5f * Mathf.Sin(dt * TwoPi * freq[j] * 1.47f) + tick) * Mathf.Exp(-dt * 60f);
                }
                float clunk = Mathf.Sin(t * TwoPi * 85f) * Mathf.Exp(-t * 12f) * 0.6f;
                lp += (s * 0.4f + clunk - lp) * k;
                data[i] = lp;
            }
            Echo(data, 0.17f, 0.3f, 0.31f, 0.15f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("amb_chain", data, 1);
        }

        /// <summary>멀리서 버팀목이 삐걱인다: 걸렸다 미끄러지는 펄스가 두 나무 공명(310·760Hz)을 울린다.</summary>
        public static AudioClip BuildCreak()
        {
            const float seconds = 1.8f;
            var data = new float[Mathf.RoundToInt(seconds * SampleRate)];
            var rng = new System.Random(1471);
            float phase = 0f, low = 0f, band = 0f, low2 = 0f, band2 = 0f, lp = 0f, k = Coef(2200f);
            float f1 = 2f * Mathf.Sin(Mathf.PI * 310f / SampleRate);
            float f2 = 2f * Mathf.Sin(Mathf.PI * 760f / SampleRate);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = t / seconds;
                float arc = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / (seconds * 0.85f))));
                float env = Mathf.Pow(arc, 0.7f);
                float rate = 13f + 20f * Mathf.Sin(Mathf.PI * x) + 4f * Mathf.Sin(t * TwoPi * 1.3f);
                phase += rate / SampleRate;
                float pulse = 0f;
                if (phase >= 1f)
                {
                    phase = -0.25f * (float)rng.NextDouble();
                    pulse = 6f * env;
                }
                low += f1 * band;
                float high = pulse - low - 0.05f * band;
                band += f1 * high;
                low2 += f2 * band2;
                float high2 = pulse - low2 - 0.08f * band2;
                band2 += f2 * high2;
                lp += (band + band2 * 0.35f - lp) * k;
                data[i] = lp;
            }
            Echo(data, 0.15f, 0.28f, 0.33f, 0.14f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("amb_creak", data, 1);
        }

        /// <summary>멀리서 짐승 그르렁: 50Hz 안팎 거친 톱니 + 목 울림(380Hz) + 숨소리, 천천히 일었다 잦아든다.</summary>
        public static AudioClip BuildGrowl()
        {
            var data = new float[Mathf.RoundToInt(2f * SampleRate)];
            var rng = new System.Random(2903);
            float phase = 0f, lp = 0f, lp2 = 0f, low = 0f, band = 0f, breath = 0f, slow = 0f;
            float kTone = Coef(320f), kFar = Coef(1100f), kBreath = Coef(700f), kSlow = Coef(6f);
            float fF = 2f * Mathf.Sin(Mathf.PI * 380f / SampleRate);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                slow += (Noise(rng) - slow) * kSlow;
                float f = 50f + 9f * Mathf.Sin(t * TwoPi * 0.6f) + 260f * slow;
                phase += Mathf.Max(20f, f) / SampleRate;
                phase -= Mathf.Floor(phase);
                float saw = phase * 2f - 1f;
                lp += (saw - lp) * kTone;
                low += fF * band;
                float high = saw - low - 0.6f * band;
                band += fF * high;
                float rough = 0.55f + 0.45f * Mathf.Sin(t * TwoPi * 9f) * Mathf.Sin(t * TwoPi * 3.3f + 0.7f);
                breath += (Noise(rng) - breath) * kBreath;
                float env = Smooth(t / 0.35f) * (1f - Smooth((t - 1.2f) / 0.75f));
                float s = (lp * 1.2f + band * 0.35f) * rough + breath * 0.5f;
                lp2 += (s * env - lp2) * kFar;
                data[i] = lp2;
            }
            Echo(data, 0.19f, 0.3f, 0.37f, 0.16f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("amb_growl", data, 1);
        }

        /// <summary>멀리서 돌·흙이 무너져 구르는 낮은 울림 + 드문 자갈 '톡'.</summary>
        public static AudioClip BuildRumble()
        {
            var data = new float[Mathf.RoundToInt(2.2f * SampleRate)];
            var rng = new System.Random(4127);
            float lp = 0f, lp2 = 0f, k1 = Coef(90f), k2 = Coef(160f);
            float pebbleAt = -1f, pebbleF = 1000f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                lp += (Noise(rng) - lp) * k1;
                lp2 += (lp - lp2) * k2;
                float env = Smooth(t / 0.4f) * Mathf.Exp(-Mathf.Max(0f, t - 0.4f) * 1.6f);
                if (rng.NextDouble() < 0.0004 * env)
                {
                    pebbleAt = t;
                    pebbleF = 700f + 700f * (float)rng.NextDouble();
                }
                float s = lp2 * 14f * env;
                if (pebbleAt >= 0f)
                {
                    float dt = t - pebbleAt;
                    s += Mathf.Sin(dt * TwoPi * pebbleF) * Mathf.Exp(-dt * 80f) * 0.25f;
                }
                data[i] = s;
            }
            Echo(data, 0.21f, 0.25f, 0.4f, 0.12f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("amb_rumble", data, 1);
        }

        // ───────────── 잠든 무리 기척(LurkSounds) ─────────────

        /// <summary>
        /// 잠든 굴쥐가 돌바닥을 긁는다: 짧은 발톱 긁힘 3~5번(2.6~3.1kHz로 거른 잡음에 잦은 '딸깍'을 실음) + 가끔 이빨 갉기 '각각'.
        /// 몸이 바닥을 스치는 500Hz 둘레 소리를 함께 섞어 벽 너머(900Hz 아래만)에서도 먹먹한 '부스럭'으로 남는다. 갈래마다 긁는 횟수·간격·높이가 다르다.
        /// </summary>
        public static AudioClip BuildScratch(int variant)
        {
            var data = new float[Mathf.RoundToInt(0.8f * SampleRate)];
            var rng = new System.Random(5101 + variant * 61);
            int bursts = 3 + rng.Next(3);
            var start = new float[bursts];
            var len = new float[bursts];
            var rate = new float[bursts];
            var amp = new float[bursts];
            float t0 = 0.015f;
            for (int b = 0; b < bursts; b++)
            {
                start[b] = t0;
                len[b] = R(rng, 0.035f, 0.08f);
                rate[b] = R(rng, 55f, 95f);
                amp[b] = R(rng, 0.55f, 1f);
                t0 += len[b] + R(rng, 0.04f, 0.13f);
            }
            float gnawAt = rng.NextDouble() < 0.6 ? t0 + 0.04f : -1f;
            float fc = 2f * Mathf.Sin(Mathf.PI * (2600f + 500f * variant) / SampleRate);
            float fGnaw = 2f * Mathf.Sin(Mathf.PI * 1500f / SampleRate);
            float fBody = 2f * Mathf.Sin(Mathf.PI * 500f / SampleRate);
            float low = 0f, band = 0f, lowG = 0f, bandG = 0f, lowB = 0f, bandB = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = Noise(rng);
                float s = 0f;
                for (int b = 0; b < bursts; b++)
                {
                    float dt = t - start[b];
                    if (dt < 0f || dt > len[b]) continue;
                    float env = (1f - Mathf.Exp(-dt / 0.004f)) * (1f - dt / len[b]);
                    float tick = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(dt * TwoPi * rate[b]), 6f);
                    s += n * env * (0.3f + 0.7f * tick) * amp[b];
                }
                low += fc * band;
                float high = s - low - 0.55f * band;
                band += fc * high;
                lowB += fBody * bandB;
                float highB = s - lowB - 0.8f * bandB;
                bandB += fBody * highB;
                float g = 0f;
                if (gnawAt > 0f)
                {
                    // 이빨 갉기: 짧은 딸깍 세 번(30ms 간격).
                    float dt = t - gnawAt;
                    if (dt >= 0f && dt < 0.09f)
                    {
                        float k = dt % 0.03f;
                        g = n * Mathf.Exp(-k * 260f) * 0.8f;
                    }
                }
                lowG += fGnaw * bandG;
                float highG = g - lowG - 0.3f * bandG;
                bandG += fGnaw * highG;
                data[i] = band * 1.3f + bandB * 2.2f + s * 0.12f + bandG * 0.6f;
            }
            Echo(data, 0.07f, 0.2f, 0.16f, 0.1f);
            FadeTail(data, 0.12f);
            Normalize(data, 0.9f);
            return Make("lurk_scratch" + variant, data, 1);
        }

        /// <summary>
        /// 멧돼지 콧김: 콧구멍으로 세게 내쉬는 '푸흐'(650Hz 둘레로 거른 잡음, 28Hz 콧방울 떨림) + 앞머리 짧은 낮은 '꿀'(85Hz),
        /// 0.42초 뒤 작은 두 번째 콧김.
        /// </summary>
        public static AudioClip BuildSnort()
        {
            var data = new float[Mathf.RoundToInt(1f * SampleRate)];
            var rng = new System.Random(6203);
            float fc = 2f * Mathf.Sin(Mathf.PI * 650f / SampleRate);
            float low = 0f, band = 0f, lp = 0f, gruntLp = 0f, phase = 0f;
            float kLp = Coef(1400f), kGrunt = Coef(320f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float puff1 = (1f - Mathf.Exp(-t / 0.025f)) * Mathf.Exp(-t * 6f);
                float t2 = t - 0.42f;
                float puff2 = t2 > 0f ? (1f - Mathf.Exp(-t2 / 0.02f)) * Mathf.Exp(-t2 * 9f) * 0.45f : 0f;
                float flutter = 0.6f + 0.4f * Mathf.Sin(t * TwoPi * 28f);
                float n = Noise(rng) * (puff1 + puff2) * flutter;
                low += fc * band;
                float high = n - low - 0.7f * band;
                band += fc * high;
                lp += (n - lp) * kLp;
                // 꿀: 85 → 70Hz로 내려가는 거친 톱니, 0.15초.
                float f = 85f - 100f * Mathf.Min(t, 0.15f);
                phase += f / SampleRate;
                phase -= Mathf.Floor(phase);
                float grunt = t < 0.18f ? (phase * 2f - 1f) * Smooth(t / 0.02f) * (1f - t / 0.18f) : 0f;
                gruntLp += (grunt - gruntLp) * kGrunt;
                data[i] = band * 1.4f + lp * 0.5f + gruntLp * 0.7f;
            }
            Echo(data, 0.09f, 0.18f, 0.21f, 0.09f);
            FadeTail(data, 0.12f);
            Normalize(data, 0.9f);
            return Make("lurk_snort", data, 1);
        }

        /// <summary>
        /// 궁수 시위 삐걱: 활을 고쳐 쥐며 나무와 시위가 비벼지는 짧은 '끼익'. 걸렸다 미끄러지는 펄스(25 → 70Hz로 잦아짐)가
        /// 활대(900Hz)·시위(2,100Hz) 공명을 울린다. 먼 버팀목 삐걱(BuildCreak)보다 높고 짧다.
        /// </summary>
        public static AudioClip BuildBowCreak()
        {
            const float seconds = 0.75f;
            var data = new float[Mathf.RoundToInt(seconds * SampleRate)];
            var rng = new System.Random(7307);
            float phase = 0f, low = 0f, band = 0f, low2 = 0f, band2 = 0f, lp = 0f, k = Coef(3800f);
            float f1 = 2f * Mathf.Sin(Mathf.PI * 900f / SampleRate);
            float f2 = 2f * Mathf.Sin(Mathf.PI * 2100f / SampleRate);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float x = t / seconds;
                float env = Smooth(x / 0.12f) * (1f - Smooth((x - 0.62f) / 0.3f));
                float rate = 25f + 45f * x + 6f * Mathf.Sin(t * TwoPi * 4f);
                phase += rate / SampleRate;
                float pulse = 0f;
                if (phase >= 1f)
                {
                    phase = -0.2f * (float)rng.NextDouble();
                    pulse = 5f * env;
                }
                low += f1 * band;
                float high = pulse - low - 0.06f * band;
                band += f1 * high;
                low2 += f2 * band2;
                float high2 = pulse - low2 - 0.1f * band2;
                band2 += f2 * high2;
                lp += (band * 0.7f + band2 * 0.5f - lp) * k;
                data[i] = lp;
            }
            Echo(data, 0.08f, 0.16f, 0.19f, 0.08f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("lurk_bowcreak", data, 1);
        }

        /// <summary>
        /// 정예 숨소리: 큰 짐승의 느린 들숨(약 0.9초, 420Hz로 거른 잡음)과 낮은 날숨(약 1.15초, 300Hz + 58Hz 그르렁 밑소리).
        /// </summary>
        public static AudioClip BuildBeastBreath()
        {
            var data = new float[Mathf.RoundToInt(2.4f * SampleRate)];
            var rng = new System.Random(8419);
            float fIn = 2f * Mathf.Sin(Mathf.PI * 420f / SampleRate);
            float fOut = 2f * Mathf.Sin(Mathf.PI * 300f / SampleRate);
            float lowI = 0f, bandI = 0f, lowO = 0f, bandO = 0f, lp = 0f, growlLp = 0f, phase = 0f;
            float kLp = Coef(900f), kGrowl = Coef(220f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = Noise(rng);
                // 들숨 0.08~1.0초(둥근 활 모양), 날숨 1.12~2.3초(빠르게 일고 천천히 잦아듦).
                float inhale = Arc((t - 0.08f) / 0.92f, 0.9f) * 0.55f;
                float tOut = t - 1.12f;
                float exhale = tOut > 0f ? Smooth(tOut / 0.12f) * (1f - Smooth((tOut - 0.35f) / 0.85f)) : 0f;
                lowI += fIn * bandI;
                float highI = n * inhale - lowI - 0.6f * bandI;
                bandI += fIn * highI;
                lowO += fOut * bandO;
                float highO = n * exhale - lowO - 0.5f * bandO;
                bandO += fOut * highO;
                lp += (n * (inhale * 0.5f + exhale) - lp) * kLp;
                phase += 58f / SampleRate;
                phase -= Mathf.Floor(phase);
                float rough = 0.6f + 0.4f * Mathf.Sin(t * TwoPi * 9f);
                growlLp += ((phase * 2f - 1f) * exhale * rough - growlLp) * kGrowl;
                data[i] = bandI * 1.1f + bandO * 1.3f + lp * 0.35f + growlLp * 0.55f;
            }
            Echo(data, 0.1f, 0.15f, 0.23f, 0.08f);
            FadeTail(data, 0.08f);
            Normalize(data, 0.9f);
            return Make("lurk_breath", data, 1);
        }

        /// <summary>
        /// 겁먹은 굴쥐 찍찍: 아래로 휘는 짧은 울음(4.2 → 2.8kHz, 각 50~80ms) 2~3번에 거친 잡음을 조금 섞는다.
        /// </summary>
        public static AudioClip BuildSqueak(int variant)
        {
            var data = new float[Mathf.RoundToInt(0.42f * SampleRate)];
            var rng = new System.Random(9521 + variant * 53);
            int chirps = variant == 0 ? 3 : 2;
            var start = new float[chirps];
            var len = new float[chirps];
            var f0 = new float[chirps];
            var amp = new float[chirps];
            float t0 = 0.005f;
            for (int c = 0; c < chirps; c++)
            {
                start[c] = t0;
                len[c] = R(rng, 0.05f, 0.08f);
                f0[c] = R(rng, 3900f, 4500f) * (variant == 0 ? 1f : 0.9f);
                amp[c] = c == chirps - 1 ? 0.7f : 1f;
                t0 += len[c] + R(rng, 0.035f, 0.06f);
            }
            var phase = new float[chirps];
            float lp = 0f, k = Coef(7000f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int c = 0; c < chirps; c++)
                {
                    float dt = t - start[c];
                    if (dt < 0f || dt > len[c]) continue;
                    float x = dt / len[c];
                    float f = f0[c] * (1f - 0.33f * x) * (1f + 0.03f * Mathf.Sin(dt * TwoPi * 60f));
                    phase[c] += f / SampleRate;
                    phase[c] -= Mathf.Floor(phase[c]);
                    float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * x)), 0.6f) * amp[c];
                    float ph = phase[c] * TwoPi;
                    s += (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph) + 0.15f * Noise(rng)) * env;
                }
                lp += (s - lp) * k;
                data[i] = lp;
            }
            Echo(data, 0.06f, 0.15f, 0.13f, 0.08f);
            FadeTail(data, 0.1f);
            Normalize(data, 0.9f);
            return Make("lurk_squeak" + variant, data, 1);
        }

        // ───────────── 낮은 체력·시체 밟기(DungeonAudio) ─────────────

        /// <summary>
        /// 낮은 체력 거친 숨(4-3, 2.2초 한 바퀴): 떨리는 들숨(0.05~0.8초, 1,100Hz) + 무거운 날숨(0.95~1.95초, 650Hz + 105Hz 거친 목소리).
        /// 처음과 끝이 조용해 반복 클립으로 이음매 없이 돈다.
        /// </summary>
        public static AudioClip BuildPlayerBreath()
        {
            var data = new float[Mathf.RoundToInt(PlayerBreathSeconds * SampleRate)];
            var rng = new System.Random(10613);
            float fIn = 2f * Mathf.Sin(Mathf.PI * 1100f / SampleRate);
            float fOut = 2f * Mathf.Sin(Mathf.PI * 650f / SampleRate);
            float lowI = 0f, bandI = 0f, lowO = 0f, bandO = 0f, voiceLp = 0f, phase = 0f;
            float kVoice = Coef(420f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = Noise(rng);
                float tremble = 0.75f + 0.25f * Mathf.Sin(t * TwoPi * 11f);
                float inhale = Arc((t - 0.05f) / 0.75f, 0.8f) * 0.6f * tremble;
                float tOut = t - 0.95f;
                float exhale = tOut > 0f ? Smooth(tOut / 0.06f) * (1f - Smooth((tOut - 0.15f) / 0.85f)) : 0f;
                lowI += fIn * bandI;
                float highI = n * inhale - lowI - 0.5f * bandI;
                bandI += fIn * highI;
                lowO += fOut * bandO;
                float highO = n * exhale - lowO - 0.45f * bandO;
                bandO += fOut * highO;
                phase += (105f - 12f * Mathf.Clamp01(tOut)) / SampleRate;
                phase -= Mathf.Floor(phase);
                voiceLp += ((phase * 2f - 1f) * exhale - voiceLp) * kVoice;
                data[i] = bandI * 1.2f + bandO * 1.3f + voiceLp * 0.18f;
            }
            Normalize(data, 0.9f);
            return Make("low_breath", data, 1);
        }

        /// <summary>
        /// 심장 '쿵-쿵'(lub-dub): 첫 소리는 표본 0에서 시작한다(붉은 가장자리 맥동이 차는 순간과 맞춤). 75 → 48Hz로 떨어지는 낮은 울림에
        /// 작은 스피커에서도 들리게 두 배 음을 섞고, HeartDubDelay 뒤 조금 높고 작은 둘째 소리.
        /// </summary>
        public static AudioClip BuildHeartbeat()
        {
            var data = new float[Mathf.RoundToInt(0.62f * SampleRate)];
            var rng = new System.Random(11717);
            float p1 = 0f, p2 = 0f, lp = 0f, click = 0f, k = Coef(260f), kClick = Coef(300f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float s = Thump(t, 0f, 75f, 48f, 18f, 1f, ref p1);
                s += Thump(t, HeartDubDelay, 90f, 60f, 24f, 0.7f, ref p2);
                float d1 = t;
                float d2 = t - HeartDubDelay;
                float c = (d1 < 0.012f ? 1f - d1 / 0.012f : 0f) + (d2 >= 0f && d2 < 0.01f ? 0.6f * (1f - d2 / 0.01f) : 0f);
                click += (Noise(rng) * c - click) * kClick;
                lp += (s + click * 0.5f - lp) * k;
                data[i] = lp;
            }
            FadeTail(data, 0.08f);
            Normalize(data, 0.9f);
            return Make("low_heart", data, 1);
        }

        /// <summary>
        /// 시체 밟기 '질척': 낮게 거른 젖은 잡음 + 작은 거품 터짐(300~700Hz, 15~25ms) 여럿 + 짧은 둔탁음(80Hz).
        /// </summary>
        public static AudioClip BuildSquelch()
        {
            var data = new float[Mathf.RoundToInt(0.38f * SampleRate)];
            var rng = new System.Random(12823);
            const int pops = 6;
            var at = new float[pops];
            var freq = new float[pops];
            var decay = new float[pops];
            for (int p = 0; p < pops; p++)
            {
                at[p] = R(rng, 0.01f, 0.22f);
                freq[p] = R(rng, 300f, 700f);
                decay[p] = 1f / R(rng, 0.015f, 0.025f);
            }
            float lp = 0f, lp2 = 0f, k = Coef(800f), k2 = Coef(2600f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Smooth(t / 0.015f) * Mathf.Exp(-t * 9f);
                lp += (Noise(rng) * env - lp) * k;
                float s = lp * 2.2f;
                for (int p = 0; p < pops; p++)
                {
                    float dt = t - at[p];
                    if (dt < 0f) continue;
                    // 위로 휘는 짧은 '뽁'.
                    s += Mathf.Sin(dt * TwoPi * freq[p] * (1f + 2f * dt)) * Mathf.Exp(-dt * decay[p]) * 0.35f;
                }
                s += Mathf.Sin(t * TwoPi * 80f) * Mathf.Exp(-t * 22f) * 0.6f;
                lp2 += (s - lp2) * k2;
                data[i] = lp2;
            }
            FadeTail(data, 0.15f);
            Normalize(data, 0.9f);
            return Make("gore_squelch", data, 1);
        }

        // ───────────── 도구 ─────────────

        /// <summary>
        /// 심장 한 소리: start초에 시작해 f0 → f1Hz로 빠르게 떨어지는 사인(+ 두 배 음), 4ms 오르고 decay로 잦아든다.
        /// phase는 부르는 쪽이 들고 있는 위상(소리마다 하나).
        /// </summary>
        static float Thump(float t, float start, float f0, float f1, float decay, float amp, ref float phase)
        {
            float dt = t - start;
            if (dt < 0f) return 0f;
            float f = f1 + (f0 - f1) * Mathf.Exp(-dt * 30f);
            phase += f / SampleRate;
            phase -= Mathf.Floor(phase);
            float env = (1f - Mathf.Exp(-dt / 0.004f)) * Mathf.Exp(-dt * decay) * amp;
            float ph = phase * TwoPi;
            return (Mathf.Sin(ph) + 0.35f * Mathf.Sin(2f * ph)) * env;
        }

        /// <summary>0~1 구간에서 둥근 활(양 끝 0, 가운데 1), 밖은 0. power가 작을수록 어깨가 넓다.</summary>
        static float Arc(float x, float power) => x <= 0f || x >= 1f ? 0f : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * x)), power);

        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>회전 행렬로 도는 사인(배경음 긴 클립에서 표본마다 삼각함수를 부르지 않으려고).</summary>
        struct Phasor
        {
            double _c, _s;
            readonly double _dc, _ds;

            public Phasor(double hz, double phase)
            {
                _c = System.Math.Cos(phase);
                _s = System.Math.Sin(phase);
                double step = 2.0 * System.Math.PI * hz / SampleRate;
                _dc = System.Math.Cos(step);
                _ds = System.Math.Sin(step);
            }

            public float Next()
            {
                float v = (float)_s;
                double c = _c * _dc - _s * _ds;
                _s = _s * _dc + _c * _ds;
                _c = c;
                return v;
            }
        }

        /// <summary>한 바퀴(loop초)에 정수 번 돌도록 맞춘 주파수.</summary>
        static double Cycles(double hz, double loop) => System.Math.Max(1.0, System.Math.Round(hz * loop)) / loop;

        static float Sin(double t, double hz, double phase) => (float)System.Math.Sin(2.0 * System.Math.PI * hz * t + phase);

        /// <summary>한 극 저역 통과 계수(차단 주파수 Hz → 0~1).</summary>
        static float Coef(float hz) => 1f - Mathf.Exp(-TwoPi * hz / SampleRate);

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>먼 벽에서 되돌아오는 메아리 두 갈래(되먹임). 잔향 필터와 별개로 '멀리서' 느낌을 준다.</summary>
        static void Echo(float[] data, float delay1, float gain1, float delay2, float gain2)
        {
            int d1 = Mathf.Max(1, Mathf.RoundToInt(delay1 * SampleRate));
            int d2 = Mathf.Max(1, Mathf.RoundToInt(delay2 * SampleRate));
            for (int i = 0; i < data.Length; i++)
            {
                float s = data[i];
                if (i >= d1) s += data[i - d1] * gain1;
                if (i >= d2) s += data[i - d2] * gain2;
                data[i] = s;
            }
        }

        static void FadeTail(float[] data, float fraction)
        {
            int f = Mathf.Max(1, (int)(data.Length * fraction));
            for (int i = 0; i < f && i < data.Length; i++)
                data[data.Length - 1 - i] *= i / (float)f;
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > max) max = a;
            }
            if (max < 1e-5f) return;
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        static AudioClip Make(string name, float[] data, int channels)
        {
            var clip = AudioClip.Create(name, data.Length / channels, channels, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
