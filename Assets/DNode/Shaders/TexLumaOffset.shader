Shader "Hidden/TexLumaOffset"
{
    Properties
    {
        _MainTex ("LHS Texture", 2D) = "white" {}
        _SubTex ("RHS Texture", 2D) = "white" {}
        _ColorBasis ("ColorBasis", Vector) = (0.33333, 0.33333, 0.33333, 0.0)
        _BaseValue ("BaseValue", Float) = 0.0
        _Shift ("Shift", Vector) = (0.0, 0.0, 0.0, 0.0)
        _ColorBasis2 ("ColorBasis2", Vector) = (0.33333, 0.33333, 0.33333, 0.0)
        _BaseValue2 ("BaseValue2", Float) = 0.0
        _Shift2 ("Shift2", Vector) = (0.0, 0.0, 0.0, 0.0)
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
            sampler2D _SubTex;
            float4 _ColorBasis;
            float _BaseValue;
            float2 _Shift;
            float4 _ColorBasis2;
            float _BaseValue2;
            float2 _Shift2;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 rhs = tex2D(_SubTex, i.uv);
                float strength = (dot(rhs.rgb, _ColorBasis.rgb) - _BaseValue) * rhs.a;
                float2 shift = strength * _Shift;
                float strength2 = (dot(rhs.rgb, _ColorBasis2.rgb) - _BaseValue2) * rhs.a;
                float2 shift2 = strength2 * _Shift2;

                float2 offset = shift + shift2;
                offset.x *= _ScreenParams.y * (_ScreenParams.z - 1.0);
                float4 lhs = tex2D(_MainTex, i.uv - offset);
                return lhs;
            }
            ENDCG
        }
    }
}
