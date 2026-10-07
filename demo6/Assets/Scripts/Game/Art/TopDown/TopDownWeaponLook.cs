using Demo6.Core.Combat.Stance;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점 무기 그림 한 벌: 그림, 칼날 구간(검풍 빛 줄), 두 손에 드는가(쌍검), 쉬는 손 자세.
    /// 무기가 바뀔 때만 만든다(매 프레임 할당 없음). Sprite는 임시 그림이고, 그림 칸(CombatArtSet.topDown.weapons)이 있으면
    /// TopDownPlayerRig이 그 그림으로 바꿔 끼운다. 칼날 구간·두 번째 손잡이 값은 그림 칸에도 그대로 쓰므로 그림은 같은 길이로 그린다(명세 6-1).
    /// 무기 그림(임시·칸 모두)에는 주먹이 없다. 쥔 주먹은 장갑 부품으로 리그가 손 자리(대검 둘째 손 = SecondGrip)에 무기 위로 놓는다(장비 문서 9-2 ②).
    /// 새 무기 6종(쇠망치·창·큰 낫·도끼·단검·사슬 철퇴)은 기획/전투-보스-무기-다듬기-1차.md 2-7 표의 길이·칼날 구간·둘째 손을 쓴다. 모르는 id는 장검이다.
    /// </summary>
    public sealed class TopDownWeaponLook
    {
        public readonly string Id;
        public readonly Sprite Sprite;
        public readonly float BladeFrom;
        public readonly float BladeTo;
        public readonly bool Twin;
        /// <summary>두 손으로 한 자루를 잡는 무기(대검)면 왼손이 잡는 자리(칼날 방향 손잡이 위치, 오른손 주먹 기준). 아니면 0.</summary>
        public readonly float SecondGrip;
        public readonly TopDownHand RestRight;
        public readonly TopDownHand RestLeft;
        /// <summary>
        /// 왼손에 방패를 드는 무기(한손검과 방패, 기획/세-무기-우클릭-소켓-1차.md 2-8)면 true. 이때 RestLeft는 방패 손 꼴
        /// (Pos = 왼손, Angle = 방패각, Length = 방패깊이)이고 리그가 ShieldPart로 방패를 놓고 왼 주먹을 쥔 주먹으로 그린다.
        /// </summary>
        public readonly bool Shield;
        /// <summary>
        /// 휘두르기 겉모습 차이(TopDownSwing이 읽음): 찌르기 뻗음(기본 0.55, 창만 길게), 반대로 쓸 때 그림 뒤집기(큰 낫),
        /// 내려치기 준비 무게(쇠망치·도끼, 대검은 예전 식 그대로), 사슬 철퇴(쇠공이 몸 둘레를 돎). 판정 시간·범위는 바꾸지 않는다.
        /// </summary>
        public TopDownSwingStyle Style { get; private set; } = TopDownSwingStyle.Default;
        /// <summary>사슬 철퇴면 손잡이 끝 고리(사슬 매다는 자리, 유닛). 아니면 0.</summary>
        public float FlailTip { get; private set; }

        public bool TwoHanded => SecondGrip != 0f;
        public bool Flail => FlailTip > 0f;

        TopDownWeaponLook(string id, Sprite sprite, float from, float to, bool twin, float secondGrip, TopDownHand right, TopDownHand left, bool shield = false)
        {
            Shield = shield;
            SecondGrip = secondGrip;
            Id = id;
            Sprite = sprite;
            BladeFrom = from;
            BladeTo = to;
            Twin = twin;
            RestRight = right;
            RestLeft = left;
        }

        TopDownWeaponLook With(TopDownSwingStyle style, float flailTip = 0f)
        {
            Style = style;
            FlailTip = flailTip;
            return this;
        }

        public static TopDownWeaponLook Of(string weaponId)
        {
            switch (weaponId)
            {
                // 세 무기(기획/세-무기-우클릭-소켓-1차.md 2-2·3-2·4-2): 쉬는 손 값은 Core 쉬는 자세(WeaponStances.Rest)에서 읽는다.
                case "wpn_longsword":
                {
                    // 한손검과 방패: 가슴 앞 오른쪽에 칼을 앞으로 듦(3차: 칼 제 길이, 늘이기·줄이기 없음), 방패는 왼쪽 앞(방패각 32°, 깊이 0.34).
                    var rest = WeaponStances.Rest(StanceWeapon.SwordShield);
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Longsword, 0.1f, 0.985f, false, 0f,
                        WeaponStanceLook.ToHand(rest.Right), WeaponStanceLook.ToShieldHand(rest), true);
                }
                case "wpn_greatsword":
                {
                    // 대검(3차): 두 손을 오른 허리 옆에 모으고 칼끝을 땅 쪽으로 오른쪽 아래로(기울기 −56). 넓은 날 기울기별 그림·둘째 손 −0.18 × max(cos 기울기, 0.65)·
                    // 칼빛 띠(날 구간 0.11 ~ 1.33 × cos 기울기)·칼 그림자는 WeaponStanceArms가 리그가 놓은 뒤 고쳐 그린다(칼 그림 배율 1).
                    var rest = WeaponStances.Rest(StanceWeapon.Greatsword);
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Greatsword, GreatswordPoses.BladeFrom, GreatswordPoses.TipAt, false, GreatswordPoses.SecondGrip,
                        WeaponStanceLook.ToHand(rest.Right), WeaponStanceLook.ToHand(rest.Left));
                }
                case "wpn_twinblades":
                {
                    // 쌍검(3차): 두 칼을 자연스럽게 쥠(두 손 몸 양옆 허리~가슴 앞, 칼끝 앞·바깥 아래 −40° / +33°, 칼 제 길이).
                    var rest = WeaponStances.Rest(StanceWeapon.Twinblades);
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Twinblade, 0.055f, 0.62f, true, 0f,
                        WeaponStanceLook.ToHand(rest.Right), WeaponStanceLook.ToHand(rest.Left));
                }
                // 새 무기 6종(기획/전투-보스-무기-다듬기-1차.md 2-7). 칼날 구간 = 검풍 빛 줄 자리, 둘째 손 = 두 손 무기의 왼 주먹 자리.
                case "wpn_maul":
                    // 무거운 머리를 오른쪽 아래로 늘어뜨려 든다. 내려치기 준비에 대검보다 조금 더 무게를 싣는다.
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Maul, 0.9f, 1.2f, false, -0.12f,
                            TopDownHand.At(new Vector2(0.14f, -0.31f), -42f), TopDownHand.At(new Vector2(0.14f, 0.31f), 42f))
                        .With(new TopDownSwingStyle { ThrustReach = TopDownSwingStyle.DefaultReach, SlamWindupLean = 0.085f });
                case "wpn_spear":
                    // 창끝을 앞으로 겨눠 옆구리에 낀다. 찌르기는 장검(0.55)보다 멀리(0.85) 뻗어 긴 창날과 함께 장검 찌르기와 구별된다.
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Spear, 1.4f, 1.75f, false, -0.35f,
                            TopDownHand.At(new Vector2(0.16f, -0.3f), -10f), TopDownHand.At(new Vector2(0.16f, 0.3f), 10f))
                        .With(new TopDownSwingStyle { ThrustReach = 0.85f });
                case "wpn_scythe":
                    // 날을 바깥(−y)으로 늘어뜨려 든다. 되거두기처럼 왼쪽 → 오른쪽으로 쓸 때는 그림을 위아래로 뒤집는다.
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Scythe, 1.0f, 1.35f, false, -0.3f,
                            TopDownHand.At(new Vector2(0.14f, -0.3f), -35f), TopDownHand.At(new Vector2(0.14f, 0.3f), 35f))
                        .With(new TopDownSwingStyle { ThrustReach = TopDownSwingStyle.DefaultReach, FlipOnReverse = true });
                case "wpn_axe":
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Axe, 0.76f, 1.02f, false, 0f,
                            TopDownHand.At(new Vector2(0.18f, -0.34f), -32f), TopDownHand.At(new Vector2(0.18f, 0.34f), 28f))
                        .With(new TopDownSwingStyle { ThrustReach = TopDownSwingStyle.DefaultReach, SlamWindupLean = 0.07f });
                case "wpn_dagger":
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Dagger, 0.055f, 0.43f, false, 0f,
                        TopDownHand.At(new Vector2(0.2f, -0.33f), -20f), TopDownHand.At(new Vector2(0.18f, 0.34f), 28f));
                case "wpn_flail":
                    // 손잡이만 그림이고 사슬·쇠공은 FlailChain이 그린다. 빛 줄은 손잡이 머리(쇠테 ~ 고리).
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Flail, 0.2f, 0.34f, false, 0f,
                            TopDownHand.At(new Vector2(0.2f, -0.33f), -40f), TopDownHand.At(new Vector2(0.18f, 0.34f), 28f))
                        .With(new TopDownSwingStyle { ThrustReach = TopDownSwingStyle.DefaultReach, Flail = true }, TopDownSprites.FlailHandleTip);
                default:
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Longsword, 0.1f, 0.985f, false, 0f,
                        TopDownHand.At(new Vector2(0.18f, -0.34f), -28f), TopDownHand.At(new Vector2(0.18f, 0.34f), 28f));
            }
        }
    }
}
