Shader "Hidden/TexBrightContrast"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Float) = 0
        _Contrast ("Contrast", Float) = 0
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
            float _Brightness;
            float _Contrast;
			float _PreserveAlpha;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                float4 result = (col - 0.5) * (_Contrast + 1.0) + 0.5 + _Brightness;
                if (_PreserveAlpha != 0) {
                  result.a = col.a;
                }
                return result;
            }
            ENDCG
        }
    }
}
