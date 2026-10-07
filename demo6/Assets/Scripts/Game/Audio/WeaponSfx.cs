using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 무기마다 다른 휘두르기·타격 소리(기획/전투-보스-무기-다듬기-1차.md 2-7 '소리는 필수': 무기마다 휘두르기 2개와 타격 1개, 정식 소리 전에는 코드 합성 임시음이라도 다르게).
    /// PlayerController.SwingHit(⑤)이 단계 그림 칸(StepArt)의 소리가 없을 때 이 값으로 낸다: 클립이 있으면 Sfx.Play(종류, 클립), 없으면 Sfx.PlayScaled(종류, 음량, 음높이).
    /// 기존 3종 값은 지금 PlayerController 식 그대로(대검 휘두르기 1.12/0.72·타격 음높이 0.8, 쌍검 0.82/1.3·1.18, 장검 1/1·1)이고 클립은 없다(M0a 손맛 그대로).
    /// 새 무기 6종은 합성 클립(휘두르기 2 + 타격 1)을 처음 부를 때 한 번 만들어 둔다: 쇠망치 0.2~0.3초 저음 '쿵', 창 짧은 바람, 큰 낫 긴 '쉭',
    /// 도끼 찍는 소리, 단검 빠른 칼 소리, 사슬 철퇴 사슬 짤랑 + 바람. 클립이 있는 무기의 음량·음높이 값은 클립을 못 만들 때만 쓰는 예비값이다. Sfx.cs는 고치지 않는다.
    /// 합성은 이름으로 씨앗을 정해 실행마다 같은 소리가 나고(Synth는 엔진 없이 표본만 만듦), 클립은 다음 실행 시작(SubsystemRegistration)에 지운다.
    /// 세 무기·오른쪽 클릭(기획/세-무기-우클릭-소켓-1차.md): 둔탁한 단계(ComboStep.blunt)는 HitClip이 '퉁' + 타격음 클립을 주고,
    /// 난사·놓아 베기 휘두르기 값(FlurrySwing·ReleaseSwing)은 WeaponActFx가 읽는다. 기존 무기·단계의 값과 소리는 그대로다.
    /// </summary>
    public static class WeaponSfx
    {
        const int Rate = 44100;
        const float TwoPi = 2f * Mathf.PI;

        sealed class Clips
        {
            public AudioClip Swing0;
            public AudioClip Swing1;
            public AudioClip Hit;
        }

        static readonly Dictionary<string, Clips> Cache = new Dictionary<string, Clips>();
        /// <summary>둔탁한 타격(ComboStep.blunt, 한손검과 방패 ② 방패 치기) 클립: 나무 판 '퉁' + 타격음. 처음 부를 때 한 번 만든다.</summary>
        static AudioClip _blunt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var c in Cache.Values)
            {
                if (c.Swing0) Object.Destroy(c.Swing0);
                if (c.Swing1) Object.Destroy(c.Swing1);
                if (c.Hit) Object.Destroy(c.Hit);
            }
            Cache.Clear();
            if (_blunt) Object.Destroy(_blunt);
            _blunt = null;
        }

        /// <summary>합성 클립을 쓰는 새 무기 6종인가.</summary>
        public static bool HasClips(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_maul":
                case "wpn_spear":
                case "wpn_scythe":
                case "wpn_axe":
                case "wpn_dagger":
                case "wpn_flail":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>휘두르기 음량 배율.</summary>
        public static float SwingVolume(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword": return 1.12f;
                case "wpn_twinblades": return .82f;
                case "wpn_maul": return 1.2f;
                case "wpn_spear": return .9f;
                case "wpn_axe": return 1.05f;
                case "wpn_dagger": return .75f;
                case "wpn_flail": return 1.1f;
                default: return 1f;
            }
        }

        /// <summary>휘두르기 음높이 배율.</summary>
        public static float SwingPitch(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword": return .72f;
                case "wpn_twinblades": return 1.3f;
                case "wpn_maul": return .62f;
                case "wpn_spear": return 1.25f;
                case "wpn_scythe": return .85f;
                case "wpn_axe": return .9f;
                case "wpn_dagger": return 1.45f;
                case "wpn_flail": return .8f;
                default: return 1f;
            }
        }

        /// <summary>보통 타격(치명·처치 아님) 음높이 배율.</summary>
        public static float HitPitch(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword": return .8f;
                case "wpn_twinblades": return 1.18f;
                case "wpn_maul": return .65f;
                case "wpn_spear": return 1.1f;
                case "wpn_scythe": return .95f;
                case "wpn_axe": return .9f;
                case "wpn_dagger": return 1.3f;
                case "wpn_flail": return .75f;
                default: return 1f;
            }
        }

        /// <summary>그 무기·단계의 합성 휘두르기 클립(없으면 null → PlayScaled). variant = 같은 단계 안 번갈이(0·1). 기존 3종은 null.</summary>
        public static AudioClip SwingClip(string weaponId, int stepIndex, int variant)
        {
            var c = Get(weaponId);
            if (c == null) return null;
            return (variant & 1) == 0 ? c.Swing0 : c.Swing1;
        }

        /// <summary>
        /// 그 무기의 합성 타격 클립(없으면 null → PlayScaled). 기존 3종은 null.
        /// 둔탁한 단계(ComboStep.blunt, 한손검과 방패 ② 방패 치기 — 기획/세-무기-우클릭-소켓-1차.md 2-3)는 무기와 상관없이 '퉁' + 타격음 클립.
        /// </summary>
        public static AudioClip HitClip(string weaponId, ComboStep step)
        {
            if (step != null && step.blunt) return BluntClip();
            var c = Get(weaponId);
            return c != null ? c.Hit : null;
        }

        /// <summary>둔탁한 타격 클립(처음 한 번 만든다, 지워졌으면 다시 만든다).</summary>
        static AudioClip BluntClip()
        {
            if (_blunt) return _blunt;
            _blunt = Make("shield_bash", BakeRaw("shield_bash", 0.12f, ShieldBash));
            return _blunt;
        }

        // ── 세 무기 오른쪽 클릭 소리(기획/세-무기-우클릭-소켓-1차.md 3-7·4-6·7-3 '효과') ──

        /// <summary>난사 작은 타 휘두르기 소리 가운데 음높이와 흔들 폭(번갈아 ±).</summary>
        public const float FlurryPitch = 1.18f;
        public const float FlurryPitchSwing = 0.06f;

        /// <summary>
        /// 난사 한 타의 휘두르기 소리(index = 0부터 판정 번호). 작은 타는 홀수 번째(1·3·5·7번째 = index 0·2·4·6)만 내고,
        /// 음높이는 1.18에서 ±0.06을 번갈아(1.12, 1.24, …) 준다(8타가 한 소리로 뭉개지지 않게). 음량은 쌍검 휘두르기 값.
        /// 마지막 X(8번째)는 마무리라 늘 낸다(조금 크고 낮게). 낼 소리가 없으면 false.
        /// </summary>
        public static bool FlurrySwing(int index, out float volume, out float pitch)
        {
            volume = SwingVolume("wpn_twinblades");
            pitch = FlurryPitch;
            if (TwinFlurry.IsFinal(index))
            {
                volume *= 1.15f;
                pitch = FlurryPitch - FlurryPitchSwing;
                return true;
            }
            if (index < 0 || (index & 1) != 0) return false;
            pitch = FlurryPitch + (((index >> 1) & 1) == 0 ? -FlurryPitchSwing : FlurryPitchSwing);
            return true;
        }

        /// <summary>대검 놓아 베기 휘두르기 음량·음높이(3-7): 대검 값(1.12/0.72)에서 단계마다 조금 크고 낮게(1단계 그대로, 3단계 1.25/0.65).</summary>
        public static void ReleaseSwing(int level, out float volume, out float pitch)
        {
            int k = Mathf.Clamp(level, 1, 3) - 1;
            volume = SwingVolume("wpn_greatsword") * (1f + 0.06f * k);
            pitch = SwingPitch("wpn_greatsword") * (1f - 0.05f * k);
        }

        /// <summary>새 무기면 클립 묶음(처음 한 번 만든다, 지워졌으면 다시 만든다).</summary>
        static Clips Get(string weaponId)
        {
            if (!HasClips(weaponId)) return null;
            if (Cache.TryGetValue(weaponId, out var c) && c.Swing0 && c.Swing1 && c.Hit) return c;
            c = new Clips
            {
                Swing0 = Make(weaponId + "_swing0", Synth(weaponId, 0)),
                Swing1 = Make(weaponId + "_swing1", Synth(weaponId, 1)),
                Hit = Make(weaponId + "_hit", Synth(weaponId, 2)),
            };
            Cache[weaponId] = c;
            return c;
        }

        static AudioClip Make(string name, float[] data)
        {
            if (data == null || data.Length == 0) return null;
            var clip = AudioClip.Create("wsfx_" + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ───────────────────────── 합성(엔진 없이 표본만, 모노 44.1kHz) ─────────────────────────

        /// <summary>합성 중 상태(위상·거름 상태). 소리마다 새로 시작한다.</summary>
        struct State
        {
            public float Phase, Phase2;
            public float Lp, Lp2;
            public float Low, Band, Low2, Band2;
        }

        delegate float Wave(float t, float length, ref State v, System.Random noise);

        /// <summary>
        /// 무기 id의 표본. which: 0·1 = 휘두르기 두 갈래, 2 = 타격. 길이·최고 음량은 무기마다 다르고, 끝 6ms를 줄여 딸깍을 막는다.
        /// 새 무기가 아니면 null.
        /// </summary>
        internal static float[] Synth(string weaponId, int which)
        {
            bool hit = which == 2;
            int variant = which & 1;
            switch (weaponId)
            {
                case "wpn_maul":
                    return hit ? Bake("maul_hit", 0.28f, 0.95f, MaulHit, 0) : Bake("maul_swing" + variant, variant == 0 ? 0.24f : 0.27f, 0.85f, MaulSwing, variant);
                case "wpn_spear":
                    return hit ? Bake("spear_hit", 0.09f, 0.8f, SpearHit, 0) : Bake("spear_swing" + variant, variant == 0 ? 0.08f : 0.075f, 0.65f, SpearSwing, variant);
                case "wpn_scythe":
                    return hit ? Bake("scythe_hit", 0.12f, 0.8f, ScytheHit, 0) : Bake("scythe_swing" + variant, variant == 0 ? 0.3f : 0.28f, 0.7f, ScytheSwing, variant);
                case "wpn_axe":
                    return hit ? Bake("axe_hit", 0.13f, 0.9f, AxeHit, 0) : Bake("axe_swing" + variant, variant == 0 ? 0.14f : 0.15f, 0.7f, AxeSwing, variant);
                case "wpn_dagger":
                    return hit ? Bake("dagger_hit", 0.06f, 0.7f, DaggerHit, 0) : Bake("dagger_swing" + variant, variant == 0 ? 0.055f : 0.05f, 0.55f, DaggerSwing, variant);
                case "wpn_flail":
                    return hit ? Bake("flail_hit", 0.18f, 0.95f, FlailHit, 0) : Bake("flail_swing" + variant, variant == 0 ? 0.26f : 0.24f, 0.8f, FlailSwing, variant);
                default:
                    return null;
            }
        }

        // 갈래(variant)는 파형 함수에 v.Phase2 초기값으로 넘긴다(0 또는 1).
        static float[] Bake(string name, float seconds, float peak, Wave wave, int variant)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var noise = new System.Random(StableSeed(name));
            var v = new State { Phase2 = variant };
            float max = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float s = wave(t, seconds, ref v, noise);
                if (float.IsNaN(s) || float.IsInfinity(s)) s = 0f;
                data[i] = s;
                float a = Mathf.Abs(s);
                if (a > max) max = a;
            }
            float gain = max > 1e-5f ? peak / max : 0f;
            float fade = 0.006f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float tail = Mathf.Clamp01((seconds - t) / fade);
                data[i] = Mathf.Clamp(data[i] * gain * tail, -1f, 1f);
            }
            return data;
        }

        /// <summary>
        /// 고르지 않고(최고 음량 맞춤 없이) 굽는다: Sfx 타격음과 같은 세기·딸깍 포화를 지키려는 소리(둔탁한 타격)용. 끝 6ms를 줄여 딸깍을 막는다.
        /// </summary>
        static float[] BakeRaw(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            var noise = new System.Random(StableSeed(name));
            var v = new State();
            const float fade = 0.006f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                float s = wave(t, seconds, ref v, noise);
                if (float.IsNaN(s) || float.IsInfinity(s)) s = 0f;
                float tail = Mathf.Clamp01((seconds - t) / fade);
                data[i] = Mathf.Clamp(s * tail, -1f, 1f);
            }
            return data;
        }

        /// <summary>
        /// 방패 치기(둔탁한 타격, 0.12초): Sfx 타격음 식(첫 5ms 딸깍 + 130 → 52Hz 몸통 + 살 둔탁음, 0.08초)을 0.75로 줄여 깔고,
        /// 그 위에 나무 판 '퉁'(140 → 78Hz 몸통 + 360Hz 판자 공명)을 겹친다.
        /// </summary>
        static float ShieldBash(float t, float len, ref State v, System.Random r)
        {
            float hit = 0f;
            const float hitLen = 0.08f;
            if (t < hitLen)
            {
                float x = t / hitLen;
                float body = Osc(ref v.Phase, Mathf.Lerp(130f, 52f, x));
                float upper = Mathf.Sin(v.Phase * 2f * TwoPi) * 0.35f;
                float thump = (body + upper) * Mathf.Exp(-t * 30f);
                float flesh = OnePole(ref v.Lp, Noise(r), 0.12f) * Mathf.Exp(-t * 45f) * 1.6f;
                hit = thump * 0.65f + flesh + Click(t, 0.005f, r) * 0.6f;
            }
            float wood = Osc(ref v.Phase2, Mathf.Lerp(140f, 78f, Mathf.Clamp01(t / len))) * Mathf.Exp(-t * 26f) * 0.4f;
            float board = BandPass(ref v.Low, ref v.Band, Noise(r) * Mathf.Exp(-t * 70f), 360f, 0.16f) * 0.8f;
            return hit * 0.75f + wood + board;
        }

        /// <summary>실행마다 같은 잡음이 나오게 하는 이름 해시(FNV-1a, Sfx와 같은 식).</summary>
        static int StableSeed(string s)
        {
            unchecked
            {
                int h = -2128831035;
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619;
                return h;
            }
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        static float Osc(ref float phase, float freq)
        {
            phase += freq / Rate;
            phase -= Mathf.Floor(phase);
            return Mathf.Sin(phase * TwoPi);
        }

        static float OnePole(ref float state, float input, float k)
        {
            state += (input - state) * k;
            return state;
        }

        /// <summary>상태 변수 필터 대역 통과(damping이 작을수록 공명이 길다).</summary>
        static float BandPass(ref float low, ref float band, float input, float freq, float damping)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(freq, Rate / 6f) / Rate);
            low += f * band;
            float high = input - low - damping * band;
            band += f * high;
            return band;
        }

        static float Click(float t, float length, System.Random r) => t < length ? Noise(r) * (1f - t / length) : 0f;

        /// <summary>쇳소리: 비조화 배음 셋(k배씩)이 rate로 줄어든다.</summary>
        static float Ring(float t, float baseFreq, float rate) =>
            (Mathf.Sin(t * TwoPi * baseFreq) + 0.6f * Mathf.Sin(t * TwoPi * baseFreq * 1.47f) + 0.4f * Mathf.Sin(t * TwoPi * baseFreq * 2.09f)) * Mathf.Exp(-t * rate);

        /// <summary>쇠망치 휘두르기: 무거운 쇳덩이가 지나가는 낮고 굵은 바람 + 웅 하는 저음. 0.24·0.27초.</summary>
        static float MaulSwing(float t, float len, ref State v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Pow(Mathf.Sin(x * Mathf.PI), 1.4f);
            float k = Mathf.Lerp(0.015f, 0.06f, Mathf.Sin(x * Mathf.PI)) * (v.Phase2 > 0.5f ? 0.85f : 1f);
            float air = OnePole(ref v.Lp, Noise(r), k);
            air = OnePole(ref v.Lp2, air, 0.3f);
            float hum = Osc(ref v.Phase, Mathf.Lerp(78f, 52f, x)) * 0.22f;
            return (air * 4f + hum) * env;
        }

        /// <summary>쇠망치 타격(2-7 '땅 울리기의 0.2~0.3초 저음'): 95 → 38Hz로 떨어지는 '쿵' + 둔탁한 잡음 + 흙 부서지는 소리. 0.28초.</summary>
        static float MaulHit(float t, float len, ref State v, System.Random r)
        {
            float x = t / len;
            float body = Osc(ref v.Phase, Mathf.Lerp(95f, 38f, Mathf.Sqrt(x)));
            float upper = Mathf.Sin(v.Phase * 2f * TwoPi) * 0.3f;
            float boom = (body + upper) * Mathf.Exp(-t * 11f);
            float thump = OnePole(ref v.Lp, Noise(r), 0.06f) * Mathf.Exp(-t * 25f) * 2.2f;
            float dirt = t > 0.02f ? BandPass(ref v.Low, ref v.Band, Noise(r), 650f, 0.6f) * Mathf.Exp(-(t - 0.02f) * 18f) * 0.35f : 0f;
            return boom * 0.9f + thump + dirt + Click(t, 0.004f, r) * 0.5f;
        }

        /// <summary>창 휘두르기: 가는 자루가 가르는 짧고 높은 바람(1.8 → 0.9kHz). 0.08·0.075초.</summary>
        static float SpearSwing(float t, float len, ref State v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Pow(1f - x, 1.5f);
            float hi = v.Phase2 > 0.5f ? 2100f : 1800f;
            float air = BandPass(ref v.Low, ref v.Band, Noise(r), Mathf.Lerp(hi, hi * 0.5f, x), 0.45f);
            return air * env;
        }

        /// <summary>창 타격: 찌르는 딸깍 + 짧은 몸통(180 → 90Hz) + 높은 '칙'. 0.09초.</summary>
        static float SpearHit(float t, float len, ref State v, System.Random r)
        {
            float x = t / len;
            float body = Osc(ref v.Phase, Mathf.Lerp(180f, 90f, x)) * Mathf.Exp(-t * 60f);
            float tick = BandPass(ref v.Low, ref v.Band, Noise(r), 3000f, 0.5f) * Mathf.Exp(-t * 90f) * 1.3f;
            return body * 0.8f + tick + Click(t, 0.003f, r) * 0.7f;
        }

        /// <summary>큰 낫 휘두르기: 길게 '쉭' 하고 올라갔다 내려오는 바람(0.6 → 3 → 0.7kHz, 다른 갈래는 반대로) + 끝에 옅은 날 울림. 0.3·0.28초.</summary>
        static float ScytheSwing(float t, float len, ref State v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Pow(Mathf.Sin(x * Mathf.PI), 0.7f);
            float arch = Mathf.Sin(x * Mathf.PI);
            float freq = v.Phase2 > 0.5f ? Mathf.Lerp(3000f, 700f, x) : 600f + 2400f * arch;
            float air = BandPass(ref v.Low, ref v.Band, Noise(r), freq, 0.35f);
            float ring = x > 0.5f ? Ring(t - len * 0.5f, 2300f, 18f) * 0.05f : 0f;
            return air * env + ring;
        }

        /// <summary>큰 낫 타격: 베어 지나가는 높은 '샥' + 낮은 몸통 + 날 울림. 0.12초.</summary>
        static float ScytheHit(float t, float len, ref State v, System.Random r)
        {
            float x = t / len;
            float slice = BandPass(ref v.Low, ref v.Band, Noise(r), Mathf.Lerp(4200f, 2800f, x), 0.4f) * Mathf.Exp(-t * 35f) * 1.4f;
            float body = Osc(ref v.Phase, Mathf.Lerp(140f, 70f, x)) * Mathf.Exp(-t * 40f) * 0.4f;
            return slice + body + Ring(t, 2600f, 25f) * 0.15f + Click(t, 0.003f, r) * 0.3f;
        }

        /// <summary>도끼 휘두르기: 중간 굵기 바람(0.9 → 0.5kHz) + 작은 웅. 0.14·0.15초.</summary>
        static float AxeSwing(float t, float len, ref State v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Sin(x * Mathf.PI);
            float top = v.Phase2 > 0.5f ? 1000f : 900f;
            float air = BandPass(ref v.Low, ref v.Band, Noise(r), Mathf.Lerp(top, top * 0.55f, x), 0.6f);
            float hum = Osc(ref v.Phase, Mathf.Lerp(130f, 100f, x)) * 0.08f;
            return (air + hum) * env;
        }

        /// <summary>도끼 타격(찍는 소리): 강한 딸깍 + '턱' 몸통(210 → 120Hz) + 나무가 울리는 공명(700Hz). 0.13초.</summary>
        static float AxeHit(float t, float len, ref State v, System.Random r)
        {
            float x = t / len;
            float thock = Osc(ref v.Phase, Mathf.Lerp(210f, 120f, x)) * Mathf.Exp(-t * 40f);
            float wood = BandPass(ref v.Low, ref v.Band, Noise(r) * Mathf.Exp(-t * 60f), 700f, 0.12f) * 1.6f;
            float flesh = OnePole(ref v.Lp, Noise(r), 0.1f) * Mathf.Exp(-t * 45f);
            return thock * 0.8f + wood + flesh + Click(t, 0.004f, r) * 0.9f;
        }

        /// <summary>단검 휘두르기: 아주 짧고 높은 '슥'(3.5·4kHz). 0.055·0.05초.</summary>
        static float DaggerSwing(float t, float len, ref State v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Clamp01(t / 0.006f) * (1f - x) * (1f - x);
            float air = BandPass(ref v.Low, ref v.Band, Noise(r), v.Phase2 > 0.5f ? 4000f : 3500f, 0.5f);
            return air * env;
        }

        /// <summary>단검 타격: 빠른 '칙'(5kHz 15ms) + 작은 몸통. 0.06초.</summary>
        static float DaggerHit(float t, float len, ref State v, System.Random r)
        {
            float tick = BandPass(ref v.Low, ref v.Band, Noise(r), 5000f, 0.5f) * Mathf.Exp(-t * 110f) * 1.4f;
            float body = Osc(ref v.Phase, 250f) * Mathf.Exp(-t * 80f) * 0.45f;
            return tick + body + Click(t, 0.002f, r) * 0.5f;
        }

        /// <summary>사슬 철퇴 휘두르기: 무거운 바람 + 사슬 고리가 부딪치는 짤랑(12~30ms마다 쇳소리 하나). 0.26·0.24초.</summary>
        static float FlailSwing(float t, float len, ref State v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Sin(x * Mathf.PI);
            float air = OnePole(ref v.Lp, Noise(r), Mathf.Lerp(0.03f, 0.09f, env));
            air = OnePole(ref v.Lp2, air, 0.35f) * 3f;
            // 짤랑: Low2 = 다음 고리 소리 시각, Band2 = 지금 고리 소리 시작 시각.
            if (t >= v.Low2)
            {
                v.Band2 = t;
                v.Low2 = t + 0.012f + (float)r.NextDouble() * (v.Phase2 > 0.5f ? 0.014f : 0.018f);
                v.Phase = 2600f + (float)r.NextDouble() * 900f;
            }
            float since = t - v.Band2;
            float clink = Ring(since, v.Phase, 140f) * 0.22f;
            return air * env + clink * (0.4f + 0.6f * env);
        }

        /// <summary>사슬 철퇴 타격: 쇠공이 박히는 묵직한 몸통(120 → 50Hz) + 쇠 부딪치는 '깡' + 뒤따르는 사슬 짤랑 둘. 0.18초.</summary>
        static float FlailHit(float t, float len, ref State v, System.Random r)
        {
            float x = t / len;
            float body = Osc(ref v.Phase, Mathf.Lerp(120f, 50f, x)) * Mathf.Exp(-t * 22f);
            float clank = Ring(t, 1100f, 30f) * 0.3f;
            float chain = (t > 0.05f ? Ring(t - 0.05f, 3100f, 120f) : 0f) * 0.18f + (t > 0.085f ? Ring(t - 0.085f, 2800f, 120f) : 0f) * 0.14f;
            float thump = OnePole(ref v.Lp, Noise(r), 0.08f) * Mathf.Exp(-t * 35f) * 1.5f;
            return body * 0.85f + clank + chain + thump + Click(t, 0.004f, r) * 0.6f;
        }
    }
}
