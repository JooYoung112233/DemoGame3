using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Play-mode identity fixture. Run/RunTwo/RunSix replace the current test campaign.
// They use only the existing Temp test-slot directory and never load or write a save.
// Current is read-only and also works on a battle prepared by another review script.
public static class VerifyBattlePawnIdentity
{
    static readonly string[] Ids = { "scout", "medic", "cook", "mechanic", "guard", "researcher" };
    static SettlementController Owner => Object.FindAnyObjectByType<SettlementController>();
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static async Task Until(Func<bool> ready, string description)
    {
        var timer = Stopwatch.StartNew();
        while (!ready())
        {
            Check(timer.ElapsedMilliseconds < 10000, "Timed out: " + description);
            await Task.Delay(40);
        }
    }

    public static async Task<string> Run()
    {
        await RunTwo();
        await RunSix();
        return "PASS: 2- and 6-member fixtures; NPC first and reversed hierarchy; exact ID/body references at Begin; Continue restores only party activity. No production saves accessed.";
    }
    public static Task<string> RunTwo() => RunCount(2);
    public static Task<string> RunSix() => RunCount(6);

    static async Task<string> RunCount(int count)
    {
        Check(Application.isPlaying, "Enter Play mode first. This fixture replaces the current test campaign.");
        CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/NpcStoryRuntimeSlots");
        PartySelectionSession.Clear();
        SceneManager.LoadScene("PartySelection");
        await Until(() => SceneManager.GetActiveScene().name == "PartySelection" && Object.FindAnyObjectByType<PartySelectionController>(), "party selection");
        var selection = Object.FindAnyObjectByType<PartySelectionController>();
        foreach (string id in Ids.Take(2)) if (!PartySelectionSession.Selected.Contains(id)) selection.Toggle(id);
        Check(PartySelectionSession.Selected.SequenceEqual(Ids.Take(2)), "Opening pair differs from the fixture IDs.");
        Check(selection.Continue.IsInteractable(), "Party confirmation is unavailable.");
        selection.Continue.onClick.Invoke();
        await Until(() => SceneManager.GetActiveScene().name == "HomeSelection" && Object.FindAnyObjectByType<HomeSelectionController>(), "home selection");
        var home = Object.FindAnyObjectByType<HomeSelectionController>();
        home.Select(0); home.Continue.onClick.Invoke();
        await Until(() => Owner && Owner.Campaign != null, "settlement initialization");
        var owner = Owner;
        owner.Introduction.Restore(10);
        foreach (string id in Ids.Skip(2).Take(count - 2))
        {
            var candidate = owner.Roster.Candidates.Single(c => c.Id == id);
            Check(owner.Campaign.AddResident(new Adventurer(candidate.DisplayName, candidate.RoleTitle, candidate.Description,
                candidate.Health, candidate.Aim, candidate.BagCapacity), 6), "Fixture recruitment failed: " + id);
            PartySelectionSession.Selected.Add(id);
        }
        owner.RefreshMembers();
        var arrival = owner.ArrivalPanel;
        Check(arrival.Begin(owner.Campaign.Party.ToArray(), owner.ExpeditionPanel.Destinations.Single(d => d.Id == "mall")), "Fixture departure failed.");
        await Until(() => !arrival.InTransit, "arrival fade");
        if (arrival.Popup.activeSelf) arrival.ClosePopup();
        Check(arrival.Participants.Count == count && arrival.PartyPawns.Count == count, "Fixture party count differs.");

        // Use the actual NPC body prefab, but not the live story component: its LateUpdate
        // intentionally hides the story actor during combat and would obscure Continue's ownership test.
        Check(arrival.Story && arrival.Story.NpcPrefab, "Doyun prefab is unavailable.");
        var npc = Object.Instantiate(arrival.Story.NpcPrefab, arrival.PawnRoot);
        npc.name = "IdentityFixture_NonPartyNpc";
        npc.SetActive(true);
        var npcBody = npc.transform.Find("Body").GetComponent<SpriteRenderer>();
        Check(npcBody && npcBody.sprite, "Fixture NPC has no body.");
        var npcSprite = npcBody.sprite;
        foreach (var pawn in arrival.PartyPawns) pawn.transform.SetAsFirstSibling();
        npc.transform.SetAsFirstSibling();
        Check(arrival.PawnRoot.GetChild(0) == npc.transform && !arrival.PartyPawns.Contains(npc), "NPC was not placed before the party.");
        Check(arrival.PawnRoot.GetChild(1) == arrival.PartyPawns[count - 1].transform, "Party hierarchy was not reversed.");

        Check(arrival.Encounter.OpenThreat("대원 ID 검수용 조우", "검수", null), "Fixture encounter failed.");
        var battle = arrival.Encounter.Battle;
        // An explicit empty lineup keeps this a stable infected fixture, independent of creature roster randomness.
        battle.NextLineup = new BattleCreature[0];
        battle.Begin(1);
        Check(battle.IsOpen, "Begin did not open battle.");
        string identities = Inspect(owner);
        Check(battle.State.Units.Count(u => !u.Enemy) == count, "Begin dropped a fixture member.");
        Check(battle.State.Units.Where(u => !u.Enemy).Select(u => owner.Roster.BodyFor(CampaignPersistence.MemberId(owner, u.Person), arrival.Scout, arrival.Medic)).Distinct().Count() == count,
            "Expected body references are not distinct.");

        // Force a terminal fixture through the public state API, then supply alternating
        // survivor states to test Continue's ownership. This is not a combat balance test.
        foreach (var enemy in battle.State.Units.Where(u => u.Enemy)) enemy.Health = 0;
        Check(battle.State.Guard() && battle.State.Outcome == FieldBattleOutcome.Victory, "Fixture did not reach a terminal state.");
        for (int i = 0; i < arrival.Participants.Count; i++)
        {
            var member = arrival.Participants[i];
            member.Health = i % 2 == 0 ? 0 : member.MaxHealth;
            battle.State.Units.Single(u => u.Person == member).Health = member.Health;
        }
        battle.Result.SetActive(true);
        battle.Continue();
        Check(!battle.IsOpen, "Continue did not finish the fixture battle.");
        for (int i = 0; i < count; i++)
            Check(arrival.PartyPawns[i].activeSelf == (arrival.Participants[i].Health > 0), "Wrong party activity after Continue: " + i);
        Check(npc && npc.activeSelf && npcBody.sprite == npcSprite, "Continue changed a non-party NPC.");
        Check(arrival.PawnRoot.GetChild(0) == npc.transform, "Continue unexpectedly reordered the field hierarchy.");
        return "PASS " + count + " members: " + identities + "; NPC-first/reversed hierarchy and party-only Continue.";
    }

