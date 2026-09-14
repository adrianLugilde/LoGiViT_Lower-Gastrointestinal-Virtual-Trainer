Shader "HDRP/NormalVisualization"
{
    Properties
    {
        _NormalLength ("Normal Length", Float) = 0.1
        _NormalColor ("Normal Color", Color) = (0,1,0,1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "Forward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _NormalLength;
                float4 _NormalColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            [maxvertexcount(6)]
            void geom(triangle Varyings input[3], inout LineStream<Varyings> lineStream)
            {
                // Draw normal lines for each vertex
                for (int j = 0; j < 3; j++)
                {
                    Varyings v1 = input[j];

                    Varyings v2 = v1;
                    v2.positionWS = v1.positionWS + normalize(v1.normalWS) * _NormalLength;
                    v2.positionCS = TransformWorldToHClip(v2.positionWS);

                    lineStream.Append(v1);
                    lineStream.Append(v2);
                    lineStream.RestartStrip();
                }
            }

            float4 frag(Varyings input) : SV_Target
            {
                return _NormalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}