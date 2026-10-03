#if PHOTON_VOICE_VIDEO_ENABLE
#if WINDOWS_UWP || ENABLE_WINMD_SUPPORT
using System;
using System.Runtime.InteropServices;
using Windows.Media.MediaProperties;
using Windows.Media.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Playback;
using System.Collections.Generic;

namespace Photon.Voice.UWP
{
    namespace PlatformAPI
    {
        public interface IMediaCapture
        {
            MediaCapture MediaCapture { get; }
            event MediaCaptureInitConmpleted MediaCaptureInitCompleted;
        }

        public interface IMixedRealityCapture
        {
            bool HologramCompositionEnabled { get; set; }
        }
    }

    public class VideoEncoder : IEncoder
    {
        protected ILogger logger;
        VoiceInfo info;
        CaptureDevice device;
        MediaEncodingProfile mep;
        MediaSource mediaSource;
        protected MediaPlayer mediaPlayer = new MediaPlayer();

        public VideoEncoder(ILogger logger, VoiceInfo info, string deviceID)
        {
            this.logger = logger;
            this.info = info;
            device = new CaptureDevice(logger, CaptureDevice.Media.Video, deviceID);

            // Use the MP4 preset to obtain H.264 video encoding profile
            //            var mep = new MediaEncodingProfile();
            mep = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            mep.Audio = null;
            mep.Container = null;

            //mep.Video = VideoEncodingProperties.CreateH264();
            mep.Video.Bitrate = (uint)info.Bitrate;
            //mep.Video.Subtype = MediaEncodingSubtypes.H264;

            //List<VideoStreamDescriptor> streams = new List<VideoStreamDescriptor>();
            //var encodeProps = VideoEncodingProperties.CreateH264();
            //encodeProps.Bitrate = (uint)info.Bitrate;
            //encodeProps.Subtype = MediaEncodingSubtypes.H264;
            //var stream1Desc = new VideoStreamDescriptor(encodeProps);
            //streams.Add(stream1Desc);
            //mep.SetVideoTracks(streams);

            mep.Video.FrameRate.Numerator = (uint)info.FPS;
            mep.Video.FrameRate.Denominator = 1;

            mep.Video.PixelAspectRatio.Numerator = 1;
            mep.Video.PixelAspectRatio.Denominator = 1;

            mep.Video.Width = (uint)info.Width;
            mep.Video.Height = (uint)info.Height;
        }

        void init()
        {
            try
            {
                device.Initialize();
                device.CaptureFailed += Device_CaptureFailed;
            }
            catch (AggregateException e)
            {
                logger.Log(LogLevel.Error, "[PV] [VE] Device initialization Error: (HResult=" + e.HResult + ") " + e);
                e.Handle((x) =>
                {
                    if (x is UnauthorizedAccessException)
                    {
                        ErrorAccess = true;
                    }
                    Error = x.Message;
                    logger.Log(LogLevel.Error, "[PV] [VE] Device initialization Error (Inner Level 2): (HResult=" + x.HResult + ") " + x);
                    if (x is AggregateException)
                    {
                        (x as AggregateException).Handle((y) =>
                        {
                            Error = y.Message;
                            logger.Log(LogLevel.Error, "[PV] [VE] Device initialization Error (Inner Level 3): (HResult=" + y.HResult + ") " + y);
                            return true;
                        });
                    }
                    return true;
                });
            }
            catch (Exception e)
            {
                Error = e.Message;
                logger.Log(LogLevel.Error, "[PV] [VE] Device initialization Error: " + e);
                return;
            }

            device.SelectPreferredCameraStreamSettingAsync(MediaStreamType.VideoRecord, (p) =>
            {
                var vp = ((VideoEncodingProperties)p);
                return vp.Width == mep.Video.Width && vp.Height == mep.Video.Height;
            }).ContinueWith((r) =>
            {

                if (r.Result == null)
                {
                    var p = (VideoEncodingProperties)device.MediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoRecord);
                    logger.Log(LogLevel.Warning, "[PV] [VE] Video resolution " + mep.Video.Width + "x" + mep.Video.Height + " is not standard, using default resolution for MediaCapture: " + p.Width + "x" + p.Height);
                }

                bool sourceFound = false;
                foreach (var x in device.MediaCapture.FrameSources)
                {
                    if (x.Value.Info.SourceKind == Windows.Media.Capture.Frames.MediaFrameSourceKind.Color)
                    {
                        mediaSource = MediaSource.CreateFromMediaFrameSource(x.Value);
                        mediaPlayer.Source = mediaSource;
                        sourceFound = true;
                        mediaPlayer.Play();
                        break;
                    }
                }

                if (!sourceFound)
                {
                    Error = "Can't find frame source for preview";
                    logger.Log(LogLevel.Error, "[PV] [VE] Device initialization Error: " + Error);
                }

            });

