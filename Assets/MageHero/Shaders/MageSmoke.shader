// Humo / ceniza espectral para el jefe volcánico: transparente, con ondulación de vértices,
// bordes que se deshacen con ruido animado (sube como humo) y un brillo de brasa en el contorno.
// Sirve para mallas (columna de humo, halo del cuerpo) y para partículas (usa el color de vértice).
Shader "MageHero/Smoke"
{
    Properties
    {
        _BaseColor ("Color del humo", Color) = (0.07, 0.05, 0.06, 0.85)
        _EdgeColor ("Color de brasa en el borde", Color) = (1.0, 0.35, 0.08, 1)
        _EdgeStrength ("Intensidad de brasa", Range(0, 4)) = 0.6
        _Soft ("Suavidad del contorno", Range(0.1, 6)) = 1.6
        _NoiseScale ("Escala del ruido", Float) = 1.6
        _NoiseSpeed ("Velocidad de subida", Float) = 0.8
        _Dissolve ("Deshacer (0 = sólido)", Range(0, 1)) = 0.35
        _Wobble ("Ondulación", Float) = 0.08
        _WobbleSpeed ("Velocidad de ondulación", Float) = 1.7
        _BottomFade ("Desvanecer abajo (altura objeto)", Float) = 0.0
        _BottomFadeRange ("Rango del desvanecido", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half _EdgeStrength, _Soft, _Dissolve;
                float _NoiseScale, _NoiseSpeed, _Wobble, _WobbleSpeed, _BottomFade, _BottomFadeRange;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 nrm : NORMAL; half4 col : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V
            {
                float4 cs : SV_POSITION;
                float3 ws : TEXCOORD0;
                float3 nw : TEXCOORD1;
                half4 col : TEXCOORD2;
                float oy : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            float hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                                 lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                                 lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float fbm(float3 p) { return noise3(p) * 0.6 + noise3(p * 2.1 + 7.3) * 0.3 + noise3(p * 4.3 + 1.7) * 0.1; }

            V vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 ws = TransformObjectToWorld(i.pos.xyz);
                float3 nw = TransformObjectToWorldNormal(i.nrm);
                float t = _Time.y * _WobbleSpeed;
                float w = sin(ws.y * 2.3 + t * 1.3 + ws.x * 1.1) * 0.6 + sin(ws.z * 2.9 - t * 1.7) * 0.4;
                ws += nw * w * _Wobble;
                o.ws = ws; o.nw = nw; o.col = i.col; o.oy = i.pos.y;
                o.cs = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.cs.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.nw);
                float3 v = normalize(GetWorldSpaceViewDir(i.ws));
                float facing = saturate(abs(dot(n, v)));
                float soft = pow(facing, _Soft);                                  // centro denso, contorno que se deshace
                float3 np = i.ws * _NoiseScale - float3(0, _Time.y * _NoiseSpeed, 0);
                float nz = fbm(np);
                float body = saturate((nz - _Dissolve) / max(0.05, 1.0 - _Dissolve) * 1.6);
                half a = _BaseColor.a * i.col.a * soft * body;
                if (_BottomFadeRange > 0.0001) a *= saturate((i.oy - _BottomFade) / _BottomFadeRange);
                float ember = pow(1.0 - facing, 2.0) * smoothstep(0.45, 0.85, nz);
                half3 c = _BaseColor.rgb * i.col.rgb + _EdgeColor.rgb * ember * _EdgeStrength;
                c = MixFog(c, i.fog);
                return half4(c, saturate(a));
            }
            ENDHLSL
        }
    }
}
