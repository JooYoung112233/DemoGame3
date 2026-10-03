using UnityEngine;
using C = Demo6.Game.TopDownCanvas;

namespace Demo6.Game
{
    /// <summary>굴쥐 v1. 정수리 파츠의 편집 원본은 이 도형·색·좌표다. 오른쪽 +x, 몸 단위 피벗 0.
    /// 런타임 파츠와 1024 PPU 원본 PNG를 같은 함수에서 각각 래스터화한다(확대 보간 아님).</summary>
    public static class RatMineArt
    {
        static Sprite _body, _head, _threat, _bite, _tail, _foot, _dead;
        public static Sprite Body => _body ? _body : _body = Build("preview");
        public static Sprite Head => _head ? _head : _head = Build("head");
        public static Sprite Threat => _threat ? _threat : _threat = Build("threat");
        public static Sprite Bite => _bite ? _bite : _bite = Build("bite");
        public static Sprite Tail => _tail ? _tail : _tail = Build("tail");
        public static Sprite Foot => _foot ? _foot : _foot = Build("foot");
        public static Sprite Dead => _dead ? _dead : _dead = Build("dead");
        static readonly Color Ink = new Color(11f/255f, 10f/255f, 13f/255f);
        static readonly Color Fur = new Color(79f/255f, 66f/255f, 55f/255f);
        static readonly Color FurLight = new Color(0.54f, 0.49f, 0.39f);
        static readonly Color Skin = new Color(0.39f, 0.285f, 0.25f);
        static readonly Color Bone = new Color(0.7f, 0.66f, 0.53f);

        public static Sprite Build(string part, float ppu = 220f)
        {
            if (part == "foot") return BuildFoot(ppu * 2f);
            var cv = new C(-0.875f, 0.875f, -0.5f, 0.5f, ppu);
            if (part == "tail" || part == "preview" || part == "dead") DrawTail(cv, part == "dead");
            if (part == "body" || part == "preview" || part == "dead") DrawBody(cv, part == "dead");
            if (part == "head" || part == "threat" || part == "bite" || part == "preview" || part == "dead")
                DrawHead(cv, part == "threat" ? 0.65f : part == "bite" ? 1f : 0f, part == "dead");
            if (part == "dead")
            {
                // 구부러진 발과 닫힌 눈. 처치 프레임은 머리·꼬리까지 합쳐 시체 복사가 빠뜨리지 않는다.
                for (int i = 0; i < 4; i++)
                {
                    float x = i < 2 ? 0.17f : -0.3f, side = (i & 1) == 0 ? 1f : -1f;
                    cv.Draw(p => C.Capsule(p,new Vector2(x,0.21f*side),new Vector2(x-0.05f,0.32f*side),0.035f),Skin,0.012f,Ink);
                }
                cv.Draw(p => C.Capsule(p,new Vector2(-0.14f,-0.025f),new Vector2(0.06f,0.015f),0.018f),new Color(0.18f,0.055f,0.04f));
            }
            Vector2 pivot = part == "tail" ? new Vector2(-0.43f,0f) :
                part == "head" || part == "threat" || part == "bite" ? new Vector2(0.23f,0f) : Vector2.zero;
            return cv.ToSprite("굴쥐 v1 " + part, pivot);
        }

        static void DrawBody(C cv, bool dead)
        {
            float flatten = dead ? 0.87f : 1f;
            cv.Draw(p => C.Ellipse(p,new Vector2(-0.16f,0f),0.34f,0.265f*flatten),
                (p,d) => C.Shade(Fur,C.Dome(d,0.21f)*(0.94f+0.12f*C.Noise(p,0.12f,181))),0.022f,Ink);
            // 큰 견갑·등 면 두 개. 잔털 대신 굵은 털 다발만 남겨 축소 화면에서도 등이 읽힌다.
            cv.Draw(p => C.Ellipse(p,new Vector2(-0.13f,0.015f),0.265f,0.175f*flatten),
                (p,d) => Color.Lerp(Fur,FurLight,0.28f+0.34f*C.Dome(d,0.16f)),0.008f,Fur);
            cv.Draw(p => C.Capsule(p,new Vector2(-0.39f,0f),new Vector2(0.15f,0f),0.035f,0.05f),
                (p,d) => C.Shade(Fur,0.58f+0.16f*Mathf.Cos(p.x*24f)),0.005f,Ink);
            for (int i=0;i<6;i++)
            {
                float x=-0.39f+i*0.087f;
                for(int side=-1;side<=1;side+=2)
                {
                    float s=side, edge=0.19f+0.045f*Mathf.Sin(i*0.7f);
                    cv.Draw(p=>C.Capsule(p,new Vector2(x+0.035f,(edge-0.018f)*s),new Vector2(x-0.045f,(edge+0.018f)*s),0.022f,0.005f),Fur,0.007f,Ink);
                }
            }
            // 어깨는 앞이 좁은 쐐기. 머리 아래로 겹쳐 관절 틈이 보이지 않게 한다.
            cv.Draw(p=>C.Ellipse(p,new Vector2(0.14f,0f),0.19f,0.19f),
                (p,d)=>C.Shade(Fur,C.Dome(d,0.12f)),0.013f,Ink);
        }

