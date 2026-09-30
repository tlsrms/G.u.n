Shader "Gun/Analog Monochrome Screen"
{
    Properties
    {
        _Scanlines ("Scanline strength", Range(0, .3)) = .1
        _Grain ("Film grain", Range(0, .05)) = .018
        _Vignette ("Glass edge shading", Range(0, 1)) = .45
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Monochrome broadcast"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Scanlines, _Grain, _Vignette;
            CBUFFER_END

            float3 SignalColor(float2 uv)
            {
                // Preserve gameplay colors (especially timing guides) through the CRT treatment.
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 pixel = rcp(_ScreenParams.xy);
                float3 light = SignalColor(uv);
                float3 halo = (SignalColor(uv + float2(pixel.x * 1.5, 0)) + SignalColor(uv - float2(pixel.x * 1.5, 0))) * .5;
                light = lerp(light, halo, .12);
                float lines = .5 + .5 * cos(uv.y * min(480, _ScreenParams.y * .5) * 6.2831853);
                float2 centered = abs(uv * 2 - 1);
                float edge = smoothstep(.5, 1.25, length(centered * float2(.75, 1)));
                float grain = frac(sin(dot(floor(uv * _ScreenParams.xy) + floor(_Time.y * 24), float2(127.1, 311.7))) * 43758.5453) - .5;
                float roll = 1 - smoothstep(0, .035, abs(uv.y - frac(_Time.y * .055)));
                light = (.008 + light * .97) * (1 - _Scanlines * lines) * (1 - _Vignette * edge);
                light += grain * _Grain + roll * .006;
                // Soft glass corners stay at the frame edge, away from gameplay and controls.
                float2 corner = max(centered - float2(.94, .91), 0) / float2(.06, .09);
                light *= 1 - smoothstep(.82, 1.1, length(corner));
                return half4(saturate(light), 1);
            }
            ENDHLSL
        }
    }
}
