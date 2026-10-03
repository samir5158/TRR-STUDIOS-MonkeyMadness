#if PHOTON_VOICE_VIDEO_ENABLE
using System;
using UnityEngine;

namespace Photon.Voice.Unity
{
    static public class VideoTexture
    {
        static public class Shader3D
        {
            static public string Name
            {
                get
                {
                    switch (Application.platform)
                    {
                        case RuntimePlatform.Android:
                            switch (SystemInfo.graphicsDeviceType)
                            {
                                #if !UNITY_2023_1_OR_NEWER
                                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2:
                                    return "PhotonVoiceApi/GLES2/VideoTextureExt3D";
                                #endif
                                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:
                                    return "PhotonVoiceApi/GLES3/VideoTextureExt3D";
                                default:
                                    return "PhotonVoiceApi/VideoTexture3D";
                            }
                        default:
                            return "PhotonVoiceApi/VideoTexture3D";
                    }
                }
            }

            static public Material MakeMaterial(Texture tex, Flip flip)
            {
                if (Name == null)
                {
                    throw new UnsupportedPlatformException("VideoTexture.Shader3D.MakeMaterial: shader", Application.platform + "/" + SystemInfo.graphicsDeviceType);
                }
                else
                {
                    var shader = Resources.Load<Shader>(Name);
                    if (shader == null)
                    {
                        throw new Exception("VideoTexture.Shader3D.MakeMaterial: shader resource " + Name + " fails to load");
                    }
                    var mat = new Material(shader);
                    mat.SetTexture("_MainTex", tex);
                    mat.SetVector("_Flip", new Vector4(flip.IsHorizontal ? -1 : 1, flip.IsVertical ? -1 : 1, 0, 0));
                    return mat;
                }
            }
        }

        static public class ShaderScreen
        {
            static public string Name
            {
                get
                {
                    switch (Application.platform)
                    {
                        case RuntimePlatform.Android:

                            switch (SystemInfo.graphicsDeviceType)
                            {
                                #if !UNITY_2023_1_OR_NEWER
                                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2:
                                    return "PhotonVoiceApi/GLES2/VideoTextureExtScreen";
                                #endif
                                case UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3:
                                    return "PhotonVoiceApi/GLES3/VideoTextureExtScreen";
                                default:
                                    return "PhotonVoiceApi/VideoTextureScreen";
                            }
                        default:
                            return "PhotonVoiceApi/VideoTextureScreen";
                    }
                }
            }
        }
    }
}
#endif
