// URP 2D lighting with existing hit-flash response.
// Based on the locally installed URP 17.6 Sprite-Lit-Default.
Shader "Demo6/SpriteFlash"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        [HideInInspector] _UseMeshRenderer("Mesh Renderer", Float) = 0
        _FlashColor("Flash Color", Color) = (1,1,1,1)
        _FlashAmount("Flash Amount", Range(0,1)) = 0
        [HideInInspector] _BladeRelief("Blade face clarity", Float) = 0
        [HideInInspector] _BladeSize("Blade sprite size and length", Vector) = (1,1,0,1)
        [HideInInspector] _BladeLineR("Right blade axis", Vector) = (0,0,1,0)
        [HideInInspector] _BladeLineL("Left blade axis", Vector) = (0,0,1,0)
        [HideInInspector] _BladeTilt("Tilt and width", Vector) = (0,0,.3,0)
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FlashColor;
                half _FlashAmount;
                half _UseMeshRenderer;
                half _BladeRelief;
                float4 _BladeSize,_BladeLineR,_BladeLineL,_BladeTilt;
            CBUFFER_END


            // Small value separation on existing painted blade faces. Alpha and geometry are unchanged.
            half BladePlaneV044(float2 uv,float4 bladeAxis,float tilt)
            {
                float2 q=(uv-bladeAxis.xy)*_BladeSize.xy;
                float along=dot(q,bladeAxis.zw);
                float side=dot(q,float2(-bladeAxis.w,bladeAxis.z));
                float width=max(.08,_BladeTilt.z);
                float end=_BladeSize.w*max(.035,abs(cos(radians(tilt))));
                float blade=smoothstep(_BladeSize.z-.02,_BladeSize.z+.04,along)*(1-smoothstep(end-.035,end+.015,along));
                blade*=1-smoothstep(width*.48,width*.65,abs(side));
                float ridge=exp(-pow(side/(.014+width*.035),2));
                float faces=lerp(-.16,.055,smoothstep(-width*.30,width*.12,side));
                return blade*(faces+.15*ridge)*(.45+.55*abs(sin(radians(tilt))));
            }
            half BladeFacetV044(float2 uv,half3 color)
            {
                half lum=dot(color,half3(.2126,.7152,.0722));
                half chroma=max(color.r,max(color.g,color.b))-min(color.r,min(color.g,color.b));
                half metal=smoothstep(.025,.12,lum)*(1-smoothstep(.045,.16,chroma));
                half tone=BladePlaneV044(uv,_BladeLineR,_BladeTilt.x);
                if(_BladeTilt.w>.5)tone+=BladePlaneV044(uv,_BladeLineL,_BladeTilt.y);
                return clamp(tone,-.15,.15)*metal*saturate(_BladeRelief);
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                if (_UseMeshRenderer < 0.5h)
                    input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * (_UseMeshRenderer > 0.5h ? half4(1,1,1,1) : unity_SpriteColor);

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 result = CommonLitFragment(input, input.color);
                half4 sourceColor = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(sourceColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 flashTone = _FlashColor.rgb * (0.30h + 0.70h * saturate(luminance * 1.8h));
                result.rgb *= 1.0h + BladeFacetV044(input.uv,sourceColor.rgb);
                // Retain the existing 0.55 hit-flash mix and alpha. Only the timed hit flash is self-lit.
                result.rgb = lerp(result.rgb, flashTone, saturate(_FlashAmount) * 0.55h);
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
                half4 _FlashColor;
                half _FlashAmount;
                half _UseMeshRenderer;
                half _BladeRelief;
                float4 _BladeSize,_BladeLineR,_BladeLineL,_BladeTilt;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                if (_UseMeshRenderer < 0.5h)
                    input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                if (_UseMeshRenderer > 0.5h)
                {
                    input.normal = float3(0, 0, -1);
                    input.tangent = float4(1, 0, 0, 1);
                }
                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * (_UseMeshRenderer > 0.5h ? half4(1,1,1,1) : unity_SpriteColor);

                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // Setup instancing for SpriteFlip is used in NormalsRenderingShared
                SetUpSpriteInstanceProperties();

                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
          
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FlashColor;
                half _FlashAmount;
                half _UseMeshRenderer;
                half _BladeRelief;
                float4 _BladeSize,_BladeLineR,_BladeLineL,_BladeTilt;
            CBUFFER_END


            // Small value separation on existing painted blade faces. Alpha and geometry are unchanged.
            half BladePlaneV044(float2 uv,float4 bladeAxis,float tilt)
            {
                float2 q=(uv-bladeAxis.xy)*_BladeSize.xy;
                float along=dot(q,bladeAxis.zw);
                float side=dot(q,float2(-bladeAxis.w,bladeAxis.z));
                float width=max(.08,_BladeTilt.z);
                float end=_BladeSize.w*max(.035,abs(cos(radians(tilt))));
                float blade=smoothstep(_BladeSize.z-.02,_BladeSize.z+.04,along)*(1-smoothstep(end-.035,end+.015,along));
                blade*=1-smoothstep(width*.48,width*.65,abs(side));
                float ridge=exp(-pow(side/(.014+width*.035),2));
                float faces=lerp(-.16,.055,smoothstep(-width*.30,width*.12,side));
                return blade*(faces+.15*ridge)*(.45+.55*abs(sin(radians(tilt))));
            }
            half BladeFacetV044(float2 uv,half3 color)
            {
                half lum=dot(color,half3(.2126,.7152,.0722));
                half chroma=max(color.r,max(color.g,color.b))-min(color.r,min(color.g,color.b));
                half metal=smoothstep(.025,.12,lum)*(1-smoothstep(.045,.16,chroma));
                half tone=BladePlaneV044(uv,_BladeLineR,_BladeTilt.x);
                if(_BladeTilt.w>.5)tone+=BladePlaneV044(uv,_BladeLineL,_BladeTilt.y);
                return clamp(tone,-.15,.15)*metal*saturate(_BladeRelief);
            }

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                if (_UseMeshRenderer < 0.5h)
                    input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * (_UseMeshRenderer > 0.5h ? half4(1,1,1,1) : unity_SpriteColor);
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 result = CommonUnlitFragment(input, input.color);
                half4 sourceColor = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(sourceColor.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 flashTone = _FlashColor.rgb * (0.30h + 0.70h * saturate(luminance * 1.8h));
                result.rgb *= 1.0h + BladeFacetV044(input.uv,sourceColor.rgb);
                // Retain the existing 0.55 hit-flash mix and alpha. Only the timed hit flash is self-lit.
                result.rgb = lerp(result.rgb, flashTone, saturate(_FlashAmount) * 0.55h);
                return result;
            }
            ENDHLSL
        }
    }
}
