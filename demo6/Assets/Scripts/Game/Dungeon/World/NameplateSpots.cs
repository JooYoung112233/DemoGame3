using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 생성 지도 밖에서 놓는 광부 명패(시스템-컨텐츠-다듬기-검토-1차.md 묶음 3 나-8). 2층 명패는 이야기 문서 3장 '계단방 옆'대로 2층 계단 앞 칸에 놓는다
    /// (FloorRecipe.Floor2Nameplate — 생성 지도에 넣지 않는 까닭은 그 주석). 아직 받지 않았을 때만, 층에 들어설 때 한 번.
    /// 받은 기록은 1층 명패와 같이 DungeonState → 꾸러미 OnceDone으로 간다(StoryItem). 보스 자리 명패는 BossReward가 놓는다.
    /// </summary>
    public static class NameplateSpots
    {
        /// <summary>계단 앞 칸 가운데에서 명패까지(계단·말뚝과 겹치지 않는 왼쪽 아래).</summary>
        static readonly Vector2 StairsRoomOffset = new Vector2(-5f, -3f);

        /// <summary>층에 들어섬(DungeonRoot): 이 층에 지도 밖 명패가 있고 아직 받지 않았으면 놓는다.</summary>
        public static void PlaceForFloor(DungeonRoot root)
        {
            if (!root || root.IsDen || root.Floor != 2 || root.World == null) return;
            var item = FloorRecipe.Floor2Nameplate;
            var carry = ProfileCarry.Data;
            if (carry != null && carry.OnceDone.Contains(item.Id)) return;
            var state = root.State;
            if (state != null && state.IsDone(item.Id)) return;
            DungeonCell stairs = null;
            foreach (var c in root.World.Cells)
                if (c.Piece == PieceKind.StairsRoom)
                {
                    stairs = c;
                    break;
                }
            if (stairs == null) return;
            Vector2 pos = stairs.Center + StairsRoomOffset;
            var feature = new CellFeature { Kind = FeatureKind.Nameplate, Id = item.Id, Label = item.Label };
            state?.Register(item.Id, DiscoveryKind.Story, stairs, pos, item.Label);
            StoryItem.Create(stairs, feature, pos);
        }
    }
}
