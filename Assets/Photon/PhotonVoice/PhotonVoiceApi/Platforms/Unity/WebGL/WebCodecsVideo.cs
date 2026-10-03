#if PHOTON_VOICE_VIDEO_ENABLE
#if UNITY_WEBGL && UNITY_2021_2_OR_NEWER // requires ES6
using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

namespace Photon.Voice.Unity
{
    public class WebCodecsVideoEncoder : IEncoder
    {
        const string lib_name = "__Internal";

        // used internally by jslib to share the config table between functions
        // declared here to avoid dropping during build
        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebCodecsVideo_EncoderConfig();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoEncoder_Start(IntPtr managedEncObj, string codec, int width, int height, int bitrate, Action<IntPtr, int, int> createCallbackStatic, Action<IntPtr, IntPtr, int, bool> dataCallbackStatic);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoEncoder_Stop(int nativeEncoder);

        protected GCHandle managedEncObj;
        public int NativeEncoder { private set; get; }
        private Action onReady;

        [MonoPInvokeCallbackAttribute(typeof(Action<IntPtr, int, int>))]
        static void createCallbackStatic(IntPtr managedCamObj, int encoderID, int err)
        {
            (GCHandle.FromIntPtr(managedCamObj).Target as WebCodecsVideoEncoder).createCallback(encoderID, err);
        }

        void createCallback(int encoderID, int err)
        {
            if (err == 0)
            {
                NativeEncoder = encoderID;
            }
            else
            {
                Error = "Encoder creation error " + err;
            }
            if (onReady != null)
            {
                onReady();
            }
        }

        [MonoPInvokeCallbackAttribute(typeof(Action<IntPtr, IntPtr, int, bool>))]
        static void dataCallbackStatic(IntPtr managedEncObj, IntPtr data, int len, bool keyframe)
        {
            (GCHandle.FromIntPtr(managedEncObj).Target as WebCodecsVideoEncoder).dataCallback(data, len, keyframe);
        }
        private byte[] buf;
        void dataCallback(IntPtr data, int len, bool keyframe)
        {
            if (Output != null)
            {
                if (buf == null || buf.Length < len)
                {
                    buf = new byte[len];
                }
                Marshal.Copy(data, buf, 0, len);
                Output(new ArraySegment<byte>(buf, 0, len), keyframe ? FrameFlags.KeyFrame : 0);
            }
        }

        public WebCodecsVideoEncoder(ILogger logger, VoiceInfo info, Action onReady)
        {
            this.onReady = onReady;
            managedEncObj = GCHandle.Alloc(this, GCHandleType.Normal);
            PhotonVoice_WebCodecsVideoEncoder_Start(GCHandle.ToIntPtr(managedEncObj), info.Codec.ToString(), info.Width, info.Height, info.Bitrate, createCallbackStatic, dataCallbackStatic);
            // mipmaps are not updated from video source on WebGL, false to avoid mipmaps usage
        }

        public string Error { get; protected set; }

        public Action<ArraySegment<byte>, FrameFlags> Output { set; protected get; }

        private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
        public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
        {
            flags = 0;
            return EmptyBuffer;
        }
        public void EndOfStream()
        {
        }

        public I GetPlatformAPI<I>() where I : class
        {
            return null;
        }

        public virtual void Dispose()
        {
            PhotonVoice_WebCodecsVideoEncoder_Stop(NativeEncoder);
            if (managedEncObj.Target != null)
            {
                // managedEncObj.Free(); // Triggers "Handle is not allocated" error
            }
        }
    }

    // not tested, not used
    public class WebCodecsVideoEncoderDirectImage : WebCodecsVideoEncoder, IEncoderDirectImage
    {
        const string lib_name = "__Internal";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebCodecsVideoEncoder_Input(IntPtr managedEncObj, IntPtr data, int width, int height, bool keyframe);

        public WebCodecsVideoEncoderDirectImage(ILogger logger, VoiceInfo info, Action onReady) : base(logger, info, onReady)
        {
            keyFrameInt = info.KeyFrameInt;
        }

        public ImageFormat ImageFormat { get { return ImageFormat.ARGB; } }

        public void Input(ImageBufferNative buf)
        {
            PhotonVoice_WebCodecsVideoEncoder_Input(GCHandle.ToIntPtr(managedEncObj), buf.Planes[0], buf.Info.Width, buf.Info.Height, frameCnt++ % keyFrameInt == 0);
        }

        int keyFrameInt;
        int frameCnt;
    }

    public class WebCodecsVideoCapture : IDisposable
    {
        const string lib_name = "__Internal";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebCodecsVideoCapture_Start();

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool PhotonVoice_WebCodecsVideoCapture_SetEncoderAndPreview(IntPtr managedCamObj, int nativeEncoder, int previewTexture);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoCapture_Stop(IntPtr managedCamObj);

        [MonoPInvokeCallbackAttribute(typeof(Action<IntPtr, int>))]
        protected static void createCallbackStatic(IntPtr managedCamObj, int err)
        {
            (GCHandle.FromIntPtr(managedCamObj).Target as WebCodecsVideoCapture).createCallback(err);
        }

