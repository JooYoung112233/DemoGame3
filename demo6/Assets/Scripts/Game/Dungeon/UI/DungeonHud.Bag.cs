using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 HUD 가방 칸 글(재화 쓸 곳 1차 5-4): 가방 단추(I) 아이콘 위 작은 글 'n/칸'(ForgeText.BagCountLine).
    /// 가득(개수 ≥ 칸, Inventory.BagFull)이면 붉게(DungeonUi.Rust), 아니면 BoneDim. 넘침이면 '22/20'처럼 그대로 붉다.
    /// 기능만 둔다(배치·반응형 다듬기는 Unity 개발 단계). DungeonHud.cs의 DrawPanel이 가방 단추를 그린 바로 뒤에 부른다.
    /// </summary>
    public sealed partial class DungeonHud
    {
        /// <summary>가방 칸 글(개수·칸이 바뀔 때만 다시 만든다).</summary>
        readonly TextCache _bagText = new TextCache();

        void DrawBagCount()
        {
            var inv = Inventory.Instance;
            if (!inv) return;
            int count = inv.BagCount;
            int capacity = inv.Capacity;
            if (_bagText.Stale(count, capacity)) _bagText.Text = ForgeText.BagCountLine(count, capacity);
            var r = UtilityRect(0);
            var color = inv.BagFull ? DungeonUi.Rust : DungeonUi.BoneDim;
            DungeonUi.ShadowLabel(new Rect(r.x - 10f, r.y - 16f, r.width + 20f, 13f), _bagText.Text, ApprovedUiV5.Style(11, TextAnchor.MiddleCenter, true), color);
        }
    }
}
