using System;
using System.Collections.Generic;
using UnityEngine;

namespace Live49.Dialogue
{
    [Serializable]
    public class DialogueLine
    {
        public string id;
        public string kind;
        public string speakerId;
        public string text;
        public string[] portraits;
        public string background;
        public bool endOfSequence;

        public bool IsNarration => kind == "narration";
    }

    [Serializable]
    public class SpeakerName
    {
        public string speakerId;
        public string displayName;
    }

    [Serializable]
    public class NameReveal
    {
        public string afterLine;
        public string speakerId;
    }

    [Serializable]
    public class DialogueScript
    {
        public string unknownLabel = "?";
        public string entryCall;
        public string returnCall;
        public string firstInputHint;
        public SpeakerName[] speakers;
        public string[] knownOnEntry;
        public string[] suppressedPortraits;
        public NameReveal[] nameReveals;
        public DialogueLine[] lines;
        public DialogueLine[] continuation;

        public static DialogueScript FromJson(TextAsset asset) => JsonUtility.FromJson<DialogueScript>(asset.text);

        public string DisplayName(string speakerId)
        {
            foreach (var s in speakers)
                if (s.speakerId == speakerId) return s.displayName;
            return speakerId;
        }
    }

    // Speakers show "?" until a line links their name to them (docs/06-대사/SPEAKER-IDENTITY-RULES.ko.md).
    public class SpeakerIdentity
    {
        readonly DialogueScript _script;
        readonly HashSet<string> _known;

        public SpeakerIdentity(DialogueScript script)
        {
            _script = script;
            _known = new HashSet<string>(script.knownOnEntry ?? Array.Empty<string>());
        }

        public string LabelFor(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return string.Empty;
            return _known.Contains(speakerId) ? _script.DisplayName(speakerId) : _script.unknownLabel;
        }

        public void OnLineFinished(string lineId)
        {
            if (_script.nameReveals == null) return;
            foreach (var reveal in _script.nameReveals)
                if (reveal.afterLine == lineId) _known.Add(reveal.speakerId);
        }
    }
}
