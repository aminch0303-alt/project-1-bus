// Unlit shader (like Particles/Standard Unlit) with:
// - Color tint + brightness
// - Optional cutout (alpha clip)
// - Single or double sided (Cull)
// - Optional cubemap reflection (like Legacy Reflective/Bumped Specular) with its own color + brightness
// - Optional normal map for the reflection, reflection mask and fresnel
// Built-in render pipeline, mobile friendly.
Shader "SGI/Unlit Reflective"
{
    Properties
    {
        [Header(Color)]
        _Color ("Color", Color) = (1,1,1,1)
        _Brightness ("Color Brightness (0 = dark, 1 = normal, 2+ = bright)", Range(0, 4)) = 1
        _MainTex ("Texture (RGB) Alpha (A)", 2D) = "white" {}

        [Header(Cutout)]
        [Toggle(_ALPHATEST_ON)] _UseCutout ("Cutout", Float) = 0
        _Cutoff ("Cutout Amount", Range(0, 1)) = 0.5

        [Header(Sides)]
        [Enum(Double Sided,0,Back Only,1,Front Only,2)] _Cull ("Render Face", Float) = 0

        [Header(Reflection)]
        [Toggle(_REFLECTION_ON)] _UseReflection ("Reflection", Float) = 1
        _Cube ("Reflection Cubemap", Cube) = "_Skybox" {}
        _ReflectColor ("Reflection Color", Color) = (1,1,1,1)
        _ReflectBrightness ("Reflection Brightness (0 = off, 1 = normal, 2+ = bright)", Range(0, 4)) = 1
        _ReflectMask ("Reflection Mask (white = full, black = none)", 2D) = "white" {}
        [Toggle] _AlphaMask ("Use Texture Alpha as Reflection Mask", Float) = 0
        _FresnelPower ("Edge Reflection (Fresnel, 0 = off)", Range(0, 5)) = 0

        [Header(Normal Map)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "IgnoreProjector"="True" }
        LOD 100
        Cull [_Cull]

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _REFLECTION_ON
            #pragma shader_feature_local _NORMALMAP
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            half _Brightness;
            half _Cutoff;

            samplerCUBE _Cube;
            fixed4 _ReflectColor;
            half _ReflectBrightness;
            sampler2D _ReflectMask;
            half _AlphaMask;
            half _FresnelPower;

            sampler2D _BumpMap; float4 _BumpMap_ST;
            half _BumpScale;

            struct appdata
            {
                float4 vertex  : POSITION;
                float2 uv      : TEXCOORD0;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                fixed4 color   : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float4 uv       : TEXCOORD0; // xy = main, zw = normal map
                float3 worldPos : TEXCOORD1;
                half3 worldN    : TEXCOORD2;
            #if _NORMALMAP
                half3 worldT    : TEXCOORD3;
                half3 worldB    : TEXCOORD4;
            #endif
                fixed4 color    : COLOR;
                UNITY_FOG_COORDS(5)
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv, _BumpMap);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
            #if _NORMALMAP
                o.worldT = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldB = cross(o.worldN, o.worldT) * v.tangent.w * unity_WorldTransformParams.w;
            #endif
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv.xy) * _Color * i.color;

            #if _ALPHATEST_ON
                clip(col.a - _Cutoff);
            #endif

                half3 rgb = col.rgb * _Brightness;

            #if _REFLECTION_ON
                half3 n = normalize(i.worldN);
            #if _NORMALMAP
                half3 tn = UnpackScaleNormal(tex2D(_BumpMap, i.uv.zw), _BumpScale);
                n = normalize(tn.x * i.worldT + tn.y * i.worldB + tn.z * n);
            #endif
                n *= (facing > 0) ? 1 : -1; // correct reflection on back faces when double sided

                half3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half3 r = reflect(-viewDir, n);
                half3 refl = texCUBE(_Cube, r).rgb * _ReflectColor.rgb * _ReflectBrightness;
                refl *= tex2D(_ReflectMask, i.uv.xy).r;
                refl *= lerp(1, col.a, _AlphaMask);

                if (_FresnelPower > 0)
                    refl *= pow(1 - saturate(dot(n, viewDir)), _FresnelPower);

                rgb += refl;
            #endif

                fixed4 outCol = fixed4(rgb, col.a);
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }

        // Shadows (respect cutout so holes cast correct shadows)
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            half _Cutoff;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
            #if _ALPHATEST_ON
                clip(tex2D(_MainTex, i.uv).a * _Color.a - _Cutoff);
            #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack Off
}
