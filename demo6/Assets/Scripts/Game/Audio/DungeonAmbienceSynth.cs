using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 배경음·먼 소리를 코드로 합성한다(기획/다크판타지-분위기-1차.md '소리': 낮게 웅웅거리는 배경음,
    /// 멀리서 물방울·쇠사슬·삐걱임·짐승 소리). 소리 파일 없이 만드는 자리·타이밍 견본이며 리소스 단계에서 녹음으로 바꾼다.
    /// 던전을 열 때 DungeonAudio가 한 번 만들고, 닫힐 때 지운다(매 프레임 할당 없음).
    /// </summary>
    public static class DungeonAmbienceSynth
    {
        /// <summary>낮은 소리가 중심이라 표본율을 절반(22,050Hz)으로 해서 만드는 시간과 메모리를 줄인다.</summary>
        public const int SampleRate = 22050;
        /// <summary>물방울 소리 갈래 수(높이가 조금씩 다르다).</summary>
        public const int DripVariants = 3;

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

        // ───────────── 도구 ─────────────

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
