#if PHOTON_VOICE_VIDEO_ENABLE
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX

using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Photon.Voice.Unity
{
    // Captures video from the webcam using Unity's WebCamTexture.
    // Sends the captured frames as native images to the local voice pipeline.
    // Exposes the WebCamTexture instance as a preview for embedding in the UI.
    // Stores provided encoder and exposes it for reading by the local voice during creation.
    // Multiple instances of VideoRecorderUnity capturing the same or different webcams at arbitrary resolutions are supported.
    // Recorders with the same webcam and resolution are grouped for efficiency.
    public class VideoRecorderUnity : IVideoRecorderPusher
    {
        #region IVideoPreview
        public object PlatformView => mb.PlatformTexture;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;
        #endregion  

        #region IVideoRecorder
        public IEncoder Encoder { get; private set; }
        public string Error { get; private set; }
        # endregion

        #region IVideoRecorderPusher
        // Delegate VideoSink calls to VideoRecorderUnityMB which actually produces data.
        public IVideoSink VideoSink { set { mb.SetSink(this, value); } }
        #endregion

        VideoRecorderUnityMB mb;

        // VideoRecorderUnityMB per camera name.
        // Each VideoRecorderUnityMB can serve several VideoRecorderUnity instances.
        static Dictionary<string, VideoRecorderUnityMB> mbs = new Dictionary<string, VideoRecorderUnityMB>();
        // Single object to run VideoRecorderUnityMB instance.
        static GameObject gameObject;

        public VideoRecorderUnity(IEncoderDirectImage encoder, GameObject gameObject_not_used, string deviceName, int width, int height, int encoderFPS, Action<IVideoRecorder> onReady)
        {
            TextureFormat textureFormat;
            if (!VideoUtil.ImageFormatToUnityTextureFormat.TryGetValue(encoder.ImageFormat, out textureFormat))
            {
                throw new Exception("[PV] [VR] " + "VideoRecorder does not support Encoder ImageFormat " + encoder.ImageFormat);
            }

            Encoder = encoder;

            if (gameObject == null)
            {
                gameObject = new GameObject("[PV] VideoRecorderUnity: " + deviceName);
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
            }

            // find or create VideoRecorderUnityMB for the given webcam and add our VideoRecorderUnity to it
            if (!mbs.TryGetValue(deviceName, out mb) || mb == null) // if md not found or disposed
            {
                mb = gameObject.AddComponent<VideoRecorderUnityMB>();
                mbs[deviceName] = mb;
                mb.Init(deviceName, encoderFPS, encoder.ImageFormat, textureFormat);
                mb.StartRecord(width, height, encoderFPS);
            }
            mb.AddRecorder(this, width, height,
                () => onReady(this),
                (e) =>
                {
                    Error = e;
                    onReady(this);
                });

            Width = width;
            Height = height;
        }

        public int MemLoop { get { return mb.MemLoop; } set { mb.MemLoop = value; } }      

        public void Dispose()
        {
            mb.RemoveRecorder(this);
        }
    }

    // Runs logic in Update()
    // One instance per webcam is created.
    public class VideoRecorderUnityMB : MonoBehaviour
    {
        // Reference to the recorder and its properties.
        class Rec
        {
            public VideoRecorderUnity rec;
            public Action onReady;
            public Action<string> onError;
            public IVideoSink sink;
        }

        // Group of recorders with the same width and height and properties shared between these instances.
        class RecGroup
        {
            public int w;
            public int h;
            public List<Rec> recs = new List<Rec>();
            public RenderTexture rt;
            public Texture2D webcamTexture2D;
            public ImageBufferNativePool<ImageBufferNativeGCHandleBytes> pushImageBufferPool;
        }

        ImageFormat imageFormat;
        TextureFormat textureFormat;

        const int MAX_IMAGE_QUEUE = 2;

        bool ready = false;
        string error = null;

        // Called when the WebCamTexture successfully started to notify already registered recorders.
        void onReady()
        {
            foreach (var rg in videoRecorders)
            {
                foreach (var r in rg.recs)
                {
                    r.onReady();
                }
            }
        }

        void onError(string s)
        {
            error = s;
            foreach (var rg in videoRecorders)
            {
                foreach (var r in rg.recs)
                {
                    r.onError(s);
                }
            }
        }

        float nextEncodingRealtime;

        WebCamTexture webcamTexture;

        List<RecGroup> videoRecorders = new List<RecGroup>();

        string deviceName;
        float encoderSPF; // sec per frame

        // Editor Inspector
        [ReadOnly]
        public string Device;
        public int StreamCount;

        public object PlatformTexture { get { return webcamTexture; } }

        public void Init(string deviceName, int encoderFPS, ImageFormat imageFormat, TextureFormat textureFormat)
        {
            this.imageFormat = imageFormat;
            this.textureFormat = textureFormat;
            Debug.LogFormat("[PV] [VR] Init");

            this.Device = this.deviceName = deviceName;
            this.encoderSPF = 1.0f / encoderFPS;
        }

        // Adds the recorder to the group.
        // Calls recorder's onReady() if the capture is already started.
        public void AddRecorder(VideoRecorderUnity videoRecorder, int width, int height, Action onReady, Action<string> onError)
        {
            StreamCount++;

            var rg = videoRecorders.Find(x => x.w == width && x.h == height);
            if (rg == null)
            {
                var pushImageBufferPool = new ImageBufferNativePool<ImageBufferNativeGCHandleBytes>(MAX_IMAGE_QUEUE + 1, // 1 more slot for image being processed (neither in queue nor in pool)
                    (pool, info) => new ImageBufferNativeGCHandleBytes(pool, info),
                    "VideoRecorder Image",
                    new ImageBufferInfo(width, height, new ImageBufferInfo.StrideSet(1, width * 4), imageFormat)
                );

                rg = new RecGroup
                {
                    w = width,
                    h = height,
                    recs = new List<Rec>(),
                    rt = new RenderTexture(width, height, 0),
                    webcamTexture2D = new Texture2D(width, height, textureFormat, false),
                    pushImageBufferPool = pushImageBufferPool,
                };
                videoRecorders.Add(rg);
            }

            rg.recs.Add(new Rec()
            {
                rec = videoRecorder,
                onReady = onReady,
                onError = onError,
            });

            if (ready)
            {
                if (error != null)
                {
                    onError(error);
                }
                else
                {
                    onReady();
                }
            }

            Debug.LogFormat("[PV] [VR] StartRecord");
        }

        public void SetSink(VideoRecorderUnity r, IVideoSink sink)
        {
            foreach (var rg in videoRecorders)
            {
                foreach (var rec in rg.recs)
                {
                    if (rec.rec == r)
                    {
                        rec.sink = sink;
                    }
                }
            }
        }

        public void RemoveRecorder(VideoRecorderUnity r)
        {
            StreamCount--;
            foreach (var rg in videoRecorders)
            {
                foreach (var rec in rg.recs)
                {
                    if (rec.rec == r) // if not found r.rec is null
                    {
                        rg.recs.Remove(rec);
                        Debug.LogFormat("[PV] [VR] StopRecord");
                        if (rg.recs.Count == 0)
                        {
                            Debug.LogFormat($"[PV] [VR] {rg.w}x{rg.h} recorders stopped. Remove group");
                            ReleaseWebcamTexture(rg);
                            videoRecorders.Remove(rg);
                            if (this.videoRecorders.Count == 0)
                            {
                                Debug.LogFormat("[PV] [VR] All recorders stopped. Stopping Camera");
                                Destroy(this);
                            }
                        }

                        return;
                    }
                }
            }
        }

        public void StartRecord(int width, int height, int encoderFPS)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                Debug.LogError("No camera devices found.");
                return;
            }
            foreach (var d in devices)
            {
                Debug.LogFormat("[PV] [VR] " + (d.isFrontFacing ? "Cam (front): " : "Cam: " + d.name));
            }

            ReleaseWebcamTexture();

            webcamTexture = new WebCamTexture(this.deviceName, width, height, encoderFPS);

            // apply texture to current object
            //Renderer renderer = GetComponent<Renderer>();
            //renderer.material.mainTexture = webcamTexture;
            webcamTexture.Play();

            StartCoroutine("StartRecordCoroutine");
        }

        public IEnumerator StartRecordCoroutine()
        {
            // workaround for iOS WebCamTexture returning 16x16 after creation
            // https://issuetracker.unity3d.com/issues/ios-webcamtexture-dot-width-slash-height-always-returns-16
            int cnt = 1000;
            if (webcamTexture.width <= 16)
            {
                while (!webcamTexture.didUpdateThisFrame)
                {
                    cnt--;
                    if (cnt == 0)
                    {
                        webcamTexture.Stop();
                        var e = "Waiting for WebCamTexture correct size takes too long";
                        Debug.LogErrorFormat("[PV] [VR] " + e);
                        onError(e);
                        yield break;
                    }
                    else
                    {
                        Debug.LogWarningFormat("[PV] [VR] " + "waiting for WebCamTexture to return the correct size");
                        yield return new WaitForEndOfFrame();
                    }
                }
                webcamTexture.Pause();
                Color32[] colors = webcamTexture.GetPixels32();
                webcamTexture.Stop();

                yield return new WaitForEndOfFrame();

                Debug.LogWarningFormat("[PV] [VR] " + "WebCamTexture is ready, size: " + webcamTexture.width + "x" + webcamTexture.height);
                webcamTexture.Play();
            }

            // Formats not in GraphicsFormat: https://stackoverflow.com/questions/66706477/unity3d-webcamtexture-graphicsformat-value-doesnt-exist#answer-66712711
            // ARGB32 = 88
            Debug.LogFormat("[PV] [VR] WebCamTexture: {0} x {1} format: {2}", webcamTexture.width, webcamTexture.height, webcamTexture.graphicsFormat);

            ready = true;
            this.onReady();
            yield return null;
        }

        public int Width { get { return webcamTexture == null ? 1 : webcamTexture.width; } }
        public int Height { get { return webcamTexture == null ? 1 : webcamTexture.height; } }

        protected virtual void Update()
        {
            if (!ready)
                return;

            if (Time.realtimeSinceStartup < nextEncodingRealtime)
            {
                return;
            }
            nextEncodingRealtime = Time.realtimeSinceStartup + encoderSPF;

            // for each recorder group, get an image buffer, fill it with the current webcamTexture contents, push it through the recorder's sink to the local voice pipeline
            foreach (var rg in videoRecorders)
            {
                var b = rg.pushImageBufferPool.New();

                if (memLoopState == 2)
                {
                    if (memLoopIdx >= memLoopFrames.Length)
                    {
                        memLoopIdx = 0;
                    }
                    Array.Copy(memLoopFrames[memLoopIdx], b.PlaneBytes[0], b.PlaneBytes[0].Length);
                    memLoopIdx++;
                }
                else
                {
                    Graphics.Blit(webcamTexture, rg.rt, new Vector2(1, -1), new Vector2(0, 1)); // flip vertically

                    RenderTexture.active = rg.rt;
                    rg.webcamTexture2D.ReadPixels(new Rect(0, 0, rg.rt.width, rg.rt.height), 0, 0);

                    NativeArray<byte> txtNative = rg.webcamTexture2D.GetRawTextureData<byte>();
                    txtNative.CopyTo(b.PlaneBytes[0]);

                    if (memLoopState == 1)
                    {
                        // store frames for the memory loop
                        if (memLoopIdx < memLoopFrames.Length)
                        {
                            memLoopFrames[memLoopIdx] = (byte[])b.PlaneBytes[0].Clone();
                            memLoopIdx++;
                        }
                        else
                        {
                            memLoopState = 2;
                            memLoopIdx = 0;
                        }
                    }
                }

                foreach (var rec in rg.recs)
                {
                    if (rec.sink != null && rec.sink.PushImageQueueCount < MAX_IMAGE_QUEUE)
                    {
                        b.Retain();
                        rec.sink.PushImageAsync(b);
                    }
                }

                b.Release();
            }
        }

        int memLoopState; // 0 - no loop, 1 - recording to memory, 2- playing back from memory
        int memLoopIdx;
        byte[][] memLoopFrames = new byte[0][];

        // stores specified amount of captured frames in memory, then plays them back instead of capturing
        // set to 0 to disable memory loop
        public int MemLoop
        {
            set
            {
                // sanity check
                if (value > 1000 || value < 0)
                {
                    throw new System.Exception("[PV] [VR] " + "VideoRecorder Loop count parameter is not valid.");
                }
                memLoopFrames = new byte[value][];
                if (value == 0)
                {
                    memLoopState = 0;
                }
                else
                {
                    memLoopState = 1;
                    memLoopIdx = 0;
                }
            }
            get
            {
                return memLoopFrames.Length;
            }
        }

        private void ReleaseWebcamTexture(RecGroup rg)
            {
            if (rg.webcamTexture2D != null)
            {
                Destroy(rg.webcamTexture2D);
                rg.webcamTexture2D = null;
            }
            if (rg.rt != null)
            {
                rg.rt.Release();
                Destroy(rg.rt);
                rg.rt = null;
            }
        }

        private void ReleaseWebcamTexture()
        {
            foreach (var rg in videoRecorders)
            {
                ReleaseWebcamTexture(rg);
            }

            if (webcamTexture != null)
            {
                webcamTexture.Stop();
                webcamTexture = null;
                Debug.Log("[PV] [VR] WebcamTexture stopped and released");
            }
        }

        void OnDestroy()
        {
            Debug.LogFormat("[PV] [VR] OnDestroy()");
            ReleaseWebcamTexture();

            foreach (var rec in videoRecorders)
            {

                if (rec.pushImageBufferPool != null)
                {
                    rec.pushImageBufferPool.Dispose();
                    rec.pushImageBufferPool = null;
                }
            }
        }
    }
}
#endif
#endif
