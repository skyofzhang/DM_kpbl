Shader "UI/CircleMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BorderColor ("Border Color", Color) = (1,0.84,0,1)
        _BorderWidth ("Border Width", Range(0, 0.15)) = 0.06
        _Softness ("Edge Softness", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _Color;
            float4 _BorderColor;
            float _BorderWidth;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Distance from center (0,0 to 1,1 UV space, center at 0.5,0.5)
                float2 center = float2(0.5, 0.5);
                float dist = distance(i.uv, center);

                float radius = 0.5;
                float innerRadius = radius - _BorderWidth;

                // Sample texture
                fixed4 texCol = tex2D(_MainTex, i.uv) * _Color * i.color;

                // Circle mask with soft edge
                float circleMask = 1.0 - smoothstep(radius - _Softness, radius, dist);

                // Border ring
                float borderMask = smoothstep(innerRadius - _Softness, innerRadius, dist)
                                 * (1.0 - smoothstep(radius - _Softness, radius, dist));

                // Combine: texture inside, border color on ring
                fixed4 finalColor = lerp(texCol, _BorderColor, borderMask);
                finalColor.a *= circleMask;

                return finalColor;
            }
            ENDCG
        }
    }
}
