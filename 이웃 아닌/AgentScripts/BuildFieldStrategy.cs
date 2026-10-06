using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Site board 1차 (기획/탐험-전략요소-설계안.md): wires ExpeditionSiteThreat into the arrival panel,
// adds the 관리실 선반 search target, door footstep tags, the resident head tag and the 숨죽이기 button.
// Idempotent: existing pieces are kept (edit them in the prefab).
public static class BuildFieldStrategy
{
    const string P = "Assets/Prefabs/Settlement/";
    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("Missing " + path);
    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }
    const string DenDescription = "무언가가 모아 둔 물건 더미입니다.\n굴이 빈 틈에만 손댈 수 있습니다.";
    // SettlementScreen overrides the nested panel's arrays (tuned drops), so the shelf is added there as well.
    static void AddDenShelf(ExpeditionArrivalPanel arrival, int den, List<string> log, string where)
    {
        var loot = arrival.Loot; var search = arrival.Search;
        if (loot.Sites.Length <= den)
        {
            var sites = loot.Sites.ToList();
            while (sites.Count < den) sites.Add(new ExpeditionLootPanel.Site { Drops = new ExpeditionLootPanel.Drop[0], Room = -1 });
            sites.Add(new ExpeditionLootPanel.Site { Room = FieldSiteState.Corridor, Drops = new[] {
                new ExpeditionLootPanel.Drop { Id = "rope", Count = 2, Chance = 75 }, new ExpeditionLootPanel.Drop { Id = "cloth", Count = 2, Chance = 70 },
                new ExpeditionLootPanel.Drop { Id = "bandage", Count = 1, Chance = 60 }, new ExpeditionLootPanel.Drop { Id = "ammo", Count = 2, Chance = 50 } } });
            loot.Sites = sites.ToArray(); EditorUtility.SetDirty(loot); log.Add(where + "den shelf site " + den);
        }
        if (arrival.ObjectNames.Length <= den)
        {
            arrival.ObjectNames = arrival.ObjectNames.Concat(Enumerable.Repeat("", den - arrival.ObjectNames.Length)).Append("관리실 선반").ToArray();
            EditorUtility.SetDirty(arrival); log.Add(where + "den shelf name");
        }
        if (arrival.ObjectDescriptions.Length <= den)
        {
            arrival.ObjectDescriptions = arrival.ObjectDescriptions.Concat(Enumerable.Repeat("", den - arrival.ObjectDescriptions.Length))
                .Append(DenDescription).ToArray();
            EditorUtility.SetDirty(arrival); log.Add(where + "den shelf description");
        }
        // The description paper holds two short lines.
        if (arrival.ObjectDescriptions[den] != DenDescription && arrival.ObjectDescriptions[den].StartsWith("무언가가 모아 둔 듯한"))
        {
            var d = arrival.ObjectDescriptions.ToArray(); d[den] = DenDescription; arrival.ObjectDescriptions = d; EditorUtility.SetDirty(arrival); log.Add(where + "den shelf description (2 lines)");
        }
        if (search.ObjectIcons.Length <= den)
        {
            search.ObjectIcons = search.ObjectIcons.Concat(Enumerable.Repeat(search.ObjectIcons.LastOrDefault(), den + 1 - search.ObjectIcons.Length)).ToArray();
            EditorUtility.SetDirty(search); log.Add(where + "den shelf icon");
        }
    }
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var log = new List<string>();
        var root = PrefabUtility.LoadPrefabContents(P + "ExpeditionArrivalPanel.prefab");
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); var rooms = arrival.Rooms;
            var riskText = arrival.Main.transform.Find("Risk").GetComponent<Text>(); var riskPaper = arrival.Main.transform.Find("RiskPaper").GetComponent<Image>();

            // 1. 관리실 선반: the den's shelf, searched from the office door while the den is empty. Rope is the settlement's bottleneck.
            int den = Array.IndexOf(CampaignPersistence.SiteIds, "mall.office.shelf");
            AddDenShelf(arrival, den, log, "");

            // 2. The threat component and its sound.
            var threat = root.GetComponent<ExpeditionSiteThreat>();
            if (!threat)
            {
                threat = root.AddComponent<ExpeditionSiteThreat>(); arrival.Threat = threat; EditorUtility.SetDirty(arrival);
                threat.Creatures = Load<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset"); threat.DenSite = den;
                var audio = new GameObject("SiteThreatAudio", typeof(AudioSource)).GetComponent<AudioSource>(); audio.transform.SetParent(root.transform, false); audio.playOnAwake = false; audio.spatialBlend = 0;
                threat.Audio = audio;
                threat.Footsteps = Load<AudioClip>("Assets/Audio/Battle/battle-step.wav");
                threat.Breathing = Load<AudioClip>("Assets/Audio/Battle/Creatures/creature-listen.wav");
                threat.Passing = Load<AudioClip>("Assets/Audio/Battle/Creatures/creature-drip.wav");
                threat.TimeFormat = "남은 {0}턴"; threat.OvertimeFormat = "초과 +{0}턴";
                log.Add("threat component");
            }
            if (!arrival.Threat) { arrival.Threat = threat; EditorUtility.SetDirty(arrival); }

            // 3. Footstep tag prefab: paper strip with one short line, pulsing when something is about to come through.
            var markerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(P + "FieldThreatMarker.prefab");
            if (!markerAsset)
            {
                var go = new GameObject("FieldThreatMarker", typeof(RectTransform)); var r = (RectTransform)go.transform; r.sizeDelta = new Vector2(176, 42); r.pivot = new Vector2(.5f, 0);
                var m = go.AddComponent<FieldThreatMarker>(); m.Root = r;
                var paper = Rect("Paper", r, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(176, 42)).gameObject.AddComponent<Image>(); paper.sprite = riskPaper.sprite; paper.type = riskPaper.type; paper.raycastTarget = false; m.Paper = paper;
                var label = Rect("Label", r, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 1), new Vector2(164, 40)).gameObject.AddComponent<Text>();
                label.font = riskText.font; label.fontSize = 22; label.alignment = TextAnchor.MiddleCenter; label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false; label.text = "발소리"; m.Label = label;
                go.SetActive(false);
                markerAsset = PrefabUtility.SaveAsPrefabAsset(go, P + "FieldThreatMarker.prefab"); Object.DestroyImmediate(go); log.Add("marker prefab");
            }
            // 4. One tag per door, on the side of the room it opens into (room, the neighbour behind it).
            var doors = new (Button button, int room, int from)[] {
                (arrival.Objects[3], FieldSiteState.Arcade, FieldSiteState.Corridor), (rooms.CorridorBack, FieldSiteState.Corridor, FieldSiteState.Arcade),
                (rooms.LockedDoor, FieldSiteState.Corridor, FieldSiteState.Storage), (rooms.OfficeDoor, FieldSiteState.Corridor, FieldSiteState.Den),
                (rooms.StorageBack, FieldSiteState.Storage, FieldSiteState.Corridor) };
            var markers = (threat.DoorMarkers ?? new FieldThreatMarker[0]).Where(x => x).ToList();
            foreach (var d in doors)
            {
                if (!d.button || markers.Any(x => x.Room == d.room && x.From == d.from)) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(markerAsset, d.button.transform); inst.name = "Footsteps";
                var rt = (RectTransform)inst.transform; rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1); rt.anchoredPosition = new Vector2(0, 8);
                var m = inst.GetComponent<FieldThreatMarker>(); m.Room = d.room; m.From = d.from; inst.SetActive(false); markers.Add(m);
                log.Add("door tag " + FieldSiteState.RoomNames[d.room] + "←" + FieldSiteState.RoomNames[d.from]);
            }
            threat.DoorMarkers = markers.ToArray();
            if (!threat.ResidentTag)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(markerAsset, arrival.Main.transform); inst.name = "ResidentTag";
                var rt = (RectTransform)inst.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.anchoredPosition = new Vector2(960, -400); inst.SetActive(false);
                threat.ResidentTag = inst.GetComponent<FieldThreatMarker>(); log.Add("resident tag");
            }

            // 5. 숨죽이기: a copy of the bottom-left paper button, mirrored to the bottom-right (same size and baseline).
            if (!threat.Hush)
            {
                var source = arrival.Return; var copy = Object.Instantiate(source.gameObject, source.transform.parent); copy.name = "Hush";
                var rt = (RectTransform)copy.transform; var src = (RectTransform)source.transform;
                rt.anchoredPosition = new Vector2(1920 - src.anchoredPosition.x - src.sizeDelta.x, src.anchoredPosition.y);
                var button = copy.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
                var text = copy.GetComponentsInChildren<Text>(true).OrderByDescending(t => t.text.Length).FirstOrDefault(); if (text) text.text = "숨죽이기 · 1턴";
                foreach (var img in copy.GetComponentsInChildren<Image>(true).Where(i => i.gameObject != copy)) if (img.rectTransform.sizeDelta.x < 60) img.gameObject.SetActive(false); // drop the back arrow
                threat.Hush = button; copy.SetActive(false); log.Add("hush button");
            }
            // 6. The risk paper grows downward to hold the noise gauge under 위험도 (same width and top edge).
            var paperRect = riskPaper.rectTransform; var textRect = riskText.rectTransform;
            if (paperRect.sizeDelta.y < 100)
            {
                paperRect.sizeDelta = new Vector2(paperRect.sizeDelta.x, 114); textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, 106);
                riskText.supportRichText = true; riskText.lineSpacing = .95f; EditorUtility.SetDirty(riskText); log.Add("risk paper two lines");
            }
            // 7. The resident's standing spots on open floor, clear of the search markers (measured on the room screens).
            var oldSpots = new[] { new Vector3(5.4f, -1.2f, 0), new Vector3(5.2f, -1.3f, 0), new Vector3(-4.6f, -1.2f, 0), Vector3.zero };
            if (threat.ResidentSpots == null || threat.ResidentSpots.SequenceEqual(oldSpots))
            {
                threat.ResidentSpots = new[] { new Vector3(3.5f, -1.5f, 0), new Vector3(-.95f, -1.7f, 0), new Vector3(-.5f, -1.65f, 0), Vector3.zero }; log.Add("resident spots");
            }
            // 8. Door tags stay inside the HUD margin (80 px from the screen edge, like the corner papers).
            var main = (RectTransform)arrival.Main.transform; var corners = new Vector3[4];
            foreach (var m in threat.DoorMarkers)
            {
                var rt = (RectTransform)m.transform; rt.GetWorldCorners(corners);
                float left = main.InverseTransformPoint(corners[0]).x - main.rect.xMin, right = main.InverseTransformPoint(corners[2]).x - main.rect.xMin;
                float k = (right - left) / Mathf.Max(1, rt.rect.width), shift = right > main.rect.width - 80 ? main.rect.width - 80 - right : left < 80 ? 80 - left : 0;
                if (Mathf.Abs(shift) > .5f) { rt.anchoredPosition += new Vector2(shift / k, 0); log.Add("tag inside margin " + FieldSiteState.RoomNames[m.Room] + "<-" + FieldSiteState.RoomNames[m.From]); }
            }
            EditorUtility.SetDirty(threat);
            PrefabUtility.SaveAsPrefabAsset(root, P + "ExpeditionArrivalPanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets(); }
        var screen = PrefabUtility.LoadPrefabContents(P + "SettlementScreen.prefab");
        try
        {
            foreach (var nested in screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true)) AddDenShelf(nested, Array.IndexOf(CampaignPersistence.SiteIds, "mall.office.shelf"), log, "SettlementScreen: ");
            if (log.Any(l => l.StartsWith("SettlementScreen"))) PrefabUtility.SaveAsPrefabAsset(screen, P + "SettlementScreen.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(screen); AssetDatabase.SaveAssets(); }
        return log.Count == 0 ? "Already built." : "Built: " + string.Join("; ", log);
    }
}
