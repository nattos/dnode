Shader "Hidden/TexSwizzle2"
{
    Properties
    {
        _MainTex ("LHS Texture", 2D) = "white" {}
        _SubTex ("RHS Texture", 2D) = "white" {}
        _XBasisA ("XBasisA", Vector) = (1.0, 0.0, 0.0, 0.0)
        _YBasisA ("YBasisA", Vector) = (0.0, 1.0, 0.0, 0.0)
        _ZBasisA ("ZBasisA", Vector) = (0.0, 0.0, 1.0, 0.0)
        _WBasisA ("WBasisA", Vector) = (0.0, 0.0, 0.0, 1.0)
        _XBasisB ("XBasisB", Vector) = (1.0, 0.0, 0.0, 0.0)
        _YBasisB ("YBasisB", Vector) = (0.0, 1.0, 0.0, 0.0)
        _ZBasisB ("ZBasisB", Vector) = (0.0, 0.0, 1.0, 0.0)
        _WBasisB ("WBasisB", Vector) = (0.0, 0.0, 0.0, 1.0)
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
            float4 _XBasisA;
            float4 _YBasisA;
            float4 _ZBasisA;
            float4 _WBasisA;
            float4 _XBasisB;
            float4 _YBasisB;
            float4 _ZBasisB;
            float4 _WBasisB;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lhs = tex2D(_MainTex, i.uv);
                float4 rhs = tex2D(_SubTex, i.uv);
                float4 result =
                    float4(dot(lhs, _XBasisA), dot(lhs, _YBasisA), dot(lhs, _ZBasisA), dot(lhs, _WBasisA)) +
                    float4(dot(rhs, _XBasisB), dot(rhs, _YBasisB), dot(rhs, _ZBasisB), dot(rhs, _WBasisB));
                return result;
            }
            ENDCG
        }
    }
}
