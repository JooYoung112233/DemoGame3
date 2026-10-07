using UnityEngine;

namespace Demo6.Game
{
    public sealed partial class Inventory
    {
        /// <summary>이름표로 고른 정확한 드랍만 기존 F 거리 안에서 줍는다. 자동 회수 경로의 규칙은 바꾸지 않는다.</summary>
        public bool TryPickUpClickedDrop(LootDrop drop)
        {
            if (!drop || !drop.Available || !_player || _player.IsDown ||
                DungeonUi.ModalOpen || TimeScaleService.Paused || PlayerInputReader.Blocked || _player.InteractBlockedByAct)
                return false;
            if ((_player.Position - drop.Position).sqrMagnitude > drop.Range * drop.Range)
            {
                DungeonEvents.Say("가까이 가서 주울 수 있다");
                return false;
            }
            if (drop.BlockedReason != null)
            {
                DungeonEvents.Say(drop.BlockedReason);
                return false;
            }
            return PickUp(drop);
        }
    }
}
