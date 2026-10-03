using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 배경음·먼 소리·동굴 잔향·발소리(기획/다크판타지-분위기-1차.md '소리', 작업 계약 B). 던전 루트에만 붙는다.
    /// - 배경음: 35~60Hz 울림 + 걸러진 바람이 느리게 출렁이는 12초 반복 클립(이음매 없음). 잔향은 건너뛴다(저음이 뭉개지지 않게).
    /// - 먼 소리: 15~40초마다 물방울(가끔 2~4번 이어짐)·쇠사슬·삐걱임·짐승 그르렁·돌 무너짐 중 하나를 작게, 좌우 무작위로.
    /// - 동굴 잔향: 듣는 이(카메라의 AudioListener)에 잔향 필터를 달아 Sfx 효과음과 먼 소리까지 울린다.
    ///   던전이 닫히면 떼므로 전투 시험장(손맛 비교 기준)에는 잔향이 없다.
    /// - 발소리: 걸은 거리로 간격을 정한다(보폭 고정). 그래서 탐험 걸음(6.5)은 장비 속도(약 5.3)보다 발이 잦다.
    /// 모든 소리는 Tuning.Sound·SoundVolume을 따른다. 매 프레임 할당이 없다.
    /// </summary>
    public sealed class DungeonAudio : MonoBehaviour
    {
        /// <summary>배경음 반복 길이(초).</summary>
        const float LoopSeconds = 12f;
        /// <summary>배경음 기본 음량(효과음 음량 설정에 곱한다).</summary>
        const float DroneLevel = 0.42f;
        const float DroneFadeIn = 4f;
        /// <summary>쓰러져 있는 동안 배경음을 이만큼으로 낮춘다(쓰러짐 경고음이 묻히지 않게).</summary>
        const float DownDuck = 0.35f;
        /// <summary>첫 먼 소리는 조금 일찍(시험하는 사람이 듣게), 그 뒤로는 15~40초마다.</summary>
        const float FirstDistantMin = 6f;
        const float FirstDistantMax = 14f;
        const float DistantGapMin = 15f;
        const float DistantGapMax = 40f;
        const float DistantPan = 0.85f;
        const int DistantVoices = 2;
        /// <summary>
        /// 한 걸음 거리(유닛). 걸은 거리로 세므로 이동 속도 배율(Tuning.MoveSpeedScale)을 따라 저절로 느려진다.
        /// 기본 배율 0.86: 장비 걸음 약 4.56이면 약 0.42초, 탐험 걸음 약 5.59면 약 0.34초 간격(배율 1이면 0.36초 / 0.29초).
        /// </summary>
        const float StrideLength = 1.9f;
        /// <summary>멈췄다 다시 걸을 때 첫 발까지 남기는 거리 비율(바로 '쿵'이 나도록 짧게).</summary>
        const float FirstStepFraction = 0.45f;
        /// <summary>한 프레임에 이보다 멀리 움직이면 순간 이동(말뚝 이동·다시 섬)으로 보고 발소리를 내지 않는다.</summary>
        const float TeleportJump = 1.5f;

        public static DungeonAudio Instance { get; private set; }

        /// <summary>도메인 다시 불러오기 꺼짐 대비(DungeonRoot.ResetStatics에서 부른다).</summary>
        public static void ResetStatics() => Instance = null;

        /// <summary>지금 동굴 잔향이 켜져 있는가.</summary>
        public bool ReverbOn => _reverb && _reverb.enabled;

        PlayerController _player;
        AudioSource _drone;
        readonly AudioSource[] _distant = new AudioSource[DistantVoices];
        int _nextDistant;
        AudioClip _droneClip;
        AudioClip[] _dripClips;
        AudioClip _chainClip, _creakClip, _growlClip, _rumbleClip;
        AudioReverbFilter _reverb;
        bool _ownsReverb;

        float _droneGain;
        float _duck = 1f;
        float _duckTarget = 1f;
        float _nextDistantAt = -1f;
        int _dripsLeft;
        float _dripAt;
        float _dripPan;

        Vector2 _lastPos;
        bool _tracking;
        float _strideLeft;
        bool _leftFoot;

        void Awake()
        {
            Instance = this;

            var holder = new GameObject("DungeonAmbience");
            holder.transform.SetParent(transform, false);
            _drone = holder.AddComponent<AudioSource>();
            _drone.playOnAwake = false;
            _drone.loop = true;
            _drone.spatialBlend = 0f;
            _drone.priority = 32;
            _drone.volume = 0f;
            // 듣는 이에 단 동굴 잔향을 건너뛴다: 35~60Hz 울림에 잔향이 걸리면 뭉개진다.
            _drone.bypassListenerEffects = true;
            for (int i = 0; i < DistantVoices; i++)
            {
                var src = holder.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.priority = 200;
                _distant[i] = src;
            }

            _droneClip = DungeonAmbienceSynth.BuildDrone(LoopSeconds);
            _dripClips = new AudioClip[DungeonAmbienceSynth.DripVariants];
            for (int i = 0; i < _dripClips.Length; i++) _dripClips[i] = DungeonAmbienceSynth.BuildDrip(i);
            _chainClip = DungeonAmbienceSynth.BuildChain();
            _creakClip = DungeonAmbienceSynth.BuildCreak();
            _growlClip = DungeonAmbienceSynth.BuildGrowl();
            _rumbleClip = DungeonAmbienceSynth.BuildRumble();

            CombatEvents.PlayerDowned += OnPlayerDowned;
            DungeonEvents.PlayerRespawned += OnPlayerRespawned;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            DungeonEvents.PlayerRespawned -= OnPlayerRespawned;
            if (_reverb)
            {
                if (_ownsReverb) Destroy(_reverb);
                else _reverb.enabled = false;
            }
            DestroyClip(_droneClip);
            if (_dripClips != null)
                for (int i = 0; i < _dripClips.Length; i++) DestroyClip(_dripClips[i]);
            DestroyClip(_chainClip);
            DestroyClip(_creakClip);
            DestroyClip(_growlClip);
            DestroyClip(_rumbleClip);
        }

        /// <summary>플레이어를 만든 뒤 DungeonRoot가 부른다. 잔향을 켜고 배경음을 틀고 발소리를 잇는다.</summary>
        public void Init(PlayerController player)
        {
            _player = player;
            _tracking = false;
            SetReverb(true);
            if (_droneClip && !_drone.isPlaying)
            {
                _drone.clip = _droneClip;
                // 매번 같은 자리에서 시작하지 않게 흩는다.
                _drone.timeSamples = Random.Range(0, _droneClip.samples);
                _drone.Play();
            }
            _nextDistantAt = Time.unscaledTime + Random.Range(FirstDistantMin, FirstDistantMax);
        }

        /// <summary>
        /// 동굴 잔향을 켜고 끈다. 듣는 이(카메라의 AudioListener)에 AudioReverbFilter를 달아 모든 효과음에 걸리게 한다.
        /// 배경음만 bypassListenerEffects로 건너뛴다. 이 컴포넌트가 사라지면 단 필터도 뗀다(던전에서만).
        /// </summary>
        public void SetReverb(bool on)
        {
            if (on && !_reverb)
            {
                var listener = FindListener();
                if (!listener) return;
                _reverb = listener.GetComponent<AudioReverbFilter>();
                _ownsReverb = !_reverb;
                if (!_reverb) _reverb = listener.gameObject.AddComponent<AudioReverbFilter>();
                ApplyCaveReverb(_reverb);
            }
            if (_reverb) _reverb.enabled = on;
        }

        static AudioListener FindListener()
        {
            var cam = Camera.main;
            var listener = cam ? cam.GetComponent<AudioListener>() : null;
            if (listener && listener.enabled) return listener;
            return FindAnyObjectByType<AudioListener>();
        }

        /// <summary>
        /// 갱도 잔향: 원음은 그대로(첫 딸깍 유지), 젖은 소리는 −13dB 안팎·2.3초로 길되 높은 소리는 빨리 죽여 어둡게 울린다.
        /// 값 단위는 Unity AudioReverbFilter(밀리벨·초).
        /// </summary>
        static void ApplyCaveReverb(AudioReverbFilter f)
        {
            f.reverbPreset = AudioReverbPreset.User;
            f.dryLevel = 0f;
            f.room = -1300f;
            f.roomHF = -1100f;
            f.roomLF = 0f;
            f.decayTime = 2.3f;
            f.decayHFRatio = 0.5f;
            f.reflectionsLevel = -800f;
            f.reflectionsDelay = 0.02f;
            f.reverbLevel = -450f;
            f.reverbDelay = 0.035f;
            f.hfReference = 5000f;
            f.lfReference = 250f;
            f.diffusion = 85f;
            f.density = 100f;
        }

        void Update()
        {
            if (!_player) _player = PlayerController.Instance;
            float dt = Time.unscaledDeltaTime;
            UpdateDrone(dt);
            UpdateDistant();
        }

        /// <summary>발소리는 이번 프레임 이동이 끝난 뒤 위치로 잰다.</summary>
        void LateUpdate() => UpdateFootsteps();

        void UpdateDrone(float dt)
        {
            if (!_drone) return;
            _droneGain = Mathf.MoveTowards(_droneGain, 1f, dt / DroneFadeIn);
            // 다시 섬 사건을 놓쳐도 일어서 있으면 배경음을 되돌린다.
            if (_duckTarget < 1f && _player && !_player.IsDown) _duckTarget = 1f;
            // 낮출 때는 빠르게, 되돌릴 때는 천천히(다시 섰을 때 어둠이 서서히 돌아오게).
            float speed = _duckTarget < _duck ? 3f : 0.35f;
            _duck = Mathf.MoveTowards(_duck, _duckTarget, dt * speed);
            float master = Tuning.Sound ? Tuning.SoundVolume : 0f;
            _drone.volume = DroneLevel * master * _droneGain * _duck;
        }

        void UpdateDistant()
        {
            if (_nextDistantAt < 0f) return;
            float now = Time.unscaledTime;

            // 이어지는 물방울.
            if (_dripsLeft > 0 && now >= _dripAt)
            {
                _dripsLeft--;
                PlayDistant(RandomDrip(), _dripPan + Random.Range(-0.08f, 0.08f), Random.Range(0.16f, 0.24f), Random.Range(0.92f, 1.1f));
                _dripAt = now + Random.Range(0.45f, 1.3f);
            }

            if (now < _nextDistantAt) return;
            _nextDistantAt = now + Random.Range(DistantGapMin, DistantGapMax);
            float pan = Random.Range(-DistantPan, DistantPan);
            float roll = Random.value;
            if (roll < 0.36f)
            {
                PlayDistant(RandomDrip(), pan, Random.Range(0.18f, 0.26f), Random.Range(0.92f, 1.1f));
                _dripPan = pan;
                _dripsLeft = Random.Range(1, 4);
                _dripAt = now + Random.Range(0.5f, 1.2f);
            }
            else if (roll < 0.56f) PlayDistant(_chainClip, pan, Random.Range(0.16f, 0.24f), Random.Range(0.9f, 1.08f));
            else if (roll < 0.74f) PlayDistant(_creakClip, pan, Random.Range(0.18f, 0.26f), Random.Range(0.88f, 1.06f));
            else if (roll < 0.9f) PlayDistant(_growlClip, pan, Random.Range(0.16f, 0.22f), Random.Range(0.85f, 1.03f));
            else PlayDistant(_rumbleClip, pan, Random.Range(0.22f, 0.3f), Random.Range(0.9f, 1.05f));
        }

        AudioClip RandomDrip() =>
            _dripClips != null && _dripClips.Length > 0 ? _dripClips[Random.Range(0, _dripClips.Length)] : null;

        void PlayDistant(AudioClip clip, float pan, float level, float pitch)
        {
            if (!clip || !Tuning.Sound) return;
            var src = _distant[_nextDistant];
            _nextDistant = (_nextDistant + 1) % DistantVoices;
            if (!src) return;
            src.clip = clip;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.pitch = pitch;
            src.volume = Mathf.Clamp01(level * Tuning.SoundVolume);
            src.Play();
        }

        /// <summary>
        /// 걸은 거리가 한 보폭을 넘을 때마다 Sfx 발소리를 낸다. 걷기(이동 자세·맞고 밀리는 중)일 때만 세고,
        /// 구르기·공격·회오리·쓰러짐은 자기 소리가 있으므로 뺀다. 왼발·오른발 음높이를 조금 다르게 한다.
        /// </summary>
        void UpdateFootsteps()
        {
            var p = _player;
            if (!p)
            {
                _tracking = false;
                return;
            }
            Vector2 pos = p.Position;
            if (!_tracking)
            {
                _lastPos = pos;
                _tracking = true;
                _strideLeft = StrideLength * FirstStepFraction;
                return;
            }
            float moved = (pos - _lastPos).magnitude;
            _lastPos = pos;
            if (moved > TeleportJump)
            {
                _strideLeft = StrideLength * FirstStepFraction;
                return;
            }

            var pose = p.Pose;
            bool walking = !p.IsDown && p.IsMoving && (pose == PlayerPose.Move || pose == PlayerPose.Hurt);
            if (!walking)
            {
                if (_strideLeft > StrideLength * FirstStepFraction) _strideLeft = StrideLength * FirstStepFraction;
                return;
            }

            _strideLeft -= moved;
            if (_strideLeft > 0f) return;
            _strideLeft += StrideLength;
            // 한 프레임이 길어도 발소리는 한 번만.
            if (_strideLeft < StrideLength * 0.5f) _strideLeft = StrideLength * 0.5f;
            _leftFoot = !_leftFoot;
            Sfx.PlayScaled(SfxKind.Footstep, Random.Range(0.85f, 1f), _leftFoot ? 0.97f : 1.03f);
        }

        void OnPlayerDowned() => _duckTarget = DownDuck;

        void OnPlayerRespawned() => _duckTarget = 1f;

        static void DestroyClip(AudioClip clip)
        {
            if (clip) Destroy(clip);
        }
    }
}
