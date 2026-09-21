using System;
using System.Collections;
using System.Linq;
using Live49.Core;
using Live49.UI;
using TMPro;
using UnityEngine;

namespace Live49.Dialogue
{
    // Shared bottom dialogue window with left/right portraits (design/ui/ch00-01/memory-dialogue.json layout).
    public class DialogueView : MonoBehaviour
    {
        [Serializable]
        public class Portrait
        {
            public string speakerId;
            public CanvasGroup group;
            [NonSerialized] public bool sourceFacesLeft;
        }

        [SerializeField] CanvasGroup panel;
        [SerializeField] GameObject nameplate;
        [SerializeField] TMP_Text nameLabel;
        [Header("화자 이름표 위치 · 표시 이름과 별개인 내부 ID")]
        [SerializeField] string[] rightSpeakerIds = { "soi", "seoyeon" };
        [SerializeField] float nameplateEdgeInset = 110f;
        [SerializeField] Typewriter body;
        [SerializeField] CanvasGroup cue;
        [SerializeField] Portrait[] portraits;
        CanvasGroup _bodyGroup;
        CanvasGroup _nameGroup;
        string _speakerId;

        public Typewriter Body => body;
        public CanvasGroup Cue => cue;
        public string SpeakerLabel=>nameLabel.text;
        public void AddPortrait(string id,Sprite sprite,bool sourceFacesLeft=false)
        {
            if(portraits.Any(p=>p.speakerId==id)||sprite==null)return;
            // Sejin's source contains a smaller face relative to the canvas than Suhyeok's.
            // Match the visible face and eye line instead of giving both PNGs the same rectangle.
            var rt=id=="sejin"?
                PanelUI.Rect(portraits[0].group.transform.parent,"Portrait_"+id,1005,125,910,1213.333f):
                PanelUI.Rect(portraits[0].group.transform.parent,"Portrait_"+id,1100,75,700,1050);
            var image=rt.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            var group=rt.gameObject.AddComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;
            var portrait=new Portrait{speakerId=id,group=group,sourceFacesLeft=sourceFacesLeft};
            portraits=portraits.Concat(new[]{portrait}).ToArray();
            rightSpeakerIds=rightSpeakerIds.Concat(new[]{id}).ToArray();
            FacePartner(portrait);
        }

        void Awake()
        {
            foreach(var portrait in portraits)
            {
                // The approved Soi/Seoyeon sources already face left. Do not blindly mirror every right slot.
                portrait.sourceFacesLeft=portrait.speakerId=="soi"||portrait.speakerId=="seoyeon";
                FacePartner(portrait);
            }
            ReadabilityGradient.AddTo(panel.transform);
            DialogueAdvanceCue.Configure(cue, nameLabel.font);
            _bodyGroup = body.GetComponent<CanvasGroup>();
            if (_bodyGroup == null) _bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
            _nameGroup = nameplate.GetComponent<CanvasGroup>();
            if (_nameGroup == null) _nameGroup = nameplate.AddComponent<CanvasGroup>();
        }
        void FacePartner(Portrait portrait)
        {
            var rt=(RectTransform)portrait.group.transform;
            bool right=rightSpeakerIds!=null&&Array.IndexOf(rightSpeakerIds,portrait.speakerId)>=0;
            // Mirror around the image centre without moving its screen-space rectangle or its nameplate.
            var position=rt.anchoredPosition;position.x+=(.5f-rt.pivot.x)*rt.rect.width;
            rt.pivot=new Vector2(.5f,rt.pivot.y);rt.anchoredPosition=position;
            var scale=rt.localScale;scale.x=Mathf.Abs(scale.x)*(right!=portrait.sourceFacesLeft?-1:1);rt.localScale=scale;
        }

        public void HideImmediate()
        {
            panel.alpha = 0f;
            cue.alpha = 0f;
            body.Clear();
            nameplate.SetActive(false);
            nameLabel.text = string.Empty;
            _speakerId = null;
            foreach (var p in portraits) p.group.alpha = 0f;
        }

        public void SetSpeaker(string speakerId, string label)
        {
            _speakerId = speakerId;
            nameplate.SetActive(!string.IsNullOrEmpty(label));
            nameLabel.text = label ?? string.Empty;
            bool right = rightSpeakerIds != null && Array.IndexOf(rightSpeakerIds, speakerId) >= 0;
            var rt = (RectTransform)nameplate.transform;
            float side = right ? 1f : 0f;
            rt.anchorMin = new Vector2(side, rt.anchorMin.y);
            rt.anchorMax = new Vector2(side, rt.anchorMax.y);
            rt.pivot = new Vector2(side, rt.pivot.y);
            rt.anchoredPosition = new Vector2(right ? -nameplateEdgeInset : nameplateEdgeInset, rt.anchoredPosition.y);
        }

        public IEnumerator PrepareLine(string speakerId, string label, float panelFade, float textFade)
        {
            bool wasVisible = panel.alpha > .001f;
            bool changed = _speakerId != speakerId || nameLabel.text != (label ?? string.Empty);
            cue.alpha = 0f;
            if (wasVisible)
            {
                float bodyFrom = _bodyGroup.alpha;
                float nameFrom = _nameGroup.alpha;
                yield return Tween.Run(textFade, k =>
                {
                    _bodyGroup.alpha = bodyFrom * (1f - k);
                    if (changed) _nameGroup.alpha = nameFrom * (1f - k);
                });
            }
            body.Clear();
            // Switch the label and side while hidden; don't expose the previous speaker during panel entry.
            if (changed || !wasVisible) _nameGroup.alpha = 0f;
            SetSpeaker(speakerId, label);
            if (!wasVisible)
            {
                _nameGroup.alpha = 1f;
                yield return FadePanel(true, panelFade);
            }
            else if (changed) yield return Tween.Fade(_nameGroup, 1f, textFade);
            _bodyGroup.alpha = 1f;
        }

        public IEnumerator FadePanel(bool show, float duration)
        {
            yield return Tween.Fade(panel, show ? 1f : 0f, duration);
            if (!show)
            {
                body.Clear();
                cue.alpha = 0f;
                nameplate.SetActive(false);
                nameLabel.text = string.Empty;
                _speakerId = null;
            }
        }

        public IEnumerator SetPortraits(string[] visible, string[] suppressed, float duration)
        {
            var from = portraits.Select(p => p.group.alpha).ToArray();
            var to = portraits.Select(p => IsShown(p.speakerId, visible, suppressed) ? 1f : 0f).ToArray();
            yield return Tween.Run(duration, k =>
            {
                for (int i = 0; i < portraits.Length; i++)
                    portraits[i].group.alpha = Mathf.Lerp(from[i], to[i], k);
            });
        }

        static bool IsShown(string speakerId, string[] visible, string[] suppressed)
        {
            bool listed = visible != null && Array.IndexOf(visible, speakerId) >= 0;
            bool hidden = suppressed != null && Array.IndexOf(suppressed, speakerId) >= 0;
            return listed && !hidden;
        }
    }
}
