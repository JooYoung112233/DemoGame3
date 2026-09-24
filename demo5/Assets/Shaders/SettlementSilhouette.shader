Shader "Demo5/SettlementSilhouette" {
 Properties {
  _MainTex("Signed distance mask", 2D) = "black" {}
  _Color("Outline", Color) = (.86,.83,.69,.88)
  _DistanceScale("Reference pixels per mask texel", Float) = 1
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float _DistanceScale;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
   Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
   half4 frag(Varyings i):SV_Target {
    float d = abs((SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).r * 2 - 1) * 16 * _DistanceScale);
    float edge = 1 - smoothstep(.2, .85, d);
    float glow = 1 - smoothstep(.6, 1.8, d);
    return half4(_Color.rgb, (edge * .52 + glow * .09) * _Color.a);
   }
   ENDHLSL
  }
 }
}
