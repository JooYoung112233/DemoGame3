// Only used by the editor's native layer exporter. No source texture is changed.
Shader "Hidden/Demo5/StandeeStraightAlphaExport"
{
    Properties { _MainTex("Native render target", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half4 Frag(v2f_img input) : SV_Target
            {
                half4 pixel = tex2D(_MainTex, input.uv);
                return half4(pixel.a > .00001h ? saturate(pixel.rgb / pixel.a) : half3(0,0,0), pixel.a);
            }
            ENDCG
        }
    }
}
