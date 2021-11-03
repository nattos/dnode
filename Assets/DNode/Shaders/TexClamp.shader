Shader "Hidden/TexClamp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Min("Min", Vector) = (0.0, 0.0, 0.0, 0.0)
        _Max("Max", Vector) = (1.0, 1.0, 1.0, 1.0)
        _PreserveAlpha ("Preserve Alpha", Int) = 1
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
            float4 _Min;
            float4 _Max;
            int _PreserveAlpha;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lhs = tex2D(_MainTex, i.uv);
                float4 minValue = _Min;
                float4 maxValue = max(_Min, _Max);
                float4 result = max(min(lhs, maxValue), minValue);
                if (_PreserveAlpha != 0) {
                  result.a = lhs.a;
                }
                return result;
            }
            ENDCG
        }
    }
}
