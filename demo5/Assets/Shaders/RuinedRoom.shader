Shader "Demo5/RuinedRoom"
{
    Properties
    {
        _MainTex("Room",2D)="white"{}
        _MaskTex("Light mask",2D)="white"{}
        _NormalMap("Normal",2D)="bump"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Exposure("Exposure",Range(.2,1.2))=.8
        _Saturation("Saturation",Range(0,1))=.65
        _Vignette("Edge shadow",Range(0,1))=.55
        _Grain("Dust grain",Range(0,.15))=.045
        _Dampness("Damp wall stains",Range(0,.5))=.22
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags {"LightMode"="Universal2D"}
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; UNITY_SKINNED_VERTEX_INPUTS };
            struct Varyings { COMMON_2D_LIT_OUTPUTS half4 color:COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Exposure,_Saturation,_Vignette,_Grain,_Dampness;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings o=CommonLitVertex(input);
                o.color=input.color*_Color*unity_SpriteColor;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 c=CommonLitFragment(input,input.color);
                float2 uv=input.uv;
                half l=dot(c.rgb,half3(.2126,.7152,.0722));
                c.rgb=lerp(l.xxx,c.rgb,_Saturation);
                // Stable room-space grain: no temporal flickering and no noise on UI or pawns.
                float2 cell=floor(uv*float2(960,540));
                half noise=frac(sin(dot(cell,float2(12.9898,78.233)))*43758.5453)-.5;
                float2 edge=(uv-.5)*2;
                half shade=1-_Vignette*smoothstep(.25,1.45,dot(edge,edge));
                half stains=saturate(sin(uv.x*63+sin(uv.y*11)*2)*sin(uv.y*18+uv.x*7));
                shade*=1-_Dampness*stains*smoothstep(.3,.85,uv.y);
                c.rgb=pow(max(c.rgb,0),1.12)*_Exposure*shade;
                c.rgb*=lerp(half3(.77,.86,1),half3(1,1,.96),saturate(l*2));
                c.rgb=max(0,c.rgb*(1+noise*_Grain*2));
                return c;
            }
            ENDHLSL
        }
    }
}
