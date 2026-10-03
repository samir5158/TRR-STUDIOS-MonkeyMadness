#if PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Photon.Voice.Unity
{
    // depends on Unity's AndroidJavaProxy
    public static class AndroidVideoGkPluginEventIssuer
    {
        const int GlPluginEventID = 1000;
        static AndroidVideoGkPluginEventIssuerMB mb;
        static GameObject go;
        static ILogger logger;

        [DllImport("photon-native-video")]
        private static extern void ensure_photon_native_video_is_loaded();


        public static void Start(ILogger logger_)
        {
            // if not loaded yet, triggers the load and Unity's UnityPluginLoad callback
            ensure_photon_native_video_is_loaded();

            logger = logger_;
            if (go == null)
            {
                go = new GameObject("[PV] AndroidVideoGkPluginEventIssuer"); ;
                mb = go.AddComponent<AndroidVideoGkPluginEventIssuerMB>();
                var nc = new AndroidJavaObject("com.exitgames.photon.video.NativeCallback");
                IntPtr pluginEventPtr = (IntPtr)nc.Call<long>("getNativeFunctionPointer", GlPluginEventID);
                mb.SetPluginEventPtr(pluginEventPtr, GlPluginEventID);
                logger.Log(LogLevel.Info, "[PV] [UAVPEI] AndroidVideoGkPluginEventIssuer started");
            }
        }

        public static void Stop()
        {
            UnityEngine.Object.Destroy(mb);
            if (go)
            {
                UnityEngine.Object.Destroy(go);
                go = null;
            }
            logger.Log(LogLevel.Info, "[PV] [UAVPEI] AndroidVideoGkPluginEventIssuer stopped");
        }
    }

    public class AndroidVideoGkPluginEventIssuerMB : MonoBehaviour
    {
        private IntPtr pluginEventPtr;
        private int eventID;
        public void SetPluginEventPtr(IntPtr pluginEventPtr, int eventID)
        {
            this.pluginEventPtr = pluginEventPtr;
            this.eventID = eventID;
        }

        public void Update()
        {
            if (pluginEventPtr != IntPtr.Zero)
            {
                GL.IssuePluginEvent(pluginEventPtr, eventID);
            }
        }
    }

    public class AndroidVideoEncoder : IEncoder
    {
        static class BufferFlag
        {
            public const int BUFFER_FLAG_CODEC_CONFIG = 2;
            public const int BUFFER_FLAG_END_OF_STREAM = 4;
            public const int BUFFER_FLAG_KEY_FRAME = 1;
            public const int BUFFER_FLAG_PARTIAL_FRAME = 8;
        }

        class DataCallback : AndroidJavaProxy
        {
            Action<ArraySegment<byte>, FrameFlags> callback;
            public DataCallback() : base("com.exitgames.photon.video.Encoder$DataCallback") { }
            public void SetCallback(Action<ArraySegment<byte>, FrameFlags> callback)
            {
                this.callback = callback;
            }

            byte[] ubuf = new byte[0];
            // ByteArray on Java side
            public void onData(AndroidJavaObject arrayObj, int bufferFlags)
            {
                if (callback != null)
                {
                    FrameFlags flags = 0;
                    if ((bufferFlags & BufferFlag.BUFFER_FLAG_CODEC_CONFIG) != 0)
                    {
                        flags |= FrameFlags.Config;
                    }
                    if ((bufferFlags & BufferFlag.BUFFER_FLAG_KEY_FRAME) != 0)
                    {
                        flags |= FrameFlags.KeyFrame;
                    }
                    if ((bufferFlags & BufferFlag.BUFFER_FLAG_END_OF_STREAM) != 0)
                    {
                        flags |= FrameFlags.EndOfStream;
                    }

                    AndroidJavaObject byteArray = arrayObj.Get<AndroidJavaObject>("buffer");
                    // we need to copy sbyte[] to byte[] if we want to avoid obsolete warninigs (see comment in AndroidVideoDecoder.Input)
                    var buf = AndroidJNIHelper.ConvertFromJNIArray<sbyte[]>(byteArray.GetRawObject());
                    if (ubuf.Length < buf.Length)
                    {
                        ubuf = new byte[buf.Length];
                    }

                    // Throws "ArgumentException: Object must be an array of primitives." in some Unity versions.
                    // See https://issuetracker.unity3d.com/issues/android-sbyte-type-is-considered-to-be-not-primitive-when-compiling-il2cpp-code
                    // Upgrade or use inefficient per byte copy:
                    // for (int i = 0; i < buf.Length; i++) ubuf[i] = (byte)buf[i];
                    Buffer.BlockCopy(buf, 0, ubuf, 0, buf.Length);

                    byteArray.Dispose();
                    arrayObj.Dispose();

                    this.callback(new ArraySegment<byte>(ubuf, 0, buf.Length), flags);
                }
            }
        }

        private AndroidJavaObject encoder;
        protected ILogger logger;
        protected VoiceInfo info;
        protected AndroidJavaObject activity;

        public AndroidVideoEncoder(ILogger logger, VoiceInfo info)
        {
            this.logger = logger;
            this.info = info;
            using (AndroidJavaClass app = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = app.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }

        public void Start(int width, int height)
        {            
            Width = width;
            Height = height;
            encoder = new AndroidJavaObject("com.exitgames.photon.video.Encoder");
            logger.Log(LogLevel.Info, "[PV] [UAVE] Unity.AndroidVideoEncoder: AndroidJavaObjects created, actual video size = " + Width + "x" + Height);
            int res = encoder.Call<int>("start", info.Codec.ToString(), Width, Height, info.Bitrate, info.FPS, info.KeyFrameInt, this.callback);
            if (res != 0)
            {
                switch (res)
                {
                    case 1: Error = "Unsupported codec " + info.Codec; break;
                    default: Error = "Error " + res; break;
                }
                logger.Log(LogLevel.Error, "[PV] [UAVD] Unity.AndroidVideoEncoder: {0}", Error);
            }
        }

        public AndroidJavaObject Surface => encoder.Call<AndroidJavaObject>("getSurface");
        public IntPtr SurfaceNative { get; private set; } // native ASurfaceTexture*
        AndroidJavaObject nativeSurfaceUtil;

        public int Width { get; private set; }
        public int Height { get; private set; }

        DataCallback callback = new DataCallback();

        public string Error { get; protected set; }

        public Action<ArraySegment<byte>, FrameFlags> Output
        {
            set
            {
                if (Error == null)
                {
                    this.callback.SetCallback(value);
                }
            }
        }

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
            Debug.Log("[PV] [UAVE] Dispose()");
            if (encoder != null)
            {
                encoder.Call("close");
            }
        }
    }

    // Not used but added for completeness
    // TODO: use it for composition instead of Encoder inheritance in IVideoRecorder implementations
    public class AndroidCamera : IDisposable
    {
        private AndroidJavaObject camera;

        protected class OnReadyCallback : AndroidJavaProxy
        {
            Action<AndroidJavaObject> callback;
            public OnReadyCallback(Action<AndroidJavaObject> callback) : base("com.exitgames.photon.video.Camera$OnReadyCallback")
            {
                this.callback = callback;
            }
            public void onReady(AndroidJavaObject camera)
            {
                if (callback != null)
                {
                    callback(camera);
                }
            }
        }

        public AndroidCamera(ILogger logger, VoiceInfo info, string deviceID)
        {
            using (AndroidJavaClass app = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = app.GetStatic<AndroidJavaObject>("currentActivity");
                camera = new AndroidJavaObject("com.exitgames.photon.video.Camera", activity, deviceID, info.Width, info.Height, info.FPS);
            }

            logger.Log(LogLevel.Info, "[PV] [UAVE] Unity.AndroidCameraVideoEncoderSurfaceView initialized");
        }

        public void AddSurface(AndroidJavaObject surface)
        {
            camera.Call("addSurface", surface, surface);
        }

        public void Start()
        {
            var videoSize = camera.Call<AndroidJavaObject>("getVideoSize");
            Width = videoSize.Call<int>("getWidth");
            Height = videoSize.Call<int>("getHeight");

            camera.Call("start");
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public void Dispose()
        {
            if (camera != null)
            {
                camera.Call("close");
            }
        }
    }

    public class AndroidCameraVideoEncoder : AndroidVideoEncoder
    {
        protected class OnReadyCallback : AndroidJavaProxy
        {
            Action<AndroidJavaObject> callback;
            public OnReadyCallback(Action<AndroidJavaObject> callback) : base("com.exitgames.photon.video.Camera$OnReadyCallback")
            {
                this.callback = callback;
            }
            public void onReady(AndroidJavaObject camera)
            {
                if (callback != null)
                {
                    callback(camera);
                }
            }
        }

        public AndroidCameraVideoEncoder(ILogger logger, VoiceInfo info) : base(logger, info)
        {
        }

        protected void startEncoderAndCamera(AndroidJavaObject camera)
        {
            this.camera = camera;

            var videoSize = camera.Call<AndroidJavaObject>("getVideoSize");
            int width = videoSize.Call<int>("getWidth");
            int height = videoSize.Call<int>("getHeight");

            Start(width, height);

            camera.Call("start");
            surfId = new AndroidJavaObject("java.lang.Object");
            camera.Call("addSurface", surfId, Surface);
        }

        private AndroidJavaObject camera;
        private AndroidJavaObject surfId; // could use base.encoder here but it's private for clarity

        public override void Dispose()
        {
            if (camera != null)
            {
                camera.Call("removeSurface", surfId);
                camera.Call("close");
            }
            base.Dispose();
        }
    }

    // renders preview to SurfaceView
    public class AndroidCameraVideoEncoderSurfaceView : AndroidCameraVideoEncoder
    {
        public AndroidJavaObject Preview { get; private set; }

        public AndroidCameraVideoEncoderSurfaceView(ILogger logger, VoiceInfo info, string cameraID)
            : base(logger, info)
        {
            if (Error != null)
            {
                return;
            }
            var camera = new AndroidJavaObject("com.exitgames.photon.video.CameraSurfaceView", activity, cameraID, info.Width, info.Height, info.FPS);
            Preview = camera.Call<AndroidJavaObject>("getPreview");

            startEncoderAndCamera(camera);

            logger.Log(LogLevel.Info, "[PV] [UAVE] Unity.AndroidCameraVideoEncoderSurfaceView initialized");
        }
    }

    // renders preview to external texture
    public class AndroidCameraVideoEncoderTexture : AndroidCameraVideoEncoder
    {
        public Texture Preview { get; private set; }

        OnReadyCallback onReadyCallback;

        public AndroidCameraVideoEncoderTexture(ILogger logger, VoiceInfo info, string cameraID, Action<AndroidCameraVideoEncoderTexture> onReady)
            : base(logger, info)
        {
            if (Error != null)
            {
                return;
            }

            if (Application.platform != RuntimePlatform.Android ||
                #if !UNITY_2023_1_OR_NEWER
                SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 &&
                #endif
                SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
            {
                Error = "Unsupported platform " + Application.platform + "/" + SystemInfo.graphicsDeviceType;
                logger.Log(LogLevel.Error, "[PV] [UAVE] Unity.AndroidCameraVideoEncoderTexture: {0}", Error);
                return;
            }

            this.onReadyCallback = new OnReadyCallback((cam) =>
            {
                var texID = cam.Call<int>("getPreview");
                if (texID != 0)
                {
                    Preview = Texture2D.CreateExternalTexture(info.Width, info.Height, TextureFormat.ARGB32, false, true, new IntPtr(texID));
                    logger.Log(LogLevel.Info, "[PV] [UAVE] camera getPreview: " + texID);
                }
                else
                {
                    Error = "Preview texture ID is 0";
                    logger.Log(LogLevel.Error, "[PV] [UAVE] camera getPreview error: {0}", Error);
                }
                onReady(this);
            });

            var camera = new AndroidJavaObject("com.exitgames.photon.video.CameraTexture", activity, cameraID, info.Width, info.Height, info.FPS, this.onReadyCallback);

            startEncoderAndCamera(camera);

            AndroidVideoGkPluginEventIssuer.Start(logger);

            logger.Log(LogLevel.Info, "[PV] [UAVE] Unity.AndroidCameraVideoEncoderTexture initialized");
        }
    }

    public class AndroidTextureVideoEncoder : AndroidVideoEncoder
    {
        [DllImport("photon-native-video")]
        static extern bool VulkanSurfaceDrawer_DrawTexture(long handle, long texturePtr, long timestampNs);

        private AndroidJavaObject drawer;
        private bool isVulkan;
        private long handle; // Vukan only
        public AndroidTextureVideoEncoder(ILogger logger, VoiceInfo info) : base(logger, info)
        {
            AndroidVideoGkPluginEventIssuer.Start(logger);

            Start(info.Width, info.Height);


            // Select drawer based on graphics API
            isVulkan = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;

            if (isVulkan)
            {
                if (info.FPS > 0)
                {
                    // VulkanSurfaceDrawer_DrawTexture expects nanoseconds
                    timestampDelta = 1_000_000_000 / info.FPS;
                }
                drawer = new AndroidJavaObject("com.exitgames.photon.video.VulkanSurfaceDrawer", Surface);
                handle = drawer.Call<long>("getNativeHandle"); // You may need to add this getter
            }
            else
            {
                if (info.FPS > 0)
                {
                    // SurfaceDrawer.drawTexture expects microsecond
                    timestampDelta = 1_000_000 / info.FPS;
                }
                drawer = new AndroidJavaObject("com.exitgames.photon.video.SurfaceDrawer", Surface);
            }

            var err = drawer.Call<String>("getError");

            if (err == null)
            {
                logger.Log(LogLevel.Info, "[PV] [UAVE] Unity.AndroidTextureVideoEncoder initialized ({0}), timestamp delta = {1}",
                    isVulkan ? "Vulkan" : "OpenGL", timestampDelta);
            }
            else
            {
                if (Error != null)
                {
                    Error = err;
                }

                logger.Log(LogLevel.Error, "[PV] [UAVE] Unity.AndroidTextureVideoEncoder {0}", err);
            }
        }

        private long lastTimestamp = 0;
        private long timestampDelta = 1;

        /// <summary>
        /// Encode a Unity texture (works with both OpenGL and Vulkan)
        /// </summary>
        /// <param name="texture">Unity texture</param>
        public void encodeTexture(Texture texture)
        {
            encodeTexture(texture, lastTimestamp + timestampDelta);
        }

        /// <summary>
        /// Encode a Unity texture with explicit timestamp
        /// </summary>
        /// <param name="texture">Unity texture</param>
        /// <param name="timestampUs">Presentation timestamp in microseconds</param>
        public void encodeTexture(Texture texture, long timestampUs)
        {
            if (drawer != null && texture != null)
            {
                lastTimestamp = timestampUs;

                if (isVulkan)
                {
                    // Vulkan: pass native texture pointer (VkImage)
                    long texturePtr = texture.GetNativeTexturePtr().ToInt64();
                    //drawer.Call("drawTexture", texturePtr, timestampUs);
                    VulkanSurfaceDrawer_DrawTexture(handle, texturePtr, timestampUs);
                }
                else
                {
                    // OpenGL: pass texture ID as int
                    int texID = (int)texture.GetNativeTexturePtr();
                    drawer.Call("drawTexture", texID, timestampUs);
                }

                var err = drawer.Call<String>("getError");
                if (err != null)
                {
                    if (Error != null)
                    {
                        Error = err;
                    }
                    logger.Log(LogLevel.Error, "[PV] [UAVE] Unity.AndroidTextureVideoEncoder {0}", err);
                }
            }
        }

        public override void Dispose()
        {
            logger.Log(LogLevel.Info, "[PV] [UAVE] Unity.AndroidTextureVideoEncoder Release");
            if (drawer != null)
            {
                drawer.Call("release");
                drawer.Dispose();
                drawer = null;
            }
            base.Dispose();
        }
    }

    public class AndroidVideoDecoder : IDecoder
    {
        protected ILogger logger;
        protected VoiceInfo info;
        public int Width { get { return info.Width; } }
        public int Height { get { return info.Height; } }

        protected AndroidJavaObject decoder;

        public AndroidVideoDecoder(ILogger logger, VoiceInfo info)
        {
            this.logger = logger;
            this.info = info;
            decoder = new AndroidJavaObject("com.exitgames.photon.video.Decoder");
        }

        public void Open(VoiceInfo info)
        {
            if (Error != null)
            {
                return;
            }

            logger.Log(LogLevel.Info, "[PV] [UAVD] Open " + info);

            try
            {
                int res = decoder.Call<int>("start", info.Codec.ToString(), info.Width, info.Height);
                if (res != 0)
                {
                    switch (res)
                    {
                        case 1: Error = "Unsupported codec " + info.Codec; break;
                        default: Error = "Error " + res; break;
                    }
                    logger.Log(LogLevel.Error, "[PV] [UAVD] Unity.AndroidVideoDecoder: {0}", Error);
                }
            }
            catch (Exception e)
            {
                Error = e.ToString();
                logger.Log(LogLevel.Error, "[PV] [UAVD] Unity.AndroidVideoDecoder: {0}", Error);
                logger.Log(LogLevel.Error, "[PV] [UAVD] Unity.AndroidVideoDecoder: {0}", e.StackTrace);
            }
        }

        public string Error { get; protected set; }

        public void Input(ref FrameBuffer buf)
        {
            if (buf.Array == null)
            {
                return;
            }
            if (Error == null)
            {
                // passing buf.Array directly results in logging "AndroidJNIHelper.GetSignature: using Byte parameters is obsolete, use SByte parameters instead"
                // and "AndroidJNIHelper: converting Byte array is obsolete, use SByte array instead"
                // on each call
                // cast (sbyte[])(Array)buf.Array does not help,
                // so we need to copy the buffer if we want to avoid obsolete warninigs
                if (sbuf.Length < buf.Length)
                {
                    sbuf = new sbyte[buf.Length];
                }

                // Throws "ArgumentException: Object must be an array of primitives." in some Unity versions.
                // See https://issuetracker.unity3d.com/issues/android-sbyte-type-is-considered-to-be-not-primitive-when-compiling-il2cpp-code
                // Upgrade or use inefficient per byte copy:
                // for (int i = 0; i < buf.Length; i++) sbuf[i] = (sbyte)buf.Array[buf.Offset + i];
                Buffer.BlockCopy(buf.Array, buf.Offset, sbuf, 0, buf.Length);
                decoder.Call("decode", new object[] { sbuf, 0, buf.Length, (int)buf.Flags });
            }
        }

        sbyte[] sbuf = new sbyte[0];

        public void Dispose()
        {
            Debug.Log("[PV] [UAVD] Dispose()");
            if (decoder != null) {
                decoder.Call("close");
            }
        }
    }

    // renders preview to SurfaceView
    public class AndroidVideoDecoderSurfaceView : AndroidVideoDecoder
    {
        public object Preview => preview.View;
        AndroidSurfaceView preview;

        public AndroidVideoDecoderSurfaceView(ILogger logger, VoiceInfo info)
            : base(logger, info)
        {
            preview = new AndroidSurfaceView(logger, info, (surface, surfaceNative) => {
                decoder.Call("setSurface", surface);
            });
            logger.Log(LogLevel.Info, "[PV] [UAVDS] Unity.AndroidVideoDecoderSurfaceView initialized");
        }
    }

    // renders preview to external texture
    public class AndroidVideoDecoderTexture : AndroidVideoDecoder
    {
        public Texture Preview => preview.Texture;
        AndroidTextureView preview;

        public AndroidVideoDecoderTexture(ILogger logger, VoiceInfo info)
            : base(logger, info)
        {
            preview = new AndroidTextureView(logger, info);
            decoder.Call("setSurface", preview.Surface);

            logger.Log(LogLevel.Info, "[PV] [UAVDT] Unity.AndroidVideoDecoderTexture initialized");
        }
    }

    public class AndroidPreviewManagerSurfaceView : Photon.Voice.PreviewManager
    {
        public AndroidJavaObject ViewManager { get; private set; }

        public AndroidPreviewManagerSurfaceView(ILogger logger)
        {
            this.logger = logger;
            using (var app = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = app.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                //var viewManagerClass = new AndroidJavaClass("com.exitgames.photon.video.ViewManager");
                //viewManager = viewManagerClass.GetStatic<AndroidJavaObject>("INSTANCE");
                ViewManager = new AndroidJavaObject("com.exitgames.photon.video.ViewManager", activity);
            }
            logger.Log(LogLevel.Info, "[PV] [UAVM] AndroidPreviewManagerSurfaceView initialized");
        }

        override public void AddView(object id, IVideoPreview view)
        {
            ViewManager.Call("addView", view.PlatformView);
            base.AddView(id, view);
        }

        override public void RemoveView(object id)
        {
            if (views.ContainsKey(id))
            {
                ViewManager.Call("removeView", views[id].PlatformView);
            }
            else
            {
                logger.Log(LogLevel.Error, "[PV] [UAVM] RemoveView: id not found: " + id);
            }
            base.RemoveView(id);
        }

        override protected void Apply(ViewState v)
        {
            ViewManager.Call("setViewBounds", (AndroidJavaObject)v.PlatformView, v.x, v.y, v.w, v.h, 0);
            logger.Log(LogLevel.Info, "[PV] [UAVM] call setViewBounds {0} {1} {2} {3}", v.x, v.y, v.w, v.h);
        }

        ILogger logger;
    }

    public class AndroidVideoRecorderSurfaceView : IVideoRecorder
    {
        public IEncoder Encoder { get; private set; }
        public object PlatformView { get { return (Encoder as AndroidCameraVideoEncoderSurfaceView).Preview; } }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;

        public int Width { get { return (Encoder as AndroidCameraVideoEncoderSurfaceView).Width; } }
        public int Height { get { return (Encoder as AndroidCameraVideoEncoderSurfaceView).Height; } }

        public string Error => Encoder.Error;

        public AndroidVideoRecorderSurfaceView(ILogger logger, VoiceInfo info, string cameraID, Action<IVideoRecorder> onReady)
        {
            Encoder = new AndroidCameraVideoEncoderSurfaceView(logger, info, cameraID);
            onReady(this);
        }

        public void Dispose()
        {
            if (Encoder != null) {
                Encoder.Dispose();
            }
        }

    }

    public class AndroidVideoRecorderUnityTexture : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;
        public object PlatformView
        {
            get
            {
                return ((AndroidCameraVideoEncoderTexture)Encoder).Preview;
            }
        }

        public int Width { get { return ((AndroidCameraVideoEncoderTexture)Encoder).Width; } }
        public int Height { get { return ((AndroidCameraVideoEncoderTexture)Encoder).Height; } }

        public string Error => Encoder.Error;

        public AndroidVideoRecorderUnityTexture(ILogger logger, VoiceInfo info, string cameraID, Action<IVideoRecorder> onReady)
        {
            Encoder = new AndroidCameraVideoEncoderTexture(logger, info, cameraID, (e) => onReady(this)); ;
            if (Error != null)
            {
                onReady(this);
            }
        }

        public void Dispose()
        {
            if (Encoder != null)
            {
                Encoder.Dispose();
            }
        }
    }

    // Pull-mode recorder: no camera capture. App blits any Unity texture into InputTexture and
    // calls EncodeFrame() to push the frame to AndroidTextureVideoEncoder (Vulkan or OpenGL,
    // selected by AndroidTextureVideoEncoder based on SystemInfo.graphicsDeviceType).
    public class AndroidVideoRecorderTextureInput : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        AndroidTextureVideoEncoder encoderTyped;

        public UnityEngine.RenderTexture InputTexture { get; private set; }

        public int Width { get { return InputTexture != null ? InputTexture.width : 0; } }
        public int Height { get { return InputTexture != null ? InputTexture.height : 0; } }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;
        public object PlatformView { get { return InputTexture; } }

        public string Error => Encoder.Error;

        public AndroidVideoRecorderTextureInput(ILogger logger, VoiceInfo info, Action<IVideoRecorder> onReady)
        {
            InputTexture = new UnityEngine.RenderTexture(info.Width, info.Height, 0, UnityEngine.RenderTextureFormat.ARGB32);
            InputTexture.Create();

            encoderTyped = new AndroidTextureVideoEncoder(logger, info);
            Encoder = encoderTyped;

            if (Error == null && onReady != null) onReady(this);
        }

        // Call this from Unity each frame after blitting into InputTexture to push the frame
        // to the encoder.
        public int EncodeFrame()
        {
            if (encoderTyped == null || InputTexture == null) return -1;
            encoderTyped.encodeTexture(InputTexture);
            return 0;
        }

        public void Dispose()
        {
            if (Encoder != null) Encoder.Dispose();
            if (InputTexture != null)
            {
                InputTexture.Release();
                UnityEngine.Object.Destroy(InputTexture);
                InputTexture = null;
            }
        }
    }

    public class AndroidVideoPlayerUnityTexture : IVideoPlayer
    {
        public IDecoder Decoder => decoder;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;
        public object PlatformView => decoder.Preview;
        public int Width => decoder.Width;
        public int Height => decoder.Height;

        private AndroidVideoDecoderTexture decoder;
        public AndroidVideoPlayerUnityTexture(ILogger logger, VoiceInfo info, Action<IVideoPlayer> onReady)
        {
            decoder = new AndroidVideoDecoderTexture(logger, info);
            onReady(this);
        }

        public void Dispose()
        {
            if (decoder != null)
            {
                decoder.Dispose();
            }
        }
    }

    /// <summary>Enumerates cameras available on device.
    /// </summary>
    public class AndroidVideoInEnumerator : DeviceEnumeratorBase
    {
        // android.hardware.camera2.CameraMetadata to FacingEnum
        static Dictionary<int, CameraFacing> androidToVoiceFacing = new Dictionary<int, CameraFacing>()
        {
            {0, CameraFacing.Front},     // LENS_FACING_FRONT
            {1, CameraFacing.Back},      // LENS_FACING_BACK
            {2, CameraFacing.Undef},     // LENS_FACING_EXTERNAL
        };

        public AndroidVideoInEnumerator(ILogger logger) : base(logger)
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
                    using (AndroidJavaClass app = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = app.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (AndroidJavaClass cameraClass = new AndroidJavaClass("com.exitgames.photon.video.Camera"))
                    {
                        var cameraStatic = cameraClass.GetStatic<AndroidJavaObject>("Companion");

                        devices = new List<DeviceInfo>();
                        var javaArr = cameraStatic.Call<AndroidJavaObject>("getDevices", activity);
                        if (javaArr != null)
                        {
                            if (javaArr.GetRawObject().ToInt64() != 0) // Is another null check required? Taken from https://forum.unity.com/threads/passing-arrays-through-the-jni.91757/#post-1432511
                            {
                                String[] arr = AndroidJNIHelper.ConvertFromJNIArray<String[]>(javaArr.GetRawObject());
                                foreach (var id in arr)
                                {
                                    var androidFacing = cameraStatic.Call<int>("getDeviceFacing", activity, id);
                                    var facing = CameraFacing.Undef;
                                    androidToVoiceFacing.TryGetValue(androidFacing, out facing);
                                    DeviceInfo d = new DeviceInfo(id, id, new DeviceFeatures(facing));
                                    devices.Add(d);
                                }
                                Error = null;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.ToString();
                if (Error == null) // should never happen but since Error used as validity flag, make sure that it's not null
                {
                    Error = "Exception in AndroidVideoInEnumerator.Refresh()";
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

    // Extracted from AndroidVideoRecorderSurfaceView for use in AndroidNDKVideoRecorderSurfaceView
    // TODO: use it for composition instead of Encoder inheritance in IVideoRecorder Unity (Java) implementations like it's used in AndroidVideoRecorderSurfaceView
    public class AndroidSurfaceView : IDisposable
    {
        protected class OnReadyCallback : AndroidJavaProxy
        {
            Action<AndroidJavaObject> callback;
            public OnReadyCallback(Action<AndroidJavaObject> callback) : base("com.exitgames.photon.video.SurfaceView$OnReadyCallback")
            {
                this.callback = callback;
            }
            public void onReady(AndroidJavaObject surface)
            {
                if (callback != null)
                {
                    callback(surface);
                }
            }
        }

        OnReadyCallback onReadyCallback;

        public string Error { get; protected set; }
        public AndroidJavaObject View { get; private set; }

        private AndroidJavaObject surfaceView;

        public AndroidSurfaceView(ILogger logger, VoiceInfo info, Action<AndroidJavaObject, IntPtr> onSurfaceReady)
        {
            using (AndroidJavaClass app = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = app.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                surfaceView = new AndroidJavaObject("com.exitgames.photon.video.SurfaceView", activity, new OnReadyCallback((AndroidJavaObject surface) =>
                {
                    logger.Log(LogLevel.Info, "[PV] [UASV] Unity.AndroidSurfaceView ready");
                    IntPtr surfaceNative;
                    using (AndroidJavaObject nativeSurfaceUtil = new AndroidJavaObject("com.exitgames.photon.video.NativeSurfaceUtil"))
                    {
                        surfaceNative = (IntPtr)nativeSurfaceUtil.Call<long>("nativeFromSurface", surface);
                    };
                    onSurfaceReady(surface, surfaceNative);
                }));

                View = surfaceView.Call<AndroidJavaObject>("getView");
                logger.Log(LogLevel.Info, "[PV] [UASV] Unity.AndroidSurfaceView initialized");
            }
        }

        public void Dispose()
        {
            if (surfaceView != null)
            {
                surfaceView.Call("close");
            }
        }

    }

    // Extracted from AndroidCameraVideoEncoderTexture for use in AndroidNDKVideoRecorderUnityTexture
    // TODO: use it for composition instead of Encoder inheritance in IVideoRecorder Unity (Java) implementations like it's used in AndroidNDKVideoRecorderUnityTexture
    public class AndroidTextureView : IDisposable
    {
        public string Error { get; protected set; }
        public Texture Texture { get; private set; }
        public AndroidJavaObject Surface { get; private set; }
        public IntPtr SurfaceNative { get; private set; } // native ASurfaceTexture*
        AndroidJavaObject nativeSurfaceUtil;

        private AndroidJavaObject textureView;

        // Vulkan
        private RenderTexture rt;

        public AndroidTextureView(ILogger logger, VoiceInfo info)
        {
            AndroidVideoGkPluginEventIssuer.Start(logger);

            if (Application.platform == RuntimePlatform.Android && 
                (
                #if !UNITY_2023_1_OR_NEWER
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 || 
                #endif
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3)
                )
            {
                textureView = new AndroidJavaObject("com.exitgames.photon.video.TextureView", info.Width, info.Height);

                var texID = textureView.Call<int>("getTexID");
                if (texID != 0)
                {
                    Texture = Texture2D.CreateExternalTexture(info.Width, info.Height, TextureFormat.ARGB32, false, true, new IntPtr(texID));
                    logger.Log(LogLevel.Info, "[PV] [UATV] AndroidTextureView getTexID: " + texID);
                }
                else
                {
                    Error = "Preview texture ID is 0";
                    logger.Log(LogLevel.Error, "[PV] [UATV] AndroidTextureView getTexID error: {0}", Error);

                    return;
                }
            }
            else if (Application.platform == RuntimePlatform.Android && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                rt = new RenderTexture(info.Width, info.Height, 0, RenderTextureFormat.ARGB32);
                rt.enableRandomWrite = true;
                rt.Create();
                Texture = rt;

                textureView = new AndroidJavaObject("com.exitgames.photon.video.ImageReaderView", rt.GetNativeTexturePtr().ToInt64(), info.Width, info.Height);
            }
            else { 
                Error = "Unsupported platform " + Application.platform + "/" + SystemInfo.graphicsDeviceType;
                logger.Log(LogLevel.Error, "[PV] [UATV] Unity.AndroidTextureView: {0}", Error);
                return;
            }

            Surface = textureView.Call<AndroidJavaObject>("getSurface");
            nativeSurfaceUtil = new AndroidJavaObject("com.exitgames.photon.video.NativeSurfaceUtil");
            SurfaceNative = (IntPtr)nativeSurfaceUtil.Call<long>("nativeFromSurface", Surface);

            logger.Log(LogLevel.Info, "[PV] [UATV] Unity.AndroidTextureView initialized");

        }

        public void Dispose()
        {
            if (nativeSurfaceUtil != null)
            {
                if (SurfaceNative != IntPtr.Zero)
                {
                    nativeSurfaceUtil.Call("releaseNativeWindow", (long)SurfaceNative);
                }
                nativeSurfaceUtil.Dispose();
            }
            if (rt   != null)
            {
                rt.Release();
            }
            if (textureView != null)
            {
                textureView.Call("close");
            }
        }
    }
}
#endif