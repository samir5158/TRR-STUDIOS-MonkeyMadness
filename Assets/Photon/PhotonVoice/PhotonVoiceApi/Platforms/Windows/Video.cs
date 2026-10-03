#if PHOTON_VOICE_VIDEO_ENABLE
#if PHOTON_VOICE_WINDOWS || UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Photon.Voice.Windows
{
    public class MFTCodec
    {
        const string lib_name = "Video";
        public delegate void LogCallbackDelegate(int instanceID, int level, IntPtr str);

        static LogLevel toVoiceLogLevel(int level)
        {
            switch (level)
            {
                case 0: return LogLevel.Error;
                case 1: return LogLevel.Warning;
                case 2: return LogLevel.Info;
                case 3: return LogLevel.Debug;
                default: return LogLevel.Trace;
            }
        }

    public class VideoEncoder : IEncoderDirectImage
        {
            [DllImport(lib_name)]
            private static extern IntPtr Photon_Video_CreateEncoder(int instanceID, OutCallbackDelegate outCallback, LogCallbackDelegate logCallback, Codec codec, int bitrate, int width, int height, int fps, int keyFrameInt);
            [DllImport(lib_name)]
            private static extern void Photon_Video_Encode(IntPtr handle, IntPtr buf, int width, int height, bool force_keyframe);
            [DllImport(lib_name)]
            private static extern void Photon_Video_DestroyEncoder(IntPtr handler);
            //[DllImport(lib_name)]
            //private static extern void Photon_Video_Encoder_GetInfo(IntPtr handler, out int codec, out int bitrate, out int width, out int height);

            public delegate void OutCallbackDelegate(int instanceID, IntPtr buf, int size, bool keyframe);

            IntPtr handle;
            bool disposed;

            ILogger logger;
            int instanceID;

            private static Dictionary<int, VideoEncoder> handles = new Dictionary<int, VideoEncoder>();
            static int instanceCnt;

            // refs to delegates preventing them from GC'ing
            OutCallbackDelegate outCallbackRef;
            LogCallbackDelegate logCallbackRef;

            public VideoEncoder(ILogger logger, VoiceInfo info)
            {
                this.logger = logger;
                this.instanceID = instanceCnt;
                instanceCnt++;
                lock (handles)
                {
                    handles.Add(this.instanceID, this);
                }

                outCallbackRef = new OutCallbackDelegate(staticOutCallback);
                logCallbackRef = new LogCallbackDelegate(staticLogCallback);
                var handle = Photon_Video_CreateEncoder(this.instanceID, outCallbackRef, logCallbackRef, info.Codec, info.Bitrate, info.Width, info.Height, info.FPS, info.KeyFrameInt);
                lock (this)
                {
                    this.handle = handle;
                }
                if (this.handle == IntPtr.Zero)
                {
                    Error = "Native Encoder creation error";
                }
            }

            public string Error { get; private set; }

            [MonoPInvokeCallbackAttribute(typeof(LogCallbackDelegate))]
            static void staticLogCallback(int instanceID, int level, IntPtr s)
            {
                bool ok;
                VideoEncoder instance;
                lock (handles)
                {
                    ok = handles.TryGetValue(instanceID, out instance);
                }
                if (ok)
                {
                    instance.logCallback(level, s);
                }
            }

            void logCallback(int level, IntPtr s)
            {
                LogLevel voiceLevel = toVoiceLogLevel(level);
                if (logger.Level >= voiceLevel)
                {
                    logger.Log(voiceLevel, "[PV] [VE] " + Marshal.PtrToStringUni(s));
                }

            }

            [MonoPInvokeCallbackAttribute(typeof(OutCallbackDelegate))]
            static void staticOutCallback(int instanceID, IntPtr buf, int len, bool keyframe)
            {
                bool ok;
                VideoEncoder instance;
                lock (handles)
                {
                    ok = handles.TryGetValue(instanceID, out instance);
                }
                if (ok)
                {
                    instance.outCallback(buf, len, keyframe);
                }
            }

            byte[] bufManaged = new byte[0];
            void outCallback(IntPtr buf, int len, bool keyframe)
            {
                FrameFlags flags = 0;
                if (keyframe)
                {
                    flags |= FrameFlags.KeyFrame;
                }
                if (bufManaged.Length < len)
                {
                    bufManaged = new byte[len];
                }
                Marshal.Copy(buf, bufManaged, 0, len);
                Output(new ArraySegment<byte>(bufManaged, 0, len), flags);
            }

            public ImageFormat ImageFormat { get { return ImageFormat.ARGB; } }

            public Action<ArraySegment<byte>, FrameFlags> Output { set; get; }

            private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
            public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
            {
                flags = 0;
                return EmptyBuffer;
            }

            IntPtr buf2;
            int buf2Size;
            IntPtr buf2_UV; // pointer inside buf2
            IntPtr buf3_UV; // separate UV-only temporal buffer
            IntPtr buf3_V; // pointer inside buf3_UV
            public void Input(ImageBufferNative imBuf)
            {
                if (Error != null)
                {
                    return;
                }
                if (Output == null)
                {
                    Error = "Output action is not set";
                    logger.Log(LogLevel.Error, "[PV] [VE] " + Error);
                    return;
                }

                lock (this)
                {
                    if (disposed)
                    {
                        return;
                    }

                    var buf = imBuf.Planes[0];

                    var w = imBuf.Info.Width;
                    var h = imBuf.Info.Height;
                    var wxh = w * h;

                    IntPtr outBuf;
                    if (imBuf.Info.Format == ImageFormat.NV12)
                    {
                        outBuf = buf;
                    }
                    else
                    {
                        int size2 = wxh * 3 / 2;
                        if (buf2Size < size2)
                        {
                            buf2Size = size2;
                            Marshal.FreeHGlobal(buf2);
                            Marshal.FreeHGlobal(buf3_UV);
                            buf2 = Marshal.AllocHGlobal(buf2Size);
                            buf2_UV = new IntPtr(buf2.ToInt64() + wxh);
                            buf3_UV = Marshal.AllocHGlobal(wxh / 2);
                            buf3_V = new IntPtr(buf3_UV.ToInt64() + wxh / 4);
                        }

                        if (imBuf.Info.Format == ImageFormat.ARGB)
                        {
                            LibYUV.LibYUV.ARGBToNV12(
                                buf, w * 4,
                                buf2, w, buf2_UV, w,
                                w, h
                                );
                            outBuf = buf2;
                        }
                        else // convert other foramts to N12 via I420 (no direct conversion methods in libYUV)
                        {
                            YUVConv.ImageFormatConvertFunc formatConvertFunc = YUVConv.ToI420[imBuf.Info.Format];
                            formatConvertFunc(
                                buf, w * 4,
                                buf2, w, buf3_UV, w / 2, buf3_V, w / 2,
                                w, h);
                            // copy temporal UV to buf2
                            LibYUV.LibYUV.I420ToNV12(
                                buf2, w, buf3_UV, w / 2, buf3_V, w / 2,
                                buf2, w, buf2_UV, w,
                                w, h);
                            outBuf = buf2;
                        }
                    }
                    Photon_Video_Encode(handle, outBuf, w, h, false);
                }
            }

            public void EndOfStream()
            {
            }

            public I GetPlatformAPI<I>() where I : class
            {
                return null;
            }

            public void Dispose()
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
                    Marshal.FreeHGlobal(buf2);
                    buf2 = IntPtr.Zero;
                    Marshal.FreeHGlobal(buf3_UV);
                    buf3_UV = IntPtr.Zero;
                    disposed = true;
                }
            }
        }

        public class VideoDecoder : IDecoderDirect<ImageBufferNative>
        {
            [DllImport(lib_name)]
            private static extern IntPtr Photon_Video_CreateDecoder(int instanceID, OutCallbackDelegate outCallback, LogCallbackDelegate logCallback, Codec codec, int frameRate);
            [DllImport(lib_name)]
            private static extern void Photon_Video_Decode(IntPtr handle, IntPtr buf, int bufSize);
            [DllImport(lib_name)]
            private static extern void Photon_Video_DestroyDecoder(IntPtr handler);

            public delegate void OutCallbackDelegate(int instanceID, IntPtr buf, int width, int height, int stride, bool keyframe);

            ILogger logger;
            VoiceInfo info;
            IntPtr handle;
            int instanceID;

            private static Dictionary<int, VideoDecoder> handles = new Dictionary<int, VideoDecoder>();
            static int instanceCnt;

            // refs to delegates preventing them from GC'ing
            OutCallbackDelegate outCallbackRef;
            LogCallbackDelegate logCallbackRef;

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

                outCallbackRef = new OutCallbackDelegate(staticOutCallback);
                logCallbackRef = new LogCallbackDelegate(staticLogCallback);
                var handle = Photon_Video_CreateDecoder(this.instanceID, outCallbackRef, logCallbackRef, info.Codec, info.FPS);
                lock (this)
                {
                    this.handle = handle;
                }
                if (this.handle == IntPtr.Zero)
                {
                    Error = "Native Encoder creation error";
                }
            }

            public string Error { get; private set; }
            public Action<ImageBufferNative> Output { get; set; }

            public void Open(VoiceInfo info)
            {
                logger.Log(LogLevel.Info, "[PV] [VD] " + info.Codec + " initialized");
            }

            public void Input(ref FrameBuffer buf)
            {
                if (buf.Array == null)
                {
                    return;
                }
                if (Error != null)
                {
                    return;
                }
                if (Output == null)
                {
                    Error = "Output action is not set";
                    logger.Log(LogLevel.Error, "[PV] [VD] " + Error);
                    return;
                }

                Photon_Video_Decode(handle, buf.Ptr, buf.Length);
            }

            [MonoPInvokeCallbackAttribute(typeof(OutCallbackDelegate))]
            static void staticOutCallback(int instanceID, IntPtr buf, int width, int height, int stride, bool keyframe)
            {
                bool ok;
                VideoDecoder instance;
                lock (handles)
                {
                    ok = handles.TryGetValue(instanceID, out instance);
                }
                if (ok)
                {
                    instance.outCallback(buf, width, height, stride, keyframe);
                }
            }

            IntPtr buf2;
            void outCallback(IntPtr inBuf, int w, int h, int stride, bool keyframe)
            {
                FrameFlags flags = 0;

                if (keyframe)
                {
                    flags |= FrameFlags.KeyFrame;
                }

                var outBuf = new ImageBufferNative(new ImageBufferInfo(w, h, new ImageBufferInfo.StrideSet(2, stride, stride), ImageFormat.NV12));
                outBuf.Planes[0] = inBuf;
                outBuf.Planes[1] = new IntPtr(inBuf.ToInt64() + stride * h);
                Output(outBuf);
            }

            [MonoPInvokeCallbackAttribute(typeof(LogCallbackDelegate))]
            static void staticLogCallback(int instanceID, int level, IntPtr s)
            {
                bool ok;
                VideoDecoder instance;
                lock (handles)
                {
                    ok = handles.TryGetValue(instanceID, out instance);
                }
                if (ok)
                {
                    instance.logCallback(level, s);
                }
            }

            void logCallback(int level, IntPtr s)
            {
                LogLevel voiceLevel = toVoiceLogLevel(level);
                if (logger.Level >= voiceLevel)
                {
                    logger.Log(voiceLevel, "[PV] [VD] " + Marshal.PtrToStringUni(s));
                }
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
                }
            }
        }
    }
}
#endif
#endif