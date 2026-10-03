using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 정수리 시점 무기 그림 한 벌: 그림, 칼날 구간(검풍 빛 줄), 두 손에 드는가(쌍검), 쉬는 손 자세.
    /// 무기가 바뀔 때만 만든다(매 프레임 할당 없음). Sprite는 임시 그림이고, 그림 칸(CombatArtSet.topDown.weapons)이 있으면
    /// TopDownPlayerRig이 그 그림으로 바꿔 끼운다. 칼날 구간·두 번째 손잡이 값은 그림 칸에도 그대로 쓰므로 그림은 같은 길이로 그린다(명세 6-1).
    /// 무기 그림(임시·칸 모두)에는 주먹이 없다. 쥔 주먹은 장갑 부품으로 리그가 손 자리(대검 둘째 손 = SecondGrip)에 무기 위로 놓는다(장비 문서 9-2 ②).
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

        public bool TwoHanded => SecondGrip != 0f;

        TopDownWeaponLook(string id, Sprite sprite, float from, float to, bool twin, float secondGrip, TopDownHand right, TopDownHand left)
        {
            SecondGrip = secondGrip;
            Id = id;
            Sprite = sprite;
            BladeFrom = from;
            BladeTo = to;
            Twin = twin;
            RestRight = right;
            RestLeft = left;
        }

        public static TopDownWeaponLook Of(string weaponId)
        {
            switch (weaponId)
            {
                case "wpn_greatsword":
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Greatsword, 0.15f, 1.4f, false, -0.105f,
                        TopDownHand.At(new Vector2(0.12f, -0.33f), -38f), TopDownHand.At(new Vector2(0.12f, 0.33f), 38f));
                case "wpn_twinblades":
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Twinblade, 0.055f, 0.62f, true, 0f,
                        TopDownHand.At(new Vector2(0.16f, -0.33f), -24f), TopDownHand.At(new Vector2(0.16f, 0.33f), 24f));
                default:
                    return new TopDownWeaponLook(weaponId, TopDownSprites.Longsword, 0.1f, 0.985f, false, 0f,
                        TopDownHand.At(new Vector2(0.18f, -0.34f), -28f), TopDownHand.At(new Vector2(0.18f, 0.34f), 28f));
            }
        }
    }
}
