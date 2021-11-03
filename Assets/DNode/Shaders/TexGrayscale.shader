Shader "Hidden/TexGrayscale"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorBasis ("Vector", Vector) = (0.33333, 0.33333, 0.33333, 0.0)
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
			float4 _ColorBasis;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb = dot(col.rgb, _ColorBasis.rgb);
                return col;
            }
            ENDCG
        }
    }
}
