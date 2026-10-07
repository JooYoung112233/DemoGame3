using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>오우거 소리 종류(기획/전투-보스-무기-다듬기-1차.md 3-9).</summary>
    public enum OgreSound
    {
        /// <summary>돌 씹는 소리(먹는 중 반복, 문 밖에서도 들림).</summary>
        Chew,
        /// <summary>포효(낮게 늘임, 마지막 돌진·2단계 전환은 더 낮게).</summary>
        Roar,
        /// <summary>내려찍기 쿵 + 돌 부스러짐.</summary>
        Slam,
        /// <summary>돌진 무거운 발소리 + 나무 끌림.</summary>
        Charge,
        /// <summary>휩쓸기 바람.</summary>
        Sweep,
        /// <summary>낙석.</summary>
        Rockfall,
        /// <summary>무너짐(무릎 꿇음).</summary>
        Kneel,
    }

    /// <summary>
    /// 오우거 코드 임시음(정식 소리 전). 한 방이 10%를 넘는 A·B·C·D는 예고 소리가 필수다. 합성 클립은 Sfx.Play(종류, 클립)로 낸다(Sfx.cs는 고치지 않음).
    /// 음량·음높이는 Sfx가 받지 않으므로 클립에 구워 둔다(0.1 음량 칸 × 0.05 음높이 칸마다 처음 쓸 때 한 번 만들고 캐시).
    /// 디아블로 톤: 낮고 젖은 소리, 높은 금속성은 쓰지 않는다. Sfx 종류는 음량 표·같은 프레임 한 번 규칙만 빌린다.
    /// </summary>
    public static class OgreSounds
    {
        const int SampleRate = 44100;
        const float TwoPi = 2f * Mathf.PI;

        static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
        static bool _quitHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clips.Clear();
            _quitHooked = false;
        }

        /// <param name="volume">0~1(클립에 구움, 0.1 칸).</param>
        /// <param name="pitch">음높이 배율(0.5~1.5, 0.05 칸). 마지막 돌진·2단계 포효는 낮게(0.75 등).</param>
        public static void Play(OgreSound sound, float volume = 1f, float pitch = 1f)
        {
            if (!Tuning.Sound || volume <= 0.01f) return;
            int vol = Mathf.Clamp(Mathf.RoundToInt(volume * 10f), 1, 10);
            int pit = Mathf.Clamp(Mathf.RoundToInt(pitch * 20f), 10, 30);
            int key = (int)sound * 10000 + pit * 100 + vol;
            if (!Clips.TryGetValue(key, out var clip) || !clip)
            {
                clip = Build(sound, vol / 10f, pit / 20f);
                Clips[key] = clip;
                HookQuit();
            }
            Sfx.Play(KindOf(sound), clip);
        }

        /// <summary>음량 표·같은 프레임 한 번 규칙을 빌릴 Sfx 종류. 무너짐은 Enemy가 같은 프레임에 Break를 내므로 겹치지 않는 종류를 쓴다.</summary>
        static SfxKind KindOf(OgreSound sound)
        {
            switch (sound)
            {
                case OgreSound.Chew: return SfxKind.Thud;
                case OgreSound.Roar: return SfxKind.BodyFall;
                case OgreSound.Slam: return SfxKind.WallBreak;
                case OgreSound.Charge: return SfxKind.BoarCharge;
                case OgreSound.Sweep: return SfxKind.Chest;
                case OgreSound.Rockfall: return SfxKind.Stake;
                default: return SfxKind.Pick;
            }
        }

        /// <summary>플레이를 끝낼 때 만든 클립을 지운다(도메인 다시 불러오기 꺼짐이라 에디터에 남지 않게).</summary>
        static void HookQuit()
        {
            if (_quitHooked) return;
            _quitHooked = true;
            Application.quitting += DestroyClips;
        }

        static void DestroyClips()
        {
            Application.quitting -= DestroyClips;
            foreach (var c in Clips.Values)
                if (c) Object.Destroy(c);
            Clips.Clear();
            _quitHooked = false;
        }

        // ───────────────────────── 합성 ─────────────────────────

        static AudioClip Build(OgreSound sound, float volume, float pitch)
        {
            float seconds = Length(sound) / Mathf.Max(0.5f, pitch);
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var rng = new System.Random(1013 + (int)sound * 7919);
            var st = new State();
            float fade = Mathf.Max(0.008f, seconds * 0.08f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                // 음높이: 시간을 늘이고(t × 배율) 주파수에 배율을 곱한다(테이프를 느리게 돌린 것처럼).
                float s = Wave(sound, t * pitch, pitch, ref st, rng);
                float tail = Mathf.Clamp01((seconds - t) / fade);
                data[i] = Mathf.Clamp(s * tail * volume, -1f, 1f);
            }
            var clip = AudioClip.Create("ogre_" + sound + "_" + Mathf.RoundToInt(pitch * 100f) + "_" + Mathf.RoundToInt(volume * 10f), count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Length(OgreSound sound)
        {
            switch (sound)
            {
                case OgreSound.Chew: return 0.34f;
                case OgreSound.Roar: return 1.1f;
                case OgreSound.Slam: return 0.7f;
                case OgreSound.Charge: return 0.62f;
                case OgreSound.Sweep: return 0.42f;
                case OgreSound.Rockfall: return 0.8f;
                default: return 0.62f;
            }
        }

        struct State
        {
            public float Phase, Phase2, Lp, Lp2, Bp;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static float Osc(ref float phase, float freq)
        {
            phase += freq / SampleRate;
            phase -= Mathf.Floor(phase);
            return Mathf.Sin(phase * TwoPi);
        }

        /// <summary>톱니에 가까운 거친 파형(포효 목소리).</summary>
        static float Growl(ref float phase, float freq)
        {
            phase += freq / SampleRate;
            phase -= Mathf.Floor(phase);
            float saw = phase * 2f - 1f;
            return saw * 0.6f + Mathf.Sin(phase * TwoPi) * 0.4f;
        }

        /// <summary>한 극 저역 통과(k가 작을수록 낮음).</summary>
        static float LowPass(ref float state, float input, float k)
        {
            state += (input - state) * k;
            return state;
        }

        /// <summary>짧은 충돌음 덩어리(시작 시각에서 지수 감쇠).</summary>
        static float Burst(float t, float start, float decay) => t < start ? 0f : Mathf.Exp(-(t - start) / decay);

        static float Wave(OgreSound sound, float t, float pitch, ref State v, System.Random r)
        {
            switch (sound)
            {
                case OgreSound.Chew:
                {
                    // 돌을 씹는 오도독 셋 + 턱이 닫히는 낮은 쿵.
                    float crunch = Burst(t, 0f, 0.018f) + Burst(t, 0.11f, 0.02f) * 0.8f + Burst(t, 0.22f, 0.016f) * 0.9f;
                    float n = LowPass(ref v.Lp, Noise(r), 0.35f);
                    float thump = Osc(ref v.Phase, 85f * pitch) * (Burst(t, 0f, 0.05f) + Burst(t, 0.11f, 0.05f) + Burst(t, 0.22f, 0.05f)) * 0.35f;
                    return n * crunch * 0.75f + thump;
                }
                case OgreSound.Roar:
                {
                    // 낮게 늘인 으르렁: 70 → 52Hz 목소리 + 숨 섞인 잡음, 0.12초에 커져 끝으로 잦아든다.
                    float env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1.1f - t) / 0.45f);
                    float f = Mathf.Lerp(70f, 52f, t / 1.1f) * pitch * (1f + 0.04f * Mathf.Sin(t * TwoPi * 5.5f));
                    float voice = Growl(ref v.Phase, f) + Growl(ref v.Phase2, f * 2.01f) * 0.35f;
                    voice = LowPass(ref v.Lp, voice, 0.08f);
                    float breath = LowPass(ref v.Lp2, Noise(r), 0.05f) * 0.6f;
                    return (voice * 1.6f + breath) * env * 0.85f;
                }
                case OgreSound.Slam:
                {
                    // 버팀목이 바닥을 치는 쿵(90 → 40Hz) + 흙 터짐 + 부스러지는 돌 딸깍들.
                    float f = Mathf.Lerp(90f, 40f, Mathf.Clamp01(t / 0.25f)) * pitch;
                    float boom = Osc(ref v.Phase, f) * Burst(t, 0f, 0.18f);
                    float dirt = LowPass(ref v.Lp, Noise(r), 0.12f) * Burst(t, 0f, 0.09f);
                    if (t > 0.08f && r.NextDouble() < 0.004) v.Bp = 1f;
                    v.Bp *= 0.995f;
                    float crumble = LowPass(ref v.Lp2, Noise(r), 0.5f) * v.Bp * 0.5f;
                    return boom * 0.9f + dirt * 0.8f + crumble;
                }
                case OgreSound.Charge:
                {
                    // 무거운 발 셋(0.2초 간격) + 버팀목이 바닥을 긁는 나무 끌림.
                    float steps = Burst(t, 0f, 0.07f) + Burst(t, 0.2f, 0.07f) + Burst(t, 0.4f, 0.07f);
                    float foot = Osc(ref v.Phase, 62f * pitch) * steps * 0.8f;
                    float n = Noise(r);
                    float low = LowPass(ref v.Lp, n, 0.18f);
                    float band = low - LowPass(ref v.Lp2, n, 0.03f);
                    float drag = band * Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((0.62f - t) / 0.2f) * 0.5f;
                    return foot + drag;
                }
                case OgreSound.Sweep:
                {
                    // 몽둥이가 가르는 낮은 바람: 거름이 점점 열리며 지나간다.
                    float env = Mathf.Sin(Mathf.Clamp01(t / 0.42f) * Mathf.PI);
                    float k = Mathf.Lerp(0.02f, 0.14f, Mathf.Clamp01(t / 0.3f));
                    float n = Noise(r);
                    float low = LowPass(ref v.Lp, n, k);
                    float band = low - LowPass(ref v.Lp2, n, k * 0.25f);
                    return band * env * 1.6f;
                }
                case OgreSound.Rockfall:
                {
                    // 천장이 우르르(낮은 잡음) + 바닥에 부딪는 돌 여럿.
                    float rumble = LowPass(ref v.Lp, Noise(r), 0.03f) * Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((0.8f - t) / 0.35f) * 1.8f;
                    if (r.NextDouble() < 0.0016) v.Bp = 0.6f + (float)r.NextDouble() * 0.4f;
                    v.Bp *= 0.9975f;
                    float clack = LowPass(ref v.Lp2, Noise(r), 0.4f) * v.Bp;
                    float thud = Osc(ref v.Phase, 55f * pitch) * Burst(t, 0f, 0.15f) * 0.5f;
                    return rumble + clack * 0.7f + thud;
                }
                default:
                {
                    // 무릎 꿇음: 무거운 몸이 내려앉는 쿵(60 → 38Hz) + 짧은 신음.
                    float f = Mathf.Lerp(60f, 38f, Mathf.Clamp01(t / 0.3f)) * pitch;
                    float thud = Osc(ref v.Phase, f) * Burst(t, 0.02f, 0.16f);
                    float dirt = LowPass(ref v.Lp, Noise(r), 0.1f) * Burst(t, 0.02f, 0.07f);
                    float groanEnv = Mathf.Clamp01((t - 0.05f) / 0.08f) * Mathf.Clamp01((0.6f - t) / 0.3f);
                    float groan = LowPass(ref v.Lp2, Growl(ref v.Phase2, 78f * pitch), 0.06f) * groanEnv;
                    return thud * 0.9f + dirt * 0.6f + groan * 0.9f;
                }
            }
        }
    }
}
