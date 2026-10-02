using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 판자벽·금 간 벽·광맥이 부서질 때 튀는 조각. 3차 초안 2-4 '손맛 효과도 빛을 무시한다'에 따라 빛을 받지 않는 재질로 그린다.
    /// HitEffects 파편처럼 게임 시간으로 움직여 히트스톱 동안 멈춘다. 다 날면 스스로 지운다.
    /// </summary>
    public sealed class WorldDebris : MonoBehaviour
    {
        const float Life = 0.65f;
        const int SortOrder = 3150;

        Transform[] _pieces;
        SpriteRenderer[] _sprites;
        Vector2[] _velocity;
        float[] _spin;
        Color[] _colors;
        float _age;

        /// <param name="center">부서진 자리 가운데.</param>
        /// <param name="area">조각이 처음 놓이는 범위(가로 × 세로).</param>
        /// <param name="dir">조각이 주로 튀는 방향(때린 쪽에서 먼 쪽).</param>
        /// <param name="a">조각 색 하나(두 색 사이에서 고른다).</param>
        public static void Burst(Vector2 center, Vector2 area, Vector2 dir, Color a, Color b, int count)
        {
            if (count <= 0) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            var go = new GameObject("WorldDebris");
            go.transform.position = center;
            var d = go.AddComponent<WorldDebris>();
            d._pieces = new Transform[count];
            d._sprites = new SpriteRenderer[count];
            d._velocity = new Vector2[count];
            d._spin = new float[count];
            d._colors = new Color[count];
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            for (int i = 0; i < count; i++)
            {
                var p = new GameObject("Piece");
                p.transform.SetParent(go.transform, false);
                p.transform.position = center + new Vector2(Random.Range(-0.5f, 0.5f) * area.x, Random.Range(-0.5f, 0.5f) * area.y);
                p.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
                float len = Random.Range(0.12f, 0.34f);
                p.transform.localScale = new Vector3(len, len * Random.Range(0.3f, 0.7f), 1f);
                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = ShapeSprites.Square;
                var c = Color.Lerp(a, b, Random.value);
                sr.color = c;
                sr.sortingOrder = SortOrder;
                RenderMaterials.MakeUnlit(sr);
                // 대부분 앞쪽으로, 일부는 옆으로 흩어진다.
                float ang = baseAngle + Random.Range(-1.1f, 1.1f);
                d._velocity[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(2.5f, 7f);
                d._spin[i] = Random.Range(-540f, 540f);
                d._pieces[i] = p.transform;
                d._sprites[i] = sr;
                d._colors[i] = c;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _age += dt;
            if (_age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            float k = 1f - _age / Life;
            float drag = Mathf.Max(0f, 1f - 5f * dt);
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (!_pieces[i]) continue;
                _velocity[i] *= drag;
                _pieces[i].position += (Vector3)(_velocity[i] * dt);
                _pieces[i].Rotate(0f, 0f, _spin[i] * dt);
                var c = _colors[i];
                c.a *= k;
                _sprites[i].color = c;
            }
        }
    }
}
