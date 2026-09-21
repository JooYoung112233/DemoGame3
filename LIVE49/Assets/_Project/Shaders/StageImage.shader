// UI image shader for stage art: blur, pixelated face mask (C0 photo) and the shared screen glitch.
Shader "Live49/UI/StageImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurTexels ("Blur Radius (source texels)", Float) = 0
        _MaskTex ("Face Mask", 2D) = "black" {}
        _MaskOn ("Face Mask On", Float) = 0
        _Cells ("Face Mask Cells", Vector) = (76, 43, 0, 0)
        _GlitchAmount ("Receives Glitch", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            sampler2D _MaskTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _BlurTexels;
            float _MaskOn;
            float4 _Cells;
            float _GlitchAmount;

            // Globals driven by the chapter director.
            float _L49Glitch;
            float _L49GlitchSeed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Hash(float n)
            {
                return frac(sin(n * 127.1 + _L49GlitchSeed * 311.7) * 43758.5453);
            }

            // 5x5 gaussian taps spread across the radius; cheap enough for a few full-screen stage images.
            half4 SampleBlurred(float2 uv)
            {
                if (_BlurTexels < 0.05)
                    return tex2Dlod(_MainTex, float4(uv, 0, 0)) + _TextureSampleAdd;

                float2 stepUV = _MainTex_TexelSize.xy * (_BlurTexels * 0.5);
                half4 acc = 0;
                float weightSum = 0;
                [unroll] for (int y = -2; y <= 2; y++)
                {
                    [unroll] for (int x = -2; x <= 2; x++)
                    {
                        float w = exp(-(x * x + y * y) / 4.5);
                        acc += tex2Dlod(_MainTex, float4(uv + float2(x, y) * stepUV, 0, 0)) * w;
                        weightSum += w;
                    }
                }
                return acc / weightSum + _TextureSampleAdd;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float glitch = _L49Glitch * _GlitchAmount;
                if (glitch > 0)
                {
                    float band = floor(uv.y * 48.0);
                    if (Hash(band) > 0.72)
                        uv.x += (Hash(band + 7.0) - 0.5) * 0.045 * glitch;
                }

                half4 color = SampleBlurred(uv);

                if (_MaskOn > 0.5)
                {
                    half mask = tex2Dlod(_MaskTex, float4(IN.texcoord, 0, 0)).a;
                    if (mask > 0.001)
                    {
                        float2 cell = (floor(uv * _Cells.xy) + 0.5) / _Cells.xy;
                        color = lerp(color, SampleBlurred(cell), mask);
                    }
                }

                color *= IN.color;

                if (glitch > 0)
                    color.rgb *= 1.0 - 0.12 * glitch * step(0.5, frac(IN.vertex.y * 0.5));

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                color.rgb *= color.a;
                return color;
            }
        ENDCG
        }
    }
}