            if (Error == null)
            {
                logger.Log(LogLevel.Info, "[PV] [VE] VideoEncoder successfully created");
            }
        }

        private void Device_CaptureFailed(object sender, MediaCaptureFailedEventArgs e)
        {
            Error = e.Message;
            logger.Log(LogLevel.Error, "[PV] [VE] Error: " + Error);
        }

        public Action<ArraySegment<byte>, FrameFlags> Output
        {
            set
            {
                init();
                if (Error != null)
                {
                    return;
                }
                device.StartRecordingAsync(mep, (buf, flags) =>
                {
                    //                    logger.Log(LogLevel.Info, "[PV] [VE] " + buf.Length + ": " + BitConverter.ToString(buf, 0, buf.Length > 20 ? 20 : buf.Length));
                    if (buf != null)
                    {
                        value(new ArraySegment<byte>(buf), flags);
                    }
                }).ContinueWith((t) =>
                {
                    if (t.Exception == null)
                    {
                        logger.Log(LogLevel.Info, "[PV] [VE] Recording successfully started");
                    }
                    else
                    {
                        t.Exception.Handle((x) =>
                        {
                            Error = x.ToString();
                            logger.Log(LogLevel.Error, "[PV] [VE] Recording starting Error: " + Error);
                            return true;
                        });
                    }
                });
            }
        }

        private static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(new byte[] { });
        public ArraySegment<byte> DequeueOutput(out FrameFlags flags)
        {
            flags = 0;
            return EmptyBuffer;
        }

        public string Error { get; protected set; }
        public bool ErrorAccess { get; private set; }

        public int Width { get { return (int)mep.Video.Width; } }
        public int Height { get { return (int)mep.Video.Height; } }

        public void EndOfStream()
        {
        }

        public I GetPlatformAPI<I>() where I : class
        {
            switch (typeof(I))
            {
                case Type intType when intType == typeof(PlatformAPI.IMediaCapture):
                    return UWPVideoEncoderInstance as I;
                case Type intType when intType == typeof(PlatformAPI.IMixedRealityCapture):
                    return MixedRealityCaptureVideoEncoderInstance as I;
                default:
                    return null;
            }
        }

        public virtual void Dispose()
        {
            if (mediaSource != null)
            {
                mediaSource.Dispose();
                mediaSource = null;
            }
            if (mediaPlayer != null)
            {
                mediaPlayer.Dispose();
                mediaPlayer = null;
            }
            device.StopRecordingAsync().ContinueWith((t) =>
            {
                logger.Log(LogLevel.Info, "[PV] [VE] VideoEncoder disposed");
            });
        }


        // Platform specific implementations

        class UWPVideoEncoder : PlatformAPI.IMediaCapture
        {
            protected CaptureDevice device;
            protected ILogger logger;

            internal UWPVideoEncoder(ILogger logger, CaptureDevice device)
            {
                this.logger = logger;
                this.device = device;
            }
            public MediaCapture MediaCapture
            {
                get
                {
                    return device.MediaCapture;
                }
            }

            public event MediaCaptureInitConmpleted MediaCaptureInitCompleted
            {
                add
                {
                    device.MediaCaptureInitCompletedAdd(value);
                }
                remove
                {
                    device.MediaCaptureInitCompleted -= value;
                }
            }
        }

        class MixedRealityCaptureVideoEncoder : UWPVideoEncoder, PlatformAPI.IMixedRealityCapture
        {
            internal MixedRealityCaptureVideoEncoder(ILogger logger, CaptureDevice device) : base(logger, device)
            {
            }

            // https://docs.microsoft.com/en-us/windows/mixed-reality/develop/platform-capabilities-and-apis/mixed-reality-capture-for-developers

            class VideoEffectDefinition : Windows.Media.Effects.IVideoEffectDefinition
            {

                public string ActivatableClassId => "Windows.Media.MixedRealityCapture.MixedRealityCaptureVideoEffect";

                public IPropertySet Properties { get; set; }
            }

            IPropertySet vfxProps = new PropertySet();
            VideoEffectDefinition vfxDef = new VideoEffectDefinition();
            IMediaExtension vfx;

