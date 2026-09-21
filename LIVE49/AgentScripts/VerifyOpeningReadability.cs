using System;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Title;
using Live49.UI;
using TMPro;
using UnityEngine;

public static class VerifyOpeningReadability
{
    static CanvasGroup Group(string name) => UnityEngine.Object.FindObjectsByType<CanvasGroup>().Single(g => g.name == name);
    static TMP_Text Text(string name) => UnityEngine.Object.FindObjectsByType<TMP_Text>().Single(t => t.name == name);
    static void CheckNameplate(string expected, bool right)
    {
        var label = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).Single(t => t.name == "NameLabel");
        var rt = (RectTransform)label.transform.parent;
        if (rt.gameObject.activeSelf != !string.IsNullOrEmpty(expected)) throw new Exception("Narration nameplate visibility incorrect");
        if (string.IsNullOrEmpty(expected)) return;
        if (label.text != expected) throw new Exception("Incorrect revealed speaker name: " + label.text);
        if (rt.anchorMin.x != (right ? 1f : 0f) || Mathf.Sign(rt.anchoredPosition.x) != (right ? -1f : 1f))
            throw new Exception("Incorrect speaker side: " + label.text);
    }
    static async Task Wait(Func<bool> condition, int ms = 10000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        while (!condition()) { if (DateTime.UtcNow >= end) throw new Exception("Opening condition timed out"); await Task.Delay(50); }
    }
    public static async Task<string> StartAndCheckBook()
    {
        var start = UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i => i.Id == "start");
        start.Owner.Confirm(start);
        await Wait(() => UnityEngine.Object.FindObjectsByType<TMP_Text>().Any(t => t.name == "CallText" && t.text == "수혁아."));
        await Task.Delay(800);
        if (Group("C0_PhotoWall").alpha != 0 || Group("C0_Present").alpha != 0 || Group("C0_BookIntro").alpha != 1)
            throw new Exception("Camper exposed before memory");
        if (Text("CallText").GetComponent<Typewriter>().IsTyping) throw new Exception("First call did not finish");
        await Wait(() => Group("CallOverlay").GetComponentsInChildren<DialogueAdvanceCue>().Single().GetComponent<CanvasGroup>().alpha > .99f);
        return "PASS: start leads to book + first call; camper/photo wall hidden; waiting for fresh input.";
    }
    public static async Task<string> EnterMemory()
    {
        FreshInput.SimulateAdvance();
        await Wait(() => Group("C0_MemoryPath").alpha == 1 && Text("Body").text.Length > 0);
        await Task.Delay(1600);
        if (Group("C0_BookIntro").alpha != 0 || Group("C0_Present").alpha != 0) throw new Exception("Incorrect memory stage visibility");
        if (UnityEngine.Object.FindObjectsByType<ReadabilityGradient>().Length != 2) throw new Exception("Both dim layers must exist");
        return "PASS: a fresh input enters memory; book and camper hidden; call/dialogue dim layers present.";
    }
    public static async Task<string> FinishMemoryAndCheckPresent()
    {
        string[] lines = { "몇 걸음 앞서던 발소리가 멎었다.", "잠깐만. 신발에 돌 들어갔어.", "소이 손 좀 잡아줘.", "응.", "됐어. 다시 엄마 손 잡을래?", "아니. 아빠 손 잡을래.", "그래. 그럼 가자." };
        foreach (string line in lines)
        {
            await Wait(() => Text("Body").text == line);
            if (line == lines[0]) CheckNameplate("", false);
            else if (line == "응.") CheckNameplate("수혁", false);
            else if (line == "아니. 아빠 손 잡을래.") CheckNameplate("소이", true);
            else CheckNameplate("?", true);
            var writer = Text("Body").GetComponent<Typewriter>();
            if (writer.IsTyping) { writer.Complete(); await Wait(() => !writer.IsTyping); }
            await Wait(() => UnityEngine.Object.FindAnyObjectByType<Live49.Dialogue.DialogueView>().Cue.alpha > .99f);
            FreshInput.SimulateAdvance();
            await Task.Delay(100);
        }
        await Wait(() => Text("CallText").text == "아빠?");
        await Task.Delay(900);
        if (Group("C0_Present").alpha != 0) throw new Exception("Present appeared before return-call input");
        FreshInput.SimulateAdvance();
        await Wait(() => Text("Body").text == "응. 잠깐 옛날 생각했어.");
        await Task.Delay(1000);
        if (Group("C0_Present").alpha != 1 || Group("C0_PhotoWall").alpha != 0) throw new Exception("Present handoff failed");
        CheckNameplate("수혁", false);
        return "PASS: seven memory lines; narration hidden, unknown speaker right, Suhyeok left, revealed Soi right; photo return and present handoff.";
    }
    public static async Task<string> CheckPresentSoi()
    {
        await Wait(() => !Text("Body").GetComponent<Typewriter>().IsTyping);
        await Wait(() => UnityEngine.Object.FindAnyObjectByType<Live49.Dialogue.DialogueView>().Cue.alpha > .99f);
        FreshInput.SimulateAdvance();
        await Wait(() => Text("Body").text == "서연이 없는 밤은 아직도 낯설다.");
        CheckNameplate("", false);
        Text("Body").GetComponent<Typewriter>().Complete();
        await Wait(() => !Text("Body").GetComponent<Typewriter>().IsTyping);
        await Wait(() => UnityEngine.Object.FindAnyObjectByType<Live49.Dialogue.DialogueView>().Cue.alpha > .99f);
        FreshInput.SimulateAdvance();
        await Wait(() => Text("Body").text == "내일도 여기 있어?");
        await Wait(() => !Text("Body").GetComponent<Typewriter>().IsTyping);
        CheckNameplate("소이", true);
        return "PASS: present narration hides the nameplate; Soi's next line restores her name on the right.";
    }
    public static async Task<string> CheckAdvanceCue()
    {
        var view = UnityEngine.Object.FindAnyObjectByType<Live49.Dialogue.DialogueView>();
        await Wait(() => !view.Body.IsTyping && view.Cue.alpha > .95f);
        var label = view.Cue.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "ContinueLabel");
        if (label.text != "계속") throw new Exception("Missing continue label");
        if (view.Cue.GetComponent<UnityEngine.UI.Image>().enabled) throw new Exception("Old icon overlaps new cue");
        string previous = Text("Body").text;
        FreshInput.SimulateAdvance();
        await Wait(() => Text("Body").text != previous);
        string next = Text("Body").text;
        bool observedTyping = view.Body.IsTyping;
        if (observedTyping)
        {
            if (view.Cue.alpha != 0) throw new Exception("Cue visible while typing");
            FreshInput.SimulateAdvance();
            await Wait(() => !view.Body.IsTyping);
            if (Text("Body").text != next) throw new Exception("Typing click advanced instead of completing");
        }
        await Wait(() => view.Cue.alpha > .95f);
        await Task.Delay(300);
        if (Text("Body").text != next) throw new Exception("Line auto-advanced");
        return "PASS: continue label, old icon removed, ready-only visibility, fresh-input progression; typing skip observed=" + observedTyping;
    }
}
