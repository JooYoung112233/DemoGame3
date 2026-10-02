using System.Collections.Generic;
using Demo6.Core.Combat;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Demo6.Game
{
    /// <summary>
    /// 어둠 공정 규칙(3차 초안 2-4).
    /// ① 빛 밖이라도 12유닛 안 적은 흰 눈 두 점(15%)이 보인다. 등잔 밝은 원 밖, 벽에 가리지 않은 적에게만 띄운다(벽 너머 눈은 정보가 새고 어색하다).
    /// ② 처치 날림 몸은 1초 동안 스스로 빛난다(반경 1.5). 적 물체는 날림이 끝나면(약 0.3초) 지워지므로 빛은 따로 만들고 몸을 따라가다 제자리에서 꺼진다.
    /// 눈은 빛을 무시하는 재질(RenderMaterials.MakeUnlit)로 그리고, 매 프레임 새로 만들지 않게 모아 두고 다시 쓴다.
    /// </summary>
    public sealed class DarkVision : MonoBehaviour
    {
        /// <summary>눈빛이 보이는 거리(2-4, 판정 실패 시 14로 늘릴 후보).</summary>
        public const float EyeRange = 12f;
        /// <summary>눈 두 점 밝기(2-4: 15%).</summary>
        public const float EyeAlpha = 0.15f;
        /// <summary>처치 몸빛 시간·반경(2-4).</summary>
        public const float GlowSeconds = 1f;
        public const float GlowInner = 0.5f;
        public const float GlowOuter = 1.5f;
        public const float GlowIntensity = 0.8f;
        /// <summary>몸빛이 마지막에 꺼지는 시간.</summary>
        const float GlowFade = 0.3f;
        const int EyeSorting = 20000;

        public static DarkVision Instance { get; private set; }

        sealed class EyePair
        {
            public GameObject Root;
            public SpriteRenderer Left;
            public SpriteRenderer Right;
        }

        sealed class Glow
        {
            public Light2D Light;
            public Transform Follow;
            public float Age;
        }

        readonly List<EyePair> _eyes = new List<EyePair>();
        readonly List<Glow> _glows = new List<Glow>();
        Transform _eyeRoot;

        void Awake()
        {
            Instance = this;
            _eyeRoot = new GameObject("Dark vision eyes").transform;
            _eyeRoot.SetParent(transform, false);
            CombatEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDestroy()
        {
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            foreach (var g in _glows)
                if (g.Light) Destroy(g.Light.gameObject);
            _glows.Clear();
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            UpdateEyes();
            UpdateGlows();
        }

        void UpdateEyes()
        {
            int used = 0;
            var player = PlayerController.Instance;
            var lighting = DungeonLighting.Instance;
            if (player && lighting && lighting.DarknessOn)
            {
                Vector2 at = player.Position;
                float bright = lighting.BrightRadius;
                var vision = VisionSystem.Instance;
                bool useVision = vision && vision.VisionOn;
                foreach (var e in Enemy.All)
                {
                    if (!e || e.Dead || e.IsDummy || e.Kind == MonsterKind.Nest) continue;
                    Vector2 to = e.Position - at;
                    float d = to.magnitude;
                    if (d > EyeRange) continue;
                    if (useVision)
                    {
                        // 좀보이드식 시야: 시야 안이지만 빛 밖이라 몸을 숨긴 적만 눈 두 점.
                        if (!e.VisionHidden || !e.VisionInSight) continue;
                    }
                    else
                    {
                        if (d <= bright) continue;
                        if (Physics2D.Linecast(at, e.Position, Layers.WallMask)) continue;
                    }
                    Place(GetPair(used++), e);
                }
            }
            for (int i = used; i < _eyes.Count; i++)
                if (_eyes[i].Root.activeSelf) _eyes[i].Root.SetActive(false);
        }

        /// <summary>머리 쪽 두 점. 옆을 보면 그쪽으로 조금 치우친다.</summary>
        static void Place(EyePair pair, Enemy e)
        {
            if (!pair.Root.activeSelf) pair.Root.SetActive(true);
            float r = Mathf.Max(0.15f, e.Radius);
            Vector2 c = (Vector2)e.transform.position + e.VisualJitter;
            Vector2 face = e.FacingDirection;
            float side = Mathf.Abs(face.x) > 0.3f ? Mathf.Sign(face.x) : 0f;
            Vector2 head = c + new Vector2(side * r * 0.35f, r * 0.3f);
            float gap = Mathf.Max(0.1f, r * 0.32f) * (side != 0f ? 0.75f : 1f);
            float size = Mathf.Clamp(r * 0.16f, 0.06f, 0.16f);
            pair.Root.transform.position = head;
            pair.Left.transform.localPosition = new Vector3(-gap * 0.5f, 0f, 0f);
            pair.Right.transform.localPosition = new Vector3(gap * 0.5f, 0f, 0f);
            pair.Left.transform.localScale = new Vector3(size, size, 1f);
            pair.Right.transform.localScale = new Vector3(size, size, 1f);
        }

        EyePair GetPair(int index)
        {
            while (_eyes.Count <= index)
            {
                var root = new GameObject("Eyes");
                root.transform.SetParent(_eyeRoot, false);
                var pair = new EyePair { Root = root, Left = Dot(root.transform, "L"), Right = Dot(root.transform, "R") };
                root.SetActive(false);
                _eyes.Add(pair);
            }
            return _eyes[index];
        }

        static SpriteRenderer Dot(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Circle;
            sr.color = new Color(1f, 1f, 1f, EyeAlpha);
            sr.sortingOrder = EyeSorting;
            RenderMaterials.MakeUnlit(sr);
            return sr;
        }

        /// <summary>처치 날림 몸빛. 둥지·허수아비는 날지 않으므로 뺀다.</summary>
        void OnEnemyKilled(Enemy enemy)
        {
            if (!enemy || enemy.IsDummy || enemy.Kind == MonsterKind.Nest) return;
            var light = DungeonLighting.Point(null, "Kill glow", GlowInner, GlowOuter, GlowIntensity, DungeonLighting.LampColor, false);
            light.transform.position = enemy.transform.position;
            _glows.Add(new Glow { Light = light, Follow = enemy.transform });
        }

        void UpdateGlows()
        {
            float dt = Time.deltaTime;
            for (int i = _glows.Count - 1; i >= 0; i--)
            {
                var g = _glows[i];
                if (!g.Light)
                {
                    _glows.RemoveAt(i);
                    continue;
                }
                // 적 물체가 지워지면(Unity null) 마지막 자리에 남아 꺼진다.
                if (g.Follow) g.Light.transform.position = g.Follow.position;
                g.Age += dt;
                float left = GlowSeconds - g.Age;
                if (left <= 0f)
                {
                    Destroy(g.Light.gameObject);
                    _glows.RemoveAt(i);
                    continue;
                }
                g.Light.intensity = GlowIntensity * Mathf.Clamp01(left / GlowFade);
            }
        }
    }
}
