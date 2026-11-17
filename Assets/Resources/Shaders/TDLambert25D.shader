// Sombreado simple para la vista 2.5D: textura + luz direccional fija (Lambert) + tinte + neblina.
// No depende de luces de la escena, por eso funciona igual con el Renderer 2D de URP
// (se dibuja como pasada "SRPDefaultUnlit", sin LightMode).
// El color de la luz, la luz ambiente y la neblina son globales (_TD_*), los fija Recursos25D.AplicarAmbiente.
Shader "TD/Lambert25D"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Emision ("Emision", Color) = (0,0,0,0)
        _Tinte ("Tinte (alfa = intensidad)", Color) = (0,0,0,0)
        _LuzDir ("Direccion hacia la luz", Vector) = (-0.35, -0.45, -0.82, 0)
        _Corte ("Recorte alfa", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "TD_Lambert"
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _Emision;
            fixed4 _Tinte;
            float4 _LuzDir;
            float _Corte;

            // Globales del ambiente (Shader.SetGlobal*)
            float4 _TD_Luz;          // rgb = color de la luz direccional
            float4 _TD_Ambiente;     // rgb = luz ambiente
            float4 _TD_Niebla;       // rgb = color de la neblina, a = intensidad máxima
            float4 _TD_NieblaRango;  // x = distancia de inicio, y = distancia final

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float dist : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                float3 mundo = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.dist = distance(mundo, _WorldSpaceCameraPos);
                return o;
            }

            fixed4 frag (v2f i, fixed cara : VFACE) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                clip(tex.a - _Corte);

                float3 n = normalize(i.normal) * (cara > 0 ? 1.0 : -1.0);
                float lambert = saturate(dot(n, normalize(_LuzDir.xyz)));
                float3 luz = _TD_Ambiente.rgb + _TD_Luz.rgb * lambert;

                float3 col = tex.rgb * luz;
                col = lerp(col, _Tinte.rgb, _Tinte.a);
                col += _Emision.rgb;

                float rango = max(0.001, _TD_NieblaRango.y - _TD_NieblaRango.x);
                float niebla = saturate((i.dist - _TD_NieblaRango.x) / rango) * _TD_Niebla.a;
                col = lerp(col, _TD_Niebla.rgb, niebla);
                return fixed4(col, tex.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
