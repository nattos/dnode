Shader "Hidden/TexEdgeDetect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Width ("Width", Vector) = (1.0, 1.0, 0.0, 0.0)
        _XBasis ("XBasis", Vector) = (1.0, 0.0, 0.0, 0.0)
        _YBasis ("YBasis", Vector) = (0.0, 1.0, 0.0, 0.0)
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
            float2 _Width;
            float2 _XBasis;
			float2 _YBasis;
            float4 _MainTex_TexelSize;
            int _PreserveAlpha;

            fixed4 frag (v2f i) : SV_Target
            {
                float2 texelSize = float2(_MainTex_TexelSize.x * _MainTex_TexelSize.w * _Width.x * 0.5, _Width.y * 0.5);
                float4 x0y0 = tex2D(_MainTex, i.uv - float2(-texelSize.x, -texelSize.y));
                float4 x1y0 = tex2D(_MainTex, i.uv - float2(0, -texelSize.y));
                float4 x2y0 = tex2D(_MainTex, i.uv - float2(texelSize.x, -texelSize.y));
                float4 x0y2 = tex2D(_MainTex, i.uv - float2(-texelSize.x, texelSize.y));
                float4 x1y2 = tex2D(_MainTex, i.uv - float2(0, texelSize.y));
                float4 x2y2 = tex2D(_MainTex, i.uv - float2(texelSize.x, texelSize.y));
                float4 x0y1 = tex2D(_MainTex, i.uv - float2(-texelSize.x, 0.0));
                float4 x2y1 = tex2D(_MainTex, i.uv - float2(texelSize.x, 0.0));

                float4 xGrad = -x0y0 - 2.0 * x0y1 - x0y2 + x2y0 + 2.0 * x2y1 + x2y2;
                float4 yGrad = -x0y0 - 2.0 * x1y0 - x2y0 + x0y2 + 2.0 * x1y2 + x2y2;

                float2 rGrad = float2(xGrad.r, yGrad.r);
                float2 gGrad = float2(xGrad.g, yGrad.g);
                float2 bGrad = float2(xGrad.b, yGrad.b);
                float2 aGrad = float2(xGrad.a, yGrad.a);

                float4 xGradRot = float4(dot(_XBasis, rGrad), dot(_XBasis, gGrad), dot(_XBasis, bGrad), dot(_XBasis, aGrad));
                float4 yGradRot = float4(dot(_YBasis, rGrad), dot(_YBasis, gGrad), dot(_YBasis, bGrad), dot(_YBasis, aGrad));

                float4 result = sqrt(xGradRot * xGradRot + yGradRot * yGradRot);
                if (_PreserveAlpha != 0) {
                  result.a = tex2D(_MainTex, i.uv).a;
                }
                return result;
            }
            ENDCG
        }
    }
}
