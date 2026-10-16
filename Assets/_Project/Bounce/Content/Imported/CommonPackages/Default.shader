Shader "DUG/Default"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [Toggle(IS_PMA)] _IsPMA ("Is PMA Texture", Float) = 0
        [Toggle(SHOW_UV)] _ShowUV ("[Dev]Show UV", Float) = 0
        [Toggle(SHOW_RGB)] _ShowRGB ("[Dev]Show RGB", Float) = 0
        [Toggle(CLAMP_ATLAS_UV)] _ClampAtlasUV ("Clamp Atlas UV", Float) = 0
        [Toggle(TILE_ATLAS_UV)] _TileAtlasUV ("Tile Atlas UV", Float) = 0

        //POLAR COORDINATES
        [Toggle(POLAR_COORDINATES)] _UsePolarCoordinates("Polar Coordinates", Float) = 0

        //GRAYSCALE_MAIN_TEXTURE
        [Toggle(GRAYSCALE_MAIN_TEXTURE)] _UseGrayscaleMainTexture("GrayScale Main Texture", Float) = 0
        _BlackColor ("Black Color", Color) = (0,0,0,1)
        _WhiteColor ("White Color", Color) = (1,1,1,1)

        //MOVE AND ROT
        [Toggle(MOVE_ROT)] _UseMainTexMoveAndRot("Use Main Texture Move and Rot", Float) = 0
        _MainTexOffsetX ("Offset X ", Range(-1,1)) = 0        
        _MainTexOffsetY ("Offset Y ", Range(-1,1)) = 0
        _MainTexMoveX ("Main Texture Move Speed X", Range(-2,2)) = 0
        _MainTexMoveY ("Main Texture Move Speed Y", Range(-2,2)) = 0
        _MainTexRotPivotX("Main Texture Rot Pivot X", Range(0,1)) = 0.5
        _MainTexRotPivotY("Main Texture Rot Pivot Y", Range(0,1)) = 0.5
        _MainTexRotOffset("Rot Offset", Range(-360, 360)) = 0
        _MainTexRot ("Main Texuture Rot Speed", Range(-2, 2)) = 0
        _MainTexShear ("Shear", Range(-1, 1)) = 0

        //ADDITIVE
        [Toggle(ADDITIVE_COLOR)] _UseAdditiveColor ("Additive Color", Float) = 0
        _AdditiveColor ("Additive Color", Color) = (0,0,0,1)
    	[Toggle] _UseTextureColor ("Use Texture Color", Float) = 1
        _AdditiveIntensity ("Additive Color Intensity", Range(0, 5)) = 0
        [Toggle(ADDITIVE_TWINKLE)] _AdditiveColorTwinkling("Twinkling", Float) = 0
        [Toggle(ADDITIVE_TWINKLE_RANDOM)] _AdditiveColorTwinklingRandom("Random Twinkling", Float) = 0
        _AdditiveColorTwinklingSpeed("Twinkling Speed", Range(0.01, 1)) = 0.5    
        [Toggle] _IsPremultipliedAlpha ("Is Premultiplied Alpha Texture", Float) = 0
    	[Toggle] _NoMask ("No Mask", Float) = 0
        [Toggle(ADDITIVE_OUTLINE_ONLY)] _AdditiveOutlineOnly("Outline Only", Float) = 0
        [Toggle(ADDITIVE_OVERLAY_ONLY)] _AdditiveOverlayOnly("Overlay Only", Float) = 0
        [Toggle(ADDITIVE_OVERLAY2_ONLY)] _AdditiveOverlay2Only("Overlay2 Only", Float) = 0

        //GRAYSCALE
        [Toggle(GRAYSCALE)] _UseGrayscale("GrayScale", Float) = 0
        _GrayScaleWeight ("Weight", Range(0,1)) = 1

        //DISSOLVE
        [Toggle(DISSOLVE)] _UseDissolve("Dissolve", Float) = 0
        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        [Toggle(DIS_POLAR_COORD)] _DisPolarCoord("Polar Coord", Float) = 0
        [Toggle(DIS_SCREEN_UV)] _DisScreenUV("Use Screen UV", Float) = 0
        [Toggle(DISSOLVE_GRAYSCALE)] _DisGrayscale("GrayScale", Float) = 0
        [Toggle(DIS_OUTLINE_ONLY)] _DisOutlineOnly("Outline Only", Float) = 0
        [Toggle(DIS_OVERLAY_ONLY)] _DisOverlayOnly("Overlay Only", Float) = 0
        [Toggle(DIS_OVERLAY2_ONLY)] _DisOverlay2Only("Overlay2 Only", Float) = 0
        [Toggle(DIS_SUB_TEX)] _UseDisSubTex("Sub Texture", Float) = 0
        [NoScaleOffset] _DisSubTex("Sub Texture", 2D) = "white" {}
        [Toggle(DIS_SUB_TEX_MAIN_ALPHA)] _DisApplyMainTexAlpha("Apply Main Texture Alpha", Float) = 0
        [Toggle] _DisNoisedUV("Affected UV", Float) = 1
        _DisNoisePower ("Noise Power", Range(0,5)) = 1
        _DisProgress("Progress",Range(0,1)) = 1
        [Toggle] _DisReverse("Reverse value", Float) = 0
        _DisEdge("Edge Range",Range(0.001,0.5)) = 0.12
        _DisEdgeAdditiveColor("Edge Additive Color", Color) = (1,0,0,1)
    	[Toggle] _DisUseTexColor("Use Texture Color", Float) = 0
        _DisEdgeIntensity ("Edge Intensity", Range(0,1)) = 0.02
        _DisRotate("Rotate", Range(-360, 360)) = 0
        _DisShear("Shear", Range(-10,10)) = 0
        [Toggle(SHOW_DISSOLVE)] _ShowDis("Show Dissolve Texture", Float) = 0
        
        //MASK
        [Toggle(MASK)] _UseMask("Use Mask", Float) = 0
        _MaskTex ("Mask Texture", 2D) = "white" {}
        [Toggle(MASK_POLAR_COORD)] _MaskPolarCoord ("Polar Coord", Float) = 0
        [Toggle(IS_EFFECT_MASK)] _IsEffectMask("Is Effect Mask", Float) = 0
        [Toggle] _NoisedMaskUV("Noised UV", Float) = 1
        _MaskNoisePower ("Noise Power", Range(0,5)) = 1
        [Toggle] _ReverseMask("Reverse Mask", Float) = 0
        _MaskOffsetX ("Offset X", Range(-1,1)) = 0
        _MaskOffsetY ("Offset Y", Range(-1,1)) = 0
        _MaskMoveX ("Speed X", Range(-2,2)) = 0
        _MaskMoveY ("Speed Y", Range(-2,2)) = 0
        _MaskRotPivotX("Mask Texture Rot Pivot X", Range(0,1)) = 0.5
        _MaskRotPivotY("Mask Texture Rot Pivot Y", Range(0,1)) = 0.5
        _MaskRotOffset("Mask Rot Offset", Range(-360, 360)) = 0
        _MaskRot ("Mask Texuture Rot Speed", Range(-2, 2)) = 0
        [Toggle(SHOW_MASK)] _ShowMask ("Show Mask", Float) = 0

        //NOISE
        [Toggle(NOISE)] _UseNoise (" Use Noise", Float) = 0
        _NoiseTex("Noise Texture", 2D) = "black" {}
        [Toggle(NOISE_POLAR_COORD)] _NoisePolarCoord("Polar Coord", Float) = 0
        _NoiseStrength("Noise Strength", Range(-5,5)) = 0.1
        [Toggle(NOISE_SCREEN_UV)] _NoiseScreenUV("Use Screen UV", Float) = 0
        [Toggle(NOISE_MASK_ONLY)] _NoiseMaskOnly("Mask Only", Float) = 0
        [Toggle(NOISE_OUTLINE_ONLY)] _NoiseOutlineOnly("Outline Only", Float) = 0
        [Toggle(NOISE_DISSOLVE_ONLY)] _NoiseDissolveOnly("Dissolve Only", Float) = 0
        [Toggle(NOISE_OVERLAY_ONLY)] _NoiseOverlayOnly("Overlay Only", Float) = 0
        [Toggle(NOISE_OVERLAY2_ONLY)] _NoiseOverlay2Only("Overlay2 Only", Float) = 0
        [Toggle] _Centralize ("Centralize", Range(0,1)) = 0
        [Toggle(NOISE_ADDITIVE)] _UseNoiseAdditiveColor ("ADDITIVE", Float) = 0
        [Toggle] _UseDiffuseColor ("Use Diffuse Color", Float) = 1
        _NoiseAdditiveColor("Addtive Color", Color) = (0,0,0,1)
        _NoiseAdditiveIntensity("Additive Intensity", Range(0,5)) = 1
        _NoiseOffsetX ("Offset X", Range(-1,1)) = 0
        _NoiseOffsetY ("Offset Y", Range(-1,1)) = 0
        _NoiseMoveX ("Speed X", Range(-2,2)) = 0
        _NoiseMoveY ("Speed Y", Range(-2,2)) = 0
        _NoiseRotPivotX("Noise Texture Rot Pivot X", Range(0,1)) = 0.5
        _NoiseRotPivotY("Noise Texture Rot Pivot Y", Range(0,1)) = 0.5
        _NoiseRotOffset("Noise Rot Offset", Range(-360, 360)) = 0
        _NoiseRot ("Noise Texuture Rot Speed", Range(-2, 2)) = 0
        [Toggle(SUB_NOISE)] _UseSubNoise (" Use SubNoise", Float) = 0
        _SubNoiseTex ("Sub Noise Texture", 2D) = "black" {}
        [Toggle(SUB_NOISE_POLAR_COORD)] _SubNoisePolarCoord("Polar Coord", Float) = 0
        _SubNoiseStrength("Sub Noise Strength", Range(-0.5,0.5)) = 0.1
        [Toggle] _SubCentralize ("Centralize", Float) = 0
        [Toggle] _SubNoiseMask("Noise Mask", Float) = 0
        _SubNoiseOffsetX ("Offset X", Range(-1,1)) = 0
        _SubNoiseOffsetY ("Offset Y", Range(-1,1)) = 0
        _SubNoiseMoveX ("Speed X", Range(-2,2)) = 0
        _SubNoiseMoveY ("Speed Y", Range(-2,2)) = 0
        _SubNoiseRotPivotX("Noise Texture Rot Pivot X", Range(0,1)) = 0.5
        _SubNoiseRotPivotY("Noise Texture Rot Pivot Y", Range(0,1)) = 0.5
        _SubNoiseRotOffset("Noise Rot Offset", Range(0, 360)) = 0
        _SubNoiseRot ("Noise Texuture Rot Speed", Range(-2, 2)) = 0
        [Toggle(SHOW_SUB_NOISE)] _ShowSubNoise ("Show Sub Noise", Float) = 0
        [Toggle(SHOW_NOISE)] _ShowNoise ("Show Noise", Float) = 0

        //TWO FACE
        [Toggle(TWO_FACE)] _UseTwoFace ("Use TwoFace", Float) = 0
        [NoScaleOffset] _BackTex("BackFace Texture", 2D) = "white" {}
        [Toggle] _InverseU("Inverse U", Float) = 0
        [Toggle] _InverseV("Inverse V", Float) = 0

        //OUTLINE
        [Toggle(OUTLINE)] _UseOutline ("Use Outline", Float) = 0
        [Toggle(OUTLINE_HIGH)] _UseOutlineHigh ("High Quality", Float) = 0
        _OutlineThickness ("Outline Thickness", Range(0.0, 0.05)) = 0.01
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)

        //WIND
        [Toggle(WIND)] _UseWind ("Use Wind", Float) = 0
        [Toggle(WIND_RANDOM)] _WindRandom ("Random Wind", Float) = 0
        _WindX("Wind X", Range(0, 0.5)) = 0
        _WindY("Wind Y", Range(0, 0.5)) = 0
        _WindSpeedX("Wind Speed X", Range(0,10)) = 0.5
    	_WindSpeedY("Wind Speed Y", Range(0,10)) = 0.5
        [Toggle(WIND_REVERSE)] _WindReverse("Reverse", Float) = 0
        [Toggle] _FixedPivot("Fixed Pivot", Range(0,1)) = 0
        _OffsetY("Fixed Pivot Y Offset", Range(-1,1)) = 0.15

        //OVERLAY
        [Toggle(OVERLAY)] _UseOverlay ("Use Overlay Texture", Float) = 0
        [Toggle(OVERLAY_SCREEN_UV)] _OverlayScreenUV("Use Screen UV", Float) = 0
        _OverlayTex("Overlay Texture", 2D) = "black" {}
        _OverlayColor("Overlay Color", Color) = (1,1,1,1)
        [Toggle(OVERLAY_ADDITIVE)] _OverlayAdditive("Overlay Additive", Float) = 0
        _OverlayAlpha("Overlay Alpha", Range(0,1)) = 1
        [Toggle(OVERLAY_POLAR_COORD)] _OverlayPolarCoord("Polar Coord", Float) = 0
        [Toggle] _OverlayNoisedUV("Noised UV", Float) = 0
        _OverlayNoisePower ("Noise Power", Range(0,5)) = 1
        [Toggle(OVERLAY_MAINTEX_ALPHA)] _OverlayMainTexAlpha("Main Tex Alpha", Float) = 0
        _OverlayOffsetX ("Offset X", Range(-1,1)) = 0
        _OverlayOffsetY ("Offset Y", Range(-1,1)) = 0
        _OverlayMoveX ("Speed X", Range(-2,2)) = 0
        _OverlayMoveY ("Speed Y", Range(-2,2)) = 0
        _OverlayRotOffset("Overlay Rot Offset", Range(-360, 360)) = 0
        _OverlayRot ("Overlay Texuture Rot Speed", Range(-2, 2)) = 0
        [Toggle(OVERLAY_FRAC)] _OverlayFrac ("Overlay UV Fractional", Float) = 0
    	[Toggle(SHOW_OVERLAY)] _ShowOverlay ("Show Overlay", Float) = 0

        //OVERLAY2
        [Toggle(OVERLAY2)] _UseOverlay2 ("Use Overlay Texture", Float) = 0
        [Toggle(OVERLAY2_SCREEN_UV)] _Overlay2ScreenUV("Use Screen UV", Float) = 0
        _Overlay2Tex("Overlay Texture", 2D) = "black" {}
        _Overlay2Color("Overlay Color", Color) = (1,1,1,1)
        [Toggle(OVERLAY2_ADDITIVE)] _Overlay2Additive("Overlay Additive", Float) = 0
        _Overlay2Alpha("Overlay Alpha", Range(0,1)) = 1
        [Toggle(OVERLAY2_POLAR_COORD)] _Overlay2PolarCoord("Polar Coord", Float) = 0
        [Toggle] _Overlay2NoisedUV("Noised UV", Float) = 0
        _Overlay2NoisePower ("Noise Power", Range(0,5)) = 1
        [Toggle(OVERLAY2_MAINTEX_ALPHA)] _Overlay2MainTexAlpha("Main Tex Alpha", Float) = 0
        _Overlay2OffsetX ("Offset X", Range(-1,1)) = 0
        _Overlay2OffsetY ("Offset Y", Range(-1,1)) = 0
        _Overlay2MoveX ("Speed X", Range(-2,2)) = 0
        _Overlay2MoveY ("Speed Y", Range(-2,2)) = 0
        _Overlay2RotOffset("Overlay Rot Offset", Range(-360, 360)) = 0
        _Overlay2Rot ("Overlay Texuture Rot Speed", Range(-2, 2)) = 0
        [Toggle(OVERLAY2_FRAC)] _Overlay2Frac ("Overlay UV Fractional", Float) = 0
    	[Toggle(SHOW_OVERLAY2)] _ShowOverlay2 ("Show Overlay", Float) = 0

        //FLIPBOOK
        [Toggle(FLIPBOOK)] _UseFlipbook("FLIPBOOK", Float) = 0
        _FlipbookCellCount("Cell Count X", Range(1, 10)) = 6
        _FlipbookCellCountY("Cell Count Y", Range(1, 10)) = 6
        _FlipbookSpeed ("Frame Per Second", Range(0.1,60)) = 24
        [Toggle(FLIPBOOK_BLEND)] _FlipbookBlend ("Flipbook Blend", Float) = 1

        //WATER_FOAM
        [Toggle(WATER_FOAM)] _UseWaterFoam("WATER FOAM", Float) = 0
        _DepthColor ("Depth Color", Color) = (0.5,0.5,0.5,1.0)
		[PowerSlider(5.0)] _DepthDistance ("Depth Distance", Range(0.01,3)) = 0.5
        _FoamSpread ("Foam Spread", Range(0.01,5)) = 2
		_FoamStrength ("Foam Strength", Range(0.01,1)) = 0.8
		_FoamColor ("Foam Color (RGB) Opacity (A)", Color) = (1,1,1,1)
        _FoamTex ("Foam (RGB)", 2D) = "white" {}
		_FoamSmooth ("Foam Smoothness", Range(0,0.5)) = 0.02
		_FoamSpeed ("Foam Speed", Vector) = (-1,-1,-2,-2)

        // VERTEX_WAVE
        [Toggle(VERTEX_WAVE)] _UseVertexWave("VERTEX WAVE", Float) = 0
        _WaveSpeed ("Speed", Range(0,1)) = 0.5
		_WaveHeight ("Height", Range(0,1)) = 0.1
		_WaveFrequency ("Frequency", Range(0,1)) = 1
        [Toggle(WAVE_OVERLAY)] _WaveOverlay("Wave Overlay2", Float) = 0
        _WaveOverlayWeight ("Overlay2 Weight", Range(0,1)) = 1
        [Toggle(WAVE_OVERLAY2)] _WaveOverlay2("Wave Overlay2", Float) = 0
        _WaveOverlay2Weight ("Overlay2 Weight", Range(0,1)) = 1

        //BLUR
        [Toggle(BLUR)] _UseBlur("Use Blur", Float) = 0
        _BlurAmount("Blur Amount", Range(0, 1)) = 0.5
        [Toggle(BLUR_HIGH)] _BlurHigh("High Quality", Float) = 0

        //PIXELATE
        [Toggle(PIXELATE)] _UsePixelate("Use Pixelate", Float) = 0
        _PixelSize("Pixel Size", Range(0.001, 1)) = 0.1
        
        [Enum(UnityEngine.Rendering.BlendMode)] _Blend1("Src Factor", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)] _Blend2("Dst Factor", Float) = 10
        [Enum(None,0,Front,1,Back,2)] _CullMode("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    	[Toggle] _DepthWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull[_CullMode]
        Lighting Off
        ZTest [_ZTest]
        ZWrite [_DepthWrite]
        Blend[_Blend1][_Blend2]

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include_with_pragmas "Packages/com.doubleugames.tacomponents/Runtime/Shader/HLSLIncludes/DUG.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                half2 texcoord : TEXCOORD0;
                half2 texcoord1 : TEXCOORD1;
                half2 texcoord2 : TEXCOORD2;

            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                half2 texcoord : TEXCOORD0;
                half4 texcoord1 : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
#if WATER_FOAM
                float4 screenPosition : TEXCOORD3;
#endif
#if UNITY_UI_CLIP_RECT
        	    float4 rect_mask: TEXCOORD5;
#endif 
            };

