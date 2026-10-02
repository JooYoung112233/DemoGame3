using UnityEngine;

namespace Demo6.Game
{
    public enum SfxKind
    {
        Swing,
        Hit,
        Crit,
        Hurt,
        Kill,
        Dodge,
        BoarCharge,
        ArcherShot,
        /// <summary>무너짐(3차): 깨지는 소리.</summary>
        Break,
        /// <summary>둥지 껍질에 막힘: '팅'.</summary>
        Shell,
        /// <summary>던전: 보통 벽을 쳤을 때 '퉁'(히트스톱 없음).</summary>
        Thud,
        /// <summary>판자벽·금 간 벽이 부서짐.</summary>
        WallBreak,
        /// <summary>궤짝 열기.</summary>
        Chest,
        /// <summary>장비·물건 줍기.</summary>
        Pickup,
        /// <summary>벽 등잔 켜기.</summary>
        Lamp,
        /// <summary>곡괭이질(광맥·금 간 벽).</summary>
        Pick,
        /// <summary>권양기 말뚝 켜기.</summary>
        Stake,
        /// <summary>레벨업.</summary>
        LevelUp,
        /// <summary>장비가 빛기둥과 함께 떨어짐.</summary>
        Loot,
    }

    /// <summary>
    /// 기획 3-6 임시 효과음 5종(휘두르기, 타격, 치명 타격, 피격, 처치). 소리 파일 없이 코드로 파형을 만든다.
    /// 음높이를 ±5% 무작위로 바꾸고, 한 프레임에 같은 종류는 한 번만 낸다(쌍검·회오리 다중 타격 겹침 방지).
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int Voices = 12;

        static Sfx _instance;
        AudioSource[] _sources;
        AudioClip[] _clips;
        readonly int[] _lastFrame = new int[System.Enum.GetValues(typeof(SfxKind)).Length];
        int _next;

        /// <summary>custom이 있으면 그 소리, 없으면 연결된 그림·소리 묶음의 소리, 그것도 없으면 코드로 만든 임시 소리.</summary>
        public static void Play(SfxKind kind, AudioClip custom = null)
        {
            if (!_instance || !Tuning.Sound) return;
            _instance.PlayInternal(kind, custom);
        }

