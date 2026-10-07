Shader "Demo6/BladeTrailV042"
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
                float age=saturate(i.uv.x);
                float edge=smoothstep(0,.30,i.uv.y)*smoothstep(0,.14,1-i.uv.y);
                float tail=pow(1-age,2.1);
                float lip=1-smoothstep(.008,.07,age);
                return half4(i.color.rgb,i.color.a*edge*(.34*tail+.70*lip));
            }
            ENDHLSL
        }
    }
}
