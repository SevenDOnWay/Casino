Shader "Custom/ClockWipeSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _FillColor ("Empty Area Color", Color) = (0,0,0,0.5)
        _ShowEmpty ("Show Empty Area (0=Clip, 1=Tint)", Float) = 0
        _Clockwise ("Clockwise (1=yes, 0=counter)", Float) = 1
        _EdgeSmooth ("Edge Smoothness", Range(0, 0.1)) = 0.005
        // Legacy sprite properties for batching
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
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
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FillColor;
                float _FillAmount;
                float _ShowEmpty;
                float _Clockwise;
                float _EdgeSmooth;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // 0 at top (12 o'clock), increases clockwise to 1.
            float ClockAngle01(float2 uv)
            {
                float2 d = uv - float2(0.5, 0.5);
                // atan2(x, y): 0 = up, + = clockwise
                float a = atan2(d.x, d.y) / 6.28318530718; // -0.5..0.5
                if (a < 0.0) a += 1.0;
                return a;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * IN.color;

                float ang = ClockAngle01(IN.uv);
                if (_Clockwise < 0.5)
                    ang = 1.0 - ang;

                float fill = saturate(_FillAmount);
                float w = fwidth(ang) * 1.5 + _EdgeSmooth;

                // 1 inside filled wedge, 0 outside
                float mask = 1.0 - smoothstep(fill - w, fill + w, ang);

                // Full / empty fast paths keep crisp sprite alpha
                if (fill >= 0.999) mask = 1.0;
                if (fill <= 0.001) mask = 0.0;

                if (_ShowEmpty < 0.5)
                {
                    // Clip mode: discard pixels outside the wedge
                    if (mask <= 0.0) discard;
                    col.a *= mask;
                    return col;
                }
                else
                {
                    half4 empty = _FillColor;
                    empty.a *= tex.a * IN.color.a;
                    half4 result = lerp(empty, col, mask);
                    return result;
                }
            }
            ENDHLSL
        }

        // Fallback pass for the 3D (UniversalForward) renderer so the
        // material still works if the project switches away from Renderer2D.
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FillColor;
                float _FillAmount;
                float _ShowEmpty;
                float _Clockwise;
                float _EdgeSmooth;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float ClockAngle01(float2 uv)
            {
                float2 d = uv - float2(0.5, 0.5);
                float a = atan2(d.x, d.y) / 6.28318530718;
                if (a < 0.0) a += 1.0;
                return a;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * IN.color;

                float ang = ClockAngle01(IN.uv);
                if (_Clockwise < 0.5)
                    ang = 1.0 - ang;

                float fill = saturate(_FillAmount);
                float w = fwidth(ang) * 1.5 + _EdgeSmooth;
                float mask = 1.0 - smoothstep(fill - w, fill + w, ang);

                if (fill >= 0.999) mask = 1.0;
                if (fill <= 0.001) mask = 0.0;

                if (_ShowEmpty < 0.5)
                {
                    if (mask <= 0.0) discard;
                    col.a *= mask;
                    return col;
                }
                else
                {
                    half4 empty = _FillColor;
                    empty.a *= tex.a * IN.color.a;
                    return lerp(empty, col, mask);
                }
            }
            ENDHLSL
        }
    }
}
