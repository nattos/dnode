Shader "Hidden/GenDotScreen"
{
    Properties
    {
        _DotParams ("_DotParams", Vector) = (1.0, 1.0, 1.0, 1.0)
        _ShapeParams ("_ShapeParams", Vector) = (0.0, 0.0, 0.0, 0.0)
        _PositionParams ("_PositionParams", Vector) = (0.0, 0.0, 1.0, 0.0)
        _PositionParams ("_LimitParams", Vector) = (10000.0, 10000.0, 10000.0, 10000.0)
        _BaseColorHSL ("_BaseColorHSL", Vector) = (1.0, 1.0, 1.0, 1.0)
        _ColorStepHSL ("_ColorStepHSL", Vector) = (0.0, 0.0, 0.0, 0.0)
        _BackgroundColor ("_BackgroundColor", Vector) = (0.0, 0.0, 0.0, 1.0)
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
			#pragma multi_compile _local SHAPE_CIRCLE SHAPE_CIRCLE_SQUIRCLE SHAPE_SQUIRCLE SHAPE_SQUIRCLE_SQUARE SHAPE_SQUARE
            #if defined(SHAPE_CIRCLE)
              #define SHAPE_HAS_CIRCLE
            #endif
            #if defined(SHAPE_CIRCLE_SQUIRCLE)
              #define SHAPE_HAS_CIRCLE
              #define SHAPE_HAS_SQUIRCLE
            #endif
            #if defined(SHAPE_SQUIRCLE)
              #define SHAPE_HAS_SQUIRCLE
            #endif
            #if defined(SHAPE_SQUIRCLE_SQUARE)
              #define SHAPE_HAS_SQUIRCLE
              #define SHAPE_HAS_SQUARE
            #endif
            #if defined(SHAPE_SQUARE)
              #define SHAPE_HAS_SQUARE
            #endif

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

            float4 _DotParams;
            float4 _ShapeParams;
            float4 _PositionParams;
            float4 _LimitParams;
            float4 _BaseColorHSL;
            float4 _ColorStepHSL;
            float4 _BackgroundColor;

            float4 frag(v2f varyings) : SV_Target {
			  float2 scaleFactor = float2(_ScreenParams.x * (_ScreenParams.w - 1.0), 1.0);
		      float2 center = _PositionParams.xy;
		      float2 xBasis = _PositionParams.zw;
		      float2 yBasis = float2(-xBasis.y, xBasis.x);
			  float cellSize = _DotParams.x;
			  float dotSize = _DotParams.y;
			  float innerSteps = _DotParams.z;
			  float hardness = _DotParams.w;

			  float2 pos = varyings.uv * scaleFactor - center * scaleFactor;
			  float2 rotatedPos = pos.x * xBasis + pos.y * yBasis;
			  float2 wrappedPos = rotatedPos * cellSize + 0.5;
			  wrappedPos -= max(_LimitParams.zw, min(_LimitParams.xy, floor(wrappedPos))) + 0.5;

            #if defined(SHAPE_HAS_SQUIRCLE)
              float squircleShapePower = 8.0;
			  float squircleDist = pow(pow(wrappedPos.x, squircleShapePower) + pow(wrappedPos.y, squircleShapePower), 1 / squircleShapePower);
            #endif
            #if defined(SHAPE_HAS_CIRCLE)
			  float circularDist = length(wrappedPos);
            #endif
            #if defined(SHAPE_HAS_SQUARE)
			  float rectDist = max(abs(wrappedPos.x), abs(wrappedPos.y));
            #endif
			  float dist;
            #if defined(SHAPE_CIRCLE)
              dist = circularDist;
            #elif defined(SHAPE_CIRCLE_SQUIRCLE)
              dist = lerp(circularDist, squircleDist, _ShapeParams.x * 2.0);
            #elif defined(SHAPE_SQUIRCLE)
              dist = squircleDist;
            #elif defined(SHAPE_SQUIRCLE_SQUARE)
              dist = lerp(squircleDist, rectDist, (_ShapeParams.x - 0.5) * 2.0);
            #elif defined(SHAPE_SQUARE)
              dist = rectDist;
            #else
              dist = 0.0;
            #endif

			  float rawLevel = (1.0 - saturate(dist * dotSize)) * innerSteps;
			  float coarseLevel = floor(rawLevel);
			  float innerLevel = rawLevel - coarseLevel;
			  float hardnessFactor = hardness * 5.0 + 1.0;
			  float hardnessPower = hardness * 63.0 + 1.0;
			  innerLevel = 1.0 - saturate(pow(saturate((1.0 - innerLevel) * hardnessFactor - hardnessFactor + 1.0), hardnessPower));
			  float totalLevel = innerLevel + coarseLevel;

			  float4 stepColor = float4(FromHsl(_BaseColorHSL.xyz + _ColorStepHSL.xyz * (innerSteps - totalLevel)), _BaseColorHSL.a);
              return lerp(_BackgroundColor, stepColor, saturate(totalLevel));
            }
            ENDCG
        }
    }
}
