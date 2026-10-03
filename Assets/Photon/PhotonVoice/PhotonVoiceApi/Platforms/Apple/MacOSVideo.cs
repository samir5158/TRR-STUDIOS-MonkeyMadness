#if PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Photon.Voice.MacOS
{
    public class VideoEncoder : IEncoderDirectImage
    {
        protected const string lib_name = "Video";
        [DllImport(lib_name)]
        private static extern IntPtr Photon_Video_CreateEncoder(int hostID, OutCallbackDelegate callback, int codec, int width, int height, int bitrate, int fps, int keyFrameInt);
        [DllImport(lib_name)]
        private static extern int Photon_Video_Encode(IntPtr handle, IntPtr buf, int width, int height);
        [DllImport(lib_name)]
        private static extern void Photon_Video_DestroyEncoder(IntPtr handle);
        // Render-thread pull-mode: stash next source + CommandBuffer.IssuePluginEventAndData
        // (GetRenderEventFunc, 0, handle) — native runs MTL blit + VT encode on Unity's render
        // thread, after Unity has issued the current frame's GPU commands.
        [DllImport(lib_name)]
        protected static extern void Photon_Video_Encoder_SetPendingSource(IntPtr handle, IntPtr mtlTex);
        [DllImport(lib_name)]
        protected static extern IntPtr Photon_Video_GetRenderEventFunc();

        public delegate void OutCallbackDelegate(int instanceID, IntPtr buf, int size, int flags);

        protected IntPtr handle;
        bool disposed;

        protected ILogger logger;
        int instanceID;

        private static Dictionary<int, VideoEncoder> handles = new Dictionary<int, VideoEncoder>();
        static int instanceCnt;

        // ref to delegate preventing it from GC'ing
        OutCallbackDelegate outCallbackDelegate;

        public VideoEncoder(ILogger logger, VoiceInfo info)
        {
            this.logger = logger;
            this.instanceID = instanceCnt;
            instanceCnt++;
            lock (handles)
            {
                handles.Add(this.instanceID, this);
            }
            outCallbackDelegate = new OutCallbackDelegate(staticOutCallback);
            var handle = Photon_Video_CreateEncoder(this.instanceID, outCallbackDelegate, 0, info.Width, info.Height, info.Bitrate, info.FPS, info.KeyFrameInt);
            lock (this)
            {
                this.handle = handle;
            }
            if (this.handle == IntPtr.Zero)
            {
                Error = "Native Encoder creation error";
            }
        }

        public string Error { get; protected set; }

        [MonoPInvokeCallbackAttribute(typeof(OutCallbackDelegate))]
        static void staticOutCallback(int instanceID, IntPtr buf, int len, int flags)
        {
            bool ok;
            VideoEncoder instance;
            lock (handles)
            {
                ok = handles.TryGetValue(instanceID, out instance);
            }
            if (ok)
            {
                instance.outCallback(buf, len, flags);
            }
        }

        byte[] bufManaged = new byte[0];
        void outCallback(IntPtr buf, int len, int flags0)
        {
            FrameFlags flags = (FrameFlags)flags0;
            if (bufManaged.Length < len)
            {
                bufManaged = new byte[len];
            }
            Marshal.Copy(buf, bufManaged, 0, len);
            if(Output != null){
                Output(new ArraySegment<byte>(bufManaged, 0, len), flags);
            }
            else 
            {
                UnityEngine.Debug.LogError("Encoding started before the local voice had been configured. Skipping frame. ");
            }
        }

        public ImageFormat ImageFormat { get { return ImageFormat.BGRA; } }

        public Action<ArraySegment<byte>, FrameFlags> Output { set; get; }

        private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
        public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
        {
            flags = 0;
            return EmptyBuffer;
        }

        public void Input(ImageBufferNative imBuf)
        {
            if (Error != null)
            {
                return;
            }
            if (disposed)
            {
                return;
            }
            if (Output == null)
            {
                Error = "Output action is not set";
                logger.Log(LogLevel.Error, "[PV] [VE] " + Error);
                return;
            }

            var err = Photon_Video_Encode(handle, imBuf.Planes[0], imBuf.Info.Width, imBuf.Info.Height);
            if (err != 0)
            {
                // the error may be not critical, do not set Error
                // Error = "Native Encoder encoding error " + err;
                logger.Log(LogLevel.Error, "[PV] [VE] Photon_Video_Encode error: " + err);
            }
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
            lock (handles)
            {
                handles.Remove(instanceID);
            }
            lock (this)
            {
                if (handle != IntPtr.Zero)
                {
                    Photon_Video_DestroyEncoder(handle);
                    handle = IntPtr.Zero;
                }
                disposed = true;
            }
        }
    }

#if UNITY_5_3_OR_NEWER // #if UNITY
    // Pull-mode encoder driven by Unity. Caller Graphics.Blit's any source into InputTexture
    // each frame, then calls EncodeFrame() to push it to the hardware encoder.
    //
    // Unity owns the RenderTexture; per call to EncodeFrame() its native MTLTexture ptr is stashed
    // on the encoder and a pre-recorded CommandBuffer is executed. Unity runs the plugin event on
    // its render thread, after the current frame's Graphics.Blit has been submitted — so the native
    // MTL blit + VT encode observes Unity's writes.
    //
    // Do not feed this instance via Input(ImageBufferNative) — push (CPU bytes) and pull (texture)
    // paths share the same native compression session.
    public class VideoEncoderUnityTextureInput : VideoEncoder
    {
        public UnityEngine.RenderTexture InputTexture { get; private set; }

        static readonly IntPtr renderEventFunc = Photon_Video_GetRenderEventFunc();
        UnityEngine.Rendering.CommandBuffer eventCb;

        public VideoEncoderUnityTextureInput(ILogger logger, VoiceInfo info)
            : base(logger, info)
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

    // Pull-mode recorder: no camera capture. App blits any Unity texture into InputTexture
    // and calls EncodeFrame() to push the frame to the hardware encoder.
    // PlatformView is the InputTexture itself (the write target), exposed for callers that want
    // to composite it elsewhere.
    public class VideoRecorderUnityTextureInput : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        VideoEncoderUnityTextureInput encoderTyped;

        public UnityEngine.RenderTexture InputTexture { get { return encoderTyped.InputTexture; } }

        public int Width { get { return InputTexture != null ? InputTexture.width : 0; } }
        public int Height { get { return InputTexture != null ? InputTexture.height : 0; } }
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
#endif

    public class VideoDecoder : IDecoderDirect<ImageBufferNative>
    {
        const string lib_name = "Video";
        [DllImport(lib_name)]
        private static extern IntPtr Photon_Video_CreateDecoder(int hostID, OutCallbackDelegate callback);
        [DllImport(lib_name)]
        private static extern int Photon_Video_Decode(IntPtr handle, IntPtr data, int size, int flags);
        [DllImport(lib_name)]
        private static extern void Photon_Video_DestroyDecoder(IntPtr handle);

        public delegate void OutCallbackDelegate(int instanceID, IntPtr buf, int width, int height, int bytesPerRow);

        bool ready;
        ILogger logger;
        VoiceInfo info;
        IntPtr handle;
        int instanceID;

        private static Dictionary<int, VideoDecoder> handles = new Dictionary<int, VideoDecoder>();
        static int instanceCnt;

        // ref to delegate preventing it from GC'ing
        OutCallbackDelegate outCallbackDelegate;

        public VideoDecoder(ILogger logger, VoiceInfo info)
        {
            this.logger = logger;
            this.info = info;

            this.instanceID = instanceCnt;
            instanceCnt++;
            lock (handles)
            {
                handles.Add(this.instanceID, this);
            }
            outCallbackDelegate = new OutCallbackDelegate(staticOutCallback);
            var handle = Photon_Video_CreateDecoder(this.instanceID, outCallbackDelegate);
            lock (this)
            {
                this.handle = handle;
            }
            if (this.handle == IntPtr.Zero)
            {
                Error = "Native Decoder creation error";
            }
        }

        public string Error { get; private set; }
        public Action<ImageBufferNative> Output { get; set; }

        private Flip flip = Flip.None;
        public void Open(VoiceInfo info)
        {
            ready = true;
            logger.Log(LogLevel.Info, "[PV] [VD] " + info.Codec + " initialized");
        }

        public void Input(ref FrameBuffer buf)
        {
            if (Error != null)
            {
                return;
            }
            if (!ready)
            {
                return;
            }
            if (buf.Array == null)
            {
                return;
            }
            if (Output == null)
            {
                Error = "Output action is not set";
                logger.Log(LogLevel.Error, "[PV] [VD] " + Error);
                return;
            }

            var err = Photon_Video_Decode(handle, buf.Ptr, buf.Length, 0);
            if (err != 0)
            {
                // the error may be not critical, do not set Error
                // Error = "Native Decoder decoding error " + err;
                logger.Log(LogLevel.Error, "[PV] [VD] Photon_Video_Decode error: " + err);
            }
        }

        [MonoPInvokeCallbackAttribute(typeof(OutCallbackDelegate))]
        static void staticOutCallback(int instanceID, IntPtr buf, int width, int height, int bytesPerRow)
        {
            bool ok;
            VideoDecoder instance;
            lock (handles)
            {
                ok = handles.TryGetValue(instanceID, out instance);
            }
            if (ok)
            {
                instance.outCallback(buf, width, height, bytesPerRow);
            }
        }

        void outCallback(IntPtr inBuf, int width, int height, int bytesPerRow)
        {
            Output(new ImageBufferNative(inBuf, width, height, width * 4, ImageFormat.BGRA));
        }

        public void Dispose()
        {
            lock (this)
            {
                if (handle != IntPtr.Zero)
                {
                    Photon_Video_DestroyDecoder(handle);
                    handle = IntPtr.Zero;
                }
                ready = false;
            }
        }
    }
}
#endif
