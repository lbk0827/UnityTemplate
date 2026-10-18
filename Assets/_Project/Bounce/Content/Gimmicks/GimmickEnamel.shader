Shader "BK/Gimmick Enamel"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _EmissionColor("Heat", Color) = (0,0,0,1)
        _Gloss("Enamel gloss", Range(8,128)) = 48
        _Specular("Softbox reflection", Range(0,1)) = .35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _EmissionColor;
            float _Gloss, _Specular;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.color=v.color;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS),v=GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float diffuse=smoothstep(-.28,.85,dot(n,sun.direction));
                float shade=lerp(.48,1.08,diffuse)*lerp(.68,1,sun.shadowAttenuation);
                float3 base=i.color.rgb*_BaseColor.rgb;
                float3 h=SafeNormalize(sun.direction+v);
                float spec=pow(saturate(dot(n,h)),_Gloss)*_Specular*sun.shadowAttenuation;
                float rim=pow(1-saturate(dot(n,v)),3)*.07;
                float3 color=base*shade*i.color.a+spec*lerp(float3(.78,.87,1),sun.color,.4)+base*rim+_EmissionColor.rgb*.25;
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _EmissionColor;
            float _Gloss, _Specular;
            CBUFFER_END
            float3 _LightDirection;
            float4 vert(float4 positionOS:POSITION,float3 normalOS:NORMAL):SV_POSITION
            {
                float3 p=TransformObjectToWorld(positionOS.xyz),n=TransformObjectToWorldNormal(normalOS);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _EmissionColor;
            float _Gloss, _Specular;
            CBUFFER_END
            float4 vert(float4 positionOS:POSITION):SV_POSITION{return TransformObjectToHClip(positionOS.xyz);}
            half4 frag(float4 positionCS:SV_POSITION):SV_Target{return positionCS.z;}
            ENDHLSL
        }
    }
}
