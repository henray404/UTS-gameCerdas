// World-space triplanar albedo so generated walls and floors take tiling textures without UVs.
// Adds soot near the floor of every storey and slow damp blotches for an abandoned-house look.
Shader "SneakHouse/HorrorTriplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Tiling ("Metres per tile", Float) = 2
        _Glossiness ("Smoothness", Range(0, 1)) = .12
        _Grime ("Grime", Range(0, 1)) = .55
        _StoreyHeight ("Storey height", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Tiling, _Glossiness, _Grime, _StoreyHeight;
        struct Input { float3 worldPos; float3 worldNormal; };

        fixed3 Sample(float3 p, float3 w)
        {
            return tex2D(_MainTex, p.zy).rgb * w.x + tex2D(_MainTex, p.xz).rgb * w.y + tex2D(_MainTex, p.xy).rgb * w.z;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 w = pow(abs(IN.worldNormal), 4);
            w /= (w.x + w.y + w.z);
            float3 p = IN.worldPos / max(.05, _Tiling);
            // Push contrast so smooth photo textures read as worn and stained under a torch.
            fixed3 albedo = saturate((Sample(p, w) - .5) * 1.8 + .5);
            // Low-frequency resample of the same texture gives irregular damp stains.
            float damp = dot(Sample(p * .17 + 3.1, w), fixed3(.33, .33, .33));
            float storey = frac(IN.worldPos.y / max(.5, _StoreyHeight));
            float soot = 1 - smoothstep(0, .3, storey);
            float grime = 1 - _Grime * (.35 * soot + .4 * smoothstep(.35, .65, damp));
            o.Albedo = albedo * _Color.rgb * grime;
            o.Smoothness = _Glossiness * (1 - .6 * soot);
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
