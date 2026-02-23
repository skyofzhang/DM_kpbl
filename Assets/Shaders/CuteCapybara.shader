Shader "CapybaraDuel/CuteCapybara"
{
    Properties
    {
        // --- Base ---
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0, 2)) = 1.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _Metallic("Metallic", Range(0, 1)) = 0.0

        [Header(Toon Lighting)]
        _CelShadeMidPoint("Shadow Mid Point", Range(-1, 1)) = 0.15
        _CelShadeSoftness("Shadow Softness", Range(0.001, 1)) = 0.1
        _ShadowColor("Shadow Tint", Color) = (0.75, 0.65, 0.85, 1)

        [Header(Specular)]
        _SpecColor2("Specular Color", Color) = (1,1,1,1)
        _Glossiness("Glossiness", Range(1, 256)) = 32
        _SpecSmoothMin("Spec Smooth Min", Range(0, 0.5)) = 0.005
        _SpecSmoothMax("Spec Smooth Max", Range(0, 0.5)) = 0.02

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1, 0.85, 0.65, 1)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimSmoothness("Rim Smoothness", Range(0, 1)) = 0.4

        [Header(Fuzzy Edge)]
        _FuzzyEdgeColor("Fuzzy Edge Color", Color) = (0.95, 0.9, 0.8, 1)
        _FuzzyPower("Fuzzy Power", Range(1, 8)) = 2.5
        _FuzzyIntensity("Fuzzy Intensity", Range(0, 1)) = 0.25
        _FuzzyNoiseScale("Fuzzy Noise Scale", Range(1, 80)) = 25.0

        [Header(Fake SSS)]
        _SSSColor("SSS Color", Color) = (1, 0.4, 0.3, 1)
        _SSSDistortion("SSS Distortion", Range(0, 1)) = 0.4
        _SSSPower("SSS Power", Range(1, 16)) = 4.0
        _SSSScale("SSS Scale", Range(0, 2)) = 0.4

        [Header(Outline)]
        _OutlineWidth("Outline Width", Range(0, 5)) = 1.5
        _OutlineColor("Outline Color", Color) = (0.25, 0.18, 0.12, 1)

        [Header(Emission  Advanced)]
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionFlickerSpeed("Flicker Speed", Range(0, 10)) = 0.0
        _EmissionFlickerMin("Flicker Min Brightness", Range(0, 1)) = 0.3
        _EmissionThreshold("Brightness Threshold", Range(0, 1)) = 0.0
        _EmissionUseTexColor("Use Texture Color (0=EmissionColor, 1=Texture)", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        // CBUFFER — 所有 pass 共享，SRP Batcher 兼容
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            float  _BumpScale;
            half   _Smoothness;
            half   _Metallic;
            // Toon
            half   _CelShadeMidPoint;
            half   _CelShadeSoftness;
            half4  _ShadowColor;
            // Specular
            half4  _SpecColor2;
            half   _Glossiness;
            half   _SpecSmoothMin;
            half   _SpecSmoothMax;
            // Rim
            half4  _RimColor;
            half   _RimPower;
            half   _RimSmoothness;
            // Fuzzy
            half4  _FuzzyEdgeColor;
            half   _FuzzyPower;
            half   _FuzzyIntensity;
            half   _FuzzyNoiseScale;
            // SSS
            half4  _SSSColor;
            half   _SSSDistortion;
            half   _SSSPower;
            half   _SSSScale;
            // Outline
            float  _OutlineWidth;
            half4  _OutlineColor;
            // Emission
            half4  _EmissionColor;
            half   _EmissionFlickerSpeed;
            half   _EmissionFlickerMin;
            half   _EmissionThreshold;
            half   _EmissionUseTexColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
        ENDHLSL

        // ==================== Pass 0: ForwardLit ====================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment LitFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                float  fogFactor    : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                o.positionCS  = vpi.positionCS;
                o.positionWS  = vpi.positionWS;
                o.normalWS    = vni.normalWS;
                o.tangentWS   = vni.tangentWS;
                o.bitangentWS = vni.bitangentWS;
                o.uv          = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor   = ComputeFogFactor(vpi.positionCS.z);
                return o;
            }

            // --- 柔和卡通光照 ---
            half3 ToonDiffuse(half3 normalWS, half3 lightDir, half3 lightColor, half shadowAtten)
            {
                half NdotL = dot(normalWS, lightDir);
                half toonNdotL = NdotL * 0.5 + 0.5;  // remap to 0..1
                half lit = smoothstep(
                    _CelShadeMidPoint - _CelShadeSoftness,
                    _CelShadeMidPoint + _CelShadeSoftness,
                    toonNdotL * shadowAtten);
                return lerp(_ShadowColor.rgb, half3(1,1,1), lit) * lightColor;
            }

            // --- 卡通高光 ---
            half3 ToonSpecular(half3 normalWS, half3 viewDir, half3 lightDir, half atten)
            {
                half3 h = normalize(lightDir + viewDir);
                half NdotH = saturate(dot(normalWS, h));
                half spec = pow(NdotH, _Glossiness * _Glossiness);
                spec *= atten;
                half toonSpec = smoothstep(_SpecSmoothMin, _SpecSmoothMax, spec);
                return toonSpec * _SpecColor2.rgb;
            }

            // --- 边缘光 ---
            half3 RimLight(half3 normalWS, half3 viewDir, half3 lightDir, half atten)
            {
                half NdotV = saturate(dot(normalWS, viewDir));
                half rim = pow(1.0 - NdotV, _RimPower);
                half NdotL = saturate(dot(normalWS, lightDir));
                half rimMask = pow(NdotL, 0.3); // 只在受光面显示
                half rimI = smoothstep(1.0 - _RimSmoothness, 1.0, rim * rimMask);
                return rimI * _RimColor.rgb * atten;
            }

            // --- 毛绒边缘 ---
            half3 FuzzyEdge(half3 normalWS, half3 viewDir, float2 uv)
            {
                half NdotV = saturate(dot(normalWS, viewDir));
                half fresnel = pow(1.0 - NdotV, _FuzzyPower);
                // 噪声打散均匀边缘，模拟毛发
                half noise = frac(sin(dot(uv * _FuzzyNoiseScale,
                    float2(12.9898, 78.233))) * 43758.5453);
                half fuzzy = fresnel * lerp(0.6, 1.4, noise);
                return _FuzzyEdgeColor.rgb * saturate(fuzzy) * _FuzzyIntensity;
            }

            // --- 假次表面散射 ---
            half3 FakeSSS(half3 normalWS, half3 viewDir, half3 lightDir, half3 lightColor)
            {
                half3 sssDir = normalize(lightDir + normalWS * _SSSDistortion);
                half VdotSSS = saturate(dot(viewDir, -sssDir));
                half sss = pow(VdotSSS, _SSSPower) * _SSSScale;
                return sss * _SSSColor.rgb * lightColor;
            }

            half4 LitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // 采样
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                baseColor *= _BaseColor;

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS, input.bitangentWS, input.normalWS)));

                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 主光源
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // 合成
                half3 diffuse  = ToonDiffuse(normalWS, mainLight.direction, mainLight.color, atten);
                half3 specular = ToonSpecular(normalWS, viewDir, mainLight.direction, atten);
                half3 rim      = RimLight(normalWS, viewDir, mainLight.direction, atten);
                half3 fuzzy    = FuzzyEdge(normalWS, viewDir, input.uv);
                half3 sss      = FakeSSS(normalWS, viewDir, mainLight.direction, mainLight.color);
                half3 ambient  = SampleSH(normalWS) * baseColor.rgb * 0.3;

                // === Advanced Emission ===
                // 闪烁：sin波 在 [FlickerMin, 1.0] 范围内脉动
                half flickerMult = 1.0;
                if (_EmissionFlickerSpeed > 0.001)
                {
                    half wave = sin(_Time.y * _EmissionFlickerSpeed * 6.2832) * 0.5 + 0.5; // 0~1
                    flickerMult = lerp(_EmissionFlickerMin, 1.0, wave);
                }

                // 亮度阈值：只有贴图亮度超过阈值的区域才受自发光影响
                half texLuminance = dot(baseColor.rgb, half3(0.299, 0.587, 0.114));
                half thresholdMask = smoothstep(_EmissionThreshold, _EmissionThreshold + 0.1, texLuminance);

                // 自发光颜色：可选择使用贴图颜色或指定颜色
                half3 emissionTint = lerp(_EmissionColor.rgb, baseColor.rgb * _EmissionColor.a, _EmissionUseTexColor);

                // 最终自发光 = 颜色 × 闪烁 × 阈值遮罩
                half3 finalEmission = emissionTint * flickerMult * thresholdMask;

                half3 color = baseColor.rgb * diffuse
                            + specular
                            + rim
                            + fuzzy
                            + sss
                            + ambient
                            + finalEmission;

                // 附加光源
                #ifdef _ADDITIONAL_LIGHTS
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    half addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    color += baseColor.rgb * addLight.color * addNdotL * addAtten * 0.5;
                }
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ==================== Pass 1: Outline ====================
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(input.normalOS);

                float width = _OutlineWidth * 0.001;
                float3 posWS = vpi.positionWS + vni.normalWS * width;
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                // 描边颜色 = 基础贴图暗化 × 描边色
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 outColor = base.rgb * _OutlineColor.rgb;
                outColor = MixFog(outColor, input.fogFactor);
                return half4(outColor, 1);
            }
            ENDHLSL
        }

        // ==================== Pass 2: ShadowCaster ====================
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ==================== Pass 3: DepthOnly ====================
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
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
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
