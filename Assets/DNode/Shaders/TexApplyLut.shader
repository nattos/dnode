Shader "Hidden/TexApplyLut"
{
    Properties
    {
        _MainTex ("LHS Texture", 2D) = "white" {}
        _LogLut3D ("LUT Cube", 3D) = "white" {}
        _Alpha ("Alpha", float) = 1.0
        _LogLut3D_Params ("_LogLut3D_Params", Vector) = (1.0, 1.0, 1.0, 1.0)
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

            // #include "UnityCG.cginc"
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

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
            sampler3D _LogLut3D;
            // TEXTURE3D(_LogLut3D);
            // SAMPLER(sampler_LogLut3D);
            float _Alpha;
            float2 _LogLut3D_Params;

            fixed4 frag (v2f i) : SV_Target
            {
                float4 lhs = tex2D(_MainTex, i.uv);

                // float3 colorLutSpace = saturate(LinearToLogC(lhs.rgb));
                float3 uvw = lhs;
                float2 scaleOffset = _LogLut3D_Params.xy;
                uvw.xyz = uvw.xyz * scaleOffset.yyy * scaleOffset.xxx + scaleOffset.xxx * 0.5;
                float3 colorLut = tex3D(_LogLut3D, uvw).rgb;
                // float3 colorLut = ApplyLut3D(TEXTURE3D_ARGS(_LogLut3D, sampler_LogLut3D), lhs, _LogLut3D_Params.xy);
                // float3 colorLinear = LogCToLinear(colorLut);

                float4 result = float4(lerp(lhs.rgb, colorLut, _Alpha), lhs.a);
                return result;
            }
            ENDCG
        }
    }
}
