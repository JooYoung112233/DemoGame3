Shader "Demo5/FacilityPaintedSprite" {
 Properties { _MainTex("Sprite",2D)="white"{} _Highlight("Highlight",Range(0,1))=0 _Width("Edge width",Float)=5 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { Tags {"LightMode"="Universal2D"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 float4 _MainTex_TexelSize; float _Highlight, _Width;
 struct A {float3 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS);o.uv=i.uv;o.color=i.color;return o;}
 half alpha(float2 uv){return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).a;}
 half4 frag(V i):SV_Target {half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);float2 d=_MainTex_TexelSize.xy*_Width;half edge=max(max(alpha(i.uv+float2(d.x,0)),alpha(i.uv-float2(d.x,0))),max(alpha(i.uv+float2(0,d.y)),alpha(i.uv-float2(0,d.y))));edge=max(edge,max(max(alpha(i.uv+d),alpha(i.uv-d)),max(alpha(i.uv+float2(d.x,-d.y)),alpha(i.uv+float2(-d.x,d.y)))));half rim=saturate(edge-c.a)*_Highlight;half opacity=c.a*i.color.a+rim*(1-c.a);half3 rgb=(c.rgb*i.color.rgb*c.a*i.color.a+half3(.74,.66,.43)*rim*(1-c.a))/max(opacity,.0001);return half4(rgb,opacity);}
 ENDHLSL
 } }
}
