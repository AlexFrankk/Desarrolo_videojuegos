// Brillo aditivo para la cinemática: el Espíritu del Maestro Athan, los números de luz y las chispas.
// - _Color admite HDR (> 1) para que el bloom lo haga resplandecer.
// - _MainTex: textura opcional (blanca por defecto). Con un atlas de fuente (TextMesh) usa el canal alfa.
// - _Fresnel: 0 = brillo uniforme; > 0 = más brillo en el contorno (aspecto espectral).
// - _Flicker: parpadeo suave a lo largo del objeto (energía viva).
Shader "MageHero/CineGlow"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (0.6, 1.4, 2.2, 1)
        _MainTex ("Textura (alfa)", 2D) = "white" {}
        _Fresnel ("Contorno (fresnel)", Range(0, 6)) = 0
        _CoreAlpha ("Opacidad del centro", Range(0, 1)) = 1
        _Flicker ("Parpadeo", Range(0, 1)) = 0
        _FlickerSpeed ("Velocidad del parpadeo", Float) = 3
        _Intensity ("Intensidad", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float4 _MainTex_ST;
                half _Fresnel, _CoreAlpha, _Flicker, _FlickerSpeed, _Intensity;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; float3 w : TEXCOORD3; half4 col : COLOR; };

            V vert(A i)
            {
                V o;
                float3 w = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(w);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.n = TransformObjectToWorldNormal(i.n);
                o.v = GetWorldSpaceViewDir(w);
                o.w = w;
                o.col = i.col;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.col.a;
                half rim = 1;
                if (_Fresnel > 0.001)
                {
                    half ndv = saturate(abs(dot(normalize(i.n), normalize(i.v))));
                    rim = lerp(_CoreAlpha, 1, pow(1 - ndv, _Fresnel));
                }
                half fl = 1 - _Flicker * 0.5 * (1 + sin(_Time.y * _FlickerSpeed + i.w.y * 3.1 + i.w.x * 1.7));
                half3 c = _Color.rgb * i.col.rgb * a * rim * fl * _Intensity * _Color.a;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
