Shader "Gun/MuzzleGroundLight"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        // Add emitted light; shadow alpha darkens only the occluded ground.
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Source, _Forward, _RoomBounds;
            float4 _Occluders[24];
            int _OccluderCount;
            float _Pulse;
            struct Output { float4 position : SV_POSITION; float2 world : TEXCOORD0; };
            Output vert(float4 position : POSITION)
            {
                Output o;
                o.position = UnityObjectToClipPos(position);
                o.world = mul(unity_ObjectToWorld, position).xy;
                return o;
            }
            float4 frag(Output i) : SV_Target
            {
                float2 p = i.world - _Source.xy;
                float distance = length(p);
                float2 ray = p / max(distance, .0001);
                float falloff = pow(saturate(1 - distance / _Source.z), 2) / (1 + distance * distance * .55);
                float directional = .35 + .65 * pow(saturate(dot(ray, _Forward.xy) * .5 + .5), 3);
                float shadow = 0;
                for (int j = 0; j < _OccluderCount; j++)
                {
                    float2 oc = _Occluders[j].xy - _Source.xy;
                    float d = length(oc);
                    float2 axis = oc / max(d, .001);
                    float along = dot(p, axis);
                    float across = abs(p.x * axis.y - p.y * axis.x);
                    // A source above a waist-high torso casts a finite, widening shadow.
                    float width = _Occluders[j].z * max(1, along / max(d, .1));
                    float penumbra = .025 + max(0, along - d) * .045;
                    float silhouette = 1 - smoothstep(width - penumbra, width + penumbra, across);
                    silhouette *= smoothstep(d - _Occluders[j].z, d, along);
                    silhouette *= 1 - smoothstep(d * 2.5, d * 3.3 + .2, along);
                    shadow = max(shadow, silhouette * _Occluders[j].w);
                }
                float inside = step(_RoomBounds.x, i.world.x) * step(_RoomBounds.y, i.world.y)
                    * step(i.world.x, _RoomBounds.z) * step(i.world.y, _RoomBounds.w);
                float ground = falloff * directional * inside;
                float forward = dot(p, _Forward.xy);
                float side = abs(p.x * _Forward.y - p.y * _Forward.x);
                // Small white-hot directional core and thin optical streak, no expanding smoke disc.
                float core = exp(-abs(forward - .09) * 18 - side * 95) * step(-.025, forward);
                float streak = exp(-abs(forward - .025) * 120 - side * 22);
                float3 light = float3(1, .79, .46) * ground * (1 - shadow) * .85;
                light += float3(1.9, 1.8, 1.5) * (core + streak * .38);
                return float4(light * _Pulse, shadow * ground * _Pulse * .65);
            }
            ENDCG
        }
    }
}
