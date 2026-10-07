using System;
using Demo6.Core.TestStart;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demo6.Game
{
    /// <summary>
    /// 바로 가기 시험 메뉴의 한 번 시작(TestLauncher가 '시작'에서 부름). 꾸러미 만들기는 Core(TestStartBuilder)가 하고, 여기서는
    /// ① 꾸러미 깔기(ProfileCarry.Clear → ProfileCarry.Install(plan.Carry), TownTravel.Clear) ② 시험 손잡이(무적 = Tuning.Invincible, 적 없음 = CellEncounters.SkipSpawnForTest,
    /// 전투 시험장 전설 = Tuning.TestLegendOn·TestLegendRoll, 전투 시험장 '시작 무기에 버팀 룬' = Tuning.TestSuperArmorRune(시험장은 꾸러미 무기를 쓰지 않음)) ③ 정식 장면 전환(마을: 단계가 '처음'이 아니면 TownTravel.Set(바구니 도착 쪽지, 요약 없음) → SceneTravel.Load(TownPath),
    /// 던전: ProfileCarry.SetTrip(TripPlan{층·도착·Den·DenFight·ForcedSeed, RequestedRealtime}) → SceneTravel.Load(DungeonPath), 전투 시험장: SceneTravel.Load(CombatTestPath))
    /// ④ 장면마다 손잡이를 넣는 TestLaunchKnobs(DontDestroyOnLoad)를 만든다. 정식 새 판과 다른 시작이면 이번 판을 '시험 판'으로 적는다(TestRunFlag, 4장 Q7).
    /// 이번 플레이에만 산다: 플레이를 새로 시작하면(SubsystemRegistration) 세션을 비우고, 시험 메뉴가 바꾼 Tuning 값(무적·전설 손잡이)을 바꾸기 전 값으로 되돌린다
    /// (도메인 다시 불러오기가 꺼져 있어 정적 값이 남으므로, 다른 장면에서 Play해도 지금처럼 동작하게).
    /// 저장·처음 화면·멈춤 창 1차 8-1·8-3: '꾸러미 글로 시작'(BeginWithCarry, 손잡이 물체 없음)도 시험 메뉴 판이다. 시험 메뉴 판은 저장하지 않는다(3-1).
    /// 판 끝(End)은 GameFlow가 이어하기·새로 시작·처음 화면으로·시험 메뉴로에서 부른다(4-4의 1).
    /// </summary>
    public static class TestLaunchSession
    {
        public const string LauncherPath = "Assets/Scenes/TestLauncher.unity";
        public const string CombatTestPath = "Assets/Scenes/CombatTest.unity";

        /// <summary>
        /// 이번 플레이에서 시험 메뉴로 시작했는가. 꾸러미 글로 시작한 판도 시험 메뉴 판이라 참이고, 저장하지 않는다(저장·처음 화면·멈춤 창 1차 3-1·8-3).
        /// End로 끝내면 거짓.
        /// </summary>
        public static bool Active => Plan != null || _carryStart;
        /// <summary>이번 시작 계획(없으면 null).</summary>
        public static TestStartPlan Plan { get; private set; }
        /// <summary>시험 메뉴 '꾸러미 글로 시작'(BeginWithCarry)으로 시작했다(저장·처음 화면·멈춤 창 1차 8-3).</summary>
        static bool _carryStart;

        // 시험 메뉴가 바꾸기 전 Tuning 값(이번 플레이에서 처음 시작할 때만 기억하고, 플레이를 새로 시작할 때 되돌린 뒤 비운다).
        static bool _tuningSaved;
        static bool _savedInvincible;
        static bool _savedSuperArmorRune;
        static readonly bool[] SavedLegendOn = new bool[3];
        static readonly int[] SavedLegendRoll = new int[3];
        static TestLaunchKnobs _knobs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Plan = null;
            _carryStart = false;
            _knobs = null;
            RestoreTuning();
        }

        /// <summary>
        /// 시험 메뉴 판을 끝낸다(저장·처음 화면·멈춤 창 1차 4-4의 1·5-5·8-1): 계획·꾸러미 글 시작 표시를 비우고, 손잡이 물체(TestLaunchKnobs)를 없애고,
        /// 시험 메뉴가 바꾼 Tuning 값(무적·전설 손잡이)을 바꾸기 전 값으로 되돌리고, '적 없음'을 끈다. 이미 끝났으면 아무 일도 하지 않는다.
        /// 꾸러미·도착 쪽지는 건드리지 않는다(부르는 GameFlow가 비우거나 깐다).
        /// </summary>
        public static void End()
        {
            bool any = Plan != null || _carryStart || _knobs != null || _tuningSaved || CellEncounters.SkipSpawnForTest;
            if (!any) return;
            Plan = null;
            _carryStart = false;
            if (_knobs) UnityEngine.Object.Destroy(_knobs.gameObject);
            _knobs = null;
            RestoreTuning();
            CellEncounters.SkipSpawnForTest = false;
            Debug.Log("[시험 메뉴] 시험 메뉴 판을 끝냈다");
        }

        /// <summary>
        /// 시작: preset으로 계획을 만들고(TestStartBuilder.Build, salt = 지금 시각) 꾸러미·손잡이를 넣은 뒤 장면을 바꾼다.
        /// 장면을 불러오지 못하면 경고를 남기고 false(시험 메뉴에 남음, 꾸러미·쪽지·손잡이는 부르기 전으로 되돌림).
        /// </summary>
        public static bool Begin(TestStartPreset preset)
        {
            if (preset == null)
            {
                Debug.LogWarning("[시험 메뉴] 설정이 없어 시작하지 않는다");
                return false;
            }
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[시험 메뉴] 플레이 중에만 시작한다");
                return false;
            }
            TestStartPlan plan;
            string summary;
            try
            {
                var p = preset.Clone().Normalize();
                plan = TestStartBuilder.Build(p, (ulong)DateTime.UtcNow.Ticks);
                summary = plan != null ? plan.Summary() : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[시험 메뉴] 시작 상태를 만들지 못했다: " + e.Message);
                return false;
            }
            if (plan == null || plan.Carry == null || plan.Preset == null)
            {
                Debug.LogWarning("[시험 메뉴] 시작 계획이 비어 시작하지 않는다");
                return false;
            }
            string path = ScenePath(plan.Scene);
            if (!SceneTravel.CanLoad(path))
            {
                Debug.LogWarning($"[시험 메뉴] {path}를 불러올 수 없어 시작하지 않는다(빌드 목록에도 파일에도 없음)");
                return false;
            }
            var p2 = plan.Preset;
            Debug.Log("[시험 메뉴] " + summary);

            // 실패하면 되돌릴 값(부르기 전 꾸러미·쪽지·손잡이).
            var oldData = ProfileCarry.Data;
            var oldTrip = ProfileCarry.Trip;
            var oldNote = TownTravel.Note;
            bool oldInvincible = Tuning.Invincible;
            bool oldSuperArmorRune = Tuning.TestSuperArmorRune;
            bool oldSkip = CellEncounters.SkipSpawnForTest;
            var oldLegendOn = (bool[])Tuning.TestLegendOn.Clone();
            var oldLegendRoll = (int[])Tuning.TestLegendRoll.Clone();
            bool savedNow = SaveTuningOnce();

            // ② 손잡이.
            Tuning.Invincible = p2.Invincible;
            CellEncounters.SkipSpawnForTest = p2.NoEnemies && plan.Scene == TestStartScene.Dungeon;
            if (plan.Scene == TestStartScene.CombatTest)
            {
                for (int i = 0; i < Tuning.TestLegendOn.Length; i++)
                {
                    Tuning.TestLegendOn[i] = i < p2.LegendOn.Length && p2.LegendOn[i];
                    Tuning.TestLegendRoll[i] = Mathf.Clamp(p2.LegendRoll, 0, 1000);
                }
                // 시작 무기에 버팀 룬: 전투 시험장은 가방·꾸러미 무기가 없어 시험 손잡이로 대신한다.
                Tuning.TestSuperArmorRune = p2.StartWeaponRune;
            }

            // 처음 화면에서 시작한 저장하는 판이 남지 않게 끝낸다(시험 메뉴 판은 저장하지 않음, 저장·처음 화면·멈춤 창 1차 3-1).
            GameSession.EndSession();

            // ① 꾸러미.
            ProfileCarry.Clear();
            TownTravel.Clear();
            ProfileCarry.Install(plan.Carry);

            // ③ 정식 장면 전환(불러오기는 다음 프레임에 끝나므로 ④ 손잡이는 불러오기를 부른 뒤에 만들어도 sceneLoaded를 받는다).
            switch (plan.Scene)
            {
                case TestStartScene.Town:
                    if (!plan.TownNewPlay) TownTravel.Set(new TownArrivalNote { Kind = TownArrivalKind.Basket });
                    break;
                case TestStartScene.Dungeon:
                    ProfileCarry.SetTrip(new TripPlan
                    {
                        Floor = plan.Floor,
                        Arrival = plan.FirstStart ? ArrivalKind.FirstStart : ArrivalKind.Basket,
                        Den = plan.Den,
                        DenFight = plan.DenFight,
                        ForcedSeed = plan.ForcedSeed,
                        PickLanding = false,
                        RequestedRealtime = Time.realtimeSinceStartup,
                    });
                    break;
            }
            if (!SceneTravel.Load(path))
            {
                Debug.LogWarning($"[시험 메뉴] {path}를 불러오지 못해 시험 메뉴에 남는다");
                ProfileCarry.Install(oldData);
                ProfileCarry.SetTrip(oldTrip);
                if (oldNote != null) TownTravel.Set(oldNote);
                else TownTravel.Clear();
                Tuning.Invincible = oldInvincible;
                Tuning.TestSuperArmorRune = oldSuperArmorRune;
                CellEncounters.SkipSpawnForTest = oldSkip;
                Array.Copy(oldLegendOn, Tuning.TestLegendOn, Tuning.TestLegendOn.Length);
                Array.Copy(oldLegendRoll, Tuning.TestLegendRoll, Tuning.TestLegendRoll.Length);
                if (savedNow) _tuningSaved = false;
                return false;
            }

            // ④ 장면별 손잡이(앞 시작의 것은 버림).
            if (_knobs) UnityEngine.Object.Destroy(_knobs.gameObject);
            var go = new GameObject("TestLaunchKnobs");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _knobs = go.AddComponent<TestLaunchKnobs>();
            _knobs.Init(plan);
            Plan = plan;
            _carryStart = false;
            // 판정 기록 '시험 판'(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q7): 정식 새 판과 다른 시작(위치·단계·레벨·장비·시험 손잡이)이면 적는다.
            if (!p2.IsPlainStart) TestRunFlag.Note(TestRunReason.Launcher);
            return true;
        }

        /// <summary>
        /// 시험 메뉴 '⑧ 꾸러미 글로 시작'(저장·처음 화면·멈춤 창 1차 8-3): 붙여 넣은 꾸러미(저장 파일 글이나 머리 없는 꾸러미 글을 SaveFile.TryParse로 푼 것)로 마을에서 시작한다.
        /// 복사본을 깔고 원정 몫(Leg)은 비운다 — 던전 F1에서 복사한 머리 없는 글의 원정 몫을 마을에 남기지 않는다(16장 12). 방문 수가 1 이상이면 바구니 도착 쪽지(요약 없음)를 두어
        /// 귀환 지점에서, 0이면 새 플레이(남쪽 길 끝·오프닝)로 시작한다. 손잡이 물체(TestLaunchKnobs)는 만들지 않는다.
        /// 시험 메뉴 판이라 Active가 참이고 저장하지 않는다(3-1). 판정 기록에도 '시험 판'(시험 메뉴)으로 적는다.
        /// 장면을 불러오지 못하면 경고를 남기고 false(꾸러미·쪽지는 부르기 전으로 되돌림). 되면 앞 시험 메뉴 판(손잡이 물체·Tuning 값)을 End로 끝낸 뒤 이번 판을 적는다.
        /// </summary>
        public static bool BeginWithCarry(Demo6.Core.Dungeon.CarryData carry)
        {
            if (carry == null)
            {
                Debug.LogWarning("[시험 메뉴] 꾸러미가 없어 시작하지 않는다");
                return false;
            }
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[시험 메뉴] 플레이 중에만 시작한다");
                return false;
            }
            Demo6.Core.Dungeon.CarryData c;
            try
            {
                c = carry.Clone();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[시험 메뉴] 꾸러미를 복사하지 못했다: " + e.Message);
                return false;
            }
            if (c == null)
            {
                Debug.LogWarning("[시험 메뉴] 꾸러미 복사본이 비어 시작하지 않는다");
                return false;
            }

            // 실패하면 되돌릴 값(부르기 전 꾸러미·쪽지).
            var oldData = ProfileCarry.Data;
            var oldTrip = ProfileCarry.Trip;
            var oldNote = TownTravel.Note;

            // 처음 화면에서 시작한 저장하는 판이 남지 않게 끝낸다(3-1).
            GameSession.EndSession();
            ProfileCarry.Clear();
            TownTravel.Clear();
            c.Leg = null;
            ProfileCarry.Install(c);
            if (TownSave.Visits(c) > 0) TownTravel.Set(new TownArrivalNote { Kind = TownArrivalKind.Basket });
            if (!SceneTravel.Load(SceneTravel.TownPath))
            {
                Debug.LogWarning($"[시험 메뉴] {SceneTravel.TownPath}를 불러오지 못해 시험 메뉴에 남는다");
                ProfileCarry.Install(oldData);
                ProfileCarry.SetTrip(oldTrip);
                if (oldNote != null) TownTravel.Set(oldNote);
                else TownTravel.Clear();
                return false;
            }

            // 앞 시험 메뉴 판의 손잡이 물체·Tuning 값이 이번 판에 남지 않게 끝낸다(장면은 다음 프레임에 바뀐다).
            End();
            _carryStart = true;
            TestRunFlag.Note(TestRunReason.Launcher);
            Debug.Log($"[시험 메뉴] 꾸러미 글로 시작 — 원정 {c.Expedition} · 레벨 {c.Level}");
            return true;
        }

        /// <summary>계획 장면의 경로.</summary>
        public static string ScenePath(TestStartScene scene)
        {
            switch (scene)
            {
                case TestStartScene.Dungeon: return SceneTravel.DungeonPath;
                case TestStartScene.CombatTest: return CombatTestPath;
                default: return SceneTravel.TownPath;
            }
        }

        /// <summary>이번 플레이에서 처음 바꿀 때만 Tuning 값을 기억한다. 이번에 기억했으면 true.</summary>
        static bool SaveTuningOnce()
        {
            if (_tuningSaved) return false;
            _tuningSaved = true;
            _savedInvincible = Tuning.Invincible;
            _savedSuperArmorRune = Tuning.TestSuperArmorRune;
            for (int i = 0; i < SavedLegendOn.Length && i < Tuning.TestLegendOn.Length; i++)
            {
                SavedLegendOn[i] = Tuning.TestLegendOn[i];
                SavedLegendRoll[i] = Tuning.TestLegendRoll[i];
            }
            return true;
        }

        static void RestoreTuning()
        {
            if (!_tuningSaved) return;
            _tuningSaved = false;
            Tuning.Invincible = _savedInvincible;
            Tuning.TestSuperArmorRune = _savedSuperArmorRune;
            for (int i = 0; i < SavedLegendOn.Length && i < Tuning.TestLegendOn.Length; i++)
            {
                Tuning.TestLegendOn[i] = SavedLegendOn[i];
                Tuning.TestLegendRoll[i] = SavedLegendRoll[i];
            }
        }
    }

    /// <summary>
    /// 시험 메뉴 세션의 장면별 손잡이(DontDestroyOnLoad 물체 하나, TestLaunchSession.Begin이 만든다). 장면을 불러올 때마다 다음 Update에서 한 번 넣는다:
    /// 던전 = 어둠 끄기(DungeonLighting.DarknessOn)·시야 끄기(VisionSystem.VisionOn), 굴 안 바로 싸움이면 들어선 뒤(DungeonRoot.Playing, 실제 시간 0.6초) 보스방 오우거를 깨움(Enemy.Wake),
    /// 전투 시험장 = 층·프리셋·무기(CombatTestRoot.SetFloor·ApplyPreset·SetTestWeapon, 첫 한 번). 마을은 넣을 것이 없다.
    /// 장면 루트가 비우는 정적 사건(CombatEvents 등)은 쓰지 않고 SceneManager.sceneLoaded만 듣는다.
    /// </summary>
    public sealed class TestLaunchKnobs : MonoBehaviour
    {
        /// <summary>굴 안 바로 싸움: 층에 들어선 뒤 오우거를 깨우기까지(실제 시간, 화면이 밝아지는 0.5초 뒤).</summary>
        public const float DenWakeDelay = 0.6f;

        TestStartPlan _plan;
        /// <summary>Begin 뒤 장면을 한 번이라도 불러왔다(그 전의 루트는 옛 장면 것이라 보지 않는다).</summary>
        bool _loadedOnce;
        /// <summary>새 장면을 불러왔고 아직 손잡이를 넣지 않았다.</summary>
        bool _scenePending;
        bool _denFightPending;
        float _denPlayingSince = -1f;
        bool _combatPending;

        /// <summary>이 세션의 계획.</summary>
        public TestStartPlan Plan => _plan;

        public void Init(TestStartPlan plan)
        {
            _plan = plan;
            _denFightPending = plan != null && plan.DenFight;
            _combatPending = plan != null && plan.Scene == TestStartScene.CombatTest;
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _loadedOnce = true;
            _scenePending = true;
            _denPlayingSince = -1f;
        }

        void Update()
        {
            if (_plan == null || _plan.Preset == null || !_loadedOnce) return;
            if (_scenePending) ApplySceneKnobs();
            if (_denFightPending) TickDenFight();
        }

        void ApplySceneKnobs()
        {
            var dungeon = DungeonRoot.Instance;
            if (dungeon)
            {
                _scenePending = false;
                var p = _plan.Preset;
                var lighting = dungeon.Lighting ? dungeon.Lighting : DungeonLighting.Instance;
                if (p.DarknessOff && lighting) lighting.DarknessOn = false;
                var vision = dungeon.Vision ? dungeon.Vision : VisionSystem.Instance;
                if (p.VisionOff && vision) vision.VisionOn = false;
                // 첫 던전 장면이 보스방 안에서 시작하지 않았으면(굴을 짓지 못함 등) 깨우지 않는다.
                if (_denFightPending && !dungeon.DenFightStart) _denFightPending = false;
                return;
            }
            var combat = CombatTestRoot.Instance;
            if (combat)
            {
                _scenePending = false;
                if (!_combatPending) return;
                _combatPending = false;
                ApplyCombat(combat);
                return;
            }
            // 마을: 넣을 것이 없다.
            if (TownRoot.Instance) _scenePending = false;
        }

        /// <summary>
        /// 전투 시험장: 층 → 프리셋(모르는 이름은 FrontBack) → 무기. '보스' 프리셋은 들어올 때 2층 장비로 맞추므로(CombatTestRoot.ApplyPreset)
        /// 고른 층과 다르면 층 단추처럼 한 번 더 맞춘다.
        /// </summary>
        void ApplyCombat(CombatTestRoot combat)
        {
            var p = _plan.Preset;
            int floor = _plan.CombatFloor > 0 ? _plan.CombatFloor : p.CombatFloor;
            string name = !string.IsNullOrEmpty(_plan.CombatPreset) ? _plan.CombatPreset : p.CombatPreset;
            if (!Enum.TryParse(name, out CombatTestRoot.Preset preset) || !Enum.IsDefined(typeof(CombatTestRoot.Preset), preset))
                preset = CombatTestRoot.Preset.FrontBack;
            combat.SetFloor(floor);
            combat.ApplyPreset(preset);
            if (combat.Floor != floor) combat.SetFloor(floor);
            combat.SetTestWeapon(p.WeaponId);
            Debug.Log($"[시험 메뉴] 전투 시험장 {preset} · {combat.Floor}층 · {combat.TestWeaponId}");
        }

        /// <summary>굴 안 바로 싸움: 들어서고 0.6초 뒤 잠든 오우거를 깨운다(플레이어가 방 안이라 BossArena가 문을 막는다). 첫 굴 장면 한 번만.</summary>
        void TickDenFight()
        {
            var root = DungeonRoot.Instance;
            if (!root || _scenePending) return;
            if (!root.IsDen || !root.DenFightStart)
            {
                _denFightPending = false;
                return;
            }
            if (!root.Playing) return;
            float now = Time.realtimeSinceStartup;
            if (_denPlayingSince < 0f)
            {
                _denPlayingSince = now;
                return;
            }
            if (now - _denPlayingSince < DenWakeDelay) return;
            _denFightPending = false;
            var arena = BossArena.Instance;
            var boss = arena ? arena.Boss : null;
            if (boss && !boss.Dead && !boss.Aware)
            {
                boss.Wake(false);
                Debug.Log("[시험 메뉴] 굴 안 바로 싸움: 오우거를 깨웠다");
            }
        }
    }
}
