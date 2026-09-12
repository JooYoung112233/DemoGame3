using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Dialogue;
using Live49.Title;
using Live49.UI;
using TMPro;
using UnityEngine;

// Run from a fresh Title play session. Drives real dialogue input without shortening typewriter or scene timings.
public static class AuditOpeningPacing
{
    static DialogueView View => UnityEngine.Object.FindAnyObjectByType<DialogueView>();
    static TMP_Text Text(string name) => UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).Single(t => t.name == name);
    static CanvasGroup Group(string name) => UnityEngine.Object.FindObjectsByType<CanvasGroup>().Single(t => t.name == name);
    static string _folder;
    static readonly List<string> Report = new List<string>();

    static async Task Wait(Func<bool> ready, int timeout = 20000, bool stablePanel = false)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeout);
        while (!ready())
        {
            if (stablePanel && View.Cue.transform.parent.GetComponent<CanvasGroup>().alpha < .999f)
                throw new Exception("Dialogue panel blinked during a same-shot line transition.");
            if (DateTime.UtcNow >= end) throw new Exception("Timed out waiting for dialogue/shot.");
            await Task.Delay(16);
        }
    }

    static async Task Shot(string file)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(_folder, file));
        await Task.Delay(350);
    }

    static void Name(string expected, bool right)
    {
        var label = Text("NameLabel");
        var rt = (RectTransform)label.transform.parent;
        if (rt.gameObject.activeSelf != (expected.Length > 0)) throw new Exception("Narration nameplate mismatch.");
        if (expected.Length == 0) return;
        if (label.text != expected || rt.anchorMin.x != (right ? 1 : 0)) throw new Exception("Incorrect speaker or side: " + label.text);
    }

    static async Task ReadyLine(string line, string name, bool right, bool terminal = false)
    {
        await Wait(() => Text("Body").text == line);
        await Wait(() => !View.Body.IsTyping);
        Name(name, right);
        await Wait(() => View.Cue.alpha > .99f);
        Report.Add("Read: " + line + "; speaker=" + name + "; side=" + (right ? "right" : "left"));
    }

    static async Task AdvanceTo(string next, bool sameShot)
    {
        FreshInput.SimulateAdvance();
        await Wait(() => Text("Body").text == next, stablePanel: sameShot);
    }

    public static async Task<string> Run()
    {
        _folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots/PacingAudit"));
        Directory.CreateDirectory(_folder);
        var started = DateTime.UtcNow;
        var start = UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i => i.Id == "start");
        start.Owner.Confirm(start);
        await Wait(() => UnityEngine.Object.FindObjectsByType<TMP_Text>().Any(t => t.name == "CallText" && t.text == "수혁아."));
        var monitor = new GameObject("OpeningPacingAuditMonitor").AddComponent<OpeningPacingAuditMonitor>();
        try
        {
            var call = Text("CallText").GetComponent<Typewriter>();
            await Wait(() => !call.IsTyping);
            // A repeated click immediately after text completion must not leave the book shot.
            FreshInput.SimulateAdvance();
            await Task.Delay(750);
            if (Group("C0_BookIntro").alpha != 1) throw new Exception("Rapid completion input skipped the book.");
            var callCue = Group("CallOverlay").GetComponentsInChildren<DialogueAdvanceCue>().Single();
            if (callCue.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("클릭") || t.text.Contains("Space"))) throw new Exception("Redundant input hint remains.");
            await Shot("01-book.png");
            FreshInput.SimulateAdvance();
            await ReadyLine("몇 걸음 앞서던 발소리가 멎었다.", "", false);
            await Shot("02-narration.png");
            await AdvanceTo("잠깐만. 신발에 돌 들어갔어.", true);
            await ReadyLine("잠깐만. 신발에 돌 들어갔어.", "?", true);
            await Shot("03-dialogue.png");
            await AdvanceTo("소이 손 좀 잡아줘.", true);
            await ReadyLine("소이 손 좀 잡아줘.", "?", true);
            await AdvanceTo("응.", true);
            await ReadyLine("응.", "수혁", false);
            monitor.ExpectedReopenName = "?";
            await AdvanceTo("됐어. 다시 엄마 손 잡을래?", false);
            monitor.ExpectedReopenName = null;
            await ReadyLine("됐어. 다시 엄마 손 잡을래?", "?", true);
            await Shot("04-handhold.png");
            await AdvanceTo("아니. 아빠 손 잡을래.", true);
            await ReadyLine("아니. 아빠 손 잡을래.", "소이", true);
            await AdvanceTo("그래. 그럼 가자.", true);
            await ReadyLine("그래. 그럼 가자.", "?", true);
            FreshInput.SimulateAdvance();
            await Wait(() => Text("CallText").text == "아빠?" && !call.IsTyping);
            await Task.Delay(750);
            if (Group("C0_Present").alpha != 0) throw new Exception("Present auto-advanced.");
            await Shot("05-photo.png");
            FreshInput.SimulateAdvance();
            await ReadyLine("응. 잠깐 옛날 생각했어.", "수혁", false);
            await Shot("06-present.png");
            monitor.CheckPresentBlur = true;
            await AdvanceTo("서연이 없는 밤은 아직도 낯설다.", true);
            await ReadyLine("서연이 없는 밤은 아직도 낯설다.", "", false);
            await AdvanceTo("내일도 여기 있어?", true);
            await ReadyLine("내일도 여기 있어?", "소이", true, true);
            await Shot("07-soi.png");
            if (monitor.Errors.Count > 0) throw new Exception(string.Join("\n", monitor.Errors.Distinct()));
            if (monitor.HandSamples < 5 || monitor.PresentSamples < 5) throw new Exception("Insufficient crossfade samples.");
            Report.Add("PASS: continuous panel between lines; no stale name on handhold reopen; stable present blur; full-opacity outgoing shot during both crossfades.");
            Report.Add("Crossfade frames sampled: hands=" + monitor.HandSamples + ", present=" + monitor.PresentSamples);
            Report.Add("Elapsed (normal typing, no typewriter skip): " + (DateTime.UtcNow - started).TotalSeconds.ToString("F1") + "s");
            File.WriteAllLines(Path.Combine(_folder, "report.txt"), Report);
            return string.Join("\n", Report);
        }
        finally { UnityEngine.Object.Destroy(monitor.gameObject); }
    }
}

