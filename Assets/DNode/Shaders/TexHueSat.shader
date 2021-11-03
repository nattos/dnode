Shader "Hidden/TexHueSat"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _HSL ("HSL", Vector) = (0.0, 1.0, 1.0, 0.0)
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

            sampler2D _MainTex;
            float3 _HSL;

            float4 frag(v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb = ShiftHsl(col.rgb, float3(_HSL));
                return col;
            }
            ENDCG
        }
    }
}
