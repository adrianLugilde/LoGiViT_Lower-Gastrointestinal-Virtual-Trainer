Shader "Hidden/PixelClipper"
{
    Properties
    {
        _ClipCenter ("Clip Center", Vector) = (0,0,0,0)
        _ClipSize ("Clip Size", Vector) = (1,1,1,0)
        _Color ("Color", Color) = (1,0,0,0.5)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "HDRenderPipeline" 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "Forward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _ClipCenter;
                float4 _ClipSize;
                float4 _Color;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float3 diff = abs(input.positionWS - _ClipCenter.xyz);
                float3 halfSize = _ClipSize.xyz * 0.5;
                
                // Clip pixels outside the bounds
                if (diff.x > halfSize.x || diff.y > halfSize.y || diff.z > halfSize.z)
                    discard;
                    
                return _Color;
            }
            ENDHLSL
        }
    }
}