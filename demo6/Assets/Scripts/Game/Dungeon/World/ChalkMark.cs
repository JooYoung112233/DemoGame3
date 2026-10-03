using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 분필 그림(3차 초안 2-3 D: 분필 그림 3개가 판자벽 쪽을 가리킴, 2-5 이야기). 바닥에 아이 솜씨처럼 삐뚤한 화살표를 그린다.
    /// 빛을 받는 옅은 그림이라 등잔 빛 안에서만 보인다. 방향은 자리 표시 Param(도, 0 = 오른쪽)을 그대로 쓰고,
    /// Param이 없을 때만 가장 가까운 안 부순 판자벽 문 쪽으로 계산한다(매판 새 탐험 1차 2-5 차례 11: 원정마다 이번 숨은 방 쪽). 판자벽도 없으면 FacingDeg.
    /// F로 살펴보면 알림만 띄운다(조사율에는 넣지 않음). 화면 글에 그린 사람 이름을 쓰지 않는다.
    /// </summary>
    public sealed class ChalkMark : MonoBehaviour
    {
        const float UseRange = 1.3f;
        /// <summary>살피기 글(2-2 '1층 분필 살피기').</summary>
        public const string InspectLine = "작은 손으로 그린 화살표. 분필 가루가 아직 젖어 있다.";
        static readonly Color Chalk = new Color(0.86f, 0.85f, 0.8f, 0.72f);

        public float AngleDeg { get; private set; }

        public static ChalkMark Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("ChalkMark " + f.Id, pos);
            var mark = go.AddComponent<ChalkMark>();
            float angle = WorldProps.ParseAngle(f.Param, float.NaN);
            mark.AngleDeg = float.IsNaN(angle) ? AngleToPlank(pos, f.FacingDeg) : angle;
            mark.Draw();
            var use = go.AddComponent<WorldInteraction>();
            use.Setup("분필 자국 살피기", 0f, UseRange, null, null, () => DungeonEvents.Say(InspectLine));
            return mark;
        }

        /// <summary>가장 가까운 안 부순 판자벽 문 가운데를 향한 각도(도). 판자벽이 없으면 fallback.</summary>
        static float AngleToPlank(Vector2 pos, float fallback)
        {
            var root = DungeonRoot.Instance;
            if (!root || root.World == null) return fallback;
            DungeonEdge best = null;
            float bestD = float.MaxValue;
            foreach (var e in root.World.Edges)
            {
                if (e.Kind != EdgeKind.Plank || e.Opened) continue;
                float d = (e.DoorCenter - pos).sqrMagnitude;
                if (d >= bestD) continue;
                bestD = d;
                best = e;
            }
            if (best == null) return fallback;
            Vector2 dir = best.DoorCenter - pos;
            return dir.sqrMagnitude > 0.0001f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : fallback;
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
