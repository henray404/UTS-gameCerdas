Shader "Hidden/SneakHouse/ChaseDread"
{
    Properties { _MainTex ("Scene", 2D) = "white" {} }
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
            float _Intensity, _Pulse, _Clock;
            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                float2 centered = uv - .5;
                centered.x *= _ScreenParams.x / _ScreenParams.y;
                float edge = smoothstep(.19, .85, length(centered));
                float2 split = centered * _MainTex_TexelSize.xy * edge * _Intensity * .85;
                float3 rgb = tex2D(_MainTex, uv).rgb;
                rgb.r = tex2D(_MainTex, uv + split).r;
                rgb.b = tex2D(_MainTex, uv - split).b;
                float lightness = dot(rgb, float3(.2126, .7152, .0722));
                rgb = lerp(rgb, lightness.xxx, _Intensity * .29);
                rgb *= lerp(float3(1, 1, 1), float3(.72, .80, .94), _Intensity);
                float tunnel = 1 - edge * _Intensity * (.91 + _Pulse * .04);
                rgb *= tunnel * tunnel;
                rgb += float3(.065, .005, .003) * edge * _Intensity * _Pulse;
                float noise = frac(sin(dot(floor(uv * _ScreenParams.xy / 2), float2(12.9898, 78.233)) + floor(_Clock * 16)) * 43758.5453);
                rgb += (noise - .5) * _Intensity * .01;
                return fixed4(saturate(rgb), 1);
            }
            ENDCG
        }
    }
}
