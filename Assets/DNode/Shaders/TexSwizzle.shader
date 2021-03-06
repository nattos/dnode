Shader "Hidden/TexSwizzle"
{
    Properties
    {
        _MainTex ("LHS Texture", 2D) = "white" {}
        _XBasis ("XBasis", Vector) = (1.0, 0.0, 0.0, 0.0)
        _YBasis ("YBasis", Vector) = (0.0, 1.0, 0.0, 0.0)
        _ZBasis ("ZBasis", Vector) = (0.0, 0.0, 1.0, 0.0)
        _WBasis ("WBasis", Vector) = (0.0, 0.0, 0.0, 1.0)
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
            float4 _XBasis;
            float4 _YBasis;
            float4 _ZBasis;
            float4 _WBasis;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lhs = tex2D(_MainTex, i.uv);
                float4 result = float4(dot(lhs, _XBasis), dot(lhs, _YBasis), dot(lhs, _ZBasis), dot(lhs, _WBasis));
                return result;
            }
            ENDCG
        }
    }
}