        [MonoPInvokeCallbackAttribute(typeof(Action<IntPtr, int, int>))]
        protected static void changeCallbackStatic(IntPtr managedCamObj, int width, int height)
        {
            (GCHandle.FromIntPtr(managedCamObj).Target as WebCodecsVideoCapture).changeCallback(width, height);
        }

        Action onReady;
        Action<int, int> onChange;
        protected GCHandle managedCamObj;

        protected virtual string Prefix => "VideoCapture";

        void createCallback(int err)
        {
            if (err != 0)
            {
                Error = Prefix + " creation error " + err;
            }
            if (onReady != null)
            {
                onReady();
            }
        }

        void changeCallback(int width, int height)
        {
            if (onChange != null)
            {
                onChange(width, height);
            }
        }

        public WebCodecsVideoCapture(ILogger logger, Action onReady, Action<int, int> onChange)
        {
            this.onReady = onReady;
            this.onChange = onChange;
            managedCamObj = GCHandle.Alloc(this, GCHandleType.Normal);
        }

        public bool SetEncoderAndPreview(WebCodecsVideoEncoder encoder, UnityEngine.Texture2D preview)
        {
            return PhotonVoice_WebCodecsVideoCapture_SetEncoderAndPreview(GCHandle.ToIntPtr(managedCamObj), encoder.NativeEncoder, preview.GetNativeTexturePtr().ToInt32());
        }

        public string Error;

        public void Dispose()
        {
            PhotonVoice_WebCodecsVideoCapture_Stop(GCHandle.ToIntPtr(managedCamObj));
            if (managedCamObj.Target != null)
            {
                // managedEncObj.Free(); // Triggers "Handle is not allocated" error
            }

        }
    }

    public class WebCodecsCamera : WebCodecsVideoCapture
    {
        const string lib_name = "__Internal";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoCapture_CameraStart(IntPtr managedCamObj, string deviceId, int width, int height, int framerate, int keyframeInterval, Action<IntPtr, int> createCallbackStatic, Action<IntPtr, int, int> changeCallbackStatic);

        protected override string Prefix => "Camera";

        public WebCodecsCamera(ILogger logger, VoiceInfo info, string deviceId, Action onReady, Action<int, int> onChange) : base(logger, onReady, onChange)
        {
            PhotonVoice_WebCodecsVideoCapture_CameraStart(GCHandle.ToIntPtr(managedCamObj), deviceId, info.Width, info.Height, info.FPS, info.KeyFrameInt, createCallbackStatic, changeCallbackStatic);
        }
    }

    public class WebCodecsScreenShare : WebCodecsVideoCapture
    {
        const string lib_name = "__Internal";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoCapture_ScreenShareStart(IntPtr managedCamObj, int width, int height, int framerate, int keyframeInterval, Action<IntPtr, int> createCallbackStatic, Action<IntPtr, int, int> changeCallbackStatic);

        protected override string Prefix => "ScreenShare";

        public WebCodecsScreenShare(ILogger logger, VoiceInfo info, VideoSourceSizeMode sizeMode, Action onReady, Action<int, int> onChange) : base(logger, onReady, onChange)
        {
            var w = info.Width;
            var h = info.Height;
            if (sizeMode.Init == VideoSourceSizeMode.Mode.Source)
            {
                w = h = 0;
            }

            PhotonVoice_WebCodecsVideoCapture_ScreenShareStart(GCHandle.ToIntPtr(managedCamObj), w, h, info.FPS, info.KeyFrameInt, createCallbackStatic, changeCallbackStatic);
        }
    }

    abstract public class WebCodecsVideoRecorderUnityTexture : IVideoRecorder
    {
        VoiceInfo info;
        WebCodecsVideoCapture capture;
        WebCodecsVideoEncoder encoder;
        UnityEngine.Texture2D preview;

        public WebCodecsVideoRecorderUnityTexture(ILogger logger, VoiceInfo voiceInfo, VideoSourceSizeMode sizeMode, Func<Action, Action<int, int>, WebCodecsVideoCapture> captureFactory, Action<IVideoRecorder> onReady)
        {
            info = voiceInfo;

            // call onReady() when both capture and encder are ready
            Action set = () => {
                if (capture.SetEncoderAndPreview(encoder, preview))
                {
                    onReady(this);
                }
            };

            // if the sizes are fixed, create a capture, encoder and preview immediately
            if (sizeMode.Init == VideoSourceSizeMode.Mode.Fixed)
            {
                preview = new UnityEngine.Texture2D(info.Width, info.Height, UnityEngine.TextureFormat.ARGB32, false);
                encoder = new WebCodecsVideoEncoder(logger, info, null);
                capture = captureFactory(set, null);
            }
            else
            {
                int callbackCnt = 0;
                capture = captureFactory(null, (w, h) => { // (re)create the encoder and preview and call onReady() on size initial report or change
                    if (callbackCnt++ > 0 && !sizeMode.Update)
                    {
                        return;
                    }
                    info.Width = w;
                    info.Height = h;
                    preview = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.ARGB32, false);
                    if (encoder != null)
                    {
                        encoder.Dispose();
                    }
                    encoder = new WebCodecsVideoEncoder(logger, info, set);
                });
            }
        }

