using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥의 기름 병(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5-6, 2026-10-07). F로 주우면 원정 몫(ExpeditionLeg.Oil)에 1병(상한 DownRules.OilCap).
    /// 나오는 곳: 나무 궤짝(35%), 도시락통을 열면, 막다른 칸(층마다 한 곳, PlaceForFloor). 원정 시작 2병은 원정 몫이 들고 시작한다.
    /// 새 그림 없이 도형(호박색 병·검은 목·마개)으로 그리고 8유닛 안에서 은은히 반짝인다. 바닥 물건이라 Y 정렬 그대로(LootDrop·열쇠와 같음).
    /// </summary>
    public sealed class OilFlask : Interactable
    {
        const float GlintRange = 8f;
        static readonly Color Glass = new Color(0.62f, 0.42f, 0.16f, 0.95f);
        static readonly Color OilColor = new Color(0.42f, 0.26f, 0.07f);
        static readonly Color Neck = new Color(0.18f, 0.15f, 0.12f);
        static readonly Color Cork = new Color(0.55f, 0.42f, 0.28f);
        static readonly Color GlintColor = new Color(1f, 0.85f, 0.5f, 0.85f);
        static readonly Color GainText = new Color(1f, 0.86f, 0.55f);

        bool _taken;
        float _seed;
        SpriteRenderer _glint;

        public override string Prompt => "기름 병 줍기";
        public override bool Available => !_taken;

        public static OilFlask Place(Vector2 pos)
        {
            if (Physics2D.OverlapCircle(pos, 0.25f, Layers.WallMask)) return null;
            var go = WorldProps.Root("OilFlask", pos);
            var flask = go.AddComponent<OilFlask>();
            flask._seed = Random.value * 10f;
            flask.Build(pos);
            return flask;
        }

        /// <summary>
        /// 층에 들어섬(DungeonRoot): 막다른 칸(문이 하나뿐인 칸, 승강장·계단 칸·보스방 빼고, 승강장에서 처음 갈 수 있는 칸) 가운데 한 곳에 1병.
        /// 고르는 칸은 원정 소금·층으로 정해져 같은 원정·같은 층이면 같다.
        /// </summary>
        public static void PlaceForFloor(DungeonRoot root)
        {
            if (!root || root.IsDen || root.Map == null || root.World == null || root.State == null) return;
            var landing = MapAnchors.FindLanding(root.Map);
            var stairs = MapAnchors.FindStairsCell(root.Map);
            if (landing == null) return;
            var reach = root.Map.Reachable(landing, FloorMap.StartPassable);
            var ends = new List<DungeonCell>();
            foreach (var mc in root.Map.Cells)
            {
                if (mc == landing || mc == stairs || mc.Edges.Count != 1 || !reach.Contains(mc)) continue;
                var c = root.World.Find(mc.Id);
                if (c != null) ends.Add(c);
            }
            if (ends.Count == 0) return;
            ends.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            ulong salt = root.State.RunSalt ^ ((ulong)(uint)root.Floor * 0x9E3779B97F4A7C15UL);
            var cell = ends[(int)(salt % (ulong)ends.Count)];
            Vector2[] tries = { new Vector2(1.6f, -1.2f), new Vector2(-1.6f, -1.2f), new Vector2(1.6f, 1.2f), new Vector2(-1.6f, 1.2f), Vector2.zero };
            foreach (var o in tries)
                if (Place(cell.Center + o)) return;
        }

        void Build(Vector2 pos)
        {
            int order = WorldProps.SortY(pos.y);
            WorldProps.Shape(transform, "Body", new Vector2(0f, -0.02f), new Vector2(0.26f, 0.3f), ShapeSprites.Circle, Glass, order, false);
            WorldProps.Shape(transform, "Oil", new Vector2(0f, -0.07f), new Vector2(0.2f, 0.16f), ShapeSprites.Circle, OilColor, order + 1, false);
            WorldProps.Shape(transform, "Neck", new Vector2(0f, 0.17f), new Vector2(0.08f, 0.14f), ShapeSprites.Square, Neck, order + 2, false);
            WorldProps.Shape(transform, "Cork", new Vector2(0f, 0.25f), new Vector2(0.1f, 0.06f), ShapeSprites.Square, Cork, order + 3, false);
            _glint = WorldProps.Shape(transform, "Glint", new Vector2(-0.05f, 0.03f), new Vector2(0.3f, 0.3f), WorldProps.SoftDot, GlintColor, order + 4, true);
            _glint.enabled = false;
        }

        public override void Interact()
        {
            if (_taken) return;
            var root = DungeonRoot.Instance;
            var leg = root ? root.Leg : null;
            if (leg == null) return;
            if (leg.Oil >= DownRules.OilCap)
            {
                DungeonEvents.Say(DownRules.OilFullLine);
                return;
            }
            leg.Oil = DownRules.AddOil(leg.Oil, 1);
            _taken = true;
            Sfx.Play(SfxKind.Pickup);
            WorldOverlay.Text(Position + Vector2.up * 1.1f, "기름 병 +1", GainText);
            DungeonEvents.Say($"기름 병 하나 — 지금 {leg.Oil}병 · 벽 등잔 하나에 1병");
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_taken || !_glint) return;
            bool show = WorldProps.PlayerDistance(Position) <= GlintRange;
            _glint.enabled = show;
            if (!show) return;
            var c = GlintColor;
            c.a = Mathf.Clamp01(Mathf.Sin(Time.unscaledTime * 2.1f + _seed) * 1.3f - 0.1f) * GlintColor.a;
            _glint.color = c;
        }
    }
}
