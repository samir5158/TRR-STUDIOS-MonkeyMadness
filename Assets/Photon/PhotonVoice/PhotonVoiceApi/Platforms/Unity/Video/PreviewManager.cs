#if PHOTON_VOICE_VIDEO_ENABLE
using UnityEngine;
using System.Collections.Generic;

namespace Photon.Voice.Unity
{
    public class PreviewManagerUnityGUI : Photon.Voice.PreviewManager
    {
        public void OnGUI()
        {
            if (Event.current.type.Equals(EventType.Repaint))
            {
                foreach (var x in views)
                {
                    var v = x.Value;
                    //v.Texture holds ref to IVideoPreview instance (see AddView calls in LBClient)
                    var t = (Texture)v.PlatformView;

                    if (t != null)
                    {
                        var r = new Rect(v.x, v.y, v.w, v.h);
                        Graphics.DrawTexture(r, t);
                    }
                }
            }
        }

        // applies on each frame in OnGUI()
        override protected void Apply(ViewState v)
        { }
    }

    public class PreviewManagerScreenQuadTexture : IPreviewManager
    {
        protected class QuadView
        {
            GameObject quad;
            int x;
            int y;
            int w;
            int h;
            Flip flip = Flip.None;
            Rotation rot = Rotation.Rotate0;
            public QuadView(Shader shaderScreen, IVideoPreview view)
            {
                //                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad = new GameObject();
                MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
                MeshFilter meshFilter = quad.AddComponent<MeshFilter>();
                Mesh mesh = new Mesh();
                mesh.vertices = new Vector3[4] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) };
                mesh.triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
                // mesh.normals = new Vector3[4] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
                mesh.uv = new Vector2[4] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                meshFilter.mesh = mesh;
                quad.name = "PreviewManagerQuad" + view;

                if (shaderScreen != null)
                {
                    renderer.material = new Material(shaderScreen);
                    renderer.material.SetTexture("_MainTex", view.PlatformView as Texture);
                }
            }

            // do not confuse this with IPreviewManager.SetBounds
            public void SetBounds(int x, int y, int w, int h, Flip flip = default(Flip), Rotation rot = Rotation.Rotate0)
            {
                if (this.x != x || this.y != y || this.w != w || this.h != h || this.flip != flip || this.rot != rot)
                {
                    //Camera.current.pixelWidth
                    var renderer = quad.GetComponent<Renderer>();   // TODO cache renderer, unless SetBounds is guaranteed to never be called every Update()
                    float sw = (float)Screen.width;
                    float sh = (float)Screen.height;
                    renderer.material.SetVector("_Pos", new Vector4(x * 2 / sw - 1, 1 - y * 2 / sh, 0, 0));
                    renderer.material.SetVector("_Scale", new Vector4(w * 2 / sw, -h * 2 / sh, 1, 1));
                    renderer.material.SetVector("_Flip", new Vector4(flip.IsHorizontal ? -1 : 1, flip.IsVertical ? -1 : 1, 0, 0));
                    this.x = x;
                    this.y = y;
                    this.w = w;
                    this.h = h;
                    this.flip = flip;
                    this.rot = rot;
                }
            }
            public void Dispose()
            {
                GameObject.Destroy(quad);
            }
        }

        protected Dictionary<object, QuadView> views = new Dictionary<object, QuadView>();
        readonly Shader shaderScreen;

        public PreviewManagerScreenQuadTexture(ILogger logger)
        {
            if (VideoTexture.ShaderScreen.Name == null)
            {
                logger.Log(LogLevel.Error, "[PV] [PMSQT] VideoTexture.ShaderScreen is not supported on the current platform " + Application.platform + "/" + SystemInfo.graphicsDeviceType);
            }
            else
            {
                shaderScreen = Resources.Load<Shader>(VideoTexture.ShaderScreen.Name);
                if (shaderScreen == null)
                {
                    logger.Log(LogLevel.Error, "[PV] [PMSQT] PreviewManagerScreenQuadTexture: shader resource " + VideoTexture.ShaderScreen.Name + " fails to load");
                }
                else
                {
                    logger.Log(LogLevel.Info, "[PV] [PMSQT] PreviewManagerScreenQuadTexture initialized");
                }
            }
        }

        public void AddView(object id, IVideoPreview view)
        {
            views[id] = new QuadView(shaderScreen, view);
        }

        public void RemoveView(object id)
        {
            if (views.TryGetValue(id, out QuadView quad))
            {
                quad.Dispose();
            }
            views.Remove(id);
        }

        public bool Has(object id)
        {
            return views.ContainsKey(id);
        }

        /// <summary>
        /// Positions the preview of the view / image. Not optimized to be called every frame.
        /// </summary>
        public void SetBounds(object id, int x, int y, int w, int h, Flip flip = default(Flip), Rotation rot = Rotation.Rotate0)
        {
            if (views.TryGetValue(id, out QuadView quad))
            {
                quad.SetBounds(x, y, w, h, flip, rot);
            }
        }
    }
}
#endif
