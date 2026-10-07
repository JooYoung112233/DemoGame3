using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 타격감 보강(M0a 판정 '시원함 부족' 반영): 맞은 자리의 파편과 베인 자국.
    /// 파편은 게임 시간으로 움직여 히트스톱 동안 멈춘다(맞는 순간이 한 장면으로 보이게).
    /// 스프라이트 묶음을 미리 만들어 돌려 쓴다. 다크 판타지 1차부터 살 있는 적의 파편은 피 색이고,
    /// 맞을 때마다 GoreSystem(피 튀김·얼룩·조각·시체)을 함께 부른다.
    /// </summary>
    public sealed class HitEffects : MonoBehaviour
    {
        const int PoolSize = 320;
        const int SortOrder = 3200;

        struct Shard
        {
            public SpriteRenderer Sprite;
            public Vector2 Velocity;
            public float Life;
            public float Age;
            public float Size;
            public Color Color;
            public bool Mark;
            public bool ArtSprite;
        }

        static HitEffects _instance;
        Shard[] _shards;
        int _next;

        /// <summary>껍질에 막힌 타격: 짧은 뼛빛 파편만 튄다.</summary>
        public static void OnBlocked(Vector2 at, Vector2 dir)
        {
            if (!_instance || !Tuning.HitSparks) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            if (ImportedFxV056.Blocked(at, -dir.normalized, 3)) return;
            _instance.Burst(at, -dir.normalized, 3, new Color(.82f,.78f,.64f), new Color(.58f,.53f,.40f), 0.65f);
        }

        /// <summary>
        /// 벽·기둥 박기(WallSlam, 기획/전투-보스-무기-다듬기-1차.md 4-2 [1]): 닿은 벽 자리에서 벽 바깥(normal) 반원으로 흙먼지 count알.
        /// 타격 파편보다 크고 느리게 퍼지며 조금 더 오래 남는다. 게임 시간이라 히트스톱 동안 멈춘다. 파편 끄기(Tuning.HitSparks)와 따로 간다.
        /// </summary>
        public static void WallDust(Vector2 at, Vector2 normal, int count)
        {
            if (!_instance || count <= 0) return;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;
            normal.Normalize();
            if (ImportedFxV056.WallDust(at, normal, count)) return;
            float baseAngle = Mathf.Atan2(normal.y, normal.x);
            for (int i = 0; i < count; i++)
            {
                // 벽 바깥 반원에 고르게 펼치고 조각마다 조금씩 흔든다.
                float spread = count > 1 ? Mathf.Lerp(-1.25f, 1.25f, i / (float)(count - 1)) : 0f;
                float a = baseAngle + spread + Random.Range(-0.2f, 0.2f);
                ref var s = ref _instance.Take();
                s.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.4f, 2.8f);
                s.Life = Random.Range(0.35f, 0.55f);
                s.Age = 0f;
                s.Size = Random.Range(0.14f, 0.24f);
                var c = Color.Lerp(GoreColors.DirtLight, WallDustLight, Random.Range(0.3f, 0.8f));
                c.a = 0.75f;
                s.Color = c;
                s.Mark = false;
                s.ArtSprite = false;
                s.Sprite.sprite = ShapeSprites.Circle;
                s.Sprite.transform.position = at + normal * 0.05f + Random.insideUnitCircle * 0.1f;
                s.Sprite.transform.rotation = Quaternion.identity;
                // 히트스톱(dt=0) 동안은 Update가 건너뛰므로 처음 모습을 여기서 넣는다.
                s.Sprite.color = s.Color;
                s.Sprite.transform.localScale = new Vector3(s.Size, s.Size, 1f);
                s.Sprite.enabled = true;
            }
        }

        /// <summary>흙먼지 밝은 쪽(빛을 받지 않는 파편이라 어둠 위에서 너무 튀지 않게 흐린 흙빛).</summary>
        static readonly Color WallDustLight = new Color(.66f, .60f, .50f);

        /// <summary>
        /// 적을 맞힌 순간(피해·넉백 뒤라 처치면 이미 Dead). 치명이면 보통 치명 연출(예전 치명 그대로)로 낸다.
        /// 치명 단계를 아는 곳(PlayerController)은 CritTier 판을 부른다.
        /// </summary>
        public static void OnHit(Enemy enemy, Vector2 dir, bool crit, bool heavy, int extraSparks = 0) =>
            OnHit(enemy, dir, crit ? CritTier.Normal : CritTier.None, heavy, extraSparks);

        /// <summary>
        /// 적을 맞힌 순간(피해·넉백 뒤라 처치면 이미 Dead). 소수의 방향성 파편과 대상 크기에 맞는 베인 자국을 남긴다.
        /// 피 튀김·얼룩·조각·시체는 GoreSystem이 맡으므로 여기서 같은 피 조각을 과도하게 겹치지 않는다.
        /// </summary>
        public static void OnHit(Enemy enemy, Vector2 dir, CritTier tier, bool heavy, int extraSparks = 0) =>
            OnHit(enemy, dir, tier, heavy, extraSparks, false);

        /// <summary>
        /// 둔탁한 타격을 가리는 판(ComboStep.blunt, 한손검과 방패 ② 방패 치기 — 기획/세-무기-우클릭-소켓-1차.md 2-3).
        /// blunt면 피를 줄인다: 살 있는 적의 피 파편은 셋에 하나만 남기고 나머지는 뼛빛·흙빛 부스러기로, 살아 있는 적에게는 GoreSystem 피 튀김·얼룩을 내지 않는다
        /// (처치하면 처치 피·조각·시체는 그대로). blunt가 아니면 OnHit(enemy, dir, tier, heavy, extraSparks)와 난수 차례까지 같다.
        /// </summary>
        public static void OnHit(Enemy enemy, Vector2 dir, CritTier tier, bool heavy, int extraSparks, bool blunt)
        {
            if (!_instance || !enemy) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            Vector2 at = enemy.Position;
            bool strong = tier >= CritTier.Normal;
            if (Tuning.HitSparks)
            {
                int count = (strong ? 5 : heavy ? 4 : 2) + Mathf.Clamp(extraSparks, 0, 2);
                float speed = strong ? 1.05f : 0.8f;
                // 가벼운 치명: 보통 타 위에 노란 불꽃 3개(치명이 읽히되 가볍게).
                int lightSparks = tier == CritTier.Light ? 1 : 0;
                switch (GoreColors.MatterOf(enemy))
                {
                    case GoreMatter.Flesh:
                        // 치명은 노란 불꽃 3개를 남겨 치명이 읽히게 한다(나머지는 피).
                        int critSparks = strong ? 2 : 0;
                        if (blunt)
                        {
                            // 둔탁한 타격: 피는 셋에 하나(최소 1), 나머지는 뼛빛 부스러기를 조금 느리게.
                            int rest = count - critSparks;
                            int blood = Mathf.Max(1, rest / 3);
                            _instance.Burst(at, dir, blood, GoreColors.BloodMid, GoreColors.BloodBright, speed);
                            _instance.Burst(at, dir, rest - blood, BluntChipA, BluntChipB, speed * 0.8f);
                        }
                        else _instance.Burst(at, dir, count - critSparks, GoreColors.BloodMid, GoreColors.BloodBright, speed);
                        if (critSparks > 0) _instance.Burst(at, dir, critSparks, CritGlint, CritGlint, speed);
                        if (enemy.Dead) _instance.Burst(at, dir, 2, GoreColors.BloodDark, GoreColors.Darken(GoreColors.BodyColor(enemy)), 0.65f);
                        break;
                    case GoreMatter.Nest:
                        _instance.Burst(at, dir, count, GoreColors.Dirt, GoreColors.Pus, speed);
                        if (enemy.Dead) _instance.Burst(at, dir, 2, GoreColors.DirtLight, GoreColors.PusDark, 0.65f);
                        break;
                    default:
                        Color body = Color.Lerp(GoreColors.BodyColor(enemy), new Color(.8f,.75f,.64f), 0.35f);
                        Color first = strong ? CritGlint : body;
                        _instance.Burst(at, dir, count, first, first, speed);
                        if (enemy.Dead) _instance.Burst(at, dir, 2, GoreColors.BodyColor(enemy), GoreColors.BodyColor(enemy), 0.65f);
                        break;
                }
                if (lightSparks > 0) _instance.Burst(at, dir, lightSparks, CritGlint, CritGlint, 0.8f);
                if (!ContactReactionV046.Hit(enemy, dir))
                    _instance.SlashMark(at, dir, enemy.Radius, strong || heavy);
            }
            // 둔탁한 타격으로 아직 살아 있으면 피 튀김·얼룩을 내지 않는다(처치 고어는 그대로).
            if (blunt && !enemy.Dead) return;
            GoreSystem.EnemyHit(enemy, dir, tier, heavy);
        }

        static readonly Color CritGlint = new Color(.88f,.70f,.38f,.9f);
        /// <summary>둔탁한 타격 부스러기(뼛빛 ~ 흙빛).</summary>
        static readonly Color BluntChipA = new Color(.80f,.76f,.64f);
        static readonly Color BluntChipB = new Color(.55f,.49f,.39f);

        /// <summary>
        /// 불꽃·부스러기 count개를 dir 쪽으로 튀긴다(무기 행동 연출: 패링 흰 불꽃 8개, 막음 작은 불꽃 — WeaponActFx).
        /// spread = 퍼지는 반각(라디안, 타격 파편은 0.55). 게임 시간이라 히트스톱 동안 멈춘다. 파편 끄기(Tuning.HitSparks)를 따른다.
        /// </summary>
        public static void Sparks(Vector2 at, Vector2 dir, int count, Color colorA, Color colorB, float speedScale, float spread)
        {
            if (!_instance || !Tuning.HitSparks || count <= 0) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            _instance.Burst(at, dir.normalized, count, colorA, colorB, speedScale, Mathf.Clamp(spread, 0.05f, Mathf.PI));
        }

        void Awake()
        {
            _instance = this;
            _shards = new Shard[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Shard");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ShapeSprites.Square;
                sr.sortingOrder = SortOrder;
                sr.enabled = false;
                RenderMaterials.MakeUnlit(sr);
                _shards[i] = new Shard { Sprite = sr, Life = 0f };
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>파편 count개. 색은 조각마다 colorA~colorB 사이에서 고른다(피는 밝고 어두운 검붉음이 섞이게). spread = 퍼지는 반각(라디안).</summary>
        void Burst(Vector2 at, Vector2 dir, int count, Color colorA, Color colorB, float speedScale, float spread = 0.55f)
        {
            var art = ArtRuntime.Active;
            var sparks = art ? art.effects.sparks : null;
            bool useArt = sparks != null && sparks.Length > 0;
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            for (int i = 0; i < count; i++)
            {
                float a = baseAngle + Random.Range(-spread, spread);
                float speed = Random.Range(3f, 6.5f) * speedScale;
                ref var s = ref Take();
                s.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                s.Life = Random.Range(0.16f, 0.3f);
                s.Age = 0f;
                s.Size = Random.Range(0.09f, 0.16f);
                s.Color = Color.Lerp(colorA, colorB, Random.value);
                s.Mark = false;
                s.ArtSprite = useArt;
                s.Sprite.sprite = useArt ? sparks[Random.Range(0, sparks.Length)] : ShapeSprites.Square;
                s.Sprite.transform.position = at + Random.insideUnitCircle * 0.12f;
                s.Sprite.transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                // 히트스톱(dt=0) 동안은 Update가 건너뛰므로 처음 모습(색·크기)을 여기서 넣는다.
                s.Sprite.color = s.Color;
                float sc = useArt ? s.Size / Mathf.Max(.001f, s.Sprite.sprite.bounds.size.x) : s.Size;
                s.Sprite.transform.localScale = new Vector3(sc, sc, 1f);
                s.Sprite.enabled = true;
            }
        }

        /// <summary>
        /// 칼자국 하나를 자리·각(긴 쪽 방향, °)·길이(유닛)·시간을 정해 띄운다(쌍검 난사 이펙트 SwingVisual.ShowFlurry, 그림만). 파편 끄기(Tuning.HitSparks)를 따른다.
        /// </summary>
        public static void SlashMarkAt(Vector2 at, float angleDeg, float length, float life, bool strong)
        {
            if (!_instance || !Tuning.HitSparks) return;
            _instance.Mark(at, angleDeg, Mathf.Max(0.05f, length), Mathf.Max(0.02f, life), strong);
        }

        /// <summary>몸을 덮지 않는 짧은 뼛빛 칼자국. 공격 방향과 직각으로 일관되게 놓는다.</summary>
        void SlashMark(Vector2 at, Vector2 dir, float radius, bool strong) =>
            Mark(at, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f, Mathf.Clamp(radius * 1.8f, 0.35f, 1.3f), strong ? 0.12f : 0.09f, strong);

        void Mark(Vector2 at, float angle, float size, float life, bool strong)
        {
            ref var s = ref Take();
            s.Velocity = Vector2.zero;
            s.Life = life;
            s.Age = 0f;
            s.Size = size;
            s.Color = new Color(.94f,.87f,.70f, strong ? .9f : .72f);
            s.Mark = true;
            var art = ArtRuntime.Active;
            var mark = art ? art.effects.slashMark : null;
            s.ArtSprite = mark;
            s.Sprite.sprite = mark ? mark : ShapeSprites.Square;
            s.Sprite.transform.position = at;
            s.Sprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            // 그림 자국은 가로 1유닛 기준으로 길이만 맞춘다.
            s.Sprite.transform.localScale = mark
                ? new Vector3(s.Size / Mathf.Max(0.01f, mark.bounds.size.x), (strong ? .13f : .09f) / Mathf.Max(.001f, mark.bounds.size.y), 1f)
                : new Vector3(s.Size, strong ? 0.09f : 0.06f, 1f);
            s.Sprite.color = s.Color;
            s.Sprite.enabled = true;
        }

        ref Shard Take()
        {
            ref var s = ref _shards[_next];
            _next = (_next + 1) % PoolSize;
            return ref s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < _shards.Length; i++)
            {
                ref var s = ref _shards[i];
                if (!s.Sprite.enabled) continue;
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    s.Sprite.enabled = false;
                    continue;
                }
                float k = 1f - s.Age / s.Life;
                var c = s.Color;
                c.a *= k;
                s.Sprite.color = c;
                if (s.Mark) continue;
                s.Velocity *= Mathf.Max(0f, 1f - 6f * dt);
                s.Sprite.transform.position += (Vector3)(s.Velocity * dt);
                float size = s.Size * (0.5f + 0.5f * k);
                // 그림 조각은 제 크기(약 0.1유닛)를 기준으로 늘이고 줄인다.
                float scale = s.ArtSprite ? size / Mathf.Max(.001f, s.Sprite.sprite.bounds.size.x) : size;
                s.Sprite.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