    public static string Current()
    {
        Check(Application.isPlaying, "Enter Play mode first.");
        return "PASS current battle: " + Inspect(Owner) + ". Read-only; no cleanup, restaging, scene, save or campaign changes.";
    }

    static string Inspect(SettlementController owner)
    {
        Check(owner && owner.Campaign != null && owner.Roster, "A live settlement campaign is required.");
        var arrival = owner.ArrivalPanel;
        Check(arrival && arrival.Encounter && arrival.Encounter.Battle, "Arrival/battle references are unavailable.");
        var battle = arrival.Encounter.Battle;
        Check(battle.IsOpen && battle.State != null, "Open a battle before calling Current.");
        int checkedCount = 0;
        var names = new System.Collections.Generic.List<string>();
        for (int i = 0; i < battle.State.Units.Count; i++)
        {
            var unit = battle.State.Units[i];
            if (unit.Enemy) continue;
            int member = arrival.Participants.ToList().IndexOf(unit.Person);
            Check(member >= 0 && member < arrival.PartyPawns.Count && arrival.PartyPawns[member], "Missing tracked pawn for " + unit.Name);
            string id = CampaignPersistence.MemberId(owner, unit.Person);
            var candidate = owner.Roster.Candidates.Single(c => c.Id == id);
            var fieldBody = arrival.PartyPawns[member].transform.Find("Body")?.GetComponent<SpriteRenderer>();
            var view = battle.PawnView(i);
            Check(candidate.Body && fieldBody && fieldBody.sprite == candidate.Body, "Field body/ID mismatch: " + id);
            Check(view && view.Body && view.Body.sprite == candidate.Body && view.Body.sprite == fieldBody.sprite,
                "Battle body/ID mismatch: " + id + " at unit " + i + " / party " + member);
            Check(member < arrival.Cards.Count && arrival.Cards[member].Portrait.sprite == candidate.Portrait, "Portrait/ID mismatch: " + id);
            names.Add(id); checkedCount++;
        }
        Check(checkedCount > 0, "No allied units were inspected.");
        return string.Join(", ", names);
    }
}
