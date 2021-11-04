Shader "Hidden/GenNoise"
{
    Properties
    {
        _Phase ("Phase", Vector) = (0.0, 0.0, 0.0, 0.0)
        _Alpha ("Alpha", Vector) = (0.0, 0.0, 0.0, 1.0)
        _Granularity ("Granularity", Vector) = (1.0, 1.0, 1.0, .0)
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

            float4 _Phase;
            float4 _Alpha;
            float4 _Granularity;

            float rand(float2 uv) {
              return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag(v2f varyings) : SV_Target {
              float2 pos = floor(varyings.uv * _Granularity.xy) * _Granularity.zw;
              float4 rawRand =
                  float4(rand(pos + _Phase.r),
                         rand(pos + _Phase.g),
                         rand(pos + _Phase.b),
                         rand(pos + _Phase.a));
              float4 result = lerp(rawRand, float4(0, 0, 0, 1), _Alpha);
              return result;
            }
            ENDCG
        }
    }
}