public class OpeningPacingAuditMonitor : MonoBehaviour
{
    public readonly List<string> Errors = new List<string>();
    public int HandSamples, PresentSamples;
    public bool CheckPresentBlur;
    public string ExpectedReopenName;
    CanvasGroup _hands, _feet, _present, _photo, _panel;
    TMP_Text _name, _body;
    StageImageFX[] _presentImages;
    bool _panelWasHidden;
    void Awake()
    {
        var groups = UnityEngine.Object.FindObjectsByType<CanvasGroup>();
        _hands = groups.Single(g => g.name == "hands-held-v2");
        _feet = groups.Single(g => g.name == "feet-paused-v1");
        _present = groups.Single(g => g.name == "C0_Present");
        _photo = groups.Single(g => g.name == "C0_PhotoWall");
        _panel = UnityEngine.Object.FindAnyObjectByType<DialogueView>().Cue.transform.parent.GetComponent<CanvasGroup>();
        var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        _name = texts.Single(t => t.name == "NameLabel"); _body = texts.Single(t => t.name == "Body");
        _presentImages = _present.GetComponentsInChildren<StageImageFX>();
        Application.logMessageReceived += Log;
    }
    void Log(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) Errors.Add(text); }
    void OnDestroy() => Application.logMessageReceived -= Log;
    void Update()
    {
        if (_hands == null) return;
        if (_hands.alpha > .01f && _hands.alpha < .99f)
        {
            HandSamples++;
            if (_feet.alpha < .999f) Errors.Add("Hand crossfade brightness dip risk.");
        }
        if (_present.alpha > .01f && _present.alpha < .99f)
        {
            PresentSamples++;
            if (_photo.alpha < .999f) Errors.Add("Present crossfade brightness dip risk.");
        }
        if (ExpectedReopenName == null) _panelWasHidden = false;
        else
        {
            if (_panel.alpha < .001f) _panelWasHidden = true;
            if (_panelWasHidden && _panel.alpha > .01f && _name.text != ExpectedReopenName) Errors.Add("Stale name flashes on panel reopen.");
        }
        if (CheckPresentBlur && _presentImages.Any(i => Mathf.Abs(i.BlurLogicalPx - 3.5f) > .01f)) Errors.Add("Present blur pumps during narration.");
    }
}