            async Task<bool> EnsureVFXExist()
            {
                if (vfx == null)
                {
                    //vfxProps.Add("StreamType", MediaStreamType.VideoPreview);
                    //vfxProps.Add("PreferredHologramPerspective", 1);
                    //vfxProps.Add("HologramCompositionEnabled", true);
                    //vfxProps.Add("RecordingIndicatorEnabled", false);
                    //vfxProps.Add("VideoStabilizationEnabled", false);
                    //vfxProps.Add("VideoStabilizationBufferLength", 0);
                    //vfxProps.Add("GlobalOpacityCoefficient", 0.5f);
                    vfxDef.Properties = vfxProps;
                    try
                    {
                        vfx = await MediaCapture.AddVideoEffectAsync(vfxDef, MediaStreamType.VideoRecord);
                    }
                    catch (Exception e)
                    {
                        logger.Log(LogLevel.Error, "[PV] [VE] MixedRealityCaptureVideoEncoder EnsureVFXExist() Exception: " + e);
                    }
                }
                return vfx != null;
            }

            string KeyHologramCompositionEnabled = "HologramCompositionEnabled";
            public bool HologramCompositionEnabled
            {
                set
                {
                    var t = EnsureVFXExist();
                    t.ContinueWith((t1) =>
                    {
                        if (t1.Result)
                        {
                            if (value)
                            {
                                vfxDef.Properties[KeyHologramCompositionEnabled] = true;
                            }
                            else
                            {
                                // TODO: disabling does not work
                                // Setting to false fails in standalone with
                                // System.ArgumentException: Value does not fall within the expected range.
                                // vfxDef.Properties[KeyHologramCompositionEnabled] = false;
                                vfxDef.Properties.Remove(KeyHologramCompositionEnabled);
                            }
                            vfx.SetProperties(vfxProps);
                        }
                    });
                }
                get
                {
                    object res;
                    if (vfxProps.TryGetValue(KeyHologramCompositionEnabled, out res) && res is bool)
                    {
                        return (bool)res;
                    }
                    return false;
                }
            }
        }

        PlatformAPI.IMediaCapture uwpVideoEncoder;
        public PlatformAPI.IMediaCapture UWPVideoEncoderInstance
        {
            get
            {
                if (uwpVideoEncoder == null)
                {
                    uwpVideoEncoder = new UWPVideoEncoder(logger, device);
                }
                return uwpVideoEncoder;
            }
        }

