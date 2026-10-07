using Demo6.Core.Combat;
using UnityEngine;
using C = Demo6.Game.TopDownCanvas;

namespace Demo6.Game
{
    /// <summary>
    /// 한손검과 방패의 방패 부품(기획/세-무기-우클릭-소켓-1차.md 2-8). 렌더러 'Shield' 하나를 리그 밑에 둔다.
    /// 임시 그림은 코드로 한 번 그려 캐시한다: 위에서 본 둥근 앞면 지름 0.56(PPU 256에서 143px), 쇠테 폭 0.03, 가운데 쇠 돌기 지름 0.12,
    /// 세로 판자 4장(이음 줄 3개), 긁힘 약간, 테두리 2px. 피벗은 가운데. 그림 x축 = 방패 바깥면이 보는 쪽(방패각)이라 깊이만큼 x로 납작해진다.
    /// 원화 칸은 TopDownWeaponArt.shield(같은 피벗·지름·방향). 칸이 차 있으면 그 그림을 쓴다(그림 묶음은 매 프레임 TopDownView.CurrentArt로 읽음, 할당 없음).
    /// 놓기: 중심 = 왼손 + 0.05 × 방패 방향, 회전 = 방패각(hand.Angle), 크기 = (방패깊이 hand.Length, 1), 깊이 최소 0.14. 순서 = 무기 층(몸 +2).
    /// 귀여운 원화 시험판(다른 세션 몫)은 머리가 커서 방패가 늘 머리에 겹친다. 그래서 리그가 머리와 같은 층을 주고 여기서 조금 뒤에 두어 머리 밑에 그린다.
    /// 쓰러지면 몸 밑(몸 −1). 방패 버팀이 0.35 아래면 색 × 0.8, 0.02 떨림(임시 표시, UI 막대 없음).
    /// 부르는 곳: TopDownPlayerRig(EnsureParts에서 만들고, Place의 PlaceArms 뒤 Place, Restore에서 Hide).
    /// </summary>
    public sealed class ShieldPart
    {
        /// <summary>방패 앞면 지름(유닛).</summary>
        public const float Diameter = 0.56f;
        /// <summary>왼손에서 방패 중심까지(방패 방향으로).</summary>
        public const float HandOffset = 0.05f;
        public const float MinDepth = 0.14f;
        /// <summary>쇠테 폭·가운데 돌기 지름(유닛).</summary>
        public const float RimWidth = 0.03f;
        public const float BossDiameter = 0.12f;
        /// <summary>방패 버팀이 낮을 때 떨림 폭(유닛)·색 배율.</summary>
        public const float LowShake = 0.02f;
        public const float LowShade = 0.8f;
        /// <summary>무기 층(리그가 넘기는 order = 몸 + 2)에서 몸 밑(몸 − 1)까지.</summary>
        const int BelowOffset = 3;
        const float Ppu = 256f;

        /// <summary>귀여운 원화 시험판 보정 손잡이(자리·배율, 8-2 위험 1). 귀여운 판에서만 쓰고, 기본 0·1이면 아무 일도 없다.</summary>
        public static Vector2 CuteOffset;
        public static float CuteScale = 1f;
        /// <summary>
        /// 귀여운 판에서 방패를 카메라에서 조금 멀리 둔다(유닛). 리그가 귀여운 머리와 같은 순서를 주므로(몸 +1), 같은 순서 안에서는 먼 쪽이 먼저 그려져
        /// 큰 머리가 방패를 덮는다(정수리 시점에서 가슴 높이 방패는 머리 밑, 규칙 ② — 0-3의 26).
        /// </summary>
        public const float CuteBehindZ = 0.01f;

        // 2-8 색: 판자 ShaftWood, 쇠테 DarkIron, 돌기 SteelMid(#8F949C), 테두리 Outline(#0B0A0D).
        static readonly Color Wood = new Color(0.27f, 0.2f, 0.14f);
        static readonly Color DarkIron = new Color(0.22f, 0.21f, 0.21f);
        static readonly Color SteelMid = new Color(0.561f, 0.580f, 0.612f);
        static readonly Color Outline = new Color(0.045f, 0.04f, 0.05f, 1f);
        /// <summary>판자 4장 밝기(판마다 조금 다르게).</summary>
        static readonly float[] PlankTone = { 0.96f, 0.9f, 1.0f, 0.93f };

        static Sprite _temp;
        readonly SpriteRenderer _sr;
        Sprite _art;

        public ShieldPart(Transform parent, Material material, SpriteRenderer like)
        {
            _sr = TopDownView.Part("Shield", parent, TempSprite, material, Color.white, like);
            _sr.enabled = false;
        }

        /// <summary>방패 렌더러(번쩍임 맞춤용).</summary>
        public SpriteRenderer Renderer => _sr;

        /// <summary>코드로 그린 임시 방패(처음 한 번 만들어 캐시).</summary>
        public static Sprite TempSprite => _temp ? _temp : _temp = Build();

        /// <summary>원화 칸 그림(없으면 null → 임시 그림).</summary>
        public void UseArt(Sprite art) => _art = art;

