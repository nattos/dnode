Shader "Hidden/PolyFillSolid" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
    Tags { "RenderType"="Opaque" }
    Cull Off ZWrite Off ZTest Always
    LOD 100

    Pass {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            float4x4 _Matrix;

            v2f vert (appdata_t v)
            {
                v2f o;
                // o.vertex = mul(unity_MatrixVP, v.vertex);
                o.vertex = mul(_Matrix, v.vertex);
                // o.vertex = v.vertex;
                return o;
            }

            float4 frag (v2f i) : COLOR
            {
                float4 col = _Color;
                return col;
            }
        ENDCG
    }
}
}