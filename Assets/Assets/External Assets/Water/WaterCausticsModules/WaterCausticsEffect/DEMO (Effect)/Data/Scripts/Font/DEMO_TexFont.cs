// WaterCausticsModules
// Copyright (c) 2021 Masataka Hakozaki

#if WCE_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_2022_3_OR_NEWER
using UnityEngine.Rendering.RendererUtils;
#endif

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace MH.WaterCausticsModules {
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu ("")]
    public class DEMO_TexFont : MonoBehaviour {
        public RenderPassEvent m_RenderEventAboveWater = RenderPassEvent.AfterRenderingSkybox;
        public RenderPassEvent m_RenderEventUnderWater = RenderPassEvent.AfterRenderingTransparents;

        private void OnEnable () {
            WaterCausticsEffectFeature.onEnqueue -= enqueuePass;
            WaterCausticsEffectFeature.onEnqueue += enqueuePass;
        }

        private void OnDisable () {
            WaterCausticsEffectFeature.onEnqueue -= enqueuePass;
        }

        private WCE_Demo_TexFontPass _pass;
        private void enqueuePass (ScriptableRenderer renderer, Camera cam) {
            _pass ??= new WCE_Demo_TexFontPass ();
            var camY = cam.transform.position.y;
            float surfY = Shader.GetGlobalFloat ("_WCECF_SurfaceY");
            var evt = surfY < camY ? m_RenderEventAboveWater : m_RenderEventUnderWater;
            _pass.Setup (evt);
            renderer.EnqueuePass (_pass);
        }

        // ----------------------------------------------------------- ScriptableRenderPass
        internal class WCE_Demo_TexFontPass : ScriptableRenderPass {
            private ShaderTagId _shaderTagId;

            internal WCE_Demo_TexFontPass () {
                base.profilingSampler = new ProfilingSampler (nameof (WCE_Demo_TexFontPass));
                _shaderTagId = new ShaderTagId ("WCE_DEMO_TexFont");
            }

            internal void Setup (RenderPassEvent evt) {
                this.renderPassEvent = evt;
            }

#if UNITY_6000_0_OR_NEWER
            private class PassData {
                internal RendererListHandle rendererListHdl;
            }
            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {
                var rendDt = frameData.Get<UniversalRenderingData> ();
                var camDt = frameData.Get<UniversalCameraData> ();
                var resourceDt = frameData.Get<UniversalResourceData> ();
                // --
                using (var builder = renderGraph.AddRasterRenderPass<PassData> (passName, out var passData, profilingSampler)) {
                    builder.SetRenderAttachment (resourceDt.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth (resourceDt.activeDepthTexture, AccessFlags.Write);
                    // --
                    var desc = new RendererListDesc (_shaderTagId, rendDt.cullResults, camDt.camera) {
                        sortingCriteria = SortingCriteria.None,
                        renderQueueRange = RenderQueueRange.all,
                        layerMask = ~0,
                        rendererConfiguration = PerObjectData.None,
                    };
                    passData.rendererListHdl = renderGraph.CreateRendererList (desc);
                    builder.UseRendererList (passData.rendererListHdl);
                    // --
                    builder.AllowPassCulling (false);
                    builder.AllowGlobalStateModification (true);
                    // --
                    builder.SetRenderFunc ((PassData passData, RasterGraphContext context) => {
                        context.cmd.DrawRendererList (passData.rendererListHdl);
                    });
                }
            }

#endif // end of UNITY_6000_0_OR_NEWER

#if UNITY_6000_0_OR_NEWER && !UNITY_6000_4_OR_NEWER
            [System.Obsolete]
#endif
#if !UNITY_6000_4_OR_NEWER
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                CommandBuffer cmd = CommandBufferPool.Get ();
                using (new ProfilingScope (cmd, base.profilingSampler)) {
                    context.ExecuteCommandBuffer (cmd);
                    cmd.Clear ();
#if UNITY_2022_3_OR_NEWER
                    var desc = new RendererListDesc (_shaderTagId, renderingData.cullResults, renderingData.cameraData.camera) {
                        sortingCriteria = SortingCriteria.None,
                        renderQueueRange = RenderQueueRange.all,
                        layerMask = ~0,
                        rendererConfiguration = PerObjectData.None,
                    };
                    var rendererList = context.CreateRendererList (desc);
                    cmd.DrawRendererList (rendererList);
#else
                    var drawingSettings = CreateDrawingSettings (_shaderTagId, ref renderingData, SortingCriteria.None);
                    drawingSettings.perObjectData = PerObjectData.None; // ←ライト情報など使用しないので省略
                    var filteringSettings = new FilteringSettings (RenderQueueRange.all, layerMask: ~0);
                    context.DrawRenderers (renderingData.cullResults, ref drawingSettings, ref filteringSettings);
#endif
                }
                context.ExecuteCommandBuffer (cmd);
                CommandBufferPool.Release (cmd);
            }
#endif // end of !UNITY_6000_4_OR_NEWER

        }

    }
}
#endif // end of WCE_URP
