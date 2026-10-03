#if PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Runtime.InteropServices;
using VPx.Encoder;
using VPx.Codec;
using VPx.Image;
using VPx.Decoder;

namespace Photon.Voice
{
    public class VPxCodec
    {
        // When creating vpx_image_t, use single format for all rgba formats
        // vpx_img_fmt_t misses some ImageFormat' rgba variations.
        static vpx_img_fmt_t formatToVPxImgFmt(ImageFormat f)
        {
            switch (f)
            {
                case ImageFormat.I420: return vpx_img_fmt_t.VPX_IMG_FMT_I420;
                case ImageFormat.YV12: return vpx_img_fmt_t.VPX_IMG_FMT_YV12;
                default: return vpx_img_fmt_t.VPX_IMG_FMT_NONE;
            }
        }

        public class Encoder : IEncoderDirectImage
        {
            struct Frame
            {
                public IntPtr Ptr;
                public vpx_image_t Image;

                // helpers
                public uint w => Image.w;
                public uint h => Image.h;
                public vpx_img_fmt_t fmt => Image.fmt;
                public IntPtr[] planes => Image.planes;
                public int[] stride => Image.stride;

                public void createImage(Encoder enc, vpx_img_fmt_t f, int w, int h)
                {
                    VPx.VPx.vpx_img_free(Ptr);
                    Ptr = VPx.VPx.vpx_img_alloc(IntPtr.Zero, f, (uint)w, (uint)h, 1);
                    if (Ptr == IntPtr.Zero)
                    {
                        enc.die_codec(0, "Failed to allocate image.");
                    }

                    Image = (vpx_image_t)Marshal.PtrToStructure(Ptr, typeof(vpx_image_t));
                }

                public void freeImage()
                {
                    VPx.VPx.vpx_img_free(Ptr);
                    Ptr = IntPtr.Zero;
                }

            }


            IntPtr ctx;
            // video -> scale -> format conversion
            Frame[] frame = new Frame[5];
            Rotation imageRotation = Rotation.Rotate0; // source can swap width and height if orientation changes?
            Flip imageFlip = Flip.None; // encoder never flips (source is responsible for correct orientation)
            YUVConv.ImageFormatConvertFunc formatConvertFunc;
            int pts;
            int frame_count;
            bool disposed;

            // scale source image before encoding
            int encoderWidth;
            int encoderHeight;
            int bitrate;
            int keyFrameInt;
            Codec codec;
            ILogger logger;

            public Encoder(ILogger logger, VoiceInfo info)
            {
                this.logger = logger;
                Open(info);
                logger.Log(LogLevel.Info, "[PV] [VE] VPx.Enc: Encoder created: " + info.Codec + ", version: " + VPx.VPx.vpx_codec_version()
                    + " " + System.Runtime.InteropServices.Marshal.SizeOf(typeof(vpx_codec_enc_cfg))
                     + " " + System.Runtime.InteropServices.Marshal.SizeOf(typeof(vpx_codec_cx_pkt_t))
                     + " " + System.Runtime.InteropServices.Marshal.SizeOf(typeof(vpx_image_t))
                    );
            }

            public string Error { get; private set; }

            void die_codec(vpx_codec_err_t code, string s)
            {
                IntPtr detail = VPx.VPx.vpx_codec_error_detail(ctx);
                IntPtr err = VPx.VPx.vpx_codec_error(ctx);
                Error = s + ". " + Marshal.PtrToStringAnsi(err) + " err=" + code;
                if (detail != IntPtr.Zero)
                    Error += " " + Marshal.PtrToStringAnsi(detail);
                var prefErr = "VPx.Enc: " + Error;
                logger.Log(LogLevel.Error, prefErr);
                throw new Exception(prefErr);
            }

            vpx_codec_enc_cfg cfg = new vpx_codec_enc_cfg();

            bool Open(VoiceInfo info)
            {
                // info dimensions may be wrong at this point, we never rescale in encoder
                //encoderWidth = info.Width;
                //encoderHeight = info.Height;
                // assign 0 to avoid 'never assigned' warning
                encoderWidth = 0;
                encoderHeight = 0;

                bitrate = info.Bitrate;
                keyFrameInt = info.KeyFrameInt;
                codec = info.Codec;
                return true;
            }

            vpx_codec_cx_pkt_t pkt = new vpx_codec_cx_pkt_t();

            public ImageFormat ImageFormat { get { return ImageFormat.BGRA; } }

            public Action<ArraySegment<byte>, FrameFlags> Output { set; get; }

