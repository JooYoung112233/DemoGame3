using Demo6.Core.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 전투 시험 화면. HUD(체력·물약·재사용)와 시험 패널(F1)을 IMGUI로 그린다.
    /// 화면 배치는 정식 단계(uGUI)에서 다시 만든다. 여기서는 판정에 필요한 정보와 조절 손잡이만 둔다.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        const float RefHeight = 1080f;
        const float PanelWidth = 400f;

        public static bool PointerOverPanel { get; private set; }
        /// <summary>패널이 차지하는 화면 오른쪽 비율. 카메라가 그 왼쪽에 방 전체를 담는다.</summary>
        public static float PanelScreenFraction { get; private set; }

        bool _panelVisible = true;
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

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) _panelVisible = !_panelVisible;
                if (kb.pKey.wasPressedThisFrame) TimeScaleService.Paused = !TimeScaleService.Paused;
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
            GUI.Label(new Rect(x + 312f, y, 150f, 22f), p.PotionCooldown > 0f ? $"물약 {p.Potions}/3 ({p.PotionCooldown:0.0})" : $"물약 {p.Potions}/3 [R]");
            y += 30f;
            Cooldown(new Rect(x, y, 145f, 44f), "회오리 [우클릭]", p.WhirlCooldown, p.WhirlCooldownMax);
            Cooldown(new Rect(x + 152f, y, 145f, 44f), "검풍 [Q]", p.WaveCooldown, p.WaveCooldownMax);
            Cooldown(new Rect(x + 304f, y, 145f, 44f), "구르기 [Space]", p.DodgeCooldown, p.DodgeCooldownMax);
            y += 50f;
            GUI.Label(new Rect(x, y, 460f, 20f), p.CounterReady ? "회피 반격! 다음 타 버팀 ×2" : "이동 WASD · 공격 좌클릭(누르고 있기) · 무기 1/2/3 · 패널 F1 · 멈춤 P", p.CounterReady ? _hudLabel : _small);

            DrawStreak(p);

            if (p.IsDown)
            {
                float w = Screen.width / _scale;
                GUI.Label(new Rect(0f, RefHeight * 0.4f, w, 40f), "쓰러짐 — 잠시 뒤 다시 일어납니다", _center);
            }
            if (TimeScaleService.Paused)
            {
                float w = Screen.width / _scale;
                GUI.Label(new Rect(0f, RefHeight * 0.45f, w, 40f), "멈춤 (P)", _center);
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
            Box(_panelRect);
            GUILayout.BeginArea(new Rect(_panelRect.x + 10f, _panelRect.y + 8f, _panelRect.width - 20f, _panelRect.height - 16f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("전투 시험 패널 (F1)", _title);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(TimeScaleService.Paused ? "계속 (P)" : "멈춤 (P)", GUILayout.Width(90f))) TimeScaleService.Paused = !TimeScaleService.Paused;
            GUILayout.Label("게임 속도", GUILayout.Width(64f));
            SpeedButton(0.5f);
            SpeedButton(1f);
            SpeedButton(2f);
            GUILayout.EndHorizontal();

            Section("무기 (1·2·3)");
            GUILayout.BeginHorizontal();
            foreach (var w in WeaponPresets.All)
            {
                bool selected = root.Player.Weapon == w;
                if (GUILayout.Toggle(selected, $"{w.displayName}", GUI.skin.button) && !selected) root.Player.SetWeapon(w);
            }
            GUILayout.EndHorizontal();
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

            Section("층 기준 (몬스터 배율 + 그 층 기준 장비)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(36f))) root.SetFloor(root.Floor - 1);
            GUILayout.Label($"{root.Floor}층", _title, GUILayout.Width(56f));
            if (GUILayout.Button("▶", GUILayout.Width(36f))) root.SetFloor(root.Floor + 1);
            var b = FloorScaling.Baseline(root.Floor);
            GUILayout.Label($"공격 {b.Attack} · 체력 {b.MaxHp} · 방어 {b.Defense}", _small);
            GUILayout.EndHorizontal();
            GUILayout.Label($"굴쥐 {HpNow(MonsterKind.Rat, root.Floor)} / 멧돼지 {HpNow(MonsterKind.Boar, root.Floor)} / 궁수 {HpNow(MonsterKind.Archer, root.Floor)} 체력" +
                            (FloorScaling.ArcherTripleShot(root.Floor) ? " · 궁수 3갈래" : "") + (FloorScaling.BoarDoubleCharge(root.Floor) ? " · 멧돼지 2연속 돌진" : ""), _small);

            Section("규칙 (적게·강하게 비교)");
            GUILayout.BeginHorizontal();
            RulesButton(root, Demo6.Core.Combat.CombatRuleset.M0a, "M0a 값");
            RulesButton(root, Demo6.Core.Combat.CombatRuleset.V3, "3차 값");
            GUILayout.EndHorizontal();
            bool v3 = Tuning.Ruleset == Demo6.Core.Combat.CombatRuleset.V3;
            if (v3)
            {
                GUILayout.BeginHorizontal();
                Tuning.DodgeCounter = GUILayout.Toggle(Tuning.DodgeCounter, "회피 반격");
                bool soft = GUILayout.Toggle(Tuning.SoftBoar, "멧돼지 약하게(1,600·버팀 70)");
                if (soft != Tuning.SoftBoar)
                {
                    Tuning.SoftBoar = soft;
                    root.Respawn();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("3차: 멧돼지 2,000·궁수 600, 버팀을 깎아 무너뜨림(무너짐 2초, 받는 피해 +30%), 마무리·검풍이 크게 깎음. 공격 기회 3/2", _small);
            }

            Section("마주침 (3차)");
            GUILayout.BeginHorizontal();
            PresetButton(root, CombatTestRoot.Preset.FrontBack, "앞뒤");
            PresetButton(root, CombatTestRoot.Preset.Elite, "정예");
            PresetButton(root, CombatTestRoot.Preset.Nest, "둥지");
            PresetButton(root, CombatTestRoot.Preset.Sleeping, "잠든 적");
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
                GUILayout.Label($"정리한 마주침 {root.Spawner.EncountersCleared}번 · 모두 쓰러뜨리면 2.5초 뒤 다시 놓임" + (root.CurrentPreset == CombatTestRoot.Preset.Sleeping ? " · 뒤에서 몰래 먼저 치면 기습(버팀 ×3)" : ""), _small);

            Section("몬스터 구성 (M0a 비교)");
            GUILayout.BeginHorizontal();
            PresetButton(root, CombatTestRoot.Preset.RatSwarm, "굴쥐 떼");
            PresetButton(root, CombatTestRoot.Preset.M0Basic, "굴쥐8·멧1");
            PresetButton(root, CombatTestRoot.Preset.Mixed, "섞기 7·2·2");
            PresetButton(root, CombatTestRoot.Preset.BoarPractice, "멧돼지");
            PresetButton(root, CombatTestRoot.Preset.ArcherPractice, "궁수");
            PresetButton(root, CombatTestRoot.Preset.Dummies, "허수아비");
            GUILayout.EndHorizontal();
            if (root.CurrentPreset != CombatTestRoot.Preset.Dummies && !CombatTestRoot.IsEncounterPreset(root.CurrentPreset))
            {
                CountRow(root, MonsterKind.Rat, "굴쥐");
                CountRow(root, MonsterKind.Boar, "멧돼지");
                CountRow(root, MonsterKind.Archer, "궁수");
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("적 모두 지우기")) root.ClearEnemies();
            if (GUILayout.Button("체력·물약 채우기")) root.Player.Refill();
            if (GUILayout.Button("통계 초기화"))
            {
                root.Stats.ResetAll();
                root.Player.ResetDowns();
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
            if (GUILayout.Button("손맛 기본값으로")) Tuning.ResetToDefaults();

            Section("통계");
            var s = root.Stats;
            GUILayout.Label($"초당 피해(최근 5초) {s.RecentDps:0} · 기본공격 가동률(30초) {s.AttackUptime * 100f:0}%");
            GUILayout.Label($"처치: 굴쥐 {s.Kills(MonsterKind.Rat)} · 멧돼지 {s.Kills(MonsterKind.Boar)} · 궁수 {s.Kills(MonsterKind.Archer)}");
            GUILayout.Label($"받은 피해 {s.DamageTaken} ({s.HitsTaken}번) · 쓰러짐 {root.Player.Downs}번 · 최고 연속 처치 {root.Player.BestKillStreak}");
            string avoid = s.AvoidableTelegraphs > 0 ? $"{(float)s.AvoidableTelegraphsHit / s.AvoidableTelegraphs * 100f:0}%" : "-";
            GUILayout.Label($"예고 공격(멧돼지·궁수): 피할 수 있었던 {s.AvoidableTelegraphs}번 중 맞음 {s.AvoidableTelegraphsHit}번 ({avoid}, 기준 40% 이하)", _small);
            GUILayout.Label($"예고 공격 전체 {s.AllTelegraphs}번 중 맞음 {s.AllTelegraphsHit}번 (굴쥐 물기는 따로 세지 않음)", _small);
            s.Shares(out float sb, out float sw, out float sv, out float crit, false);
            GUILayout.Label($"출처(30초): 기본 {sb * 100f:0}% · 회오리 {sw * 100f:0}% · 검풍 {sv * 100f:0}% · 치명 {crit * 100f:0}%", _small);

            if (root.CurrentPreset == CombatTestRoot.Preset.Dummies) DrawDummy(root);

            Section("마주침 기록 (3차 값, 판정 기준)");
            s.EncounterSummary(CombatStats.EncounterKind.Normal, out int normalCount, out float encTime, out float encLoss, out float early, out _);
            GUILayout.Label($"보통(앞뒤·잠든 적) {normalCount}번 · 시간 중앙 {encTime:0.0}초 (기준 12~25) · 체력 소모 중앙 {encLoss * 100f:0}% (기준 5~15) · 첫 5초 안에 멧돼지가 무너진 비율 {early * 100f:0}% (기준 30% 이하)", _small);
            s.EncounterSummary(CombatStats.EncounterKind.Elite, out int eliteFights, out float eliteTime, out _, out _, out int eliteBroken);
            s.EncounterSummary(CombatStats.EncounterKind.Nest, out int nestCount, out float nestTime, out _, out _, out _);
            GUILayout.Label($"정예 {eliteFights}번 · 정예를 무너뜨린 싸움 {eliteBroken}/{eliteFights} (기준 70% 이상) · 시간 중앙 {eliteTime:0.0}초 (가안 35~40) · 둥지 {nestCount}번 · 시간 중앙 {nestTime:0.0}초 (가안 20~25)", _small);

            Section("판정 질문 (적게·강하게)");
            GUILayout.Label("4. 한 마리 한 마리가 기억에 남는가\n5. 마무리와 검풍을 아껴 쓰게 되는가, 늘 첫 타에 쓰는가\n6. 둥지가 아닌 보통 싸움이 끝날 때도 시원한가\n7. M0a 굴쥐 떼 10분과 3차 앞뒤 10분 중 다음 날 다시 켜고 싶은 쪽은?", _small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
                GUILayout.Label($"오우거 예상 처치: 5층 {25754f / dps:0}초 (기준 28초) · 10층 {154706f / dps:0}초 (기준 60초)", _small);
            s.Shares(out float sb, out float sw, out float sv, out float crit, true);
            GUILayout.Label($"허수아비 출처: 기본 {sb * 100f:0}% · 회오리 {sw * 100f:0}% · 검풍 {sv * 100f:0}% · 치명 {crit * 100f:0}%", _small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이 무기 기록 저장")) s.RecordWeapon($"{p.Weapon.displayName} {root.Floor}층", dps);
            if (GUILayout.Button("측정 다시")) s.ResetDummyWindow();
            GUILayout.EndHorizontal();
            foreach (var kv in s.WeaponRecords) GUILayout.Label($"· {kv.Key}: 초당 {kv.Value:0}", _small);
        }

        void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, _hudLabel);
        }

        /// <summary>지금 규칙(M0a / 3차, 멧돼지 약하게)의 그 층 체력.</summary>
        static int HpNow(MonsterKind kind, int floor) => FloorScaling.MonsterHp(MonsterRule.Of(kind, Tuning.Ruleset, Tuning.SoftBoar), floor);

        static void RulesButton(CombatTestRoot root, Demo6.Core.Combat.CombatRuleset rules, string label)
        {
            bool on = Tuning.Ruleset == rules;
            if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) root.SetRuleset(rules);
        }

        static void ArtButton(ArtMode mode, string label)
        {
            bool on = ArtRuntime.Mode == mode;
            if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) ArtRuntime.Mode = mode;
        }

        static void SpeedButton(float speed)
        {
            bool on = Mathf.Approximately(TimeScaleService.GameSpeed, speed);
            if (GUILayout.Toggle(on, $"×{speed}", GUI.skin.button, GUILayout.Width(52f)) && !on) TimeScaleService.GameSpeed = speed;
        }

        static void PresetButton(CombatTestRoot root, CombatTestRoot.Preset preset, string label)
        {
            bool on = root.CurrentPreset == preset;
            if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) root.ApplyPreset(preset);
        }

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
