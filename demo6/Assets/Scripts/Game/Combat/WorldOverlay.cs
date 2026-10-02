using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    public enum NumberKind
    {
        Normal,
        Crit,
        Taken,
        Heal,
    }

    /// <summary>
    /// 기획 3-6 피해 숫자와 적 체력바. 원화·TMP 전 시험용이라 IMGUI로 그린다.
    /// 숫자는 실제 시간 0.7초 동안 0.8유닛 떠오르고, 0.15초 안에 겹치면 좌우로 비킨다. 동시 최대 40개.
    /// </summary>
    public sealed class WorldOverlay : MonoBehaviour
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
        }

        static WorldOverlay _instance;
        readonly List<Entry> _entries = new List<Entry>(MaxNumbers);
        float _lastSpawnTime = -1f;
        Vector2 _lastSpawnPos;
        int _jitterSide = 1;
        Camera _cam;
        GUIStyle _style;

        public static void Number(Vector2 world, int amount, NumberKind kind)
        {
            if (!_instance || !Tuning.DamageNumbers) return;
            _instance.Add(world, amount, kind);
        }

        /// <summary>'무너짐!', '마주침 정리' 같은 짧은 글자. 피해 숫자 끄기와 관계없이 보인다.</summary>
        public static void Text(Vector2 world, string text, Color color)
        {
            if (!_instance) return;
            if (_instance._entries.Count >= MaxNumbers) _instance._entries.RemoveAt(0);
            _instance._entries.Add(new Entry { World = world, Text = text, Color = color, Scale = 1.15f, Born = Time.unscaledTime });
        }

        void Awake() => _instance = this;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Add(Vector2 world, int amount, NumberKind kind)
        {
            float now = Time.unscaledTime;
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
            _entries.Add(new Entry
            {
                World = world,
                Text = kind == NumberKind.Heal ? "+" + amount : amount.ToString(),
                Color = kind switch
                {
                    NumberKind.Crit => Palette.NumberCrit,
                    NumberKind.Taken => Palette.NumberTaken,
                    NumberKind.Heal => Palette.NumberHeal,
                    _ => Palette.NumberNormal,
                },
                Scale = kind == NumberKind.Crit ? 1.4f : 1f,
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
            DrawNumbers();
        }

        void DrawBars()
        {
            float now = Time.unscaledTime;
            float pxPerUnit = Screen.height / (_cam.orthographicSize * 2f);
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || e.VisionHidden) continue;
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
                    GUI.color = new Color(0f, 0f, 0f, 0.65f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = Palette.HealthBar;
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * e.Health.Fraction, rect.height), Texture2D.whiteTexture);
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
                if (!e.Aware) DrawSleep(e, pxPerUnit);
            }
            GUI.color = Color.white;
        }

        void DrawSleep(Enemy e, float pxPerUnit)
        {
            // 먹는 무리는 깨어 있지 않지만 자는 것도 아니다(가끔 '냠' 글자는 CellEncounters가 띄운다).
            if (_style == null || e.IsEating) return;
            // 던전 어둠: 글자는 빛을 받지 않으므로 등잔 흐린 반경 밖의 잠든 적을 드러내지 않게 한다(3차 초안 2-4 공정 규칙은 눈빛 두 점만).
            var lighting = DungeonLighting.Instance;
            var player = PlayerController.Instance;
            var vision = VisionSystem.Instance;
            // 시야를 켜면 VisionHidden이 아닌 것(시야 안 + 빛 안)만 여기 온다. 시야를 끄면 등잔 흐린 반경으로 가린다.
            bool visionOn = vision && vision.VisionOn;
            if (!visionOn && lighting && lighting.DarknessOn && player && (e.Position - player.Position).magnitude > lighting.DimRadius) return;
            var p = _cam.WorldToScreenPoint(e.Position + new Vector2(e.Radius * 0.6f, e.Radius + 0.45f + 0.1f * Mathf.Sin(Time.unscaledTime * 2f)));
            _style.fontSize = Mathf.RoundToInt(16f * Screen.height / 1080f);
            GUI.color = new Color(0.85f, 0.85f, 0.9f, 0.85f);
            GUI.Label(new Rect(p.x - 30f, Screen.height - p.y - 15f, 60f, 30f), "z z", _style);
        }

        void DrawNumbers()
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, wordWrap = false };
            float now = Time.unscaledTime;
            float uiScale = Screen.height / 1080f;
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (now - _entries[i].Born > Life) _entries.RemoveAt(i);
            foreach (var n in _entries)
            {
                float t = (now - n.Born) / Life;
                var screen = _cam.WorldToScreenPoint(n.World + Vector2.up * (0.35f + Rise * t));
                _style.fontSize = Mathf.RoundToInt(22f * n.Scale * uiScale);
                var rect = new Rect(screen.x - 80f, Screen.height - screen.y - 20f, 160f, 40f);
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), n.Text, _style);
                var c = n.Color;
                c.a = alpha;
                GUI.color = c;
                GUI.Label(rect, n.Text, _style);
            }
            GUI.color = Color.white;
        }
    }
}