            private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
            public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
            {
                flags = 0;
                return EmptyBuffer;
            }

            bool firstFrame = true;
            public void Input(ImageBufferNative imBuf)
            {
                vpx_codec_err_t err;
                if (Error != null)
                {
                    return;
                }
                if (Output == null)
                {
                    Error = "Output action is not set";
                    logger.Log(LogLevel.Error, "[PV] [VE] VPx.Enc: " + Error);
                    return;
                }

                var buf = imBuf.Planes;
                int srcWidth = imBuf.Info.Width;
                int srcHeight = imBuf.Info.Height;
                var stride = imBuf.Info.Stride;
                ImageFormat srcFormat = imBuf.Info.Format;
                Rotation rotation = imBuf.Info.Rotation;
                Flip flip = imBuf.Info.Flip;

                lock (this)
                {
                    if (disposed)
                    {
                        return;
                    }

                    bool flipDone; //same var used in setup and processing (after reinitialization)
                                   // flip may be done by one of the methods in pipeline
                                   // horizontal: Mirror functionality can also be achieved with the I420Scale and ARGBScale functions by passing negative width and / or height.
                                   // verical: Inverting can be achieved with almost any libyuv function by passing a negative source height.

                    var srcVPxImgFmt = formatToVPxImgFmt(srcFormat);
                    uint pre_rot_w, pre_rot_h;
                    if (ctx == IntPtr.Zero || frame[0].w != srcWidth || frame[0].h != srcHeight || frame[0].fmt != srcVPxImgFmt || imageRotation != rotation || imageFlip != flip)
                    {
                        free();

                        IntPtr iface;
                        switch (codec)
                        {
                            case Codec.VideoVP8:
                                iface = VPx.VPx.vpx_codec_vp8_cx();
                                break;
                            case Codec.VideoVP9:
                                iface = VPx.VPx.vpx_codec_vp9_cx();
                                break;
                            default:
                                throw new Exception("[PV] [VE] VPxCodec.Encoder: Wrong codec " + codec);
                        }

                        ctx = Marshal.AllocHGlobal(vpx_codec_ctx.Size);
                        err = VPx.VPx.vpx_codec_enc_config_default(iface, ref cfg, 0);
                        if (err != 0)
                        {
                            die_codec(err, "vpx_codec_enc_config_default");
                        }
                        cfg.g_error_resilient = vpx_codec_er_flags_t.VPX_ERROR_RESILIENT_DEFAULT;
                        if (bitrate != 0)
                        {
                            cfg.rc_target_bitrate = (uint)bitrate / 1024;
                        }
                        if (encoderWidth != 0)
                        {
                            cfg.g_w = (uint)encoderWidth;
                            cfg.g_h = (uint)(encoderHeight == -1 ? encoderWidth * srcHeight / srcWidth : encoderHeight);
                        }
                        else
                        {
                            cfg.g_w = (uint)srcWidth;
                            cfg.g_h = (uint)srcHeight;
                        }
                        pre_rot_w = cfg.g_w;
                        pre_rot_h = cfg.g_h;
                        if (rotation == Rotation.Rotate90 || rotation == Rotation.Rotate270)
                        {
                            var tmp = cfg.g_w; cfg.g_w = cfg.g_h; cfg.g_h = tmp;
                        }
                        err = VPx.VPx.vpx_codec_enc_init(ctx, iface, ref cfg, 0);
                        if (err != 0)
                        {
                            die_codec(err, "vpx_codec_enc_init");
                        }

                        formatConvertFunc = YUVConv.ToI420[srcFormat];

                        // Conversion is not required if source format is one of 2 supported.
                        vpx_img_fmt_t format = vpx_img_fmt_t.VPX_IMG_FMT_I420;
                        if (srcFormat == ImageFormat.YV12)
                        {
                            format = vpx_img_fmt_t.VPX_IMG_FMT_YV12;
                        }

                        frame[0].createImage(this, srcVPxImgFmt, srcWidth, srcHeight);

                        flipDone = flip == Flip.None;
                        if (formatConvertFunc != YUVConv.ImageFormatBypass)
                        {
                            frame[1].createImage(this, format, srcWidth, srcHeight);
                            if (flip.IsVertical)
                            {
                                flipDone = true;
                            }
                        }

                        if (srcWidth != pre_rot_w || srcHeight != pre_rot_h)
                        {
                            frame[2].createImage(this, format, (int)pre_rot_w, (int)pre_rot_h);
                            flipDone = true;
                        }

                        imageRotation = rotation;
                        imageFlip = flip;
                        if (rotation != Rotation.Rotate0)
                        {
                            frame[3].createImage(this, format, (int)cfg.g_w, (int)cfg.g_h);
                            if (flip.IsVertical)
                            {
                                flipDone = true;
                            }
                        }

                        if (!flipDone)
                        {
                            frame[4].createImage(this, format, (int)cfg.g_w, (int)cfg.g_h);
                        }

                        logger.Log(LogLevel.Info, "[PV] [VE] VPx.Enc: " + codec + " initialized");
                    }

                    flipDone = flip == Flip.None;
                    // - height for vertical flip
                    var flipH = flip.IsVertical ? -1 : 1;
                    // - width for horizontal flip
                    var flipW = flip.IsHorizontal ? -1 : 1;

                    for (int i = 0; i < buf.Length; i++)
                    {
                        frame[0].planes[i] = buf[i];
                        frame[0].stride[i] = stride[i];
                    }
                    int I = 0; // current frame in processing pipe
                    if (formatConvertFunc != YUVConv.ImageFormatBypass)
                    {
                        if (formatConvertFunc == YUVConv.ImageFormatNotImplemented)
                        {
                            throw new NotImplementedException("[PV] [VE] VPx.Enc: image format convertion from " + srcFormat + " to ToI420 is not implemented");
                        }
                        else if (formatConvertFunc == YUVConv.Android420ToI420)
                        {
                            var p = frame[0].planes;
                            var s = frame[0].stride;
                            LibYUV.LibYUV.Android420ToI420(p[0], s[0], p[1], s[1], p[2], s[2], 2,
                                frame[1].planes[0], frame[1].stride[0],
                                frame[1].planes[1], frame[1].stride[1],
                                frame[1].planes[2], frame[1].stride[2],
                                (int)frame[1].w, (int)frame[1].h * flipH);
                        }
                        else
                        {
                            formatConvertFunc(
                                frame[0].planes[0], (int)frame[0].w * 4,
                                frame[1].planes[0], frame[1].stride[0],
                                frame[1].planes[1], frame[1].stride[1],
                                frame[1].planes[2], frame[1].stride[2],
                                (int)frame[1].w, (int)frame[1].h * flipH
                                );
                        }
                        if (flip.IsVertical)
                        {
                            flipDone = true;
                            flipW = flipH = 1;
                        }
                        I = 1;
                    }

                    pre_rot_w = cfg.g_w;
                    pre_rot_h = cfg.g_h;
                    if (rotation == Rotation.Rotate90 || rotation == Rotation.Rotate270)
                    {
                        var tmp = pre_rot_w; pre_rot_w = pre_rot_h; pre_rot_h = tmp;
                    }
                    // TODO: YV12 scale
                    if (srcWidth != pre_rot_w || srcHeight != pre_rot_h)
                    {
                        LibYUV.LibYUV.I420Scale(
                            frame[I].planes[0], frame[I].stride[0],
                            frame[I].planes[1], frame[I].stride[1],
                            frame[I].planes[2], frame[I].stride[2],
                            (int)frame[1].w * flipW, (int)frame[1].h * flipH,
                            frame[2].planes[0], frame[2].stride[0],
                            frame[2].planes[1], frame[2].stride[1],
                            frame[2].planes[2], frame[2].stride[2],
                            (int)frame[2].w, (int)frame[2].h,
                            LibYUV.LibYUV.FilterMode.kFilterNone
                            );
                        flipDone = true;
                        flipW = flipH = 1;
                        I = 2;
                    }

                    if (rotation != Rotation.Rotate0)
                    {
                        LibYUV.LibYUV.I420Rotate(
                            frame[I].planes[0], frame[I].stride[0],
                            frame[I].planes[1], frame[I].stride[1],
                            frame[I].planes[2], frame[I].stride[2],
                            frame[3].planes[0], frame[3].stride[0],
                            frame[3].planes[1], frame[3].stride[1],
                            frame[3].planes[2], frame[3].stride[2],
                            (int)frame[I].w, (int)frame[I].h * flipH,
                            (LibYUV.LibYUV.RotationMode)rotation
                            );
                        if (flip.IsVertical)
                        {
                            flipDone = true;
                            flipW = flipH = 1;
                        }
                        I = 3;
                    }

                    if (!flipDone)
                    {
                        if (flip.IsVertical)
                        {
                            LibYUV.LibYUV.I420Copy(
                                frame[I].planes[0], frame[I].stride[0],
                                frame[I].planes[1], frame[I].stride[1],
                                frame[I].planes[2], frame[I].stride[2],
                                frame[4].planes[0], frame[4].stride[0],
                                frame[4].planes[1], frame[4].stride[1],
                                frame[4].planes[2], frame[4].stride[2],
                                (int)frame[I].w, -(int)frame[I].h
                                );
                        }
                        else
                        {
                            LibYUV.LibYUV.I420Mirror(
                                frame[I].planes[0], frame[I].stride[0],
                                frame[I].planes[1], frame[I].stride[1],
                                frame[I].planes[2], frame[I].stride[2],
                                frame[4].planes[0], frame[4].stride[0],
                                frame[4].planes[1], frame[4].stride[1],
                                frame[4].planes[2], frame[4].stride[2],
                                (int)frame[I].w, (int)frame[I].h
                                );
                        }
                        I = 4;
                    }

                    int flags = 0;
                    if (frame_count % keyFrameInt == 0)
                    {
                        flags |= VPx.Encoder.EncoderConst.VPX_EFLAG_FORCE_KF;
                    }

                    // debug
                    //var p0 = new byte[frame[I].stride[0] * frame[I].h];
                    //var p1 = new byte[frame[I].stride[1] * frame[I].h];
                    //var p2 = new byte[frame[I].stride[2] * frame[I].h];
                    //Marshal.Copy(frame[I].planes[0], p0, 0, p0.Length);
                    //Marshal.Copy(frame[I].planes[1], p1, 0, p1.Length);
                    //Marshal.Copy(frame[I].planes[2], p2, 0, p2.Length);

                    // encode black image to produce the 1st packet which is also a config packet as small as possible
                    if (firstFrame)
                    {
                        Frame black = new Frame();
                        black.createImage(this, frame[I].fmt, (int)frame[I].w, (int)frame[I].h);
                        err = encode(ref black.Image, flags, true);
                        black.freeImage();
                        if (err != vpx_codec_err_t.VPX_CODEC_OK)
                        {
                            return;
                        }
                        firstFrame = false;
                    }

                    err = encode(ref frame[I].Image, flags, false);

                    frame_count++;
                }
            }

