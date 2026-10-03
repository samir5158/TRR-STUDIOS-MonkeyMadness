// Codec ID in config:
// 3 bytes after . described in  Section 7 of RFC 6190.
// We use kVTProfileLevel_H264_Main_AutoLevel on Apple and eAVEncH264VProfile_Main on Windows.
// Some codec IDs from https://developer.apple.com/library/archive/documentation/NetworkingInternet/Conceptual/StreamingMediaGuide/FrequentlyAskedQuestions/FrequentlyAskedQuestions.html :
// AAC-LC                           "mp4a.40.2"
// HE-AAC                           "mp4a.40.5"
// MP3                              "mp4a.40.34"
// H.264 Baseline Profile level 3.0 "avc1.42001e" or "avc1.66.30"   Note: Use "avc1.66.30" for compatibility with iOS versions 3.0 to 3.1.2.
// H.264 Baseline Profile level 3.1 "avc1.42001f"
//                                  "avc1.42e01f" ? // https://developers.cloudflare.com/stream/webrtc-beta/ h264 (Constrained Baseline Profile Level 3.1, referred to as 42e01f in the SDP offer’s profile-level-id parameter.)
// H.264 Main Profile level 3.0     "avc1.4d001e" or "avc1.77.30"   Note: Use "avc1.77.30" for compatibility with iOS versions 3.0 to 3.12.
// H.264 Main Profile level 3.1     "avc1.4d001f"
// H.264 Main Profile level 4.0     "avc1.4d0028"
// H.264 High Profile level 3.1     "avc1.64001f"
// H.264 High Profile level 4.0     "avc1.640028"
// H.264 High Profile level 4.1     "avc1.640029"

// Encoder configuration samples from  https://w3c.github.io/webcodecs/samples/encode-decode-worker/js/main.js <- https://w3c.github.io/webcodecs/samples/encode-decode-worker/index.html <- https://w3c.github.io/webcodecs/samples/
// switch (preferredCodec)
// {
//     case "H264":
//         config.codec = "avc1.42002A";  // baseline profile, level 4.2
//         config.avc = { format: "annexb" };
//         config.pt = 1;
//         break;
//     case "H265":
//         config.codec = "hvc1.1.6.L123.00"; // Main profile, level 4.1, main Tier
//         config.hevc = { format: "annexb" };
//         config.pt = 2;
//         break;
//     case "VP8":
//         config.codec = "vp8";
//         config.pt = 3;
//         break;
//     case "VP9":
//         config.codec = "vp09.00.10.08"; //VP9, Profile 0, level 1, bit depth 8
//         config.pt = 4;
//         break;
//     case "AV1":
//         config.codec = "av01.0.08M.10.0.110.09" // AV1 Main Profile, level 4.0, Main tier, 10-bit content, non-monochrome, with 4:2:0 chroma subsampling
//         config.pt = 5;
//         break;
// }

