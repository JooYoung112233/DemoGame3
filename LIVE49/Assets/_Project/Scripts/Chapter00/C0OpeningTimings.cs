using System;
using UnityEngine;

namespace Live49.Chapter00
{
    // Seconds from docs/03-콘티/챕터0/C0-PHOTO-MEMORY-IMPLEMENTATION.ko.md and C0-PRESENT-DIALOGUE.ko.md.
    // Only the title D00~D04 values are approved; the rest are draft values meant to be tuned on screen.
    [Serializable]
    public class C0OpeningTimings
    {
        [Header("P02 벽 사진 진입")]
        public float entryDissolve = 0.45f;
        public float zoomStart = 0.45f;
        public float zoomEnd = 1.70f;
        public float photoBlurPx = 13f;
        public float wallBlurPx = 4f;
        [Range(0f, 1f)] public float wallDimAlpha = 0.46f;
        public float dimStart = 2.00f;
        public float dimDuration = 0.55f;
        public float callStart = 2.40f;
        public float secondsPerCallChar = 0.11f;
        public float cueFade = 0.22f;

        [Header("읽기와 대사 사이 여백")]
        public float bookHold = 0.45f;
        public float readSettle = 0.35f;
        public float advanceHold = 0.18f;
        public float lineTextFade = 0.16f;

        [Header("P03 회상 전환")]
        public float toBlack = 0.70f;
        public float blackHold = 0.22f;
        public float fromBlack = 0.90f;

        [Header("P03~P07 회상 대화")]
        public float arrivalHold = 1.10f;
        public float panelFade = 0.45f;
        public float portraitFade = 0.60f;
        public float secondsPerChar = 0.075f;
        public float dialogueBlurPx = 3.5f;
        public float handoffFade = 1.0f;
        public float handholdPause = 1.60f;

        [Header("P08~P09 사진 복귀")]
        public float memoryOutBlack = 0.80f;
        public float returnAppear = 0.85f;
        public float focusStart = 1.05f;
        public float focusEnd = 2.05f;
        public float glitchStart = 2.30f;
        public float glitchEnd = 2.46f;
        public float returnDimStart = 3.15f;
        public float returnCallStart = 3.70f;

        [Header("P10 현재 복귀")]
        public float presentDissolve = 1.60f;
        public float presentHold = 1.40f;
    }
}
