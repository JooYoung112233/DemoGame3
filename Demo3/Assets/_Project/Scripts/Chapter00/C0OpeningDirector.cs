using System.Collections;
using System.Linq;
using Live49.Core;
using Live49.Dialogue;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.Chapter00
{
    // C0 opening from the title handoff to the present-day question:
    // book → "수혁아." → memory path/handhold → wall photo (face lost, glitch) → "아빠?" → present camper.
    public partial class C0OpeningDirector : MonoBehaviour
    {
        static readonly Rect PolaroidSmall = new Rect(1442f, 269f, 44f, 28.34f);
        static readonly Rect PolaroidDetail = new Rect(300f, 70f, 1320f, 850.16f);
        static readonly int GlitchId = Shader.PropertyToID("_L49Glitch");
        static readonly int GlitchSeedId = Shader.PropertyToID("_L49GlitchSeed");

        [SerializeField] TextAsset dialogueJson;
        [SerializeField] C0OpeningTimings timings = new C0OpeningTimings();
        [SerializeField] Sprite openingBook;
        CanvasGroup _bookShot;

        [Header("Screen")]
        [SerializeField] CanvasGroup gameRoot;
        [SerializeField] CanvasGroup screenFade;

        [Header("P02 · P08 벽 사진")]
        [SerializeField] CanvasGroup photoWall;
        [SerializeField] StageImageFX camperWall;
        [SerializeField] CanvasGroup wallDim;
        [SerializeField] RectTransform polaroid;
        [SerializeField] StageImageFX photo;

        [Header("P03~P07 회상")]
        [SerializeField] CanvasGroup memoryPath;
        [SerializeField] CanvasGroup feet;
        [SerializeField] CanvasGroup hands;

        [Header("P10 현재")]
        [SerializeField] CanvasGroup present;

        [Header("부름 · 대화")]
        [SerializeField] CanvasGroup callOverlay;
        [SerializeField] CanvasGroup bottomDim;
        [SerializeField] Typewriter callText;
        [SerializeField] CanvasGroup callCue;
        [SerializeField] CanvasGroup firstInputHint;
        [SerializeField] TMP_Text firstInputHintLabel;
        [SerializeField] DialogueView dialogue;

        DialogueScript _script;
        SpeakerIdentity _identity;
        StageImageFX[] _memoryLayers;
        StageImageFX[] _presentLayers;
        Coroutine _portraitRoutine;
        Coroutine _blurRoutine;
        CamperInteractions _interactions;
        bool _answeredSoi, _bookSeen, _interactionBusy;
        public bool HasAnsweredSoi => _answeredSoi;
        public bool HasSeenBook => _bookSeen;

        IEnumerator Start()
        {
            GameHud.Ensure(dialogue.Body.GetComponent<TMP_Text>().font).SetNarrativeMode();
            _script = DialogueScript.FromJson(dialogueJson);
            _identity = new SpeakerIdentity(_script);
            _memoryLayers = memoryPath.GetComponentsInChildren<StageImageFX>(true);
            _presentLayers = present.GetComponentsInChildren<StageImageFX>(true);
            firstInputHintLabel.text = _script.firstInputHint;
            BuildBookShot();
            ReadabilityGradient.AddTo(bottomDim.transform);
            DialogueAdvanceCue.Configure(callCue, callText.GetComponent<TMP_Text>().font);
            ResetStage();
            bool restoring = SaveSystem.Pending != null;
            _state = SaveSystem.TakePending() ?? new JourneyState();
            SaveSystem.Current = _state;
            _graph = ContinuationGraph.Load();

            bool fromTitle = SceneFlow.OpeningHandoffPending;
            if (fromTitle)
            {
                gameRoot.alpha = 0f;
                while (!SceneFlow.TitleReadyForHandoff) yield return null;
            }
            else
            {
                screenFade.alpha = 1f; // opened directly for testing: rise from black instead of the title dissolve
            }

            if (restoring)
            {
                if (fromTitle) SceneFlow.FinishTitleHandoff();
                yield return RestoreJourney();
                yield break;
            }
            yield return BookEntry(fromTitle);
            yield return Memory();
            yield return PhotoReturn();
            yield return Present();
            yield return EnterExploration();
        }

        void OnDestroy()
        {
            SetGlitch(0f);
            if (_interactions != null) Destroy(_interactions.gameObject);
            if (_journeyActions != null) Destroy(_journeyActions.gameObject);
            if (_banditPanel != null) Destroy(_banditPanel.gameObject);
        }

        void ResetStage()
        {
            gameRoot.alpha = 1f;
            screenFade.alpha = 0f;
            photoWall.alpha = 0f;
            _bookShot.alpha = 1f;
            memoryPath.alpha = 0f;
            present.alpha = 0f;
            feet.alpha = 1f;
            hands.alpha = 0f;
            wallDim.alpha = 0f;
            callOverlay.alpha = 1f;
            bottomDim.alpha = 0f;
            callCue.alpha = 0f;
            firstInputHint.alpha = 0f;
            callText.Clear();
            dialogue.HideImmediate();
            camperWall.SetBlur(0f);
            photo.SetMask(false);
            photo.SetBlur(timings.photoBlurPx);
            SetPolaroid(PolaroidSmall);
            SetGlitch(0f);
        }

        void BuildBookShot()
        {
            var go = new GameObject("C0_BookIntro", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(photoWall.transform.parent, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.66f, .48f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = new Vector3(1.035f, 1.035f, 1f);
            var image = go.GetComponent<Image>();
            image.sprite = openingBook;
            image.preserveAspect = true;
            image.raycastTarget = false;
            _bookShot = go.GetComponent<CanvasGroup>();
        }

        // Stay on the title's book shot for the first voice. The camper is revealed only after memory.
        IEnumerator BookEntry(bool fromTitle)
        {
            gameRoot.alpha = 1f;
            if (fromTitle) SceneFlow.FinishTitleHandoff();
            else yield return Tween.Fade(screenFade, 0f, .6f);
            screenFade.alpha = 0f;
            yield return Tween.Wait(timings.bookHold);
            yield return Tween.Fade(bottomDim, 1f, timings.dimDuration);
            yield return TypeCall(_script.entryCall, firstInputHint);
        }

        // P03 → P07: memory path narration and dialogue, then the first handhold entrance.
        IEnumerator Memory()
        {
            var T = timings;
            yield return Tween.Run(T.toBlack, k => screenFade.alpha = k);
            _bookShot.alpha = 0f;
            photoWall.alpha = 0f;
            bottomDim.alpha = 0f;
            callText.Clear();
            memoryPath.alpha = 1f;
            yield return Tween.Wait(T.blackHold);
            yield return Tween.Run(T.fromBlack, k => screenFade.alpha = 1f - k);

            yield return Tween.Wait(T.arrivalHold);
            yield return PlayLines("path", _memoryLayers);

            // The panel and portraits step aside for an unobstructed look at the held hands.
            Restart(ref _portraitRoutine, dialogue.SetPortraits(null, _script.suppressedPortraits, T.portraitFade));
            Restart(ref _blurRoutine, BlurStage(_memoryLayers, 0f, T.handoffFade));
            yield return dialogue.FadePanel(false, T.panelFade);
            yield return Tween.Run(T.handoffFade, k =>
            {
                hands.alpha = k;
                feet.alpha = 1f; // Opaque outgoing shot under the incoming shot prevents a brightness dip.
            });
            feet.alpha = 0f;
            yield return Tween.Wait(T.handholdPause);

            yield return PlayLines("handhold", _memoryLayers);
        }

        // P08 → P09: memory resolves into the wall photo, the mother's face lost, then "아빠?".
        IEnumerator PhotoReturn()
        {
            var T = timings;
            yield return Tween.Run(T.memoryOutBlack, k => screenFade.alpha = k);

            StopRoutine(ref _portraitRoutine);
            StopRoutine(ref _blurRoutine);
            memoryPath.alpha = 0f;
            dialogue.HideImmediate();
            photoWall.alpha = 1f;
            SetPolaroid(PolaroidDetail);
            camperWall.SetBlur(T.wallBlurPx);
            wallDim.alpha = T.wallDimAlpha;
            photo.SetMask(true); // on before the first visible frame so the full face never flashes
            photo.SetBlur(T.photoBlurPx);

            bool seeded = false;
            float t = 0f;
            while (t < T.returnCallStart)
            {
                t += SeqTime.Delta;
                screenFade.alpha = 1f - Tween.Smooth(Tween.Seg(t, 0f, T.returnAppear));
                photo.SetBlur(T.photoBlurPx * (1f - Tween.Smooth(Tween.Seg(t, T.focusStart, T.focusEnd - T.focusStart))));

                bool glitching = t >= T.glitchStart && t < T.glitchEnd;
                if (glitching && !seeded)
                {
                    Shader.SetGlobalFloat(GlitchSeedId, Random.value * 100f);
                    seeded = true;
                }
                SetGlitch(glitching ? 1f : 0f);

                bottomDim.alpha = Tween.Smooth(Tween.Seg(t, T.returnDimStart, T.dimDuration));
                yield return null;
            }

            SetGlitch(0f);
            screenFade.alpha = 0f;
            photo.SetBlur(0f);
            yield return TypeCall(_script.returnCall, null);
        }

        // P10 → C0-03: the photo fades back into the present camper and the current dialogue runs.
        IEnumerator Present()
        {
            var T = timings;
            yield return Tween.Run(T.presentDissolve, k =>
            {
                photoWall.alpha = 1f; // Keep coverage beneath the incoming camper throughout the dissolve.
                callOverlay.alpha = 1f - k;
                present.alpha = k;
            });
            photoWall.alpha = 0f;
            callText.Clear();
            bottomDim.alpha = 0f;
            callOverlay.alpha = 1f;
            photo.SetMask(false);
            yield return Tween.Wait(T.presentHold);

            yield return PlayLines("present", _presentLayers);
        }

        IEnumerator PlayLines(string background, StageImageFX[] stage)
        {
            foreach (var line in _script.lines.Where(l => l.background == background))
                yield return PlayLine(line, stage);
        }

        IEnumerator PlayLine(DialogueLine line, StageImageFX[] stage)
        {
            var T = timings;
            dialogue.Cue.alpha = 0f;
            Restart(ref _portraitRoutine, dialogue.SetPortraits(line.portraits, _script.suppressedPortraits, T.portraitFade));
            // Keep background focus stable between narration and speech within the same shot.
            Restart(ref _blurRoutine, BlurStage(stage, T.dialogueBlurPx, T.portraitFade));
            yield return dialogue.PrepareLine(line.speakerId, line.IsNarration ? string.Empty : line.speakerId == "radio" ? "라디오" : line.speakerId=="sejin"?(_state.Has("name.sejin")?"세진":"?"):_identity.LabelFor(line.speakerId), T.panelFade, T.lineTextFade);
            FreshInput.DiscardPending();

            yield return TypeWithSkip(dialogue.Body, line.text, T.secondsPerChar);
            yield return WaitAdvance(dialogue.Cue, null);
            _identity.OnLineFinished(line.id);
        }

        IEnumerator EnterExploration(bool save=true)
        {
            StopRoutine(ref _portraitRoutine); StopRoutine(ref _blurRoutine);
            _portraitRoutine = StartCoroutine(dialogue.SetPortraits(null, _script.suppressedPortraits, timings.portraitFade));
            _blurRoutine = StartCoroutine(BlurStage(_presentLayers, 0, .65f));
            if (_journeyFX != null) _journeyFX.SetBlur(0);
            yield return dialogue.FadePanel(false, timings.panelFade);
            yield return Tween.Wait(.22f);
            var hud = GameHud.Instance;
            if (_interactions == null)
            {
                _interactions = CamperInteractions.Create(hud, dialogue.Body.GetComponent<TMP_Text>().font, Interact);
            }
            _state.answeredSoi = _answeredSoi; _state.bookSeen = _bookSeen;
            bool encounterStarted=BanditEncounter.Begin(_state)||BanditEncounter.BeginReturn(_state);
            if(BanditEncounter.Active(_state))
            {yield return ShowBanditEncounter(encounterStarted);yield break;}
            RefreshJourneyHud();
            FreshInput.DiscardPending();
            _interactionBusy = false;
            if(save)SaveJourney();
        }
        void Interact(string target)
        {
            var hud = GameHud.Instance;
            if (_interactionBusy || hud == null || !hud.IsExploring || hud.IsPaused) return;
            switch (target)
            {
                case "soi":
                    if (!_answeredSoi) hud.ShowInteraction("소이에게 대답하기", "“내일도 여기 있어?”", new[] { "다른 데 가 보고 싶어?", "어디로 가고 싶은데?" },
                        choice => StartCoroutine(ExploreConversation(choice == 0 ? "reply_a" : "reply_b")));
                    else hud.ShowInteraction("소이", "식탁에 펼쳐 둔 스케치북을 함께 살펴볼 수 있어요.");
                    break;
                case "kitchen":
                    hud.OpenActivity(ActivityPanel.Kind.Kitchen);
                    break;
                case "bed":
                    hud.RequestDayEnd();
                    break;
                case "book":
                    if (!_answeredSoi) hud.ShowInteraction("스케치북", "소이가 대답을 기다리고 있어요.\n먼저 소이와 이야기를 나눠주세요.");
                    else if (_bookSeen) hud.ShowInteraction("스케치북", "모서리는 닳았지만,\n아직 그릴 수 있는 빈 페이지가 남아 있어요.");
                    else StartCoroutine(ExploreConversation("book"));
                    break;
            }
        }
        IEnumerator ExploreConversation(string section)
        {
            if (_interactionBusy) yield break;
            _interactionBusy = true;
            GameHud.Instance.SetNarrativeMode();
            FreshInput.DiscardPending();
            foreach (var line in _script.continuation.Where(line => line.background == section))
                yield return PlayLine(line, _presentLayers);
            if (section == "book") _bookSeen = true; else _answeredSoi = true;
            yield return EnterExploration();
            if (section == "book") yield return ContinueStory("camp_radio");
        }

        IEnumerator TypeCall(string line, CanvasGroup hint)
        {
            callCue.alpha = 0f;
            if (hint != null) hint.alpha = 0f;
            yield return TypeWithSkip(callText, line, timings.secondsPerCallChar);
            yield return WaitAdvance(callCue, hint);
        }

        // Input while typing only completes the line; the next fresh input advances.
        IEnumerator TypeWithSkip(Typewriter writer, string line, float secondsPerChar)
        {
            StartCoroutine(writer.Play(line, secondsPerChar));
            while (writer.IsTyping)
            {
                if (FreshInput.TryAdvance()) writer.Complete();
                yield return null;
            }
        }

        IEnumerator WaitAdvance(CanvasGroup cue, CanvasGroup hint)
        {
            // Discard rapid repeat input while the finished line settles; never queue it for the next beat.
            float settle = 0f;
            while (settle < timings.readSettle)
            {
                FreshInput.DiscardPending();
                settle += SeqTime.Delta;
                yield return null;
            }
            float t = 0f;
            while (true)
            {
                t += SeqTime.Delta;
                float a = Tween.Smooth(Tween.Seg(t, 0f, timings.cueFade));
                cue.alpha = a;
                if (FreshInput.TryAdvance()) break;
                yield return null;
            }
            yield return Tween.Fade(cue, 0f, timings.cueFade);
            yield return Tween.Wait(timings.advanceHold);
            FreshInput.DiscardPending();
            if (hint != null) hint.alpha = 0f;
        }

        IEnumerator BlurStage(StageImageFX[] layers, float to, float duration)
        {
            var from = layers.Select(layer => layer.BlurLogicalPx).ToArray();
            yield return Tween.Run(duration, k =>
            {
                for (int i = 0; i < layers.Length; i++) layers[i].SetBlur(Mathf.Lerp(from[i], to, k));
            });
        }


        void Restart(ref Coroutine handle, IEnumerator routine)
        {
            StopRoutine(ref handle);
            handle = StartCoroutine(routine);
        }

        void StopRoutine(ref Coroutine handle)
        {
            if (handle != null) StopCoroutine(handle);
            handle = null;
        }

        void SetPolaroid(Rect r)
        {
            polaroid.anchoredPosition = new Vector2(r.x, -r.y);
            polaroid.sizeDelta = new Vector2(r.width, r.height);
        }

        static Rect LerpRect(Rect a, Rect b, float k) => new Rect(
            Mathf.Lerp(a.x, b.x, k), Mathf.Lerp(a.y, b.y, k),
            Mathf.Lerp(a.width, b.width, k), Mathf.Lerp(a.height, b.height, k));

        static void SetGlitch(float amount) => Shader.SetGlobalFloat(GlitchId, amount);
    }
}
