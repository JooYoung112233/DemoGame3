using Demo6.Core.Combat;
using UnityEngine;
using System.Collections.Generic;

namespace Demo6.Game
{
    /// <summary>
    /// 기존 타격 시점에 칼끝 잔상을 잠깐 남긴다(실제 시간). 충돌 판정과 독립인 시각 전용 표현이다.
    /// 부채꼴은 단계마다 방향을 엇갈리고, 대검의 내려찍기는 지면 먼지로 구분한다.
    /// </summary>
    public sealed class SwingVisual : MonoBehaviour
    {
        SpriteRenderer _sprite;
        float _age;
        float _life;
        Color _color;
        float _growFrom = 1f;
        Vector3 _baseScale;
        bool _line;
        Vector2 _lineOrigin;
        Vector2 _lineDir;
        float _lineLength;
        float _turn;
        static readonly Dictionary<int, Sprite> Trails = new Dictionary<int, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTrails()
        {
            foreach(var s in Trails.Values) if(s){Destroy(s.texture);Destroy(s);}
            Trails.Clear();
        }

        // 채워진 흰 부채 대신 칼끝 부근의 가늘고 끝이 사라지는 호. 내부의 적을 가리지 않는다.
        static Sprite Trail(float arc,float thickness)
        {
            int key=Mathf.RoundToInt(arc)*1000+Mathf.RoundToInt(thickness*1000);
            if(Trails.TryGetValue(key,out var cached))return cached;
            const int n=256;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Weapon edge trail",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++){
                var p=new Vector2((x+.5f)/n*2-1,(y+.5f)/n*2-1);float angle=Mathf.Abs(Mathf.Atan2(p.y,p.x)*Mathf.Rad2Deg);float taper=Mathf.Clamp01((arc*.5f-angle)/Mathf.Max(8,arc*.20f));float radial=Mathf.Clamp01((1-Mathf.Abs(p.magnitude-(.95f-thickness*.5f))/(thickness*.5f))*2);
                pixels[y*n+x]=new Color(1,1,1,radial*taper);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);var sprite=Sprite.Create(texture,new Rect(0,0,n,n),Vector2.one*.5f,n*.5f);Trails.Add(key,sprite);return sprite;
        }

