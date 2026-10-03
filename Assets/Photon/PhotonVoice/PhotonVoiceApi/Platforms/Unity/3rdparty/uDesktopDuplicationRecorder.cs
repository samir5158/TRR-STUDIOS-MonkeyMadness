#if PHOTON_VOICE_VIDEO_ENABLE
// Basic support for https://github.com/hecomi/uDesktopDuplication

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && U_DESKTOP_DUPLICATION_RECORDER_ENABLE

using UnityEngine;

namespace Photon.Voice.Unity
{
    public class uDesktopDuplicationRecorder : MonoBehaviour, IVideoRecorderPusher
    {
        const int MAX_IMAGE_QUEUE = 2;

        public int encoderFPS = 30;
        float encoderSPF;
        float nextEncodingRealtime;

        uDesktopDuplication.Monitor monitor;
        Texture2D texture;
        ImageBufferNative buf;
        const ImageFormat OUTPUT_IMAGE_FORMAT = ImageFormat.ARGB; // matches TextureFormat.BGRA32 hardcoded in uDesktopDuplication.Monitor

        protected virtual void Awake()
        {
            monitor = uDesktopDuplication.Manager.primary;
            //monitor = Manager.monitors[1];
            monitor.useGetPixels = true;
            // getter initializes texture to which native lib renders
            texture = monitor.texture;

            buf = new ImageBufferNative(new ImageBufferInfo(monitor.width, monitor.height, new ImageBufferInfo.StrideSet(1, monitor.width * 4), OUTPUT_IMAGE_FORMAT));
            encoderSPF = 1.0f / encoderFPS;
        }

        protected virtual void Update()
        {
            if (Time.realtimeSinceStartup < nextEncodingRealtime)
            {
                return;
            }
            nextEncodingRealtime = Time.realtimeSinceStartup + encoderSPF;

            if (this.VideoSink != null && VideoSink.PushImageQueueCount < MAX_IMAGE_QUEUE)
            {
                buf.Planes[0] = monitor.buffer;
                VideoSink.PushImageAsync(buf);
            }

            monitor.shouldBeUpdated = true;
        }

        // IVideoPreview
        public object PlatformView => texture;
        public int Width => texture.width;
        public int Height => texture.height;
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.Vertical;
        public IEncoder Encoder { get; set; }
        public string Error => Encoder == null ? "" : Encoder.Error;
        public IVideoSink VideoSink { private get; set; }

        public void Dispose()
        {
            VideoSink = null;
        }
    }
}
#endif
#endif
