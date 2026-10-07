using Demo6.Core.Combat;
using Demo6.Core.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 시험 화면. HUD(체력·물약·재사용)와 시험 패널(F1)을 IMGUI로 그린다.
    /// 화면 배치는 정식 단계(uGUI)에서 다시 만든다. 여기서는 판정에 필요한 정보와 조절 손잡이만 둔다.
    /// 시험 패널은 Unity 편집기에서만 연다(키 배치 1차 0장 5, DevPanelGate). 만든 게임에서는 끈 채 시작하고 F1도 아무 일도 하지 않는다.
    /// 시험용 키는 뺐다(키 배치 1차 0장 2·4·6): 무기 종류는 패널 '무기' 단추로, 멈춤은 패널 '멈춤' 단추로만 바꾼다(숫자키·P 없음).
    /// 전투 시험장 자체가 시험 장소라 패널 손잡이를 써도 따로 '시험 판'으로 적지 않는다(던전 판정 기록과 섞이지 않음).
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        const float RefHeight = 1080f;
        const float PanelWidth = 400f;

        public static bool PointerOverPanel { get; private set; }
        /// <summary>패널이 차지하는 화면 오른쪽 비율. 카메라가 그 왼쪽에 방 전체를 담는다.</summary>
        public static float PanelScreenFraction { get; private set; }

        /// <summary>시험 패널이 보이는가. 편집기에서는 켠 채 시작하고, 만든 게임에서는 끈 채 시작해 열리지 않는다(키 배치 1차 0장 5, Awake).</summary>
        bool _panelVisible;
        Rect _panelRect;
        Vector2 _scroll;
        float _scale = 1f;
        GUIStyle _title;
        GUIStyle _small;
        GUIStyle _hudLabel;
        GUIStyle _center;
        bool _stylesReady;

        public static void ResetStatics()
        {
            PointerOverPanel = false;
            PanelScreenFraction = 0f;
        }

        int _shownStreak;
        float _streakPop;
        /// <summary>패널 구성을 바꾸는 단추의 일(Defer). 다음 Update 처음에 한 번 한다.</summary>
        System.Action _deferred;

        // F1 시험 패널은 Unity 편집기에서만(키 배치 1차 0장 5): 편집기면 바로 보이고, 만든 게임에서는 DevPanelGate가 열지 않는다.
        void Awake() => _panelVisible = DevPanelGate.OpensDirectly;

        void Update()
        {
            if (_deferred != null)
            {
                var act = _deferred;
                _deferred = null;
                act();
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame)
                {
                    if (_panelVisible) _panelVisible = false;
                    else if (DevPanelGate.RequestOpen(() => _panelVisible = true)) _panelVisible = true;
                }
                // P 멈춤 키는 뺐다(키 배치 1차 0장 4). 멈춤은 패널 '멈춤' 단추로만.
            }
            _scale = Screen.height / RefHeight;
            _panelRect = new Rect(Screen.width / _scale - PanelWidth - 12f, 12f, PanelWidth, RefHeight - 24f);
            PanelScreenFraction = _panelVisible ? Mathf.Clamp01((PanelWidth + 24f) * _scale / Mathf.Max(1, Screen.width)) : 0f;
            var mouse = Mouse.current;
            if (mouse != null && _panelVisible)
            {
                Vector2 m = mouse.position.ReadValue();
                var gui = new Vector2(m.x, Screen.height - m.y) / _scale;
                PointerOverPanel = _panelRect.Contains(gui);
            }
            else
            {
                PointerOverPanel = false;
            }
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _hudLabel = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _center = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.skin.label.fontSize = 14;
            GUI.skin.button.fontSize = 14;
            GUI.skin.toggle.fontSize = 14;
        }

        void OnGUI()
        {
            var root = CombatTestRoot.Instance;
            if (!root || !root.Player) return;
            EnsureStyles();
            GUI.depth = 0;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            DrawHud(root);
            if (_panelVisible) DrawPanel(root);
            GUI.matrix = Matrix4x4.identity;
        }

        void DrawHud(CombatTestRoot root)
        {
            var p = root.Player;
            float x = 20f;
            float y = RefHeight - 150f;
            Box(new Rect(x - 8f, y - 8f, 470f, 140f));

            GUI.Label(new Rect(x, y, 300f, 22f), $"{p.Weapon.displayName} · {root.Floor}층 기준 · 공격 {p.Attack}", _hudLabel);
            DrawCombo(new Rect(x + 300f, y, 160f, 22f), p);
            y += 26f;
            Bar(new Rect(x, y, 300f, 20f), p.Health.Fraction, new Color(0.85f, 0.82f, 0.74f), $"체력 {p.Health.Current} / {p.Health.Max}");
            GUI.Label(new Rect(x + 312f, y, 150f, 22f), p.PotionCooldown > 0f ? $"물약 {p.Potions}/{p.PotionCapacity} ({p.PotionCooldown:0.0})" : $"물약 {p.Potions}/{p.PotionCapacity} [R]");
            y += 30f;
            // 회오리는 E, 오른쪽 클릭은 무기 행동(기획/세-무기-우클릭-소켓-1차.md 5-9).
            Cooldown(new Rect(x, y, 145f, 44f), "회오리 [" + WeaponActCommon.WhirlKeyLabel + "]", p.WhirlCooldown, p.WhirlCooldownMax);
            Cooldown(new Rect(x + 152f, y, 145f, 44f), "검풍 [Q]", p.WaveCooldown, p.WaveCooldownMax);
            Cooldown(new Rect(x + 304f, y, 145f, 44f), "구르기 [Space]", p.DodgeCooldown, p.DodgeCooldownMax);
            y += 50f;
            // 패링 반격과 회피 반격이 함께 열리면 패링 반격만 쓴다(0-3의 14).
            bool parryCounter = p.ParryCounterReady;
            bool counter = parryCounter || p.CounterReady;
            GUI.Label(new Rect(x, y, 460f, 20f), parryCounter
                ? $"튕겨 냄! 다음 좌클릭은 반격 베기(피해 ×{ShieldRule.RiposteDamageScale:0.0} · 버팀 ×{ShieldRule.RipostePoiseScale:0})"
                : counter
                    ? $"회피 반격! 다음 타 버팀 ×{CounterRule.PoiseMultiplier:0} · 피해 +{CounterRule.DamageBonus * 100f:0}%"
                    : $"이동 WASD · 공격 좌클릭(누르고 있기) · 무기 행동 [{WeaponActCommon.ActKeyLabel}] · 웅크리기 C", counter ? _hudLabel : _small);
            // 웅크림 한 줄(결정 ③): 상자 바로 위.
            if (p.Crouching) GUI.Label(new Rect(x, RefHeight - 150f - 34f, 200f, 24f), Demo6.Core.Dungeon.CrouchRules.HudLabel, _hudLabel);
            // 무기 행동 한 줄(쓰는 중·끊김·난사 재사용): 웅크림 줄 위.
            string act = ActStatusLine(p);
            if (act != null) GUI.Label(new Rect(x, RefHeight - 150f - 58f, 460f, 24f), act, _hudLabel);

            DrawStreak(p);

            if (p.IsDown)
            {
                float w = Screen.width / _scale;
                GUI.Label(new Rect(0f, RefHeight * 0.4f, w, 40f), root.CurrentPreset == CombatTestRoot.Preset.Boss
                    ? "쓰러짐 — 잠시 뒤 문 쪽에서 다시 일어나고 오우거도 처음부터"
                    : "쓰러짐 — 잠시 뒤 다시 일어납니다", _center);
            }
            if (TimeScaleService.Paused)
            {
                float w = Screen.width / _scale;
                GUI.Label(new Rect(0f, RefHeight * 0.45f, w, 40f), "멈춤", _center);
            }
        }

        /// <summary>콤보 단계 점: 지난 단계는 흰색, 마무리는 테두리 굵게.</summary>
        void DrawCombo(Rect rect, PlayerController p)
        {
            var combo = p.Weapon.combo;
            int now = p.ComboStepNumber;
            float size = 14f;
            float gap = 6f;
            float x = rect.x;
            var old = GUI.color;
            for (int i = 0; i < combo.Length; i++)
            {
                bool lit = i < now;
                GUI.color = lit ? (combo[i].finisher ? Palette.NumberCrit : Color.white) : new Color(1f, 1f, 1f, 0.22f);
                float s = combo[i].finisher ? size + 4f : size;
                GUI.DrawTexture(new Rect(x, rect.y + (22f - s) * 0.5f, s, s), Texture2D.whiteTexture);
                x += s + gap;
            }
            GUI.color = old;
            if (now > 0) GUI.Label(new Rect(x + 4f, rect.y, 120f, 22f), p.ComboStepName, _small);
        }

        /// <summary>연속 처치 숫자: 3 이상이면 화면 위에 크게, 늘어날 때 튀어 오른다.</summary>
        void DrawStreak(PlayerController p)
        {
            if (!Tuning.KillStreakText) return;
            float since = Time.time - p.KillStreakTime;
            int streak = since <= 2f ? p.KillStreak : 0;
            if (streak != _shownStreak)
            {
                if (streak > _shownStreak) _streakPop = Time.unscaledTime;
                _shownStreak = streak;
            }
            if (streak < 3 && since > 3.2f) return;
            int shown = streak >= 3 ? streak : p.KillStreak;
            if (shown < 3) return;
            float fade = since <= 2f ? 1f : Mathf.Clamp01(1f - (since - 2f) / 1.2f);
            float pop = 1f + 0.35f * Mathf.Clamp01(1f - (Time.unscaledTime - _streakPop) / 0.15f);
            float w = Screen.width / _scale * (1f - PanelScreenFraction);
            var style = new GUIStyle(_center) { fontSize = Mathf.RoundToInt(40f * pop) };
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f * fade);
            GUI.Label(new Rect(3f, 63f, w, 60f), $"{shown} 처치", style);
            GUI.color = new Color(1f, 0.93f, 0.6f, fade);
            GUI.Label(new Rect(0f, 60f, w, 60f), $"{shown} 처치", style);
            GUI.color = old;
        }

        void DrawPanel(CombatTestRoot root)
        {
            // 카메라가 그리지 않는 오른쪽 영역을 매 프레임 덮어 잔상을 막는다.
            float screenW = Screen.width / _scale;
            float left = screenW * (1f - PanelScreenFraction);
            var old = GUI.color;
            GUI.color = Palette.Background;
            GUI.DrawTexture(new Rect(left, 0f, screenW - left, RefHeight), Texture2D.whiteTexture);
            GUI.color = old;
            // 멈춤 창이 열려 있으면 패널은 그리지 않는다(위 잔상 덮기만, HUD는 그대로 — 저장·처음 화면·멈춤 창 1차 5-3·8-1).
            if (PauseMenu.IsOpen) return;
            Box(_panelRect);
            GUILayout.BeginArea(new Rect(_panelRect.x + 10f, _panelRect.y + 8f, _panelRect.width - 20f, _panelRect.height - 16f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            var back = DevPanelExtras.DrawBackToLauncher(GUI.skin.button); if (back != null) Defer(back);

            GUILayout.Label("전투 시험 패널", _title);

            GUILayout.BeginHorizontal();
            // 멈춤은 이 단추로만 바꾼다(P 키는 뺌, 키 배치 1차 0장 4).
            if (GUILayout.Button(TimeScaleService.Paused ? "계속" : "멈춤", GUILayout.Width(90f))) TimeScaleService.Paused = !TimeScaleService.Paused;
            GUILayout.Label("게임 속도", GUILayout.Width(64f));
            SpeedButton(0.5f);
            SpeedButton(1f);
            SpeedButton(2f);
            GUILayout.EndHorizontal();

            Section("무기");
            // WeaponPresets.All 차례, 한 줄에 셋씩. 무기 종류는 이 단추로만 바꾼다(숫자키는 뺌, 키 배치 1차 0장 2·3).
            var weapons = WeaponPresets.All;
            for (int row = 0; row < weapons.Length; row += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < row + 3 && i < weapons.Length; i++)
                {
                    var w = weapons[i];
                    bool selected = root.Player.Weapon == w;
                    if (GUILayout.Toggle(selected, w.displayName, GUI.skin.button) && !selected)
                    {
                        string id = w.id;
                        Defer(() => root.SetTestWeapon(id));
                    }
                }
                GUILayout.EndHorizontal();
            }
            var weapon = root.Player.Weapon;
            var comboText = new System.Text.StringBuilder();
            for (int i = 0; i < weapon.combo.Length; i++)
            {
                var st = weapon.combo[i];
                if (i > 0) comboText.Append(" → ");
                comboText.Append($"{i + 1} {st.name} {(st.hits > 1 ? $"{st.hits}×{st.hitPercent:0}%" : $"{st.hitPercent:0}%")}");
            }
            GUILayout.Label($"콤보: {comboText}", _small);
            GUILayout.Label($"한 바퀴 {weapon.CycleSeconds:0.00}초 · 단일 대상 계수 {weapon.SingleTargetCoefficient:0.00} · 0.5초 멈추면 처음부터", _small);
            DrawWeaponCard(root.Player, weapon);
            DrawWeaponAct(root.Player);

            DrawGearKnobs(root);

            Section("층 기준 (몬스터 배율 + 그 층 기준 장비)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(36f))) root.SetFloor(root.Floor - 1);
            GUILayout.Label($"{root.Floor}층", _title, GUILayout.Width(56f));
            if (GUILayout.Button("▶", GUILayout.Width(36f))) root.SetFloor(root.Floor + 1);
            var b = FloorScaling.Baseline(root.Floor);
            GUILayout.Label($"공격 {b.Attack} · 체력 {b.MaxHp} · 방어 {b.Defense}", _small);
            GUILayout.EndHorizontal();
            GUILayout.Label($"굴쥐 {HpNow(MonsterKind.Rat, root.Floor)} / 돌충이 {HpNow(MonsterKind.Boar, root.Floor)} / 궁수 {HpNow(MonsterKind.Archer, root.Floor)} 체력" +
                            (FloorScaling.ArcherTripleShot(root.Floor) ? " · 궁수 3갈래" : "") + (FloorScaling.BoarDoubleCharge(root.Floor) ? " · 돌충이 2연속 돌진" : ""), _small);

            Section("규칙 (적게·강하게 비교)");
            GUILayout.BeginHorizontal();
            RulesButton(root, Demo6.Core.Combat.CombatRuleset.M0a, "M0a 값");
            RulesButton(root, Demo6.Core.Combat.CombatRuleset.V3, "3차 값");
            GUILayout.EndHorizontal();
            bool v3 = Tuning.Ruleset == Demo6.Core.Combat.CombatRuleset.V3;
            if (v3)
            {
                bool soft = GUILayout.Toggle(Tuning.SoftBoar, "돌충이 약하게(1,600·버팀 70)");
                if (soft != Tuning.SoftBoar)
                {
                    Tuning.SoftBoar = soft;
                    root.Respawn();
                }
                GUILayout.Label("3차: 돌충이 2,000·궁수 600, 버팀을 깎아 무너뜨림(무너짐 2초, 받는 피해 +30%), 마무리·검풍이 크게 깎음. 공격 기회 3/2. 회피 반격은 '살 붙이기'에", _small);
            }

            Section("마주침 (3차)");
            GUILayout.BeginHorizontal();
            PresetButton(root, CombatTestRoot.Preset.FrontBack, "앞뒤");
            PresetButton(root, CombatTestRoot.Preset.Elite, "정예");
            PresetButton(root, CombatTestRoot.Preset.Nest, "둥지");
            PresetButton(root, CombatTestRoot.Preset.Sleeping, "잠든 적");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            PresetButton(root, CombatTestRoot.Preset.Boss, "보스");
            PresetButton(root, CombatTestRoot.Preset.PillarBoar, "기둥 옆 돌충이");
            GUILayout.EndHorizontal();
            if (root.CurrentPreset == CombatTestRoot.Preset.Elite)
            {
                GUILayout.BeginHorizontal();
                bool hard = GUILayout.Toggle(root.EliteHardened, "단단한");
                bool pack = GUILayout.Toggle(root.ElitePack, "무리 거느린");
                GUILayout.EndHorizontal();
                if (hard != root.EliteHardened || pack != root.ElitePack)
                {
                    root.EliteHardened = hard;
                    root.ElitePack = pack;
                    root.Respawn();
                }
            }
            if (CombatTestRoot.IsEncounterPreset(root.CurrentPreset))
                GUILayout.Label($"정리한 마주침 {root.Spawner.EncountersCleared}번 · 모두 쓰러뜨리면 2.5초 뒤 다시 놓임" + PresetNote(root.CurrentPreset), _small);
            if (root.CurrentPreset == CombatTestRoot.Preset.Boss) DrawBoss(root);

            DrawPace(root);
            DrawCrouch(root);
            DrawExtras();
            DrawPositional(root.Player);

            Section("몬스터 구성 (M0a 비교)");
            GUILayout.BeginHorizontal();
            PresetButton(root, CombatTestRoot.Preset.RatSwarm, "굴쥐 떼");
            PresetButton(root, CombatTestRoot.Preset.M0Basic, "굴쥐8·돌충이1");
            PresetButton(root, CombatTestRoot.Preset.Mixed, "섞기 7·2·2");
            PresetButton(root, CombatTestRoot.Preset.BoarPractice, "돌충이");
            PresetButton(root, CombatTestRoot.Preset.ArcherPractice, "궁수");
            PresetButton(root, CombatTestRoot.Preset.Dummies, "허수아비");
            GUILayout.EndHorizontal();
            if (root.CurrentPreset != CombatTestRoot.Preset.Dummies && !CombatTestRoot.IsEncounterPreset(root.CurrentPreset))
            {
                CountRow(root, MonsterKind.Rat, "굴쥐");
                CountRow(root, MonsterKind.Boar, "돌충이");
                CountRow(root, MonsterKind.Archer, "궁수");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("적 모두 지우기")) root.ClearEnemies();
            if (GUILayout.Button("체력·물약 채우기")) root.Player.Refill();
            if (GUILayout.Button("통계 초기화"))
            {
                root.Stats.ResetAll();
                root.Player.ResetDowns();
                root.Crits.Reset();
                TimeScaleService.ResetHitStopRecord();
            }
            GUILayout.EndHorizontal();

            Section("그림 (교체 자리 확인)");
            GUILayout.BeginHorizontal();
            ArtButton(ArtMode.Shapes, "도형");
            bool hasProject = root.ProjectArt && root.ProjectArt.HasAnyContent;
            GUI.enabled = hasProject;
            ArtButton(ArtMode.Project, hasProject ? "연결된 그림" : "연결된 그림(비어 있음)");
            GUI.enabled = true;
            ArtButton(ArtMode.Placeholder, "시험용 그림");
            GUILayout.EndHorizontal();
            // 정수리 시점 시험판: 켜면 위 도형·그림 대신 위에서 본 임시 그림(무기만 휘두름)으로 바꿔 비교한다.
            TopDownView.Enabled = GUILayout.Toggle(TopDownView.Enabled, "정수리 시점(시험)");
            var visual = root.Player ? root.Player.GetComponent<PlayerVisual>() : null;
            if (visual && visual.DebugFrame >= 0)
            {
                string key = visual.DebugKeyFrame >= 0 ? $" · 타격 프레임 {visual.DebugKeyFrame + 1}" : "";
                bool onKey = visual.DebugKeyFrame >= 0 && visual.DebugFrame == visual.DebugKeyFrame;
                GUILayout.Label($"몸: {visual.DebugClip} {visual.DebugFrame + 1}/{visual.DebugFrameCount}{key}{(onKey ? "  ◀ 타격" : "")}", _small);
            }
            if (ArtRuntime.Mode == ArtMode.Placeholder)
                GUILayout.Label("시험용 그림은 타이밍 확인용이다. 칼날이 노란 프레임이 타격 프레임이고, 판정 순간(히트스톱)에 멈춰 보여야 한다.", _small);

            Section("시원함 보강 (켜고 끄며 비교)");
            GUILayout.BeginHorizontal();
            Tuning.KillFling = GUILayout.Toggle(Tuning.KillFling, "처치 날림");
            Tuning.HitSparks = GUILayout.Toggle(Tuning.HitSparks, "타격 파편");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Tuning.MultiKillJuice = GUILayout.Toggle(Tuning.MultiKillJuice, "다중 처치 연출");
            Tuning.KillStreakText = GUILayout.Toggle(Tuning.KillStreakText, "연속 처치 숫자");
            GUILayout.EndHorizontal();
            Tuning.KillFlingScale = Slider("날림 거리 배율", Tuning.KillFlingScale, 0f, 2f);

            Section("손맛 조절");
            Tuning.HitStopEnabled = GUILayout.Toggle(Tuning.HitStopEnabled, "히트스톱 켜기");
            Tuning.HitStopScale = Slider("히트스톱 배율", Tuning.HitStopScale, 0f, 3f);
            Tuning.KnockbackScale = Slider("넉백 배율", Tuning.KnockbackScale, 0f, 3f);
            Tuning.ShakeScale = Slider("화면 흔들림 배율", Tuning.ShakeScale, 0f, 3f);
            // 걷기·적 걷기 배율 슬라이더는 '걸음' 칸에 있다.
            Tuning.MoveInertia = GUILayout.Toggle(Tuning.MoveInertia, $"가감속(묵직함) 붙기 {PlayerController.WalkAccelTime:0.00}초 · 서기 {PlayerController.WalkDecelTime:0.00}초");
            GUILayout.BeginHorizontal();
            Tuning.DamageNumbers = GUILayout.Toggle(Tuning.DamageNumbers, "피해 숫자");
            Tuning.Invincible = GUILayout.Toggle(Tuning.Invincible, "무적");
            GUILayout.EndHorizontal();
            Tuning.SmartTargeting = GUILayout.Toggle(Tuning.SmartTargeting, "자동 조준 2차 (직전 대상 유지·점수·다가가기)");
            GUILayout.BeginHorizontal();
            Tuning.Sound = GUILayout.Toggle(Tuning.Sound, "임시 효과음", GUILayout.Width(110f));
            GUILayout.Label($"{Tuning.SoundVolume:0.00}", GUILayout.Width(40f));
            Tuning.SoundVolume = Mathf.Round(GUILayout.HorizontalSlider(Tuning.SoundVolume, 0f, 1f) * 20f) / 20f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("손맛 기본값으로"))
            {
                Tuning.ResetToDefaults();
                // 보스 패턴 강제도 '없음'으로(오우거가 따로 들고 있을 수 있다).
                var ogre = OgreBrain.Current;
                if (ogre) ogre.ForcePattern(null);
            }

            Section("통계");
            var s = root.Stats;
            GUILayout.Label($"초당 피해(최근 5초) {s.RecentDps:0} · 기본공격 가동률(30초) {s.AttackUptime * 100f:0}%");
            GUILayout.Label($"처치: 굴쥐 {s.Kills(MonsterKind.Rat)} · 돌충이 {s.Kills(MonsterKind.Boar)} · 궁수 {s.Kills(MonsterKind.Archer)} · 오우거 {s.Kills(MonsterKind.Ogre)}");
            GUILayout.Label($"받은 피해 {s.DamageTaken} ({s.HitsTaken}번) · 쓰러짐 {root.Player.Downs}번 · 최고 연속 처치 {root.Player.BestKillStreak}");
            string avoid = s.AvoidableTelegraphs > 0 ? $"{(float)s.AvoidableTelegraphsHit / s.AvoidableTelegraphs * 100f:0}%" : "-";
            GUILayout.Label($"예고 공격(돌충이·궁수): 피할 수 있었던 {s.AvoidableTelegraphs}번 중 맞음 {s.AvoidableTelegraphsHit}번 ({avoid}, 기준 40% 이하)", _small);
            GUILayout.Label($"예고 공격 전체 {s.AllTelegraphs}번 중 맞음 {s.AllTelegraphsHit}번 (굴쥐 물기는 따로 세지 않음)", _small);
            s.Shares(out float sb, out float sw, out float sv, out float sl, out float sbl, out float sen, out float crit, false);
            GUILayout.Label($"출처(30초): 기본 {sb * 100f:0}% · 회오리 {sw * 100f:0}% · 검풍 {sv * 100f:0}% · 전설 {sl * 100f:0}% · 출혈 {sbl * 100f:0}% · 환경 {sen * 100f:0}% · 치명 {crit * 100f:0}%", _small);
            GUILayout.Label($"벽 박기 {s.WallSlams} · 겁먹음 {s.Frightened} · 처형 기습 {s.AmbushExecutions} / 무너짐 {s.BreakExecutions} · " +
                            $"회피 반격 열림 {s.CountersOpened} / 적중 {s.CountersLanded} · 보스 단계 {(s.BossPhase > 0 ? s.BossPhase.ToString() : "-")} (2단계 {s.BossPhase2Entries}번)", _small);
            GUILayout.Label($"처형으로 끝난 무거운 적 {s.HeavyExecuted}/{s.HeavyKills} ({s.HeavyExecutedFraction * 100f:0}%, 기준 50~70%) · " +
                            $"히트스톱 예산에 잘린 비율 {TimeScaleService.HitStopTrimmedFraction * 100f:0.0}% (기준 10% 이하)", _small);

            DrawCritRecord(root, crit);

            if (root.CurrentPreset == CombatTestRoot.Preset.Dummies) DrawDummy(root);

            Section("마주침 기록 (3차 값, 판정 기준)");
            s.EncounterSummary(CombatStats.EncounterKind.Normal, out int normalCount, out float encTime, out float encLoss, out float early, out _);
            GUILayout.Label($"보통(앞뒤·잠든 적) {normalCount}번 · 시간 중앙 {encTime:0.0}초 (기준 12~25) · 체력 소모 중앙 {encLoss * 100f:0}% (기준 5~15) · 첫 5초 안에 돌충이가 무너진 비율 {early * 100f:0}% (기준 30% 이하)", _small);
            s.EncounterSummary(CombatStats.EncounterKind.Elite, out int eliteFights, out float eliteTime, out _, out _, out int eliteBroken);
            s.EncounterSummary(CombatStats.EncounterKind.Nest, out int nestCount, out float nestTime, out _, out _, out _);
            GUILayout.Label($"정예 {eliteFights}번 · 정예를 무너뜨린 싸움 {eliteBroken}/{eliteFights} (기준 70% 이상) · 시간 중앙 {eliteTime:0.0}초 (가안 35~40) · 둥지 {nestCount}번 · 시간 중앙 {nestTime:0.0}초 (가안 20~25)", _small);

            Section("판정 질문 (적게·강하게)");
            GUILayout.Label("4. 한 마리 한 마리가 기억에 남는가\n5. 마무리와 검풍을 아껴 쓰게 되는가, 늘 첫 타에 쓰는가\n6. 둥지가 아닌 보통 싸움이 끝날 때도 시원한가\n7. M0a 굴쥐 떼 10분과 3차 앞뒤 10분 중 다음 날 다시 켜고 싶은 쪽은?", _small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static readonly string[] LegendNames = { "연쇄 번개", "불꽃 발자국", "연쇄 폭발" };

        /// <summary>무기 카드 숫자(장비 문서 3-1): 초당 휘두르기(공격 속도 포함), 한 방 세기, 치명, 무너뜨리기, 한 번에 최대.</summary>
        void DrawWeaponCard(PlayerController p, WeaponAttackRule weapon)
        {
            int aspd = p.AttackSpeedPermille;
            var sheet = p.Sheet;
            int cc = sheet != null ? sheet.CritChancePermille : Mathf.RoundToInt(p.CritChance * 1000f);
            int cd = sheet != null ? sheet.CritDamagePermille : Mathf.RoundToInt(p.CritDamage * 1000f);
            GUILayout.Label($"무기 카드: 초당 휘두르기 {StatCalc.SwingsPerSecond(weapon, aspd):0.00}회 · 한 방 세기 {weapon.AverageHitPercent:0}% · " +
                            $"치명 {cc / 10f:0.#}% / {cd / 10f:0}% · 무너뜨리기 {weapon.PoiseWord}({weapon.PoisePerSecond:0}) · " +
                            $"한 번에 최대 {weapon.MaxTargets}(마무리 {weapon.FinisherMaxTargets})", _small);
            // 설명 한 줄과 고유 규칙 한 줄(전투·보스·무기 다듬기 1차 2-6 '방식 다름'). 기존 3종은 비어 있을 수 있다.
            bool hasFlavor = !string.IsNullOrEmpty(weapon.flavor);
            bool hasTrait = !string.IsNullOrEmpty(weapon.traitLine);
            if (hasFlavor || hasTrait)
                GUILayout.Label(hasFlavor && hasTrait ? $"\"{weapon.flavor}\" · {weapon.traitLine}" : hasFlavor ? $"\"{weapon.flavor}\"" : weapon.traitLine, _small);
            if (HasInertia(weapon))
                GUILayout.Label($"관성: {(p.InertiaActive ? "켜짐" : "꺼짐")} (마무리 뒤 {InertiaRule.Window:0.0}초 안에 다시 치면 동작 ×{InertiaRule.DurationScale:0.0}, 스킬·구르기를 쓰면 사라짐)", p.InertiaActive ? _hudLabel : _small);
        }

        // ── 세 무기·오른쪽 클릭·소켓 1차(기획/세-무기-우클릭-소켓-1차.md 5-9, IMGUI 기능만). PlayerController 읽기 값과 Tuning 손잡이만 쓴다 ──

        /// <summary>
        /// 무기 행동 칸: 행동 이름·카드 줄, 지금 단계, 방어 게이지, 마지막 막기 결과, 기 모으기 단계, 난사 판정 수, 버팀 여부와 손잡이 9개(방어 게이지 셋 포함).
        /// 줄 수는 늘 같다(IMGUI 배치 패스와 그리기 패스의 칸 수가 어긋나지 않게).
        /// </summary>
        void DrawWeaponAct(PlayerController p)
        {
            Section($"무기 행동 [{WeaponActCommon.ActKeyLabel}] · 회오리는 [{WeaponActCommon.WhirlKeyLabel}]");
            var kind = p.ActKind;
            string card = WeaponActRules.CardLine(kind);
            GUILayout.Label(card != null ? $"{WeaponActRules.Name(kind)} — {card}" : "이 무기는 우클릭 행동이 없음(나머지 6종)", _small);
            string state = p.InWeaponAct ? $"{PhaseName(p.ActPhase)} · 행동 {p.ActTime:0.00}초 · 이 단계 {p.ActPhaseTime:0.00}초" : "쉼";
            if (p.Flinching) state += $" · 끊김 경직 남음 {p.FlinchRemaining:0.00}초";
            GUILayout.Label($"지금: {state}", _small);
            GUILayout.Label($"방어 게이지 {p.GuardMeter:0} / {p.GuardMeterMax:0}{(p.GuardRecovering ? " · 깨짐 뒤 절반까지 차는 중(들 수 없음)" : "")} · " +
                            $"막기 {(p.GuardActive ? "켜짐" : "꺼짐")} · 패링 창 {(p.ParryWindowOpen ? "열림" : "닫힘")} · " +
                            $"반격 베기 {(p.ParryCounterReady ? "준비" : "-")} · 마지막 막기 결과 {ResultName(p.LastHitResult)}" +
                            (p.LastParryDamageTime > 0f ? $" · 마지막 패링 피해 {p.LastParryDamage}" : ""), _small);
            GUILayout.Label($"기 모으기 {p.ChargeLevel}단계(모은 {p.ChargeHeldTime:0.00}초, 단계 {GreatswordCharge.L1:0.00} / {GreatswordCharge.L2:0.00} / {GreatswordCharge.L3:0.00}초) · " +
                            $"놓아 베기 {(p.ReleaseLevel > 0 ? p.ReleaseLevel + "단계" : "-")} · 난사 판정 {p.ActHitsDone} / {TwinFlurry.HitCount} · " +
                            $"난사 재사용 {(p.ActCooldown > 0f ? $"{p.ActCooldown:0.0} / {p.ActCooldownMax:0}초" : "준비")}", _small);
            GUILayout.Label($"버팀 {(p.SuperArmorNow ? "켜짐(맞아도 안 끊김)" : "꺼짐")} · 버팀 룬 {(p.HasSuperArmorRune ? "있음" : "없음")}", _small);

            Tuning.GuardEnabled = GUILayout.Toggle(Tuning.GuardEnabled, "방패 막기 켜기");
            Tuning.ParryWindow = FineSlider("패링 창(초)", Tuning.ParryWindow, 0.10f, 0.30f);
            Tuning.GuardDamageScale = FineSlider("막은 일반 타 피해 ×", Tuning.GuardDamageScale, 0f, 0.5f);
            // 방어 게이지 손잡이(0-3의 28): 최대치, 회복 빠르기(쉴 때 초당 50·막는 중 초당 10에 곱함), 패링 환급.
            Tuning.GuardMeterMax = WholeSlider("방어 게이지 최대치", Tuning.GuardMeterMax, GuardGauge.KnobMaxLow, GuardGauge.KnobMaxHigh);
            Tuning.GuardRegenScale = FineSlider("게이지 회복 빠르기 ×", Tuning.GuardRegenScale, GuardGauge.KnobRegenLow, GuardGauge.KnobRegenHigh);
            Tuning.GuardParryRefund = WholeSlider("패링 환급", Tuning.GuardParryRefund, 0f, GuardGauge.KnobRefundHigh);
            // 패링 보상(2026-10-05): 피해(공격력 %), 그로기(버팀) 깎기 일반·정예 몫. 보스 휩쓸기 6%는 그대로.
            Tuning.ParryDamagePercent = WholeSlider("패링 피해 %", Tuning.ParryDamagePercent, 0f, 100f);
            Tuning.ParryPoiseFraction = FineSlider("패링 그로기 깎기(일반)", Tuning.ParryPoiseFraction, 0f, 1f);
            Tuning.ParryElitePoiseFraction = FineSlider("패링 그로기 깎기(정예)", Tuning.ParryElitePoiseFraction, 0f, 1f);
            GUILayout.BeginHorizontal();
            Tuning.TestSuperArmorRune = GUILayout.Toggle(Tuning.TestSuperArmorRune, "버팀 룬 시험");
            Tuning.SuperArmorAllActs = GUILayout.Toggle(Tuning.SuperArmorAllActs, "세 행동 모두 버팀");
            GUILayout.EndHorizontal();
            Tuning.WeaponActInterruptMinPermille = IntSlider("끊김 최소 피해 ‰", Tuning.WeaponActInterruptMinPermille, 0, 100, 5);
            GUILayout.Label("버팀 룬 시험: 낀 무기에 버팀 룬이 있는 것처럼(대검 기 모으기·놓아 베기가 맞아도 안 끊김). 세 행동 모두 버팀: 막기·난사에도 적용. " +
                            "끊김 최소 피해: 최대 체력 대비 ‰(0 = 문턱 없음, 30 = 3%). " +
                            "방어 게이지: 막은 공격 세기만큼 줄고(굴쥐 25), 깨지면 절반까지 다시 차야 든다. 최대치를 바꿔도 깎는 양은 같다.", _small);
        }

        /// <summary>HUD 상자 위 무기 행동 한 줄(쓰는 중·끊김·난사 재사용). 보일 것이 없으면 null.</summary>
        static string ActStatusLine(PlayerController p)
        {
            if (p.Flinching) return $"끊김 · 경직 {p.FlinchRemaining:0.00}초";
            if (p.InWeaponAct)
            {
                switch (p.ActKind)
                {
                    case WeaponActKind.Guard:
                        return $"{PhaseName(p.ActPhase)} · 방어 게이지 {p.GuardMeter:0}{(p.ParryWindowOpen ? " · 패링 창" : "")}";
                    case WeaponActKind.Charge:
                        return p.ActPhase == WeaponActPhase.Release
                            ? $"놓아 베기 {p.ReleaseLevel}단계"
                            : $"기 모으기 {p.ChargeLevel}단계 · {p.ChargeHeldTime:0.00}초{(p.SuperArmorNow ? " · 버팀" : "")}";
                    case WeaponActKind.Flurry:
                        return $"난사 {p.ActHitsDone} / {TwinFlurry.HitCount}";
                    default:
                        return PhaseName(p.ActPhase);
                }
            }
            if (p.ActKind == WeaponActKind.Flurry && p.ActCooldown > 0f) return $"난사 재사용 {p.ActCooldown:0.0}초";
            return null;
        }

        static string PhaseName(WeaponActPhase phase)
        {
            switch (phase)
            {
                case WeaponActPhase.Raise: return "방패 들기";
                case WeaponActPhase.Hold: return "막기";
                case WeaponActPhase.Lower: return "방패 내리기";
                case WeaponActPhase.Recoil: return "막은 반동";
                case WeaponActPhase.ParryPush: return "튕겨 내고 밀쳐 내기";
                case WeaponActPhase.Break: return "막기 깨짐";
                case WeaponActPhase.Charging: return "기 모으기";
                case WeaponActPhase.Release: return "놓아 베기";
                case WeaponActPhase.Flurry: return "난사";
                case WeaponActPhase.Flinch: return "끊김 경직";
                default: return "-";
            }
        }

        /// <summary>마지막 ReceiveHit 결과: 맞음·막음·튕김·피함.</summary>
        static string ResultName(HitResult result)
        {
            switch (result)
            {
                case HitResult.Hit: return "맞음";
                case HitResult.Blocked: return "막음";
                case HitResult.Parried: return "튕김";
                case HitResult.Avoided: return "피함";
                default: return "-";
            }
        }

        static bool HasInertia(WeaponAttackRule weapon)
        {
            foreach (var st in weapon.combo)
                if (st.inertia) return true;
            return false;
        }

        /// <summary>장비 능력치 손잡이(장비 문서 3-4, IMGUI 기능만). 값은 Tuning에 두고 CombatTestRoot가 바뀐 것을 보고 능력치를 다시 넣는다.</summary>
        void DrawGearKnobs(CombatTestRoot root)
        {
            var p = root.Player;
            var sheet = p.Sheet;
            Section("장비 능력치 손잡이 (치명·공격 속도·전설)");
            Tuning.TestWeaponIntrinsic = GUILayout.Toggle(Tuning.TestWeaponIntrinsic, "무기 고유 치명 (끄면 맨몸 5% / 150%)");
            Tuning.TestAttackSpeedPermille = IntSlider("공격 속도 ‰", Tuning.TestAttackSpeedPermille, 0, Tuning.TestAttackSpeedMax, 10);

            bool ccOn = Tuning.TestCritChancePermille >= 0;
            bool ccNow = GUILayout.Toggle(ccOn, "치명 확률 직접 정하기");
            if (ccNow != ccOn) Tuning.TestCritChancePermille = ccNow ? (sheet != null ? sheet.CritChancePermille : 50) : -1;
            if (ccNow) Tuning.TestCritChancePermille = IntSlider("치명 확률 ‰", Tuning.TestCritChancePermille, 0, StatCaps.CritChancePermille, 5);

            bool cdOn = Tuning.TestCritDamagePermille >= 0;
            bool cdNow = GUILayout.Toggle(cdOn, "치명 피해 직접 정하기");
            if (cdNow != cdOn) Tuning.TestCritDamagePermille = cdNow ? (sheet != null ? sheet.CritDamagePermille : 1500) : -1;
            if (cdNow) Tuning.TestCritDamagePermille = IntSlider("치명 피해 ‰", Tuning.TestCritDamagePermille, StatCaps.CritDamageMinPermille, StatCaps.CritDamagePermille, 10);

            for (int i = 0; i < Tuning.TestLegendOn.Length && i < LegendNames.Length; i++)
            {
                GUILayout.BeginHorizontal();
                Tuning.TestLegendOn[i] = GUILayout.Toggle(Tuning.TestLegendOn[i], $"전설 {LegendNames[i]}", GUILayout.Width(150f));
                GUILayout.Label($"세기 {Tuning.TestLegendRoll[i]}‰", GUILayout.Width(80f));
                float v = GUILayout.HorizontalSlider(Tuning.TestLegendRoll[i], 0f, 1000f);
                GUILayout.EndHorizontal();
                if (!Mathf.Approximately(v, Tuning.TestLegendRoll[i])) Tuning.TestLegendRoll[i] = Mathf.Clamp(Mathf.RoundToInt(v / 50f) * 50, 0, 1000);
            }

            if (sheet != null)
            {
                GUILayout.Label($"능력치: 공격 {sheet.Attack} · 체력 {sheet.MaxHp} · 방어 {sheet.Defense} · 치명 {sheet.CritChancePermille}‰ / {sheet.CritDamagePermille}‰ · " +
                                $"공속 {sheet.AttackSpeedPermille}‰ · 이동 {sheet.MoveSpeedPermille}‰ · 재사용 감소 {sheet.CooldownReductionPermille}‰", _small);
            }
            var plan = p.CurrentSwingPlan;
            if (plan.Duration > 0f)
                GUILayout.Label($"마지막 동작 {p.ComboStepName}: 길이 {plan.Duration:0.000}초 · 첫 판정 {plan.FirstHit:0.000}초 · 마지막 판정 {plan.LastHit:0.000}초 · 회수 {plan.Recovery:0.000}초", _small);
            GUILayout.Label($"치명 굴림 씨앗 {p.CritSeed} (판마다 다름, 기록에 함께 적기)", _small);
        }

        /// <summary>치명·손맛 기록(장비 문서 12장 판정 표).</summary>
        void DrawCritRecord(CombatTestRoot root, float critRate30)
        {
            var c = root.Crits;
            Section("치명 기록 (장비 문서 12장)");
            GUILayout.Label($"30초 치명 비율 {critRate30 * 100f:0}% · 치명 연출 가벼움 {c.Count(CritTier.Light)} · 보통 {c.Count(CritTier.Normal)} · 무거움 {c.Count(CritTier.Heavy)} (마무리 {c.FinisherHeavy})", _small);
            string gaps = c.HeavyGaps > 0
                ? $"최소 {c.HeavyGapMin:0.00}초 · 평균 {c.HeavyGapAverage:0.00}초 · 0.5초보다 짧음 {c.HeavyGapsUnderHalf}번(마무리 예외)"
                : "아직 없음";
            GUILayout.Label($"무거운 치명 사이 간격: {gaps} (기준 0.5초 이상)", _small);
            GUILayout.Label($"히트스톱이 1초 예산에 잘린 비율 {TimeScaleService.HitStopTrimmedFraction * 100f:0.0}% (요청 {TimeScaleService.HitStopRequested:0.00}초 · 허락 {TimeScaleService.HitStopGranted:0.00}초, 5% 넘으면 무거움 간격을 0.8초로)", _small);
            root.Stats.EncounterSummary(CombatStats.EncounterKind.Normal, out int count, out _, out _, out float early, out _);
            GUILayout.Label($"첫 5초 안 무너짐(보통 마주침 {count}번) {early * 100f:0}% (기준 30% 이하)", _small);
            GUILayout.Label("판정 질문: 무기를 바꾸면 치명이 다르게 느껴지나(쌍검은 자주·가볍게, 대검은 드물게·무겁게)? · 공격 속도 +24%에서 묵직함이 남았나, 뚝뚝 끊기나? · 치명이 너무 흔해 특별함이 사라졌나?", _small);
        }

        /// <summary>정수 손잡이(step 단위). 손대지 않으면 값을 바꾸지 않는다.</summary>
        static int IntSlider(string label, int value, int min, int max, int step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value}", GUILayout.Width(170f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(v, value)) return value;
            return Mathf.Clamp(Mathf.RoundToInt(v / step) * step, min, max);
        }

        void DrawDummy(CombatTestRoot root)
        {
            Section("허수아비 (나무: 체력 무한 · 쥐: 고른 층 굴쥐)");
            var p = root.Player;
            var s = root.Stats;
            float dps = s.WoodDps(out float seconds, out bool done);
            GUILayout.Label(done ? $"나무 허수아비 초당 피해 {dps:0} (30초 측정 완료, 기록 저장됨)" : $"나무 허수아비 초당 피해 {dps:0} (측정 {seconds:0}초 / 30초, 첫 타부터)");
            GUILayout.Label($"공격력 대비 {(p.Attack > 0 ? dps / p.Attack : 0f):0.00} (기획 가정 1.76)", _small);
            int ratHp = FloorScaling.MonsterHp(MonsterKind.Rat, root.Floor);
            int minSwing = int.MaxValue;
            foreach (var st in p.Weapon.combo)
            {
                int sum = 0;
                for (int i = 0; i < st.hits; i++)
                    sum += DamageMath.ToMonster(p.Attack, st.hitPercent, false, p.CritDamage, DamageMath.RollMin);
                minSwing = Mathf.Min(minSwing, sum);
            }
            GUILayout.Label($"이 무기로 {root.Floor}층 굴쥐 한 방(모든 콤보 단계): {(minSwing >= ratHp ? "예" : "아니오")} (최저 {minSwing} / 체력 {ratHp})");
            if (dps > 0f)
                GUILayout.Label($"오우거 예상 처치(가만히 선 허수아비 기준, 실전보다 짧음): 2층 시험판 {BossRules.Hp(2) / dps:0}초 (실전 기준 45~65초) · " +
                                $"5층 {BossRules.Hp(5) / dps:0}초 · 10층 {BossRules.Hp(10) / dps:0}초", _small);
            s.Shares(out float sb, out float sw, out float sv, out float sl, out float crit, true);
            GUILayout.Label($"허수아비 출처: 기본 {sb * 100f:0}% · 회오리 {sw * 100f:0}% · 검풍 {sv * 100f:0}% · 전설 {sl * 100f:0}% · 치명 {crit * 100f:0}%", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이 무기 기록 저장")) s.RecordWeapon($"{p.Weapon.displayName} {root.Floor}층", dps);
            if (GUILayout.Button("측정 다시")) s.ResetDummyWindow();
            GUILayout.EndHorizontal();
            foreach (var kv in s.WeaponRecords) GUILayout.Label($"· {kv.Key}: 초당 {kv.Value:0}", _small);
        }

        // ── 전투·보스·무기 다듬기 1차 시험 패널(기획/전투-보스-무기-다듬기-1차.md 1-2·3-10·4장, IMGUI 기능만) ──

        /// <summary>걸음 비교안 단추(M0a·지금·A·B·C: 나 / 적 걷기 배율)와 배율 슬라이더(0.01 단위), 걸음 숫자.</summary>
        void DrawPace(CombatTestRoot root)
        {
            Section("걸음 (1-2 비교안: 나 / 적 배율)");
            GUILayout.BeginHorizontal();
            foreach (var opt in Demo6.Core.Dungeon.ExplorePace.Options)
            {
                bool on = Mathf.Approximately(Tuning.MoveSpeedScale, opt.PlayerScale) && Mathf.Approximately(Tuning.EnemyMoveScale, opt.EnemyScale);
                if (GUILayout.Toggle(on, opt.Name, GUI.skin.button) && !on)
                {
                    Tuning.MoveSpeedScale = opt.PlayerScale;
                    Tuning.EnemyMoveScale = opt.EnemyScale;
                }
            }
            GUILayout.EndHorizontal();
            Tuning.MoveSpeedScale = FineSlider("걷기 배율", Tuning.MoveSpeedScale, 0.5f, 1.2f);
            Tuning.EnemyMoveScale = FineSlider("적 걷기 배율", Tuning.EnemyMoveScale, 0.7f, 1.1f);
            float walk = root.Player.WalkSpeed * Tuning.MoveSpeedScale;
            float known = Demo6.Core.Dungeon.ExplorePace.KnownPathSpeed * Tuning.MoveSpeedScale;
            float ratBase = MonsterRule.Rat.MoveSpeed;
            float rat = ratBase * Tuning.EnemyMoveScale;
            GUILayout.Label($"걸음 {walk:0.00} · 아는 길 {known:0.00} · 굴쥐 {ratBase:0.0} × {Tuning.EnemyMoveScale:0.00} = {rat:0.00} " +
                            $"(나 ÷ 굴쥐 {(rat > 0f ? walk / rat : 0f):0.000}, 기준 {Demo6.Core.Dungeon.ExplorePace.MinPlayerOverRat:0.00} 이상) · 웅크림 {walk * Tuning.CrouchMoveScale:0.00}", _small);
            GUILayout.Label("시험장에서는 '지금'과 A가 같다(A는 던전 처음 칸의 빠른 걸음만 없앰). 아는 길은 던전에서 다시 들어간 칸에서만 쓴다. 돌진·뒤로 뛰기·화살·구르기는 배율을 받지 않는다.", _small);
        }

        /// <summary>웅크렸을 때 그 종류가 등 뒤 소리로 알아채는 중심 거리(몸 사이 거리 × 소음 배율, Enemy.DetectsPlayer와 같은 식).</summary>
        static float CrouchHear(MonsterKind kind) =>
            Demo6.Core.Dungeon.CrouchRules.HearCenterDistance(3f, MonsterRule.Of(kind, CombatRuleset.V3).Diameter * 0.5f + PlayerController.Radius, Tuning.CrouchNoiseScale);

        /// <summary>웅크리기(결정 ③, 키 C) 상태와 손잡이. 시야·몸 둘레는 던전 시야만 쓴다.</summary>
        void DrawCrouch(CombatTestRoot root)
        {
            Section("웅크리기 (C)");
            var p = root.Player;
            GUILayout.Label(p.Crouching
                ? $"지금 {Demo6.Core.Dungeon.CrouchRules.HudLabel}: 걸음 ×{Tuning.CrouchMoveScale:0.00} · 소음 ×{Tuning.CrouchNoiseScale:0.00} (잠든 적 등 뒤 감지 3 → 굴쥐 {CrouchHear(MonsterKind.Rat):0.00} · 돌충이 {CrouchHear(MonsterKind.Boar):0.00})"
                : "서 있음 · C로 웅크림(구르기·공격·스킬을 쓰면 일어섬)", p.Crouching ? _hudLabel : _small);
            Tuning.CrouchMoveScale = FineSlider("웅크림 걸음 배율", Tuning.CrouchMoveScale, 0.3f, 1f);
            Tuning.CrouchNoiseScale = FineSlider("웅크림 소음 배율", Tuning.CrouchNoiseScale, 0f, 1f);
            Tuning.CrouchConeHalfAngle = WholeSlider("웅크림 시야 반각°", Tuning.CrouchConeHalfAngle, 20f, 65f);
            Tuning.CrouchViewRadius = FineSlider("웅크림 시야 반경", Tuning.CrouchViewRadius, 6f, 14f);
            Tuning.CrouchNearRadius = FineSlider("웅크림 몸 둘레", Tuning.CrouchNearRadius, 1f, 2.5f);
            GUILayout.Label("시야 반각·반경·몸 둘레는 던전 시야에서만 쓴다(시험장에는 시야가 없음).", _small);
        }

        /// <summary>살 붙이기 켜고 끄기(4-3 '무너짐 공급원은 하나씩 켠다')와 처형 문턱·뒤척임.</summary>
        void DrawExtras()
        {
            Section("살 붙이기 (무너짐 공급원은 하나씩 켜기)");
            GUILayout.BeginHorizontal();
            Tuning.WallSlamOn = GUILayout.Toggle(Tuning.WallSlamOn, "벽·기둥 박기");
            Tuning.FearOn = GUILayout.Toggle(Tuning.FearOn, "무리 공포");
            Tuning.DodgeCounter = GUILayout.Toggle(Tuning.DodgeCounter, "회피 반격");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Tuning.ExecutionOn = GUILayout.Toggle(Tuning.ExecutionOn, "무너짐 처형");
            Tuning.AmbushExecutionOn = GUILayout.Toggle(Tuning.AmbushExecutionOn, "기습 처형(웅크려 등 뒤 첫 타)");
            GUILayout.EndHorizontal();
            Tuning.ExecutionHealOn = GUILayout.Toggle(Tuning.ExecutionHealOn, $"처형하면 체력 {ExecutionRule.HealFraction * 100f:0}% 회복");
            Tuning.ExecuteThresholdMedium = PercentSlider("처형 문턱 보통(궁수)", Tuning.ExecuteThresholdMedium, 0f, 0.5f);
            Tuning.ExecuteThresholdHeavy = PercentSlider("처형 문턱 무거움(돌충이)", Tuning.ExecuteThresholdHeavy, 0f, 0.5f);
            Tuning.ExecuteThresholdElite = PercentSlider("처형 문턱 정예", Tuning.ExecuteThresholdElite, 0f, 0.5f);
            Tuning.SleeperTurnChance = PercentSlider("잠든 무리 뒤척임", Tuning.SleeperTurnChance, 0f, 1f);
            GUILayout.Label($"회피 반격: 예고를 구르기로 받아 내면 {CounterRule.Window:0.0}초 안 첫 타 버팀 ×{CounterRule.PoiseMultiplier:0} · 피해 +{CounterRule.DamageBonus * 100f:0}%. " +
                            $"뒤척임은 {ExecutionRule.SleeperTurnMinSeconds:0}~{ExecutionRule.SleeperTurnMaxSeconds:0}초마다 이 확률로 몸을 돌림('숨어 다니기만' 보이면 30%)", _small);
        }

        /// <summary>백어택·헤드어택(PositionalHitRule) 켜고 끄기와 배율, 이 판에 맞힌 횟수, 정면 압박이 빨라지는 정도(보스 무너뜨리기 창 확인용).</summary>
        void DrawPositional(PlayerController p)
        {
            Section("백어택 · 헤드어택");
            GUILayout.BeginHorizontal();
            Tuning.BackAttackOn = GUILayout.Toggle(Tuning.BackAttackOn, $"백어택(등 뒤 ±{PositionalHitRule.BackHalfAngle:0}°)");
            Tuning.HeadAttackOn = GUILayout.Toggle(Tuning.HeadAttackOn, $"헤드어택(정면 ±{PositionalHitRule.FrontHalfAngle:0}° 치명)");
            GUILayout.EndHorizontal();
            // 두 칸 옆에 붙이면 패널 오른쪽 끝에 잘려 '글' 일부만 보여서 줄을 나눈다.
            Tuning.PositionalHitText = GUILayout.Toggle(Tuning.PositionalHitText, "위치 글자 띄우기('백어택'·'헤드어택')");
            Tuning.BackAttackDamagePermille = IntSlider("백어택 피해 +‰", Tuning.BackAttackDamagePermille, 0, 500, 10);
            Tuning.BackAttackCritPermille = IntSlider("백어택 치명 +‰", Tuning.BackAttackCritPermille, 0, 300, 10);
            Tuning.HeadAttackPoiseScale = FineSlider("헤드어택 버팀 ×", Tuning.HeadAttackPoiseScale, 1f, 3f);
            int critPermille = p ? Mathf.RoundToInt(p.CritChance * 1000f) : 0;
            double gain = PositionalHitRule.ExpectedFrontPoiseGain(critPermille, Tuning.HeadAttackPoiseScale);
            string hits = p ? $"이 판 백어택 {p.BackAttackHits}번 · 헤드어택 {p.HeadAttackHits}번 · " : "";
            GUILayout.Label($"{hits}지금 치명 {critPermille / 10f:0.#}%면 정면 버팀 깎기 기대 +{(gain - 1.0) * 100.0:0}%. " +
                            $"단검 등 찌르기(+{BackstabRule.CritBonusPermille / 10}%)와 백어택 치명은 큰 쪽 하나. 둥지·허수아비는 듣지 않음, 보스는 둘 다 들음", _small);
        }

        static string PresetNote(CombatTestRoot.Preset preset)
        {
            switch (preset)
            {
                case CombatTestRoot.Preset.Sleeping: return " · 뒤에서 몰래 먼저 치면 기습(버팀 ×3)";
                case CombatTestRoot.Preset.PillarBoar: return " · 돌충이를 기둥 쪽으로 밀어 박기(대검 내려 쪼개기·한손검 마무리 베기·검풍은 됨, 한손검 되베기·쌍검 가위 가르기는 안 됨)";
                case CombatTestRoot.Preset.Boss: return " · 쓰러졌다 일어나면 오우거를 처음부터 다시 놓음";
                default: return "";
            }
        }

        static readonly int[] BossFloors = { 2, 5, 10 };

        /// <summary>보스 시험 칸(3-10): 상태 한 줄, 패턴 강제, 체력 50%로, 2단계 켜기, 재도전 물약, 층별 값(2·5·10층), 판 기록.</summary>
        void DrawBoss(CombatTestRoot root)
        {
            Section("보스 (갱도 오우거)");
            var ogre = OgreBrain.Current;
            bool alive = ogre && !ogre.Dead;
            if (alive)
            {
                string pattern = ogre.CurrentPattern.HasValue ? PatternName(ogre.CurrentPattern.Value) : "없음";
                string poise = ogre.Poise != null ? $"{ogre.Poise.Current:0}/{ogre.Poise.Max:0}" : "-";
                string broken = ogre.Broken ? $" · 무너짐 {ogre.BreakRemaining:0.0}초" : "";
                GUILayout.Label($"{ogre.StateLabel} · {ogre.Phase}단계 · 체력 {ogre.Health.Current:N0}/{ogre.Health.Max:N0} · 버팀 {poise}{broken} · 패턴 {pattern}", _small);
            }
            else
            {
                GUILayout.Label("오우거 없음(쓰러져 다시 놓이는 중이거나 보스 두뇌가 아직 없음)", _small);
            }

            GUILayout.Label("패턴 강제(다음 고르기부터)", _small);
            GUILayout.BeginHorizontal();
            ForceButton(null, "없음");
            ForceButton(BossPattern.Slam, "A 내려찍기");
            ForceButton(BossPattern.Charge, "B 돌진");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ForceButton(BossPattern.Roar, "C 포효·낙석");
            ForceButton(BossPattern.Sweep, "D 휩쓸기");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = alive;
            if (GUILayout.Button("체력 50%로") && alive) ogre.SetHpFraction(BossRules.PhaseThreshold);
            GUI.enabled = true;
            Tuning.BossPhase2On = GUILayout.Toggle(Tuning.BossPhase2On, "2단계 켜기");
            GUILayout.EndHorizontal();
            Tuning.BossRetryFullPotions = GUILayout.Toggle(Tuning.BossRetryFullPotions, "재도전 때 체력·물약 가득 (끄면 남은 물약 그대로)");
            GUILayout.Label($"재도전 {root.BossRetries}번 · 쓰러졌다 일어나면 문 쪽에서 다시 서고 오우거는 처음부터(다시 먹는 중)", _small);

            int floor = root.Floor;
            GUILayout.Label($"지금 {floor}층 오우거: 체력 {BossRules.Hp(floor):N0} · 공격 {BossRules.Attack(floor)} · 버팀 {BossRules.Poise(floor):0} · 이름표 Lv {BossRules.PlateLevel(floor)} (시험판은 2층)", _small);
            GUILayout.BeginHorizontal();
            GUILayout.Label("층 바꿔 싸우기", GUILayout.Width(110f));
            foreach (int f in BossFloors)
            {
                bool on = floor == f;
                if (GUILayout.Toggle(on, $"{f}층", GUI.skin.button) && !on) root.SetFloor(f);
            }
            GUILayout.EndHorizontal();
            var values = new System.Text.StringBuilder();
            foreach (int f in BossFloors)
            {
                if (values.Length > 0) values.Append(" / ");
                values.Append($"{f}층 {BossRules.Hp(f):N0} · {BossRules.Attack(f)} · {BossRules.Poise(f):0}");
            }
            GUILayout.Label($"체력 · 공격 · 버팀: {values}", _small);

            string live = BossFightLog.Live;
            if (!string.IsNullOrEmpty(live)) GUILayout.Label($"지금 판: {live}", _small);
            var lines = BossFightLog.Lines;
            if (lines != null)
                for (int i = Mathf.Max(0, lines.Count - 6); i < lines.Count; i++)
                    GUILayout.Label("· " + lines[i], _small);
            root.Stats.EncounterSummary(CombatStats.EncounterKind.Boss, out int fights, out float medianTime, out float medianLoss, out _, out _);
            GUILayout.Label($"보스 처치 {fights}번 · 시간 중앙 {medianTime:0.0}초 (기준 45~65) · 체력 소모 중앙 {medianLoss * 100f:0}%", _small);
            GUILayout.Label("판정 질문: 한 마리와 춤추는 느낌인가? · 마지막 돌진을 기둥에 박았을 때 '내가 해냈다'가 왔나? · 2단계가 무섭고 읽히는가, 지저분한가?", _small);
        }

        static void ForceButton(BossPattern? pattern, string label)
        {
            int code = pattern.HasValue ? (int)pattern.Value : -1;
            bool on = Tuning.BossForcePattern == code;
            if (!GUILayout.Toggle(on, label, GUI.skin.button) || on) return;
            Tuning.BossForcePattern = code;
            var ogre = OgreBrain.Current;
            if (ogre) ogre.ForcePattern(pattern);
        }

        static string PatternName(BossPattern pattern)
        {
            switch (pattern)
            {
                case BossPattern.Slam: return "A 내려찍기";
                case BossPattern.Charge: return "B 돌진";
                case BossPattern.Sweep: return "D 휩쓸기";
                default: return "C 포효·낙석";
            }
        }

        /// <summary>0.01 단위 손잡이. 손대지 않으면 값을 바꾸지 않는다(0.05 단위 Slider는 0.86 같은 기본값을 저절로 바꾼다).</summary>
        static float FineSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:0.00}", GUILayout.Width(170f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return v != value ? Mathf.Round(v * 100f) / 100f : value;
        }

        /// <summary>1 단위 손잡이(각도). 손대지 않으면 값을 바꾸지 않는다.</summary>
        static float WholeSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:0}", GUILayout.Width(170f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return v != value ? Mathf.Round(v) : value;
        }

        /// <summary>비율(0~1)을 %로 보이는 1% 단위 손잡이. 손대지 않으면 값을 바꾸지 않는다.</summary>
        static float PercentSlider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value * 100f:0}%", GUILayout.Width(170f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return v != value ? Mathf.Round(v * 100f) / 100f : value;
        }

        void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, _hudLabel);
        }

        /// <summary>지금 규칙(M0a / 3차, 멧돼지 약하게)의 그 층 체력.</summary>
        static int HpNow(MonsterKind kind, int floor) => FloorScaling.MonsterHp(MonsterRule.Of(kind, Tuning.Ruleset, Tuning.SoftBoar), floor);

        void RulesButton(CombatTestRoot root, Demo6.Core.Combat.CombatRuleset rules, string label)
        {
            bool on = Tuning.Ruleset == rules;
            if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) Defer(() => root.SetRuleset(rules));
        }

        void ArtButton(ArtMode mode, string label)
        {
            bool on = ArtRuntime.Mode == mode;
            if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) Defer(() => ArtRuntime.Mode = mode);
        }

        static void SpeedButton(float speed)
        {
            bool on = Mathf.Approximately(TimeScaleService.GameSpeed, speed);
            if (GUILayout.Toggle(on, $"×{speed}", GUI.skin.button, GUILayout.Width(52f)) && !on) TimeScaleService.GameSpeed = speed;
        }

        void PresetButton(CombatTestRoot root, CombatTestRoot.Preset preset, string label)
        {
            bool on = root.CurrentPreset == preset;
            if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) Defer(() => root.ApplyPreset(preset));
        }

        /// <summary>
        /// 패널 구성(보스 칸·정예 체크·무기 설명 줄)을 바꾸는 단추는 다음 Update에서 처리한다.
        /// 그리는 도중에 바꾸면 IMGUI 배치 패스와 다음 패스의 칸 수가 어긋나 오류가 난다.
        /// </summary>
        void Defer(System.Action action) => _deferred += action;

        static void CountRow(CombatTestRoot root, MonsterKind kind, string label)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {root.Spawner.Target(kind)}마리 (지금 {root.Spawner.Alive(kind)})", GUILayout.Width(200f));
            if (GUILayout.Button("−", GUILayout.Width(36f))) root.ChangeCount(kind, -1);
            if (GUILayout.Button("+", GUILayout.Width(36f))) root.ChangeCount(kind, 1);
            GUILayout.EndHorizontal();
        }

        static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {value:0.00}", GUILayout.Width(170f));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return Mathf.Round(value * 20f) / 20f;
        }

        static void Box(Rect rect)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Bar(Rect rect, float fraction, Color color, string label)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width, rect.height), label);
            GUI.color = old;
        }

        void Cooldown(Rect rect, string label, float remaining, float max)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            if (remaining > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(remaining / max), rect.height), Texture2D.whiteTexture);
            }
            GUI.color = old;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 8f, 20f), label, _small);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 20f, rect.width - 8f, 22f), remaining > 0f ? $"{remaining:0.0}초" : "준비", _hudLabel);
        }
    }
}
