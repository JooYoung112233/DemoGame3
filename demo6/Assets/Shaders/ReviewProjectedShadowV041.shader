Shader "Demo6/ReviewProjectedShadowV041"
{
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float3 positionOS:POSITION; half4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; };
   Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.color=v.color; return o; }
   half4 Frag(Varyings v):SV_Target { return v.color; }
   ENDHLSL
  }
 }
}
