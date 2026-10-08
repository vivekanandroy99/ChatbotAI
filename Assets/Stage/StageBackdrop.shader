// The stage behind the avatar (StageBackdrop.cs sets every property). Unlit - no lights, no shadows: cheap on the GPU,
// which the voice engines share. Two modes:
//   studio / gradient: a colour gradient on the curved wall, a glow behind the character, a pool of light and a soft
//                      contact shadow on the floor (gradient = the same without glow and pool);
//   picture:           a picture on a screen-filling plane, blurred like a photo's out-of-focus background.
// A little noise (in display space) keeps dark gradients from showing bands.
Shader "ChatbotAI/Stage Backdrop"
{
    Properties
    {
        _WallTop ("Wall top", Color) = (0.07, 0.07, 0.08, 1)
        _WallBottom ("Wall bottom", Color) = (0.15, 0.15, 0.17, 1)
        _Floor ("Floor", Color) = (0.11, 0.11, 0.12, 1)
        _Glow ("Glow", Color) = (0.2, 0.22, 0.28, 1)
        _GlowCenter ("Glow centre (world)", Vector) = (0, 1.6, -8, 0)
        _GlowRadius ("Glow radius (m)", Float) = 4
        _PoolCenter ("Character position (world)", Vector) = (0, 0, 0, 0)
        _PoolRadius ("Light pool radius (m)", Float) = 2.5
        _PoolStrength ("Light pool strength", Float) = 0.6
        _ShadowRadius ("Contact shadow radius (m)", Float) = 0.45
        _ShadowStrength ("Contact shadow strength", Range(0, 1)) = 0.5
        _GradientHeight ("Gradient height (m)", Float) = 6
        _Brightness ("Brightness", Float) = 1
        _MainTex ("Picture", 2D) = "black" {}
        _PictureBlur ("Picture blur (mip level)", Float) = 3
        _PictureMode ("Picture mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StageBackdrop"
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WallTop, _WallBottom, _Floor, _Glow;
                float4 _GlowCenter, _PoolCenter;
                float _GlowRadius, _PoolRadius, _PoolStrength, _ShadowRadius, _ShadowStrength;
                float _GradientHeight, _Brightness, _PictureBlur, _PictureMode;
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }

            float Falloff(float distance, float radius)
            {
                float d = distance / max(radius, 0.001);
                return exp(-d * d);
            }

            float3 Studio(float3 p)
            {
                float height = saturate(p.y / max(_GradientHeight, 0.01));
                float3 wall = lerp(_WallBottom.rgb, _WallTop.rgb, smoothstep(0.0, 1.0, height));
                // The floor fades into the wall through the curved cove (no horizon line).
                float onFloor = 1.0 - smoothstep(0.0, 1.2, p.y);
                float3 color = lerp(wall, _Floor.rgb, onFloor);

                color += _Glow.rgb * Falloff(distance(p, _GlowCenter.xyz), _GlowRadius);
                float2 fromFeet = p.xz - _PoolCenter.xz;
                color += _Glow.rgb * _PoolStrength * onFloor * Falloff(length(fromFeet), _PoolRadius);
                // Soft shadow under the feet, a little longer front-to-back than sideways.
                float shadow = Falloff(length(fromFeet * float2(1.0, 0.75)), _ShadowRadius) * onFloor;
                return color * (1.0 - _ShadowStrength * shadow);
            }

            float3 Picture(float2 uv)
            {
                // Out-of-focus look: a ring of samples from a lower mip level.
                const float2 taps[8] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                                         float2(0.7, 0.7), float2(-0.7, 0.7), float2(0.7, -0.7), float2(-0.7, -0.7) };
                float lod = max(_PictureBlur, 0.0);
                float spread = exp2(lod) * 1.5 / 1024.0;
                float3 sum = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, lod).rgb * 2.0;
                [unroll] for (int i = 0; i < 8; i++)
                    sum += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv + taps[i] * spread, lod).rgb;
                return sum / 10.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 color = (_PictureMode > 0.5 ? Picture(input.uv) : Studio(input.positionWS)) * _Brightness;

                // Dither in display (sRGB) space, about half a step of 8-bit colour.
                float noise = frac(sin(dot(input.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    float3 display = LinearToSRGB(max(color, 0.0));
                    color = SRGBToLinear(saturate(display + noise / 255.0));
                #else
                    color = saturate(color + noise / 255.0);
                #endif
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
