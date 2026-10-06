Shader "Demo5/SettlementProp"
{
 Properties { _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { Tags {"LightMode"="Universal2D"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _Color;
   CBUFFER_END
   struct Input {float3 positionOS:POSITION;half4 color:COLOR;};
   struct Output {float4 positionCS:SV_POSITION;half4 color:COLOR;float2 grain:TEXCOORD0;};
   Output Vert(Input v){Output o;o.positionCS=TransformObjectToHClip(v.positionOS);o.color=v.color*_Color;o.grain=v.positionOS.xy;return o;}
   half4 Frag(Output i):SV_Target {float n=frac(sin(dot(floor(i.grain*210),float2(12.9898,78.233)))*43758.5453);return half4(i.color.rgb*(.91+.16*n),i.color.a);}
   ENDHLSL
  }
 }
}
