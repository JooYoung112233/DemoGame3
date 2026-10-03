using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 효과음 종류. 다른 코드가 이름으로 부르므로 기존 이름은 바꾸지 않고, 새 종류는 맨 끝에 더한다.
    /// </summary>
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
        /// <summary>둥지 껍질에 막힘: 딱딱한 껍질 '탁'.</summary>
        Shell,
        /// <summary>던전: 보통 벽을 쳤을 때 '퉁'(히트스톱 없음).</summary>
        Thud,
        /// <summary>판자벽·금 간 벽이 부서짐.</summary>
        WallBreak,
        /// <summary>궤짝 열기(잠긴 문·금고·도시락통도 같이 쓴다): 무거운 나무 삐걱 + 쿵.</summary>
        Chest,
        /// <summary>장비·물건 줍기: 낮은 가죽 소리 + 동전.</summary>
        Pickup,
        /// <summary>벽 등잔 켜기: 불붙는 '화'.</summary>
        Lamp,
        /// <summary>곡괭이질(광맥·금 간 벽).</summary>
        Pick,
        /// <summary>권양기 말뚝 켜기: 쇠사슬.</summary>
        Stake,
        /// <summary>레벨업: 깊은 종 + 단조 화음.</summary>
        LevelUp,
        /// <summary>장비가 빛기둥과 함께 떨어짐: 낮은 종.</summary>
        Loot,
        /// <summary>다크 판타지 1차: 피 튀김 '철퍽'(살 있는 적을 벨 때 타격음 위에 겹친다).</summary>
        Blood,
        /// <summary>다크 판타지 1차: 무거운 몸이 쓰러지는 소리(멧돼지·정예 처치).</summary>
        BodyFall,
        /// <summary>다크 판타지 1차: 플레이어가 쓰러질 때 낮은 울림 경고. Sfx가 CombatEvents.PlayerDowned에서 스스로 낸다.</summary>
        PlayerDeath,
        /// <summary>다크 판타지 1차: 흙·돌 위 무거운 발소리(던전 DungeonAudio가 걸음에 맞춰 낸다).</summary>
        Footstep,
    }

    /// <summary>
    /// 코드로 합성하는 임시 효과음(소리 파일 없음). 기획 3-6 전투음 5종에 던전 물체음을 더했고,
    /// 다크 판타지 1차(기획/다크판타지-분위기-1차.md '소리')에서 디아블로 1·2 톤으로 다시 만들었다:
    /// 타격·치명은 낮고 묵직하게, 처치는 젖은 둔탁음, 줍기·빛기둥·레벨업은 낮은 종·단조, 궤짝은 무거운 나무.
    /// 손맛 비교 기준(M0a)을 지키려고 타격·치명·처치의 첫 5~6ms 딸깍과 타격·치명 길이(0.08초·0.14초)는 그대로 둔다.
    /// 음높이를 조금 무작위로 바꾸고, 한 프레임에 같은 종류는 한 번만 낸다(쌍검·회오리 다중 타격 겹침 방지).
    /// 잔향은 여기서 걸지 않는다: 던전에서만 DungeonAudio가 듣는 이(카메라)에 동굴 잔향을 달아 이 소리들에도 걸린다.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int SampleRate = 44100;
        /// <summary>동시에 나는 소리 수. 발소리·피 소리가 더해져 12에서 늘렸다(프로젝트 실제 음성 32 안).</summary>
        const int Voices = 16;
        const float TwoPi = 2f * Mathf.PI;

        static readonly int KindCount = System.Enum.GetValues(typeof(SfxKind)).Length;

        static Sfx _instance;
        AudioSource[] _sources;
        float[] _started;
        /// <summary>종류마다 1개 이상의 합성 클립(발소리·피는 갈래가 여럿).</summary>
        AudioClip[][] _clips;
        readonly int[] _lastFrame = new int[KindCount];
        int _next;

        /// <summary>custom이 있으면 그 소리, 없으면 연결된 그림·소리 묶음의 소리, 그것도 없으면 코드로 만든 임시 소리.</summary>
        public static void Play(SfxKind kind, AudioClip custom = null)
        {
            if (!_instance || !Tuning.Sound) return;
            _instance.PlayInternal(kind, custom, 1f, 1f);
        }

        /// <summary>
        /// 음량·음높이 배율을 주어 낸다(피 튀김 크기, 발소리 좌우 차이 등). 배율은 기본 음량 표에 곱한다.
        /// 같은 프레임 같은 종류 한 번 규칙은 똑같이 따른다.
        /// </summary>
        public static void PlayScaled(SfxKind kind, float volumeScale, float pitchScale = 1f)
        {
            if (!_instance || !Tuning.Sound) return;
            _instance.PlayInternal(kind, null, Mathf.Max(0f, volumeScale), Mathf.Clamp(pitchScale, 0.25f, 3f));
        }

        void Awake()
        {
            _instance = this;
            _sources = new AudioSource[Voices];
            _started = new float[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sources[i] = src;
                _started[i] = -999f;
            }

            _clips = new AudioClip[KindCount][];
            Set(SfxKind.Swing, Build("swing", 0.09f, Swing));
            Set(SfxKind.Hit, Build("hit", 0.08f, Hit));
            Set(SfxKind.Crit, Build("crit", 0.14f, Crit));
            Set(SfxKind.Hurt, Build("hurt", 0.15f, Hurt));
            Set(SfxKind.Kill, Build("kill", 0.2f, Kill));
            Set(SfxKind.Dodge, Build("dodge", 0.12f, Dodge));
            Set(SfxKind.BoarCharge, Build("boar", 0.3f, BoarCharge));
            Set(SfxKind.ArcherShot, Build("archer", 0.12f, ArcherShot));
            Set(SfxKind.Break, Build("break", 0.32f, BreakSound));
            Set(SfxKind.Shell, Build("shell", 0.12f, Shell));
            Set(SfxKind.Thud, Build("thud", 0.12f, Thud));
            Set(SfxKind.WallBreak, Build("wallbreak", 0.45f, WallBreak));
            Set(SfxKind.Chest, Build("chest", 0.65f, ChestOpen));
            Set(SfxKind.Pickup, Build("pickup", 0.2f, Pickup));
            Set(SfxKind.Lamp, Build("lamp", 0.45f, Lamp));
            Set(SfxKind.Pick, Build("pick", 0.16f, Pick));
            Set(SfxKind.Stake, Build("stake", 0.7f, StakeOn));
            Set(SfxKind.LevelUp, Build("levelup", 1.4f, LevelUp));
            Set(SfxKind.Loot, Build("loot", 0.7f, Loot));
            Set(SfxKind.Blood, Build("blood0", 0.17f, Blood, 0), Build("blood1", 0.17f, Blood, 1));
            Set(SfxKind.BodyFall, Build("bodyfall", 0.5f, BodyFall));
            Set(SfxKind.PlayerDeath, Build("playerdeath", 1.6f, PlayerDeath));
            Set(SfxKind.Footstep, Build("step0", 0.14f, Footstep, 0), Build("step1", 0.14f, Footstep, 1), Build("step2", 0.14f, Footstep, 2));
            // 합성을 빠뜨린 종류가 있으면 타격음으로 채운다(새 종류를 더해도 소리는 난다).
            var hit = _clips[(int)SfxKind.Hit];
            for (int i = 0; i < KindCount; i++)
                if (_clips[i] == null || _clips[i].Length == 0) _clips[i] = hit;

            for (int i = 0; i < _lastFrame.Length; i++) _lastFrame[i] = -1;
            CombatEvents.PlayerDowned += OnPlayerDowned;
        }

        void OnDestroy()
        {
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            if (_instance == this) _instance = null;
            if (_clips == null) return;
            var hit = _clips[(int)SfxKind.Hit];
            for (int k = 0; k < _clips.Length; k++)
            {
                var set = _clips[k];
                // 빈 종류를 채운 타격음 묶음은 한 번만 지운다.
                if (set == null || (set == hit && k != (int)SfxKind.Hit)) continue;
                for (int i = 0; i < set.Length; i++)
                    if (set[i]) Destroy(set[i]);
            }
        }

        /// <summary>쓰러짐: 낮은 울림 경고(두 시험장 공통). 피격음과 같은 프레임에 겹쳐 난다.</summary>
        void OnPlayerDowned() => Play(SfxKind.PlayerDeath);

        void Set(SfxKind kind, params AudioClip[] clips) => _clips[(int)kind] = clips;

        void PlayInternal(SfxKind kind, AudioClip custom, float volumeScale, float pitchScale)
        {
            int k = (int)kind;
            if ((uint)k >= (uint)_lastFrame.Length) return;
            if (_lastFrame[k] == Time.frameCount) return;
            _lastFrame[k] = Time.frameCount;

            var clip = custom ? custom : ArtClip(kind);
            if (!clip)
            {
                var set = _clips[k];
                clip = set.Length == 1 ? set[0] : set[Random.Range(0, set.Length)];
            }
            if (!clip) return;

            var src = _sources[PickVoice()];
            src.clip = clip;
            float spread = PitchSpread(kind);
            src.pitch = Random.Range(1f - spread, 1f + spread) * pitchScale;
            src.volume = Mathf.Clamp01(Tuning.SoundVolume * Volume(kind) * volumeScale);
            src.Play();
        }

        /// <summary>쉬는 음성을 먼저 쓰고, 다 바쁘면 가장 먼저 시작한 소리를 끊는다(긴 레벨업 종이 발소리에 바로 잘리지 않게).</summary>
        int PickVoice()
        {
            int best = _next;
            float oldest = float.MaxValue;
            for (int n = 0; n < Voices; n++)
            {
                int i = (_next + n) % Voices;
                if (!_sources[i].isPlaying)
                {
                    best = i;
                    break;
                }
                if (_started[i] < oldest)
                {
                    oldest = _started[i];
                    best = i;
                }
            }
            _next = (best + 1) % Voices;
            _started[best] = Time.unscaledTime;
            return best;
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
                case SfxKind.Kill: return 0.85f;
                case SfxKind.Dodge: return 0.3f;
                case SfxKind.BoarCharge: return 0.55f;
                case SfxKind.ArcherShot: return 0.45f;
                case SfxKind.Break: return 0.95f;
                case SfxKind.Shell: return 0.5f;
                case SfxKind.Thud: return 0.45f;
                case SfxKind.WallBreak: return 0.85f;
                case SfxKind.Chest: return 0.7f;
                case SfxKind.Pickup: return 0.55f;
                case SfxKind.Lamp: return 0.55f;
                case SfxKind.Stake: return 0.65f;
                case SfxKind.LevelUp: return 0.8f;
                case SfxKind.Loot: return 0.6f;
                case SfxKind.Blood: return 0.55f;
                case SfxKind.BodyFall: return 0.9f;
                case SfxKind.PlayerDeath: return 0.85f;
                case SfxKind.Footstep: return 0.33f;
                default: return 0.75f;
            }
        }

        /// <summary>음높이 무작위 폭. 발소리는 넓게(같은 소리 반복 티를 줄임), 종소리는 좁게.</summary>
        static float PitchSpread(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.Footstep: return 0.07f;
                case SfxKind.LevelUp:
                case SfxKind.Loot:
                case SfxKind.PlayerDeath: return 0.02f;
                default: return 0.05f;
            }
        }

        // ───────────────────────── 합성 ─────────────────────────

        /// <summary>합성 중 상태(위상·거름 상태·사슬 고리 같은 사건). 소리마다 새로 시작한다.</summary>
        struct SynthState
        {
            public int Variant;
            public float Phase, Phase2;
            public float Lp, Lp2, Lp3;
            public float Low, Band, Low2, Band2;
            public float EventA, EventB, FreqA, FreqB;
        }

        delegate float Wave(float t, float length, ref SynthState v, System.Random noise);

        /// <summary>파형을 표본으로 굽는다. 끝 딸깍을 막으려고 마지막 6ms(긴 소리는 길이의 8%)를 줄여 끝낸다(시작·길이는 그대로).</summary>
        static AudioClip Build(string name, float seconds, Wave wave, int variant = 0)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var noise = new System.Random(StableSeed(name));
            var v = new SynthState { Variant = variant, EventA = -1f, EventB = -1f };
            float fade = Mathf.Max(0.006f, seconds * 0.08f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float s = wave(t, seconds, ref v, noise);
                float tail = Mathf.Clamp01((seconds - t) / fade);
                data[i] = Mathf.Clamp(s * tail, -1f, 1f);
            }
            var clip = AudioClip.Create("sfx_" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>실행마다 같은 잡음이 나오게 하는 이름 해시(FNV-1a).</summary>
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

        static float Tone(float t, float freq) => Mathf.Sin(t * TwoPi * freq);

        /// <summary>위상을 주파수만큼 돌리고 사인값을 낸다(위상은 0~1로 감아 정밀도를 지킨다).</summary>
        static float Osc(ref float phase, float freq)
        {
            phase += freq / SampleRate;
            phase -= Mathf.Floor(phase);
            return Mathf.Sin(phase * TwoPi);
        }

        /// <summary>한 극 저역 통과. k는 0~1(클수록 밝다).</summary>
        static float OnePole(ref float state, float input, float k)
        {
            state += (input - state) * k;
            return state;
        }

        /// <summary>상태 변수 필터(챔벌린) 대역 통과. damping이 작을수록 공명이 길다(0.05~1).</summary>
        static float BandPass(ref float low, ref float band, float input, float freq, float damping)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(freq, SampleRate / 6f) / SampleRate);
            low += f * band;
            float high = input - low - damping * band;
            band += f * high;
            return band;
        }

        /// <summary>손맛의 첫 딸깍: length 동안 줄어드는 잡음(M0a와 같은 모양).</summary>
        static float Click(float t, float length, System.Random r) => t < length ? Noise(r) * (1f - t / length) : 0f;

        /// <summary>바람 가르는 소리: 더 낮게 거른 잡음 + 작은 '웅'. 길이는 그대로 0.09초.</summary>
        static float Swing(float t, float len, ref SynthState v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Sin(x * Mathf.PI);
            float air = OnePole(ref v.Lp, Noise(r), Mathf.Lerp(0.03f, 0.2f, x));
            air = OnePole(ref v.Lp2, air, 0.4f);
            float whum = Osc(ref v.Phase, Mathf.Lerp(150f, 85f, x)) * 0.16f;
            return (air * 2.4f + whum) * env;
        }

        /// <summary>타격: 첫 5ms 딸깍(그대로) + 더 낮게 내려가는 몸통(130→52Hz, 작은 스피커용 2배음) + 살 맞는 둔탁한 잡음. 0.08초.</summary>
        static float Hit(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float body = Osc(ref v.Phase, Mathf.Lerp(130f, 52f, x));
            float upper = Mathf.Sin(v.Phase * 2f * TwoPi) * 0.35f;
            float thump = (body + upper) * Mathf.Exp(-t * 30f);
            float flesh = OnePole(ref v.Lp, Noise(r), 0.12f) * Mathf.Exp(-t * 45f) * 1.6f;
            return thump * 0.65f + flesh + Click(t, 0.005f, r) * 0.6f;
        }

        /// <summary>치명: 첫 6ms 딸깍(그대로) + 낮은 몸통 + 거친 쇳소리(비조화 배음) + 뼈 부서지는 잡음. 0.14초.</summary>
        static float Crit(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float body = Osc(ref v.Phase, Mathf.Lerp(170f, 55f, x));
            float upper = Mathf.Sin(v.Phase * 2f * TwoPi) * 0.3f;
            float thump = (body + upper) * Mathf.Exp(-t * 20f);
            float ring = (Tone(t, 587f) + Tone(t, 911f) * 0.6f) * Mathf.Exp(-t * 26f) * 0.2f;
            float crunch = OnePole(ref v.Lp, Noise(r), 0.18f) * Mathf.Exp(-t * 32f) * 1.1f;
            return thump * 0.65f + ring + crunch + Click(t, 0.006f, r) * 0.7f;
        }

        /// <summary>피격: 낮게 떨리는 신음 같은 톱니(거름) + 맞는 둔탁음.</summary>
        static float Hurt(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float wobble = 1f + 0.05f * Mathf.Sin(t * TwoPi * 17f);
            v.Phase += Mathf.Lerp(105f, 68f, x) * wobble / SampleRate;
            v.Phase -= Mathf.Floor(v.Phase);
            float saw = v.Phase * 2f - 1f;
            float growl = OnePole(ref v.Lp, saw, 0.08f) * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 15f);
            float impact = OnePole(ref v.Lp2, Noise(r), 0.15f) * Mathf.Exp(-t * 40f) * 1.5f;
            return growl * 0.75f + impact + Click(t, 0.005f, r) * 0.45f;
        }

        /// <summary>처치: '뾱' 대신 젖은 둔탁음. 첫 5ms 딸깍(마무리 타격의 손맛) + 낮은 쿵 + 아래로 미끄러지며 꿀렁이는 '철퍽'.</summary>
        static float Kill(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float thump = Osc(ref v.Phase, Mathf.Lerp(120f, 42f, x)) * Mathf.Exp(-t * 16f);
            float gurgle = 0.55f + 0.45f * Mathf.Sin(t * TwoPi * 38f);
            float wet = BandPass(ref v.Low, ref v.Band, Noise(r), Mathf.Lerp(950f, 260f, Mathf.Sqrt(x)), 0.35f);
            wet *= Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 13f) * gurgle * 1.4f;
            return thump * 0.65f + wet + Click(t, 0.005f, r) * 0.6f;
        }

        /// <summary>구르기: 낮고 짧은 바람 + 옷자락 스침.</summary>
        static float Dodge(float t, float len, ref SynthState v, System.Random r)
        {
            float x = Mathf.Clamp01(t / len);
            float env = Mathf.Sin(x * Mathf.PI);
            float air = OnePole(ref v.Lp, Noise(r), 0.06f) * 2.6f;
            float cloth = OnePole(ref v.Lp2, Noise(r), 0.25f) * (0.5f + 0.5f * Mathf.Sin(t * TwoPi * 55f)) * 0.3f;
            return (air + cloth) * env;
        }

        /// <summary>멧돼지 돌진: 낮고 거친 짐승 그르렁 + 콧김.</summary>
        static float BoarCharge(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            v.Phase += Mathf.Lerp(62f, 95f, x) / SampleRate;
            v.Phase -= Mathf.Floor(v.Phase);
            float saw = v.Phase * 2f - 1f;
            float rough = 0.55f + 0.45f * Mathf.Sin(t * TwoPi * 24f);
            float growl = OnePole(ref v.Lp, saw, 0.06f) * rough;
            float breath = OnePole(ref v.Lp2, Noise(r), 0.1f) * 0.6f;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 6f);
            return (growl * 0.95f + breath) * env;
        }

        /// <summary>궁수: 낮게 우는 활시위 + 나무 활대 딱.</summary>
        static float ArcherShot(float t, float len, ref SynthState v, System.Random r)
        {
            float s = Osc(ref v.Phase, Mathf.Lerp(330f, 210f, t / len));
            float buzz = Mathf.Sin(v.Phase * 3f * TwoPi) * 0.4f;
            float twang = (s + buzz) * Mathf.Exp(-t * 30f);
            float wood = OnePole(ref v.Lp, Noise(r), 0.2f) * Mathf.Exp(-t * 70f);
            return twang * 0.55f + wood * 0.6f + Click(t, 0.004f, r) * 0.35f;
        }

        /// <summary>무너짐: 무겁게 쩍 갈라지는 소리(잡음 터짐 + 낮은 울림 + 부스러기).</summary>
        static float BreakSound(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float boom = Osc(ref v.Phase, Mathf.Lerp(95f, 38f, x)) * Mathf.Exp(-t * 9f);
            float crack = OnePole(ref v.Lp, Noise(r), 0.45f) * Mathf.Exp(-t * 24f) * (t < 0.07f ? 1f : 0.5f);
            float grit = OnePole(ref v.Lp2, Noise(r), 0.08f) * Mathf.Exp(-t * 7f);
            return boom * 0.75f + crack * 0.65f + grit * 0.85f;
        }

        /// <summary>껍질: 단단한 껍질에 막힌 '탁'(전보다 낮고 둔한 쇳소리).</summary>
        static float Shell(float t, float len, ref SynthState v, System.Random r)
        {
            float ring = Tone(t, 1180f) * Mathf.Exp(-t * 38f) + Tone(t, 1730f) * Mathf.Exp(-t * 55f) * 0.5f;
            float knock = OnePole(ref v.Lp, Noise(r), 0.3f) * Mathf.Exp(-t * 90f);
            return ring * 0.45f + knock * 0.9f;
        }

        /// <summary>벽 '퉁': 낮고 짧게 막힌 돌 소리.</summary>
        static float Thud(float t, float len, ref SynthState v, System.Random r)
        {
            float body = Osc(ref v.Phase, Mathf.Lerp(95f, 50f, t / len)) * Mathf.Exp(-t * 34f);
            float grit = OnePole(ref v.Lp, Noise(r), 0.15f) * Mathf.Exp(-t * 60f);
            return body * 0.8f + grit * 0.9f;
        }

        /// <summary>벽이 부서짐: 나무 쪼개지는 소리 + 낮은 울림 + 흙더미 무너짐.</summary>
        static float WallBreak(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float boom = Osc(ref v.Phase, Mathf.Lerp(80f, 36f, x)) * Mathf.Exp(-t * 7f);
            float crackle = Noise(r) * Mathf.Exp(-t * 10f) * (0.5f + 0.5f * Mathf.Sin(t * TwoPi * 31f));
            float splinter = BandPass(ref v.Low, ref v.Band, crackle, Mathf.Lerp(1600f, 700f, x), 0.5f) * 1.3f;
            float rubble = OnePole(ref v.Lp, Noise(r), 0.07f) * Mathf.Exp(-t * 4.5f) * 1.3f;
            return boom * 0.5f + splinter + rubble;
        }

        /// <summary>궤짝: 무거운 경첩이 걸렸다 미끄러지며 삐걱(나무 공명) → 0.42초에 뚜껑이 넘어가 '쿵'.</summary>
        static float ChestOpen(float t, float len, ref SynthState v, System.Random r)
        {
            const float creakEnd = 0.4f;
            float pulse = 0f;
            if (t < creakEnd)
            {
                v.Phase += Mathf.Lerp(40f, 17f, t / creakEnd) / SampleRate;
                if (v.Phase >= 1f)
                {
                    // 다음 걸림까지 간격을 조금씩 다르게.
                    v.Phase = -0.35f * (float)r.NextDouble();
                    pulse = 6f * Mathf.Clamp01(t / 0.04f) * Mathf.Clamp01((creakEnd - t) / 0.06f);
                }
            }
            float wood = BandPass(ref v.Low, ref v.Band, pulse, 470f + 50f * Mathf.Sin(t * 9f), 0.06f);
            float hinge = BandPass(ref v.Low2, ref v.Band2, pulse, 1130f, 0.09f) * 0.3f;
            float lid = 0f;
            float tt = t - 0.42f;
            if (tt > 0f)
            {
                lid = Osc(ref v.Phase2, Mathf.Lerp(85f, 45f, Mathf.Clamp01(tt / 0.2f))) * Mathf.Exp(-tt * 12f) * 0.75f
                    + OnePole(ref v.Lp, Noise(r), 0.1f) * Mathf.Exp(-tt * 30f) * 1.8f;
            }
            return wood + hinge + lid;
        }

        /// <summary>줍기: 낮은 가죽 주머니 소리 + 동전 두 번 짤랑(비조화 쇳소리).</summary>
        static float Pickup(float t, float len, ref SynthState v, System.Random r)
        {
            float leather = OnePole(ref v.Lp, Noise(r), 0.12f) * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 28f) * 1.5f;
            float pouch = Osc(ref v.Phase, 180f) * Mathf.Exp(-t * 50f) * 0.2f;
            float coins = Coin(t - 0.02f, 1420f, 2160f) + Coin(t - 0.075f, 1260f, 1930f) * 0.7f;
            return leather + pouch + coins * 0.28f;
        }

        static float Coin(float t, float f1, float f2)
        {
            if (t < 0f) return 0f;
            return (Tone(t, f1) + Tone(t, f2) * 0.6f) * Mathf.Exp(-t * 32f);
        }

        /// <summary>등잔: 불붙는 '화'(밝아지며 부풀었다 잦아드는 잡음 + 타닥 + 낮은 불꽃 울림).</summary>
        static float Lamp(float t, float len, ref SynthState v, System.Random r)
        {
            float swell = Mathf.Clamp01(t / 0.06f) * Mathf.Exp(-t * 5f);
            float roar = OnePole(ref v.Lp, Noise(r), Mathf.Lerp(0.05f, 0.18f, Mathf.Clamp01(t / 0.08f)));
            roar = OnePole(ref v.Lp2, roar, 0.3f);
            if (r.NextDouble() < 0.0012) v.EventA = t;
            float crackle = v.EventA >= 0f && t - v.EventA < 0.003f ? Noise(r) * 0.45f * Mathf.Exp(-t * 3f) : 0f;
            float low = Osc(ref v.Phase, 62f + 25f * t) * swell * 0.22f;
            return roar * swell * 2.8f + crackle + low;
        }

        /// <summary>곡괭이: 돌을 찍는 낮은 '깡' + 돌 부스러기.</summary>
        static float Pick(float t, float len, ref SynthState v, System.Random r)
        {
            float ring = (Tone(t, 940f) + Tone(t, 1490f) * 0.55f) * Mathf.Exp(-t * 38f) * 0.42f;
            float body = Osc(ref v.Phase, 120f) * Mathf.Exp(-t * 40f) * 0.35f;
            float grit = OnePole(ref v.Lp, Noise(r), 0.35f) * Mathf.Exp(-t * 45f) * 0.8f;
            return ring + body + grit + Click(t, 0.006f, r) * 0.6f;
        }

        /// <summary>말뚝: 쇠 추가 걸리는 낮은 '쿵' + 녹슨 쇠사슬이 잦게 부딪치다 잦아든다.</summary>
        static float StakeOn(float t, float len, ref SynthState v, System.Random r)
        {
            float thump = Osc(ref v.Phase, Mathf.Lerp(80f, 48f, Mathf.Clamp01(t / 0.3f))) * Mathf.Exp(-t * 10f) * 0.65f;
            float density = t > 0.04f && t < 0.55f ? 0.0008f * (1f - (t - 0.04f) / 0.51f) + 0.00015f : 0f;
            if (r.NextDouble() < density)
            {
                v.EventB = v.EventA;
                v.FreqB = v.FreqA;
                v.EventA = t;
                v.FreqA = 1500f + 1400f * (float)r.NextDouble();
            }
            float n = Noise(r);
            float chain = Link(t, v.EventA, v.FreqA, n) + Link(t, v.EventB, v.FreqB, n) * 0.75f;
            return thump + chain * 0.3f;
        }

        /// <summary>사슬 고리 하나가 부딪친 소리(짧은 잡음 + 비조화 쇳소리).</summary>
        static float Link(float t, float start, float freq, float n)
        {
            if (start < 0f) return 0f;
            float dt = t - start;
            if (dt < 0f) return 0f;
            float tick = dt < 0.0015f ? n * 0.7f : 0f;
            return (Tone(dt, freq) + Tone(dt, freq * 1.47f) * 0.5f + tick) * Mathf.Exp(-dt * 55f);
        }

        /// <summary>레벨업(1.4초): 깊은 종(라2, 단3도 배음이 든 교회 종) + 0.1초 뒤 부풀어 오르는 가단조 화음.</summary>
        static float LevelUp(float t, float len, ref SynthState v, System.Random r)
        {
            float bell = Bell(t, 110f) * 0.32f;
            float strike = t < 0.012f ? OnePole(ref v.Lp, Noise(r), 0.3f) * (1f - t / 0.012f) * 0.5f : 0f;
            float pad = 0f;
            float pt = t - 0.1f;
            if (pt > 0f)
            {
                float env = Mathf.Clamp01(pt / 0.3f) * Mathf.Exp(-pt * 1.6f);
                pad = (Organ(t, 110f) + Organ(t, 130.81f) + Organ(t, 164.81f) + Organ(t, 220.6f) * 0.5f) * env * 0.075f;
            }
            return bell + strike + pad;
        }

        /// <summary>빛기둥: 낮은 종(솔3) 한 번. 전의 맑은 '띵'보다 어둡게.</summary>
        static float Loot(float t, float len, ref SynthState v, System.Random r)
        {
            float strike = t < 0.008f ? OnePole(ref v.Lp, Noise(r), 0.35f) * (1f - t / 0.008f) * 0.35f : 0f;
            return Bell(t, 196f) * Mathf.Exp(-t * 1.5f) * 0.32f + strike;
        }

        /// <summary>교회 종 배음(험 0.5, 프라임 1, 단3도 1.2, 5도 1.5, 노미널 2 …). 단3도 배음이 종 특유의 어두운 울림을 만든다.</summary>
        static float Bell(float t, float f)
        {
            float w = TwoPi * f * t;
            return 0.55f * Mathf.Sin(w * 0.5f) * Mathf.Exp(-t * 1.2f)
                 + 0.45f * Mathf.Sin(w) * Mathf.Exp(-t * 1.8f)
                 + 0.4f * Mathf.Sin(w * 1.2f) * Mathf.Exp(-t * 2.2f)
                 + 0.2f * Mathf.Sin(w * 1.5f) * Mathf.Exp(-t * 2.8f)
                 + 0.32f * Mathf.Sin(w * 2f) * Mathf.Exp(-t * 3.2f)
                 + 0.14f * Mathf.Sin(w * 2.51f) * Mathf.Exp(-t * 4.5f)
                 + 0.1f * Mathf.Sin(w * 3.01f) * Mathf.Exp(-t * 6f)
                 + 0.07f * Mathf.Sin(w * 4.07f) * Mathf.Exp(-t * 8f);
        }

        /// <summary>낮은 오르간 한 음(기음 + 2·3배음).</summary>
        static float Organ(float t, float f)
        {
            float w = TwoPi * f * t;
            return Mathf.Sin(w) + 0.45f * Mathf.Sin(2f * w) + 0.2f * Mathf.Sin(3f * w);
        }

        /// <summary>피 튀김 '철퍽': 아래로 미끄러지는 공명 잡음이 꿀렁이고 짧은 살 소리가 받친다. 갈래 2개(높이·꿀렁임이 다름).</summary>
        static float Blood(float t, float len, ref SynthState v, System.Random r)
        {
            float x = t / len;
            float shift = v.Variant == 0 ? 1f : 0.8f;
            float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t * 22f);
            float splat = BandPass(ref v.Low, ref v.Band, Noise(r) * env, Mathf.Lerp(1500f, 380f, Mathf.Sqrt(x)) * shift, 0.28f);
            float gurgle = 0.6f + 0.4f * Mathf.Sin(t * TwoPi * (v.Variant == 0 ? 47f : 61f));
            float smack = OnePole(ref v.Lp, Noise(r), 0.1f) * Mathf.Exp(-t * 50f) * 1.2f;
            return splat * gurgle * 1.3f + smack;
        }

        /// <summary>몸 쓰러짐: 무거운 낮은 쿵 + 살덩이 둔탁음, 0.13초 뒤 한 번 더 털썩, 흙먼지 스침.</summary>
        static float BodyFall(float t, float len, ref SynthState v, System.Random r)
        {
            float main = Osc(ref v.Phase, Mathf.Lerp(72f, 38f, Mathf.Clamp01(t / 0.25f))) * Mathf.Exp(-t * 9f);
            float mass = OnePole(ref v.Lp, Noise(r), 0.05f) * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 14f) * 2.4f;
            float settle = 0f;
            float t2 = t - 0.13f;
            if (t2 > 0f)
                settle = (Osc(ref v.Phase2, 58f) * 0.5f + OnePole(ref v.Lp2, Noise(r), 0.07f) * 2.2f) * Mathf.Exp(-t2 * 18f);
            float dust = OnePole(ref v.Lp3, Noise(r), 0.25f) * Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 6f) * 0.25f;
            return main * 0.7f + mass + settle * 0.6f + dust;
        }

        /// <summary>쓰러짐 경고(1.6초): 낮은 쿵 + 단2도로 부딪치는 낮은 두 음(맥놀이가 불길하게 떤다) + 땅울림.</summary>
        static float PlayerDeath(float t, float len, ref SynthState v, System.Random r)
        {
            float boom = Osc(ref v.Phase, Mathf.Lerp(70f, 34f, Mathf.Clamp01(t / 0.6f))) * Mathf.Exp(-t * 3.2f);
            float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t * 1.5f);
            float drone = (Tone(t, 55f) + Tone(t, 58.27f) + 0.5f * Tone(t, 110f) + 0.35f * Tone(t, 116.54f) + 0.25f * Tone(t, 164.8f)) * env;
            float rumble = OnePole(ref v.Lp, Noise(r), 0.02f) * Mathf.Exp(-t * 2f) * 2.5f;
            float strike = t < 0.01f ? Noise(r) * (1f - t / 0.01f) * 0.4f : 0f;
            return boom * 0.55f + drone * 0.14f + rumble + strike;
        }

        /// <summary>발소리: 무거운 뒤꿈치 쿵 + 흙 눌리는 소리 + 20ms 뒤 자갈 바스락. 갈래 3개(높이가 조금씩 다름).</summary>
        static float Footstep(float t, float len, ref SynthState v, System.Random r)
        {
            float tone = v.Variant == 0 ? 1f : v.Variant == 1 ? 0.88f : 1.12f;
            float heel = Osc(ref v.Phase, Mathf.Lerp(95f, 55f, t / len) * tone) * Mathf.Exp(-t * 38f);
            float soil = OnePole(ref v.Lp, Noise(r), 0.09f * tone) * Mathf.Clamp01(t / 0.002f) * Mathf.Exp(-t * 42f) * 1.8f;
            float grit = 0f;
            float t2 = t - 0.018f;
            if (t2 > 0f)
                grit = BandPass(ref v.Low, ref v.Band, Noise(r), 1900f * tone, 0.6f) * Mathf.Exp(-t2 * 55f) * 0.6f;
            return heel * 0.6f + soil + grit;
        }
    }
}
