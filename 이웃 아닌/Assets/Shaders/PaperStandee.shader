// One native treatment for every person and creature. The PNG supplies only the shape.
// Paper width is measured in render-target pixels, not texture or world units.
Shader "Demo5/PaperStandee"
{
    Properties
    {
        [PerRendererData] _MainTex("Body alpha", 2D) = "white" {}
        _BodyColor("Body ink", Color) = (.0667,.0784,.0745,1)
        _RimColor("Paper edge", Color) = (.65,.62,.55,1)
        _RimPixels("Paper edge in screen pixels", Range(0,8)) = 2.5
        [HideInInspector] _Color("Sprite opacity", Color) = (1,1,1,1)
        [HideInInspector] _SpriteBounds("Local center and extents", Vector) = (0,0,.5,.5)
        [HideInInspector] _UvRect("Sprite UV bounds", Vector) = (0,0,1,1)
        [HideInInspector] _RendererFlip("Requested sprite flip", Vector) = (1,1,0,0)
        [HideInInspector] _RemovePaper("Remove legacy baked paper", Float) = 0
        [HideInInspector] _PaperCutoff("Legacy paper cutoff in sRGB", Float) = .38
    }
    SubShader
    {
        // Outline expansion reads each sprite's local bounds. CPU batching bakes its vertices
        // into another space and can turn that expansion inward, clipping heads and sides.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half opacity : TEXCOORD3; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor, _RimColor, _Color;
                float4 _SpriteBounds, _UvRect, _RendererFlip;
                float _RimPixels, _RemovePaper, _PaperCutoff;
            CBUFFER_END

            float2 PixelPosition(float3 local)
            {
                float4 clip = TransformObjectToHClip(local);
                return clip.xy / max(abs(clip.w), .0001) * _ScreenParams.xy * .5;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                // Some SpriteRenderer paths preflip vertices on the CPU, others leave it
                // to unity_SpriteProps. Match the bounds center to the incoming geometry.
                float2 center = _SpriteBounds.xy * _RendererFlip.xy * unity_SpriteProps.xy;
                float2 halfSize = max(_SpriteBounds.zw, .00001);
                float3 flippedCenter = UnityFlipSprite(float3(center, input.positionOS.z), unity_SpriteProps.xy);
                float2 pixelCenter = PixelPosition(flippedCenter);
                float2 pixelX = PixelPosition(UnityFlipSprite(float3(center + float2(halfSize.x, 0), input.positionOS.z), unity_SpriteProps.xy));
                float2 pixelY = PixelPosition(UnityFlipSprite(float3(center + float2(0, halfSize.y), input.positionOS.z), unity_SpriteProps.xy));
                float2 halfPixels = max(float2(length(pixelX - pixelCenter), length(pixelY - pixelCenter)), .001);
                float2 corner = sign(input.positionOS.xy - center);
                // SpriteRenderer can deliver flipped geometry with unflipped UVs. Derive the
                // UV direction from the texture corners; reusing geometry's sign stretches
                // a flipped body against its quad and slices off its arms/paper rim.
                float2 uvCorner = sign(input.uv - (_UvRect.xy + _UvRect.zw) * .5);
                // A FullRect quad is expanded before flipping. UVs extrapolate instead of stretching the body.
                float2 growth = (_RimPixels + 1.0) / halfPixels;
                input.positionOS.xy += corner * halfSize * growth;
                input.uv += uvCorner * (_UvRect.zw - _UvRect.xy) * .5 * growth;
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.opacity = input.color.a * _Color.a * unity_SpriteColor.a;
                return output;
            }

            half Shape(float2 uv)
            {
                // Clamp the sample, then mask the original rect: adjacent packed pixels cannot bleed into the edge.
                float inside = step(_UvRect.x, uv.x) * step(_UvRect.y, uv.y) * step(uv.x, _UvRect.z) * step(uv.y, _UvRect.w);
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, clamp(uv, _UvRect.xy, _UvRect.zw));
                if (_RemovePaper > .5)
                {
                    half3 rgb = source.rgb;
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                    rgb = LinearToSRGB(rgb);
                    #endif
                    half brightness = max(rgb.r, max(rgb.g, rgb.b));
                    source.a *= 1 - smoothstep(_PaperCutoff - .035, _PaperCutoff + .035, brightness);
                }
                // Generated alpha can contain almost invisible dust and translucent interior shading.
                // The pawn is opaque paper: normalize the body and reject that dust before outlining it.
                return smoothstep(.2h, .8h, source.a) * inside;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 x = ddx(input.uv) * _RimPixels;
                float2 y = ddy(input.uv) * _RimPixels;
                half body = Shape(input.uv);
                half expanded = body;
                // 16 directions plus an inner ring keep narrow fingers/antennae and small gaps continuous.
                const float2 directions[16] = {
                    float2(1,0), float2(.9238795,.3826834), float2(.7071068,.7071068), float2(.3826834,.9238795),
                    float2(0,1), float2(-.3826834,.9238795), float2(-.7071068,.7071068), float2(-.9238795,.3826834),
                    float2(-1,0), float2(-.9238795,-.3826834), float2(-.7071068,-.7071068), float2(-.3826834,-.9238795),
                    float2(0,-1), float2(.3826834,-.9238795), float2(.7071068,-.7071068), float2(.9238795,-.3826834)
                };
                [unroll] for (int i = 0; i < 16; i++)
                    expanded = max(expanded, Shape(input.uv + directions[i].x * x + directions[i].y * y));
                [unroll] for (int j = 0; j < 8; j++)
                    expanded = max(expanded, Shape(input.uv + (directions[j * 2].x * x + directions[j * 2].y * y) * .5));
                half inkAlpha = body * _BodyColor.a;
                // Express the edge as a real layer beneath the ink. This preserves the normal render
                // while also allowing BodyColor.a=0 / RimColor.a=0 to export independently composable layers.
                half paperCoverage = saturate((expanded - body) / max(1 - body, .0001h)) * _RimColor.a;
                half paperAlpha = paperCoverage * (1 - inkAlpha);
                half alpha = inkAlpha + paperAlpha;
                half3 rgb = (_BodyColor.rgb * inkAlpha + _RimColor.rgb * paperAlpha) / max(alpha, .0001h);
                return half4(rgb, alpha * input.opacity);
            }
            ENDHLSL
        }
    }
}