        public IEncoder Encoder => encoder;

        public object PlatformView => preview;
        public int Width => info.Width;
        public int Height => info.Height;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;

        public string Error => encoder != null && encoder.Error != null ? (capture.Error != null ? capture.Error + " / " + encoder.Error : encoder.Error)
                                                                        : (capture.Error != null ? capture.Error                         : null         );
        public void Dispose()
        {
            if (capture != null)
            {
                capture.Dispose();
            }
            if (encoder != null)
            {
                encoder.Dispose();
            }
        }
    }

    public class WebCodecsCameraRecorderUnityTexture : WebCodecsVideoRecorderUnityTexture
    {
        public WebCodecsCameraRecorderUnityTexture(ILogger logger, VoiceInfo info, string camDevice, Action<IVideoRecorder> onReady) : base(logger, info, new VideoSourceSizeMode() { Init = VideoSourceSizeMode.Mode.Fixed }, (onCreate, onChange) => new WebCodecsCamera(logger, info, camDevice, onCreate, onChange), onReady)
        {
        }
    }

    public class WebCodecsScreenRecorderUnityTexture : WebCodecsVideoRecorderUnityTexture
    {
        public WebCodecsScreenRecorderUnityTexture(ILogger logger, VoiceInfo info, VideoSourceSizeMode sizeMode, Action<IVideoRecorder> onReady) : base(logger, info, sizeMode, (onCreate, onChange) => new WebCodecsScreenShare(logger, info, sizeMode, onCreate, onChange), onReady)
        {
        }
    }

    public class WebCodecsVideoDecoderUnityTexture : IDecoder
    {
        const string lib_name = "__Internal";

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int PhotonVoice_WebCodecsVideoDecoder_Start(IntPtr managedCamObj, string codec, int previewTex, Action<IntPtr, int, int> createCallbackStatic);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoDecoder_Input(int nativeDecoder, byte[] byf, int offset, int len, bool keyframe);

        [DllImport(lib_name, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void PhotonVoice_WebCodecsVideoDecoder_Stop(int nativeDecoder);

        [MonoPInvokeCallbackAttribute(typeof(Action<IntPtr, int, int>))]
        static void createCallbackStatic(IntPtr managedCamObj, int decoderID, int err)
        {
            (GCHandle.FromIntPtr(managedCamObj).Target as WebCodecsVideoDecoderUnityTexture).createCallback(decoderID, err);
        }

        void createCallback(int decoderID, int err)
        {
            if (err == 0)
            {
                nativeDecoder = decoderID;
            }
            else
            {
                Error = "Decoder creation error " + err;
            }
        }

        protected GCHandle managedDecObj;
        protected int nativeDecoder { private set; get; }

        public UnityEngine.Texture2D Preview { get; private set; }

        public WebCodecsVideoDecoderUnityTexture(ILogger logger, VoiceInfo info)
        {
            managedDecObj = GCHandle.Alloc(this, GCHandleType.Normal);
            // mipmaps are not updated from video source on WebGL, false to avoid mipmaps usage
            Preview = new UnityEngine.Texture2D(info.Width, info.Height, UnityEngine.TextureFormat.ARGB32, false);
        }

        public void Open(VoiceInfo info)
        {
            PhotonVoice_WebCodecsVideoDecoder_Start(GCHandle.ToIntPtr(managedDecObj), info.Codec.ToString(), Preview.GetNativeTexturePtr().ToInt32(), createCallbackStatic);
        }

        public void Input(ref FrameBuffer buf)
        {
            if (Error != null)
            {
                return;
            }

            if (buf.Array == null)
            {
                return;
            }

            if (nativeDecoder > 0)
            {
                PhotonVoice_WebCodecsVideoDecoder_Input(nativeDecoder, buf.Array, buf.Offset, buf.Length, buf.IsKeyframe || buf.IsConfig);
            }
        }

        public string Error { get; protected set; }

        public void Dispose()
        {
            PhotonVoice_WebCodecsVideoDecoder_Stop(nativeDecoder);
            if (managedDecObj.Target != null)
            {
                // managedDecObj.Free(); // Triggers "Handle is not allocated" error
            }
        }

    }

    public class WebCodecsVideoPlayerUnityTexture : IVideoPlayer
    {
        VoiceInfo info;
        public IDecoder Decoder { get; private set; }

        public WebCodecsVideoPlayerUnityTexture(ILogger logger, VoiceInfo info, Action<IVideoPlayer> onReady)
        {
            this.info = info;
            //preview.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            //preview.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            Decoder = new WebCodecsVideoDecoderUnityTexture(logger, info);
            onReady(this);
        }

        public object PlatformView => (Decoder as WebCodecsVideoDecoderUnityTexture).Preview;
        public int Width => info.Width;
        public int Height => info.Height;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;

        public void Dispose()
        {
            Decoder.Dispose();
        }
    }
}
#endif
#endif