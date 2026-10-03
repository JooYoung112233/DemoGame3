using System;
using System.Linq;
using System.Threading.Tasks;
using EastTrain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyEastTrainPresentation
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static TextMesh[] VisibleText() => UnityEngine.Object.FindObjectsByType<TextMesh>()
        .Where(t => t.GetComponent<Renderer>().enabled && t.gameObject.activeInHierarchy).ToArray();
    public static async Task<string> Run()
    {
        var d = UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>();
        Check(d != null && Application.isPlaying, "Play mode required");
        var previousUI = d.PrototypeUI; d.PrototypeUI = false;
        var previous = Keyboard.current; var key = InputSystem.AddDevice<Keyboard>("PresentationVerification"); key.MakeCurrent();
        try
        {
            d.State = new TrainState(); d.Driving = d.Repairing = d.Outside = false;
            d.PlayerX = -7.5f; d.PlayerY = -1.65f;
            await Task.Delay(3000);
            Check(VisibleText().Length == 0 && !d.ContextHintVisible, "Idle station labels or persistent hint remain");
            InputSystem.QueueStateEvent(key, new KeyboardState(Key.Tab)); await Task.Delay(100);
            Check(d.ContextHintVisible && VisibleText().Length == 1, "Tab must reveal only one contextual hint");
            Check(VisibleText()[0].GetComponent<Renderer>().bounds.size.y < .35f, "Context hint text is oversized in close view");
            InputSystem.QueueStateEvent(key, new KeyboardState()); await Task.Delay(100);
            Check(!d.ContextHintVisible, "Tab release did not clear expired hint");
            d.PlayerX = EastTrainDemo.RepairStationX; d.Interact(); await Task.Delay(100);
            Check(d.Repairing && GameObject.Find("Mechanical timing tool") != null, "Repair tool should appear only during repair");
            d.Repairing = false; d.PlayerX = 6.6f; d.PlayerY = .78f;
            d.State.Fuel = 40; d.State.Integrity = 1; d.Interact();
            await Task.Delay(3900);
            Check(d.Driving && GameObject.Find("Driving mode marker") == null, "Large driving banner remains");
            Check(GameObject.Find("Occupied driving station") != null, "Physical driving light missing");
            Check(VisibleText().Length == 0, "Driving text did not expire");
            Check(GameObject.Find("Mechanical timing tool") == null, "Repair dial remains outside repairing");
            Check(GameObject.Find("Traveller") != null && GameObject.Find("Locomotive / cutaway") != null
                && !d.CrewMode, "Solo placeholder presentation is not active");
            d.Driving = false; d.PlayerX = 0; d.PlayerY = -1.65f;
            return "PASS: no persistent text, Tab reveal/release, small context text, contextual repair dial, no driving banner, physical control light, driving hint expiry, solo placeholders (10 checks).";
        }
        finally { d.PrototypeUI = previousUI; InputSystem.RemoveDevice(key); if (previous != null && previous.added) previous.MakeCurrent(); }
    }
}
