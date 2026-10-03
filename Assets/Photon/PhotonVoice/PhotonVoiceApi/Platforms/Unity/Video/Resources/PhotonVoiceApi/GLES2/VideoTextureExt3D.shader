Shader "PhotonVoiceApi/GLES2/VideoTextureExt3D" {
    Properties{
        _MainTex("Texture", 2D) = "red" {}
        _Flip("Flip", Vector) = (1, 1, 0, 0)
    }

    GLSLINCLUDE
    #include "UnityCG.glslinc"
    ENDGLSL

    // https://stackoverflow.com/questions/25618977/how-to-render-to-a-gl-texture-external-oes
    // https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/DefaultResourcesExtra/VideoDecodeAndroid.shader
    SubShader{
        Pass {
            GLSLPROGRAM

            #ifdef VERTEX

            uniform vec4 _Flip;

            varying vec2 vTextureCoord;
            void main() {
                gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
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
                //gl_FragColor = vec4(vTextureCoord.x, vTextureCoord.y, 0, 1);
            }
            #endif

            ENDGLSL
        }
    }
}