Shader "Custom/UI Color List Stencil Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [IntRange] _MaskColorCount ("Mask Color Count", Range(0, 8)) = 1
        _MaskColor0 ("Mask Color 0", Color) = (1,0,1,1)
        _MaskColor1 ("Mask Color 1", Color) = (1,1,1,1)
        _MaskColor2 ("Mask Color 2", Color) = (1,1,1,1)
        _MaskColor3 ("Mask Color 3", Color) = (1,1,1,1)
        _MaskColor4 ("Mask Color 4", Color) = (1,1,1,1)
        _MaskColor5 ("Mask Color 5", Color) = (1,1,1,1)
        _MaskColor6 ("Mask Color 6", Color) = (1,1,1,1)
        _MaskColor7 ("Mask Color 7", Color) = (1,1,1,1)

        _Tolerance ("Color Tolerance", Range(0, 1)) = 0.01
        _Tolerance255 ("Color Tolerance 0-255", Range(0, 32)) = 1
        _Softness ("Mask Softness", Range(0, 1)) = 0
        [Toggle] _UseTintForMask ("Use Tint For Mask", Float) = 0
        [Toggle] _UseGammaColorMatch ("Use Gamma Color Match", Float) = 1
        [Toggle] _DebugMask ("Debug Mask Preview", Float) = 0
        [Toggle] _InvertMask ("Invert Mask", Float) = 0
        _StencilRef ("Stencil Ref", Range(1, 255)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            half _MaskColorCount;
            fixed4 _MaskColor0;
            fixed4 _MaskColor1;
            fixed4 _MaskColor2;
            fixed4 _MaskColor3;
            fixed4 _MaskColor4;
            fixed4 _MaskColor5;
            fixed4 _MaskColor6;
            fixed4 _MaskColor7;
            half _Tolerance;
            half _Tolerance255;
            half _Softness;
            fixed _UseTintForMask;
            fixed _UseGammaColorMatch;
            fixed _DebugMask;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            v2f Vert(appdata IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half MatchSingleColor(fixed3 spriteColor, fixed4 maskColor, half colorSlot)
            {
                half edge = max(max(_Tolerance, _Tolerance255 / 255.0), 0.5 / 255.0);
                half softness = max(_Softness, 0.0001);
                fixed3 compareSpriteColor = spriteColor;
                fixed3 compareMaskColor = maskColor.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                compareSpriteColor = lerp(compareSpriteColor, LinearToGammaSpace(compareSpriteColor), _UseGammaColorMatch);
                compareMaskColor = lerp(compareMaskColor, LinearToGammaSpace(compareMaskColor), _UseGammaColorMatch);
                #endif
                fixed3 quantizedSpriteColor = floor(saturate(compareSpriteColor) * 255.0 + 0.5) / 255.0;
                fixed3 quantizedMaskColor = floor(saturate(compareMaskColor) * 255.0 + 0.5) / 255.0;
                fixed3 colorDifference = abs(quantizedSpriteColor - quantizedMaskColor);
                half colorDistance = max(max(colorDifference.r, colorDifference.g), colorDifference.b);
                half enabled = step(colorSlot, _MaskColorCount);

                if (_Softness <= 0.0001)
                    return step(colorDistance, edge) * enabled;

                return (1 - smoothstep(edge, edge + softness, colorDistance)) * enabled;
            }

            half MatchColorMask(fixed3 spriteColor)
            {
                half mask = 0;
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor0, 1));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor1, 2));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor2, 3));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor3, 4));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor4, 5));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor5, 6));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor6, 7));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor7, 8));

                return mask;
            }

            fixed4 Frag(v2f IN) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                fixed4 color = textureColor * IN.color;
                fixed3 colorForMask = lerp(textureColor.rgb, color.rgb, _UseTintForMask);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                if (_DebugMask > 0.5)
                {
                    half mask = MatchColorMask(colorForMask);
                    return fixed4(mask, 0, 1 - mask, color.a);
                }

                return color;
            }
            ENDCG
        }

        Pass
        {
            ColorMask 0
            Blend One Zero

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
                ReadMask 255
                WriteMask 255
            }

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment MaskFrag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            half _MaskColorCount;
            fixed4 _MaskColor0;
            fixed4 _MaskColor1;
            fixed4 _MaskColor2;
            fixed4 _MaskColor3;
            fixed4 _MaskColor4;
            fixed4 _MaskColor5;
            fixed4 _MaskColor6;
            fixed4 _MaskColor7;
            half _Tolerance;
            half _Tolerance255;
            half _Softness;
            fixed _UseTintForMask;
            fixed _UseGammaColorMatch;
            fixed _DebugMask;
            fixed _InvertMask;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            v2f Vert(appdata IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half MatchSingleColor(fixed3 spriteColor, fixed4 maskColor, half colorSlot)
            {
                half edge = max(max(_Tolerance, _Tolerance255 / 255.0), 0.5 / 255.0);
                half softness = max(_Softness, 0.0001);
                fixed3 compareSpriteColor = spriteColor;
                fixed3 compareMaskColor = maskColor.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                compareSpriteColor = lerp(compareSpriteColor, LinearToGammaSpace(compareSpriteColor), _UseGammaColorMatch);
                compareMaskColor = lerp(compareMaskColor, LinearToGammaSpace(compareMaskColor), _UseGammaColorMatch);
                #endif
                fixed3 quantizedSpriteColor = floor(saturate(compareSpriteColor) * 255.0 + 0.5) / 255.0;
                fixed3 quantizedMaskColor = floor(saturate(compareMaskColor) * 255.0 + 0.5) / 255.0;
                fixed3 colorDifference = abs(quantizedSpriteColor - quantizedMaskColor);
                half colorDistance = max(max(colorDifference.r, colorDifference.g), colorDifference.b);
                half enabled = step(colorSlot, _MaskColorCount);

                if (_Softness <= 0.0001)
                    return step(colorDistance, edge) * enabled;

                return (1 - smoothstep(edge, edge + softness, colorDistance)) * enabled;
            }

            half MatchColorMask(fixed3 spriteColor)
            {
                half mask = 0;
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor0, 1));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor1, 2));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor2, 3));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor3, 4));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor4, 5));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor5, 6));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor6, 7));
                mask = max(mask, MatchSingleColor(spriteColor, _MaskColor7, 8));

                if (_InvertMask > 0.5)
                    mask = 1 - mask;

                return mask;
            }

            fixed4 MaskFrag(v2f IN) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                fixed4 color = textureColor * IN.color;
                fixed3 colorForMask = lerp(textureColor.rgb, color.rgb, _UseTintForMask);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                half mask = MatchColorMask(colorForMask);
                if (_DebugMask > 0.5)
                    return fixed4(mask, 0, 1 - mask, color.a);

                clip((color.a * mask) - 0.001);

                return 0;
            }
            ENDCG
        }
    }
}
