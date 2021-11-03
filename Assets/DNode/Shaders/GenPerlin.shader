Shader "Hidden/GenPerlin"
{
    Properties
    {
        _Octaves ("Octaves", Int) = 4
        _Gain ("Gain", Float) = 1.0
        _Scale ("Scale", Float) = 8
        _Frequency ("Frequency", Float) = 3.0
        _Amplitude ("Amplitude", Float) = 0.9
        _Stretch ("Stretch", Vector) = (1.0, 1.0, 0.0, 0.0)
        _Phase ("Phase", Vector) = (0.0, 0.0, 0.0, 0.0)
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

            int _Octaves;
            float _Gain;
            float _Scale;
            float _Frequency;
            float _Amplitude;
            float2 _Stretch;
            float3 _Phase;

            float4 frag(v2f varyings) : SV_Target {
              float acc = 0;
              float alpha = 1.0f;
			  float scale = _Scale;
              for (int i = 0; i < _Octaves; ++i) {
                acc += perlinNoise(float3(varyings.uv * scale * _Stretch, 0) - _Phase) * alpha;
                scale *= _Frequency;
                alpha *= _Amplitude;
              }
              return ((acc - 0.5) * _Gain) + 0.5;
            }
            ENDCG
        }
    }
}
