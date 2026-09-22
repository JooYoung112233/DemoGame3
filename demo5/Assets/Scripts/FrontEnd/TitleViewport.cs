using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    [ExecuteAlways]
    public sealed class TitleViewport : MonoBehaviour
    {
        public RectTransform DesignRoot;
        public Font FontOverride;
        public Vector2 DesignSize = new Vector2(1920,1080);
        Font runtimeFont;
        bool ownsFont;
        void OnEnable() { RefreshFont(); Fit(); }
        void Update() { Fit(); }
        public void RefreshFont()
        {
            if (FontOverride && runtimeFont != FontOverride)
            {
                if (runtimeFont && ownsFont) { if(Application.isPlaying) Destroy(runtimeFont); else DestroyImmediate(runtimeFont); }
                runtimeFont = FontOverride;
                ownsFont = false;
            }
            if (!runtimeFont) { runtimeFont = FontOverride ? FontOverride : Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},32); ownsFont = !FontOverride && runtimeFont; }
            if (!runtimeFont) runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (!FontOverride || !text.font || text.font.name == "LegacyRuntime") text.font = runtimeFont;
        }
        void Fit()
        {
            if (!DesignRoot || !(transform is RectTransform parent)) return;
            float scale = Mathf.Min(parent.rect.width / DesignSize.x, parent.rect.height / DesignSize.y);
            if (scale > 0 && !Mathf.Approximately(DesignRoot.localScale.x,scale)) DesignRoot.localScale=Vector3.one*scale;
        }
        void OnDestroy() { if (runtimeFont && ownsFont) { if(Application.isPlaying) Destroy(runtimeFont); else DestroyImmediate(runtimeFont); } }
    }
}