#if WATER_FOAM
            TEXTURE2D(_CameraDepthTexture); 
            SAMPLER(sampler_CameraDepthTexture);

            half4 _DepthColor;
			half _DepthDistance;
			float4 _FoamSpeed;
			half _FoamSpread;
			half _FoamStrength;
			sampler2D _FoamTex;
            float4 _FoamTex_ST;
			half4 _FoamColor;
			half _FoamSmooth;
#endif
            
            v2f vert (appdata v)
            {
                v2f o;
                half4 atlas = half4(0,0,1,1);
#if CLAMP_ATLAS_UV
                atlas = half4(v.texcoord1, v.texcoord2);
#endif 
                float2 wind = GetWind(v.texcoord, atlas);
                v.vertex.xy += wind;

                o.texcoord = v.texcoord;
                o.texcoord1 = atlas;
                o.worldPosition = TransformObjectToWorld(v.vertex.xyz);

#if VERTEX_WAVE      
    #if defined(OVERLAY) && defined(WAVE_OVERLAY)
                float2 overlayUV = o.texcoord;
        #if OVERLAY_POLAR_COORD
                overlayUV = PolarCoordinates(overlayUV);
        #endif 
                overlayUV = GetOverlayUV(overlayUV);
                half4 overlay = tex2Dlod(_OverlayTex, float4(overlayUV, 0.0f, 0.0f));
                o.worldPosition.y += overlay.r * _WaveOverlayWeight;
    #endif
    #if defined(OVERLAY2) && defined(WAVE_OVERLAY2)
                float2 overlay2UV = o.texcoord;
        #if OVERLAY2_POLAR_COORD
                overlay2UV = PolarCoordinates(overlay2UV);
        #endif
                overlay2UV = GetOverlay2UV(overlay2UV);
                half4 overlay2 = tex2Dlod(_Overlay2Tex, float4(overlay2UV, 0.0f, 0.0f));
                o.worldPosition.y += overlay2.r * _WaveOverlay2Weight;
    #endif

                float3 wavePos = o.worldPosition * _WaveFrequency;
                float phase = _Time.y * _WaveSpeed;
                half4 vsw_offsets = half4(1.0, 2.2, 0.6, 1.3);
                half4 vsw_ph_offsets = half4(1.0, 1.3, 2.2, 0.4);
                half4 waveXZ = sin((wavePos.xxzz * vsw_offsets) + (phase.xxxx * vsw_ph_offsets));
                float waveFactorX = dot(waveXZ.xy, 1) * _WaveHeight / 2;
				float waveFactorZ = dot(waveXZ.zw, 1) * _WaveHeight / 2;
                o.worldPosition.y += (waveFactorX + waveFactorZ);
#endif 

                o.vertex = TransformWorldToHClip(o.worldPosition);

                half4 color = _Color;
#if IS_PMA
                color.rgb *= color.a * v.color.a;
#endif
                o.color = v.color * color;

#if UNITY_UI_CLIP_RECT
        	    float2 pixelSize = o.vertex.w;
        	    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
        	    o.rect_mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
#endif

#if WATER_FOAM
                float4 ndc = o.vertex * 0.5;
                o.screenPosition.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
                o.screenPosition.zw = o.vertex.zw;
#endif                

                return o;
            }