        /// <summary>
        /// on = 방패 무기인가(TopDownWeaponLook.Shield), hand = 방패 손(Pos·Angle = 방패각·Length = 방패깊이·Size), tint = 밝기·투명도,
        /// order = 그리는 순서(무기 층, 귀여운 판은 머리와 같은 층), below = 쓰러짐(몸 밑), meterFraction = 방패 버팀 몫(PlayerController.GuardMeterFraction),
        /// cute = 귀여운 원화 시험판(보정 손잡이를 쓰고 CuteBehindZ만큼 뒤에 두어 머리 밑에 그림).
        /// </summary>
        public void Place(bool on, TopDownHand hand, Color tint, int order, bool below, float meterFraction, bool cute = false)
        {
            if (!_sr) return;
            if (!on)
            {
                if (_sr.enabled) _sr.enabled = false;
                return;
            }
            var set = TopDownView.CurrentArt();
            var weapons = set != null ? set.weapons : null;
            UseArt(weapons != null ? weapons.shield : null);
            var sprite = _art ? _art : TempSprite;
            if (_sr.sprite != sprite) _sr.sprite = sprite;
            if (!_sr.enabled) _sr.enabled = true;

            float depth = Mathf.Max(MinDepth, hand.Length);
            float size = hand.Size > 0f ? hand.Size : 1f;
            float rad = hand.Angle * Mathf.Deg2Rad;
            Vector2 pos = hand.Pos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * HandOffset + (cute ? CuteOffset : Vector2.zero);
            float scale = cute ? CuteScale : 1f;
            var color = tint;
            if (meterFraction < ShieldRule.LowMeterFraction)
            {
                color = new Color(tint.r * LowShade, tint.g * LowShade, tint.b * LowShade, tint.a);
                float time = Time.time;
                pos += new Vector2(Mathf.Sin(time * 61f), Mathf.Sin(time * 47f + 1.7f)) * LowShake;
            }
            var tr = _sr.transform;
            tr.localPosition = new Vector3(pos.x, pos.y, cute ? CuteBehindZ : 0f);
            tr.localRotation = Quaternion.Euler(0f, 0f, hand.Angle);
            tr.localScale = new Vector3(depth * size * scale, size * scale, 1f);
            _sr.color = color;
            _sr.sortingOrder = below ? order - BelowOffset : order;
        }

        public void Hide()
        {
            if (_sr && _sr.enabled) _sr.enabled = false;
        }

        /// <summary>임시 방패: 둥근 앞면(쇠테 + 세로 판자 4장 + 이음 줄 3개 + 가운데 쇠 돌기 + 긁힘), 테두리 2px. 피벗 가운데.</summary>
        static Sprite Build()
        {
            const float r = Diameter * 0.5f;
            const float outline = 2f / Ppu;
            var cv = new C(-r - 0.02f, r + 0.02f, -r - 0.02f, r + 0.02f, Ppu);
            var center = Vector2.zero;
            // 쇠테(바깥 원 전체를 먼저 쇠로 깔고 안쪽을 판자로 덮음).
            cv.Draw(p => C.Circle(p, center, r), (p, d) => C.Shade(DarkIron, C.Dome(d, RimWidth) * C.Rim(d, 0.008f, 0.5f)), outline, Outline);
            float inner = r - RimWidth;
            // 세로 판자 4장: 그림 y(방패 너비) 방향으로 나뉘고 x(방패 세로)로 결이 난다. 판마다 밝기가 조금 다르다.
            cv.Draw(p => C.Circle(p, center, inner), (p, d) =>
            {
                int plank = Mathf.Clamp(Mathf.FloorToInt((p.y + inner) / (inner * 0.5f)), 0, 3);
                float tone = PlankTone[plank];
                float grain = 0.9f + 0.12f * Mathf.Abs(Mathf.Sin(p.x * 95f + plank * 1.7f + 6f * C.Noise(p, 0.05f, 31 + plank)));
                return C.Shade(Wood, tone * grain * C.Dome(d, 0.05f));
            }, 0.006f, C.Shade(DarkIron, 0.7f));
            // 이음 줄 3개.
            for (int i = 1; i <= 3; i++)
            {
                float y = -inner + inner * 0.5f * i;
                cv.Draw(p => Mathf.Max(Mathf.Abs(p.y - y) - 0.0045f, C.Circle(p, center, inner)), C.Shade(Wood, 0.45f));
            }
            // 긁힘 약간(밝은 가는 선).
            cv.Draw(p => Mathf.Max(C.Capsule(p, new Vector2(-0.16f, 0.05f), new Vector2(-0.07f, 0.12f), 0.0035f), C.Circle(p, center, inner)), C.Shade(Wood, 1.45f));
            cv.Draw(p => Mathf.Max(C.Capsule(p, new Vector2(0.08f, -0.17f), new Vector2(0.15f, -0.09f), 0.003f), C.Circle(p, center, inner)), C.Shade(Wood, 1.35f));
            cv.Draw(p => Mathf.Max(C.Capsule(p, new Vector2(0.11f, 0.13f), new Vector2(0.17f, 0.10f), 0.0025f), C.Circle(p, center, inner)), C.Shade(Wood, 1.3f));
            // 쇠테 못 6개.
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.3f;
                var at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - RimWidth * 0.5f);
                cv.Draw(p => C.Circle(p, at, 0.008f), (p, d) => C.Shade(SteelMid, C.Dome(d, 0.008f)), 0.003f, Outline);
            }
            // 가운데 쇠 돌기.
            cv.Draw(p => C.Circle(p, center, BossDiameter * 0.5f), (p, d) => C.Shade(SteelMid, C.Dome(d, BossDiameter * 0.5f) * C.Rim(d, 0.01f, 0.4f)), outline, Outline);
            return cv.ToSprite("정수리 방패", Vector2.zero);
        }
    }
}
