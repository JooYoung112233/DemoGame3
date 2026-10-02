using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 글자 지도 자리 표시 → 실제 물체. 원정마다 바뀌는 것(적·광맥·사건)과 한 번만 받는 것(궤짝·등잔·말뚝·이야기)을 각자의 만들기 함수에 넘긴다.
    /// 프로필에 한 번만 받는 것은 DungeonState에 먼저 등록해 조사율·큰 지도 아이콘에 들어가게 한다.
    /// </summary>
    public static class DungeonContent
    {
        public static void Spawn(DungeonRoot root, DungeonCell cell, CellFeature f)
        {
            Vector2 pos = cell.World(f.Local);
            var state = root.State;
            switch (f.Kind)
            {
                case FeatureKind.Group:
                case FeatureKind.Nest:
                    root.Encounters.AddGroup(cell, f, pos);
                    break;
                case FeatureKind.WoodChest:
                    state.Register(f.Id, DiscoveryKind.WoodChest, cell, pos, f.Label);
                    Chest.Create(cell, f, pos, false);
                    break;
                case FeatureKind.IronChest:
                    state.Register(f.Id, DiscoveryKind.IronChest, cell, pos, f.Label);
                    Chest.Create(cell, f, pos, true);
                    break;
                case FeatureKind.WallLamp:
                    state.Register(f.Id, DiscoveryKind.WallLamp, cell, pos, f.Label);
                    WallLamp.Create(cell, f, pos);
                    break;
                case FeatureKind.Stake:
                    state.Register(f.Id, DiscoveryKind.Stake, cell, pos, f.Label);
                    Stake.Create(cell, f, pos);
                    break;
                case FeatureKind.Stairs:
                    Stairs.Create(cell, f, pos);
                    break;
                case FeatureKind.Ore:
                    OreVein.Create(cell, f, pos);
                    break;
                case FeatureKind.Lunchbox:
                    state.Register(f.Id, DiscoveryKind.Event, cell, pos, f.Label);
                    LunchboxEvent.Create(cell, f, pos);
                    break;
                case FeatureKind.Nameplate:
                case FeatureKind.Note:
                    state.Register(f.Id, DiscoveryKind.Story, cell, pos, f.Label);
                    StoryItem.Create(cell, f, pos);
                    break;
                case FeatureKind.Chalk:
                    ChalkMark.Create(cell, f, pos);
                    break;
                case FeatureKind.Pickaxe:
                    state.Register(f.Id, DiscoveryKind.Ability, cell, pos, f.Label);
                    PickaxePickup.Create(cell, f, pos);
                    break;
                case FeatureKind.Safe:
                    state.Register(f.Id, DiscoveryKind.Safe, cell, pos, f.Label);
                    Safe.Create(cell, f, pos);
                    break;
            }
        }

        /// <summary>열린 길이 아닌 문틈을 채우는 물체. Wall 레이어 충돌체가 있어야 지나갈 수 없다.</summary>
        public static GameObject CreateBlocker(DungeonEdge edge)
        {
            switch (edge.Kind)
            {
                case EdgeKind.Plank: return PlankWall.Create(edge);
                case EdgeKind.Cracked: return CrackedWall.Create(edge);
                case EdgeKind.Locked: return LockedDoor.Create(edge);
                default: return null;
            }
        }
    }
}