#if TWO_FACE
            half4 frag (v2f i, half facing : VFACE) : SV_Target
#else
	        half4 frag(v2f i) : SV_Target
#endif
            {
                float2 uv = i.texcoord.xy;
                float4 atlas = i.texcoord1;
                float2 atlasSize = float2(1,1);
#if CLAMP_ATLAS_UV
                float2 minUV = atlas.xy;
                float2 maxUV = atlas.zw;
                atlasSize = maxUV - minUV;
#endif

#if PIXELATE
                uv = ApplyPixelate(uv, _MainTex_TexelSize.zw, atlas);
#endif

                float2 uv01 = (uv - atlas.xy) / (atlas.zw - atlas.xy);
                half3 noise = GetNoise(uv01, i.worldPosition.xy, atlas);
                half mask = GetMask(uv01, noise.rg * _MaskNoisePower);
                float2 overlayUV = uv01;
#if OVERLAY_SCREEN_UV
                overlayUV = i.worldPosition.xy;
#endif
                half4 overlay = GetOverlay(overlayUV, noise.rg * _OverlayNoisePower);
#if defined(DISSOLVE) && defined(DIS_OVERLAY_ONLY)
                overlay = ApplyDissolve(overlay, uv01, i.worldPosition.xy, noise.rg * _DisNoisePower, 1);
#endif 

                float2 overlay2UV = uv01;
#if OVERLAY_SCREEN_UV2
                overlay2UV = i.worldPosition.xy;
#endif
                half4 overlay2 = GetOverlay2(overlay2UV, noise.rg * _Overlay2NoisePower);
#if defined(DISSOLVE) && defined(DIS_OVERLAY2_ONLY)
                overlay2 = ApplyDissolve(overlay2, uv01, i.worldPosition.xy, noise.rg * _DisNoisePower, 1);
#endif 

                uv = GetFlipbookUV(uv);
                uv = PolarMainTexCoordinates(uv);
                uv = MoveRotMainTex(uv, atlas);

                half4 outline = GetOutline(uv, noise.rg * atlasSize, mask, atlas) * i.color.a;
#if defined(DISSOLVE) && defined(DIS_OUTLINE_ONLY)
                outline = ApplyDissolve(outline, uv01, i.worldPosition.xy, noise.rg * _DisNoisePower, mask);
#endif 

                ////////////////////////////////
#if defined(NOISE) && (defined(NOISE_MASK_ONLY) || defined(NOISE_OUTLINE_ONLY) || defined(NOISE_DISSOLVE_ONLY) || defined(NOISE_OVERLAY_ONLY) || defined(NOISE_OVERLAY2_ONLY))
                uv = ClampAtlasUV(uv, atlas);
#else
                float2 noisedUV = uv + noise.rg * atlasSize;
                noisedUV = ClampAtlasUV(noisedUV, atlas);
                uv = lerp(uv, noisedUV, mask);
#endif

#if TWO_FACE
                half4 ret = GetTwoFaceTexture(uv, facing, atlas);
#else
    #if BLUR
                half4 ret = GetBlurMainTex(uv, mask, atlas);
    #else
        #if PIXELATE
                half4 ret = tex2Dgrad(_MainTex, uv, half2(0,0), half2(0,0));
        #else
                half4 ret = tex2D(_MainTex, uv);
        #endif
    #endif
#endif

#if FLIPBOOK && FLIPBOOK_BLEND
                ret = BlendFlipbook(uv01, ret, noise.rg);
#endif
                ////////////////////////////////

#if GRAYSCALE_MAIN_TEXTURE
                ret = lerp(_BlackColor, _WhiteColor, ret.r * ret.a);
#endif
                
                ret.rgb = ApplyGrayScale(ret.rgb, mask);

                ret *= i.color;

#if defined(ADDITIVE_COLOR) 
                half3 additiveColor;
    #if defined(ADDITIVE_OUTLINE_ONLY)
                additiveColor = GetAdditiveColor(outline, mask);
                outline.rgb = lerp(outline.rgb, outline.rgb + additiveColor, outline.a);
    #endif

    #if defined(ADDITIVE_OVERLAY_ONLY)
                additiveColor = GetAdditiveColor(overlay, mask);
                overlay.rgb = lerp(overlay.rgb, overlay.rgb + additiveColor, overlay.a);
    #endif 

    #if defined(ADDITIVE_OVERLAY2_ONLY)
                additiveColor = GetAdditiveColor(overlay2, mask);
                overlay2.rgb = lerp(overlay2.rgb, overlay2.rgb + additiveColor, overlay2.a);
    #endif 

    #if !defined(ADDITIVE_OUTLINE_ONLY) && !defined(ADDITIVE_OVERLAY_ONLY) && !defined(ADDITIVE_OVERLAY2_ONLY)
                additiveColor = GetAdditiveColor(ret, mask);
                ret.rgb += additiveColor;
    #endif
#else
                half3 additiveColor = GetAdditiveColor(ret, mask);
                ret.rgb += additiveColor;   
#endif

#if defined(NOISE)
                half3 noiseAdditiveColor = GetNoiseAdditiveColor(noise.b, mask, ret.rgb);
    #if defined(NOISE_OUTLINE_ONLY)
                outline.rgb = lerp(outline.rgb, outline.rgb + noiseAdditiveColor, outline.a);
    #endif

    #if defined(NOISE_OVERLAY_ONLY)
                overlay.rgb = lerp(overlay.rgb, overlay.rgb + noiseAdditiveColor, overlay.a);
    #endif 

    #if defined(NOISE_OVERLAY2_ONLY)
                overlay2.rgb = lerp(overlay2.rgb, overlay2.rgb + noiseAdditiveColor, overlay2.a);
    #endif 

    #if !defined(NOISE_OUTLINE_ONLY) && !defined(NOISE_OVERLAY_ONLY) && !defined(NOISE_OVERLAY2_ONLY)
        #if IS_PMA
                ret.rgb += lerp(ret.rgb, noiseAdditiveColor, ret.a);
        #else
                ret.rgb += noiseAdditiveColor;
        #endif
    #endif
#endif

#if defined(OVERLAY)
    #if defined(OVERLAY_MAINTEX_ALPHA)
        #if OVERLAY_ADDITIVE
                ret.rgb = lerp(ret.rgb, ret.rgb + overlay.rgb * i.color.rgb * _OverlayColor.rgb, _OverlayAlpha * overlay.a * mask);
        #else
                ret.rgb = lerp(ret.rgb, overlay.rgb * i.color.rgb * _OverlayColor.rgb, _OverlayAlpha * overlay.a * mask);
        #endif
    #else
        #if OVERLAY_ADDITIVE
                ret = lerp(ret, ret + overlay * i.color * _OverlayColor, _OverlayAlpha * overlay.a * mask);
        #else
                ret = lerp(ret, overlay * i.color * _OverlayColor, _OverlayAlpha * overlay.a * mask);
        #endif
    #endif
#endif

#if defined(OVERLAY2)
    #if defined(OVERLAY2_MAINTEX_ALPHA)
        #if OVERLAY2_ADDITIVE
                ret.rgb = lerp(ret.rgb, ret.rgb + overlay2.rgb * i.color.rgb * _Overlay2Color.rgb, _Overlay2Alpha * overlay2.a * mask);
        #else
                ret.rgb = lerp(ret.rgb, overlay2.rgb * i.color.rgb * _Overlay2Color.rgb, _Overlay2Alpha * overlay2.a * mask);
        #endif
    #else
        #if OVERLAY2_ADDITIVE
                ret = lerp(ret, ret + overlay2 * i.color * _Overlay2Color, _Overlay2Alpha * overlay2.a * mask);
        #else
                ret = lerp(ret, overlay2 * i.color * _Overlay2Color, _Overlay2Alpha * overlay2.a * mask);
        #endif
    #endif
#endif

#if OUTLINE
                ret = lerp(outline, ret, ret.a);
#endif

#if DISSOLVE
    #if !defined(DIS_OUTLINE_ONLY) && !defined(DIS_OVERLAY_ONLY) && !defined(DIS_OVERLAY2_ONLY)
                ret = ApplyDissolve(ret, uv01, i.worldPosition.xy, noise.rg * _DisNoisePower, mask);
    #endif
#endif 

                ret = ApplyMask(ret, mask);

#if UNITY_UI_CLIP_RECT
			    float2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.rect_mask.xy)) * i.rect_mask.zw);
			    half a = m.x * m.y;
			    ret *= a;
