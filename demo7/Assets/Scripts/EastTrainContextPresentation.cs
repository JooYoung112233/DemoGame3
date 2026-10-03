using UnityEngine;
using UnityEngine.InputSystem;

namespace EastTrain
{
    public sealed partial class EastTrainDemo
    {
        string lastContext = "";
        float contextTime;
        MeshRenderer contextRenderer;
        SpriteRenderer contextBacking;
        Transform timingTool;
        public bool ContextHintVisible => hintBoard != null && hintBoard.gameObject.activeSelf;

        void BuildContextPresentation()
        {
            // Names, status paragraphs, pickup letters and radio paper were prototype labels.
            // Keep data references alive, but only the temporary nearby hint can render text.
            foreach (var label in world.GetComponentsInChildren<TextMesh>(true))
                label.GetComponent<MeshRenderer>().enabled = false;
            contextRenderer = hintText.GetComponent<MeshRenderer>(); contextRenderer.enabled = true;
            contextBacking = hintBoard.GetComponentInChildren<SpriteRenderer>();
            HideGeometry("Receiver paper", "Control marker lead");
            driveBeacon.localPosition = new Vector3(6.85f, 1.6f, 0);
            driveBeacon.localScale = Vector3.one * .13f;
            timingTool = needle.parent;
            timingTool.gameObject.SetActive(false);
            controlBoard.gameObject.SetActive(false);
        }

        void UpdateContextPresentation(float dt)
        {
            bool help = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            string context = Driving ? "W/S 출력 · SPACE 제동 · E 놓기" : InteractionHint();
            if (State.Arrived) context = "동부 차량기지에 도착했다";
            if (context != lastContext) { lastContext = context; contextTime = Driving ? 3.5f : 2.6f; }
            contextTime = Mathf.Max(0, contextTime - dt);
            if (help && context.Length == 0) context = "A/D 이동 · W/S 사다리 · E 사용 · 휠 확대";
            bool visible = context.Length > 0 && (help || contextTime > 0);
            hintBoard.gameObject.SetActive(visible);
            hintText.text = context;
            float logicalSize = Driving || JourneyView ? .16f : .095f;
            hintText.characterSize = logicalSize * .28f;
            float alpha = help ? 1 : Mathf.Clamp01(contextTime / .5f);
            hintText.color = new Color(.96f, .92f, .8f, alpha);
            contextBacking.color = new Color(.07f, .11f, .14f, .74f * alpha);
            float width = Mathf.Clamp(context.Length * logicalSize * .77f, 1.4f, 6.3f);
            contextBacking.transform.localScale = new Vector3(width + .35f, Driving ? .4f : .31f, 1);
            timingTool.gameObject.SetActive(Repairing);
            foreach (var bolt in bolts) bolt.gameObject.SetActive(Repairing);
        }
    }
}
