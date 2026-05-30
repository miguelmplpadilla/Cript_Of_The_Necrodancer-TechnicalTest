Shader "Custom/Sprite Inside Stencil Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilRef ("Stencil Ref", Range(1, 255)) = 1
        _ParentShadeStrength ("Parent Shade Strength", Range(0, 1)) = 1
        _ParentTintStrength ("Parent Tint Strength", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 3)) = 1
        _Saturation ("Color Intensity", Range(0, 3)) = 1
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
        Blend One OneMinusSrcAlpha

        Stencil
        {
            Ref [_StencilRef]
            Comp Equal
            Pass Keep
        }

        GrabPass
        {
            "_SpriteParentColorTexture"
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            sampler2D _SpriteParentColorTexture;
            half _ParentShadeStrength;
            half _ParentTintStrength;
            half _Brightness;
            half _Saturation;

            struct v2f_parent_shaded
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            v2f_parent_shaded Vert(appdata_t IN)
            {
                v2f sprite = SpriteVert(IN);
                v2f_parent_shaded OUT;

                OUT.vertex = sprite.vertex;
                OUT.color = sprite.color;
                OUT.texcoord = sprite.texcoord;
                OUT.screenPos = ComputeGrabScreenPos(sprite.vertex);

                return OUT;
            }

            fixed4 Frag(v2f_parent_shaded IN) : SV_Target
            {
                fixed4 childColor = SampleSpriteTexture(IN.texcoord) * IN.color;
                fixed4 parentColor = tex2Dproj(_SpriteParentColorTexture, UNITY_PROJ_COORD(IN.screenPos));
                half parentLuma = dot(parentColor.rgb, half3(0.299, 0.587, 0.114));
                fixed3 shadedColor = childColor.rgb * parentLuma;
                fixed3 tintedColor = childColor.rgb * parentColor.rgb;

                childColor.rgb = lerp(childColor.rgb, shadedColor, _ParentShadeStrength);
                childColor.rgb = lerp(childColor.rgb, tintedColor, _ParentTintStrength);
                childColor.rgb *= _Brightness;

                half childLuma = dot(childColor.rgb, half3(0.299, 0.587, 0.114));
                childColor.rgb = lerp(childLuma.xxx, childColor.rgb, _Saturation);
                childColor.rgb *= childColor.a;

                return childColor;
            }
            ENDCG
        }
    }
}
