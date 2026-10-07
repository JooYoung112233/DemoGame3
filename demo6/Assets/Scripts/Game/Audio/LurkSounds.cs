using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 잠든 무리 기척(기획/전투-보스-무기-다듬기-1차.md 4장·6-1 묶음 4, 7장 #2·#18): 잠든·먹는 무리가 종류별 거리·간격으로 작은 소리를 낸다
    /// (굴쥐 긁기, 멧돼지 콧김, 궁수 시위 삐걱, 정예 숨소리). 벽 너머(Linecast WallMask)면 음량 × 0.6에 900Hz 아래만 들린다(저역 통과 필터).
    /// 가까운 2무리까지만 낸다(정예 숨소리도 그 무리의 몫). 소리는 제자리에서 나며 플레이어 쪽 거리·좌우로 섞는다.
    /// 문턱 기척: 잠든 무리가 있는 칸의 문에 다가가면 문 하나에 원정당 1번 조금 크게 소리를 내고 문틀에 흙먼지를 떨군다
    /// (DungeonEvents.RaiseNoise 반경 0: DungeonAir가 흙먼지만 떨구고, 반경 0이라 CellEncounters는 아무도 깨우지 않는다).
    /// 겁먹은 굴쥐 찍찍(CombatEvents.EnemyFrightened): 무리 공포의 소리 몫이라 '기척' 토글과 관계없이 난다.
    /// 웅크림과는 무관하다(들리는 쪽의 소리라 내 걸음과 상관없음). 던전에만 붙는다(DungeonRoot, 전투 시험장에는 안 붙임). 합성은 DungeonAmbienceSynth.
    /// 시간은 게임 시간이라 멈춤 동안 새 소리를 내지 않는다. 매 프레임 할당이 없다(무리 시계 사전은 처음 본 무리만 더함). 전역 Random 순서를 건드리지 않게 자기 난수를 쓴다.
    /// </summary>
    public sealed class LurkSounds : MonoBehaviour
    {
        /// <summary>던전 F1 패널 '기척 끔/켬'(판정 ④). 꾸러미 ①이 ExplorationLog에 토글을 둔다. 끄면 울리던 기척도 바로 멈춘다.</summary>
        public static bool Enabled = true;

        public static LurkSounds Instance { get; private set; }

        /// <summary>도메인 다시 불러오기 꺼짐 대비: 플레이 시작 때 토글을 켬으로 되돌린다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            Enabled = true;
            Instance = null;
        }

        /// <summary>기척을 낼 수 있는 소리 갈래(무리 몫). 정예는 종류와 관계없이 숨소리.</summary>
        public enum Voice
        {
            None = -1,
            /// <summary>굴쥐 긁기.</summary>
            Scratch,
            /// <summary>멧돼지 콧김.</summary>
            Snort,
            /// <summary>궁수 시위 삐걱.</summary>
            Creak,
            /// <summary>정예 숨소리.</summary>
            Breath,
        }

        /// <summary>한 번에 기척을 내는 무리 수(가까운 순, 7장 #18 소리 과밀).</summary>
        public const int MaxGroups = 2;
        /// <summary>벽 너머 음량 배율과 저역 통과 차단 주파수.</summary>
        public const float WallVolume = 0.6f;
        public const float WallCutoff = 900f;
        /// <summary>가장 먼 기척 거리(종류별 거리 중 가장 큰 값 = 정예 숨소리 16). 이보다 먼 무리는 살피지 않는다.</summary>
        public const float MaxRange = 16f;
        /// <summary>문턱 기척: 문 가운데에서 이 거리 안에 들어서면.</summary>
        public const float ThresholdRange = 3.5f;
        /// <summary>문턱 기척은 보통 기척보다 이만큼 크게.</summary>
        const float ThresholdLevel = 1.35f;
        /// <summary>정예가 있는 무리에서 숨소리를 고를 확률(나머지는 졸개 소리).</summary>
        const float EliteBreathChance = 0.5f;
        const float ScanInterval = 0.25f;
        const int Voices = 3;
        const int MaxScan = 32;
        /// <summary>처음 가까워진 무리의 첫 기척까지(초).</summary>
        const float FirstGapMin = 0.8f;
        const float FirstGapMax = 2.5f;
        /// <summary>고른 졸개가 없을 때 다시 볼 때까지(초).</summary>
        const float RetryGap = 1f;
        /// <summary>좌우: 플레이어보다 이만큼 옆이면 끝까지(±0.8).</summary>
        const float PanSpan = 9f;
        const float MaxPan = 0.8f;
        /// <summary>거리 끝에서 남는 음량 비율(거리 비율 제곱으로 줄어듦).</summary>
        const float EdgeGain = 0.15f;
        /// <summary>찍찍: 거리, 음량, 한 번의 공포에서 낼 수(나머지는 묻음), 둘째 찍찍 늦춤.</summary>
        const float SqueakRange = 16f;
        const float SqueakLevel = 0.5f;
        const int SqueaksPerBurst = 2;
        const float SqueakBurstWindow = 0.3f;
        const float SqueakSecondDelay = 0.07f;

        /// <summary>종류별 들리는 거리(유닛). 정예 숨소리 16은 3차 초안 2-8 '소리·빛 단서'(어둠 속 길잡이) 값이다.</summary>
        public static float RangeOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch: return 12f;
                case Voice.Snort: return 14f;
                case Voice.Creak: return 12f;
                case Voice.Breath: return 16f;
                default: return 0f;
            }
        }

        /// <summary>종류별 간격(초, 무리마다 다음 소리까지 무작위).</summary>
        public static void GapOf(Voice v, out float min, out float max)
        {
            switch (v)
            {
                case Voice.Scratch: min = 4f; max = 7f; return;
                case Voice.Snort: min = 5f; max = 9f; return;
                case Voice.Creak: min = 6f; max = 9f; return;
                case Voice.Breath: min = 4f; max = 6f; return;
                default: min = 6f; max = 9f; return;
            }
        }

        /// <summary>종류별 기본 음량(효과음 음량에 곱함). 먼 소리(0.16~0.3)와 비슷한 크기라 싸움 소리를 덮지 않는다.</summary>
        static float LevelOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch: return 0.3f;
                case Voice.Snort: return 0.34f;
                case Voice.Creak: return 0.26f;
                case Voice.Breath: return 0.32f;
                default: return 0f;
            }
        }

        /// <summary>적의 기척 갈래. 정예는 숨소리, 둥지·보스(오우거 소리는 꾸러미 ②)는 없음.</summary>
        public static Voice VoiceOf(Enemy e)
        {
            if (!e || e.IsDummy || e.IsBoss) return Voice.None;
            if (e.Kind == MonsterKind.Nest) return Voice.None;
            if (e.IsElite) return Voice.Breath;
            switch (e.Kind)
            {
                case MonsterKind.Rat: return Voice.Scratch;
                case MonsterKind.Boar: return Voice.Snort;
                case MonsterKind.Archer: return Voice.Creak;
                default: return Voice.None;
            }
        }

        /// <summary>기척을 낼 수 있는 상태(잠·먹는 중이고 알아채기 전, 무리에 속함).</summary>
        static bool Resting(Enemy e) =>
            e && !e.Dead && !e.Aware && !e.WakePending && e.GroupId >= 0 && e.isActiveAndEnabled;

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
            /// <summary>기척(토글로 끔)인가, 찍찍(토글과 관계없음)인가.</summary>
            public bool Lurk;
        }

        // ── 시험·eval 확인용 ──
        /// <summary>지금 울리는 소리 수(기척 + 찍찍).</summary>
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
        /// <summary>지금 울리는 소리 중 벽 너머(작고 먹먹하게)인 수.</summary>
        public int MuffledPlayingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].Src && _slots[i].Src.isPlaying && _slots[i].Muffled) n++;
                return n;
            }
        }
        /// <summary>지난 살핌에서 기척을 낼 무리 수(0~2).</summary>
        public int ActiveGroupCount { get; private set; }
        /// <summary>지금까지 낸 기척 수 / 그중 벽 너머 / 문턱 기척 수 / 찍찍 수.</summary>
        public int SoundsStarted { get; private set; }
        public int MuffledStarted { get; private set; }
        public int DoorsFired { get; private set; }
        public int SqueaksPlayed { get; private set; }

        readonly Slot[] _slots = new Slot[Voices];
        AudioClip[] _scratch;
        AudioClip[] _squeak;
        AudioClip _snort, _creak, _breath;

        readonly int[] _scanId = new int[MaxScan];
        readonly float[] _scanSq = new float[MaxScan];
        int _scanCount;
        readonly int[] _active = { -1, -1 };
        readonly Dictionary<int, float> _nextAt = new Dictionary<int, float>(64);
        readonly HashSet<DungeonEdge> _doorsDone = new HashSet<DungeonEdge>();
        float _scanAt;
        float _squeakBurstAt = -99f;
        int _squeaksInBurst;
        System.Random _rng;

        void Awake()
        {
            Instance = this;
            _rng = new System.Random(unchecked((int)(System.DateTime.Now.Ticks & 0x7fffffff)));
            var holder = new GameObject("LurkSounds");
            holder.transform.SetParent(transform, false);
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Lurk voice " + i);
                go.transform.SetParent(holder.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                src.priority = 150;
                src.volume = 0f;
                // 벽 너머: 900Hz 아래만(소리 줄기마다 저역 통과 필터 하나, 벽 너머일 때만 켬).
                var lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = WallCutoff;
                lp.lowpassResonanceQ = 1f;
                lp.enabled = false;
                _slots[i] = new Slot { Src = src, Filter = lp, Started = -99f };
            }

            _scratch = new AudioClip[DungeonAmbienceSynth.ScratchVariants];
            for (int i = 0; i < _scratch.Length; i++) _scratch[i] = DungeonAmbienceSynth.BuildScratch(i);
            _squeak = new AudioClip[DungeonAmbienceSynth.SqueakVariants];
            for (int i = 0; i < _squeak.Length; i++) _squeak[i] = DungeonAmbienceSynth.BuildSqueak(i);
            _snort = DungeonAmbienceSynth.BuildSnort();
            _creak = DungeonAmbienceSynth.BuildBowCreak();
            _breath = DungeonAmbienceSynth.BuildBeastBreath();

            // 루트가 ResetStatics로 구독을 비운 뒤에 붙으므로 여기서 구독한다.
            CombatEvents.EnemyFrightened += OnFrightened;
            DungeonEvents.ExpeditionRestarted += OnExpeditionRestarted;
        }

        void OnDestroy()
        {
            CombatEvents.EnemyFrightened -= OnFrightened;
            DungeonEvents.ExpeditionRestarted -= OnExpeditionRestarted;
            if (Instance == this) Instance = null;
            DestroyClips(_scratch);
            DestroyClips(_squeak);
            DestroyClip(_snort);
            DestroyClip(_creak);
            DestroyClip(_breath);
        }

        void Update()
        {
            var p = PlayerController.Instance;
            if (!Tuning.Sound) StopSlots(false);
            else if (!Enabled) StopSlots(true);
            if (!p) return;
            Vector2 ear = p.Position;
            UpdateMix(ear);

            var root = DungeonRoot.Instance;
            if (!Enabled || !Tuning.Sound || p.IsDown || (root && !root.Playing))
            {
                ActiveGroupCount = 0;
                _active[0] = _active[1] = -1;
                return;
            }
            float now = Time.time;
            if (now >= _scanAt)
            {
                _scanAt = now + ScanInterval;
                Scan(ear, now);
                CheckThresholds(root, ear, now);
            }
            for (int k = 0; k < MaxGroups; k++)
            {
                int id = _active[k];
                if (id < 0) continue;
                if (_nextAt.TryGetValue(id, out float at) && now >= at) Speak(id, ear, now);
            }
        }

        // ───────────────────────── 무리 기척 ─────────────────────────

        /// <summary>들리는 거리 안에 쉬는 졸개가 있는 무리를 모아 가까운 둘을 고른다. 새로 든 무리는 첫 소리를 조금 뒤에 낸다.</summary>
        void Scan(Vector2 ear, float now)
        {
            _scanCount = 0;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!Resting(e)) continue;
                var v = VoiceOf(e);
                if (v == Voice.None) continue;
                float sq = (e.Position - ear).sqrMagnitude;
                float r = RangeOf(v);
                if (sq > r * r) continue;
                int s = FindScan(e.GroupId);
                if (s < 0)
                {
                    if (_scanCount >= MaxScan) continue;
                    s = _scanCount++;
                    _scanId[s] = e.GroupId;
                    _scanSq[s] = sq;
                }
                else if (sq < _scanSq[s]) _scanSq[s] = sq;
            }

            int a = -1, b = -1;
            for (int s = 0; s < _scanCount; s++)
            {
                if (a < 0 || _scanSq[s] < _scanSq[a])
                {
                    b = a;
                    a = s;
                }
                else if (b < 0 || _scanSq[s] < _scanSq[b]) b = s;
            }
            int idA = a >= 0 ? _scanId[a] : -1;
            int idB = b >= 0 ? _scanId[b] : -1;
            if (idA >= 0 && idA != _active[0] && idA != _active[1]) Arm(idA, now);
            if (idB >= 0 && idB != _active[0] && idB != _active[1]) Arm(idB, now);
            _active[0] = idA;
            _active[1] = idB;
            ActiveGroupCount = (idA >= 0 ? 1 : 0) + (idB >= 0 ? 1 : 0);
        }

        int FindScan(int groupId)
        {
            for (int s = 0; s < _scanCount; s++)
                if (_scanId[s] == groupId) return s;
            return -1;
        }

        /// <summary>가까운 둘에 새로 든 무리: 남은 시계가 너무 이르면 첫 소리를 FirstGap 뒤로 미룬다(다가가자마자 겹쳐 나지 않게).</summary>
        void Arm(int groupId, float now)
        {
            float first = now + Range(FirstGapMin, FirstGapMax);
            if (!_nextAt.TryGetValue(groupId, out float at) || at < first) _nextAt[groupId] = first;
        }

        /// <summary>무리 하나가 소리를 낸다: 정예가 있으면 절반은 숨소리, 아니면 들리는 거리 안 졸개 하나를 무작위로.</summary>
        void Speak(int groupId, Vector2 ear, float now)
        {
            Enemy elite = null, pick = null;
            int seen = 0;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!Resting(e) || e.GroupId != groupId) continue;
                var v = VoiceOf(e);
                if (v == Voice.None) continue;
                float r = RangeOf(v);
                if ((e.Position - ear).sqrMagnitude > r * r) continue;
                if (v == Voice.Breath)
                {
                    if (!elite) elite = e;
                    continue;
                }
                seen++;
                if (_rng.Next(seen) == 0) pick = e;
            }
            var chosen = elite && (!pick || _rng.NextDouble() < EliteBreathChance) ? elite : pick;
            if (!chosen)
            {
                _nextAt[groupId] = now + RetryGap;
                return;
            }
            var voice = VoiceOf(chosen);
            GapOf(voice, out float gapMin, out float gapMax);
            _nextAt[groupId] = now + Range(gapMin, gapMax);
            Play(chosen, chosen.Position, ClipOf(voice), LevelOf(voice), PitchOf(voice), RangeOf(voice), true, 0f, ear);
        }

        AudioClip ClipOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch: return _scratch != null && _scratch.Length > 0 ? _scratch[_rng.Next(_scratch.Length)] : null;
                case Voice.Snort: return _snort;
                case Voice.Creak: return _creak;
                case Voice.Breath: return _breath;
                default: return null;
            }
        }

        /// <summary>같은 소리 반복 티를 줄이는 음높이 폭(굴쥐는 넓게, 정예 숨은 낮게).</summary>
        float PitchOf(Voice v)
        {
            switch (v)
            {
                case Voice.Scratch: return Range(0.9f, 1.15f);
                case Voice.Snort: return Range(0.85f, 1.05f);
                case Voice.Creak: return Range(0.92f, 1.08f);
                case Voice.Breath: return Range(0.88f, 1f);
                default: return 1f;
            }
        }

        // ───────────────────────── 문턱 기척 ─────────────────────────

        /// <summary>
        /// 다가간 문 너머 칸에 쉬는 무리가 있으면 그 무리 소리를 조금 크게 한 번 내고 문틀에 흙먼지를 떨군다. 문 하나에 원정당 1번(장면마다 새 지도라
        /// 이 장면 안에서 한 번, 원정 다시 시작이면 비움). 한 번 살필 때 문 하나만.
        /// </summary>
        void CheckThresholds(DungeonRoot root, Vector2 ear, float now)
        {
            var world = root ? root.World : null;
            if (world == null) return;
            var edges = world.Edges;
            float rr = ThresholdRange * ThresholdRange;
            var cur = root.CurrentCell;
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null || (edge.DoorCenter - ear).sqrMagnitude > rr || _doorsDone.Contains(edge)) continue;
                var far = FarSide(edge, cur, ear);
                if (far == null) continue;
                var m = RestingIn(far);
                if (!m) continue;
                _doorsDone.Add(edge);
                DoorsFired++;
                var voice = VoiceOf(m);
                float dist = (m.Position - ear).magnitude;
                Play(m, m.Position, ClipOf(voice), LevelOf(voice) * ThresholdLevel, PitchOf(voice), Mathf.Max(RangeOf(voice), dist + 4f), true, 0f, ear);
                // 이 무리의 보통 기척은 한 바퀴 뒤로(문턱 소리와 겹치지 않게).
                if (m.GroupId >= 0)
                {
                    GapOf(voice, out float gapMin, out float gapMax);
                    _nextAt[m.GroupId] = now + Range(gapMin, gapMax);
                }
                // 문틀 흙먼지: DungeonAir가 소리 자리에 떨군다. 반경 0이라 소리로 깨우기(CellEncounters)는 아무도 걸리지 않는다.
                DungeonEvents.RaiseNoise(edge.DoorCenter, 0f);
                return;
            }
        }

        /// <summary>문의 저쪽 칸(플레이어가 있는 칸의 반대). 문틈에 걸쳐 있으면 마지막 칸 기준, 그것도 모르면 위치로.</summary>
        static DungeonCell FarSide(DungeonEdge edge, DungeonCell cur, Vector2 at)
        {
            if (cur != null)
            {
                if (cur == edge.A) return edge.B;
                if (cur == edge.B) return edge.A;
            }
            bool inA = edge.A != null && edge.A.Contains(at);
            bool inB = edge.B != null && edge.B.Contains(at);
            if (inA && !inB) return edge.B;
            if (inB && !inA) return edge.A;
            return null;
        }

        /// <summary>칸 안(제자리 기준)에서 쉬는, 기척을 낼 수 있는 적 하나(정예 먼저).</summary>
        static Enemy RestingIn(DungeonCell cell)
        {
            Enemy found = null;
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!Resting(e) || VoiceOf(e) == Voice.None || !cell.Bounds.Contains(e.HomePosition)) continue;
                if (e.IsElite) return e;
                if (!found) found = e;
            }
            return found;
        }

        void OnExpeditionRestarted()
        {
            _doorsDone.Clear();
            _nextAt.Clear();
            _active[0] = _active[1] = -1;
            StopSlots(false);
        }

        // ───────────────────────── 찍찍 ─────────────────────────

        /// <summary>겁먹은 굴쥐 찍찍(무리 공포 ③). 한 번의 공포에 여러 마리가 겁먹어도 둘까지만, 둘째는 조금 늦고 높게.</summary>
        void OnFrightened(Enemy e, float seconds)
        {
            if (!e || e.Kind != MonsterKind.Rat || !Tuning.Sound || _squeak == null || _squeak.Length == 0) return;
            var p = PlayerController.Instance;
            if (!p) return;
            Vector2 ear = p.Position;
            if ((e.Position - ear).sqrMagnitude > SqueakRange * SqueakRange) return;
            float now = Time.unscaledTime;
            if (now - _squeakBurstAt > SqueakBurstWindow)
            {
                _squeakBurstAt = now;
                _squeaksInBurst = 0;
            }
            if (_squeaksInBurst >= SqueaksPerBurst) return;
            float delay = _squeaksInBurst * SqueakSecondDelay;
            float pitch = Range(0.92f, 1.12f) * (_squeaksInBurst > 0 ? 1.1f : 1f);
            _squeaksInBurst++;
            Play(e, e.Position, _squeak[_rng.Next(_squeak.Length)], SqueakLevel, pitch, SqueakRange, false, delay, ear);
            SqueaksPlayed++;
        }

        // ───────────────────────── 소리 줄기 ─────────────────────────

        /// <summary>쉬는 줄기를 먼저, 다 바쁘면 가장 먼저 시작한 줄기를 끊고 낸다. 벽 너머는 시작할 때 한 번 정한다(소리 도중 필터를 바꾸면 툭 끊겨 들림).</summary>
        void Play(Enemy source, Vector2 pos, AudioClip clip, float level, float pitch, float range, bool lurk, float delay, Vector2 ear)
        {
            if (!clip || !Tuning.Sound) return;
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
            s.Source = source;
            s.Pos = pos;
            s.Level = level;
            s.Range = Mathf.Max(0.1f, range);
            s.Lurk = lurk;
            s.Started = Time.unscaledTime;
            s.Muffled = Physics2D.Linecast(ear, pos, Layers.WallMask).collider != null;
            s.Filter.enabled = s.Muffled;
            s.Src.Stop();
            s.Src.clip = clip;
            s.Src.pitch = pitch;
            Mix(ref s, ear);
            if (delay > 0f) s.Src.PlayDelayed(delay);
            else s.Src.Play();
            if (lurk)
            {
                SoundsStarted++;
                if (s.Muffled) MuffledStarted++;
            }
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

        static void Mix(ref Slot s, Vector2 ear)
        {
            float d = (s.Pos - ear).magnitude;
            float k = Mathf.Clamp01(d / s.Range);
            float g = EdgeGain + (1f - EdgeGain) * (1f - k) * (1f - k);
            if (s.Muffled) g *= WallVolume;
            s.Src.volume = Mathf.Clamp01(s.Level * g * Tuning.SoundVolume);
            s.Src.panStereo = Mathf.Clamp((s.Pos.x - ear.x) / PanSpan, -MaxPan, MaxPan);
        }

        /// <summary>울리는 소리를 멈춘다. lurkOnly면 기척만(찍찍은 남김).</summary>
        void StopSlots(bool lurkOnly)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Src || !s.Src.isPlaying || (lurkOnly && !s.Lurk)) continue;
                s.Src.Stop();
                s.Source = null;
            }
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        static void DestroyClips(AudioClip[] clips)
        {
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++) DestroyClip(clips[i]);
        }

        static void DestroyClip(AudioClip clip)
        {
            if (clip) Destroy(clip);
        }
    }
}
