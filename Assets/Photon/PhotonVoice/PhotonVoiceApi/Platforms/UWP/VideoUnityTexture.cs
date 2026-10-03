#if PHOTON_VOICE_VIDEO_ENABLE
#if UNITY_5_3_OR_NEWER // #if UNITY
#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT
using System;
using System.Runtime.InteropServices;
using Windows.Media.MediaProperties;
using Windows.Media.Core;
using System.Collections.Concurrent;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Playback;
using UnityEngine;

namespace Photon.Voice.UWP
{
    internal static class UnitySharedVideoTexture
    {
        [DllImport("UnitySharedVideoTexture", CallingConvention = CallingConvention.StdCall)]
        internal static extern int CreateSharedTexture();

        [DllImport("UnitySharedVideoTexture", CallingConvention = CallingConvention.StdCall)]
        internal static extern void ReleaseSharedTexture(int handle);

        [DllImport("UnitySharedVideoTexture", CallingConvention = CallingConvention.StdCall)]
        internal static extern void GetPrimaryTexture(int handle, UInt32 width, UInt32 height, out System.IntPtr playbackTexture, out IDirect3DSurface mediaSurface);
    };

    public class VideoEncoderUnityTexture : VideoEncoder
    {
        IDirect3DSurface mediaSurface;
        int sharedSurfaceHandle = 0;

        public Texture2D PreviewUnityTexture { get; private set; }

        public VideoEncoderUnityTexture(ILogger logger, VoiceInfo info, string deviceID)
            : base(logger, info, deviceID)
        {
            sharedSurfaceHandle = UnitySharedVideoTexture.CreateSharedTexture();
            IntPtr nativeTex = IntPtr.Zero;
            UnitySharedVideoTexture.GetPrimaryTexture(sharedSurfaceHandle, (uint)info.Width, (uint)info.Height, out nativeTex, out mediaSurface);
            PreviewUnityTexture = Texture2D.CreateExternalTexture(info.Width, info.Height, TextureFormat.BGRA32, false, false, nativeTex);
            mediaPlayer.IsVideoFrameServerEnabled = true;
            mediaPlayer.VideoFrameAvailable += this.onVideoFrameAvailable;

            if (Error == null)
            {
                logger.Log(LogLevel.Info, "[PV] [VE] VideoEncoderUnityTexture created");
            }
        }

        private void onVideoFrameAvailable(MediaPlayer sender, object args)
        {
            sender.CopyFrameToVideoSurface(mediaSurface);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (sharedSurfaceHandle != 0)
            {
                UnitySharedVideoTexture.ReleaseSharedTexture(sharedSurfaceHandle);
                sharedSurfaceHandle = 0;
            }
            logger.Log(LogLevel.Info, "[PV] [VE] VideoEncoderUnityTexture disposed");
        }
    }

    public class VideoDecoderUnityTexture : VideoDecoder
    {
        IDirect3DSurface mediaSurface;
        int sharedSurfaceHandle = 0;

        public Texture2D PreviewUnityTexture { get; private set; }

        public VideoDecoderUnityTexture(ILogger logger, VoiceInfo info) : base(logger, info)
        {
            sharedSurfaceHandle = UnitySharedVideoTexture.CreateSharedTexture();
            IntPtr nativeTex = IntPtr.Zero;
            UnitySharedVideoTexture.GetPrimaryTexture(sharedSurfaceHandle, (uint)info.Width, (uint)info.Height, out nativeTex, out mediaSurface);
            PreviewUnityTexture = Texture2D.CreateExternalTexture(info.Width, info.Height, TextureFormat.BGRA32, false, false, nativeTex);
            mediaPlayer.IsVideoFrameServerEnabled = true;
            mediaPlayer.VideoFrameAvailable += this.onVideoFrameAvailable;

            if (Error == null)
            {
                logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoderUnityTexture created");
            }
        }

        private void onVideoFrameAvailable(MediaPlayer sender, object args)
        {
            sender.CopyFrameToVideoSurface(mediaSurface);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (sharedSurfaceHandle != 0)
            {
                UnitySharedVideoTexture.ReleaseSharedTexture(sharedSurfaceHandle);
                sharedSurfaceHandle = 0;
            }
            logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoderUnityTexture disposed");
        }
    }

    public class VideoRecorderUnityTexture : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;
        public object PlatformView { get; private set; }

        public int Width { get { return ((VideoEncoderUnityTexture)Encoder).Width; } }
        public int Height { get { return ((VideoEncoderUnityTexture)Encoder).Height; } }

        public string Error => Encoder.Error;

        public VideoRecorderUnityTexture(ILogger logger, VoiceInfo info, string deviceID, Action<IVideoRecorder> onReady)
        {
            var e = new VideoEncoderUnityTexture(logger, info, deviceID);
            Encoder = e;
            PlatformView = e.PreviewUnityTexture;

            onReady(this);
        }

        public void Dispose()
        {
            if (Encoder != null)
            {
                Encoder.Dispose();
            }
        }
    }

    public class VideoPlayerUnityTexture : IVideoPlayer
    {
        public IDecoder Decoder { get; protected set; }
        public object PlatformView { get; protected set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;

        public int Width { get; protected set; }
        public int Height { get; protected set; }

        Texture2D previewTexture;

        public VideoPlayerUnityTexture(ILogger logger, VoiceInfo info, Action<IVideoPlayer> onReady)
        {
            var d = new VideoDecoderUnityTexture(logger, info);
            Decoder = d;

            this.PlatformView = d.PreviewUnityTexture;

            Width = info.Width;
            Height = info.Height;

            onReady(this);
        }

        public void Dispose()
        {
            if (Decoder != null)
            {
                Decoder.Dispose();
            }
        }
    }
}
#endif
#endif
#endif
