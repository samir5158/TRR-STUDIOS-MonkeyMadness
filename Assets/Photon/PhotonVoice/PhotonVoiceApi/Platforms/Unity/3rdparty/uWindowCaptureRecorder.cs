#if PHOTON_VOICE_VIDEO_ENABLE
// Basic support for https://github.com/hecomi/uWindowCapture

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && U_WINDOW_CAPTURE_RECORDER_ENABLE

using System;
using UnityEngine;

namespace Photon.Voice.Unity
{
   public class uWindowCaptureRecorder : IVideoRecorderPusher
    {
        uWindowCaptureHost capture;

        const int MAX_IMAGE_QUEUE = 2;

        // use Window.buffer if available
        ImageBufferNative buffer;
        const ImageFormat OUTPUT_IMAGE_FORMAT = ImageFormat.ARGB; // see comment in uDesktopDuplication

        // copy texture otherwise
        TextureFormat textureFormat;
        RenderTexture rt;
        Texture2D t2d;
        ImageBufferNativePool<ImageBufferNativeGCHandleBytes> pushImageBufferPool;
        float encodeInt;
        float nextEncodeTime;
        public uWindowCaptureRecorder(GameObject gameObject, int fps = 30)
        {
            #if UNITY_6000_0_OR_NEWER
            capture = GameObject.FindFirstObjectByType<uWindowCaptureHost>();
            #else
            capture = GameObject.FindObjectOfType<uWindowCaptureHost>();
            #endif
            if (capture == null)
            {
                Debug.LogFormat("uWindowCaptureRecorder: Adding uWindowCaptureRecorder component");
                capture = gameObject.AddComponent<uWindowCaptureHost>();
                capture.Type = global::uWindowCapture.WindowTextureType.Desktop;
            }
            this.encodeInt = 1.0f / fps;
            capture.Register(this, fps, onCaptured);
        }

        void onCaptured()
        {
            if (OnReady == null)
            {
                return;
            }

            if (Time.realtimeSinceStartup < nextEncodeTime)
            {
                return;
            }
            nextEncodeTime = Time.realtimeSinceStartup + encodeInt;

            // use window buffer if available
            if (capture.UseWindowBuffer && capture.Window.buffer != IntPtr.Zero)
            {
                if (!capture.PrevBufferMode || buffer == null || buffer.Info.Width != Width || buffer.Info.Height != Height)
                {
                    Debug.LogFormat("uWindowCaptureRecorder: Window.buffer is available, using it. Window.texture is " + (capture.Window.texture == null ? "not " : "") + "available", capture.Window.texture);
                    buffer = new ImageBufferNative(new ImageBufferInfo(Width, Height, new ImageBufferInfo.StrideSet(1, Width * 4), OUTPUT_IMAGE_FORMAT));

                    OnReady(this);
                }

                if (this.VideoSink != null && VideoSink.PushImageQueueCount < MAX_IMAGE_QUEUE)
                {
                    buffer.Planes[0] = capture.Window.buffer;
                    VideoSink.PushImageAsync(buffer);
                }
            }
            // copy texture otherwise
            else
            {
                int w = Width;
                int h = Height;
                if (capture.PrevBufferMode || pushImageBufferPool == null || rt == null || rt.width != w || rt.height != h)
                {
                    if (capture.Window.buffer == IntPtr.Zero)
                    {
                        Debug.LogFormat("uWindowCaptureRecorder: Window.buffer is not available, using Window.texture");
                    }
                    else
                    {
                        Debug.LogFormat("uWindowCaptureRecorder: Window.buffer is available, using Window.texture because BufferMode is not set");
                    }
                    OnReady(this); // sets Encoder

                    if (rt != null)
                    {
                        rt.Release();
                    }
                    GameObject.Destroy(rt);
                    GameObject.Destroy(t2d);
                    rt = new RenderTexture(w, h, 0);
                    if (!VideoUtil.ImageFormatToUnityTextureFormat.TryGetValue((Encoder as IEncoderDirectImage).ImageFormat, out textureFormat))
                    {
                        throw new Exception("[PV] [VR] " + "VideoRecorder does not support Encoder ImageFormat " + (Encoder as IEncoderDirectImage).ImageFormat);
                    }

                    t2d = new Texture2D(w, h, textureFormat, false);
                    if (pushImageBufferPool != null)
                    {
                        pushImageBufferPool.Dispose();
                    }
                    pushImageBufferPool = new ImageBufferNativePool<ImageBufferNativeGCHandleBytes>(MAX_IMAGE_QUEUE + 1, // 1 more slot for image being processed (neither in queue nor in pool)
                        (pool, info) => new ImageBufferNativeGCHandleBytes(pool, info),
                        "uWindowCaptureRecorder Image",
                        new ImageBufferInfo(Width, Height, new ImageBufferInfo.StrideSet(1, Width * 4), (Encoder as IEncoderDirectImage).ImageFormat)
                        );
                }

                if (this.VideoSink != null && VideoSink.PushImageQueueCount < MAX_IMAGE_QUEUE)
                {
                    Graphics.Blit(capture.Window.texture, rt, new Vector2(1, 1), new Vector2(0, 1));

                    RenderTexture.active = rt;
                    t2d.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    RenderTexture.active = null;

                    var buf = pushImageBufferPool.New();
                    var d = t2d.GetRawTextureData<byte>();
                    d.CopyTo(buf.PlaneBytes[0]);

                    buf.Info.Flip = Flip.None;
                    this.VideoSink.PushImageAsync(buf);
                }
            }
        }

        // IVideoPreview
        public object PlatformView => capture.Window.texture;
        public int Width => capture.Window.width / 16 * 16;
        public int Height => capture.Window.height / 16 * 16;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;
        public IEncoder Encoder { get; set; }

        public string Error => Encoder == null ? "" : Encoder.Error;
        public IVideoSink VideoSink { private get; set; }

        public void Dispose()
        {
            capture.Unregister(this);
            VideoSink = null;
            if (rt != null)
            {
                rt.Release();
                GameObject.Destroy(rt);
                rt = null;
            }
            if (t2d != null)
            {
                GameObject.Destroy(t2d);
                t2d = null;
            }
            buffer = null;
        }

        public Action<uWindowCaptureRecorder> OnReady;
    }
}

#endif
#endif
