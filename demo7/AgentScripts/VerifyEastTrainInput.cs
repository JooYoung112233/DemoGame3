using System;
using System.Threading.Tasks;
using EastTrain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyEastTrainInput
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static async Task Hold(Keyboard keyboard, Key key, int milliseconds)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        await Task.Delay(milliseconds);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Task.Delay(120);
    }
    public static async Task<string> Run()
    {
        var d = UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>();
        if (d == null || !Application.isPlaying) throw new Exception("Play mode required");
        var original = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>("EastTrainVerification");
        keyboard.MakeCurrent();
        try
        {
            d.State = new TrainState(); d.Outside = d.Driving = d.Repairing = false;
            d.Carry = 0; d.PlayerX = -7.5f; d.PlayerY = -1.65f;
            await Hold(keyboard, Key.E, 120);
            Check(d.Carry == 1, "E input did not pick up fuel");
            await Hold(keyboard, Key.D, 400);
            Check(d.PlayerX > -7, "D input did not move the traveller");
            d.PlayerX = -5.2f; await Hold(keyboard, Key.E, 120);
            Check(d.Carry == 0 && d.State.Fuel > 27, "E input did not load furnace");
            d.PlayerX = EastTrainDemo.RepairStationX; await Hold(keyboard, Key.E, 120);
            Check(d.Repairing, "E input did not start repair");
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (d.RepairPhase < .66f && DateTime.UtcNow < deadline) await Task.Delay(10);
            Check(d.RepairPhase >= .66f, "Repair needle did not advance");
            await Hold(keyboard, Key.Space, 80);
            Check(d.State.Integrity > .3f, "SPACE timing input did not repair");
            await Hold(keyboard, Key.E, 120);
            Check(!d.Repairing, "E did not leave repair station");
            d.PlayerX = 3.2f; await Hold(keyboard, Key.W, 1800);
            Check(d.PlayerY > .7f, "W did not climb ladder");
            d.PlayerX = 6.6f; await Hold(keyboard, Key.E, 120);
            Check(d.Driving, "E did not take driving control");
            await Hold(keyboard, Key.Space, 120);
            Check(!d.State.Brake, "SPACE did not release brake");
            await Hold(keyboard, Key.W, 1000);
            Check(d.State.Throttle > .2f && d.State.Distance > 0, "W did not drive train");
            await Hold(keyboard, Key.E, 120);
            Check(!d.Driving, "E did not leave driving control");
            d.State = new TrainState(); d.Outside = true; d.Carry = 0; d.PlayerX = 12; d.PlayerY = -2.62f;
            await Hold(keyboard, Key.E, 3400);
            Check(d.Carry == 2, "Holding E did not gather emergency tinder");
            d.State.Distance = TrainState.SnowStop; d.PlayerX = TrainState.SnowStop + 11;
            await Hold(keyboard, Key.E, 5500);
            Check(d.State.Snow == 0, "Holding E did not shovel blocked track");
            d.Outside = false; d.PlayerX = 6.6f; d.PlayerY = .78f;
            d.State.Distance = TrainState.Destination - 1; d.State.Fuel = 28; d.State.Integrity = 1;
            d.State.Brake = false; d.State.Throttle = 1; d.State.Speed = 5;
            await Task.Delay(800);
            Check(d.State.Arrived && d.State.Speed == 0, "Runtime arrival did not finish prologue");
            return "PASS: actual Input System events for pick up, walk, refuel, repair entry, timed SPACE, cancel repair, climb, take controls, brake, throttle/travel, leave controls, emergency tinder, shovelling; runtime arrival (14 checks).";
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (original != null && original.added) original.MakeCurrent();
            d.State.Brake = true; d.State.Throttle = 0;
        }
    }
}
