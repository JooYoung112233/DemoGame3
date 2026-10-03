using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 불꽃 발자국 불길 하나(장비 문서 6장 '잿불 걸음'): 반경 0.8, 2.5초. 피해·느려짐은 FlameStepsEffect가 모든 불길을 한 번에 보고 넣는다
    /// (불길이 겹쳐도 적 하나는 0.5초에 한 번). 이 컴포넌트는 자리·세기·남은 시간과 도형 임시 그림(주황 원 + 속 불빛 + 테두리)만 맡고,
    /// 시간이 다 되면 스스로 사라진다(효과가 꺼지거나 플레이어가 없어져도 남은 불길은 끝까지 탄다).
    /// 바닥 높이(-70)에 빛을 무시하는 재질로 그린다: 핏자국·시체(-993~-989) 위, 빨간 예고(-60·-59) 아래라 예고가 불에 묻히지 않는다.
    /// </summary>
    public sealed class FlamePatch : MonoBehaviour
    {
        const int GroundOrder = -70;
        const float FadeIn = 0.12f;
        const float FadeOut = 0.5f;

        static readonly Color FillColor = new Color(1f, 0.42f, 0.1f, 0.34f);
        static readonly Color CoreColor = new Color(1f, 0.72f, 0.28f, 0.55f);
        static readonly Color EdgeColor = new Color(1f, 0.5f, 0.15f, 0.4f);

        SpriteRenderer _fill;
        SpriteRenderer _core;
        SpriteRenderer _edge;
        float _phase;

        /// <summary>불길 가운데.</summary>
        public Vector2 Center { get; private set; }
        /// <summary>한 번 태울 때 공격력 배율(%, 깔 때의 세기로 정함).</summary>
        public double Percent { get; private set; }
        /// <summary>깐 시각(게임 시간).</summary>
        public float Born { get; private set; }
        public float Age => Time.time - Born;
        public bool Expired => Age >= (float)LegendRules.FlameLifetime;

        /// <summary>적 몸이 불길에 닿았는가(가운데에서 몸 가장자리까지 반경 0.8 안).</summary>
        public bool Touches(Enemy enemy) =>
            enemy && (enemy.Position - Center).magnitude - enemy.Radius <= (float)LegendRules.FlameRadius;

        public static FlamePatch Spawn(Vector2 at, double percent)
        {
            var go = new GameObject("FlamePatch");
            go.transform.position = at;
            var patch = go.AddComponent<FlamePatch>();
            patch.Center = at;
            patch.Percent = percent;
            patch.Born = Time.time;
            patch._phase = UnityEngine.Random.value * 10f;
            float size = (float)LegendRules.FlameRadius * 2f;
            patch._fill = Part(go.transform, "Fill", ShapeSprites.Circle, size, FillColor, GroundOrder);
            patch._core = Part(go.transform, "Core", ShapeSprites.Glow, size * 0.9f, CoreColor, GroundOrder + 1);
            patch._edge = Part(go.transform, "Edge", ShapeSprites.Ring, size, EdgeColor, GroundOrder + 2);
            patch.Refresh();
            return patch;
        }

        static SpriteRenderer Part(Transform parent, string name, Sprite sprite, float size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            RenderMaterials.MakeUnlit(sr);
            return sr;
        }

        void Update()
        {
            if (Expired)
            {
                Destroy(gameObject);
                return;
            }
            Refresh();
        }

        /// <summary>깜빡임과 나타나기·사라지기. 게임 시간으로 흘러 히트스톱 동안 멈춘다.</summary>
        void Refresh()
        {
            float age = Age;
            float life = (float)LegendRules.FlameLifetime;
            float fade = Mathf.Clamp01(age / FadeIn) * Mathf.Clamp01((life - age) / FadeOut);
            float flicker = 0.82f + 0.18f * Mathf.Sin((Time.time + _phase) * 23f) * Mathf.Sin((Time.time + _phase) * 7f);
            Set(_fill, FillColor, fade * flicker);
            Set(_core, CoreColor, fade * (0.7f + 0.3f * flicker));
            Set(_edge, EdgeColor, fade);
            if (_core) _core.transform.localScale = Vector3.one * ((float)LegendRules.FlameRadius * 2f * (0.85f + 0.08f * flicker));
        }

        static void Set(SpriteRenderer sr, Color color, float alpha)
        {
            if (!sr) return;
            color.a *= alpha;
            sr.color = color;
        }
    }
}
