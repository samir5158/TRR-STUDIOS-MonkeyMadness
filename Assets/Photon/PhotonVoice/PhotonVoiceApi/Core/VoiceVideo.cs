// -----------------------------------------------------------------------
// <copyright file="VoiceVideo.cs" company="Exit Games GmbH">
//   Photon Voice API Framework for Photon - Copyright (C) 2017 Exit Games GmbH
// </copyright>
// <summary>
//   Photon data streaming support.
// </summary>
// <author>developer@photonengine.com</author>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace Photon.Voice
{
    // Uncompressed native image buffers consumer
    public interface IVideoSink
    {
        int PushImageQueueCount { get; }
        void PushImageAsync(ImageBufferNative buf);
        void PushImage(ImageBufferNative buf);
    }

    public class VideoSinkArray : IVideoSink
    {
        List<object> ids = new List<object>();
        List<IVideoSink> sinks = new List<IVideoSink>();

        public void Add(IVideoSink s)
        {
            sinks.Add(s);
        }

        public void Remove(IVideoSink s)
        {
            sinks.Remove(s);
        }

        public void Set(object id, IVideoSink s)
        {
            var i = ids.FindIndex(x => x == id);
            if (i == -1)
            {
                ids.Add(id);
                sinks.Add(s);
            }
            else
            {
                sinks[i] = s;
            }
        }

        public int PushImageQueueCount => sinks.Max(x => x.PushImageQueueCount);

        public void PushImage(ImageBufferNative buf)
        {
            buf.Retain(sinks.Count);
            foreach (var s in sinks)
            {
                s.PushImage(buf);
            }
            buf.Release();
        }

        public void PushImageAsync(ImageBufferNative buf)
        {
            buf.Retain(sinks.Count);
            foreach (var s in sinks)
            {
                s.PushImageAsync(buf);
            }
            buf.Release();
        }
    }

    public class LocalVoiceVideo : LocalVoice, IVideoSink
    {
        internal LocalVoiceVideo(VoiceClient voiceClient, byte id, VoiceInfo voiceInfo, int channelId, VoiceCreateOptions opt) : base(voiceClient, id, voiceInfo, channelId, opt)
        {
        }

        bool imageEncodeThreadStarted;
        Queue<ImageBufferNative> pushImageQueue = new Queue<ImageBufferNative>();
        AutoResetEvent pushImageQueueReady = new AutoResetEvent(false);
        public int PushImageQueueCount { get { return pushImageQueue.Count; } }
        public void PushImageAsync(ImageBufferNative buf)
        {
            if (disposed) return;

            if (!imageEncodeThreadStarted)
            {
                voiceClient.logger.Log(LogLevel.Info, LogPrefix + ": Starting image encode thread");
#if NETFX_CORE
                Windows.System.Threading.ThreadPool.RunAsync((x) =>
                {
                    PushImageAsyncThread();
                });
#else
                var t = new Thread(PushImageAsyncThread);
                Util.SetThreadName(t, "[PV] EncImg " + shortName);
                t.Start();
#endif
                imageEncodeThreadStarted = true;
            }

            lock (pushImageQueue)
            {
                pushImageQueue.Enqueue(buf);
            }
            pushImageQueueReady.Set();
        }
        bool exitThread = false;
        private void PushImageAsyncThread()
        {

#if PROFILE
            UnityEngine.Profiling.Profiler.BeginThreadProfiling("PhotonVoice", LogPrefix);
#endif

            try
            {
                while (!exitThread)
                {
                    pushImageQueueReady.WaitOne(); // Wait until data is pushed to the queue or Dispose signals.

#if PROFILE
                    UnityEngine.Profiling.Profiler.BeginSample("Encoder");
#endif

                    while (true) // Dequeue and process while the queue is not empty.
                    {
                        if (exitThread) break; // early exit to save few resources

                        ImageBufferNative b = null;
                        lock (pushImageQueue)
                        {
                            if (pushImageQueue.Count > 0)
                            {
                                b = pushImageQueue.Dequeue();
                            }
                        }

                        if (b != null)
                        {
                            PushImage(b);
                            b.Release();
                        }
                        else
                        {
                            break;
                        }
                    }

#if PROFILE
                    UnityEngine.Profiling.Profiler.EndSample();
#endif

                }
            }
            catch (Exception e)
            {
                voiceClient.logger.Log(LogLevel.Error, LogPrefix + ": Exception in encode thread: " + e);
                throw e;
            }
            finally
            {
                Dispose();
                lock (pushImageQueue)
                {
                    while (pushImageQueue.Count > 0)
                    {
                        pushImageQueue.Dequeue().Dispose();
                    }
                }

#if NETFX_CORE
                pushImageQueueReady.Dispose();
#else
                pushImageQueueReady.Close();
#endif

                voiceClient.logger.Log(LogLevel.Info, LogPrefix + ": Exiting image encode thread");

#if PROFILE
                UnityEngine.Profiling.Profiler.EndThreadProfiling();
#endif

            }
        }

        public void PushImage(ImageBufferNative buf)
        {
            if (this.voiceClient.transport.IsChannelJoined(this.channelId) && this.TransmitEnabled)
            {
                if (this.encoder is IEncoderDirectImage)
                {
                    lock (disposeLock)
                    {
                        if (!disposed)
                        {
                            ((IEncoderDirectImage)this.encoder).Input(buf);
                        }
                    }
                }
                else
                {
                    throw new Exception(LogPrefix + ": PushImage() called on encoder of unsupported type " + (this.encoder == null ? "null" : this.encoder.GetType().ToString()));
                }
            }
        }

        public override void Dispose()
        {
            exitThread = true;
            lock (disposeLock)
            {
                if (!disposed)
                {
                    // objects used for async push disposed in encode thread 'finally'
                    base.Dispose();
                    pushImageQueueReady.Set(); // let worker exit
                }
            }
        }
    }
}