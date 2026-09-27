Shader "Hidden/Gun/RetroVideo"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Strength, _VideoTime;

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float tick = floor(_VideoTime * 24);
                float jitter = sin(uv.y * 65 + _VideoTime * 1.7) * .3 * _Strength;
                uv.x += jitter * abs(_MainTex_TexelSize.x);
                float2 bleed = float2(abs(_MainTex_TexelSize.x) * 1.3 * _Strength, 0);
                float3 baseColor = tex2D(_MainTex, uv).rgb;
                float3 color = float3(tex2D(_MainTex, uv + bleed).r, baseColor.g, tex2D(_MainTex, uv - bleed).b);
                color = lerp(baseColor, color, .45);
                float scan = .5 + .5 * cos(uv.y * 240 * 6.2831853);
                float grain = Hash(floor(uv * float2(640, 480)) + tick) - .5;
                float2 centered = uv * 2 - 1;
                float vignette = saturate(dot(centered, centered) * .5);
                color *= 1 - _Strength * (.065 * scan + .12 * vignette);
                color += grain * .045 * _Strength;
                // Gentle black lift and highlight roll-off retain colored gameplay markers.
                color = lerp(color, .012 + color * .97, _Strength);
                return fixed4(saturate(color), 1);
            }
            ENDCG
        }
    }
}
