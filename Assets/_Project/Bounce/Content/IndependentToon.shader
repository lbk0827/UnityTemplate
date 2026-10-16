Shader "BK/Toon"
{
 Properties
 {
  _BaseMap("Texture",2D)="white"{}
  _BaseColor("Color",Color)=(1,1,1,1)
  _HColor("Highlight",Color)=(1,1,1,1)
  _SColor("Shadow",Color)=(.4,.4,.4,1)
  _RampThreshold("Ramp",Range(0,1))=.5
  _RampSmoothing("Softness",Range(.001,1))=.2
  _MatCapTex("Matcap",2D)="black"{}
  _MatCapColor("Matcap Color",Color)=(0,0,0,1)
  _UseMatCap("Use Matcap",Float)=0
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);TEXTURE2D(_MatCapTex);SAMPLER(sampler_MatCapTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_BaseColor,_HColor,_SColor,_MatCapColor;float _RampThreshold,_RampSmoothing,_UseMatCap;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;};
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
   half4 frag(Varyings i):SV_Target
   {
    float3 normal=normalize(i.normalWS);Light sun=GetMainLight();
    float diffuse=saturate(dot(normal,sun.direction)*.5+.5);
    float ramp=smoothstep(_RampThreshold-_RampSmoothing*.5,_RampThreshold+_RampSmoothing*.5,diffuse);
    float3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb*lerp(_SColor.rgb,_HColor.rgb,ramp);
    float2 capUV=mul((float3x3)UNITY_MATRIX_V,normal).xy*.5+.5;
    color+=SAMPLE_TEXTURE2D(_MatCapTex,sampler_MatCapTex,capUV).rgb*_MatCapColor.rgb*_UseMatCap*.35;
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
