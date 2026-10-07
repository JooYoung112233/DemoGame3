using UnityEngine;

namespace Demo6.Game
{
    /// <summary>가죽·철제 UI 원본을 9분할로 그린다. 모서리 크기는 화면 기준으로 유지한다.</summary>
    public static class UiSkinArt
    {
        static Texture2D _panel, _slot;
        public static Texture2D Panel => _panel ? _panel : (_panel = Resources.Load<Texture2D>("UI/V10/frame") ?? Resources.Load<Texture2D>("UI/Skin/panel"));
        public static Texture2D Socket => _slot ? _slot : (_slot = Resources.Load<Texture2D>("UI/V10/frame") ?? Resources.Load<Texture2D>("UI/Skin/slot"));

        public static void NineSlice(Rect r, Texture2D texture, float corner, Color tint, float interiorShade = 0f)
        {
            if (!texture) return;
            float b = Mathf.Min(corner, Mathf.Min(r.width, r.height) * .45f);
            const float uv = .12f;
            var previous = GUI.color;
            GUI.color = tint;
            // GUI y grows downward; texture UV y grows upward.
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                float dx = x == 0 ? r.x : x == 1 ? r.x + b : r.xMax - b;
                float dy = y == 0 ? r.y : y == 1 ? r.y + b : r.yMax - b;
                float w = x == 1 ? r.width - b * 2 : b;
                float h = y == 1 ? r.height - b * 2 : b;
                float sx = x == 0 ? 0 : x == 1 ? uv : 1 - uv;
                float sy = y == 0 ? 1 - uv : y == 1 ? uv : 0;
                float sw = x == 1 ? 1 - uv * 2 : uv;
                float sh = y == 1 ? 1 - uv * 2 : uv;
                GUI.DrawTextureWithTexCoords(new Rect(dx,dy,w,h), texture, new Rect(sx,sy,sw,sh));
            }
            if (interiorShade > 0)
                DungeonUi.Fill(new Rect(r.x+b*.58f,r.y+b*.58f,r.width-b*1.16f,r.height-b*1.16f),new Color(0,0,0,interiorShade*tint.a));
            GUI.color = previous;
        }

        public static Texture2D Button(float brightness)
        {
            var source = Resources.Load<Texture2D>("UI/V10/button") ?? Resources.Load<Texture2D>("UI/Skin/button");
            if (!source) return null;
            var tex = Object.Instantiate(source);
            tex.name = "Iron button " + brightness;
            tex.hideFlags = HideFlags.DontSave;
            var pixels = tex.GetPixels();
            for(int i=0;i<pixels.Length;i++) { var c=pixels[i]; c.r*=brightness; c.g*=brightness; c.b*=brightness; pixels[i]=c; }
            tex.SetPixels(pixels); tex.Apply(false,true);
            return tex;
        }

        public static void Selection(Rect r)
        {
            // Four bone-silver corner clasps remain distinct from a grade-colored inner rim.
            const float d=7f, length=12f, t=2f;
            Color c=DungeonUi.Bone;
            DungeonUi.Fill(new Rect(r.x+d,r.y+d,length,t),c);
            DungeonUi.Fill(new Rect(r.x+d,r.y+d,t,length),c);
            DungeonUi.Fill(new Rect(r.xMax-d-length,r.y+d,length,t),c);
            DungeonUi.Fill(new Rect(r.xMax-d-t,r.y+d,t,length),c);
            DungeonUi.Fill(new Rect(r.x+d,r.yMax-d-t,length,t),c);
            DungeonUi.Fill(new Rect(r.x+d,r.yMax-d-length,t,length),c);
            DungeonUi.Fill(new Rect(r.xMax-d-length,r.yMax-d-t,length,t),c);
            DungeonUi.Fill(new Rect(r.xMax-d-t,r.yMax-d-length,t,length),c);
        }
    }
}
