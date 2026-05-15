Shader "Outlined/Hologram"
{
    Properties
    {
        _RimColor("Rim Color", Color) = (0, 0.5, 0.5, 0.0)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0
        _PulseFrequency("Pulse Frequency", Range(0, 100)) = 4
        _VerticalOffeset("Vertical Offset", Range(0, 100)) = 100
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        // Pass 1: Z-prepass (scrive solo nello z-buffer per fix z-ordering)
        Pass
        {
            Name "DepthOnly"
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Pass 2: Effetto ologramma (rim + pulse)
        Pass
        {
            Name "HologramForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex HoloVert
            #pragma fragment HoloFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float  _RimPower;
                float  _PulseFrequency;
                float  _VerticalOffeset;   // nome originale mantenuto per compatibilita'
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
            };

            Varyings HoloVert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;

                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                // Passa la posizione object-space per il pulse verticale
                // +3 come nello shader originale
                output.positionOS = input.positionOS.xyz + 3.0;

                return output;
            }

            half4 HoloFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Rim lighting
                half rim = 1.0 - saturate(dot(viewDirWS, normalWS));
                half rimPow = pow(rim, _RimPower);

                // Emissione: colore rim amplificato
                half3 emission = _RimColor.rgb * rimPow * 10.0;

                // Alpha con pulse sinusoidale verticale (come originale)
                float pulse = sin(_Time.y * _PulseFrequency - input.positionOS.y * _VerticalOffeset);
                half alpha = (pulse * rimPow * 0.3) + rimPow * 0.7;

                return half4(emission, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}