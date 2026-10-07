using Demo6.Core.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 디아블로식 대상 이름표(한 마리 RPG 요소): 커서 아래 적(커서 월드 위치에서 적 반지름 + 0.4 안)을 먼저, 없으면 2.5초 안에 플레이어가 때린 적을
    /// 화면 위 가운데(전투 시험장 오른쪽 패널·던전 F1 패널을 뺀 왼쪽 영역 기준)에 보인다. 두 시험장에 붙는다.
    /// 보이는 것: 이름(정예는 접두사 + "정예 " + 이름, 밝은 뼈색 + 테두리, 등급색 금지), 레벨(일반 = 층, 정예 = 층 + 1), 체력 막대(숫자 + 깎인 몫이 잠깐 남는 꼬리),
    /// 버팀이 있으면 얇은 버팀 막대(무너짐이면 '무너짐!'), 특성 한 줄(무게, 잠듦/먹는 중/순찰 중/깨어남).
    /// 시야 밖 적(VisionHidden이고 VisionInSight가 아님)과 허수아비는 고르지 않는다. 대상이 사라지면 0.35초에 걸쳐 흐려진다.
    /// 보스(기획/전투-보스-무기-다듬기-1차.md 3-7): 깨어 있는 보스는 커서·최근 대상보다 먼저 고정해 보인다. 이름 줄 '갱도 오우거  Lv 3'(BossRules.PlateLevel),
    /// 체력 막대 50% 자리 흰 눈금, 버팀 막대는 늘, '무너짐!'과 3.0초 줄어드는 막대, 특성 줄 '보스 · 넉백 없음'(2단계 '· 성남').
    /// 판마다 처음 깰 때 이름이 1.5초 크게 떴다 흐려지고 체력 막대가 0.8초에 걸쳐 0에서 100%로 찬다. 배치는 Unity 개발 단계에서 다시 잡는다.
    /// 수치·판정은 읽기만 한다. 글은 대상이나 값이 바뀔 때만 다시 만든다(매 프레임 할당 없음). 정식 화면은 Unity 개발 단계(uGUI)에서 다시 만든다.
    /// </summary>
    public sealed class TargetPlate : MonoBehaviour
    {
        const float RefHeight = 1080f;
        /// <summary>커서가 적 반지름보다 이만큼 더 떨어져도 그 적을 고른다(기획 요구 0.4).</summary>
        const float HoverPad = 0.4f;
        /// <summary>커서 아래 적이 없을 때 방금 때린 적을 보이는 시간(게임 시간).</summary>
        const float RecentHitSeconds = 2.5f;
        const float FadeOutSeconds = 0.35f;
        /// <summary>체력이 깎이면 깎인 몫(꼬리)이 이만큼 머문 뒤 줄어든다.</summary>
        const float LagHold = 0.35f;
        const float LagDrainPerSecond = 0.9f;
        const float PlateWidth = 440f;
        const float TopMargin = 12f;
        /// <summary>전투 시험장의 연속 처치 글자(CombatHud, 기준 y 60~120)가 떠 있으면 그 아래로 비킨다.</summary>
        const float BelowStreakTop = 126f;
        const int GuiDepth = 4;
        /// <summary>보스 첫 깸 연출: 큰 이름 1.5초(마지막 0.5초에 흐려짐), 체력 막대 0.8초에 0 → 100%.</summary>
        const float BossIntroName = 1.5f;
        const float BossIntroNameFade = 0.5f;
        const float BossIntroFill = 0.8f;

        static readonly Color NameColor = DungeonUi.Bone;
        /// <summary>정예 이름: 밝은 뼈색(세상 속 정예 흰 테두리와 같은 뜻, 등급색이 아님).</summary>
        static readonly Color EliteNameColor = new Color(0.98f, 0.95f, 0.86f, 1f);
        static readonly Color EliteFrame = new Color(0.98f, 0.95f, 0.86f, 0.6f);
        static readonly Color TextEdge = new Color(0f, 0f, 0f, 0.9f);
        static readonly Color LevelColor = DungeonUi.Ember;
        static readonly Color TraitColor = DungeonUi.BoneDim;
        static readonly Color NumberColor = DungeonUi.Bone;
        static readonly Color BarBack = new Color(0.02f, 0f, 0f, 0.85f);
        static readonly Color BarFill = DungeonUi.Blood;
        static readonly Color BarLag = new Color(0.86f, 0.58f, 0.42f, 0.85f);
        static readonly Color BarShine = new Color(1f, 0.55f, 0.45f, 0.22f);
        static readonly Color PoiseBack = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color BrokenColor = new Color(1f, 0.89f, 0.36f, 1f);
        static readonly Color HalfTick = new Color(1f, 1f, 1f, 0.9f);

        public static TargetPlate Instance { get; private set; }

        /// <summary>지금 이름표에 보이는 적(흐려지는 중이면 그 적, 없으면 null).</summary>
        public Enemy Target => _hasPlate ? _target : null;

        Camera _cam;
        Enemy _target;
        bool _hasPlate;
        bool _fading;
        float _fadeStart;
        float _alpha;

        Enemy _lastHit;
        float _lastHitTime = -999f;

        // 대상이 바뀔 때만 만드는 글.
        string _nameText;
        string _levelText;
        /// <summary>보스 첫 깸 큰 이름(레벨 없이).</summary>
        string _bossTitle;
        bool _elite;
        bool _boss;
        bool _hasPoise;
        // 값이 바뀔 때만 만드는 글.
        string _hpText;
        int _hpCur = int.MinValue;
        int _hpMax = int.MinValue;
        string _traitText;
        int _traitKey = -1;

        // 마지막으로 읽은 값(대상이 지워진 뒤 흐려질 때 그대로 그린다).
        float _frac;
        float _lagFrac;
        float _lagHoldUntil;
        float _poiseFrac;
        bool _broken;
        float _breakFrac;

        float _topOffset;

        // 보스 첫 깸 연출(판 번호가 바뀔 때마다 다시).
        Enemy _introBoss;
        int _introFight = -1;
        float _introStart = -999f;

        bool _stylesReady;
        GUIStyle _nameStyle;
        GUIStyle _levelStyle;
        GUIStyle _numberStyle;
        GUIStyle _traitStyle;
        GUIStyle _poiseStyle;
        GUIStyle _bossTitleStyle;

        void Awake()
        {
            Instance = this;
            CombatEvents.PlayerDealtDamage += OnDealt;
        }

        void OnDestroy()
        {
            CombatEvents.PlayerDealtDamage -= OnDealt;
            if (Instance == this) Instance = null;
        }

        /// <summary>플레이어가 때린 적을 기억한다(커서 아래 적이 없을 때 2.5초 동안 보인다).</summary>
        void OnDealt(DamageDealt d)
        {
            var e = d.Target;
            if (!e || e.IsDummy || d.Source == DamageSource.Enemy) return;
            _lastHit = e;
            _lastHitTime = Time.time;
        }

        /// <summary>이름표에 올릴 수 있는 적인가: 살아 있고, 허수아비가 아니고, 시야 밖(가려짐 + 시야 다각형 밖)이 아니다.</summary>
        static bool Eligible(Enemy e)
        {
            if (!e || e.Dead || e.IsDummy || e.Health == null) return false;
            return !(e.VisionHidden && !e.VisionInSight);
        }

        /// <summary>깨어 있는 살아 있는 보스(이름표에 고정). 없으면 null. 시야에 가려도 보스 싸움 중에는 보인다.</summary>
        static Enemy PinnedBoss()
        {
            var all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e && e.IsBoss && e.Aware && !e.Dead && e.Health != null) return e;
            }
            return null;
        }

        void Update()
        {
            var pick = PinnedBoss();
            if (pick) NoteBossIntro(pick);
            if (!pick) pick = PickHovered();
            if (!pick && _lastHit && Time.time - _lastHitTime <= RecentHitSeconds && Eligible(_lastHit)) pick = _lastHit;

            float now = Time.unscaledTime;
            if (pick)
            {
                if (!_hasPlate || pick != _target) Bind(pick);
                _fading = false;
                _alpha = 1f;
            }
            else if (_hasPlate)
            {
                if (!_fading)
                {
                    _fading = true;
                    _fadeStart = now;
                }
                _alpha = 1f - (now - _fadeStart) / FadeOutSeconds;
                if (_alpha <= 0f)
                {
                    _hasPlate = false;
                    _target = null;
                    _alpha = 0f;
                }
            }

            if (_hasPlate) ReadValues(now);
            UpdateTopOffset();
        }

        /// <summary>판마다 처음 깰 때 연출을 시작한다(오우거는 깰 때마다 판 번호가 오른다. 다른 보스는 처음 고정될 때 한 번).</summary>
        void NoteBossIntro(Enemy boss)
        {
            int fight = boss is OgreBrain og ? og.FightId : 0;
            if (boss == _introBoss && fight == _introFight) return;
            _introBoss = boss;
            _introFight = fight;
            _introStart = Time.unscaledTime;
        }

        /// <summary>커서 아래(반지름 + 0.4 안) 적 중 가장 가까운 것. 시험 패널 위에 커서가 있으면 없음.</summary>
        Enemy PickHovered()
        {
            var mouse = Mouse.current;
            if (mouse == null || CombatHud.PointerOverPanel) return null;
            var log = ExplorationLog.Instance;
            if (log && log.PointerOverPanel) return null;
            if (!_cam) _cam = Camera.main;
            if (!_cam) return null;
            Vector2 sp = mouse.position.ReadValue();
            if (!_cam.pixelRect.Contains(sp)) return null;
            Vector2 cursor = _cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10f));
            Enemy best = null;
            float bestScore = float.MaxValue;
            foreach (var e in Enemy.All)
            {
                if (!Eligible(e)) continue;
                float d = (e.Position - cursor).magnitude;
                if (d > e.Radius + HoverPad) continue;
                float score = d - e.Radius;
                if (score >= bestScore) continue;
                best = e;
                bestScore = score;
            }
            return best;
        }

        /// <summary>새 대상: 이름·레벨 글을 만든다(대상이 바뀔 때만).</summary>
        void Bind(Enemy e)
        {
            _target = e;
            _hasPlate = true;
            var rule = MonsterRule.Of(e.Kind, Tuning.Ruleset, Tuning.SoftBoar);
            _elite = e.IsElite;
            _boss = e.IsBoss;
            // 접두사 + '정예' + 이름(1-2층 탐험 맛 1차 4-1 '단단한 정예 돌충이', ExploreText.PackElite와 같은 차례).
            _nameText = _elite ? AffixPrefix(e.Affixes) + "정예 " + rule.DisplayName : rule.DisplayName;
            int floor = e.Floor > 0 ? e.Floor : RootFloor();
            _levelText = "Lv " + (_elite ? floor + 1 : floor);
            if (_boss)
            {
                // 보스: 이름 줄에 레벨을 붙인다(Lv = 층 + 1, 정예와 같음).
                _nameText = rule.DisplayName + "  Lv " + BossRules.PlateLevel(floor);
                _bossTitle = rule.DisplayName;
                _levelText = "";
            }
            _hpCur = int.MinValue;
            _hpMax = int.MinValue;
            _traitKey = -1;
            _frac = e.Health.Fraction;
            _lagFrac = _frac;
            _lagHoldUntil = 0f;
        }

        /// <summary>정예 접두사(3차 초안 3-4: '단단한', '무리 거느린'). 둘 다면 둘 다 붙인다.</summary>
        static string AffixPrefix(EliteAffix affixes)
        {
            bool hard = (affixes & EliteAffix.Hardened) != 0;
            bool pack = (affixes & EliteAffix.Pack) != 0;
            if (hard && pack) return "단단한 무리 거느린 ";
            if (hard) return "단단한 ";
            if (pack) return "무리 거느린 ";
            return "";
        }

        static int RootFloor()
        {
            if (DungeonRoot.Instance) return DungeonRoot.Instance.Floor;
            if (CombatTestRoot.Instance) return CombatTestRoot.Instance.Floor;
            return 1;
        }

        /// <summary>체력·버팀·상태를 읽고, 바뀐 글만 다시 만든다. 대상이 지워졌으면 마지막 값을 그대로 둔다.</summary>
        void ReadValues(float now)
        {
            var e = _target;
            if (!e || e.Health == null) return;
            var hp = e.Health;
            int cur = e.Dead ? 0 : hp.Current;
            if (cur != _hpCur || hp.Max != _hpMax)
            {
                _hpCur = cur;
                _hpMax = hp.Max;
                _hpText = cur + " / " + hp.Max;
            }
            float frac = hp.Max > 0 ? Mathf.Clamp01((float)cur / hp.Max) : 0f;
            // 깎인 몫(꼬리): 깎이는 순간 잠깐 머물고 줄어든다. 회복은 바로 따라간다.
            if (frac < _frac - 0.0001f) _lagHoldUntil = now + LagHold;
            _frac = frac;
            if (_lagFrac < _frac) _lagFrac = _frac;
            else if (now >= _lagHoldUntil) _lagFrac = Mathf.MoveTowards(_lagFrac, _frac, Time.unscaledDeltaTime * LagDrainPerSecond);

            _hasPoise = e.Poise != null;
            _broken = _hasPoise && e.Broken;
            _poiseFrac = _hasPoise ? Mathf.Clamp01((float)e.Poise.Fraction) : 0f;
            // 무너짐 길이(일반 2초, 단단한 정예 3초) 기준이라 무너지는 순간 막대가 가득 찬다.
            _breakFrac = _broken ? Mathf.Clamp01(e.BreakRemaining / Mathf.Max(0.1f, e.BreakLength)) : 0f;

            if (e is NestBrain nb)
            {
                // 둥지: 무게·잠 대신 껍질 진행(굴쥐를 몇 마리 더 잡아야 껍질이 깨지는가)을 보인다. 값이 바뀔 때만 글을 만든다.
                int nestKey = nb.HasShell ? 1000 + nb.KilledForShell * 100 + nb.ShellGoal : 999;
                if (nestKey != _traitKey)
                {
                    _traitKey = nestKey;
                    _traitText = nb.HasShell ? "껍질 · 굴쥐 " + nb.KilledForShell + "/" + nb.ShellGoal : "껍질 깨짐";
                }
                return;
            }
            if (_boss)
            {
                // 보스: 무게·잠 대신 '보스 · 넉백 없음', 2단계면 '· 성남'.
                int phase = e is OgreBrain og ? og.Phase : 1;
                int bossKey = 2000 + phase;
                if (bossKey != _traitKey)
                {
                    _traitKey = bossKey;
                    _traitText = phase >= 2 ? "보스 · 넉백 없음 · 성남" : "보스 · 넉백 없음";
                }
                return;
            }
            // 순찰(1-2층 탐험 맛 1차 4-3)은 잠과 따로 보인다('z z'를 지운 것과 같은 뜻). 열쇠 셈 Weight * 4 + state는 0~3을 그대로 담는다.
            int state = e.Aware ? 2 : e.IsEating ? 1 : e.IsPatrolling ? 3 : 0;
            int key = (int)e.Weight * 4 + state;
            if (key != _traitKey)
            {
                _traitKey = key;
                _traitText = "무게 " + WeightName(e.Weight) + " · " + StateName(state);
            }
        }

        static string WeightName(EnemyWeight w)
        {
            switch (w)
            {
                case EnemyWeight.Light: return "가벼움";
                case EnemyWeight.Medium: return "보통";
                default: return "무거움";
            }
        }

        static string StateName(int state)
        {
            switch (state)
            {
                case 0: return "잠듦";
                case 1: return "먹는 중";
                case 3: return "순찰 중";
                default: return "깨어남";
            }
        }

        /// <summary>전투 시험장: 연속 처치 글자(3 이상, 2~3.2초)가 떠 있는 동안 이름표를 그 아래로 부드럽게 내린다.</summary>
        void UpdateTopOffset()
        {
            float want = 0f;
            if (CombatTestRoot.Instance && Tuning.KillStreakText)
            {
                var p = PlayerController.Instance;
                if (p && p.KillStreak >= 3 && Time.time - p.KillStreakTime <= 3.2f) want = BelowStreakTop - TopMargin;
            }
            _topOffset = Mathf.MoveTowards(_topOffset, want, Time.unscaledDeltaTime * 900f);
        }

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            var serif = DungeonUi.Serif;
            var label = GUI.skin.label;
            _nameStyle = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            _levelStyle = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow };
            _numberStyle = new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _traitStyle = new GUIStyle(label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _poiseStyle = new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow };
            _bossTitleStyle = new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            White(_nameStyle);
            White(_levelStyle);
            White(_numberStyle);
            White(_traitStyle);
            White(_poiseStyle);
            White(_bossTitleStyle);
        }

        static void White(GUIStyle s)
        {
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
        }

        /// <summary>그릴 수 없는 때: 던전 창이 열림, 플레이어가 쓰러짐.</summary>
        static bool Suppressed()
        {
            var p = PlayerController.Instance;
            if (!p || p.IsDown) return true;
            // 창 상태는 던전에서만 본다(전투 시험장에서는 지난 던전 플레이의 값이 남아 있을 수 있다).
            return DungeonRoot.Instance && DungeonUi.ModalOpen;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!_hasPlate || _alpha <= 0.002f || _nameText == null || Suppressed()) return;
            EnsureStyles();
            GUI.depth = GuiDepth;
            float scale = Mathf.Max(0.1f, Screen.height / RefHeight);
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Draw(scale);
            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        /// <summary>왼쪽 영역(오른쪽 시험 패널 제외) 가운데 위에 검은 쇠 판 하나.</summary>
        void Draw(float scale)
        {
            float a = Mathf.Clamp01(_alpha);
            float screenW = Screen.width / scale;
            float reserved = 0f;
            if (CombatTestRoot.Instance) reserved = screenW * CombatHud.PanelScreenFraction;
            else if (ExplorationLog.Instance) reserved = ExplorationLog.Instance.PanelReservedWidth;
            float cx = (screenW - reserved) * 0.5f;

            float top = TopMargin + _topOffset;
            float height = _hasPoise ? 104f : 90f;
            var plate = new Rect(cx - PlateWidth * 0.5f, top, PlateWidth, height);
            DungeonUi.Box(plate, 0.8f * a);
            if (_elite)
            {
                // 정예: 밝은 뼈색 테두리(세상 속 흰 테두리와 같은 뜻).
                DungeonUi.Outline(new Rect(plate.x - 3f, plate.y - 3f, plate.width + 6f, plate.height + 6f), Fade(EliteFrame, a), 1f);
                DungeonUi.Outline(new Rect(plate.x + 6f, plate.y + 6f, plate.width - 12f, plate.height - 12f), Fade(EliteFrame, 0.5f * a), 1f);
            }

            float x = plate.x + 16f;
            float w = PlateWidth - 32f;
            float y = top + 9f;

            // 이름(가운데)과 레벨(왼쪽).
            var nameRect = new Rect(x, y, w, 26f);
            if (_elite) OutlinedLabel(nameRect, _nameText, _nameStyle, Fade(EliteNameColor, a), Fade(TextEdge, a));
            else DungeonUi.ShadowLabel(nameRect, _nameText, _nameStyle, Fade(NameColor, a));
            if (!string.IsNullOrEmpty(_levelText)) DungeonUi.ShadowLabel(new Rect(x, y, 70f, 26f), _levelText, _levelStyle, Fade(LevelColor, a));
            y += 30f;

            // 보스 첫 깸: 체력 막대가 0.8초에 걸쳐 0에서 차오른다(실제 시간).
            float introAge = _boss && _target == _introBoss ? Time.unscaledTime - _introStart : 999f;
            float shownFrac = introAge < BossIntroFill ? Mathf.Min(_frac, Mathf.Clamp01(introAge / BossIntroFill)) : _frac;

            // 체력 막대: 검은 홈 → 깎인 꼬리 → 짙은 피색 → 윗면 광 → 쇠 테 → 숫자.
            var bar = new Rect(x, y, w, 17f);
            DungeonUi.Fill(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), Fade(BarBack, a));
            if (_lagFrac > _frac && shownFrac >= _frac) DungeonUi.Fill(new Rect(bar.x, bar.y, bar.width * _lagFrac, bar.height), Fade(BarLag, a));
            if (shownFrac > 0f)
            {
                var fill = new Rect(bar.x, bar.y, bar.width * shownFrac, bar.height);
                DungeonUi.Fill(fill, Fade(BarFill, a));
                DungeonUi.Fill(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f)), Fade(BarShine, a));
            }
            DungeonUi.Outline(bar, Fade(DungeonUi.IronEdge, a), 1f);
            // 보스: 50% 자리 흰 눈금(2단계 문턱).
            if (_boss) DungeonUi.Fill(new Rect(bar.x + bar.width * BossRules.PhaseThreshold - 1f, bar.y - 2f, 2f, bar.height + 4f), Fade(HalfTick, a));
            DungeonUi.ShadowLabel(bar, _hpText, _numberStyle, Fade(NumberColor, a), 1f);
            y += 21f;

            // 버팀: 왼쪽 글('버팀' / 무너지면 깜빡이는 '무너짐!') + 얇은 막대.
            if (_hasPoise)
            {
                float blink = _broken ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 20f) : 1f;
                var labelRect = new Rect(x, y, 64f, 14f);
                if (_broken) DungeonUi.ShadowLabel(labelRect, "무너짐!", _poiseStyle, Fade(BrokenColor, a * blink));
                else DungeonUi.ShadowLabel(labelRect, "버팀", _poiseStyle, Fade(TraitColor, a));
                var pr = new Rect(x + 66f, y + 5f, w - 66f, 5f);
                DungeonUi.Fill(pr, Fade(PoiseBack, a));
                if (_broken) DungeonUi.Fill(new Rect(pr.x, pr.y, pr.width * _breakFrac, pr.height), Fade(BrokenColor, a * blink));
                else if (_poiseFrac > 0f) DungeonUi.Fill(new Rect(pr.x, pr.y, pr.width * _poiseFrac, pr.height), Fade(Palette.Poise, a));
                y += 15f;
            }

            // 특성 한 줄.
            DungeonUi.ShadowLabel(new Rect(x, y, w, 20f), _traitText, _traitStyle, Fade(TraitColor, a));

            // 보스 첫 깸: 이름이 판 아래에 1.5초 크게 떴다가 흐려진다.
            if (introAge < BossIntroName)
            {
                float fade = introAge <= BossIntroName - BossIntroNameFade ? 1f : 1f - (introAge - (BossIntroName - BossIntroNameFade)) / BossIntroNameFade;
                var titleRect = new Rect(plate.x - 120f, plate.yMax + 8f, PlateWidth + 240f, 48f);
                OutlinedLabel(titleRect, _bossTitle, _bossTitleStyle, Fade(EliteNameColor, a * fade), Fade(TextEdge, a * fade));
            }
        }

        static Color Fade(Color c, float a)
        {
            c.a *= a;
            return c;
        }

        /// <summary>글 둘레 여덟 방향에 어두운 테두리를 두르고 그 위에 글을 쓴다(정예 이름).</summary>
        static void OutlinedLabel(Rect r, string text, GUIStyle style, Color color, Color edge)
        {
            if (string.IsNullOrEmpty(text)) return;
            var prev = GUI.color;
            GUI.color = edge;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), text, style);
            }
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = prev;
        }
    }
}
