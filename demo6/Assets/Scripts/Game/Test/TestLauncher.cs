using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Save;
using Demo6.Core.TestStart;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바로 가기 시험 메뉴(TestLauncher.unity의 "TestLauncher" 물체, IMGUI 한 화면 — 기능만, 배치는 다듬지 않는다).
    /// ① 시작 위치 ② 레벨 1~10(0 = 단계대로)·스킬 자동 배분 ③ 무기 9종 ④ 장비 묶음(시작 장비 / 일반~전설 한 벌, 장비 층(iLv), 방어구 무게, 전설 효과 셋 켜기·세기)
    /// ⑤ 의뢰 단계 ⑥ 시험 손잡이(무적·적 없음·어둠 끄기·시야 끄기·씨앗 고정(번호)·강화석·골드·버팀 룬 넣기·시작 무기에 버팀 룬 끼우기), 전투 시험장 프리셋·층.
    /// '숫자키로 무기 바꾸기' 손잡이는 뺐다(기획/키-배치-1차.md 0장 2). 무기 종류 바꾸기는 F1 시험 패널 단추로 한다(0장 3).
    /// 마을 처음·기본값이 아닌 시작은 판정 기록에 '시험 판'으로 남는다(TestStartPreset.IsPlainStart, 4장 Q7).
    /// ⑦ [시작]과 [마지막 설정으로 바로 시작]. 설정은 PlayerPrefs(PrefsKey)에 TestStartPreset.ToText 글로 기억한다(읽기·쓰기 모두 try/catch, 실패하면 기본값).
    /// 시작은 TestLaunchSession.Begin. 주민은 내부용이라 '갱도지기(춘삼)'처럼 적는다.
    /// ⑧ 꾸러미 글로 시작(기획/저장-처음화면-멈춤창-1차.md 8-3): 여러 줄 글 칸에 저장 파일 글 전체나 머리 없는 꾸러미 글(carry v1·v2, 마을 F1 '꾸러미 글 복사')을 넣고
    /// [클립보드에서 붙여 넣기]·[저장 파일 글 불러오기](GameSave.ReadRawTextForTest — 에디터 저장 본 파일, 없으면 백업). 칸 아래 한 줄은 SaveFile.TryParse 결과(글이 바뀔 때만 다시 품).
    /// [이 글로 시작(마을)]은 읽을 수 있을 때만 TestLaunchSession.BeginWithCarry(귀환 지점, 방문 0이면 새 플레이). 시험 메뉴 판이라 저장하지 않는다. 글 칸 내용은 기억하지 않는다.
    /// 아래 단추 줄 [처음 화면으로]는 GameFlow.LeaveToTitle(저장 없이, 8-3).
    /// 단추는 OnGUI에서 받아 다음 Update에서 한 번 처리한다(TownDebugPanel 방식, 배치가 바뀌는 값을 그리는 도중에 바꾸지 않게).
    /// </summary>
    public sealed class TestLauncher : MonoBehaviour
    {
        /// <summary>설정 기억 PlayerPrefs 키.</summary>
        public const string PrefsKey = "demo6.testlauncher.preset";

        const float Margin = 16f;
        /// <summary>화면 아래 미리 보기·[시작] 줄 높이.</summary>
        const float ActionsHeight = 100f;
        const int LegendRollStep = 50;
        /// <summary>⑧ 꾸러미 글 칸 높이(넘치면 칸 안에서 스크롤).</summary>
        const float CarryTextHeight = 160f;
        static readonly string[] WeightNames = { "가죽", "사슬", "판금" };
        static readonly ArmorWeight[] Weights = { ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy };

        /// <summary>지금 화면의 설정.</summary>
        public TestStartPreset Preset { get; private set; } = new TestStartPreset();

        /// <summary>마지막 안내 글(시작 실패·기억 없음 등).</summary>
        public string Notice { get; private set; } = "";

        Vector2 _scroll;
        Action _pending;
        string _seedText = "1";
        bool _starting;
        List<GearBase> _weapons;
        string[] _combatPresets;
        // 미리 보기 한 줄(설정 글이 바뀔 때만 다시 만든다).
        string _previewKey;
        string _previewLine = "";
        // ⑧ 꾸러미 글(기억하지 않음)과 읽기 결과 한 줄(글이 바뀔 때만 다시 푼다).
        string _carryText = "";
        Vector2 _carryScroll;
        string _carryKey;
        string _carryLine = "";
        bool _carryReady;
        GUIStyle _small;
        GUIStyle _button;
        GUIStyle _note;
        GUIStyle _area;

        void Awake()
        {
            var saved = LoadSaved();
            Preset = saved ?? new TestStartPreset();
            _seedText = Preset.Seed.ToString();
            _weapons = GearBaseTable.ForPart(GearPart.Weapon);
            _combatPresets = Enum.GetNames(typeof(CombatTestRoot.Preset));
            Notice = saved != null ? "기억한 설정을 불러왔다." : "";
            Debug.Log("[시험 메뉴] 열림" + (saved != null ? " (기억한 설정)" : " (기본값)"));
        }

        void Update()
        {
            if (_pending == null) return;
            var action = _pending;
            _pending = null;
            action();
        }

        void Do(Action action) => _pending = action;

        // ───────────────────────── 설정 기억 ─────────────────────────

        /// <summary>기억한 설정(PlayerPrefs)을 읽는다. 없거나 읽지 못하면 null.</summary>
        public static TestStartPreset LoadSaved()
        {
            try
            {
                string text = PlayerPrefs.GetString(PrefsKey, "");
                if (string.IsNullOrEmpty(text)) return null;
                return TestStartPreset.TryParse(text, out var preset) ? preset : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[시험 메뉴] 기억한 설정을 읽지 못했다: " + e.Message);
                return null;
            }
        }

        /// <summary>설정을 기억한다(PlayerPrefs). 실패하면 경고만.</summary>
        public static bool Save(TestStartPreset preset)
        {
            if (preset == null) return false;
            try
            {
                PlayerPrefs.SetString(PrefsKey, preset.ToText());
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[시험 메뉴] 설정을 기억하지 못했다: " + e.Message);
                return false;
            }
        }

        // ───────────────────────── 시작 ─────────────────────────

        void StartNow()
        {
            if (_starting) return;
            ApplySeedText();
            Save(Preset);
            Launch(Preset);
        }

        void StartLast()
        {
            if (_starting) return;
            var saved = LoadSaved();
            if (saved == null)
            {
                Notice = "기억한 설정이 없다 — [시작]으로 한 번 시작하면 기억한다.";
                return;
            }
            Preset = saved;
            _seedText = Preset.Seed.ToString();
            Launch(Preset);
        }

        void Launch(TestStartPreset preset)
        {
            if (TestLaunchSession.Begin(preset))
            {
                _starting = true;
                Notice = "불러오는 중…";
            }
            else Notice = "시작하지 못했다 — 콘솔 경고를 본다.";
        }

        void ResetDefaults()
        {
            Preset = new TestStartPreset();
            _seedText = Preset.Seed.ToString();
            Notice = "기본값으로 되돌렸다(기억한 설정은 [시작]할 때 바뀐다).";
        }

        void ApplySeedText()
        {
            if (ulong.TryParse((_seedText ?? "").Trim(), out ulong seed)) Preset.Seed = seed;
            else _seedText = Preset.Seed.ToString();
        }

        // ───────────────────────── ⑧ 꾸러미 글로 시작 (저장·처음 화면·멈춤 창 1차 8-3) ─────────────────────────

        /// <summary>[클립보드에서 붙여 넣기]: 클립보드 글을 글 칸에 넣는다(마을 F1 '꾸러미 글 복사' 글을 그대로).</summary>
        void PasteCarry()
        {
            string text;
            try
            {
                text = GUIUtility.systemCopyBuffer;
            }
            catch (Exception e)
            {
                Notice = "클립보드를 읽지 못했다: " + e.Message;
                return;
            }
            if (string.IsNullOrEmpty(text))
            {
                Notice = "클립보드가 비었다.";
                return;
            }
            SetCarryText(text);
            Notice = "클립보드 글을 붙여 넣었다.";
        }

        /// <summary>[저장 파일 글 불러오기]: 이 기기 저장 글(에디터는 editor-slot1 본 파일, 없으면 백업)을 글 칸에 넣는다. 파일은 바꾸지 않는다.</summary>
        void LoadSaveText()
        {
            string text;
            try
            {
                text = GameSave.ReadRawTextForTest();
            }
            catch (Exception e)
            {
                Notice = "저장 파일을 읽지 못했다: " + e.Message;
                return;
            }
            if (text == null)
            {
                Notice = "저장 파일이 없다.";
                return;
            }
            SetCarryText(text);
            Notice = "저장 파일 글을 불러왔다.";
        }

        void SetCarryText(string text)
        {
            // 글 칸이 키보드를 잡고 있으면 칸이 제 글을 계속 들고 있어 새 글이 보이지 않으므로 놓게 한다.
            GUIUtility.keyboardControl = 0;
            _carryText = text ?? "";
            _carryScroll = Vector2.zero;
        }

        /// <summary>[이 글로 시작(마을)]: 글을 다시 풀어 읽을 수 있을 때만 TestLaunchSession.BeginWithCarry(마을 귀환 지점, 저장하지 않는 시험 메뉴 판).</summary>
        void StartWithCarry()
        {
            if (_starting) return;
            SaveParseStatus status;
            Demo6.Core.Dungeon.CarryData carry;
            try
            {
                status = SaveFile.TryParse(_carryText ?? "", out _, out carry);
            }
            catch (Exception e)
            {
                Notice = "읽지 못했다 — " + e.Message;
                return;
            }
            if (status != SaveParseStatus.Ok || carry == null)
            {
                Notice = "이 글로는 시작하지 않는다 — " + CarryStatusLine(status, carry);
                return;
            }
            if (TestLaunchSession.BeginWithCarry(carry))
            {
                _starting = true;
                Notice = "불러오는 중…";
            }
            else Notice = "시작하지 못했다 — 콘솔 경고를 본다.";
        }

        /// <summary>[처음 화면으로]: 저장 없이 처음 화면으로 간다(GameFlow.LeaveToTitle — 시험 메뉴 판 끝·꾸러미 비움·Town 다시 불러오기).</summary>
        void LeaveToTitle()
        {
            if (_starting) return;
            if (GameFlow.LeaveToTitle(false))
            {
                _starting = true;
                Notice = "처음 화면으로 가는 중…";
            }
            else Notice = "처음 화면으로 가지 못했다 — 콘솔 경고를 본다.";
        }

        /// <summary>글 칸 아래 읽기 결과 한 줄. 글이 바뀔 때만 SaveFile.TryParse로 다시 푼다.</summary>
        string CarryLine()
        {
            string text = _carryText ?? "";
            if (text == _carryKey) return _carryLine;
            _carryKey = text;
            _carryReady = false;
            if (string.IsNullOrWhiteSpace(text))
            {
                _carryLine = "글이 비었다 — 붙여 넣거나 저장 파일 글을 불러온다.";
                return _carryLine;
            }
            try
            {
                var status = SaveFile.TryParse(text, out _, out var carry);
                _carryReady = status == SaveParseStatus.Ok && carry != null;
                _carryLine = CarryStatusLine(status, carry);
            }
            catch (Exception e)
            {
                _carryLine = "읽지 못함 — " + e.Message;
            }
            return _carryLine;
        }

        /// <summary>읽기 결과 글(저장·처음 화면·멈춤 창 1차 8-3 초안).</summary>
        static string CarryStatusLine(SaveParseStatus status, Demo6.Core.Dungeon.CarryData carry)
        {
            switch (status)
            {
                case SaveParseStatus.Ok:
                    return carry != null ? $"읽을 수 있음 — 원정 {carry.Expedition}번째 준비 · 레벨 {carry.Level}" : "읽지 못함 — 꾸러미가 비었다";
                case SaveParseStatus.Damaged:
                    return "상함 — 검사 값이 맞지 않는다(check 줄을 지우면 넘어간다)";
                case SaveParseStatus.NewerVersion:
                    return "더 새 판이라 읽지 않는다";
                default:
                    return "읽지 못함 — 첫 줄이 carry v 나 demo6 save v 가 아니다";
            }
        }

        // ───────────────────────── 화면 ─────────────────────────

        void OnGUI()
        {
            DungeonUi.Begin();
            if (_small == null)
            {
                _small = new GUIStyle(DungeonUi.Small) { fontSize = 15 };
                _note = new GUIStyle(DungeonUi.Small) { fontSize = 14, fontStyle = FontStyle.Italic };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = false, padding = new RectOffset(8, 8, 4, 4) };
                _area = new GUIStyle(GUI.skin.textArea) { fontSize = 13, wordWrap = true };
            }
            var panel = new Rect(Margin, Margin, DungeonUi.Width - Margin * 2f, DungeonUi.Height - Margin * 2f);
            DungeonUi.Box(panel, 0.92f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 36f), "개발 · 바로 가기 시험 메뉴", DungeonUi.Title);
            var area = new Rect(panel.x + 16f, panel.y + 56f, panel.width - 32f, panel.height - 72f);
            GUILayout.BeginArea(area);
            // 아래 [시작] 줄 자리(ActionsHeight)를 남기고 위는 스크롤.
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(120f, area.height - ActionsHeight)));
            var p = Preset;

            DrawStart(p);
            DrawLevel(p);
            DrawWeapon(p);
            DrawGear(p);
            DrawStage(p);
            DrawKnobs(p);
            DrawCombat(p);
            DrawCarryStart();

            GUILayout.EndScrollView();
            DrawActions(p);
            GUILayout.EndArea();
        }

        static void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, DungeonUi.Bold);
        }

        /// <summary>고르기 단추(골라 있으면 눌린 모양). 새로 골랐으면 true(부르는 쪽이 Do로 다음 Update에 넘긴다).</summary>
        bool Choice(bool selected, string label, params GUILayoutOption[] options)
        {
            bool now = GUILayout.Toggle(selected, label, _button, options);
            return now && !selected;
        }

        void DrawStart(TestStartPreset p)
        {
            Section("① 시작 위치");
            GUILayout.BeginHorizontal();
            int i = 0;
            foreach (TestStartAt at in Enum.GetValues(typeof(TestStartAt)))
            {
                if (i > 0 && i % 4 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                var value = at;
                if (Choice(p.StartAt == at, SafeLabel(() => TestStartBuilder.StartLabel(value), at.ToString()))) Do(() => Preset.StartAt = value);
                i++;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawStage(TestStartPreset p)
        {
            Section("⑤ 의뢰 단계");
            bool fixedFresh = p.StartAt == TestStartAt.TownFresh;
            if (fixedFresh) GUILayout.Label("마을 처음은 의뢰 단계 '처음'으로 고정(오프닝부터).", _note);
            else if (p.StartAt == TestStartAt.CombatTest) GUILayout.Label("전투 시험장은 의뢰 단계를 쓰지 않는다.", _note);
            bool was = GUI.enabled;
            GUI.enabled = was && !fixedFresh;
            GUILayout.BeginHorizontal();
            foreach (TestQuestStage stage in Enum.GetValues(typeof(TestQuestStage)))
            {
                var value = stage;
                if (Choice(p.Stage == stage, SafeLabel(() => TestStartBuilder.StageLabel(value), stage.ToString()))) Do(() => Preset.Stage = value);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.enabled = was;
            var effective = p.EffectiveStage;
            GUILayout.Label(SafeLabel(() => TestStartBuilder.StageNote(effective), ""), _small);
        }

        void DrawLevel(TestStartPreset p)
        {
            Section("② 레벨·스킬");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−", _button, GUILayout.Width(40f))) Do(() => Preset.Level = Mathf.Max(0, Preset.Level - 1));
            GUILayout.Label(p.Level <= 0 ? "레벨: 단계대로" : $"레벨 {p.Level}", _small, GUILayout.Width(140f));
            if (GUILayout.Button("+", _button, GUILayout.Width(40f))) Do(() => Preset.Level = Mathf.Min(TestStartPreset.MaxLevel, Preset.Level + 1));
            if (GUILayout.Button("단계대로", _button)) Do(() => Preset.Level = 0);
            if (GUILayout.Button("레벨 5", _button)) Do(() => Preset.Level = 5);
            if (GUILayout.Button($"레벨 {TestStartPreset.MaxLevel}", _button)) Do(() => Preset.Level = TestStartPreset.MaxLevel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            bool auto = GUILayout.Toggle(p.AutoSkills, "스킬 점수 자동 배분(회오리 → 검풍 → 마무리 → 몸, 한 점씩) — 끄면 점수를 그대로 남김");
            if (auto != p.AutoSkills) Do(() => Preset.AutoSkills = auto);
        }

        void DrawWeapon(TestStartPreset p)
        {
            Section("③ 무기");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _weapons.Count; i++)
            {
                if (i > 0 && i % 5 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                var b = _weapons[i];
                string id = b.Id;
                if (Choice(p.WeaponId == id, b.Name, GUILayout.Width(110f))) Do(() => Preset.WeaponId = id);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawGear(TestStartPreset p)
        {
            Section("④ 장비 묶음");
            GUILayout.BeginHorizontal();
            foreach (TestGearSet set in Enum.GetValues(typeof(TestGearSet)))
            {
                var value = set;
                if (Choice(p.GearSet == set, SafeLabel(() => TestStartBuilder.GearSetLabel(value), set.ToString()))) Do(() => Preset.GearSet = value);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            bool starting = p.GearSet == TestGearSet.Starting;
            if (starting) GUILayout.Label("시작 장비: 무기 종류만 고른 것으로, 반지·목걸이 빔, iLv1 · 전설 효과는 던전·마을에서 무시(전투 시험장은 아래 전설 손잡이로 씀).", _note);
            if (p.StartAt == TestStartAt.CombatTest) GUILayout.Label("전투 시험장은 장비 묶음·레벨을 쓰지 않는다(시작 장비 + 고른 무기, 전설은 시험장 손잡이).", _note);

            bool was = GUI.enabled;
            GUI.enabled = was && !starting;
            GUILayout.BeginHorizontal();
            GUILayout.Label("장비 층(iLv)", _small, GUILayout.Width(110f));
            if (GUILayout.Button("−", _button, GUILayout.Width(40f))) Do(() => Preset.GearLevel = Mathf.Max(1, Preset.GearLevel - 1));
            GUILayout.Label(p.GearLevel.ToString(), _small, GUILayout.Width(40f));
            if (GUILayout.Button("+", _button, GUILayout.Width(40f))) Do(() => Preset.GearLevel = Mathf.Min(GearMath.MaxItemLevel, Preset.GearLevel + 1));
            GUILayout.Space(24f);
            GUILayout.Label("방어구", _small, GUILayout.Width(60f));
            for (int i = 0; i < Weights.Length; i++)
            {
                var w = Weights[i];
                bool selected = p.ArmorWeight == w || (i == 0 && p.ArmorWeight == ArmorWeight.None);
                if (Choice(selected, WeightNames[i], GUILayout.Width(70f))) Do(() => Preset.ArmorWeight = w);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.enabled = was;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < LegendaryTable.Count && i < p.LegendOn.Length; i++)
            {
                int k = i;
                bool on = GUILayout.Toggle(p.LegendOn[i], "전설 " + LegendaryTable.Get((LegendaryEffect)i).Name, GUILayout.Width(170f));
                if (on != p.LegendOn[i]) Do(() => Preset.LegendOn[k] = on);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"전설 세기 {p.LegendRoll}‰", _small, GUILayout.Width(140f));
            float v = GUILayout.HorizontalSlider(p.LegendRoll, 0f, 1000f, GUILayout.Width(260f));
            int roll = Mathf.Clamp(Mathf.RoundToInt(v / LegendRollStep) * LegendRollStep, 0, 1000);
            if (roll != p.LegendRoll) p.LegendRoll = roll;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label("전설 한 벌: 켠 효과의 부위는 전설, 끈 부위는 영웅 · 다른 한 벌: 켠 효과마다 대표 자리(번개 무기·발자국 장화·폭발 목걸이)만 전설.", _note);
        }

        void DrawKnobs(TestStartPreset p)
        {
            Section("⑥ 시험 손잡이");
            GUILayout.BeginHorizontal();
            bool inv = GUILayout.Toggle(p.Invincible, "무적", GUILayout.Width(90f));
            if (inv != p.Invincible) Do(() => Preset.Invincible = inv);
            bool none = GUILayout.Toggle(p.NoEnemies, "적 없음(던전 무리·둥지, 보스는 그대로)", GUILayout.Width(330f));
            if (none != p.NoEnemies) Do(() => Preset.NoEnemies = none);
            bool dark = GUILayout.Toggle(p.DarknessOff, "어둠 끄기", GUILayout.Width(110f));
            if (dark != p.DarknessOff) Do(() => Preset.DarknessOff = dark);
            bool vision = GUILayout.Toggle(p.VisionOff, "시야 끄기", GUILayout.Width(110f));
            if (vision != p.VisionOff) Do(() => Preset.VisionOff = vision);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            bool fix = GUILayout.Toggle(p.FixSeed, "씨앗 고정", GUILayout.Width(110f));
            if (fix != p.FixSeed) Do(() => Preset.FixSeed = fix);
            bool was = GUI.enabled;
            GUI.enabled = was && p.FixSeed;
            // 안내 칸은 늘 그린다(글자를 치는 사건 도중에 칸 수가 바뀌지 않게).
            bool validBefore = ulong.TryParse((_seedText ?? "").Trim(), out _);
            _seedText = GUILayout.TextField(_seedText ?? "", 20, GUILayout.Width(220f));
            if (ulong.TryParse((_seedText ?? "").Trim(), out ulong seed)) p.Seed = seed;
            GUILayout.Label(validBefore ? "" : "숫자만", _note, GUILayout.Width(60f));
            GUI.enabled = was;
            GUILayout.Label("(프로필 소금·시작 층 지도(굴 제외)·장비 옵션 굴림)", _note);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            AddRow("강화석 넣기", p.AddStones, n => Preset.AddStones = n);
            AddRow("골드 넣기", p.AddGold, n => Preset.AddGold = n);
            DrawRunes(p);

            GUILayout.Label(p.IsPlainStart
                ? "지금 설정은 정식 새 판과 같은 시작이다(판정 기록에 시험 판 표시 없음)."
                : "마을 처음·기본값과 다른 시작이라 판정 기록에 '시험 판'으로 남는다.", _note);
        }

        /// <summary>룬(기획/세-무기-우클릭-소켓-1차.md 6장): 주머니에 버팀 룬 +n(0~99), 시작 무기에 버팀 룬 끼우기(기본 꺼짐).</summary>
        void DrawRunes(TestStartPreset p)
        {
            string rune = RuneTable.SuperArmor.Name;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{rune} 넣기 {p.AddRunes}", _small, GUILayout.Width(180f));
            if (GUILayout.Button("+1", _button, GUILayout.Width(60f))) Do(() => Preset.AddRunes = Mathf.Min(RuneRules.PouchCap, Preset.AddRunes + 1));
            if (GUILayout.Button("+10", _button, GUILayout.Width(70f))) Do(() => Preset.AddRunes = Mathf.Min(RuneRules.PouchCap, Preset.AddRunes + 10));
            if (GUILayout.Button("0", _button, GUILayout.Width(40f))) Do(() => Preset.AddRunes = 0);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            bool on = GUILayout.Toggle(p.StartWeaponRune, $"시작 무기에 {rune} 끼우기(전투 시험장은 시험 손잡이로 대신)");
            if (on != p.StartWeaponRune) Do(() => Preset.StartWeaponRune = on);
        }

        void AddRow(string label, int value, Action<int> set)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value}", _small, GUILayout.Width(180f));
            if (GUILayout.Button("+10", _button, GUILayout.Width(60f))) Do(() => set(Mathf.Min(TestStartPreset.MaxAdd, value + 10)));
            if (GUILayout.Button("+100", _button, GUILayout.Width(70f))) Do(() => set(Mathf.Min(TestStartPreset.MaxAdd, value + 100)));
            if (GUILayout.Button("0", _button, GUILayout.Width(40f))) Do(() => set(0));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawCombat(TestStartPreset p)
        {
            Section("⑦ 전투 시험장(시작 위치가 전투 시험장일 때)");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _combatPresets.Length; i++)
            {
                if (i > 0 && i % 6 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                string name = _combatPresets[i];
                if (Choice(p.CombatPreset == name, name, GUILayout.Width(130f)))
                    Do(() =>
                    {
                        Preset.CombatPreset = name;
                        // '보스'는 전투 시험장이 2층 장비로 맞춰 들어온다(판정 기준) — 층도 같이 맞춰 둔다(그 뒤 층을 바꿔도 됨).
                        if (name == nameof(CombatTestRoot.Preset.Boss)) Preset.CombatFloor = BossRules.TrialFloor;
                    });
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("전투 시험장 층", _small, GUILayout.Width(130f));
            if (GUILayout.Button("−", _button, GUILayout.Width(40f))) Do(() => Preset.CombatFloor = Mathf.Max(FloorScaling.MinFloor, Preset.CombatFloor - 1));
            GUILayout.Label(p.CombatFloor.ToString(), _small, GUILayout.Width(40f));
            if (GUILayout.Button("+", _button, GUILayout.Width(40f))) Do(() => Preset.CombatFloor = Mathf.Min(FloorScaling.MaxFloor, Preset.CombatFloor + 1));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// '⑧ 꾸러미 글로 시작'(저장·처음 화면·멈춤 창 1차 8-3): 높이를 고정한 여러 줄 글 칸(넘치면 칸 안에서 스크롤), 읽기 결과 한 줄,
        /// [클립보드에서 붙여 넣기] [저장 파일 글 불러오기] [이 글로 시작(마을)](읽을 수 있을 때만). 위 ①~⑦ 설정은 쓰지 않는다.
        /// </summary>
        void DrawCarryStart()
        {
            Section("⑧ 꾸러미 글로 시작(마을)");
            GUILayout.Label("저장 파일 글 전체나 꾸러미 글(carry v1·v2, 마을 F1 '꾸러미 글 복사')을 넣는다. 위 ①~⑦ 설정은 쓰지 않는다. 원정 몫(leg.*)은 지우고 마을 귀환 지점에서(방문 0이면 오프닝부터) 시작한다. 시험 메뉴 판이라 저장하지 않는다.", _note);
            _carryScroll = GUILayout.BeginScrollView(_carryScroll, GUILayout.Height(CarryTextHeight));
            _carryText = GUILayout.TextArea(_carryText ?? "", _area, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.Label(CarryLine(), _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("클립보드에서 붙여 넣기", _button)) Do(PasteCarry);
            if (GUILayout.Button("저장 파일 글 불러오기", _button)) Do(LoadSaveText);
            bool was = GUI.enabled;
            GUI.enabled = was && !_starting && _carryReady;
            if (GUILayout.Button("이 글로 시작(마을)", _button)) Do(StartWithCarry);
            GUI.enabled = was;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawActions(TestStartPreset p)
        {
            GUILayout.Space(6f);
            GUILayout.Label(Preview(p), _small);
            GUILayout.BeginHorizontal();
            bool was = GUI.enabled;
            GUI.enabled = was && !_starting;
            if (GUILayout.Button("시작", _button, GUILayout.Width(160f), GUILayout.Height(40f))) Do(StartNow);
            if (GUILayout.Button("마지막 설정으로 바로 시작", _button, GUILayout.Height(40f))) Do(StartLast);
            if (GUILayout.Button("기본값으로", _button, GUILayout.Height(40f))) Do(ResetDefaults);
            // 저장 없이 처음 화면으로(저장·처음 화면·멈춤 창 1차 8-3).
            if (GUILayout.Button("처음 화면으로", _button, GUILayout.Height(40f))) Do(LeaveToTitle);
            GUI.enabled = was;
            GUILayout.Space(16f);
            GUILayout.Label(Notice ?? "", _small);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>시작하면 무엇이 되는지 한 줄(설정 글이 바뀔 때만 다시 만든다, 시작 상태 빌더가 아직 없거나 실패하면 그 까닭).</summary>
        string Preview(TestStartPreset p)
        {
            string key;
            try
            {
                key = p.ToText();
            }
            catch (Exception e)
            {
                return "미리 보기 없음: " + e.Message;
            }
            if (key == _previewKey) return _previewLine;
            _previewKey = key;
            try
            {
                var plan = TestStartBuilder.Build(p, 0UL);
                _previewLine = plan != null ? plan.Summary() : "";
            }
            catch (Exception e)
            {
                _previewLine = "미리 보기 없음: " + e.Message;
            }
            return _previewLine;
        }

        /// <summary>화면 글을 Core에서 받는다. 아직 없거나 실패하면 대신 글.</summary>
        static string SafeLabel(Func<string> label, string fallback)
        {
            try
            {
                string s = label();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
