Shader "Custom/OccludedRed"
{
    Properties
    {
        _Color("Occluded Color", Color) = (1, 0, 0, 0.7)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
        }

        Pass
        {
            Name "OccludedRed"

            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }

            // 半透明表示
            Blend SrcAlpha OneMinusSrcAlpha

            // 深度情報は書き込まない
            ZWrite Off

            // 手前に何か存在する部分だけ描画
            ZTest Greater

            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }
    }
}