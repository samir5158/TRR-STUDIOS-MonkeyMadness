// Supports Single-Pass Stereo Rendering for HoloLens: 
// https://docs.unity3d.com/Manual/SinglePassStereoRenderingHoloLens.html
// NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'

Shader "PhotonVoiceApi/VideoTextureScreen" {
    Properties{
        _MainTex("Texture", 2D) = "red" {}
        _Pos("Pos", Vector) = (0, 0, 0, 0)
        _Scale("Scale", Vector) = (1, 1, 1, 1)
        _Flip("Flip", Vector) = (1, 1, 0, 0)
    }

    SubShader{
        Tags { "Queue" = "Overlay" } // render after everything else

        Pass {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha // use alpha blending
            ZTest Always // deactivate depth test

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Pos;
            float4 _Scale;
            float4 _Flip;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);                
                o.vertex = v.vertex * _Scale + _Pos;
                o.vertex.z = 1;
                o.vertex.w = 1;
                o.uv = (v.uv - float2(0.5, 0.5)) * float2(1, -1) * _Flip.xy + float2(0.5, 0.5);

// Compensates for the vertical flip that Unity does when anti-aliasing is enabled in quality settings
// https://docs.unity3d.com/2018.3/Documentation/Manual/SL-PlatformDifferences.html (Rendering in UV space)
                if (_ProjectionParams.x < 0)
                        o.vertex.y = -o.vertex.y;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }

            ENDCG
        }
    }
}
