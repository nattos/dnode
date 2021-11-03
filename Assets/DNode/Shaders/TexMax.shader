Shader "Hidden/TexMax"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Value("Value", Float) = 0.0
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
            float _Value;
            int _PreserveAlpha;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lhs = tex2D(_MainTex, i.uv);
                float4 result = max(lhs, _Value);
                if (_PreserveAlpha != 0) {
                  result.a = lhs.a;
                }
                return result;
            }
            ENDCG
        }
    }
}