mergeInto(LibraryManager.library, {

    // used internally only to share the table between functions
    PhotonVoice_WebCodecsVideo_EncoderConfig: function() {
        return {
            VideoH264: {
                codec: "avc1.42002A", // baseline profile, level 4.2
                avc: { format: "annexb" },
            },
            VideoH265: {
                // from chrome samples (only decoder works): https://github.com/w3c/webtransport/blob/55eb1637feb6caa338e43271ded53cea581a8ff0/samples/webcodecs-echo/js/main.js#L429
                codec: "hev1.1.6.L120.B0",  // Main profile, level 4, up to 2048 x 1080@30
                //codec: "hev1.1.4.L93.B0", // Main 10
                //codec: "hev1.2.4.L120.B0", // Main 10, Level 4.0
                //codec: "hev1.1.6.L93.B0", // Main profile, level 3.1, up to 1280 x 720@33.7
                hevc: { format: "annexb" },
            },
            VideoVP8: {
                codec: "vp8",
            },
            VideoVP9: {
                codec: "vp09.00.10.08", //VP9, Profile 0, level 1, bit depth 8
            },
            VideoAV1: {
                codec: "av01.0.05M.08",// from youtube player, also "av01.0.08M.08" and "av01.0.12M.10.0.110.09.16.09.0"
                // from chrome samples (does not work): https://github.com/w3c/webtransport/blob/55eb1637feb6caa338e43271ded53cea581a8ff0/samples/webcodecs-echo/js/main.js#L429
                //codec: "av01.0.08M.10.0.110.09", // AV1 Main Profile, level 4.0, Main tier, 10-bit content, non-monochrome, with 4:2:0 chroma subsampling
            },
        }
    },
    
    /////////// Encoder

    PhotonVoice_WebCodecsVideoEncoder_Start: async function(managedEncObj, codec, width, height, bitrate, createCallback, dataCallback) {
        const codecStr = UTF8ToString(codec);
        
        function onOutput(buf, metadata) {
            const ptr = _malloc(buf.byteLength);
            const arr = new Int8Array(HEAPU8.buffer, ptr, buf.byteLength);
            buf.copyTo(arr);
            {{{ makeDynCall('viiii', 'dataCallback') }}}(managedEncObj, ptr, buf.byteLength, buf.type == "key");
            _free(ptr);

            if (metadata.decoderConfig) {
                // Decoder needs to be configured (or reconfigured) with new parameters
                // when metadata has a new decoderConfig.
                // Usually it happens in the beginning or when the encoder has a new
                // codec specific binary configuration. (VideoDecoderConfig.description).
                console.info('[PV] PhotonVoice_WebCodecsVideoEncoder_Start: onOutput metadata.decoderConfig', metadata.decoderConfig);
            }
        }

        const codecConfig =_PhotonVoice_WebCodecsVideo_EncoderConfig()[codecStr];
        if (!codecConfig) {
            console.error('[PV] PhotonVoice_WebCodecsVideoEncoder_Start ' + codecStr + ' error: codec not found');
            {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 1);
            return;
        }
        const config = {
            width: width,
            height: height,
            bitrate: bitrate,
            optimizeForLatency: true,
//                latencyMode: "realtime", // "quality" 
//                bitrateMode: "variable", // "constant", "quantizer"
//                hardwareAcceleration: "prefer-hardware", // "prefer-software", "no-preference"
        };
        Object.assign(config, codecConfig);
       
        try {
            const { supported } = await VideoEncoder.isConfigSupported(config);
            if (!supported) {
                console.error('[PV] PhotonVoice_WebCodecsVideoEncoder_Start', codecStr, 'error: config is not supported', config);
                {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 2);           
                return;
            }
        } catch (err) {
            console.error('[PV] PhotonVoice_WebCodecsVideoEncoder_Start', codecStr, config, 'isConfigSupported error:', err.message);
            {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 3);
            return;
        }

        Module.PhotonVoice_WebCodecsVideoEncoder_Cnt = (Module.PhotonVoice_WebCodecsVideoEncoder_Cnt || 1); // skip 0
        let encoderID = Module.PhotonVoice_WebCodecsVideoEncoder_Cnt++;
        
        const encoder = new VideoEncoder({
            output: onOutput,
            error: (e) => {
                console.error('[PV] PhotonVoice_WebCodecsVideoEncoder_Start', 'Encoder error:', e);
                _PhotonVoice_WebCodecsVideoEncoder_Stop(encoderID);
                {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, encoderID, 11);
            }
        });

        try {
            encoder.configure(config);
            console.info('[PV] PhotonVoice_WebCodecsVideoEncoder_Start: encoder configured ' + codecStr);
        } catch (err) {
            console.error('[PV] PhotonVoice_WebCodecsVideoEncoder_Start ' + codecStr + ' error:', err.message);
            {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 4);
            return;
        }

        Module.PhotonVoice_WebCodecsVideoEncoder_Ctx = Module.PhotonVoice_WebCodecsVideoEncoder_Ctx || new Map();
        Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.set(encoderID, {
            encoder: encoder
        });
        {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, encoderID, 0);
    },
    
    // IEncoderDirectImage, not tested, not used
    PhotonVoice_WebCodecsVideoEncoder_Input: async function(managedEncObj, ptr, width, height, keyFrame) {
        const encoder = Module.PhotonVoice_WebCodecsVideoEncoder_Ctx && Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.get(encoderID) && Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.get(encoderID).encoder;
        if (!encoder) {
            console.error('[PV] PhotonVoice_WebCodecsVideoEncoder_Input: encoder is not found');
        }

        if (encoder.encodeQueueSize > 2) {
            console.warn("[PV] PhotonVoice_WebCodecsVideoCapture: dropping frame due to too many frames in encoder's queue");
        } else {
            const arr = new Int8Array(HEAPU8.buffer, ptr, width * height);
            const frame = new VideoFrame(arr, width, height);
            encoder.encode(frame, {
                keyFrame: keyFrame
            });
        }   
    },

    PhotonVoice_WebCodecsVideoEncoder_Stop: function(encoderID) {
        console.info('[PV] WebCodecsVideoEncoder_Stop');
        const ctx = Module.PhotonVoice_WebCodecsVideoEncoder_Ctx && Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.get(encoderID);
        if (ctx) {
            if (Module.PhotonVoice_WebCodecsVideoCapture_Ctx) {
                Module.PhotonVoice_WebCodecsVideoCapture_Ctx.forEach(capture => {
                    if (capture.encoder == ctx.encoder) {
                        capture.encoder = null;
                    }
                })
            }
            ctx.encoder.close();
            Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.delete(encoderID);
        }
    },

    /////////// Video Capture

    PhotonVoice_WebCodecsVideoCapture_CameraStart: function(managedCamObj, deviceId, width, height, frameRate, keyFrameInterval, createCallback, changeCallback) {
        const deviceIdStr = deviceId ? UTF8ToString(deviceId) : "";
        let constr = {
            video: {
                width: width,
                height: height,
                frameRate: frameRate,
                deviceId: deviceIdStr == "" ? undefined : {
                    exact: deviceIdStr
                }
            }
        };

        _PhotonVoice_WebCodecsVideoCapture_Start("PhotonVoice_WebCodecsVideoCapture_CameraStart", navigator.mediaDevices && navigator.mediaDevices.getUserMedia, constr, managedCamObj, keyFrameInterval, createCallback, changeCallback);
    },
    
    PhotonVoice_WebCodecsVideoCapture_ScreenShareStart: function(managedCamObj, width, height, frameRate, keyFrameInterval, createCallback, changeCallback) {
        let constr = {
            video: {
                frameRate: frameRate,
                displaySurface: "monitor",
            },
            surfaceSwitching: "include",
        };
        if (width > 0) {
            constr.video.width = width;
            constr.video.height = height;
        }

        _PhotonVoice_WebCodecsVideoCapture_Start("PhotonVoice_WebCodecsVideoCapture_ScreenShareStart", navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia, constr, managedCamObj, keyFrameInterval, createCallback, changeCallback);
    },

    PhotonVoice_WebCodecsVideoCapture_SetEncoderAndPreview: function(managedCamObj, encoderID, previewTex) {
        const capture = Module.PhotonVoice_WebCodecsVideoCapture_Ctx && Module.PhotonVoice_WebCodecsVideoCapture_Ctx.get(managedCamObj);
        const encoder = Module.PhotonVoice_WebCodecsVideoEncoder_Ctx && Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.get(encoderID) && Module.PhotonVoice_WebCodecsVideoEncoder_Ctx.get(encoderID).encoder;
        const tex = GL.textures[previewTex];
        if (capture && encoder) {
            capture.encoder = encoder;
            capture.previewTex = tex;
            console.info('[PV] PhotonVoice_WebCodecsVideoCapture_SetEncoderAndPreview set:', capture, "<-", encoder, tex);
            return true;
        } else {
            console.info('[PV] PhotonVoice_WebCodecsVideoCapture_SetEncoderAndPreview waiting: ', capture, "<-", encoder, tex);
            return false;
        }
    },
    
    // used internally only
    PhotonVoice_WebCodecsVideoCapture_Start: function(prefix, getMedia, constr, managedCamObj, keyFrameInterval, createCallback, changeCallback) {
                
        // avoid change callback call on first call if sizes are set
        let codedWidth = constr.video.width;
        let codedHeight = constr.video.height;
        
        let keyIntMs = keyFrameInterval * 1000 / constr.video.frameRate;
        let keyIntMsExt = keyFrameInterval * 1100 / constr.video.frameRate; // + 10% to give a normal keyframe a chance to trigger
        
        if (getMedia) {

            console.log('[PV] ' + prefix + ': getMedia');
            // waits for the user to grant mic permission
            getMedia.call(navigator.mediaDevices, constr)
                .then(async function(stream) {
                    // mic permission granted

                    var video = document.createElement("video");

                    video.srcObject = stream;
                    video.play();

                    const track = stream.getTracks()[0];

                    const trackProcessor = new MediaStreamTrackProcessor(track);

                    Module.PhotonVoice_WebCodecsVideoCapture_Ctx = Module.PhotonVoice_WebCodecsVideoCapture_Ctx || new Map();
                    Module.PhotonVoice_WebCodecsVideoCapture_Ctx.set(managedCamObj, {
                        track: track
                    });

                    const reader = trackProcessor.readable.getReader();

                    console.log('[PV] ' + prefix + ': input created');
                    {{{ makeDynCall('vii', 'createCallback') }}}(managedCamObj, 0);
                    
                    let frameCounter = 0;
                    let nextKeyFrameTime = 0;
                    let frame = null;
                    let keyFrameTimer = null;
                    while (true) {
                        const capture = Module.PhotonVoice_WebCodecsVideoCapture_Ctx.get(managedCamObj)
                        if (!capture) {
                            break; // Stop called
                        }
                        
                        const result = await reader.read();
                        if (result.done) {
                            console.info("[PV] PhotonVoice_WebCodecsVideoCapture: stream complete");
                            break;
                        }
                        if (frame) {
                            frame.close();
                        }
                    
                        frame = result.value;
                        
                        if (codedWidth != frame.codedWidth || codedHeight != frame.codedHeight) {
                            codedWidth = frame.codedWidth;
                            codedHeight = frame.codedHeight;
                            if (changeCallback) {
                                {{{ makeDynCall('viii', 'changeCallback') }}}(managedCamObj, codedWidth, codedHeight)
                            }
                            frameCounter = 0; // make sure we issue a keyframe when sizes and possible encoder change; this keyframe still may be missed by the remote client
                        }
                        
                        const encoder = capture.encoder;
                        const tex = capture.previewTex;
                        
                        //console.info("[PV] ============== ", frame.codedWidth, frame.codedHeight, frame.displayWidth, frame.displayHeight);
                        if (tex) {
                            GLctx.bindTexture(GLctx.TEXTURE_2D, tex);
                            GLctx.texSubImage2D(GLctx.TEXTURE_2D, 0, 0, 0, GLctx.RGBA, GLctx.UNSIGNED_BYTE, video);
                        }
                        
                        if (encoder) {
                            if (encoder.encodeQueueSize > 2) {
                                console.warn("[PV] PhotonVoice_WebCodecsVideoCapture: dropping frame due to too many frames in encoder's queue");
                            } else {
                                const time = new Date().valueOf();
                                //console.info("[PV] ============== ", frameCounter, nextKeyFrameTime, time, nextKeyFrameTime - time);
                                
                                // issue a keyframe each keyFrameInterval (happens normally) or after keyIntMs since the last keyframe (happens when some frames were skipped by the video capture, e.g. by a screen share of a static window)
                                const keyFrame = frameCounter % keyFrameInterval == 0 || time >= nextKeyFrameTime;
                                encoder.encode(frame, {
                                    keyFrame: keyFrame
                                });
                                
                                if (keyFrame) {
                                    // reset the next keyframe counter and time
                                    frameCounter = 1;
                                    nextKeyFrameTime = time + keyIntMs;
                                    //console.info("[PV] ============== KEYFRAME", frameCounter, nextKeyFrameTime);
                                    
                                    // send the last frame as a keyframe every keyIntMsExt if the video capture does not produce any frames like a screen share of a static window
                                    clearInterval(keyFrameTimer)
                                    keyFrameTimer = setInterval(() => {
                                        const capture = Module.PhotonVoice_WebCodecsVideoCapture_Ctx.get(managedCamObj)
                                        if (capture) {
                                            capture.encoder.encode(frame, {
                                                keyFrame: true
                                            });
                                            //console.info("[PV] ============== RESEND", frameCounter, nextKeyFrameTime);
                                            
                                            // reset the next keyframe counter and time
                                            frameCounter = 1;
                                            nextKeyFrameTime = new Date().valueOf() + keyIntMs;
                                        }
                                    }, keyIntMsExt);
                                } else  {
                                    frameCounter++;
                                }
                            }
                        }
                        
                    }

                    if (frame) {
                        frame.close();
                    }
                    clearInterval(keyFrameTimer)

                    console.log('[PV] ' + prefix + ': stopped');
                })
                .catch(function(err) {
                    console.error('[PV] ' + prefix + ' getMedia error:', err.message);
                    {{{ makeDynCall('vii', 'createCallback') }}}(managedCamObj, 2);
                    return;
                });
        } else {
            console.error('[PV] ' + prefix + ': getMedia not supported on your browser!');
            {{{ makeDynCall('vii', 'createCallback') }}}(managedCamObj, 3);
            return;
        }
    },

    PhotonVoice_WebCodecsVideoCapture_Stop: function(managedCamObj) {
        console.info('[PV] PhotonVoice_WebCodecsVideoCapture_Stop');
        const ctx = Module.PhotonVoice_WebCodecsVideoCapture_Ctx && Module.PhotonVoice_WebCodecsVideoCapture_Ctx.get(managedCamObj);
        if (ctx) {
            ctx.track.stop();
            Module.PhotonVoice_WebCodecsVideoCapture_Ctx.delete(managedCamObj);
        }
    },

    /////////// Decoder

    PhotonVoice_WebCodecsVideoDecoder_Start: async function(managedEncObj, codec, previewTex, createCallback) {
        const codecStr = UTF8ToString(codec);
        
        const tex = GL.textures[previewTex];

        function onOutput(frame) {
            GLctx.bindTexture(GLctx.TEXTURE_2D, tex);
            GLctx.texSubImage2D(GLctx.TEXTURE_2D, 0, 0, 0, GLctx.RGBA, GLctx.UNSIGNED_BYTE, frame);
            frame.close();
        }

        const codecConfig =_PhotonVoice_WebCodecsVideo_EncoderConfig()[codecStr];
        if (!codecConfig) {
            console.error('[PV] PhotonVoice_WebCodecsVideoDecoder_Start ' + codecStr + ' error: codec not found');
            {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 1);
            return;
        }
        // take only config string from the encoder definition
        const config = {
            codec: codecConfig.codec,
        };

        try {
            const { supported } = await VideoDecoder.isConfigSupported(config);
            if (!supported) {
                console.error('[PV] PPhotonVoice_WebCodecsVideoDecoder_Start', codecStr, 'error: config is not supported', config);
                {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 2);
                return;
            }
        } catch (err) {
            console.error('[PV] PhotonVoice_WebCodecsVideoDecoder_Start ', codecStr, config, 'isConfigSupported error:', err.message);
            {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 3);
            return;
        }

        Module.PhotonVoice_WebCodecsVideoDecoder_Cnt = (Module.PhotonVoice_WebCodecsVideoDecoder_Cnt || 1); // skip 0
        let decoderID = Module.PhotonVoice_WebCodecsVideoDecoder_Cnt++;

        const decoder = new VideoDecoder({
            output: onOutput,
            error: (e) => {
                console.error('[PV] PhotonVoice_WebCodecsVideoDecoder_Start', 'Decoder error:', e);
                _PhotonVoice_WebCodecsVideoDecoder_Stop(decoderID);
                {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, decoderID, 11);
            }
        });
        
        try {
            decoder.configure(config);
            console.info('[PV] PhotonVoice_WebCodecsVideoDecoder_Start: decoder ' + codecStr + ' configured');
        } catch (err) {
            console.error('[PV] PhotonVoice_WebCodecsVideoDecoder_Start ' + codecStr + ' error:', err.message);
            {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, 0, 4);
            return;
        }

        Module.PhotonVoice_WebCodecsVideoDecoder_Ctx = Module.PhotonVoice_WebCodecsVideoDecoder_Ctx || new Map();
        Module.PhotonVoice_WebCodecsVideoDecoder_Ctx.set(decoderID, {
            decoder: decoder,
            isAV1: codecStr == "VideoAV1"
        });
        
        {{{ makeDynCall('viii', 'createCallback') }}}(managedEncObj, decoderID, 0);
    },

    PhotonVoice_WebCodecsVideoDecoder_Input: function(decoderID, ptr, offset, len, keyFrame) {
        const ctx = Module.PhotonVoice_WebCodecsVideoDecoder_Ctx.get(decoderID);
        if (ctx) {
            // The 1st keyframe breaks the decoder for an unknown reason, we skip it for now.
            // NOTE: A lost packet still can break the decoder at any time later (voice ring buffer may lose packets even if the transport is reliable).
            if (keyFrame && !ctx.startKeyFrameSkipped) {
                ctx.startKeyFrameSkipped = true;
                return;
            }

            if (ctx.decoder.decodeQueueSize > 2) {
                console.warn("[PV] PhotonVoice_WebCodecsVideoDecoder: dropping frame due to too many frames in decoder's queue");
                return;
            }

            const arr = new Int8Array(HEAPU8.buffer, ptr + offset, len);
            try {
                ctx.decoder.decode(new EncodedVideoChunk({
                    type: keyFrame ? "key" : "delta",
                    data: arr,
                    timestamp: 0,
                    //duration: 2000000,
                }));
            } catch (err) {
                console.warn('[PV] PhotonVoice_WebCodecsVideoDecoder_Input error:', err.message);
            }
        }
    },

    PhotonVoice_WebCodecsVideoDecoder_Stop: function(decoderID) {
        console.info('[PV] PhotonVoice_WebCodecsVideoDecoder_Stop');
        const ctx = Module.PhotonVoice_WebCodecsVideoDecoder_Ctx && Module.PhotonVoice_WebCodecsVideoDecoder_Ctx.get(decoderID);
        if (ctx) {
            try {
                ctx.decoder.close();
            } catch (err) {
                console.warn('[PV] PhotonVoice_WebCodecsVideoDecoder_Stop error:', err.message);
            }
            Module.PhotonVoice_WebCodecsVideoDecoder_Ctx.delete(decoderID);
        }
    },
});