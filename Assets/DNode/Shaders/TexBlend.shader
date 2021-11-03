Shader "Hidden/TexBlend"
{
    Properties
    {
        _MainTex ("LHS Texture", 2D) = "white" {}
        _SubTex ("RHS Texture", 2D) = "white" {}
        _Alpha ("Alpha", float) = 0.5
        _PrescaleB ("Prescale RHS", float) = 1.0
        _PrebiasB ("Prebias RHS", float) = 0.0
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
			#pragma multi_compile _local BLEND_MODE_BLEND BLEND_MODE_ADD BLEND_MODE_SUBTRACT BLEND_MODE_MULTIPLY BLEND_MODE_EXPOSURE

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
            float _Alpha;
			float _PrescaleB;
			float _PrebiasB;
            int _PreserveAlpha;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lhs = tex2D(_MainTex, i.uv);
                float4 rhs = tex2D(_SubTex, i.uv);
				float alpha = _Alpha * rhs.a;
				rhs = (rhs + _PrebiasB) * _PrescaleB;
                float4 result = lhs;
              #if BLEND_MODE_BLEND
				result = lerp(lhs, rhs, alpha);
              #endif // BLEND_MODE_BLEND
              #if BLEND_MODE_ADD
				result = lhs + rhs * alpha;
              #endif // BLEND_MODE_ADD
              #if BLEND_MODE_SUBTRACT
				result = lhs - rhs * alpha;
              #endif // BLEND_MODE_BLEND
              #if BLEND_MODE_MULTIPLY
				result = lhs * rhs * alpha;
              #endif // BLEND_MODE_MULTIPLY
              #if BLEND_MODE_EXPOSURE
				result = lhs * pow(2, rhs * alpha);
              #endif // BLEND_MODE_EXPOSURE
                if (_PreserveAlpha != 0) {
                  result.a = lhs.a;
                }
                return result;
            }
            ENDCG
        }
    }
}
