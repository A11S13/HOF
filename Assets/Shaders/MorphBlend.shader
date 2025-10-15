Shader "Custom/FullMorphBlend"
{
    Properties
    {
        _Blend ("Blend", Range(0,1)) = 0

        _MainTexA ("Albedo A", 2D) = "white" {}
        _MainTexB ("Albedo B", 2D) = "white" {}

        _BumpMapA ("Normal A", 2D) = "bump" {}
        _BumpMapB ("Normal B", 2D) = "bump" {}

        _RoughnessA ("Roughness A", 2D) = "white" {}
        _RoughnessB ("Roughness B", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTexA, _MainTexB;
        sampler2D _BumpMapA, _BumpMapB;
        sampler2D _RoughnessA, _RoughnessB;

        float _Blend;

        struct Input
        {
            float2 uv_MainTexA;
            float2 uv_MainTexB;
            float2 uv_BumpMapA;
            float2 uv_BumpMapB;
            float2 uv_RoughnessA;
            float2 uv_RoughnessB;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo
            float4 colA = tex2D(_MainTexA, IN.uv_MainTexA);
            float4 colB = tex2D(_MainTexB, IN.uv_MainTexB);
            float4 albedo = lerp(colA, colB, _Blend);

            o.Albedo = albedo.rgb;
            o.Alpha  = albedo.a;

            // Normals
            float3 nA = UnpackNormal(tex2D(_BumpMapA, IN.uv_BumpMapA));
            float3 nB = UnpackNormal(tex2D(_BumpMapB, IN.uv_BumpMapB));
            o.Normal = normalize(lerp(nA, nB, _Blend));

            // Roughness Smoothness
            float roughA = tex2D(_RoughnessA, IN.uv_RoughnessA).r;
            float roughB = tex2D(_RoughnessB, IN.uv_RoughnessB).r;
            float rough = lerp(roughA, roughB, _Blend);

            o.Smoothness = 1.0 - rough; // Unity wants smoothness
            o.Metallic = 0;             // can expose later if needed
        }
        ENDCG
    }
    FallBack "Standard"
}