        void Awake()
        {
            _instance = this;
            _sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sources[i] = src;
            }
            _clips = new[]
            {
                Build("swing", 0.09f, Swing),
                Build("hit", 0.08f, Hit),
                Build("crit", 0.14f, Crit),
                Build("hurt", 0.13f, Hurt),
                Build("kill", 0.11f, Kill),
                Build("dodge", 0.12f, Dodge),
                Build("boar", 0.22f, BoarCharge),
                Build("archer", 0.1f, ArcherShot),
                Build("break", 0.28f, BreakSound),
                Build("shell", 0.12f, Shell),
                Build("thud", 0.12f, Thud),
                Build("wallbreak", 0.35f, WallBreak),
                Build("chest", 0.25f, ChestOpen),
                Build("pickup", 0.12f, Pickup),
                Build("lamp", 0.3f, Lamp),
                Build("pick", 0.14f, Pick),
                Build("stake", 0.4f, StakeOn),
                Build("levelup", 0.6f, LevelUp),
                Build("loot", 0.35f, Loot),
            };
            for (int i = 0; i < _lastFrame.Length; i++) _lastFrame[i] = -1;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_clips == null) return;
            foreach (var c in _clips)
                if (c) Destroy(c);
        }

        void PlayInternal(SfxKind kind, AudioClip custom)
        {
            int k = (int)kind;
            if (_lastFrame[k] == Time.frameCount) return;
            _lastFrame[k] = Time.frameCount;
            var src = _sources[_next];
            _next = (_next + 1) % Voices;
            src.clip = custom ? custom : ArtClip(kind) ?? _clips[k];
            src.pitch = Random.Range(0.95f, 1.05f);
            src.volume = Tuning.SoundVolume * Volume(kind);
            src.Play();
        }

        static AudioClip ArtClip(SfxKind kind)
        {
            var set = ArtRuntime.Active;
            if (!set) return null;
            var s = set.sounds;
            AudioClip c;
            switch (kind)
            {
                case SfxKind.Swing: c = s.swing; break;
                case SfxKind.Hit: c = s.hit; break;
                case SfxKind.Crit: c = s.crit; break;
                case SfxKind.Hurt: c = s.hurt; break;
                case SfxKind.Kill: c = s.kill; break;
                case SfxKind.Dodge: c = s.dodge; break;
                case SfxKind.BoarCharge: c = s.boarCharge; break;
                case SfxKind.ArcherShot: c = s.archerShot; break;
                default: c = null; break;
            }
            return c ? c : null;
        }

        static float Volume(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.Swing: return 0.35f;
                case SfxKind.Hit: return 0.8f;
                case SfxKind.Crit: return 0.9f;
                case SfxKind.Hurt: return 0.8f;
                case SfxKind.Dodge: return 0.3f;
                case SfxKind.BoarCharge: return 0.55f;
                case SfxKind.ArcherShot: return 0.45f;
                case SfxKind.Break: return 0.95f;
                case SfxKind.Shell: return 0.5f;
                case SfxKind.Thud: return 0.45f;
                case SfxKind.Pickup: return 0.5f;
                case SfxKind.Lamp: return 0.5f;
                case SfxKind.Loot: return 0.6f;
                default: return 0.75f;
            }
        }

        delegate float Wave(float t, float length, ref float state, System.Random noise);

        static AudioClip Build(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var noise = new System.Random(name.GetHashCode());
            float state = 0f;
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(wave(i / (float)SampleRate, seconds, ref state, noise), -1f, 1f);
            var clip = AudioClip.Create("sfx_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        /// <summary>바람 가르는 소리: 거른 잡음이 짧게 부풀었다 사라진다.</summary>
        static float Swing(float t, float len, ref float lp, System.Random r)
        {
            float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
            float cutoff = Mathf.Lerp(0.05f, 0.35f, t / len);
            lp += (Noise(r) - lp) * cutoff;
            return lp * env * 1.6f;
        }

        /// <summary>둔탁한 타격: 내려가는 저음 + 첫 5ms 딸깍.</summary>
        static float Hit(float t, float len, ref float phase, System.Random r)
        {
            float freq = Mathf.Lerp(170f, 70f, t / len);
            phase += freq / SampleRate;
            float body = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 38f);
            float click = t < 0.005f ? Noise(r) * (1f - t / 0.005f) : 0f;
            return body * 0.9f + click * 0.6f;
        }

        /// <summary>치명: 더 높은 타격음에 쇳소리 배음을 얹는다.</summary>
        static float Crit(float t, float len, ref float phase, System.Random r)
        {
            float freq = Mathf.Lerp(260f, 110f, t / len);
            phase += freq / SampleRate;
            float body = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 26f);
            float ring = Mathf.Sin(t * 2f * Mathf.PI * 1320f) * Mathf.Exp(-t * 30f) * 0.35f;
            float click = t < 0.006f ? Noise(r) * (1f - t / 0.006f) : 0f;
            return body * 0.85f + ring + click * 0.7f;
        }

        /// <summary>피격: 낮게 떨리는 소리.</summary>
        static float Hurt(float t, float len, ref float phase, System.Random r)
        {
            phase += 95f / SampleRate;
            float square = Mathf.Sign(Mathf.Sin(phase * 2f * Mathf.PI));
            return square * 0.45f * Mathf.Exp(-t * 18f) + Noise(r) * 0.12f * Mathf.Exp(-t * 30f);
        }

        /// <summary>구르기: 낮고 짧은 바람 소리.</summary>
        static float Dodge(float t, float len, ref float lp, System.Random r)
        {
            float env = Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI);
            lp += (Noise(r) - lp) * 0.08f;
            return lp * env * 1.8f;
        }

        /// <summary>멧돼지 돌진: 낮게 그르렁거리며 출발.</summary>
        static float BoarCharge(float t, float len, ref float phase, System.Random r)
        {
            phase += Mathf.Lerp(70f, 110f, t / len) / SampleRate;
            float growl = Mathf.Sin(phase * 2f * Mathf.PI) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 28f));
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 9f);
            return growl * env * 0.8f + Noise(r) * 0.1f * env;
        }

        /// <summary>궁수: 활시위 튕기는 소리.</summary>
        static float ArcherShot(float t, float len, ref float phase, System.Random r)
        {
            phase += Mathf.Lerp(520f, 300f, t / len) / SampleRate;
            float twang = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 35f);
            float click = t < 0.004f ? Noise(r) : 0f;
            return twang * 0.7f + click * 0.4f;
        }

        /// <summary>무너짐: 무겁게 쩍 갈라지는 소리(잡음 터짐 + 낮은 울림).</summary>
        static float BreakSound(float t, float len, ref float phase, System.Random r)
        {
            phase += Mathf.Lerp(120f, 55f, t / len) / SampleRate;
            float boom = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 11f);
            float crack = Noise(r) * Mathf.Exp(-t * 26f) * (t < 0.08f ? 1f : 0.5f);
            return boom * 0.8f + crack * 0.6f;
        }

        /// <summary>껍질: 높고 짧은 금속성 '팅'.</summary>
        static float Shell(float t, float len, ref float phase, System.Random r)
        {
            float ring = Mathf.Sin(t * 2f * Mathf.PI * 1760f) * Mathf.Exp(-t * 40f) + Mathf.Sin(t * 2f * Mathf.PI * 2640f) * Mathf.Exp(-t * 55f) * 0.5f;
            return ring * 0.6f;
        }

        /// <summary>벽 '퉁': 낮고 짧게 막힌 소리.</summary>
        static float Thud(float t, float len, ref float phase, System.Random r)
        {
            phase += Mathf.Lerp(110f, 60f, t / len) / SampleRate;
            return Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 40f) * 0.8f + Noise(r) * 0.15f * Mathf.Exp(-t * 80f);
        }

        /// <summary>벽이 부서짐: 나무 쪼개지는 잡음 + 낮은 울림.</summary>
        static float WallBreak(float t, float len, ref float phase, System.Random r)
        {
            phase += Mathf.Lerp(90f, 45f, t / len) / SampleRate;
            float crackle = Noise(r) * Mathf.Exp(-t * 12f) * (0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 37f));
            return crackle * 0.7f + Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 9f) * 0.6f;
        }

        /// <summary>궤짝: 경첩 삐걱 + 뚜껑 닫히는 탁.</summary>
        static float ChestOpen(float t, float len, ref float phase, System.Random r)
        {
            phase += Mathf.Lerp(300f, 520f, t / len) / SampleRate;
            float creak = Mathf.Sin(phase * 2f * Mathf.PI) * (t < 0.18f ? 0.35f : 0f) * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 40f));
            float knock = t > 0.18f ? Noise(r) * Mathf.Exp(-(t - 0.18f) * 60f) * 0.6f : 0f;
            return creak + knock;
        }

        /// <summary>줍기: 짧게 올라가는 두 음.</summary>
        static float Pickup(float t, float len, ref float phase, System.Random r)
        {
            float f = t < len * 0.5f ? 660f : 990f;
            return Mathf.Sin(t * 2f * Mathf.PI * f) * Mathf.Exp(-(t % (len * 0.5f)) * 30f) * 0.5f;
        }

        /// <summary>등잔: 불붙는 '화' 잡음.</summary>
        static float Lamp(float t, float len, ref float lp, System.Random r)
        {
            lp += (Noise(r) - lp) * 0.2f;
            return lp * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 7f) * 1.4f;
        }

        /// <summary>곡괭이: 돌을 찍는 '깡'.</summary>
        static float Pick(float t, float len, ref float phase, System.Random r)
        {
            float ring = Mathf.Sin(t * 2f * Mathf.PI * 1250f) * Mathf.Exp(-t * 45f) * 0.5f;
            float click = t < 0.006f ? Noise(r) : 0f;
            return ring + click * 0.6f + Noise(r) * 0.15f * Mathf.Exp(-t * 50f);
        }

        /// <summary>말뚝: 쇠 고리가 걸리는 낮은 '쿵-챙'.</summary>
        static float StakeOn(float t, float len, ref float phase, System.Random r)
        {
            phase += 70f / SampleRate;
            float thump = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 12f) * 0.7f;
            float chime = t > 0.08f ? Mathf.Sin(t * 2f * Mathf.PI * 880f) * Mathf.Exp(-(t - 0.08f) * 9f) * 0.25f : 0f;
            return thump + chime;
        }

        /// <summary>레벨업: 따뜻한 세 음 오름.</summary>
        static float LevelUp(float t, float len, ref float phase, System.Random r)
        {
            float step = len / 3f;
            int i = Mathf.Min(2, (int)(t / step));
            float f = i == 0 ? 523f : i == 1 ? 659f : 784f;
            float local = t - i * step;
            return Mathf.Sin(t * 2f * Mathf.PI * f) * Mathf.Exp(-local * 6f) * 0.45f;
        }

        /// <summary>장비 떨어짐: 맑은 '띵'.</summary>
        static float Loot(float t, float len, ref float phase, System.Random r)
        {
            return (Mathf.Sin(t * 2f * Mathf.PI * 1046f) + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 1568f)) * Mathf.Exp(-t * 9f) * 0.4f;
        }

        /// <summary>처치: 위로 튀는 짧은 소리.</summary>
        static float Kill(float t, float len, ref float phase, System.Random r)
        {
            float freq = Mathf.Lerp(380f, 900f, t / len);
            phase += freq / SampleRate;
            float pop = Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 30f);
            return pop * 0.7f + Noise(r) * 0.25f * Mathf.Exp(-t * 60f);
        }
    }
}
