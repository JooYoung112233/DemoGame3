Shader "Demo5/SettlementFacilityContour" {
 Properties { _Color("Color",Color)=(1,1,1,1) }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" } Pass {
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; };
 struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; };
 Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;return o;}
 half4 frag(Varyings i):SV_Target{return i.color;}
 ENDHLSL
 } }
}