        public static void ShowStep(WeaponAttackRule weapon, int stepIndex, ComboStep step, Vector2 origin, Vector2 dir, int hitIndex)
        {
            var player = PlayerController.Instance;
            var motion=TopDownView.PlayerRig?.FirstAttackArt;
            if (player && weapon == player.Weapon && motion?.Active == true &&
                ((player.Pose == PlayerPose.Attack && (stepIndex == 0 || motion.OwnsExtendedMotions)) ||
                 (motion.OwnsExtendedMotions && player.InWeaponAct &&
                  (player.ActPhase == WeaponActPhase.Release || player.ActPhase == WeaponActPhase.Flurry)))) return;
            int stepNumber = stepIndex + 1;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // 그림 이펙트가 있으면 조준 방향으로 돌려 재생한다(원형은 원 중심, 부채꼴·찌르기는 검사 중심 기준).
            var set = ArtRuntime.Active;
            var stepArt = set ? set.Weapon(weapon.id)?.Step(stepIndex) : null;
            if (stepArt != null && stepArt.slash.Has)
            {
                Vector2 at = step.shape == ComboShape.Circle ? origin + dir * step.centerOffset : origin;
                bool mirror = stepArt.mirrorAlternate && (stepNumber + hitIndex) % 2 == 0;
                ClipVfx.Play(stepArt.slash, at, step.shape == ComboShape.Circle ? 0f : angle, stepArt.slashScale, mirror);
                return;
            }
            bool strong = step.finisher;
            // 쇠망치는 대검처럼 '무거움' 색·흙먼지를 쓰고 부채꼴은 장검 호를 다시 쓴다(기획/전투-보스-무기-다듬기-1차.md 2-7 '새로 그리지 않음').
            // 사슬 철퇴 원은 돌지 않는 흙먼지(회오리와 구별), 단검 호는 쌍검처럼 가늘다.
            bool maul=weapon.id=="wpn_maul", flail=weapon.id=="wpn_flail", dagger=weapon.id=="wpn_dagger";
            bool heavy=weapon.id=="wpn_greatsword"||maul, twin=weapon.id=="wpn_twinblades";
            var effects = set ? set.effects : null;
            var color = heavy ? new Color(.79f,.70f,.51f,strong?.78f:.70f) : twin ? new Color(.73f,.83f,.82f,.76f) : new Color(.91f,.88f,.75f,strong?.82f:.76f);
            float life = heavy ? .18f : twin ? .085f : .12f;
            switch (step.shape)
            {
                case ComboShape.Line:
                {
                    var sprite = effects != null && effects.thrust ? effects.thrust : ShapeSprites.Square;
                    var v = Create(sprite, origin + dir * (step.size * 0.5f), angle, Fit(sprite, step.size, step.width * 0.22f), color, life);
                    v._growFrom = 0.4f;
                    v._line = true;
                    v._lineOrigin = origin;
                    v._lineDir = dir;
                    v._lineLength = step.size;
                    break;
                }
                case ComboShape.Circle:
                {
                    Vector2 center = origin + dir * step.centerOffset;
                    var sprite = heavy ? (effects != null ? effects.impactDust : null) : (twin && effects != null ? effects.whirl : null);
                    if (!sprite) sprite = LootVisuals.DustRing;
                    var ring = Create(sprite, center, angle, Fit(sprite, step.size * 2f, step.size * 2f), heavy || flail ? new Color(.57f,.48f,.34f,.68f) : color, life);
                    ring._growFrom = 0.55f;
                    if (!heavy && !flail) ring._turn = 30f;
                    break;
                }
                default:
                {
                    // 홀수·짝수 단계와 타를 엇갈려 좌우로 베는 느낌을 준다.
                    bool flip = (stepNumber + hitIndex) % 2 == 0;
                    var sprite = effects == null ? null : heavy && !maul ? effects.arcHeavy : twin ? effects.arcTwin : weapon.id == "wpn_longsword" || maul ? effects.arcLong : null;
                    if (!sprite) sprite = Trail(step.arcDeg,heavy?.18f:twin||dagger?.05f:.09f);
                    var v = Create(sprite, origin, angle, Fit(sprite, step.size * 2f, step.size * 2f), color, life);
                    v._sprite.flipY = flip;
                    // 쌍검 ④ 가위 가르기 셋째 타(꽉 닫힌 X): 몸 앞에서 엇갈리는 베인 자국 둘을 겹쳐 X로 보인다(그림만, 호는 좌우가 같아 뒤집어도 X가 안 보임).
                    if (twin && step.finisher && step.hits >= 3 && hitIndex == step.hits - 1)
                    {
                        Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
                        FlurryMark(origin, d, 0.95f, -14f, 55f, 0.75f, 0.14f, true);
                        FlurryMark(origin, d, 0.95f, 14f, -55f, 0.75f, 0.14f, true);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 쌍검 난사 한 타 이펙트(2026-10-05 사용자 원문 "모션은 엇베기랑비슷한데 이펙트로 여러번때리는것처럼해주면될것같아", spec 난사 '이펙트'). index = 0부터 판정 번호.
        /// 작은 타: 쌍검 ③ 엇베기 그림(단계 번호 2)을 동작과 따로 둔 방향 표(TwinFlurry.EffectDirDeg)만큼 돌려 보이고(왼손 타는 뒤집음),
        /// 짧은 베인 자국 2개(반지름 0.9·1.4, 표 각·표 각 ± 12°, 기울기 ±50°, 0.08~0.09초)를 띄운다. X가 생기는 2·4·6번째 타에는 몸 앞 0.6에 흰 번쩍임(불꽃 3개).
        /// 마지막 X: ③ 그림 두 장(하나는 뒤집어) X + 자국 4개 + 불꽃 6개. 판정(시각·범위·배율)과 무관한 그림만이다.
        /// </summary>
        public static void ShowFlurry(WeaponAttackRule weapon, int index, ComboStep step, Vector2 origin, Vector2 dir)
        {
            if (weapon == null || step == null) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            if (TwinFlurry.IsFinal(index))
            {
                ShowStep(weapon, 2, step, origin, dir, 0);
                ShowStep(weapon, 2, step, origin, dir, 1);
                FlurryMark(origin, dir, 0.9f, -18f, 55f, 0.7f, 0.12f, true);
                FlurryMark(origin, dir, 0.9f, 18f, -55f, 0.7f, 0.12f, true);
                FlurryMark(origin, dir, 1.45f, -8f, 45f, 0.55f, 0.12f, true);
                FlurryMark(origin, dir, 1.45f, 8f, -45f, 0.55f, 0.12f, true);
                HitEffects.Sparks(origin + dir * 0.6f, dir, 6, FlashWhite, FlashGold, 1.1f, 1.2f);
                return;
            }
            var table = TwinFlurry.EffectDirDeg;
            float a = table.Length > 0 ? table[Mathf.Clamp(index, 0, table.Length - 1)] : 0f;
            bool right = TwinFlurry.RightHand(index);
            // ③ 그림 첫 타(단계 3 + 타 0 = 홀수 → 뒤집지 않음)는 오른칼, 둘째 타(뒤집음)는 왼칼 모양이다.
            ShowStep(weapon, 2, step, origin, Rotate(dir, a), right ? 0 : 1);
            float life = right ? 0.08f : 0.09f;
            FlurryMark(origin, dir, 0.9f, a, 50f, 0.55f, life, false);
            FlurryMark(origin, dir, 1.4f, a + (right ? 12f : -12f), -50f, 0.45f, life, false);
            if (TwinFlurry.FlashOn(index)) HitEffects.Sparks(origin + dir * 0.6f, dir, 3, FlashWhite, FlashGold, 0.9f, 1.0f);
        }

        static readonly Color FlashWhite = new Color(1f, 0.98f, 0.92f, 0.95f);
        static readonly Color FlashGold = new Color(.95f, .86f, .62f, 0.9f);

        /// <summary>난사 베인 자국 하나: 몸에서 조준 방향 + angle°, 반지름 radius 자리에, 쓸기 방향(반지름에 직각)에서 tilt°만큼 기운 길이 length의 자국.</summary>
        static void FlurryMark(Vector2 origin, Vector2 dir, float radius, float angle, float tilt, float length, float life, bool strong)
        {
            Vector2 radial = Rotate(dir, angle);
            float facing = Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg;
            HitEffects.SlashMarkAt(origin + radial * radius, facing + 90f + tilt, length, life, strong);
        }

        /// <summary>
        /// 대검 놓아 베기 칼 그림(3-7): 오른쪽 뒤에서 앞을 지나 왼쪽으로 쓰는 동작이라 대검 ① 걷어 베기 그림을 놓기 단계(부채꼴 150/170/200°, 2.5/2.8/3.1)에 맞춰 쓴다.
        /// 2·3단계는 바깥에 옅은 잔상 호를 하나 더 둬 크기를 보인다. 판정과 무관한 그림만이다.
        /// </summary>
        public static void ShowRelease(WeaponAttackRule weapon, int level, ComboStep step, Vector2 origin, Vector2 dir)
        {
            if (weapon == null || step == null) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            ShowStep(weapon, 0, step, origin, dir, 0);
            if(TopDownView.PlayerRig?.FirstAttackArt?.OwnsExtendedMotions == true) return;
            if (level < 2 || step.shape != ComboShape.Arc) return;
            var set = ArtRuntime.Active;
            var effects = set ? set.effects : null;
            var sprite = effects != null && effects.arcHeavy ? effects.arcHeavy : Trail(step.arcDeg, .18f);
            float grow = level >= 3 ? 1.16f : 1.08f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var echo = Create(sprite, origin, angle, Fit(sprite, step.size * 2f * grow, step.size * 2f * grow), new Color(.79f,.70f,.51f, level >= 3 ? .5f : .38f), .24f);
            echo._growFrom = 0.8f;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static void ShowRing(Vector2 origin, float radius)
        {
            var art = ArtRuntime.Active;
            var sprite = art && art.effects.whirl ? art.effects.whirl : LootVisuals.DustRing;
            var facing = PlayerController.Instance ? PlayerController.Instance.FacingDirection : Vector2.right;
            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + Time.time * 160f;
            var v = Create(sprite, origin, angle, Fit(sprite, radius * 2f, radius * 2f), new Color(.84f,.82f,.71f,.8f), 0.12f);
            v._growFrom = 0.7f;
            v._turn = 32f;
        }

        static Vector3 Fit(Sprite sprite, float width, float height) => new Vector3(width / Mathf.Max(.001f, sprite.bounds.size.x), height / Mathf.Max(.001f, sprite.bounds.size.y), 1f);

        static SwingVisual Create(Sprite sprite, Vector2 position, float angle, Vector3 scale, Color color, float life)
        {
            var go = new GameObject("SwingVisual");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = scale;
            var v = go.AddComponent<SwingVisual>();
            v._sprite = go.AddComponent<SpriteRenderer>();
            v._sprite.sprite = sprite;
            v._sprite.sortingOrder = 2900;
            RenderMaterials.MakeUnlit(v._sprite);
            v._color = color;
            v._sprite.color = color;
            v._life = life;
            v._baseScale = scale;
            return v;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(_age / _life);
            var c = _color;
            c.a *= 1f - p * p;
            _sprite.color = c;
            if (_turn != 0f) transform.Rotate(0f, 0f, _turn * Time.unscaledDeltaTime / _life);
            if (_growFrom < 1f)
            {
                float g = Mathf.Lerp(_growFrom, 1f, 1f - (1f - p) * (1f - p));
                if (_line)
                {
                    // 찌르기는 플레이어 쪽 끝을 고정하고 앞으로 뻗는다.
                    float length = _lineLength * g;
                    transform.localScale = new Vector3(_baseScale.x * g, _baseScale.y, 1f);
                    transform.position = _lineOrigin + _lineDir * (length * 0.5f);
                }
                else transform.localScale = _baseScale * g;
            }
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
