Shader "Demo6/SlashWaveV044"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0; };
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;o.uv=i.uv;return o;}
            half4 frag(Varyings i):SV_Target{
                float u=saturate(i.uv.x),v=saturate(i.uv.y);
                float tail=smoothstep(0,.16,u)*(.27+.73*pow(u,.75));
                float outside=1-smoothstep(.94,1,v);
                float lip=exp(-pow((v-.83)/.17,2));
                float wash=smoothstep(0,.58,v)*.19;
                float a=(.78*lip+wash)*outside*tail;
                return half4(i.color.rgb,i.color.a*a);
            }
            ENDHLSL
        }
    }
}
