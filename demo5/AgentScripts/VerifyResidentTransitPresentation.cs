using System;
using System.Linq;
using System.Reflection;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated display test: no saved scenes, encounter callbacks, clocks or production spawn data are changed.
public static class VerifyResidentTransitPresentation
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string Run()
    {
        var previousScene = SceneManager.GetActiveScene();
        bool wasPlaying = EditorApplication.isPlaying;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var holder = new GameObject("Resident transit display verification") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false); SceneManager.MoveGameObjectToScene(holder, scene);
            var host = Child(holder, "Arrival");
            var arrival = host.AddComponent<ExpeditionArrivalPanel>();
            arrival.Main = host.AddComponent<CanvasGroup>();
            arrival.Status = Child(host, "Status", typeof(RectTransform)).AddComponent<Text>();
            arrival.PawnRoot = Child(holder, "Pawns").transform;
            arrival.PawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/FieldPawn.prefab");
            var threat = host.AddComponent<ExpeditionSiteThreat>();
            threat.Creatures = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
            Require(arrival.PawnPrefab && threat.Creatures && threat.Creatures.Find(threat.ResidentId)?.Body,
                "Resident source assets are missing.");
            threat.ResidentTag = Child(holder, "Resident tag", typeof(RectTransform)).AddComponent<FieldThreatMarker>();
            threat.Initialize(arrival);

            // A later-visit resident naturally takes the first step out of its den into the corridor.
            var state = new FieldSiteState(threat.Rules, () => 0, false, 3);
            state.MoveParty(FieldSiteState.Corridor); state.EndTurn(0, false);
            Require(state.Visible, "The fixture must begin with a visible corridor resident.");
            typeof(ExpeditionSiteThreat).GetProperty("State").GetSetMethod(true).Invoke(threat, new object[] { state });
            var stateFields = typeof(FieldSiteState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var before = stateFields.Select(field => field.GetValue(state)).ToArray();

            var resident = Child(arrival.PawnRoot.gameObject, "Resident");
            resident.transform.localPosition = threat.ResidentSpots[FieldSiteState.Corridor];
            var originalPosition = resident.transform.localPosition;
            typeof(ExpeditionSiteThreat).GetField("residentPawn", Private).SetValue(threat, resident);
            threat.ResidentTag.Show("fixture", true);
            int childCount = arrival.PawnRoot.childCount;

            arrival.SetRoomTransit(true);
            typeof(ExpeditionSiteThreat).GetMethod("LateUpdate", Private).Invoke(threat, null);
            Require(!resident.activeSelf && !threat.ResidentTag.gameObject.activeSelf,
                "LateUpdate left a resident/tag visible during the room fade.");
            Require(resident.transform.localPosition == originalPosition, "The display filter moved the resident.");

            // A refresh during the fade must not spawn, destroy or reveal the old resident.
            resident.SetActive(true); threat.ResidentTag.Show("fixture", true);
            threat.Refresh();
            Require(!resident.activeSelf && !threat.ResidentTag.gameObject.activeSelf,
                "Refresh revealed a resident/tag while InTransit.");
            Require(arrival.PawnRoot.childCount == childCount &&
                (GameObject)typeof(ExpeditionSiteThreat).GetField("residentPawn", Private).GetValue(threat) == resident,
                "Transit changed the resident instance or spawn count.");

            // The original state-based refresh restores that same standee only when arrival has ended.
            arrival.SetRoomTransit(false); threat.Refresh();
            Require(resident.activeSelf && threat.ResidentTag.gameObject.activeSelf,
                "A visible resident was not restored by the arrived-state refresh.");
            Require((GameObject)typeof(ExpeditionSiteThreat).GetField("residentPawn", Private).GetValue(threat) == resident,
                "Arrival unnecessarily replaced the hidden standee.");
            Require(resident.transform.localPosition == threat.ResidentSpots[state.ResidentRoom],
                "Restored standee did not use the arrived state's room position.");
            Require(stateFields.Select((field, i) => Equals(field.GetValue(state), before[i])).All(equal => equal),
                "A display-only refresh changed the site state, clock, danger, encounter or patrol data.");

            // Once the resident is not in this room, the transit filter itself must never reveal it.
            resident.SetActive(false); threat.ResidentTag.Hide();
            state.MoveParty(FieldSiteState.Arcade);
            bool filtered = (bool)typeof(ExpeditionSiteThreat).GetMethod("HideResidentDuringTransit", Private).Invoke(threat, null);
            Require(!filtered && !resident.activeSelf && !threat.ResidentTag.gameObject.activeSelf,
                "Ending transit bypassed the existing visibility rule.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Require(SceneManager.GetActiveScene() == previousScene && EditorApplication.isPlaying == wasPlaying,
            "Verification changed the open scene or play state.");
        return "PASS: resident and tag hidden in LateUpdate/Refresh during transit; same instance restored at arrived-room spot; no movement/spawn/state/turn/encounter changes from the display filter. No scene saved.";
    }

    static GameObject Child(GameObject parent, string name, params Type[] components)
    {
        var child = new GameObject(name, components); child.transform.SetParent(parent.transform, false); return child;
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
