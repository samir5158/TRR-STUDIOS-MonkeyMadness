#if PHOTON_VOICE_VIDEO_ENABLE
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX

using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Photon.Voice.Unity
{
    public class VideoPlayerUnity : IVideoPlayer
    {
        VideoPlayerUnityMB mb;

        GameObject gameObject;

        public IDecoder Decoder { get; private set; }

        public VideoPlayerUnity(IDecoder d, Action<IVideoPlayer> onReady, GameObject gameObject = null)
        {
            Decoder = d;
            if (gameObject == null)
            {
                gameObject = new GameObject("[PV] VideoPlayerUnity");
                this.gameObject = gameObject;
            }
            mb = gameObject.AddComponent<VideoPlayerUnityMB>();
            mb.OnTextureUpdated += (Texture2D t) =>
            {
                onReady(this);
                if (OnTextureUpdated != null)
                {
                    OnTextureUpdated(t);
                }
            };
        }

        public event Action<Texture2D> OnTextureUpdated;

        public void Draw(ImageBufferNative b)
        {
            mb.Draw(b);
        }
        public object PlatformView => mb.PlatformTexture;
        public int Width => mb.Width;
        public int Height => mb.Height;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;

        public void Dispose()
        {
            UnityEngine.Object.Destroy(mb);
            if (gameObject)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }

    public class VideoPlayerUnityMB : MonoBehaviour
    {
        Texture2D tex2d;

        public object PlatformTexture { get { return tex2d; } }

        public int Width { get { return tex2d == null ? 1 : tex2d.width; } }
        public int Height { get { return tex2d == null ? 1 : tex2d.height; } }

        public event Action<Texture2D> OnTextureUpdated;

        void Awake()
        {
            Debug.LogFormat("[PV] [VP] Awake...");
        }

        // Update is called once per frame
        void Update()
        {
            if (bufReady)
            {
                if (tex2d == null || tex2d.width != lastDrawWidth || tex2d.height != lastDrawHeight || tex2d.format != lastTextureFormat)
                {
                    Destroy(tex2d);
                    tex2d = new Texture2D((int)lastDrawWidth, (int)lastDrawHeight, lastTextureFormat, false);
                    OnTextureUpdated(tex2d);
                }
                tex2d.LoadRawTextureData(buf);
                tex2d.Apply();
                bufReady = false;
            }
        }

        volatile bool bufReady;
        volatile int lastDrawWidth;
        volatile int lastDrawHeight;
        volatile TextureFormat lastTextureFormat;
        volatile ImageFormat lastImageFormat;
        volatile bool disposed;

        IntPtr bufARGB;
        byte[] buf = new byte[4]; // as tex2d inited
        public void Draw(ImageBufferNative b)
        {
            IntPtr bufPtr = b.Planes[0];
            int w = b.Info.Width;
            int h = b.Info.Height;

            if (bufPtr == IntPtr.Zero)
                return;
            lock (this)
            {
                if (disposed)
                    return;
                if (!bufReady)
                {
                    if (lastDrawWidth != w || lastDrawHeight != h)
                    {
                        lastDrawWidth = w;
                        lastDrawHeight = h;
                        buf = new byte[w * h * 4];
                        if (b.Info.Format == ImageFormat.NV12 || b.Info.Format == ImageFormat.I420)
                        {
                            Marshal.FreeHGlobal(bufARGB);
                            bufARGB = IntPtr.Zero;
                        }
                    }
                    if (b.Info.Format == ImageFormat.Undefined)
                    {
                        throw new Exception("[PV] [VP] " + "ImageFormat is not defined in input image");
                    }
                    if (lastImageFormat != b.Info.Format)
                    {
                        lastImageFormat = b.Info.Format;
                        if (!VideoUtil.ImageFormatToUnityTextureFormat.TryGetValue(lastImageFormat, out TextureFormat tmp))
                        {
                            throw new Exception("[PV] [VP] " + "VideoPlayer texture does not support ImageFormat " + lastImageFormat);
                        }
                        lastTextureFormat = tmp;
                    }

                    if (b.Info.Format == ImageFormat.NV12)
                    {
                        if (bufARGB == IntPtr.Zero)
                        {
                            bufARGB = Marshal.AllocHGlobal(w * h * 4);
                        }
                        LibYUV.LibYUV.NV12ToARGB(
                                    b.Planes[0], b.Info.Stride[0],
                                    b.Planes[1], b.Info.Stride[1],
                                    bufARGB, w * 4,
                                    w, h
                                    );
                        Marshal.Copy(bufARGB, buf, 0, buf.Length);
                    }
                    else if (b.Info.Format == ImageFormat.I420)
                    {
                        if (bufARGB == IntPtr.Zero)
                        {
                            bufARGB = Marshal.AllocHGlobal(w * h * 4);
                        }
                        LibYUV.LibYUV.I420ToARGB(
                                    b.Planes[0], b.Info.Stride[0],
                                    b.Planes[1], b.Info.Stride[1],
                                    b.Planes[2], b.Info.Stride[2],
                                    bufARGB, w * 4,
                                    w, h
                                    );
                        Marshal.Copy(bufARGB, buf, 0, buf.Length);
                    }
                    else if (VideoUtil.ImageFormatToUnityTextureFormat.ContainsKey(b.Info.Format)) // everything else is 4x8 formats: texture format set accordingly, no conversion required
                    {
                        Marshal.Copy(bufPtr, buf, 0, buf.Length);
                    }
                    else
                    {
                        throw new Exception("[PV] [VP] " + "VideoPlayer format converter does not support ImageFormat " + b.Info.Format);
                    }

                    bufReady = true;
                }
            }
        }

        void OnDestroy()
        {
            lock (this)
            {
                Marshal.FreeHGlobal(bufARGB);
                bufARGB = IntPtr.Zero;
                Destroy(tex2d);
                disposed = true;
            }
        }
    }
}
#endif
#endif