            vpx_codec_err_t encode(ref vpx_image_t frame, int flags, bool config)
            {
                vpx_codec_err_t err = VPx.VPx.vpx_codec_encode(ctx, ref frame, pts, 30000000, flags, EncoderConst.VPX_DL_REALTIME);
                if (err != 0)
                {
                    die_codec(err, "Failed to encode frame");
                }
                pts++;

                IntPtr iter;
                IntPtr pktPtr;
                while ((pktPtr = VPx.VPx.vpx_codec_get_cx_data(ctx, out iter)) != IntPtr.Zero)
                {
                    pkt = (vpx_codec_cx_pkt_t)Marshal.PtrToStructure(pktPtr, typeof(vpx_codec_cx_pkt_t));
                    if (pkt.kind == vpx_codec_cx_pkt_kind.VPX_CODEC_CX_FRAME_PKT)
                    {
                        var size = (int)pkt.data.frame.sz;
                        if (payload.Length < size)
                        {
                            payload = new byte[size];
                        }
                        Marshal.Copy(pkt.data.frame.buf, payload, 0, size);

                        FrameFlags frameFlags = 0;

                        if ((pkt.data.frame.flags & EncoderConst.VPX_FRAME_IS_KEY) != 0)
                        {
                            frameFlags |= FrameFlags.KeyFrame;
                        }

                        if (config)
                        {
                            // clear keyframe flag, config is useless as 1st keyframe because we use black image
                            frameFlags = FrameFlags.Config;
                            logger.Log(LogLevel.Info, "[PV] [VE] VPx.Enc: codec config frame " + size + ": " + BitConverter.ToString(payload, 0, size));
                        }

                        Output(new ArraySegment<byte>(payload, 0, size), frameFlags);
                    }
                }

                return vpx_codec_err_t.VPX_CODEC_OK;
            }