        static void DrawTail(C cv, bool dead)
        {
            Vector2 end=dead?new Vector2(-0.76f,-0.2f):new Vector2(-0.79f,0.16f);
            cv.Draw(p=>C.Curve(p,new Vector2(-0.43f,0f),new Vector2(-0.74f,-0.19f),end,0.047f,0.009f),
                (p,d)=>C.Shade(Skin,C.Dome(d,0.035f)*0.9f),0.014f,Ink);
            cv.Draw(p=>C.Curve(p,new Vector2(-0.46f,0.012f),new Vector2(-0.74f,-0.17f),end,0.01f,0.003f),
                new Color(0.49f,0.37f,0.31f));
        }

        static void DrawHead(C cv, float open, bool dead)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;
                cv.Draw(p=>C.Ellipse(p,new Vector2(0.22f,0.153f*s),0.095f,0.065f,18f*s),Fur,0.017f,Ink);
                cv.Draw(p=>C.Ellipse(p,new Vector2(0.24f,0.16f*s),0.048f,0.035f,18f*s),C.Shade(Skin,0.86f));
            }
            cv.Draw(p=>Mathf.Min(C.Ellipse(p,new Vector2(0.32f,0f),0.16f,0.13f),
                C.Triangle(p,new Vector2(0.31f,-0.13f),new Vector2(0.68f,0f),new Vector2(0.31f,0.13f))),
                (p,d)=>C.Shade(FurLight,C.Dome(d,0.13f)*(0.92f+0.06f*C.Noise(p,0.09f,183))),0.018f,Ink);
            if(open>0f)
            {
                // 정수리에서 보이는 앞쪽 입 안과 아래턱. 방향은 +x로 유지한다.
                cv.Draw(p=>C.Ellipse(p,new Vector2(0.57f+open*0.035f,0f),0.09f,0.055f+open*0.022f),Ink);
                for(int side=-1;side<=1;side+=2)
                {
                    float s=side;
                    cv.Draw(p=>C.Triangle(p,new Vector2(0.55f,0.06f*s),new Vector2(0.64f,0.019f*s),new Vector2(0.59f,0.063f*s)),Bone,0.004f,Ink);
                }
            }
            else
                cv.Draw(p=>C.Capsule(p,new Vector2(0.55f,-0.035f),new Vector2(0.6f,0.035f),0.007f),C.Shade(Skin,0.5f));
            cv.Draw(p=>C.Ellipse(p,new Vector2(0.64f+open*0.055f,0f),0.033f,0.037f),Ink,0.008f,C.Shade(Skin,0.8f));
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;
                if(dead)cv.Draw(p=>C.Capsule(p,new Vector2(0.405f,0.092f*s),new Vector2(0.44f,0.076f*s),0.008f),Ink);
                else{
                    cv.Draw(p=>C.Ellipse(p,new Vector2(0.425f,0.085f*s),0.024f,0.013f),Ink);
                    cv.Draw(p=>C.Circle(p,new Vector2(0.435f,0.089f*s),0.006f),new Color(0.68f,0.55f,0.32f));
                }
                cv.Draw(p=>C.Capsule(p,new Vector2(0.525f,0.055f*s),new Vector2(0.59f,0.15f*s),0.006f),new Color(0.55f,0.5f,0.42f));
            }
        }

        static Sprite BuildFoot(float ppu)
        {
            var cv=new C(-0.03125f,0.03125f,-0.0234375f,0.0234375f,ppu);
            cv.Draw(p=>C.Ellipse(p*2f,new Vector2(-0.009f,0f),0.04f,0.027f),Skin,0.005f,Ink);
            for(int i=-1;i<=1;i++){
                float y=i*0.015f;
                cv.Draw(p=>C.Capsule(p*2f,new Vector2(0.01f,y),new Vector2(0.04f,y),0.009f),Skin,0.003f,Ink);
                cv.Draw(p=>C.Triangle(p*2f,new Vector2(0.035f,y-0.005f),new Vector2(0.061f,y),new Vector2(0.035f,y+0.005f)),Bone);
            }
            return cv.ToSprite("굴쥐 v1 발",Vector2.zero);
        }
    }

}
