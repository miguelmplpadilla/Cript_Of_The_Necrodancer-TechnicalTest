Shader "Custom/UI Inside Stencil Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ParentTex ("Parent Texture", 2D) = "white" {}
        _StencilRef ("Stencil Ref", Range(1, 255)) = 1
        _ParentShadeStrength ("Parent Shade Strength", Range(0, 1)) = 1
        _ParentTintStrength ("Parent Tint Strength", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 3)) = 1
        _Saturation ("Color Intensity", Range(0, 3)) = 1

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
            "Queue" = "Transparent+1"
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

        Stencil
        {
            Ref [_StencilRef]
            Comp Equal
            Pass Keep
            ReadMask 255
            WriteMask 255
        }

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
            sampler2D _ParentTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            half _ParentShadeStrength;
            half _ParentTintStrength;
            half _Brightness;
            half _Saturation;

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

            fixed4 Frag(v2f IN) : SV_Target
            {
                fixed4 childColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                fixed4 parentColor = tex2D(_ParentTex, IN.texcoord);
                half parentLuma = dot(parentColor.rgb, half3(0.299, 0.587, 0.114));
                fixed3 shadedColor = childColor.rgb * parentLuma;
                fixed3 tintedColor = childColor.rgb * parentColor.rgb;

                childColor.rgb = lerp(childColor.rgb, shadedColor, _ParentShadeStrength);
                childColor.rgb = lerp(childColor.rgb, tintedColor, _ParentTintStrength);
                childColor.rgb *= _Brightness;

                half childLuma = dot(childColor.rgb, half3(0.299, 0.587, 0.114));
                childColor.rgb = lerp(childLuma.xxx, childColor.rgb, _Saturation);

                #ifdef UNITY_UI_CLIP_RECT
                childColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(childColor.a - 0.001);
                #endif

                return childColor;
            }
            ENDCG
        }
    }
}
