Shader "Demo5/GroundShadow"
{
    Properties { _MainTex("Silhouette",2D)="white"{} _Color("Shadow",Color)=(.01,.017,.025,.32) }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags {"LightMode"="Universal2D"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Input {float3 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Output Vert(Input v){Output o;o.positionCS=TransformObjectToHClip(v.positionOS);o.uv=v.uv;return o;}
            half4 Frag(Output i):SV_Target{half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;return half4(_Color.rgb,_Color.a*alpha*(1-.6*smoothstep(.15,1,i.uv.y)));}
            ENDHLSL
        }
    }
}
