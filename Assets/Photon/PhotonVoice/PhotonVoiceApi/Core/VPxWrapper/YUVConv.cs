using System;
using System.Collections.Generic;

namespace Photon.Voice
{
    public class YUVConv
    {
        public delegate int ImageFormatConvertFunc(IntPtr src_frame, int src_stride_frame, IntPtr dst_y, int dst_stride_y, IntPtr dst_u, int dst_stride_u, IntPtr dst_v, int dst_stride_v, int width, int height);

        static YUVConv()
        {
            var len = Enum.GetValues(typeof(ImageFormat)).Length;
            if (len != ToI420.Count)
            {
                throw new Exception("[PV] Wrong toEncoderFormat elements count");
            }
            if (len != FromI420.Count)
            {
                throw new Exception("[PV] Wrong fromEncoderFormat elements count");
            }
        }

        public static int ImageFormatNotImplemented(IntPtr src_y, int src_stride_y, IntPtr src_u, int src_stride_u, IntPtr src_v, int src_stride_v, IntPtr dst, int dst_stride, int width, int height)
        {
            return 0;
        }

        public static int ImageFormatBypass(IntPtr src_y, int src_stride_y, IntPtr src_u, int src_stride_u, IntPtr src_v, int src_stride_v, IntPtr dst, int dst_stride, int width, int height)
        {
            return 0;
        }

        public static int Android420ToI420(IntPtr src_y, int src_stride_y, IntPtr src_u, int src_stride_u, IntPtr src_v, int src_stride_v, IntPtr dst, int dst_stride, int width, int height)
        {
            return 0;
        }

        public static Dictionary<ImageFormat, ImageFormatConvertFunc> ToI420 = new Dictionary<ImageFormat, ImageFormatConvertFunc>()
            {
                { ImageFormat.Undefined, ImageFormatBypass},
                { ImageFormat.I420, ImageFormatBypass},
                { ImageFormat.YV12, ImageFormatBypass},
                { ImageFormat.Android420, Android420ToI420},
                { ImageFormat.ABGR, LibYUV.LibYUV.ABGRToI420},
                { ImageFormat.BGRA, LibYUV.LibYUV.BGRAToI420},
                { ImageFormat.ARGB, LibYUV.LibYUV.ARGBToI420},
                { ImageFormat.NV12, ImageFormatNotImplemented},
            };

        public static Dictionary<ImageFormat, ImageFormatConvertFunc> FromI420 = new Dictionary<ImageFormat, ImageFormatConvertFunc>()
            {
                { ImageFormat.Undefined, ImageFormatBypass},
                { ImageFormat.I420, ImageFormatBypass}, // TODO:  I420ToYV12
                { ImageFormat.YV12, ImageFormatBypass},
                { ImageFormat.Android420, Android420ToI420 }, // or unsupported?
                { ImageFormat.ABGR, LibYUV.LibYUV.I420ToABGR},
                { ImageFormat.BGRA, LibYUV.LibYUV.I420ToBGRA},
                { ImageFormat.ARGB, LibYUV.LibYUV.I420ToARGB},
                { ImageFormat.NV12, ImageFormatNotImplemented},
            };

    }
}