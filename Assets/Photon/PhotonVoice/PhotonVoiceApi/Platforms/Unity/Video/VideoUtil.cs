using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Photon.Voice.Unity
{
    public static class VideoUtil
    {
        public static Dictionary<ImageFormat, TextureFormat> ImageFormatToUnityTextureFormat = new Dictionary<ImageFormat, TextureFormat>()
        {
            { ImageFormat.ABGR, TextureFormat.RGBA32 },
            { ImageFormat.BGRA, TextureFormat.ARGB32 },
            { ImageFormat.ARGB, TextureFormat.BGRA32 },
            { ImageFormat.NV12, TextureFormat.BGRA32 },
            { ImageFormat.I420, TextureFormat.BGRA32 },
        };
    }
}