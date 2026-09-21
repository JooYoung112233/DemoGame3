using System.Collections;
using Live49.Core;
using TMPro;
using UnityEngine;

namespace Live49.UI
{
    // Lays out the whole line first and reveals characters in place, so centred lines never shift while typing.
    [RequireComponent(typeof(TMP_Text))]
    public class Typewriter : MonoBehaviour
    {
        TMP_Text _text;
        bool _skip;

        public bool IsTyping { get; private set; }

        TMP_Text Text => _text != null ? _text : _text = GetComponent<TMP_Text>();

        public void Clear()
        {
            Text.text = string.Empty;
            Text.maxVisibleCharacters = 0;
            IsTyping = false;
        }

        public IEnumerator Play(string line, float secondsPerChar, bool firstImmediate = true)
        {
            Text.text = line;
            Text.maxVisibleCharacters = 0;
            Text.ForceMeshUpdate();

            int total = Text.textInfo.characterCount;
            if (GamePreferences.InstantText)
            {
                Text.maxVisibleCharacters = total;
                IsTyping = false;
                yield break;
            }
            secondsPerChar = Mathf.Max(.001f, secondsPerChar / GamePreferences.TextSpeed);
            int shown = firstImmediate ? Mathf.Min(1, total) : 0;
            float t = 0f;
            _skip = false;
            IsTyping = true;
            Text.maxVisibleCharacters = shown;

            while (shown < total && !_skip)
            {
                t += SeqTime.Delta;
                while (t >= secondsPerChar && shown < total)
                {
                    t -= secondsPerChar;
                    shown++;
                }
                Text.maxVisibleCharacters = shown;
                yield return null;
            }

            Text.maxVisibleCharacters = total;
            IsTyping = false;
        }

        public void Complete() => _skip = true;
    }
}
