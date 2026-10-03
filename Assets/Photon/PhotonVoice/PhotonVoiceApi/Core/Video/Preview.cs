#if PHOTON_VOICE_VIDEO_ENABLE
using System;
using System.Collections.Generic;

namespace Photon.Voice
{
    public interface IVideoPreview : IDisposable
    {
        // platform dependent view texture resource
        object PlatformView { get; }
        int Width { get; }
        int Height { get; }
        Rotation Rotation { get; }
        Flip Flip { get; }
    }

    // Video preview and encoder.
    // Video capture wired to Encoder directly.
    // LocalVoice just broadcasts Encoder's output and does not need to consume images.
    public interface IVideoRecorder : IVideoPreview
    {
        IEncoder Encoder { get; }
        string Error { get; }
    }

    // Video capture is a separate module that produces images and pushes them to LocalVoice implementing VideoSink.
    // LocalVoice pushes these images to Encoder and broadcasts its output.
    public interface IVideoRecorderPusher : IVideoRecorder
    {
        // Set the sink the recorder will use for sending captured uncompressed native images.
        IVideoSink VideoSink { set; }
    }

    public interface IVideoPlayer : IVideoPreview
    {
        IDecoder Decoder { get; }
    }

    public class VideoPlayer : IVideoPlayer
    {
        public IDecoder Decoder { get; private set; }
        public object PlatformView { get; private set; }
        public Rotation Rotation => Rotation.Rotate0;
        public Flip Flip => Flip.None;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public VideoPlayer(IDecoder decoder, object platformView, int width, int height, Action<IVideoPlayer> onReady)
        {
            Decoder = decoder;
            this.PlatformView = platformView;
            Width = width;
            Height = height;
            // default implementation assumes that VideoPlayer is ready in constructor (e.g. encoder provides correct dimensions)
            onReady(this);
        }

        public void Dispose()
        {
        }
    }

    public interface IPreviewManager
    {
        void AddView(object id, IVideoPreview view);
        void RemoveView(object id);
        bool Has(object id);
        /// <summary>
        /// Positions the preview of the view / image. Not optimized to be called every frame.
        /// </summary>
        void SetBounds(object id, int x, int y, int w, int h, Flip flip = default(Flip), Rotation rot = Rotation.Rotate0);
    }

    // Collection of views with geometric parameters.
    // Applies flip to positions and sizes in SetBounds.
    // May be a base for specific implementation.
    abstract public class PreviewManager : IPreviewManager
    {
        protected class ViewState
        {
            IVideoPreview view;
            public object PlatformView { get { return view.PlatformView; } }
            public int x { get; set; }
            public int y { get; set; }
            public int w { get; set; }
            public int h { get; set; }
            public ViewState(IVideoPreview v)
            {
                view = v;
            }

            public override string ToString()
            {
                return "view: x=" + x + " y=" + y + " w=" + w + " h=" + h;
            }
        }

        protected Dictionary<object, ViewState> views = new Dictionary<object, ViewState>();

        // call at the end of override
        virtual public void AddView(object id, IVideoPreview view)
        {
            views[id] = new ViewState(view);
            Apply(views[id]);
        }

        // call at the end of override
        virtual public void RemoveView(object id)
        {
            views.Remove(id);
        }

        virtual public bool Has(object id)
        {
            return views.ContainsKey(id);
        }

        /// <summary>
        /// Positions the preview of the view / image. Not optimized to be called every frame.
        /// </summary>
        /// <remarks>
        /// call at the end of override
        /// </remarks>
        virtual public void SetBounds(object id, int x, int y, int w, int h, Flip flip = default(Flip), Rotation rot = Rotation.Rotate0)
        {
            
            ViewState v;
            if (views.TryGetValue(id, out v))
            {
                var yFlipped = flip.IsVertical ? y + h : y;
                var hFlipped = flip.IsVertical ? -h : h;
                if (v.x != x || v.y != yFlipped || v.w != w || v.h != hFlipped)
                {
                    v.x = x;
                    v.y = yFlipped;
                    v.w = w;
                    v.h = hFlipped;
                    Apply(v);
                }
            }
        }

        abstract protected void Apply(ViewState v);
    }

    /// <summary>
    /// Defines how the video source is resized before passing to the encoder.
    /// These options are available depending on the video source.
    /// </summary>
    [Serializable]
    public struct VideoSourceSizeMode
    {
        /// <summary>What sizes to use.</summary>
        public enum Mode
        {
            /// <summary>Use sizes given during the stream initialization.</summary>
            Fixed,
            /// <summary>Adjusts one of the given sizes to keep aspect ratio.</summary>
            Constrained,
            /// <summary>Use source sizes.</summary>
            Source,
        }

        /// <summary>How to initialize the sizes.</summary>
        public Mode Init;

        /// <summary>If true, update the sizes when the source is resized. Ignored if Init is Fixed</summary>
        public bool Update;
    }
}
#endif
