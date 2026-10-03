#if PHOTON_VOICE_VIDEO_ENABLE
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Photon.Voice.IOS
{
    // Wraps the native Photon_Capture: opens the camera, owns the AVCaptureSession,
    // optionally exposes an AVCaptureVideoPreviewLayer and/or a Metal preview texture.
    public class VideoCapture : IDisposable
    {
        protected const string lib_name = "__Internal";

        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_CreateCapture(int hostID, string deviceID, bool createPreviewLayer, IntPtr previewLayer, Action<int, IntPtr, int, int> previewTextureUpdateCallback);
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Destroy_Capture(IntPtr handler);
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_Capture_GetPreviewLayer(IntPtr handler);

        public IntPtr Handle { get; protected set; }
        public string Error { get; protected set; }

        protected VideoCapture() { }

        public virtual void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                Photon_Video_Destroy_Capture(Handle);
                Handle = IntPtr.Zero;
            }
        }
    }

    // Camera capture that exposes a native AVCaptureVideoPreviewLayer.
    public class VideoCaptureLayer : VideoCapture
    {
        public IntPtr PreviewLayer { get; private set; }

        public VideoCaptureLayer(ILogger logger, string deviceID, bool createPreviewLayer = true, IntPtr previewLayer = default(IntPtr))
        {
            try
            {
                Handle = Photon_Video_CreateCapture(0, deviceID, createPreviewLayer, previewLayer, null);
                if (Handle == IntPtr.Zero)
                {
                    Error = "Native Capture creation error";
                    logger.Log(LogLevel.Error, "[PV] [VC] Error: " + Error);
                    return;
                }
                PreviewLayer = Photon_Video_Capture_GetPreviewLayer(Handle);
            }
            catch (Exception e)
            {
                Error = e.ToString() ?? "Exception in VideoCaptureLayer constructor";
                logger.Log(LogLevel.Error, "[PV] [VC] Error: " + Error);
            }
        }
    }

