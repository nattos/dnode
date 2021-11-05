Shader "Hidden/TexWhiteBalance"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorBalance ("ColorBalance", Vector) = (1.0, 1.0, 1.0, 0.0)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Common.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float3 _ColorBalance;

            // Converts linear RGB to LMS
            // Full float precision to avoid precision artefact when using ACES tonemapping
            float3 LinearToLMS(float3 x)
            {
                const float3x3 LIN_2_LMS_MAT = {
                    3.90405e-1, 5.49941e-1, 8.92632e-3,
                    7.08416e-2, 9.63172e-1, 1.35775e-3,
                    2.31082e-2, 1.28021e-1, 9.36245e-1
                };

                return mul(LIN_2_LMS_MAT, x);
            }

            // Full float precision to avoid precision artefact when using ACES tonemapping
            float3 LMSToLinear(float3 x)
            {
                const float3x3 LMS_2_LIN_MAT = {
                    2.85847e+0, -1.62879e+0, -2.48910e-2,
                    -2.10182e-1,  1.15820e+0,  3.24281e-4,
                    -4.18120e-2, -1.18169e-1,  1.06867e+0
                };

                return mul(LMS_2_LIN_MAT, x);
            }

            float4 frag(v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);
                float3 colorLMS = LinearToLMS(col.rgb);
                colorLMS *= _ColorBalance;
                col.rgb = LMSToLinear(colorLMS);
                return col;
            }
            ENDCG
        }
    }
}
