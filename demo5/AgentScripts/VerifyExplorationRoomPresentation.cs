using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class VerifyExplorationRoomPresentation
{
    public static string Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ExpeditionWorld.prefab");
        Require(prefab && prefab.GetComponent<ExplorationRoomPresentation>(), "Run BuildExplorationRoomPresentation first.");
        var previousScene = SceneManager.GetActiveScene();
        bool wasPlaying = EditorApplication.isPlaying;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var holder = new GameObject("Room presentation verification") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false); SceneManager.MoveGameObjectToScene(holder, scene);
            var world = Object.Instantiate(prefab, holder.transform);
            var presentation = world.GetComponent<ExplorationRoomPresentation>();
            var party = new List<GameObject>();
            for (int i = 0; i < 6; i++)
            {
                var pawn = new GameObject("Party " + i); pawn.transform.SetParent(presentation.PawnRoot, false);
                pawn.transform.localPosition = presentation.ArrivalPosition(-1, 0, i, 6);
                pawn.AddComponent<PawnGroundShadow>();
                party.Add(pawn);
            }
            var resident = new GameObject("Resident"); resident.transform.SetParent(presentation.PawnRoot, false);
            resident.transform.localPosition = new Vector3(4, -1.1f, 0); resident.AddComponent<PawnGroundShadow>();
            Vector3 residentStart = resident.transform.localPosition;

            foreach (var route in presentation.Routes)
            {
                for (int count = 1; count <= 6; count++)
                {
                    var positions = Enumerable.Range(0, count).Select(i => presentation.ArrivalPosition(route.FromRoom, route.ToRoom, i, count)).ToArray();
                    foreach (var p in positions)
                        Require(Mathf.Abs(p.x) < 8 && p.y > -1.87f && p.y < -1.4f, "Formation collides with a frame edge/HUD: " + route.Label + " / " + count);
                    for (int i = 1; i < count; i++) Require(Vector3.Distance(positions[i - 1], positions[i]) >= 1.2f, "Six-person base spacing is too small.");
                }
                for (int i = 0; i < 6; i++)
                {
                    var start = party[i].transform.localPosition;
                    Require(Vector3.Distance(ExplorationRoomPresentation.TravelPoint(route, start, i, 0), start) < .0001f, "Travel starts with a teleport.");
                    Vector3 end = route.DepartureDoor + route.DepartureQueue * i;
                    Require(Vector3.Distance(ExplorationRoomPresentation.TravelPoint(route, start, i, 1), end) < .0001f, "Travel does not end at its doorway queue.");
                    for (int frame = 0; frame <= 100; frame++)
                    {
                        var p = ExplorationRoomPresentation.TravelPoint(route, start, i, frame / 100f);
                        Require(Mathf.Abs(p.x) <= 8.6f && p.y >= -2.05f && p.y <= .13f, "Walk leaves the authored floor corridor.");
                    }
                }
            }
            var returnRoute = presentation.Route(2, 1);
            Require(returnRoute != null && returnRoute.ArrivalDoor.x > 1.5f && returnRoute.ArrivalDoor.x < 3, "Storage return still arrives at the corridor's left door.");
            for (int room = 0; room < 3; room++)
            {
                presentation.ApplyRoom(room);
                var light = presentation.EntranceLight.transform.position;
                foreach (var shadow in presentation.PawnRoot.GetComponentsInChildren<PawnGroundShadow>(true))
                    Require(Vector2.Distance(shadow.LightPosition, light) < .0001f, "A pawn shadow disagrees with its actual room light.");
            }

            // Exercise the real navigation coroutine's first movement frame. Its captured move set must exclude world residents.
            var ownerObject = new GameObject("Temporary arrival owner"); ownerObject.transform.SetParent(holder.transform, false);
            var arrival = ownerObject.AddComponent<ExpeditionArrivalPanel>(); arrival.World = world; arrival.PawnRoot = presentation.PawnRoot;
            var navigation = ownerObject.AddComponent<ExpeditionRoomNavigation>(); navigation.MoveDuration = 999;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var backing = (List<GameObject>)typeof(ExpeditionArrivalPanel).GetField("pawns", flags).GetValue(arrival);
            backing.AddRange(party);
            typeof(ExpeditionRoomNavigation).GetField("owner", flags).SetValue(navigation, arrival);
            var travel = (IEnumerator)typeof(ExpeditionRoomNavigation).GetMethod("Travel", flags).Invoke(navigation, new object[] { 1, 1, 0 });
            Require(travel.MoveNext(), "Travel did not expose its movement frame.");
            var transforms = travel.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(List<Transform>)).Select(f => f.GetValue(travel) as List<Transform>).FirstOrDefault(x => x != null);
            Require(transforms != null && transforms.Count == 6 && !transforms.Contains(resident.transform), "Navigation is still moving non-party world pawns.");
            Require(resident.transform.localPosition == residentStart, "Travel moved the resident.");
            (travel as IDisposable)?.Dispose();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Require(SceneManager.GetActiveScene() == previousScene && EditorApplication.isPlaying == wasPlaying, "Verification changed editor scene/play state.");
        return "PASS: all directional gates, 1–6 member floor/HUD clearances, complete walk paths, central-door return, actual room-light/shadow agreement and real Travel coroutine excluding Resident. No game state or scene saved.";
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