            byte[] payload = new byte[0];

            public void EndOfStream()
            {
            }

            public I GetPlatformAPI<I>() where I : class
            {
                return null;
            }

            public void Dispose()
            {
                lock (this)
                {
                    disposed = true;
                    free();
                }
            }
            void free()
            {
                if (ctx != IntPtr.Zero)
                {
                    VPx.VPx.vpx_codec_destroy(ctx);
                    Marshal.FreeHGlobal(ctx);
                    ctx = IntPtr.Zero;
                }
                for (int i = 0; i < frame.Length; i++)
                {
                    frame[i].freeImage();
                }
            }
        }


        public class Decoder : IDecoderDirect<ImageBufferNative>
        {
            IntPtr ctx = Marshal.AllocHGlobal(VPx.Codec.vpx_codec_ctx.Size);
            bool ready;
            ILogger logger;

            public Decoder(ILogger logger)
            {
                this.logger = logger;
                logger.Log(LogLevel.Info, "[PV] [VD] VPx.Dec: Decoder created, version: " + VPx.VPx.vpx_codec_version());
            }

            public string Error { get; private set; }
            public Action<ImageBufferNative> Output { get; set; }

            void die(string s)
            {
                Error = s;
                var prefErr = "VPx.Dec: " + s;
                logger.Log(LogLevel.Error, prefErr);
                throw new Exception(prefErr);
            }

