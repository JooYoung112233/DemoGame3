using System;
using System.Threading.Tasks;
using EastTrain;
using UnityEngine;

public static class VerifyEastTrainFeedback
{
    static EastTrainDemo Demo => UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>();
    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    public static string Interior()
    {
        var d = Demo; d.State = new TrainState { Fuel = 35, Integrity = 1, Throttle = .35f, Vent = true };
        d.PlayerX = -2.9f; d.PlayerY = -1.65f; d.Driving = d.Outside = d.Repairing = false; d.Carry = 0;
        return "Interior presentation";
    }
    public static string Drive()
    {
        var d = Demo; d.State = new TrainState { Fuel = 35, Integrity = 1, Throttle = .55f, Brake = false };
        d.Outside = d.Repairing = d.Driving = false; d.Carry = 0; d.PlayerX = 6.6f; d.PlayerY = .78f; d.Interact();
        return "Driving presentation";
    }
    public static async Task<string> Verify()
    {
        Interior(); var d = Demo;
        await Task.Delay(1400);
        Check(d.ViewMode == "interior" && Camera.main.orthographicSize < 4, "Interior camera did not zoom in");
        float from = d.PlayerX; d.MoveTraveller(1, 0, .25f);
        Check(d.PlayerX - from > 1.5f, "Normal walking speed is not responsive");
        d.State.Warmth = 0; d.Carry = 1; from = d.PlayerX; d.MoveTraveller(1, 0, .25f);
        Check(d.PlayerX - from > 1.15f, "Cold carrying speed is still too slow");
        Drive(); await Task.Delay(1400);
        Check(d.Driving && Camera.main.orthographicSize > 7.8f, "Driving camera did not show landscape");
        Check(GameObject.Find("Driving mode marker") == null && GameObject.Find("Occupied driving station") != null, "Use physical control light instead of persistent driving banner");
        Check(d.DriveStatus.Contains("주행 중"), "Moving state was not communicated");
        d.State.Brake = true; await Task.Delay(100);
        Check(d.DriveStatus.Contains("제동 중"), "Brake cause is hidden");
        d.State.Fuel = 0; Check(d.DriveStatus.Contains("연료 없음"), "Fuel cause is hidden");
        d.State.Integrity = 0; Check(d.DriveStatus.Contains("수리 필요"), "Broken engine cause is hidden");
        d.State.Fire = true; Check(d.DriveStatus.Contains("화재"), "Fire cause is hidden");
        d.State.Fire = false; d.Driving = false; d.PlayerX = 4.7f;
        await Task.Delay(1400);
        Check(Camera.main.orthographicSize < 4 && GameObject.Find("Driving mode marker") == null, "Leaving controls did not clear driving mode and zoom in");
        Check(!d.State.EngineRunning, "Broken empty engine incorrectly reports running");
        return "PASS: interior zoom, faster walk/carry, driving zoom, physical control light without banner, motion/brake/fuel/repair/fire statuses, leaving controls and inactive engine (12 checks).";
    }
}