        PlatformAPI.IMixedRealityCapture mixedRealityCaptureVideoEncoder;
        public PlatformAPI.IMixedRealityCapture MixedRealityCaptureVideoEncoderInstance
        {
            get
            {
                if (mixedRealityCaptureVideoEncoder == null)
                {
                    mixedRealityCaptureVideoEncoder = new MixedRealityCaptureVideoEncoder(logger, device);
                }
                return mixedRealityCaptureVideoEncoder;
            }
        }
    }

    public class VideoEncoderMediaPlayerElement : VideoEncoder
    {
        public MediaPlayerElement PreviewMediaPlayerElement { get; private set; }

        public VideoEncoderMediaPlayerElement(ILogger logger, VoiceInfo info, string deviceID, MediaPlayerElement customPreview = null)
            : base(logger, info, deviceID)
        {
            this.PreviewMediaPlayerElement = customPreview;

            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    if (PreviewMediaPlayerElement == null)
                    {
                        PreviewMediaPlayerElement = new MediaPlayerElement();
                    }
                    PreviewMediaPlayerElement.SetMediaPlayer(mediaPlayer);
                    this.PreviewMediaPlayerElement.Visibility = Visibility.Visible;
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    logger.Log(LogLevel.Error, "[PV] [VE] Constructor: Error: " + Error);
                    throw;
                }
            }).AsTask().Wait();

            if (Error == null)
            {
                logger.Log(LogLevel.Info, "[PV] [VE] VideoEncoderMediaPlayerElement created");
            }

        }
        public override void Dispose()
        {
            base.Dispose();
            logger.Log(LogLevel.Info, "[PV] [VE] VideoEncoderMediaPlayerElement disposed");
        }

    }

    public class VideoDecoder : IDecoder
    {
        protected ILogger logger;
        protected VoiceInfo info;
        MediaSource mediaSource;
        protected MediaPlayer mediaPlayer = new MediaPlayer();

        public VideoDecoder(ILogger logger, VoiceInfo info)
        {
            this.logger = logger;
            this.info = info;
        }

        public virtual void Open(VoiceInfo info)
        {
            var videoEncodingProperties = VideoEncodingProperties.CreateH264();
            videoEncodingProperties.Width = (uint)info.Width;
            videoEncodingProperties.Height = (uint)info.Height;
            //videoEncodingProperties.FrameRate.Numerator = (uint)info.FPS;
            //videoEncodingProperties.FrameRate.Denominator = 1;

            var mediaStreamSource = new MediaStreamSource(new VideoStreamDescriptor(videoEncodingProperties))
            {
                // never turn live on because it tries to skip frame which breaks the h264 decoding
                IsLive = false,
                // 0 to avoid too high playback delay?
                BufferTime = TimeSpan.FromSeconds(0.0),
            };

            mediaStreamSource.SampleRequested += this.MediaStreamSource_SampleRequestedDefer;
            mediaSource = MediaSource.CreateFromMediaStreamSource(mediaStreamSource);
            mediaPlayer.Source = mediaSource;
            mediaPlayer.RealTimePlayback = true;
            mediaPlayer.Play();
            startTime = DateTime.Now;
            logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoder opened");
        }

        Queue<byte[]> sampleQueue =  new Queue<byte[]>();
        object sampleQueueLock = new object();
        async private void MediaStreamSource_SampleRequestedDefer(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            var deferral = args.Request.GetDeferral();
            MediaStreamSample sample = await dequeueSample();
            args.Request.Sample = sample;
            deferral.Complete();
        }

        async Task<MediaStreamSample> dequeueSample()
        {
            byte[] buf;
            while (true)
            {
                bool ok = false;
                lock (sampleQueueLock)
                {
                    ok = sampleQueue.TryDequeue(out buf);
                }

                if (ok){
                    var ts = DateTime.Now.Subtract(startTime);
                    MediaStreamSample sample = MediaStreamSample.CreateFromBuffer(buf.AsBuffer(), ts);
                    //sample.Duration = TimeSpan.FromSeconds(1.0f / info.FPS);
                    //sample.KeyFrame = (flags & FrameFlags.KeyFrame) != 0;

                    // see comment in Input()
                    //sample.Processed += (_, __) =>
                    //{
                    //    buf.Release();
                    //};

                    return sample;
                }
                await Task.Delay(1);
            }
        }

        DateTime startTime = DateTime.Now;

        public void Input(ref FrameBuffer buf)
        {
            if (buf.Array == null)
            {
                return;
            }

            // Proper buffer processing relies on FrameBuffer.Release() call in MediaStreamSample.Processed handler...
            // var buf1 = buf;
            // buf.Retain();

            // ... but MediaStreamSample.Processed seems to skip some of the samples passed to mediaStreamSource, leading to leaks. See https://social.msdn.microsoft.com/Forums/en-US/e82113b0-8b6f-4da9-b1a5-36e81ff30284/custom-mediastreamsource-and-memory-leaks-during-samplerequested?forum=winappswithcsharp
            // So we copy the buffer and rely on GC.
            var buf1 = new byte[buf.Length];
            Array.Copy(buf.Array, buf.Offset, buf1, 0, buf.Length);

            lock (sampleQueueLock)
            {
                // in case the sample request callback is not called for some reason, ...
                if (sampleQueue.Count > 5)
                {
                    if ((buf.Flags & FrameFlags.KeyFrame) != 0) // ... clear the queue if keyframe is processed...
                    {
                        //foreach (var s in sampleQueue)
                        //{
                        //    s.Release();
                        //}
                        sampleQueue.Clear();
                        sampleQueue.Enqueue(buf1);
                    }
                    //else // ... or ignore intermediate frame.
                    //{
                    //    buf1.Release();
                    //}
                }
                else
                {
                    sampleQueue.Enqueue(buf1);
                }
            }

        }

        public string Error { get; protected set; }

        public virtual void Dispose()
        {
            if (mediaSource != null)
            {
                mediaSource.Dispose();
                mediaSource = null;
            }
            if (mediaPlayer != null)
            {
                mediaPlayer.Dispose();
                mediaPlayer = null;
            }

            logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoder disposed");
        }
    }

    // https://github.com/marklauter/TelloAPI-SDK-2.0/blob/master/src/Tello.App.UWP/MainPage.xaml.cs
    public class VideoDecoderMediaPlayerElement : VideoDecoder
    {
        public MediaPlayerElement PreviewMediaPlayerElement { get; private set; }

        public VideoDecoderMediaPlayerElement(ILogger logger, VoiceInfo info, MediaPlayerElement customPreview = null)
            : base(logger, info)
        {
            this.PreviewMediaPlayerElement = customPreview;

            if (PreviewMediaPlayerElement == null)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    PreviewMediaPlayerElement = new MediaPlayerElement();
                    this.PreviewMediaPlayerElement.Visibility = Visibility.Visible;
                }).AsTask().Wait();
            }
            if (Error == null)
            {
                logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoderMediaPlayerElement created");
            }
        }

        public override void Open(VoiceInfo info)
        {
            base.Open(info);
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PreviewMediaPlayerElement.SetMediaPlayer(mediaPlayer);
            });
        }

        public override void Dispose()
        {
            base.Dispose();
            logger.Log(LogLevel.Info, "[PV] [VD] VideoDecoderMediaPlayerElement disposed");
        }
    }

    public class PreviewManagerMediaPlayerElement : Photon.Voice.PreviewManager
    {

        [DllImport("__Internal")]
        extern static int GetPageContent([MarshalAs(UnmanagedType.IInspectable)] object frame, [MarshalAs(UnmanagedType.IInspectable)] out object pageContent);

        ILogger logger;
        Canvas previews;

        public PreviewManagerMediaPlayerElement(ILogger logger)
        {
            this.logger = logger;

            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                object pageContent;
                if (Windows.UI.Xaml.Window.Current == null || Windows.UI.Xaml.Window.Current.Content == null)
                {
                    logger.Log(LogLevel.Error, "[PV] [VM] Constructor: Windows.UI.Xaml.Window.Current.Content is null (make sure that Unity Built Type is XAML project)");
                    return;
                }
                var result = GetPageContent(Windows.UI.Xaml.Window.Current.Content, out pageContent);
                if (result < 0)
                {
                    logger.Log(LogLevel.Error, "[PV] [VM] Constructor: failed to get page content, GetPageContent() result: " + result);
                    return;
                }
                var swapChainPanel = pageContent as Windows.UI.Xaml.Controls.SwapChainPanel;
                if (swapChainPanel == null)
                {
                    logger.Log(LogLevel.Error, "[PV] [VM] Constructor: page content is not SwapChainPanel: " + (pageContent == null ? "(null)" : pageContent.GetType().ToString()));
                    return;
                }
                previews = new Canvas();
                swapChainPanel.Children.Add(previews);
                logger.Log(LogLevel.Info, "[PV] [VM] Constructor: success");
            }).AsTask().Wait();
        }

        override public void AddView(object id, IVideoPreview view)
        {
            var fel = view.PlatformView as FrameworkElement;
            if (fel == null)
            {
                logger.Log(LogLevel.Error, "[PV] [VM] AddView: view is not a FrameworkElement: " + view);
            }
            else
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    previews.Children.Add(fel);
                }).AsTask().Wait();
                base.AddView(id, view);
            }
        }

        override public void RemoveView(object id)
        {
            if (views.ContainsKey(id))
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    previews.Children.Remove((FrameworkElement)views[id].PlatformView);
                }).AsTask().Wait();
            }
            else
            {
                logger.Log(LogLevel.Error, "[PV] [VM] RemoveView: id not found: " + id);
            }
            base.RemoveView(id);
        }

        override protected void Apply(ViewState v)
        {
            Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var t = (FrameworkElement)v.PlatformView;
                Canvas.SetLeft(t, v.x);
                Canvas.SetTop(t, v.y);
                t.Width = v.w;
                t.Height = v.h;
                logger.Log(LogLevel.Info, "[PV] [VM] call setViewBounds {0} {1} {2} {3} {4}", v.PlatformView, v.x, v.y, v.w, v.h);
            }).AsTask().Wait();
        }
    }

    public class VideoRecorderMediaPlayerElement : IVideoRecorder
    {
        public IEncoder Encoder { get; protected set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;
        public object PlatformView { get; private set; }

        public int Width { get { return ((VideoEncoder)Encoder).Width; } }
        public int Height { get { return ((VideoEncoder)Encoder).Height; } }

        public string Error => Encoder.Error;

        public VideoRecorderMediaPlayerElement(ILogger logger, VoiceInfo info, string deviceID, Action<IVideoRecorder> onReady)
        {
            var e = new UWP.VideoEncoderMediaPlayerElement(logger, info, deviceID);
            Encoder = e;
            PlatformView = e.PreviewMediaPlayerElement;

            onReady(this);
        }

        public void Dispose()
        {
            if (Encoder != null)
            {
                Encoder.Dispose();
            }
        }
    }
}
#endif
#endif
