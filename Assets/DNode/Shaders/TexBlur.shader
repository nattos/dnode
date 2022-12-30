Shader "Hidden/TexBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Strength", Float) = 0.0
        _TargetStepSize ("TargetStepSize", Float) = 32.0
        _Axis ("Axis", Float) = 0.0
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
            float _Strength;
            float _TargetStepSize;
            float _Axis;

            fixed4 frag (v2f varyings) : SV_Target
            {
                const float2 texelSize = float2(_ScreenParams.z - 1.0, _ScreenParams.w - 1.0);
                const int weightCount = 4;
                const float rawWeight0 = 1.0f;
                const float rawWeight1 = 0.9f;
                const float rawWeight2 = 0.55f;
                const float rawWeight3 = 0.18f;
                const float rawWeight4 = 0.1f;
                const float normalization = 1.0 / (2.0 * (rawWeight1 + rawWeight2 + rawWeight3 + rawWeight4));
                const float weight0 = rawWeight0 * normalization;
                const float weights[weightCount] = {
                    rawWeight1 * normalization,
                    rawWeight2 * normalization,
                    rawWeight3 * normalization,
                    rawWeight4 * normalization,
                };

                float2 center = varyings.uv;

                bool isY = _Axis > 0.0;
                float firstWeightStep = rawWeight2 / rawWeight1;

                float width = (isY ? texelSize.y : texelSize.x) * _ScreenParams.y * _Strength;
                float screenWidth = isY ? _ScreenParams.y : _ScreenParams.x;
                float rawStages = max(1.0, min(32.0, screenWidth / (width * _TargetStepSize)));
                float coarseStages = floor(rawStages);
                float fineStages = rawStages - coarseStages;
                int stages = (int)coarseStages;
                float stageOffset = 1.0 / stages;
                float step = width / rawStages;

                int stage = 0;
                float4 acc = 0.0;
                float totalWeight = 0;
                for (int stage = 0; stage < stages; ++stage) {
                  float weightAdjust = lerp(1.0, firstWeightStep, stage / (float)stages);
                  if (stage == stages - 1) {
                    weightAdjust *= fineStages;
                  }

                  float offset;
                  offset = step * stageOffset * (stage + 0.5);
                  float4 stageAcc = 0;
                  [unroll]
                  for (int i = 0; i < weightCount; ++i) {
                    float2 uv = center;
                    if (isY) {
                      uv.y += offset;
                    } else {
                      uv.x += offset;
                    }
                    stageAcc += tex2D(_MainTex, uv) * weights[i];
                    offset += step;
                  }
                  offset = step * -stageOffset * (stage + 0.5);
                  [unroll]
                  for (int i = 0; i < weightCount; ++i) {
                    float2 uv = center;
                    if (isY) {
                      uv.y += offset;
                    } else {
                      uv.x += offset;
                    }
                    stageAcc += tex2D(_MainTex, uv) * weights[i];
                    offset -= step;
                  }
                  acc += stageAcc * weightAdjust;
                  totalWeight += weightAdjust;
                }
                acc /= totalWeight;

                return acc;
            }
            ENDCG
        }
    }
}