            void die_codec(IntPtr ctx, string s)
            {
                IntPtr detail = VPx.VPx.vpx_codec_error_detail(ctx);
                IntPtr err = VPx.VPx.vpx_codec_error(ctx);
                Error = s + ". " + Marshal.PtrToStringAnsi(err);
                if (detail != IntPtr.Zero)
                    Error += " " + Marshal.PtrToStringAnsi(detail);
                var prefErr = "VPx.Dec: " + Error;
                logger.Log(LogLevel.Error, prefErr);
                throw new Exception(prefErr);
            }

            // logs but doesn't set Error
            void error_frame(IntPtr ctx, string s)
            {
                IntPtr detail = VPx.VPx.vpx_codec_error_detail(ctx);
                IntPtr err = VPx.VPx.vpx_codec_error(ctx);
                s = s + ". " + Marshal.PtrToStringAnsi(err);
                if (detail != IntPtr.Zero)
                    s += " " + Marshal.PtrToStringAnsi(detail);
                logger.Log(LogLevel.Error, s);
            }

            public void Open(VoiceInfo info)
            {
                vpx_codec_dec_cfg cfg = new vpx_codec_dec_cfg();
                cfg.w = 100;
                cfg.h = 100;

                IntPtr iface;
                switch (info.Codec)
                {
                    case Codec.VideoVP8:
                        iface = VPx.VPx.vpx_codec_vp8_dx();
                        break;
                    case Codec.VideoVP9:
                        iface = VPx.VPx.vpx_codec_vp9_dx();
                        break;
                    default:
                        throw new Exception("[PV] [VD] VPx.Dec: VPxCodec.Decoder: Wrong codec " + info.Codec);
                }

                int flags = 0;
                var res = VPx.VPx.vpx_codec_dec_init(ctx, iface, ref cfg, flags);
                if (res != 0)
                {
                    die_codec(ctx, "vpx_codec_dec_init");
                }
                ready = true;

                logger.Log(LogLevel.Info, "[PV] [VD] VPx.Dec: " + info.Codec + " initialized");
            }

            public void Input(ref FrameBuffer buf)
            {
                lock (this)
                {
                    if (!ready)
                        return;
                    if (buf.Array == null)
                        return;

                    if (0 != VPx.VPx.vpx_codec_decode(ctx, buf.Ptr, buf.Length, IntPtr.Zero, 0))
                    {
                        error_frame(ctx, "Failed to decode frame");
                    }

                    IntPtr iter;
                    IntPtr imgPtr;
                    while ((imgPtr = VPx.VPx.vpx_codec_get_frame(ctx, out iter)) != IntPtr.Zero)
                    {
                        vpx_image_t img = (vpx_image_t)Marshal.PtrToStructure(imgPtr, typeof(vpx_image_t));
                        var w = img.d_w;
                        var h = img.d_h;

                        if (this.Output != null)
                        {
                            var imgOut = new ImageBufferNative(new ImageBufferInfo((int)w, (int)h, new ImageBufferInfo.StrideSet(4, img.stride[0], img.stride[1], img.stride[2], img.stride[3]), ImageFormat.I420));
                            imgOut.Planes[0] = img.planes[0];
                            imgOut.Planes[1] = img.planes[1];
                            imgOut.Planes[2] = img.planes[2];
                            imgOut.Planes[3] = img.planes[3];
                            Output(imgOut);
                        }
                    }
                }
            }

            public void Dispose()
            {
                lock (this)
                {
                    ready = false;
                    VPx.VPx.vpx_codec_destroy(ctx);
                    Marshal.FreeHGlobal(ctx);
                    ctx = IntPtr.Zero;
                }
            }
        }
    }
}
#endif