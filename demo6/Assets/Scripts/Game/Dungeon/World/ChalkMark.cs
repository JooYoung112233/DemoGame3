using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 분필 그림(3차 초안 2-3 D: 분필 그림 3개가 판자벽 쪽을 가리킴, 2-5 이야기). 바닥에 아이 솜씨처럼 삐뚤한 화살표를 그린다.
    /// 빛을 받는 옅은 그림이라 등잔 빛 안에서만 보인다. 방향은 자리 표시 Param(도, 0 = 오른쪽), 읽지 못하면 FacingDeg.
    /// F로 살펴보면 알림만 띄운다(조사율에는 넣지 않음). 화면 글에 그린 사람 이름을 쓰지 않는다.
    /// </summary>
    public sealed class ChalkMark : MonoBehaviour
    {
        const float UseRange = 1.3f;
        static readonly Color Chalk = new Color(0.86f, 0.85f, 0.8f, 0.72f);

        public float AngleDeg { get; private set; }

        public static ChalkMark Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("ChalkMark " + f.Id, pos);
            var mark = go.AddComponent<ChalkMark>();
            mark.AngleDeg = WorldProps.ParseAngle(f.Param, f.FacingDeg);
            mark.Draw();
            var use = go.AddComponent<WorldInteraction>();
            use.Setup("분필 그림 보기", 0f, UseRange, null, null,
                () => DungeonEvents.Say("누군가 분필로 그린 화살표가 아래쪽 벽을 가리킨다"));
            return mark;
        }

        /// <summary>몸통 한 획 + 끝 'V' 두 획 + 꼬리 점. 화살표 끝은 이 물체에서 0.6유닛 앞.</summary>
        void Draw()
        {
            var art = new GameObject("Arrow").transform;
            art.SetParent(transform, false);
            art.localRotation = Quaternion.Euler(0f, 0f, AngleDeg);
            int order = WorldProps.FloorDecalOrder + 3;
            Vector2 tip = new Vector2(0.6f, 0f);
            WorldProps.Stroke(art, "Shaft", new Vector2(-0.65f, 0.03f), new Vector2(-0.05f, -0.02f), 0.1f, Chalk, order, false);
            WorldProps.Stroke(art, "Shaft", new Vector2(-0.05f, -0.02f), tip, 0.1f, Chalk, order, false);
            WorldProps.Stroke(art, "Head", tip, tip + WorldProps.Rotate(Vector2.left, -32f) * 0.42f, 0.1f, Chalk, order, false);
            WorldProps.Stroke(art, "Head", tip, tip + WorldProps.Rotate(Vector2.left, 36f) * 0.4f, 0.1f, Chalk, order, false);
            WorldProps.Shape(art, "Dot", new Vector2(-0.82f, 0.04f), new Vector2(0.13f, 0.13f), ShapeSprites.Circle, Chalk, order, false);
        }
    }
}
