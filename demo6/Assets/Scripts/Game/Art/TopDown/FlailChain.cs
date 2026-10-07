using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 사슬 철퇴 사슬 선과 쇠공(기획/전투-보스-무기-다듬기-1차.md 2-2 '0줄 + 사슬 선', 2-7 '사슬 철퇴 2장(손잡이 + 쇠공, 사슬은 코드 선)').
    /// 손잡이 끝 고리에서 쇠공까지 사슬 고리(TopDownSprites.FlailLink) 10개를 눕힘·세움으로 번갈아 놓고, 쇠공은 사슬 방향으로 돌려 놓는다.
    /// 쇠공이 빠르게 돌면 지나온 자리에 옅은 잔상 3개를 남긴다. 관성(PlayerController.InertiaActive)이면 잔상이 두 배 길게 끌린다(궤적이 더 무겁게 보임).
    /// 쇠공 밑에는 작은 바닥 그림자를 깔아 회오리(몸이 도는 칼날)와 달리 '바닥 가까이 도는 쇠뭉치'로 읽히게 한다.
    /// TopDownPlayerRig이 무기가 사슬 철퇴일 때 만들고(부모 = 리그 틀) 매 LateUpdate 몬다. 할당은 만들 때만 한다.
    /// </summary>
    public sealed class FlailChain
    {
        const int Links = 10;
        const int Ghosts = 3;
        const int History = 24;
        /// <summary>잔상이 나는 쇠공 빠르기(유닛/초). 돌려 치기 판정 무렵은 초당 20 안팎이다.</summary>
        const float GhostSpeed = 5f;
        static readonly float[] GhostLag = { 0.018f, 0.036f, 0.054f };
        static readonly float[] GhostAlpha = { 0.3f, 0.18f, 0.09f };

        readonly Transform _parent;
        readonly SpriteRenderer[] _links = new SpriteRenderer[Links];
        readonly SpriteRenderer[] _ghosts = new SpriteRenderer[Ghosts];
        readonly SpriteRenderer _ball;
        readonly SpriteRenderer _shadow;
        readonly Vector3[] _trail = new Vector3[History];
        readonly float[] _trailTime = new float[History];
        int _trailHead;
        int _trailCount;
        bool _shown;
        Color _tint = Color.white;
        int _order;
        bool _dragging;
        float _ballScale = 1f;

        /// <summary>parent = 리그 틀(몸 틀 회전·웅크림 크기를 따름). material = 리그 부품 재질. like = 정렬 층·시야 숨김을 따를 몸.</summary>
        public FlailChain(Transform parent, Material material, SpriteRenderer like)
        {
            _parent = parent;
            for (int i = 0; i < Links; i++)
                _links[i] = TopDownView.Part("FlailLink" + i, parent, TopDownSprites.FlailLink, material, Color.white, like);
            for (int i = 0; i < Ghosts; i++)
                _ghosts[i] = TopDownView.Part("FlailGhost" + i, parent, TopDownSprites.FlailBall, material, new Color(1f, 1f, 1f, 0f), like);
            _ball = TopDownView.Part("FlailBall", parent, TopDownSprites.FlailBall, material, Color.white, like);
            _shadow = TopDownView.Part("FlailBallShadow", parent, TopDownSprites.Shadow, material, new Color(0f, 0f, 0f, 0.35f), like);
            Hide();
        }

        /// <summary>손잡이 끝(월드)과 쇠공 자리(월드)로 사슬을 다시 그린다(색·정렬은 마지막 Place 값).</summary>
        public void Draw(Vector2 handleTip, Vector2 ball, float alpha)
        {
            if (!_parent) return;
            Vector2 a = _parent.InverseTransformPoint(handleTip);
            Vector2 b = _parent.InverseTransformPoint(ball);
            var tint = _tint;
            tint.a = alpha;
            Place(a, b, tint, _order, _dragging, null, _ballScale);
        }

        /// <summary>
        /// 리그 틀 좌표(유닛)로 놓는다. tint = 밝기·투명도(리그의 몸 밝기), order = 사슬 정렬(쇠공은 +1, 잔상은 −3), dragging = 관성(잔상을 길게),
        /// ballSprite = 쇠공 그림(그림 칸이 있으면 그것, null이면 그대로), ballScale = 쇠공 크기(내리꽂기에서 들려 커짐·처형 칼 배율, 평소 1).
        /// </summary>
        public void Place(Vector2 tip, Vector2 ball, Color tint, int order, bool dragging, Sprite ballSprite, float ballScale = 1f)
        {
            if (!_parent) return;
            _tint = tint;
            _order = order;
            _dragging = dragging;
            _ballScale = ballScale;
            if (!_shown) Show();
            if (ballSprite && _ball.sprite != ballSprite)
            {
                _ball.sprite = ballSprite;
                for (int i = 0; i < Ghosts; i++) _ghosts[i].sprite = ballSprite;
            }

            Vector2 d = ball - tip;
            float angle = d.sqrMagnitude > 1e-8f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
            var rot = Quaternion.Euler(0f, 0f, angle);
            for (int i = 0; i < Links; i++)
            {
                var link = _links[i];
                var t = link.transform;
                Vector2 at = Vector2.Lerp(tip, ball, (i + 0.5f) / Links);
                t.localPosition = new Vector3(at.x, at.y, 0f);
                t.localRotation = rot;
                // 눕힌 고리와 세운 고리를 번갈아(세운 고리는 위에서 얇은 막대로 보임).
                t.localScale = new Vector3(1f, i % 2 == 0 ? 1f : 0.38f, 1f);
                link.color = tint;
                link.sortingOrder = order;
            }

            var bt = _ball.transform;
            bt.localPosition = new Vector3(ball.x, ball.y, 0f);
            bt.localRotation = rot;
            bt.localScale = new Vector3(ballScale, ballScale, 1f);
            _ball.color = tint;
            _ball.sortingOrder = order + 1;

            // 바닥 그림자(빛 방향 없이 조금 아래로), 그림자 순서는 리그 몸 그림자와 같다.
            var st = _shadow.transform;
            st.localPosition = new Vector3(ball.x + 0.02f, ball.y - 0.03f, 0f);
            st.localScale = new Vector3(0.3f, 0.26f, 1f);
            _shadow.color = new Color(0f, 0f, 0f, 0.35f * tint.a);
            _shadow.sortingOrder = TopDownPlayerRig.ShadowOrder;

            Trail(bt.position, tint, order, dragging);
        }

        /// <summary>그리기를 숨긴다(다른 무기·리그 끔). 잔상 기록도 지운다.</summary>
        public void Hide()
        {
            _shown = false;
            _trailCount = 0;
            for (int i = 0; i < Links; i++)
                if (_links[i]) _links[i].enabled = false;
            for (int i = 0; i < Ghosts; i++)
                if (_ghosts[i]) _ghosts[i].enabled = false;
            if (_ball) _ball.enabled = false;
            if (_shadow) _shadow.enabled = false;
        }

        void Show()
        {
            _shown = true;
            for (int i = 0; i < Links; i++) _links[i].enabled = true;
            _ball.enabled = true;
            _shadow.enabled = true;
        }

        /// <summary>쇠공 지나온 자리(월드)를 기록하고, 빠르게 돌 때 lag초 전 자리에 옅은 잔상을 놓는다. 관성이면 lag가 두 배.</summary>
        void Trail(Vector3 world, Color tint, int order, bool dragging)
        {
            float now = Time.time;
            int last = (_trailHead + History - 1) % History;
            // 같은 시각(히트스톱·한 프레임 두 번)이면 덮어쓴다.
            if (_trailCount > 0 && _trailTime[last] >= now) _trail[last] = world;
            else
            {
                _trail[_trailHead] = world;
                _trailTime[_trailHead] = now;
                _trailHead = (_trailHead + 1) % History;
                if (_trailCount < History) _trailCount++;
            }

            float speed = 0f;
            if (_trailCount >= 2)
            {
                int prev = (_trailHead + History - 2) % History;
                float dt = _trailTime[(_trailHead + History - 1) % History] - _trailTime[prev];
                if (dt > 1e-4f) speed = (world - _trail[prev]).magnitude / dt;
            }
            bool on = speed > GhostSpeed;
            float lagScale = dragging ? 2f : 1f;
            for (int i = 0; i < Ghosts; i++)
            {
                var g = _ghosts[i];
                if (!on || !Sample(now - GhostLag[i] * lagScale, out var at))
                {
                    if (g.enabled) g.enabled = false;
                    continue;
                }
                if (!g.enabled) g.enabled = true;
                g.transform.position = at;
                g.transform.localRotation = _ball.transform.localRotation;
                g.transform.localScale = _ball.transform.localScale;
                g.color = new Color(tint.r, tint.g, tint.b, tint.a * GhostAlpha[i] * (dragging ? 1.35f : 1f));
                g.sortingOrder = order - 3;
            }
        }

        /// <summary>기록에서 그 시각의 자리를 사이 보간으로 찾는다(기록보다 이르면 false).</summary>
        bool Sample(float time, out Vector3 at)
        {
            at = default;
            for (int n = 1; n < _trailCount; n++)
            {
                int i = (_trailHead + History - n) % History;
                int j = (_trailHead + History - n - 1) % History;
                if (_trailTime[j] > time) continue;
                float span = _trailTime[i] - _trailTime[j];
                float k = span > 1e-5f ? Mathf.Clamp01((time - _trailTime[j]) / span) : 1f;
                at = Vector3.Lerp(_trail[j], _trail[i], k);
                return true;
            }
            return false;
        }
    }
}
