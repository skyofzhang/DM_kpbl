Shader "UI/ProgressBarFX"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 渐变色
        _GradientColorA ("Gradient Start", Color) = (1, 0.4, 0, 1)
        _GradientColorB ("Gradient End", Color) = (1, 0.7, 0.2, 1)
        _GradientDir ("Gradient Direction", Range(0, 1)) = 0  // 0=horizontal, 1=vertical

        // 扫光效果
        _SweepColor ("Sweep Color", Color) = (1, 1, 1, 0.4)
        _SweepWidth ("Sweep Width", Range(0.02, 0.3)) = 0.1
        _SweepSpeed ("Sweep Speed", Range(0.2, 3.0)) = 1.0
        _SweepDir ("Sweep Direction", Range(-1, 1)) = 1  // 1=向右, -1=向左

        // 边缘描边/发光
        _EdgeGlow ("Edge Glow Intensity", Range(0, 2)) = 0.8
        _EdgeWidth ("Edge Width", Range(0.01, 0.15)) = 0.04
        _EdgeColor ("Edge Glow Color", Color) = (1, 1, 1, 0.6)

        // 脉冲效果（拉扯感）
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 0.3)) = 0.1

        // Unity UI 必需
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _GradientColorA;
            fixed4 _GradientColorB;
            float _GradientDir;

            fixed4 _SweepColor;
            float _SweepWidth;
            float _SweepSpeed;
            float _SweepDir;

            float _EdgeGlow;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            float _PulseSpeed;
            float _PulseIntensity;

            float4 _ClipRect;
            float4 _MainTex_ST;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 基础纹理
                half4 texColor = tex2D(_MainTex, i.texcoord);

                // === 渐变色 ===
                float gradT = lerp(i.texcoord.x, i.texcoord.y, _GradientDir);
                fixed4 gradColor = lerp(_GradientColorA, _GradientColorB, gradT);
                fixed4 col = texColor * gradColor * i.color;

                // === 脉冲（微弱亮度波动，增加拉扯感） ===
                float pulse = 1.0 + _PulseIntensity * sin(_Time.y * _PulseSpeed);
                col.rgb *= pulse;

                // === 扫光效果 ===
                // 扫光位置在UV.x上循环滑动
                float sweepPos = frac(_Time.y * _SweepSpeed * 0.5);
                if (_SweepDir < 0) sweepPos = 1.0 - sweepPos;
                float sweepDist = abs(i.texcoord.x - sweepPos);
                float sweepMask = smoothstep(_SweepWidth, 0.0, sweepDist);
                col.rgb += _SweepColor.rgb * _SweepColor.a * sweepMask;

                // === 边缘描边/发光 ===
                float edgeX = min(i.texcoord.x, 1.0 - i.texcoord.x);
                float edgeY = min(i.texcoord.y, 1.0 - i.texcoord.y);
                float edgeDist = min(edgeX, edgeY);
                float edgeMask = smoothstep(_EdgeWidth, _EdgeWidth * 0.3, edgeDist);
                col.rgb += _EdgeColor.rgb * _EdgeColor.a * edgeMask * _EdgeGlow;

                // UI裁剪
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                // 丢弃全透明像素
                clip(col.a - 0.001);

                return col;
            }
            ENDCG
        }
    }
}