#endif

#if WATER_FOAM
                float sceneZ = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.screenPosition.xy / i.screenPosition.w).r;
    #if defined(UNITY_REVERSED_Z)
                sceneZ = 1.0f - sceneZ;
    #endif
                sceneZ = (sceneZ * _ProjectionParams.z) + _ProjectionParams.y;
                float partZ = TransformWorldToView(i.worldPosition).z;
                partZ = -partZ;
                float depthDiff = abs(sceneZ - partZ);

                float2 foamUV = uv01;
                foamUV *= _FoamTex_ST.xy;
                foamUV += _FoamTex_ST.zw;
                foamUV.xy += _Time.yy*_FoamSpeed.xy*0.05;
                float4 foam = tex2D(_FoamTex, foamUV);
                foamUV.xy += _Time.yy*_FoamSpeed.zw*0.05;
                float4 foam2 = tex2D(_FoamTex, foamUV);
                foam = (foam + foam2) / 2;
                float foamDepth = saturate(_FoamSpread * depthDiff);
                float foamTerm = (smoothstep(foam.r - _FoamSmooth, foam.r + _FoamSmooth, saturate(_FoamStrength - foamDepth)) * saturate(1 - foamDepth)) * _FoamColor.a;
                //Alter color based on depth buffer (soft particles technique)
                ret.rgb = lerp(_DepthColor.rgb, ret.rgb, saturate(_DepthDistance * depthDiff));	//N.V corrects the result based on view direction (depthDiff tends to not look consistent depending on view angle)));
                ret.rgb = lerp(ret.rgb, _FoamColor.rgb, foamTerm);
