using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 등 뒤 소리(기획/시야와-문-1차.md 4-6, 작성자 D). 부채꼴 밖 적은 보이지 않으므로(4-1) 깬 적의 자리를 소리로 알린다.
    /// 새 소리 파일 없이 DungeonAmbienceSynth의 긁기·시위 삐걱·숨소리를 음높이·크기만 바꿔 쓴다.
    /// ① 등 뒤 기척: 깬 적(제자리로 돌아가는 중 아님)이 안 보이고 9 안이며, 보는 칸 안이거나 벽에 막히지 않았으면(문틈으로 이어짐)
    ///    종류별 간격(SightRules.BehindGap)으로 소리를 낸다. 가까운 2마리까지. 새로 든 적은 0.15~0.35초 뒤 첫 소리.
    /// ② 깨는 기척: 안 보이는 적이 12 안에서 깨면 그 적 소리 한 번(1.3배). 무리가 한꺼번에 깨면 0.3초에 하나(나머지는 묻음).
    ///    '1·2층 탐험 맛'의 순찰이 등 뒤에서 나를 알아챌 때도 이 소리다(Enemy.Wake → CombatEvents.EnemyWoke).
    /// ③ 예고 기척: 안 보이는 적이 14 안에서 예고를 시작하면(Telegraph.Started) 그 적 소리 한 번(1.2배). 예고 시작 소리(Q6)는 따로 늘 난다(4-5).
    /// 소리: 굴쥐 긁기, 돌충이(코드 이름 Boar) 긁기를 낮게(음높이 0.65 근처), 궁수 시위 삐걱, 정예 숨소리. 둥지·보스·허수아비는 없음.
    /// 돌충이 소리는 사용자 소리가 들어오면 LurkSounds·예고 시작 소리와 함께 바꾼다(넘길 일 H4).
    /// 거리 음량·좌우는 LurkSounds와 같은 방식(끝 0.15, 거리 비율 제곱, 9유닛에 ±0.8). 보는 칸 밖이거나 벽 너머면 작고 먹먹하게
    /// (음량 × 0.6, 900Hz 아래만. 소리를 시작할 때 한 번 정한다 — 도중에 필터를 바꾸면 툭 끊겨 들림).
    /// 시야를 끄면 적이 모두 보이므로 소리도 나지 않는다. 시간은 게임 시간이라 멈춤 동안 새 소리를 내지 않는다.
    /// 소리 끔(Tuning.Sound)·F1 '등 뒤 기척 소리'(Enabled)를 따르고, 플레이어가 쓰러지면 쉰다.
    /// 던전에만 붙는다(VisionSystem.Awake가 붙임, 전투 시험장에는 없음). 자기 난수를 써서 전역 Random 차례를 건드리지 않는다. 매 프레임 할당이 없다.
    /// </summary>
    public sealed class BehindSounds : MonoBehaviour
    {
        /// <summary>던전 F1 패널 '등 뒤 기척 소리'(시야와 문 1차 13장, 넘길 일 H1). 끄면 울리던 소리도 바로 멈춘다.</summary>
        public static bool Enabled = true;

        public static BehindSounds Instance { get; private set; }

        /// <summary>도메인 다시 불러오기 꺼짐 대비: 플레이 시작 때 손잡이를 켬으로 되돌린다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            Enabled = true;
            Instance = null;
        }

        /// <summary>등 뒤 소리 갈래. 정예는 종류와 관계없이 숨소리.</summary>
        enum Voice
        {
            None = -1,
            /// <summary>굴쥐 긁기.</summary>
            Scratch,
            /// <summary>돌충이: 긁기를 낮게(SightRules.BeetlePitch).</summary>
            Beetle,
            /// <summary>궁수 시위 삐걱.</summary>
            Creak,
            /// <summary>정예 숨소리.</summary>
            Breath,
        }

        /// <summary>좌우: 플레이어보다 이만큼 옆이면 끝까지(±0.8). LurkSounds와 같은 값.</summary>
        const float PanSpan = 9f;
        const float MaxPan = 0.8f;
        /// <summary>거리 끝에서 남는 음량 비율(거리 비율 제곱으로 줄어듦). LurkSounds와 같은 값.</summary>
        const float EdgeGain = 0.15f;
        /// <summary>소리 줄기 우선순위(낮을수록 먼저). 쉬는 무리 기척(150)보다 조금 앞: 지금 덤비는 적의 소리다.</summary>
        const int VoicePriority = 140;
        /// <summary>적마다 다음 소리 시각을 기억하는 수가 이보다 많아지면 오래되거나 지워진 적을 비운다.</summary>
        const int PruneAbove = 64;
        /// <summary>다음 소리 시각이 이만큼(초) 지난 적은 비운다.</summary>
        const float PruneAge = 10f;

        struct Slot
        {
            public AudioSource Src;
            public AudioLowPassFilter Filter;
            public Enemy Source;
            public Vector2 Pos;
            public float Level;
            public float Range;
            public float Started;
            public bool Muffled;
        }

        // ── 시험·eval 확인용 ──
        /// <summary>지금까지 낸 소리 수(등 뒤 기척 + 깨는 기척 + 예고 기척).</summary>
        public int CuesPlayed { get; private set; }
        /// <summary>그중 깨는 기척 수.</summary>
        public int WakeCues { get; private set; }
        /// <summary>그중 예고 기척 수.</summary>
        public int TelegraphCues { get; private set; }
        /// <summary>지금 울리는 소리 수(0~2).</summary>
        public int PlayingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].Src && _slots[i].Src.isPlaying) n++;
                return n;
            }
        }

        readonly Slot[] _slots = new Slot[SightRules.BehindVoices];
        /// <summary>지난 훑기에서 고른 등 뒤 기척 적(가까운 순, 빈자리는 null).</summary>
        readonly Enemy[] _active = new Enemy[SightRules.BehindVoices];
        /// <summary>훑는 동안 고르는 가까운 적과 거리 제곱(가까운 순).</summary>
        readonly Enemy[] _pick = new Enemy[SightRules.BehindVoices];
        readonly float[] _pickSq = new float[SightRules.BehindVoices];
        int _pickCount;
        /// <summary>적마다 다음 등 뒤 기척 시각(게임 시간). 고른 둘에서 빠졌다 다시 들어와도 간격을 지킨다.</summary>
        readonly Dictionary<Enemy, float> _nextAt = new Dictionary<Enemy, float>(64, EnemyRef.Comparer);
        readonly List<Enemy> _pruneKeys = new List<Enemy>(64);
        AudioClip[] _scratch;
        AudioClip _creak, _breath;
        float _scanAt;
        float _lastWakeCue = -99f;
        Enemy _lastTelegraphOwner;
        float _lastTelegraphCue = -99f;
        System.Random _rng;

        void Awake()
        {
            Instance = this;
            _rng = new System.Random(unchecked((int)(System.DateTime.Now.Ticks & 0x7fffffff)));
            var holder = new GameObject("BehindSounds");
            holder.transform.SetParent(transform, false);
            for (int i = 0; i < _slots.Length; i++)
            {
                var go = new GameObject("Behind voice " + i);
                go.transform.SetParent(holder.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.priority = VoicePriority;
                src.volume = 0f;
                // 벽 너머·보는 칸 밖: 900Hz 아래만(소리 줄기마다 저역 통과 필터 하나, 먹먹할 때만 켬).
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = LurkSounds.WallCutoff;
                lp.lowpassResonanceQ = 1f;
                lp.enabled = false;
                _slots[i] = new Slot { Src = src, Filter = lp, Started = -99f };
            }

            _scratch = new AudioClip[DungeonAmbienceSynth.ScratchVariants];
            for (int i = 0; i < _scratch.Length; i++) _scratch[i] = DungeonAmbienceSynth.BuildScratch(i);
            _creak = DungeonAmbienceSynth.BuildBowCreak();
            _breath = DungeonAmbienceSynth.BuildBeastBreath();

            // 루트가 SceneStatics.Reset으로 구독을 비운 뒤(DungeonRoot.Awake 첫 줄) VisionSystem과 함께 붙으므로 여기서 구독한다.
            CombatEvents.EnemyWoke += OnEnemyWoke;
            Telegraph.Started += OnTelegraphStarted;
            DungeonEvents.ExpeditionRestarted += OnExpeditionRestarted;
        }

        void OnDestroy()
        {
            CombatEvents.EnemyWoke -= OnEnemyWoke;
            Telegraph.Started -= OnTelegraphStarted;
            DungeonEvents.ExpeditionRestarted -= OnExpeditionRestarted;
            if (Instance == this) Instance = null;
            if (_scratch != null)
                for (int i = 0; i < _scratch.Length; i++) DestroyClip(_scratch[i]);
            DestroyClip(_creak);
            DestroyClip(_breath);
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!Tuning.Sound || !Enabled) StopSlots();
            if (!p) return;
            Vector2 ear = p.Position;
            UpdateMix(ear);

            var vision = VisionSystem.Instance;
            if (Quiet(p, vision))
            {
                ClearActive();
                return;
            }
            // 게임 시간: 멈춤(시간 0) 동안 훑기·소리 시계가 함께 멈춘다.
            float now = Time.time;
            if (now >= _scanAt)
            {
                _scanAt = now + SightRules.BehindScanSeconds;
                Scan(ear, vision, now);
            }
            for (int k = 0; k < _active.Length; k++)
            {
                var e = _active[k];
                if (!e || !_nextAt.TryGetValue(e, out float at) || now < at) continue;
                // 훑기 사이에 보이게 됐거나 쓰러졌으면 내지 않는다(다음 훑기가 뺀다).
                if (!Lurking(e)) continue;
                Speak(e, ear, vision, now);
            }
        }

        /// <summary>새 소리를 내지 않는 때: 손잡이·소리 끔, 플레이어 쓰러짐, 장면 시작 전·끝난 뒤, 시야 꺼짐(적이 모두 보임).</summary>
        static bool Quiet(PlayerController p, VisionSystem vision)
        {
            var root = DungeonRoot.Instance;
            return !Enabled || !Tuning.Sound || !p || p.IsDown || (root && !root.Playing) || !vision || !vision.VisionOn;
        }

        // ───────────────────────── 등 뒤 기척 ─────────────────────────

        /// <summary>등 뒤 기척을 낼 수 있는 상태: 살아 깨어 있고(돌아가는 중 아님) 지금 안 보이며 소리 갈래가 있다.</summary>
        static bool Lurking(Enemy e) =>
            e && !e.Dead && e.isActiveAndEnabled && e.Aware && !e.IsReturning && !e.VisionInSight && VoiceOf(e) != Voice.None;

        /// <summary>보는 칸 안이거나, 벽에 막히지 않고 이어져 있는가(문틈 너머). 소리가 길을 따라 들린다(4-6).</summary>
        static bool Reachable(Vector2 pos, Vector2 ear, VisionSystem vision) =>
            vision.InViewCell(pos) || !VisionSystem.WallBetween(ear, pos);

        /// <summary>9 안의 등 뒤 적 가운데 가까운 둘을 고른다(벽 가림은 거리 안 후보만 본다, 8-1). 새로 든 적은 첫 소리를 조금 뒤에 낸다.</summary>
        void Scan(Vector2 ear, VisionSystem vision, float now)
        {
            _pickCount = 0;
            float rr = SightRules.BehindRange * SightRules.BehindRange;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!Lurking(e)) continue;
                float sq = (e.Position - ear).sqrMagnitude;
                if (sq > rr) continue;
                // 이미 더 가까운 둘이 있으면 벽 가림(광선)을 보지 않고 넘긴다.
                if (_pickCount == _pick.Length && sq >= _pickSq[_pickCount - 1]) continue;
                if (!Reachable(e.Position, ear, vision)) continue;
                Consider(e, sq);
            }
            for (int k = 0; k < _pickCount; k++)
                if (!WasActive(_pick[k])) Arm(_pick[k], now);
            for (int k = 0; k < _active.Length; k++)
            {
                _active[k] = k < _pickCount ? _pick[k] : null;
                _pick[k] = null;
            }
            Prune(now);
        }

        /// <summary>가까운 순 목록(길이 BehindVoices)에 넣는다. 꽉 찼으면 가장 먼 것을 밀어낸다.</summary>
        void Consider(Enemy e, float sq)
        {
            int n = _pickCount;
            int i = n < _pick.Length ? n : n - 1;
            while (i > 0 && _pickSq[i - 1] > sq)
            {
                _pick[i] = _pick[i - 1];
                _pickSq[i] = _pickSq[i - 1];
                i--;
            }
            _pick[i] = e;
            _pickSq[i] = sq;
            if (n < _pick.Length) _pickCount = n + 1;
        }

        bool WasActive(Enemy e)
        {
            for (int k = 0; k < _active.Length; k++)
                if (ReferenceEquals(_active[k], e)) return true;
            return false;
        }

        /// <summary>가까운 둘에 새로 든 적: 남은 시계가 너무 이르면 첫 소리를 BehindFirstMin~Max 뒤로 미룬다(드나들어도 간격을 지킴).</summary>
        void Arm(Enemy e, float now)
        {
            float first = now + Range(SightRules.BehindFirstMin, SightRules.BehindFirstMax);
            if (!_nextAt.TryGetValue(e, out float at) || at < first) _nextAt[e] = first;
        }

        /// <summary>등 뒤 기척 한 번: 다음 소리는 종류별 간격 뒤.</summary>
        void Speak(Enemy e, Vector2 ear, VisionSystem vision, float now)
        {
            SightRules.BehindGap(e.Kind, e.IsElite, out float min, out float max);
            _nextAt[e] = now + Range(min, max);
            Play(e, VoiceOf(e), 1f, SightRules.BehindRange, ear, vision);
        }

        /// <summary>깨는 기척·예고 기척을 낸 적의 등 뒤 기척은 한 간격 뒤로(곧바로 겹쳐 나지 않게).</summary>
        void Delay(Enemy e, float now)
        {
            SightRules.BehindGap(e.Kind, e.IsElite, out float min, out float max);
            float next = now + Range(min, max);
            if (!_nextAt.TryGetValue(e, out float at) || at < next) _nextAt[e] = next;
        }

        void ClearActive()
        {
            for (int k = 0; k < _active.Length; k++) _active[k] = null;
        }

        /// <summary>기억한 적이 많아지면 지워진 적과 시계가 오래 지난 적을 비운다(할당 없이 재사용 목록으로).</summary>
        void Prune(float now)
        {
            if (_nextAt.Count <= PruneAbove) return;
            _pruneKeys.Clear();
            foreach (var kv in _nextAt)
                if (!kv.Key || now - kv.Value > PruneAge) _pruneKeys.Add(kv.Key);
            for (int i = 0; i < _pruneKeys.Count; i++) _nextAt.Remove(_pruneKeys[i]);
            _pruneKeys.Clear();
        }

        // ───────────────────────── 깨는 기척·예고 기척 ─────────────────────────

        /// <summary>깨는 기척(4-6): 안 보이는 적이 12 안에서 깼다. 0.3초에 하나(무리가 한꺼번에 깨면 나머지는 묻음).</summary>
        void OnEnemyWoke(Enemy e)
        {
            var p = PlayerController.Instance;
            var vision = VisionSystem.Instance;
            if (!e || e.Dead || e.VisionInSight || TimeScaleService.Paused || Quiet(p, vision)) return;
            var voice = VoiceOf(e);
            if (voice == Voice.None) return;
            Vector2 ear = p.Position;
            if ((e.Position - ear).sqrMagnitude > SightRules.WakeCueRange * SightRules.WakeCueRange) return;
            float now = Time.time;
            if (now - _lastWakeCue < SightRules.WakeCueSpacing) return;
            if (!Play(e, voice, SightRules.WakeCueLevel, SightRules.WakeCueRange, ear, vision)) return;
            _lastWakeCue = now;
            WakeCues++;
            Delay(e, now);
        }

        /// <summary>
        /// 예고 기척(4-5·4-6): 주인 적이 안 보이는 예고가 14 안에서 시작됐다. 보스 예고는 늘 그려지므로 내지 않는다.
        /// 궁수 세 갈래처럼 한 적이 한 번에 여러 예고를 만들면 한 번만(0.3초 안 같은 주인은 묻음).
        /// </summary>
        void OnTelegraphStarted(Telegraph t)
        {
            var owner = t ? t.Owner : null;
            var p = PlayerController.Instance;
            var vision = VisionSystem.Instance;
            if (!owner || owner.IsBoss || owner.Dead || owner.VisionInSight || TimeScaleService.Paused || Quiet(p, vision)) return;
            var voice = VoiceOf(owner);
            if (voice == Voice.None) return;
            Vector2 ear = p.Position;
            if ((owner.Position - ear).sqrMagnitude > SightRules.TelegraphCueRange * SightRules.TelegraphCueRange) return;
            float now = Time.time;
            if (ReferenceEquals(owner, _lastTelegraphOwner) && now - _lastTelegraphCue < SightRules.WakeCueSpacing) return;
            if (!Play(owner, voice, SightRules.TelegraphCueLevel, SightRules.TelegraphCueRange, ear, vision)) return;
            _lastTelegraphOwner = owner;
            _lastTelegraphCue = now;
            TelegraphCues++;
            Delay(owner, now);
        }

        void OnExpeditionRestarted()
        {
            _nextAt.Clear();
            ClearActive();
            _lastTelegraphOwner = null;
            StopSlots();
        }

        // ───────────────────────── 목소리 ─────────────────────────

        /// <summary>적의 등 뒤 소리 갈래. 정예는 숨소리, 둥지·보스(오우거)·허수아비는 없음.</summary>
        static Voice VoiceOf(Enemy e)
        {
            if (!e || e.IsDummy || e.IsBoss) return Voice.None;
            if (e.Kind == MonsterKind.Nest || e.Kind == MonsterKind.Ogre) return Voice.None;
            if (e.IsElite) return Voice.Breath;
            switch (e.Kind)
            {
                case MonsterKind.Rat: return Voice.Scratch;
                case MonsterKind.Boar: return Voice.Beetle;
                case MonsterKind.Archer: return Voice.Creak;
                default: return Voice.None;
            }
        }

        /// <summary>갈래별 기본 음량(효과음 음량에 곱함). LurkSounds 기척과 같은 크기(돌충이는 콧김 자리 값).</summary>
        static float LevelOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch: return 0.3f;
                case Voice.Beetle: return 0.34f;
                case Voice.Creak: return 0.26f;
                case Voice.Breath: return 0.32f;
                default: return 0f;
            }
        }

        /// <summary>같은 소리 반복 티를 줄이는 음높이 폭. 돌충이는 긁기를 0.65배 근처로 낮춘다(4-6).</summary>
        float PitchOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch: return Range(0.9f, 1.15f);
                case Voice.Beetle: return SightRules.BeetlePitch * Range(0.92f, 1.08f);
                case Voice.Creak: return Range(0.92f, 1.08f);
                case Voice.Breath: return Range(0.88f, 1f);
                default: return 1f;
            }
        }

        AudioClip ClipOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch:
                case Voice.Beetle:
                    return _scratch != null && _scratch.Length > 0 ? _scratch[_rng.Next(_scratch.Length)] : null;
                case Voice.Creak: return _creak;
                case Voice.Breath: return _breath;
                default: return null;
            }
        }

        // ───────────────────────── 소리 줄기 ─────────────────────────

        /// <summary>
        /// 쉬는 줄기를 먼저, 다 바쁘면 가장 먼저 시작한 줄기를 끊고 낸다. 보는 칸 밖이거나 벽 너머면 먹먹하게(시작할 때 한 번 정함).
        /// 소리는 낸 적을 따라 움직인다(UpdateMix).
        /// </summary>
        /// <returns>소리를 냈는가.</returns>
        bool Play(Enemy source, Voice voice, float levelScale, float range, Vector2 ear, VisionSystem vision)
        {
            var clip = ClipOf(voice);
            if (!clip || !source || !Tuning.Sound) return false;
            int best = 0;
            float oldest = float.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Src.isPlaying)
                {
                    best = i;
                    break;
                }
                if (_slots[i].Started < oldest)
                {
                    oldest = _slots[i].Started;
                    best = i;
                }
            }
            ref var s = ref _slots[best];
            Vector2 pos = source.Position;
            s.Source = source;
            s.Pos = pos;
            s.Level = LevelOf(voice) * levelScale;
            s.Range = Mathf.Max(0.1f, range);
            s.Started = Time.unscaledTime;
            s.Muffled = (vision && !vision.InViewCell(pos)) || VisionSystem.WallBetween(ear, pos);
            s.Filter.enabled = s.Muffled;
            s.Src.Stop();
            s.Src.clip = clip;
            s.Src.pitch = PitchOf(voice);
            Mix(ref s, ear);
            s.Src.Play();
            CuesPlayed++;
            return true;
        }

        /// <summary>울리는 줄기의 자리(소리 낸 적을 따라감)·거리 음량·좌우를 매 프레임 맞춘다.</summary>
        void UpdateMix(Vector2 ear)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Src || !s.Src.isPlaying)
                {
                    s.Source = null;
                    continue;
                }
                if (s.Source) s.Pos = s.Source.Position;
                Mix(ref s, ear);
            }
        }

        /// <summary>LurkSounds.Mix와 같은 방식: 거리 비율 제곱으로 줄어 끝에서 0.15, 먹먹하면 × 0.6, 9유닛 옆이면 좌우 끝(±0.8).</summary>
        static void Mix(ref Slot s, Vector2 ear)
        {
            float d = (s.Pos - ear).magnitude;
            float k = Mathf.Clamp01(d / s.Range);
            float g = EdgeGain + (1f - EdgeGain) * (1f - k) * (1f - k);
            if (s.Muffled) g *= LurkSounds.WallVolume;
            s.Src.volume = Mathf.Clamp01(s.Level * g * Tuning.SoundVolume);
            s.Src.panStereo = Mathf.Clamp((s.Pos.x - ear.x) / PanSpan, -MaxPan, MaxPan);
        }

        void StopSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Src || !s.Src.isPlaying) continue;
                s.Src.Stop();
                s.Source = null;
            }
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        static void DestroyClip(AudioClip clip)
        {
            if (clip) Destroy(clip);
        }
    }
}
