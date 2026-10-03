#if PHOTON_VOICE_VIDEO_ENABLE
// https://gist.github.com/DashW/74d726293c0d3aeb53f4
using System;
using Unity.Collections;
using UnityEngine;

namespace Photon.Voice.Unity
{
    public class ScreenRecorder : MonoBehaviour, IVideoRecorderPusher
    {
        const int MAX_IMAGE_QUEUE = 2;

        public int encoderFPS = 30;
        float nextEncodingRealtime;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidTextureVideoEncoder encoder;
        bool onReadyPending;
#else
        IEncoderDirectImage encoder;
#endif
        TextureFormat textureFormat;
        RenderTexture rt;
        Texture2D t2d;
        ImageBufferNativePool<ImageBufferNativeGCHandleBytes> pushImageBufferPool;

        void Start()
        {
            if (gameObject.GetComponent<Camera>() == null)
            {
                throw new MissingComponentException("ScreenRecorder is attached to an object without Camera component");
            }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (Time.realtimeSinceStartup >= nextEncodingRealtime)
            {
                nextEncodingRealtime = Time.realtimeSinceStartup + 1.0f / encoderFPS;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (encoder != null)
                {
                    if (OnReady != null && onReadyPending)
                    {
                        OnReady(this);
                        onReadyPending = false;
                    }
                    encoder.encodeTexture(src);
                }
#else
                if (this.VideoSink != null && VideoSink.PushImageQueueCount < MAX_IMAGE_QUEUE)
                {
                    int w = Width;
                    int h = Height;
                    if (pushImageBufferPool == null || rt == null || rt.width != w || rt.height != h)
                    {
                        if (rt != null)
                        {
                            rt.Release();
                        }
                        Destroy(rt);
                        Destroy(t2d);
                        rt = new RenderTexture(w, h, 0);
                        t2d = new Texture2D(w, h, textureFormat, false);
                        if (pushImageBufferPool != null)
                        {
                            pushImageBufferPool.Dispose();
                        }
                        pushImageBufferPool = new ImageBufferNativePool<ImageBufferNativeGCHandleBytes>(MAX_IMAGE_QUEUE + 1, // 1 more slot for image being processed (neither in queue nor in pool)
                            (pool, info) => new ImageBufferNativeGCHandleBytes(pool, info),
                            "ScreenRecorder Image",
                            new ImageBufferInfo(Width, Height, new ImageBufferInfo.StrideSet(1, Width * 4), encoder.ImageFormat)
                            );

                        if (OnReady != null)
                        {
                            OnReady(this);
                        }
                    }

                    Graphics.Blit(src, rt, new Vector2(1, -1), new Vector2(0, 1)); // flip vertically

                    RenderTexture.active = rt;
                    t2d.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    RenderTexture.active = null;

                    var buf = pushImageBufferPool.New();
                    NativeArray<byte> txtNative = t2d.GetRawTextureData<byte>();
                    txtNative.CopyTo(buf.PlaneBytes[0]);

                    buf.Info.Flip = Flip.None;
                    this.VideoSink.PushImageAsync(buf);
                }
#endif
            }
            Graphics.Blit(src, dest);
        }

        void Update()
        {
        }

        // IVideoPreview
        public object PlatformView => rt;
        public int Width => Screen.width / 16 * 16;
        public int Height => Screen.height / 16 * 16;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical; // flip preview to compensate for the flip we did in in Blit to produce image with standard orientation
        public IEncoder Encoder
        {
            get { return encoder; }
        }
        public void Init(ILogger logger, VoiceInfo info)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            encoder = new AndroidTextureVideoEncoder(logger, info);
            if (encoder.Error == null)
            {
                onReadyPending = true;
            }
#else
            encoder = Platform.CreateDefaultVideoEncoder(logger, info);
            if (!VideoUtil.ImageFormatToUnityTextureFormat.TryGetValue(encoder.ImageFormat, out textureFormat))
            {
                throw new Exception("[PV] [VR] " + "VideoRecorder does not support Encoder ImageFormat " + encoder.ImageFormat);
            }
#endif
        }

        public string Error => Encoder == null ? "" : Encoder.Error;
        public IVideoSink VideoSink { private get; set; }

        public Action<IVideoRecorder> OnReady { private get; set; }

        public void Dispose()
        {
            encoder.Dispose();
            encoder = null;
#if UNITY_ANDROID && !UNITY_EDITOR
#else
            VideoSink = null;
            if (rt != null)
            {
                rt.Release();
            }
            Destroy(rt);
            Destroy(t2d);

            if (pushImageBufferPool != null)
            {
                pushImageBufferPool.Dispose();
                pushImageBufferPool = null;
            }
#endif
        }
    }
}
#endif
