// Sombreado simple para la vista 2.5D: textura + luz direccional fija (Lambert) + tinte.
// No depende de luces de la escena, por eso funciona igual con el Renderer 2D de URP
// (se dibuja como pasada "SRPDefaultUnlit", sin LightMode).
Shader "TD/Lambert25D"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Emision ("Emision", Color) = (0,0,0,0)
        _Tinte ("Tinte (alfa = intensidad)", Color) = (0,0,0,0)
        _LuzDir ("Direccion hacia la luz", Vector) = (-0.35, -0.45, -0.82, 0)
        _Ambiente ("Luz ambiente", Range(0,1)) = 0.5
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
            float _Ambiente;
            float _Corte;

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i, fixed cara : VFACE) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                clip(tex.a - _Corte);

                float3 n = normalize(i.normal) * (cara > 0 ? 1.0 : -1.0);
                float lambert = saturate(dot(n, normalize(_LuzDir.xyz)));
                float luz = _Ambiente + (1.0 - _Ambiente) * lambert;

                fixed3 col = tex.rgb * luz;
                col = lerp(col, _Tinte.rgb, _Tinte.a);
                col += _Emision.rgb;
                return fixed4(col, tex.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
