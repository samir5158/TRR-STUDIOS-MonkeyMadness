Shader "PhotonVoiceApi/GLES2/VideoTextureExtScreen" {
    Properties{
        _MainTex("Texture", 2D) = "red" {}
        _Pos("Pos", Vector) = (0, 0, 0, 0)
        _Scale("Scale", Vector) = (1, 1, 1, 1)
        _Flip("Flip", Vector) = (1, 1, 0, 0)
    }

    GLSLINCLUDE
    #include "UnityCG.glslinc"
    ENDGLSL

    SubShader{
        Tags { "Queue" = "Overlay" } // render after everything else

        Pass {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha // use alpha blending
            ZTest Always // deactivate depth test
                
            GLSLPROGRAM

            #ifdef VERTEX

            uniform vec4 _Pos;
            uniform vec4 _Scale;
            uniform vec4 _Flip;

            varying vec2 vTextureCoord;

            void main()
            {
                gl_Position = gl_Vertex *_Scale + _Pos;
                gl_Position.z = -1.0;
                gl_Position.w = 1.0;
                vTextureCoord = (gl_MultiTexCoord0.st - vec2(0.5, 0.5)) * _Flip.xy + vec2(0.5, 0.5);
            }

            #endif

            #ifdef FRAGMENT

            // SHADER_API_GLES3 does not work here
            #extension GL_OES_EGL_image_external : require

            precision mediump float;
            uniform samplerExternalOES _MainTex;
            varying vec2 vTextureCoord;
            void main() {
#ifdef SHADER_API_GLES3
                gl_FragColor = texture(_MainTex, vTextureCoord);
#else
                gl_FragColor = textureExternal(_MainTex, vTextureCoord);
#endif
                //gl_FragColor = vec4(vTextureCoord.x, vTextureCoord.y,0,1);
            }

            #endif

            ENDGLSL
        }
    }
}
