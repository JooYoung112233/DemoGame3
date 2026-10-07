using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    public enum NumberKind
    {
        Normal,
        Crit,
        Taken,
        Heal,
        /// <summary>방패로 막은 피해(기획/세-무기-우클릭-소켓-1차.md 2-5): 회색, 0.8배. 0이어도 띄운다(보통 화살).</summary>
        Blocked,
    }

    /// <summary>
    /// 기획 3-6 피해 숫자와 적 체력바. 원화·TMP 전 시험용이라 IMGUI로 그린다.
    /// 숫자는 실제 시간 0.7초 동안 0.8유닛 떠오르고, 0.15초 안에 겹치면 좌우로 비킨다. 동시 최대 40개.
    /// 던전에서는(DungeonRoot가 있을 때만) 다크 판타지 1차 '화면 연출' 색을 쓴다: 보통 숫자는 뼈색, 치명 노랑·받은 피해 빨강·회복 초록은 뜻 그대로 읽히게 조금만 낮추고,
    /// 짧은 글자는 바탕체 + 진한 그림자, 적 체력바는 짙은 피색. 전투 시험장은 손맛 비교 기준이라 그대로 둔다. 시간·크기·떠오름은 바꾸지 않는다.
    /// 위치 글자(Tag: '백어택' 흐린 은색, '헤드어택' 짙은 금색)는 두 시험장 모두 그 타의 피해 숫자 바로 위에 작게(0.78배) 한 번, 한 프레임에 2개까지.
    /// 같은 자리의 짧은 글자('무너짐!'·'!')는 위치 글자 위로, 생긴 순서대로 서로도 겹치지 않게 쌓인다(숫자 → 위치 글자 → '!' → '무너짐!').
    /// 위치 글자가 없는 자리에서도 짧은 글자끼리는 겹치면 나중 글자가 위로 비킨다. 피해 숫자끼리는 예전처럼 좌우로만 비킨다.
    /// </summary>
    public sealed partial class WorldOverlay : MonoBehaviour
    {
        const int MaxNumbers = 40;
        const float Life = 0.7f;
        const float Rise = 0.8f;

        struct Entry
        {
            public Vector2 World;
            public string Text;
            public Color Color;
            public float Scale;
            public float Born;
            /// <summary>짧은 글자인가(숫자가 아님). 던전에서 바탕체로 그린다.</summary>
            public bool Word;
            /// <summary>비키기 전 자리(피해 숫자). 위치 글자(Tag)가 자기 숫자를 찾는 데 쓴다.</summary>
            public Vector2 Origin;
            /// <summary>위치 글자(백어택·헤드어택)인가. 어디서나 바탕체(있으면) + 진한 그림자로 그린다.</summary>
            public bool Tag;
            /// <summary>숫자 위로 올려 그리는 높이(1080 기준 픽셀). 숫자·다른 글자는 0.</summary>
            public float Lift;
        }

        // 위치 글자(PositionalHitRule): 피해 숫자 바로 위에 작게. 백어택 = 흐린 은색, 헤드어택 = 짙은 금색(치명 노랑보다 어둡고 붉어 숫자와 섞이지 않음).
        static readonly Color TagBackColor = new Color(0.68f, 0.70f, 0.74f, 1f);
        static readonly Color TagHeadColor = new Color(0.78f, 0.56f, 0.18f, 1f);
        const float TagScale = 0.78f;
        /// <summary>한 프레임에 띄우는 위치 글자 상한(대검 걷어 베기로 여러 마리를 한 번에 맞혀도 글자가 쌓이지 않게, 가까운 대상부터).</summary>
        const int MaxTagsPerFrame = 2;
        /// <summary>숫자와 글자 사이 틈(1080 기준 픽셀).</summary>
        const float TagGap = 2f;
        /// <summary>위치 글자와 짧은 글자('무너짐!'·'!' 등)의 가운데가 가로로 이만큼(유닛) 안이면 같은 자리로 보고 짧은 글자를 위로 비킨다.</summary>
        const float StackNearX = 1f;
        /// <summary>쌓기에서 소수 오차로 생기는 아주 작은 겹침은 무시한다(1080 기준 픽셀).</summary>
        const float StackSlack = 0.01f;

        // 던전 색(다크 판타지 1차). 피해 숫자 색 뜻은 유지한다.
        static readonly Color DungeonNormal = new Color(0.88f, 0.83f, 0.72f, 1f);
        static readonly Color DungeonCrit = new Color(1f, 0.83f, 0.3f, 1f);
        static readonly Color DungeonTaken = new Color(0.94f, 0.24f, 0.2f, 1f);
        static readonly Color DungeonHeal = new Color(0.5f, 0.86f, 0.48f, 1f);
        static readonly Color DungeonWordWhite = new Color(0.86f, 0.8f, 0.68f, 1f);
        static readonly Color DungeonBarFill = new Color32(0x8A, 0x10, 0x10, 0xFF);
        static readonly Color DungeonBarBack = new Color(0.02f, 0f, 0f, 0.8f);
        static readonly Color DungeonBarShine = new Color(1f, 0.55f, 0.45f, 0.22f);
        static readonly Color DungeonSleep = new Color(0.6f, 0.62f, 0.7f, 0.7f);
        /// <summary>막은 피해 숫자(NumberKind.Blocked): 회색. 던전은 조금 따뜻한 회색.</summary>
        static readonly Color BlockedColor = new Color(0.68f, 0.69f, 0.71f, 1f);
        static readonly Color DungeonBlocked = new Color(0.62f, 0.6f, 0.56f, 1f);
        const float BlockedScale = 0.8f;
        /// <summary>짧은 글자 기본 크기(Text). 크기 인자가 있는 Text는 이 값에 곱한다.</summary>
        const float WordScale = 1.15f;

        /// <summary>지금 던전 시험장인가(전투 시험장은 기존 색 그대로).</summary>
        static bool InDungeon => DungeonRoot.Instance != null;

        static WorldOverlay _instance;
        readonly List<Entry> _entries = new List<Entry>(MaxNumbers);
        /// <summary>글자 쌓기 때 아래에 깔린 글자 목록(매번 비워 다시 씀).</summary>
        readonly List<Entry> _stack = new List<Entry>(8);
        float _lastSpawnTime = -1f;
        Vector2 _lastSpawnPos;
        /// <summary>한 번 쓰는 치명 단계 자리(SetNextCritTier). 비어 있으면 예전 1.4배.</summary>
        CritTier _nextCritTier;
        bool _hasNextCritTier;
        int _jitterSide = 1;
        int _tagFrame = -1;
        int _tagsThisFrame;
        Camera _cam;
        GUIStyle _style;

        public static void Number(Vector2 world, int amount, NumberKind kind)
        {
            if (!_instance || !Tuning.DamageNumbers) return;
            _instance.Add(world, amount, kind);
        }

        /// <summary>
        /// 다음 치명 숫자 하나의 연출 단계(장비 문서 3-4). PlayerController가 적 TakeHit 직전에 넣고 직후에 지운다
        /// (Enemy가 띄우는 숫자 크기를 Enemy를 고치지 않고 정함). 치명 숫자 하나가 쓰면 비워진다.
        /// </summary>
        public static void SetNextCritTier(CritTier tier)
        {
            if (!_instance) return;
            _instance._nextCritTier = tier;
            _instance._hasNextCritTier = true;
        }

        public static void ClearNextCritTier()
        {
            if (!_instance) return;
            _instance._hasNextCritTier = false;
        }

        /// <summary>치명 숫자 크기: 가벼움 1.2배, 보통·무거움 1.4배(예전 치명 크기).</summary>
        public static float CritScale(CritTier tier) => tier == CritTier.Light ? LightCritScale : CritScaleDefault;

        const float CritScaleDefault = 1.4f;
        const float LightCritScale = 1.2f;

        /// <summary>
        /// '무너짐!', '잠잠해졌다' 같은 짧은 글자. 피해 숫자 끄기와 관계없이 보인다. 던전에서 순수 흰색은 뼈색으로 바꾼다(등급색 등 다른 색은 그대로).
        /// 같은 자리에 살아 있는 위치 글자·짧은 글자와 겹치면 그 위로 비킨다(StackWord).
        /// </summary>
        public static void Text(Vector2 world, string text, Color color) => Text(world, text, color, 1f);

        /// <summary>
        /// 크기를 정하는 짧은 글자(무기 행동 머리 위 글: '튕겨 냄!' 1.1배, '끊김'·'버팀' 1배). sizeScale은 보통 짧은 글자 크기(1.15) 대비 배율이다.
        /// 그 밖은 Text(world, text, color)와 같다(겹치면 위로 비킴, 던전 흰색은 뼈색).
        /// </summary>
        public static void Text(Vector2 world, string text, Color color, float sizeScale)
        {
            if (!_instance) return;
            if (InDungeon && color == Color.white) color = DungeonWordWhite;
            if (_instance._entries.Count >= MaxNumbers) _instance._entries.RemoveAt(0);
            float scale = WordScale * Mathf.Clamp(sizeScale, 0.5f, 2f);
            var entry = new Entry { World = world, Text = text, Color = color, Scale = scale, Born = Time.unscaledTime, Word = true };
            _instance.StackWord(ref entry);
            _instance._entries.Add(entry);
        }

        /// <summary>
        /// '백어택'·'헤드어택'(PositionalHitRule)을 그 타의 피해 숫자 바로 위에 작게 한 번 띄운다. world는 피해 숫자를 띄운 자리(적 Position + 위 × Radius)다:
        /// 같은 프레임에 그 자리에서 띄운 숫자가 있으면 비킨 자리·크기를 따라 그 위에, 없으면(피해 숫자 끔) 그 자리 위에 그린다. 시간·떠오름은 숫자와 같다.
        /// 한 프레임에 MaxTagsPerFrame개까지(넘으면 띄우지 않음). Tuning.PositionalHitText를 끄면 띄우지 않는다.
        /// </summary>
        public static void Tag(Vector2 world, HitTag tag)
        {
            if (!_instance || tag == HitTag.None || !Tuning.PositionalHitText) return;
            _instance.AddTag(world, tag);
        }

        void AddTag(Vector2 world, HitTag tag)
        {
            int frame = Time.frameCount;
            if (frame != _tagFrame)
            {
                _tagFrame = frame;
                _tagsThisFrame = 0;
            }
            if (_tagsThisFrame >= MaxTagsPerFrame) return;
            _tagsThisFrame++;
            float now = Time.unscaledTime;
            Vector2 at = world;
            float numberScale = 1f;
            // 같은 프레임에 같은 자리에서 띄운 피해 숫자(가장 최근)를 찾는다. 처치 사건 등으로 사이에 다른 숫자가 끼어도 자리로 가린다.
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Born != now) break;
                if (e.Word || e.Tag || (e.Origin - world).sqrMagnitude > 0.0001f) continue;
                at = e.World;
                numberScale = e.Scale;
                break;
            }
            if (_entries.Count >= MaxNumbers) _entries.RemoveAt(0);
            var tagEntry = new Entry
            {
                World = at,
                Origin = world,
                Text = PositionalHitRule.Word(tag),
                Color = tag == HitTag.Head ? TagHeadColor : TagBackColor,
                Scale = TagScale,
                Born = now,
                Tag = true,
                // 숫자 글자 높이의 반 + 글자 높이의 반 + 틈.
                Lift = 22f * (numberScale + TagScale) * 0.5f + TagGap,
            };
            // 같은 타에서 먼저 뜬 '무너짐!'(Break는 TakeHit 안에서 위치 글자보다 먼저 불림)·알아챔 '!'가 이 글자와 겹치면 그 위로 올린다.
            // 짧은 글자는 생긴 순서대로 위치 글자와 먼저 쌓인 짧은 글자 위에 쌓는다: 숫자 → 위치 글자 → '!' → '무너짐!'.
            // 짧은 글자를 하나씩 따로 올리면 거의 같은 자리의 '!'(Radius+0.5)와 '무너짐!'(Radius+0.6)이 같은 높이로 올라가 겹친다.
            // 같이 떠오르므로 한 번 비키면 0.7초 내내 떨어져 있다.
            float ppu = PixelsPerUnit1080();
            _stack.Clear();
            foreach (var e in _entries)
                if (e.Tag && now - e.Born <= Life && Mathf.Abs(e.World.x - at.x) <= StackNearX) _stack.Add(e);
            _stack.Add(tagEntry);
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.Word || e.Tag || now - e.Born > Life || Mathf.Abs(e.World.x - at.x) > StackNearX) continue;
                float up = ClearOf(e, _stack, now, ppu);
                if (up > 0f)
                {
                    e.Lift += up;
                    _entries[i] = e;
                }
                _stack.Add(e);
            }
            _entries.Add(tagEntry);
        }

        /// <summary>
        /// 새 짧은 글자를 같은 자리(가로 StackNearX 안)에 살아 있는 위치 글자와 먼저 뜬 짧은 글자들 위로 올린다
        /// (벽 박기 '무너짐!'이 위치 글자 뒤에 뜰 때, 알아챔 '!' 바로 뒤 기습으로 '무너짐!'이 뜰 때 등). 겹치지 않는 글자는 그대로 둔다.
        /// </summary>
        void StackWord(ref Entry word)
        {
            float now = Time.unscaledTime;
            float ppu = PixelsPerUnit1080();
            _stack.Clear();
            foreach (var e in _entries)
                if ((e.Tag || e.Word) && now - e.Born <= Life && Mathf.Abs(word.World.x - e.World.x) <= StackNearX) _stack.Add(e);
            if (_stack.Count == 0) return;
            word.Lift += ClearOf(word, _stack, now, ppu);
        }

        /// <summary>
        /// word를 below의 어느 글자와도 겹치지 않을 때까지 올리는 높이(1080 기준 픽셀). 하나를 비키다 다른 것에 닿으면 다시 비킨다.
        /// 한 번 위로 비킨 글자와는 다시 닿지 않으므로 below 개수 + 1번이면 끝난다.
        /// </summary>
        static float ClearOf(Entry word, List<Entry> below, float now, float ppu)
        {
            float start = word.Lift;
            for (int pass = 0; pass <= below.Count; pass++)
            {
                bool moved = false;
                foreach (var o in below)
                {
                    float up = RaiseOver(word, o, now, ppu);
                    if (up <= 0f) continue;
                    word.Lift += up;
                    moved = true;
                }
                if (!moved) break;
            }
            return word.Lift - start;
        }

        /// <summary>
        /// word가 아래 글자 other(위치 글자 또는 먼저 쌓인 짧은 글자)와 겹치면 other 위로 비키는 데 더할 높이(1080 기준 픽셀), 겹치지 않으면 0.
        /// 가운데 높이 = (자리 + 떠오름) × 픽셀/유닛 + Lift. 둘 다 같은 빠르기로 떠오르므로 지금 차이가 끝까지 간다.
        /// </summary>
        static float RaiseOver(Entry word, Entry other, float now, float ppu)
        {
            if (Mathf.Abs(word.World.x - other.World.x) > StackNearX) return 0f;
            float need = 22f * (word.Scale + other.Scale) * 0.5f + TagGap;
            float gap = CenterPx(word, now, ppu) - CenterPx(other, now, ppu);
            // 방금 비킨 글자가 소수 오차로 다시 걸리지 않게 조금 봐준다.
            if (gap >= need - StackSlack || gap <= -need) return 0f;
            return need - gap;
        }

        static float CenterPx(Entry e, float now, float ppu) => (e.World.y + Rise * (now - e.Born) / Life) * ppu + e.Lift;

        /// <summary>1080 높이 화면에서 1유닛의 픽셀 수(정사영 크기 8이면 67.5). Lift와 같은 단위.</summary>
        static float PixelsPerUnit1080()
        {
            var cam = Camera.main;
            float size = cam && cam.orthographic && cam.orthographicSize > 0.01f ? cam.orthographicSize : DungeonCamera.Size;
            return 1080f / (size * 2f);
        }

        void Awake() => _instance = this;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Add(Vector2 world, int amount, NumberKind kind)
        {
            float now = Time.unscaledTime;
            Vector2 origin = world;
            if (now - _lastSpawnTime < 0.15f && (world - _lastSpawnPos).sqrMagnitude < 1f)
            {
                world.x += 0.3f * _jitterSide;
                _jitterSide = -_jitterSide;
            }
            else
            {
                _lastSpawnPos = world;
            }
            _lastSpawnTime = now;
            if (_entries.Count >= MaxNumbers) _entries.RemoveAt(0);
            bool dungeon = InDungeon;
            float critScale = CritScaleDefault;
            if (kind == NumberKind.Crit && _hasNextCritTier)
            {
                critScale = CritScale(_nextCritTier);
                _hasNextCritTier = false;
            }
            _entries.Add(new Entry
            {
                World = world,
                Origin = origin,
                Text = kind == NumberKind.Heal ? "+" + amount : amount.ToString(),
                Color = kind switch
                {
                    NumberKind.Crit => dungeon ? DungeonCrit : Palette.NumberCrit,
                    NumberKind.Taken => dungeon ? DungeonTaken : Palette.NumberTaken,
                    NumberKind.Heal => dungeon ? DungeonHeal : Palette.NumberHeal,
                    NumberKind.Blocked => dungeon ? DungeonBlocked : BlockedColor,
                    _ => dungeon ? DungeonNormal : Palette.NumberNormal,
                },
                Scale = kind == NumberKind.Crit ? critScale : kind == NumberKind.Blocked ? BlockedScale : 1f,
                Born = now,
            });
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;
            GUI.depth = 10;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, wordWrap = false };
            DrawBars();
            // 한손검과 방패 방어 게이지(캐릭터 밑, 적 머리 위 막대를 피해 그림, WorldOverlay.GuardGauge.cs).
            DrawGuardGauge();
            DrawNumbers();
        }

        void DrawBars()
        {
            float now = Time.unscaledTime;
            bool dungeon = InDungeon;
            float pxPerUnit = Screen.height / (_cam.orthographicSize * 2f);
            _enemyBarRects.Clear();
            // 대상 이름표(TargetPlate)에 체력·버팀이 이미 보이는 적은 머리 위 작은 막대를 겹쳐 그리지 않는다.
            var plateTarget = TargetPlate.Instance ? TargetPlate.Instance.Target : null;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || e.VisionHidden) continue;
                if (plateTarget && e == plateTarget)
                {
                    if (!e.Aware) DrawSleep(e, pxPerUnit);
                    continue;
                }
                // 3차: 무거운 적·정예는 깨어 있는 동안 체력바와 버팀 막대를 늘 보인다(정보만). 나머지는 맞은 뒤 3초.
                bool always = e.IsV3 && e.Aware && e.Weight == EnemyWeight.Heavy && !e.IsDummy;
                if (!always && (e.Health.Infinite || e.Health.BarVisibleUntil < now))
                {
                    if (!e.Aware) DrawSleep(e, pxPerUnit);
                    continue;
                }
                float widthUnits = Mathf.Max(0.8f, e.Radius * 2f);
                var top = _cam.WorldToScreenPoint(e.Position + Vector2.up * (e.Radius + 0.22f));
                float w = widthUnits * pxPerUnit;
                float h = Mathf.Max(3f, 0.09f * pxPerUnit);
                var rect = new Rect(top.x - w * 0.5f, Screen.height - top.y - h, w, h);
                if (!e.Health.Infinite)
                {
                    if (dungeon)
                    {
                        // 던전: 검은 홈 + 짙은 피색 채움 + 윗면 옅은 광.
                        GUI.color = DungeonBarBack;
                        GUI.DrawTexture(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), Texture2D.whiteTexture);
                        var fill = new Rect(rect.x, rect.y, rect.width * e.Health.Fraction, rect.height);
                        GUI.color = DungeonBarFill;
                        GUI.DrawTexture(fill, Texture2D.whiteTexture);
                        GUI.color = DungeonBarShine;
                        GUI.DrawTexture(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f)), Texture2D.whiteTexture);
                    }
                    else
                    {
                        GUI.color = new Color(0f, 0f, 0f, 0.65f);
                        GUI.DrawTexture(rect, Texture2D.whiteTexture);
                        GUI.color = Palette.HealthBar;
                        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * e.Health.Fraction, rect.height), Texture2D.whiteTexture);
                    }
                }
                if (e.Poise != null)
                {
                    var pr = new Rect(rect.x, rect.yMax + 2f, rect.width, Mathf.Max(2f, h * 0.6f));
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(pr, Texture2D.whiteTexture);
                    if (e.Broken)
                    {
                        // 무너짐 남은 시간: 깜빡이는 노란 막대.
                        float blink = 0.6f + 0.4f * Mathf.Sin(now * 20f);
                        GUI.color = new Color(1f, 0.89f, 0.36f, blink);
                        GUI.DrawTexture(new Rect(pr.x, pr.y, pr.width * Mathf.Clamp01(e.BreakRemaining / 3f), pr.height), Texture2D.whiteTexture);
                    }
                    else
                    {
                        GUI.color = Palette.Poise;
                        GUI.DrawTexture(new Rect(pr.x, pr.y, pr.width * (float)e.Poise.Fraction, pr.height), Texture2D.whiteTexture);
                    }
                }
                // 방어 게이지가 이 막대(체력 + 버팀)를 피해 그려지게 자리를 적어 둔다.
                float barBottom = e.Poise != null ? rect.yMax + 2f + Mathf.Max(2f, h * 0.6f) : rect.yMax;
                _enemyBarRects.Add(Rect.MinMaxRect(rect.x - 1f, rect.y - 1f, rect.xMax + 1f, barBottom + 1f));
                if (!e.Aware) DrawSleep(e, pxPerUnit);
            }
            GUI.color = Color.white;
        }

        void DrawSleep(Enemy e, float pxPerUnit)
        {
            // 먹는 무리는 깨어 있지 않지만 자는 것도 아니다(가끔 '우적' 글자는 CellEncounters가 띄운다). 순찰은 걷는 중이라 'z z'를 띄우지 않는다(1-2층 탐험 맛 1차 4-3).
            if (_style == null || e.IsEating || e.IsPatrolling) return;
            // 던전 어둠: 글자는 빛을 받지 않으므로 등잔 흐린 반경 밖의 잠든 적을 드러내지 않게 한다(3차 초안 2-4 공정 규칙은 눈빛 두 점만).
            var lighting = DungeonLighting.Instance;
            var player = PlayerController.Instance;
            var vision = VisionSystem.Instance;
            // 시야를 켜면 VisionHidden이 아닌 것(시야 안 + 빛 안)만 여기 온다. 시야를 끄면 등잔 흐린 반경으로 가린다.
            bool visionOn = vision && vision.VisionOn;
            if (!visionOn && lighting && lighting.DarknessOn && player && (e.Position - player.Position).magnitude > lighting.DimRadius) return;
            var p = _cam.WorldToScreenPoint(e.Position + new Vector2(e.Radius * 0.6f, e.Radius + 0.45f + 0.1f * Mathf.Sin(Time.unscaledTime * 2f)));
            _style.fontSize = Mathf.RoundToInt(16f * Screen.height / 1080f);
            _style.font = null;
            GUI.color = InDungeon ? DungeonSleep : new Color(0.85f, 0.85f, 0.9f, 0.85f);
            GUI.Label(new Rect(p.x - 30f, Screen.height - p.y - 15f, 60f, 30f), "z z", _style);
        }

        void DrawNumbers()
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, wordWrap = false };
            float now = Time.unscaledTime;
            float uiScale = Screen.height / 1080f;
            bool dungeon = InDungeon;
            Font serif = dungeon ? DungeonUi.Serif : null;
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (now - _entries[i].Born > Life) _entries.RemoveAt(i);
            foreach (var n in _entries)
            {
                float t = (now - n.Born) / Life;
                var screen = _cam.WorldToScreenPoint(n.World + Vector2.up * (0.35f + Rise * t));
                // 위치 글자(백어택·헤드어택)는 두 시험장 모두 바탕체(있으면) + 두 겹 그림자로, 자기 숫자 위로 Lift만큼 올려 그린다.
                bool word = (dungeon && n.Word) || n.Tag;
                _style.font = n.Tag ? DungeonUi.Serif : word ? serif : null;
                _style.fontSize = Mathf.RoundToInt(22f * n.Scale * uiScale);
                var rect = new Rect(screen.x - 80f, Screen.height - screen.y - 20f - n.Lift * uiScale, 160f, 40f);
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), n.Text, _style);
                if (word)
                {
                    // 던전 글자: 어두운 바닥 위에서도 읽히게 반대쪽 그림자를 한 겹 더.
                    GUI.Label(new Rect(rect.x - 1f, rect.y - 1f, rect.width, rect.height), n.Text, _style);
                }
                var c = n.Color;
                c.a = alpha;
                GUI.color = c;
                GUI.Label(rect, n.Text, _style);
            }
            _style.font = null;
            GUI.color = Color.white;
        }
    }
}
