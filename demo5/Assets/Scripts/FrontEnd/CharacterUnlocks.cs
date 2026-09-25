using System;
using System.Collections.Generic;
using System.Linq;

namespace Demo5.FrontEnd
{
    public enum CharacterUnlockRule { OpeningPair, RestoredWorkshop, RestoredKitchen, PreparedHousing, SharedRecords }

    public sealed partial class SettlementController
    {
        readonly HashSet<string> unlockedCharacters = new HashSet<string>();

        // Unlocks belong to this campaign. A new game still starts with the original pair.
        public void RefreshCharacterUnlocks(bool announce = true)
        {
            if (Campaign == null || !Roster) return;
            foreach (var candidate in Roster.Candidates.Where(c => c != null))
            {
                bool known = PartySelectionSession.Selected.Contains(candidate.Id);
                if (!known && !MeetsUnlock(candidate)) continue;
                if (!unlockedCharacters.Add(candidate.Id) || !announce || candidate.AvailableAtStart || known) continue;
                ActivityLog.Add(Campaign.ClockText.Replace("\n", " ") + " · 새 동료 합류 조건 달성 · " + UnlockMilestone(candidate.UnlockRule));
            }
        }

        bool MeetsUnlock(PartyCandidate candidate)
        {
            if (candidate.AvailableAtStart) return true;
            var development = Development ? Development.State : null;
            bool returned = (Opening && Opening.State.FirstReturn) || (ReturnPanel && ReturnPanel.HasReport);
            switch (candidate.UnlockRule)
            {
                case CharacterUnlockRule.RestoredWorkshop: return returned && development != null && development.Workbench;
                case CharacterUnlockRule.RestoredKitchen: return returned && development != null && development.Cooker;
                case CharacterUnlockRule.PreparedHousing: return returned && CraftPanel && CraftPanel.SideRoomReady;
                case CharacterUnlockRule.SharedRecords:
                    var story = ArrivalPanel ? ArrivalPanel.Story : null;
                    return development != null && development.Research && story && story.State.Returned;
                default: return false;
            }
        }

        static string UnlockMilestone(CharacterUnlockRule rule)
        {
            switch (rule)
            {
                case CharacterUnlockRule.RestoredWorkshop: return "원정 귀환 · 작업대 복구";
                case CharacterUnlockRule.RestoredKitchen: return "원정 귀환 · 조리대 복구";
                case CharacterUnlockRule.PreparedHousing: return "옆방 거주 준비";
                case CharacterUnlockRule.SharedRecords: return "연구대 · 생존자와 나눈 이야기";
                default: return "여정의 시작";
            }
        }

        public bool IsCharacterUnlocked(string id)
        {
            RefreshCharacterUnlocks();
            return !string.IsNullOrEmpty(id) && unlockedCharacters.Contains(id);
        }

        public string[] ExportCharacterUnlocks()
        {
            RefreshCharacterUnlocks();
            return unlockedCharacters.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        public void RestoreCharacterUnlocks(IEnumerable<string> ids)
        {
            unlockedCharacters.Clear();
            foreach (var id in ids ?? Array.Empty<string>()) unlockedCharacters.Add(id);
            RefreshCharacterUnlocks(false);
        }

        public string NextCharacterClue()
        {
            RefreshCharacterUnlocks();
            var remaining = Roster.Candidates.Where(c => c != null && !c.AvailableAtStart && !PartySelectionSession.Selected.Contains(c.Id))
                .OrderBy(c => c.UnlockOrder).ToArray();
            if (remaining.Length == 0) return "현재 알려진 동료가 모두 함께하고 있습니다.";
            if (remaining.Any(c => unlockedCharacters.Contains(c.Id))) return "합류 가능한 동료가 있습니다.\n방문 시간에 이야기를 나눠보세요.";
            return remaining[0].UnlockHint;
        }
    }
}
