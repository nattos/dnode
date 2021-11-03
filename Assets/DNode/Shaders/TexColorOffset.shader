Shader "Hidden/TexColorOffset"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorBasisR ("ColorBasisR", Vector) = (0.0, 0.0, 0.0, 0.0)
        _ColorBasisG ("ColorBasisG", Vector) = (0.0, 0.0, 0.0, 0.0)
        _ColorBasisB ("ColorBasisB", Vector) = (0.0, 0.0, 0.0, 0.0)
        _ShiftR ("ShiftR", Vector) = (0.0, 0.0, 0.0, 0.0)
        _ShiftG ("ShiftG", Vector) = (0.0, 0.0, 0.0, 0.0)
        _ShiftB ("ShiftB", Vector) = (0.0, 0.0, 0.0, 0.0)
        _AlphaR ("AlphaR", Float) = 1.0
        _AlphaG ("AlphaG", Float) = 1.0
        _AlphaB ("AlphaB", Float) = 1.0
        _HueShift ("HueShift", Float) = 0.0
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
            float4 _ColorBasisR;
            float4 _ColorBasisG;
            float4 _ColorBasisB;
            float2 _ShiftR;
            float2 _ShiftG;
            float2 _ShiftB;
            float _AlphaR;
            float _AlphaG;
            float _AlphaB;
			float _HueShift;

            float4 frag(v2f i) : SV_Target {
                float hueShift = _HueShift;
                float3 colorBasesR = _ColorBasisR;
                float3 colorBasesG = _ColorBasisG;
                float3 colorBasesB = _ColorBasisB;

                float2 shiftR = _ShiftR;
                float2 shiftG = _ShiftG;
                float2 shiftB = _ShiftB;
                shiftR.x *= _ScreenParams.y * (_ScreenParams.z - 1.0);
                shiftG.x *= _ScreenParams.y * (_ScreenParams.z - 1.0);
                shiftB.x *= _ScreenParams.y * (_ScreenParams.z - 1.0);

                float4 colR = tex2D(_MainTex, i.uv - shiftR);
                float4 colG = tex2D(_MainTex, i.uv - shiftG);
                float4 colB = tex2D(_MainTex, i.uv - shiftB);
                colR.rgb = ShiftHue(colR.rgb, hueShift);
                colG.rgb = ShiftHue(colG.rgb, hueShift);
                colB.rgb = ShiftHue(colB.rgb, hueShift);
                float4 outR = float4(dot(colR.rgb, colorBasesR.rgb) * colorBasesR.rgb, _ColorBasisR.a * _AlphaR);
                float4 outG = float4(dot(colG.rgb, colorBasesG.rgb) * colorBasesG.rgb, _ColorBasisG.a * _AlphaG);
                float4 outB = float4(dot(colB.rgb, colorBasesB.rgb) * colorBasesB.rgb, _ColorBasisB.a * _AlphaB);
                outR.rgb = ShiftHue(outR.rgb, -hueShift);
                outG.rgb = ShiftHue(outG.rgb, -hueShift);
                outB.rgb = ShiftHue(outB.rgb, -hueShift);
                float4 result = float4(outR.rgb * outR.a + outG.rgb * outG.a + outB.rgb * outB.a, (outR.a + outG.a + outB.a) / 3);
                return result;
            }
            ENDCG
        }
    }
}
