Shader "Custom/BooleanArrayUnlitShader"
{
    Properties
    {
        _MainTex ("Boolean Array Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        CGPROGRAM
        #pragma surface surf Lambert

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Sample the boolean array texture
            fixed4 booleanArrayColor = tex2D(_MainTex, IN.uv_MainTex);

            // Use the sampled color to control the output color
            o.Albedo = booleanArrayColor.rgb;
        }
        ENDCG

        ZWrite On
        ZTest Always
    }

    FallBack "Diffuse"
}
