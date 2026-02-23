Shader "CapybaraDuel/MagicOrange"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.0

        [Header(Fresnel Rim Light)]
        [Toggle(_FRESNEL_ON)] _FresnelOn ("Enable Fresnel", Float) = 1
        _FresnelColor ("Fresnel Color", Color) = (1, 0.7, 0.2, 1)
        _FresnelPower ("Fresnel Power (edge sharpness)", Range(0.5, 8)) = 2.5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 5)) = 1.5

        [Header(Outer Glow)]
        [Toggle(_GLOW_ON)] _GlowOn ("Enable Outer Glow", Float) = 1
        _GlowColor ("Glow Color", Color) = (1, 0.5, 0, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 8)) = 2.0
        _GlowPower ("Glow Falloff", Range(0.5, 6)) = 1.5

        [Header(Specular Highlight Boost)]
        _SpecBoost ("Specular Boost", Range(1, 10)) = 3.0
        _SpecPower ("Specular Power", Range(8, 256)) = 64

        [Header(Surface Flow Light)]
        [Toggle(_FLOW_ON)] _FlowOn ("Enable Flow Light", Float) = 1
        _FlowColor ("Flow Color", Color) = (1, 0.85, 0.4, 1)
        _FlowIntensity ("Flow Intensity", Range(0, 3)) = 0.8
        _FlowSpeed ("Flow Speed", Range(0.1, 5)) = 0.5
        _FlowWidth ("Flow Width", Range(0.02, 0.5)) = 0.12
        _FlowDirection ("Flow Direction (XYZ)", Vector) = (0.3, 1, 0.2, 0)

        [Header(Pulse Animation)]
        [Toggle(_PULSE_ON)] _PulseOn ("Enable Pulse", Float) = 1
        _PulseSpeed ("Pulse Speed", Range(0.2, 5)) = 1.2
        _PulseMin ("Pulse Min Brightness", Range(0, 1)) = 0.7
        _PulseMax ("Pulse Max Brightness", Range(1, 3)) = 1.3

        [Header(Emission)]
        _EmissionColor ("Emission Color", Color) = (0.3, 0.15, 0, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 5)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ===================== MAIN PASS =====================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #pragma shader_feature_local _FRESNEL_ON
            #pragma shader_feature_local _GLOW_ON
            #pragma shader_feature_local _FLOW_ON
            #pragma shader_feature_local _PULSE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                float3 positionOS  : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Smoothness;
                half   _Metallic;

                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelIntensity;

                half4  _GlowColor;
                half   _GlowIntensity;
                half   _GlowPower;

                half   _SpecBoost;
                half   _SpecPower;

                half4  _FlowColor;
                half   _FlowIntensity;
                half   _FlowSpeed;
                half   _FlowWidth;
                float4 _FlowDirection;

                half   _PulseSpeed;
                half   _PulseMin;
                half   _PulseMax;

                half4  _EmissionColor;
                half   _EmissionIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS   = normInputs.normalWS;
                output.viewDirWS  = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ---- Base Color ----
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 albedo = baseCol.rgb;

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float NdotV = saturate(dot(N, V));

                // ---- URP Main Light ----
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = mainLight.direction;
                float3 H = normalize(L + V);
                float NdotL = saturate(dot(N, L));
                float NdotH = saturate(dot(N, H));

                // Diffuse (Lambert)
                half3 diffuse = albedo * mainLight.color * NdotL * mainLight.shadowAttenuation;

                // Ambient
                half3 ambient = albedo * half3(0.15, 0.12, 0.08);

                // ---- Enhanced Specular ----
                half spec = pow(NdotH, _SpecPower) * _SpecBoost;
                half3 specular = mainLight.color * spec * mainLight.shadowAttenuation * _Smoothness;

                // ---- Fresnel Rim Light ----
                half3 fresnel = half3(0, 0, 0);
                #ifdef _FRESNEL_ON
                {
                    half rim = 1.0 - NdotV;
                    rim = pow(rim, _FresnelPower);
                    fresnel = _FresnelColor.rgb * rim * _FresnelIntensity;
                }
                #endif

                // ---- Outer Glow (additive, based on fresnel-like falloff) ----
                half3 glow = half3(0, 0, 0);
                #ifdef _GLOW_ON
                {
                    half glowRim = 1.0 - NdotV;
                    glowRim = pow(glowRim, _GlowPower);
                    glow = _GlowColor.rgb * glowRim * _GlowIntensity;
                }
                #endif

                // ---- Surface Flow Light ----
                half3 flow = half3(0, 0, 0);
                #ifdef _FLOW_ON
                {
                    float3 flowDir = normalize(_FlowDirection.xyz);
                    // Project object-space position onto flow direction
                    float projectedPos = dot(input.positionOS, flowDir);

                    // Animated sweep line
                    float sweepPos = frac(_Time.y * _FlowSpeed) * 3.0 - 1.0;
                    // _Time.y * speed creates sweep over range [−1, 2]

                    float dist = abs(projectedPos - sweepPos);
                    float flowMask = 1.0 - saturate(dist / _FlowWidth);
                    flowMask = flowMask * flowMask; // Sharpen

                    // Also add a secondary sweep for more density
                    float sweepPos2 = frac(_Time.y * _FlowSpeed * 0.7 + 0.5) * 3.0 - 1.0;
                    float dist2 = abs(projectedPos - sweepPos2);
                    float flowMask2 = 1.0 - saturate(dist2 / (_FlowWidth * 0.7));
                    flowMask2 = flowMask2 * flowMask2;

                    float combinedFlow = saturate(flowMask + flowMask2 * 0.6);

                    // Flow is more visible at edges (fresnel blend)
                    half edgeFactor = 1.0 - NdotV;
                    edgeFactor = saturate(edgeFactor * 1.5 + 0.3);

                    flow = _FlowColor.rgb * combinedFlow * _FlowIntensity * edgeFactor;
                }
                #endif

                // ---- Pulse Animation ----
                half pulseFactor = 1.0;
                #ifdef _PULSE_ON
                {
                    half pulse = sin(_Time.y * _PulseSpeed * 6.2832) * 0.5 + 0.5;
                    pulseFactor = lerp(_PulseMin, _PulseMax, pulse);
                }
                #endif

                // ---- Emission ----
                half3 emission = _EmissionColor.rgb * _EmissionIntensity;

                // ---- Combine ----
                half3 color = (diffuse + ambient + specular) * pulseFactor;
                color += fresnel;
                color += glow;
                color += flow;
                color += emission;

                // Fog
                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ===================== SHADOW CASTER PASS =====================
        // 使用 URP 内置 ShadowCaster（避免版本兼容性问题）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ===================== DEPTH ONLY PASS =====================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
