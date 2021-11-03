Shader "Hidden/GenSolidColor"
{
    Properties
    {
        _Color ("Color", Vector) = (1.0, 1.0, 1.0, 1.0)
        _Gain ("Gain", Float) = 1.0
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

            float4 _Color;
            float _Gain;

            float4 frag(v2f varyings) : SV_Target {
              return _Color * _Gain;
            }
            ENDCG
        }
    }
}
