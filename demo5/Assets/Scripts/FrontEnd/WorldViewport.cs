using UnityEngine;

namespace Demo5.FrontEnd
{
    // Keep the painted room and TitleViewport's fitted UI in the same 16:9 frame.
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class WorldViewport : MonoBehaviour
    {
        public Vector2 DesignWorldSize = new Vector2(19.2f, 10.8f);
        Camera target;
        void OnEnable() { target = GetComponent<Camera>(); Fit(); }
        void LateUpdate() { Fit(); }
        void OnPreCull() { Fit(); }
        public void Fit()
        {
            if (!target) target = GetComponent<Camera>();
            if (!target.orthographic || target.aspect <= 0) return;
            target.orthographicSize = Mathf.Max(DesignWorldSize.y * .5f,
                DesignWorldSize.x * .5f / target.aspect);
        }
    }
}
