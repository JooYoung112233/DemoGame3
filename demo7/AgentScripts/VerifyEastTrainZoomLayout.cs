using System;
using System.Threading.Tasks;
using EastTrain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyEastTrainZoomLayout
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static async Task<string> Run()
    {
        var d = UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>();
        d.State = new TrainState(); d.Driving = d.Outside = d.Repairing = false;
        d.PlayerX = 0; d.PlayerY = -1.65f; d.ResetZoom();
        var previous = Mouse.current; var mouse = InputSystem.AddDevice<Mouse>("ZoomVerification"); mouse.MakeCurrent();
        try
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
            await Task.Delay(150);
            Check(d.InteriorZoom > 4, "Mouse wheel did not zoom out");
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 120) });
            await Task.Delay(150);
            Check(d.InteriorZoom < 4, "Mouse wheel did not zoom in");
            d.State.Fuel = 40; d.State.Integrity = 1; d.State.Throttle = .55f; d.State.Brake = false;
            await Task.Delay(1700);
            Check(!d.Driving && d.JourneyView && Camera.main.orthographicSize >= 9.4f, "Walking on a moving train must retain wide view");
            var ahead = Camera.main.WorldToViewportPoint(new Vector3(d.State.Distance + 22, -2.4f, 0));
            Check(ahead.x > 0 && ahead.x < 1 && ahead.y > 0 && ahead.y < 1, "Fuel ahead of locomotive is outside camera");
            for (int i = 0; i < 20; i++) d.AdjustZoom(120);
            Check(d.JourneyZoom >= 9.6f, "Zoom-in hides approaching fuel during travel");
            d.State.Speed = 0; d.State.Brake = true; d.State.Throttle = 0; d.State.Integrity = 0;
            d.PlayerX = 1.5f; d.Interact(); Check(!d.Repairing, "Old crowded location still starts repairs");
            d.PlayerX = EastTrainDemo.RepairStationX; d.Interact(); Check(d.Repairing, "New separated workbench cannot be used");
            await Task.Delay(1400); Check(Camera.main.orthographicSize < 4.1f, "Stopped train does not return to interior zoom");
            d.Repairing = false; d.ResetZoom();
            return "PASS: actual wheel zoom in/out, wide view while walking on moving train, ahead pickup visibility, travel zoom limit, separated repair interaction, stop zoom (8 checks).";
        }
        finally { InputSystem.RemoveDevice(mouse); if (previous != null && previous.added) previous.MakeCurrent(); }
    }
}
