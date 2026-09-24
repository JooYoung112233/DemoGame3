// UI uses the same flat ink as world standees. Card paper stays a separate UI image.
// Retains Unity UI stencil and RectMask2D behavior; it never draws a rim outside a card.
Shader "Demo5/PaperPortrait"
{
    Properties
    {
        [PerRendererData] _MainTex("Portrait alpha", 2D) = "white" {}
        _BodyColor("Body ink", Color) = (.0667,.0784,.0745,1)
        _Color("Opacity", Color) = (1,1,1,1)
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct AppData { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; half4 mask : TEXCOORD2; half opacity : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            half4 _BodyColor, _Color, _TextureSampleAdd;
            float4 _ClipRect, _MainTex_ST;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            Varyings Vert(AppData input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.opacity = input.color.a * _Color.a;
                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = half4(input.vertex.xy * 2 - rect.xy - rect.zw, .25 / (.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half sourceAlpha = (tex2D(_MainTex, input.uv) + _TextureSampleAdd).a;
                half alpha = smoothstep(.2h, .8h, sourceAlpha) * _BodyColor.a * input.opacity;
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                alpha *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - .001);
                #endif
                return half4(_BodyColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
