Shader "CapybaraDuel/CapybaraUnit"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1.0
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3

        [Header(Camp Fresnel Rim)]
        [Toggle(_CAMP_FRESNEL_ON)] _CampFresnelOn ("Enable Camp Fresnel", Float) = 1
        _CampColor ("Camp Color", Color) = (1, 0.55, 0, 1)
        _CampFresnelPower ("Fresnel Power", Range(1, 8)) = 3.0
        _CampFresnelIntensity ("Fresnel Intensity", Range(0, 3)) = 1.2
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

            #pragma shader_feature_local _CAMP_FRESNEL_ON
            #pragma shader_feature_local _NORMALMAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 viewDirWS   : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float4 _NormalMap_ST;
                half   _NormalScale;
                half   _Metallic;
                half   _Smoothness;

                half4  _CampColor;
                half   _CampFresnelPower;
                half   _CampFresnelIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS  = posInputs.positionCS;
                output.positionWS  = posInputs.positionWS;
                output.normalWS    = normInputs.normalWS;
                output.tangentWS   = normInputs.tangentWS;
                output.bitangentWS = normInputs.bitangentWS;
                output.viewDirWS   = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ---- Base Color ----
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 albedo = baseCol.rgb;

                // ---- Normal Map ----
                float3 normalWS;
                #ifdef _NORMALMAP
                {
                    float3 N = normalize(input.normalWS);
                    float3 T = normalize(input.tangentWS);
                    float3 B = normalize(input.bitangentWS);
                    float3x3 TBN = float3x3(T, B, N);

                    half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                    half3 normalTS = UnpackNormalScale(normalSample, _NormalScale);
                    normalWS = normalize(mul(normalTS, TBN));
                }
                #else
                    normalWS = normalize(input.normalWS);
                #endif

                float3 V = normalize(input.viewDirWS);

                // ---- URP Main Light ----
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = mainLight.direction;
                float3 H = normalize(L + V);
                float NdotL = saturate(dot(normalWS, L));
                float NdotH = saturate(dot(normalWS, H));

                // Diffuse (Lambert)
                half3 diffuse = albedo * mainLight.color * NdotL * mainLight.shadowAttenuation;

                // Specular (Blinn-Phong)
                half specPower = _Smoothness * 256.0;
                half spec = pow(NdotH, specPower) * (1.0 - _Metallic);
                half3 specular = mainLight.color * spec * mainLight.shadowAttenuation;

                // Ambient (SH)
                half3 ambient = SampleSH(normalWS) * albedo;

                // ---- Camp Fresnel Rim ----
                half3 fresnel = half3(0, 0, 0);
                #ifdef _CAMP_FRESNEL_ON
                {
                    half rim = pow(1.0 - saturate(dot(normalWS, V)), _CampFresnelPower);
                    half3 fresnelColor = _CampColor.rgb * rim * _CampFresnelIntensity;
                    fresnel = fresnelColor;
                }
                #endif

                // ---- Combine ----
                half3 color = diffuse + ambient + specular + fresnel;

                // Fog
                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ===================== SHADOW CASTER PASS =====================
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