#endif 

                ////////////////////////////////
                // DEV
                ret = ShowNoise(ret, noise.b);
                ret = ShowMask(ret, mask);
                ret = ShowDissolve(ret, uv01, i.worldPosition.xy, noise.rg * _DisNoisePower);
                ret = ShowOverlay(ret, overlay);
                ret = ShowOverlay2(ret, overlay2);
#ifdef SHOW_UV
                ret = half4(uv, 0, 1);
#endif
#ifdef SHOW_RGB
                ret.a = 1;
#endif 

                ///////////////////////////////
                
                return ret;
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "Manual" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include_with_pragmas "Packages/com.doubleugames.tacomponents/Runtime/Shader/HLSLIncludes/DUG.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                half2 texcoord : TEXCOORD0;
                half2 texcoord1 : TEXCOORD1;
                half2 texcoord2 : TEXCOORD2;

            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                half2 texcoord : TEXCOORD0;
                half4 texcoord1 : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
#if UNITY_UI_CLIP_RECT
        	    float4 rect_mask: TEXCOORD5;
#endif 
            };

            v2f vert (appdata v)
            {
                v2f o;
                half4 atlas = half4(0,0,1,1);
#if CLAMP_ATLAS_UV
                atlas = half4(v.texcoord1, v.texcoord2);
#endif 
                float2 wind = GetWind(v.texcoord, atlas);
                v.vertex.xy += wind;

                o.texcoord = v.texcoord;
                o.texcoord1 = atlas;
                o.worldPosition = TransformObjectToWorld(v.vertex.xyz);

                o.vertex = TransformWorldToHClip(o.worldPosition);

                half4 color = _Color;
#if IS_PMA
                color.rgb *= color.a * v.color.a;
#endif
                o.color = v.color * color;

#if UNITY_UI_CLIP_RECT
        	    float2 pixelSize = o.vertex.w;
        	    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
        	    o.rect_mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
#endif     

                return o;
            }

	        half4 frag(v2f i) : SV_Target
            {
                half4 ret = half4(0, 0, 0, 0);
#if DISSOLVE
                float2 uv = i.texcoord.xy;
                ret = tex2D(_MainTex, uv);
                ret = ApplyDissolve2(ret, uv, i.worldPosition.xy, 1, 1);

    #if MASK
                half mask = GetMask(uv);
                ret.a = saturate(ret.a * mask);
    #endif
#endif                
                return ret;
            }

            ENDHLSL
        }
    }

    CustomEditor "DefaultGUI"
}