#if UNITY_5_3_OR_NEWER // #if UNITY
    // Camera capture that exposes the camera frame as a UnityEngine.Texture2D (Metal-backed).
    public class VideoCaptureUnityTexture : VideoCapture
    {
        public UnityEngine.Texture2D PreviewTexture { get; private set; }

        Action<IntPtr, int, int> onPreviewTextureUpdate;
        Action userOnPreviewTextureReady;
        int instanceID;

        static int instanceCnt;
        static Dictionary<int, VideoCaptureUnityTexture> instancePerHandle = new Dictionary<int, VideoCaptureUnityTexture>();

        public VideoCaptureUnityTexture(ILogger logger, string deviceID, Action userOnPreviewTextureReady)
        {
            this.userOnPreviewTextureReady = userOnPreviewTextureReady;
            this.onPreviewTextureUpdate = (nativeTex, width, height) =>
            {
                try
                {
                    if (PreviewTexture == null)
                    {
                        PreviewTexture = UnityEngine.Texture2D.CreateExternalTexture(width, height, UnityEngine.TextureFormat.ARGB32, false, true, nativeTex);
                        logger.Log(LogLevel.Info, "[PV] [VC] Preview Texture created: {0}", PreviewTexture);
                        if (this.userOnPreviewTextureReady != null) this.userOnPreviewTextureReady();
                    }
                    else
                    {
                        PreviewTexture.UpdateExternalTexture(nativeTex);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(LogLevel.Error, "[PV] [VC] Exception in OnPreviewTextureReady: {0}", e);
                }
            };

            try
            {
                lock (instancePerHandle)
                {
                    this.instanceID = instanceCnt++;
                    instancePerHandle.Add(this.instanceID, this);
                }
                Handle = Photon_Video_CreateCapture(this.instanceID, deviceID, false, IntPtr.Zero, nativeTextureUpdateCallback);
                if (Handle == IntPtr.Zero)
                {
                    Error = "Native Capture creation error";
                    logger.Log(LogLevel.Error, "[PV] [VC] Error: " + Error);
                }
            }
            catch (Exception e)
            {
                Error = e.ToString() ?? "Exception in VideoCaptureUnityTexture constructor";
                logger.Log(LogLevel.Error, "[PV] [VC] Error: " + Error);
            }
        }

        delegate void TextureUpdateCallbackDelegate(int instanceID, IntPtr nativeTex, int width, int height);
        [MonoPInvokeCallbackAttribute(typeof(TextureUpdateCallbackDelegate))]
        static void nativeTextureUpdateCallback(int instanceID, IntPtr nativeTex, int width, int height)
        {
            VideoCaptureUnityTexture instance;
            bool ok;
            lock (instancePerHandle)
            {
                ok = instancePerHandle.TryGetValue(instanceID, out instance);
            }
            if (ok)
            {
                instance.onPreviewTextureUpdate(nativeTex, width, height);
            }
        }

        public override void Dispose()
        {
            lock (instancePerHandle)
            {
                instancePerHandle.Remove(instanceID);
            }
            base.Dispose();
        }
    }
#endif

    public class VideoEncoder : IEncoder
    {
        const string lib_name = "__Internal";
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_CreateEncoder(int instanceID, IntPtr captureHandle, Action<int, IntPtr, int, int> pushCallback, int codec, int bitrate, int width, int height, int fps, int keyFrameInt);
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Destroy_Encoder(IntPtr handler);
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Encoder_GetInfo(IntPtr handler, out int codec, out int bitrate, out int width, out int height);
        // Render-thread pull-mode: stash next source + CommandBuffer.IssuePluginEventAndData
        // (GetRenderEventFunc, 0, handle) — native runs MTL blit + VT encode on Unity's render
        // thread, after Unity has issued the current frame's GPU commands.
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Encoder_SetPendingSource(IntPtr handler, IntPtr mtlTex);
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_GetRenderEventFunc();

        public static Dictionary<Codec, int> CodecCodes = new Dictionary<Codec, int>()
        {
            {Codec.VideoH264, fourCharCode('a', 'v', 'c', '1')},
        };

        private delegate void CallbackDelegate(int instanceID, IntPtr buf, int len);

        private static int fourCharCode(char c0, char c1, char c2, char c3)
        {
            return (c0 << 24) + (c0 << 16) + (c0 << 8) + c3;
        }

        // Pass capture==null to create a pull-mode encoder driven via the render-thread plugin
        // event (see VideoEncoderUnityTextureInput).
        protected VideoEncoder(ILogger logger, VoiceInfo info, VideoCapture capture)
        {
            try
            {
                int codecCode;
                if (!VideoEncoder.CodecCodes.TryGetValue(info.Codec, out codecCode))
                {
                    Error = "Unsupported codec " + info.Codec;
                    logger.Log(LogLevel.Error, "[PV] [VE] Error: {0}", Error);
                    return;
                }

                IntPtr captureHandle = capture != null ? capture.Handle : IntPtr.Zero;
                var handle = Photon_Video_CreateEncoder(instanceCnt, captureHandle, nativePushCallback, codecCode, info.Bitrate, info.Width, info.Height, info.FPS, info.KeyFrameInt);
                lock (instancePerHandle)
                {
                    this.handle = handle;
                    this.instanceID = instanceCnt;
                    instancePerHandle.Add(instanceCnt++, this);
                    int c, w, h, bitrate;
                    Photon_Video_Encoder_GetInfo(handle, out c, out bitrate, out w, out h);
                    Width = w;
                    Height = h;
                }
            }
            catch (Exception e)
            {
                Error = e.ToString();
                if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                {
                    Error = "Exception in VideoEncoder constructor";
                }
                logger.Log(LogLevel.Error, "[PV] [VE] Error: " + Error);
            }
        }

        // IL2CPP does not support marshaling delegates that point to instance methods to native code.
        // Using static method and per instance table.
        static protected int instanceCnt;
        protected static Dictionary<int, VideoEncoder> instancePerHandle = new Dictionary<int, VideoEncoder>();

        [MonoPInvokeCallbackAttribute(typeof(CallbackDelegate))]
        protected static void nativePushCallback(int instanceID, IntPtr buf, int len, int flags)
        {
            VideoEncoder instance;
            bool ok;
            lock (instancePerHandle)
            {
                ok = instancePerHandle.TryGetValue(instanceID, out instance);
            }
            if (ok)
            {
                instance.push(buf, len, flags);
            }
        }

        protected IntPtr handle;
        protected int instanceID;
        byte[] bufManaged = new byte[0];

        void push(IntPtr buf, int len, int bufferFlags)
        {
            if (Output != null)
            {
                // native code uses flags defined in FrameFlags
                FrameFlags flags = (FrameFlags)bufferFlags;
                if (bufManaged.Length < len)
                {
                    bufManaged = new byte[len];
                }
                Marshal.Copy(buf, bufManaged, 0, len);
                Output(new ArraySegment<byte>((byte[])(Array)bufManaged, 0, len), flags);
            }
        }

        public int Width { get; protected set; }
        public int Height { get; protected set; }

        public Action<ArraySegment<byte>, FrameFlags> Output { set; private get; }

        static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
        public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
        {
            flags = 0;
            return EmptyBuffer;
        }

        public string Error { get; protected set; }

        public void EndOfStream()
        {
        }

        public I GetPlatformAPI<I>() where I : class
        {
            return null;
        }

        public virtual void Dispose()
        {
            lock (instancePerHandle)
            {
                instancePerHandle.Remove(instanceID);
            }
            if (handle != IntPtr.Zero)
            {
                Photon_Video_Destroy_Encoder(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    // Encoder fed by a VideoCapture (camera).
    public class VideoEncoderFromCapture : VideoEncoder
    {
        public VideoCapture Capture { get; private set; }

        public VideoEncoderFromCapture(ILogger logger, VoiceInfo info, VideoCapture capture)
            : base(logger, info, capture)
        {
            this.Capture = capture;
        }
    }

#if UNITY_5_3_OR_NEWER // #if UNITY
    // Pull-mode encoder driven by Unity. Caller Graphics.Blit's any source (WebCamTexture,
    // RenderTexture, Camera output, etc.) into InputTexture each frame, then calls EncodeFrame()
    // to push it to the hardware encoder.
    //
    // Unity owns the RenderTexture; per call to EncodeFrame() its native MTLTexture ptr is stashed
    // on the encoder and a pre-recorded CommandBuffer is executed. Unity runs the plugin event on
    // its render thread, after the current frame's Graphics.Blit has been submitted — so the native
    // MTL blit + VT encode observes Unity's writes.
    public class VideoEncoderUnityTextureInput : VideoEncoder
    {
        public UnityEngine.RenderTexture InputTexture { get; private set; }

        static readonly IntPtr renderEventFunc = Photon_Video_GetRenderEventFunc();
        UnityEngine.Rendering.CommandBuffer eventCb;

        public VideoEncoderUnityTextureInput(ILogger logger, VoiceInfo info)
            : base(logger, info, null)
        {
            if (Error != null) return;
            try
            {
                InputTexture = new UnityEngine.RenderTexture(info.Width, info.Height, 0, UnityEngine.RenderTextureFormat.BGRA32);
                InputTexture.Create();

                // Pre-recorded: the encoder handle (data) is constant for this instance's lifetime.
                eventCb = new UnityEngine.Rendering.CommandBuffer();
                eventCb.IssuePluginEventAndData(renderEventFunc, 0, handle);

                logger.Log(LogLevel.Info, "[PV] [VE] Pull-mode InputTexture (Unity RT) created: {0}", InputTexture);
            }
            catch (Exception e)
            {
                Error = e.ToString() ?? "Exception in VideoEncoderUnityTextureInput constructor";
                logger.Log(LogLevel.Error, "[PV] [VE] Error: " + Error);
            }
        }

        // Stash the InputTexture's MTLTexture pointer and ask Unity to run our native callback on
        // the render thread. The callback does the MTL blit + VT submit there, so it sees Unity's
        // just-issued Graphics.Blit writes.
        public int EncodeFrame()
        {
            if (handle == IntPtr.Zero || InputTexture == null || eventCb == null) return -1;
            Photon_Video_Encoder_SetPendingSource(handle, InputTexture.GetNativeTexturePtr());
            UnityEngine.Graphics.ExecuteCommandBuffer(eventCb);
            return 0;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (eventCb != null)
            {
                eventCb.Release();
                eventCb = null;
            }
            if (InputTexture != null)
            {
                InputTexture.Release();
                UnityEngine.Object.Destroy(InputTexture);
                InputTexture = null;
            }
        }
    }
#endif

    public class VideoDecoder : IDecoder
    {
        const string lib_name = "__Internal";
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_CreateDecoderLayer();
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_CreateDecoderTexture(int instanceID, Action<int, IntPtr, int, int> previewTextureUpdateCallback);
        [DllImport(lib_name)]
        protected static extern int Photon_Video_Decoder_Input(IntPtr handler, IntPtr data, int len, int flags);
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Destroy_Decoder(IntPtr handler);
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_Decoder_GetPreviewLayer(IntPtr handler);

        protected IntPtr handle;
        protected ILogger logger;

        public VideoDecoder(ILogger logger)
        {
            this.logger = logger;
        }

        public void Open(VoiceInfo info)
        {
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
            // native code uses flags defined in FrameFlags
            var err = Photon_Video_Decoder_Input(handle, buf.Ptr, buf.Length, (int)buf.Flags);
            if (err != 0)
            {
                // the error may be not critical, do not set Error
                //                Error = "Native Decoder decoding error " + err;
                logger.Log(LogLevel.Error, "[PV] [VD] Photon_Video_Decoder_Input error: " + err);
            }
        }

        public string Error { get; protected set; }

        virtual public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Photon_Video_Destroy_Decoder(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    public class VideoDecoderLayer : VideoDecoder
    {
        public IntPtr PreviewLayer { get; protected set; }
        public VideoDecoderLayer(ILogger logger) : base(logger)
        {
            try
            {
                this.handle = Photon_Video_CreateDecoderLayer();
                PreviewLayer = Photon_Video_Decoder_GetPreviewLayer(this.handle);
                logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoder created");
            }
            catch (Exception e)
            {
                Error = e.ToString();
                if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                {
                    Error = "Exception in VideoDecoder constructor";
                }
                logger.Log(LogLevel.Error, "[PV] [VD] Error: " + Error);
            }
        }
    }

#if UNITY_5_3_OR_NEWER // #if UNITY
    public class VideoDecoderUnityTexture : VideoDecoder
    {
        public UnityEngine.Texture2D PreviewTexture { get; private set; }
        Action<IntPtr, int, int> onPreviewTextureUpdate;

        public int Width { get; protected set; }
        public int Height { get; protected set; }

        public VideoDecoderUnityTexture(ILogger logger, Action userOnPreviewTextureReady) : base(logger)
        {
            this.onPreviewTextureUpdate = (nativeTex, width, height) =>
            {
                try
                {
                    if (PreviewTexture == null)
                    {
                        PreviewTexture = UnityEngine.Texture2D.CreateExternalTexture(width, height, UnityEngine.TextureFormat.ARGB32, false, true, nativeTex);
                        Width = width;
                        Height = height;
                        logger.Log(LogLevel.Info, "[PV] [VD] Preview Texture created: {0}", PreviewTexture);
                        userOnPreviewTextureReady();
                    }
                    else
                    {
                        PreviewTexture.UpdateExternalTexture(nativeTex);
                    }
                }
                catch (Exception e)
                {
                    logger.Log(LogLevel.Error, "[PV] [VD] Exception in OnPreviewTextureReady: {0}", e);
                }
            };

            try
            {
                var handle = Photon_Video_CreateDecoderTexture(instanceCnt, nativeTextureUpdateCallback);
                lock (instancePerHandle)
                {
                    this.handle = handle;
                    this.instanceID = instanceCnt;
                    instancePerHandle.Add(instanceCnt++, this);
                }

                logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoder created");
            }
            catch (Exception e)
            {
                Error = e.ToString();
                if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                {
                    Error = "Exception in VideoDecoder constructor";
                }
                logger.Log(LogLevel.Error, "[PV] [VD] Error: " + Error);
            }
        }

        int instanceID;

        static protected int instanceCnt;
        protected static Dictionary<int, VideoDecoderUnityTexture> instancePerHandle = new Dictionary<int, VideoDecoderUnityTexture>();

        private delegate void TextureUpdateCallbackDelegate(int instanceID, IntPtr nativeTex, int width, int height);
        [MonoPInvokeCallbackAttribute(typeof(TextureUpdateCallbackDelegate))]
        protected static void nativeTextureUpdateCallback(int instanceID, IntPtr nativeTex, int width, int height)
        {
            VideoDecoderUnityTexture instance;
            bool ok;
            lock (instancePerHandle)
            {
                ok = instancePerHandle.TryGetValue(instanceID, out instance);
            }
            if (ok)
            {
                instance.onPreviewTextureUpdate(nativeTex, width, height);
            }
        }

        override public void Dispose()
        {
            lock (instancePerHandle)
            {
                instancePerHandle.Remove(instanceID);
            }
            base.Dispose();
        }
    }
#endif

    public class PreviewManagerLayer : Photon.Voice.PreviewManager
    {
        const string lib_name = "__Internal";
        [DllImport(lib_name)]
        private static extern void Photon_Video_SetViewBounds(IntPtr view, int x, int y, int w, int h, int order);

        ILogger logger;

        public PreviewManagerLayer(ILogger logger)
        {
            this.logger = logger;
        }

        override protected void Apply(ViewState v)
        {
            Photon_Video_SetViewBounds((IntPtr)v.PlatformView, v.x, v.y, v.w, v.h, 0);
            logger.Log(LogLevel.Info, "[PV] [VM] call setViewBounds {0} {1} {2} {3} {4}", v.PlatformView, v.x, v.y, v.w, v.h);
        }
    }

    // Camera capture (CaptureLayer) + capture-attached encoder. PlatformView = capture's PreviewLayer.
    public class VideoRecorderLayer : IVideoRecorder
    {
        public IEncoder Encoder { get; private set; }
        VideoCaptureLayer capture;
        public object PlatformView { get { return capture.PreviewLayer; } }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;

        public int Width { get { return ((VideoEncoder)Encoder).Width; } }
        public int Height { get { return ((VideoEncoder)Encoder).Height; } }

        public string Error => capture.Error ?? Encoder.Error;

        public VideoRecorderLayer(ILogger logger, VoiceInfo info, string deviceID, Action<IVideoRecorder> onReady)
        {
            capture = new VideoCaptureLayer(logger, deviceID);
            Encoder = new VideoEncoderFromCapture(logger, info, capture);
            onReady(this);
        }

        public void Dispose()
        {
            if (Encoder != null) Encoder.Dispose();
            if (capture != null) capture.Dispose();
        }
    }

#if UNITY_5_3_OR_NEWER // #if UNITY
    // Camera capture (CaptureUnityTexture) + capture-attached encoder. PlatformView = capture's preview Texture2D.
    public class VideoRecorderUnityTexture : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        VideoCaptureUnityTexture capture;

        public int Width { get { return ((VideoEncoder)Encoder).Width; } }
        public int Height { get { return ((VideoEncoder)Encoder).Height; } }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;
        public object PlatformView { get { return capture.PreviewTexture; } }

        public string Error => capture.Error ?? Encoder.Error;

        public VideoRecorderUnityTexture(ILogger logger, VoiceInfo info, string deviceID, Action<IVideoRecorder> onReady)
        {
            capture = new VideoCaptureUnityTexture(logger, deviceID, () =>
            {
                onReady(this);
            });
            Encoder = new VideoEncoderFromCapture(logger, info, capture);
        }

        public void Dispose()
        {
            if (Encoder != null) Encoder.Dispose();
            if (capture != null) capture.Dispose();
        }
    }

    // Pull-mode recorder: no camera capture. App blits any Unity texture into InputTexture
    // and calls EncodeFrame() to push the frame to the hardware encoder.
    // PlatformView is the InputTexture itself (the write target), exposed for callers that want to
    // composite it elsewhere.
    public class VideoRecorderUnityTextureInput : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        VideoEncoderUnityTextureInput encoderTyped;

        public UnityEngine.RenderTexture InputTexture { get { return encoderTyped.InputTexture; } }

        public int Width { get { return encoderTyped.Width; } }
        public int Height { get { return encoderTyped.Height; } }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;
        public object PlatformView { get { return encoderTyped.InputTexture; } }

        public string Error => Encoder.Error;

        public VideoRecorderUnityTextureInput(ILogger logger, VoiceInfo info, Action<IVideoRecorder> onReady)
        {
            encoderTyped = new VideoEncoderUnityTextureInput(logger, info);
            Encoder = encoderTyped;
            if (Error == null && onReady != null) onReady(this);
        }

        // Call this from Unity each frame after blitting into InputTexture to push the frame to the encoder.
        public int EncodeFrame() { return encoderTyped.EncodeFrame(); }

        public void Dispose()
        {
            if (Encoder != null) Encoder.Dispose();
        }
    }

    public class VideoPlayerUnityTexture : IVideoPlayer
    {
        public IDecoder Decoder { get; protected set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;
        public object PlatformView
        {
            get
            {
                return ((VideoDecoderUnityTexture)Decoder).PreviewTexture;
            }
        }

        public int Width { get { return ((VideoDecoderUnityTexture)Decoder).Width; } }
        public int Height { get { return ((VideoDecoderUnityTexture)Decoder).Height; } }

        public VideoPlayerUnityTexture(ILogger logger, VoiceInfo info, Action<IVideoPlayer> onReady)
        {
            Decoder = new VideoDecoderUnityTexture(logger, () => onReady(this));
        }

        public void Dispose()
        {
            if (Decoder != null)
            {
                Decoder.Dispose();
            }
        }
    }
#endif

    /// <summary>Enumerates cameras available on device.
    /// </summary>
    public class VideoInEnumerator : DeviceEnumeratorBase
    {
        const string lib_name = "__Internal";
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_Create_CameraEnumerator();
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Destroy_CameraEnumerator(IntPtr handler);
        [DllImport(lib_name)]
        protected static extern int Photon_Video_CameraEnumerator_GetCount(IntPtr handler);
        [DllImport(lib_name)]
        protected static extern string Photon_Video_CameraEnumerator_GetID(IntPtr handler, int idx);
        [DllImport(lib_name)]
        protected static extern string Photon_Video_CameraEnumerator_GetName(IntPtr handler, int idx);
        [DllImport(lib_name)]
        protected static extern int Photon_Video_CameraEnumerator_GetFacing(IntPtr handler, int idx);

        // AVCaptureDevicePosition to FacingEnum
        static Dictionary<int, CameraFacing> iosToVoiceFacing = new Dictionary<int, CameraFacing>()
        {
            {0, CameraFacing.Undef},     // AVCaptureDevicePositionUnspecified
            {1, CameraFacing.Back},      // AVCaptureDevicePositionBack
            {2, CameraFacing.Front},     // AVCaptureDevicePositionFront
        };

        public VideoInEnumerator(ILogger logger) : base(logger)
        {
            Refresh();
        }

        public override bool IsSupported => true;

        /// <summary>Refreshes the microphones list.
        /// </summary>
        public override void Refresh()
        {
            try
            {
                if (IsSupported)
                {
                    IntPtr handler = Photon_Video_Create_CameraEnumerator();
                    devices = new List<DeviceInfo>();
                    for (int i = 0; i < Photon_Video_CameraEnumerator_GetCount(handler); i++)
                    {
                        string id = Photon_Video_CameraEnumerator_GetID(handler, i);
                        string name = Photon_Video_CameraEnumerator_GetName(handler, i);
                        int iosFacing = Photon_Video_CameraEnumerator_GetFacing(handler, i);
                        var facing = CameraFacing.Undef;
                        iosToVoiceFacing.TryGetValue(iosFacing, out facing);
                        DeviceInfo d = new DeviceInfo(id, name, new DeviceFeatures(facing));
                        devices.Add(d);
                    }
                    Photon_Video_Destroy_CameraEnumerator(handler);
                }
            }
            catch (Exception e)
            {
                Error = e.ToString();
                if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                {
                    Error = "Exception in VideoInEnumerator.Refresh()";
                }
            }

            if (OnReady != null)
            {
                OnReady();
            }
        }

        public override void Dispose()
        {
        }
    }
}
#endif
#endif
