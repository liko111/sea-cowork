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
    public class DEMO_DrawEffectPass : MonoBehaviour {
        public string m_ShaderTag = "WCE_DEMO_UnderWaterEffect";
        public RenderPassEvent m_RenderEventAboveWater = RenderPassEvent.AfterRenderingSkybox;
        public RenderPassEvent m_RenderEventUnderWater = RenderPassEvent.AfterRenderingTransparents;

        private void OnEnable () {
            WaterCausticsEffectFeature.onEnqueue -= enqueuePass;
            WaterCausticsEffectFeature.onEnqueue += enqueuePass;
        }

        private void OnDisable () {
            WaterCausticsEffectFeature.onEnqueue -= enqueuePass;
        }

        private WCE_DEMO_DrawEffectPass _pass;
        private void enqueuePass (ScriptableRenderer renderer, Camera cam) {
            _pass ??= new WCE_DEMO_DrawEffectPass ();
            var camY = cam.transform.position.y;
            float surfY = Shader.GetGlobalFloat ("_WCECF_SurfaceY");
            var evt = surfY < camY ? m_RenderEventAboveWater : m_RenderEventUnderWater;
            _pass.Setup (evt, m_ShaderTag);
            renderer.EnqueuePass (_pass);
        }

        // ----------------------------------------------------------- ScriptableRenderPass
        internal class WCE_DEMO_DrawEffectPass : ScriptableRenderPass {
            private string _shaderTag;
            private ShaderTagId _shaderTagId;

            internal WCE_DEMO_DrawEffectPass () {
                base.profilingSampler = new ProfilingSampler (nameof (WCE_DEMO_DrawEffectPass));
            }

            internal void Setup (RenderPassEvent evt, string shaderTag) {
                this.renderPassEvent = evt;
                if (_shaderTag != shaderTag) {
                    _shaderTag = shaderTag;
                    _shaderTagId = new ShaderTagId (shaderTag);
                }
            }

#if UNITY_6000_0_OR_NEWER
            private class PassData {
                internal RendererListHandle rendererListHdl;
            }
            private void registerReadTex (IRasterRenderGraphBuilder builder, TextureHandle tex) {
                if (tex.IsValid ()) builder.UseTexture (tex, AccessFlags.Read);
            }
            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) {
                var camDt = frameData.Get<UniversalCameraData> ();
                var rendDt = frameData.Get<UniversalRenderingData> ();
                // var lightDt = frameData.Get<UniversalLightData> ();
                var resourceDt = frameData.Get<UniversalResourceData> ();
                // --
                using (var builder = renderGraph.AddRasterRenderPass<PassData> (passName, out var passData, profilingSampler)) {
                    builder.SetRenderAttachment (resourceDt.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth (resourceDt.activeDepthTexture, AccessFlags.Write);
                    // --
                    registerReadTex (builder, resourceDt.mainShadowsTexture);
                    registerReadTex (builder, resourceDt.additionalShadowsTexture);
                    // --
                    // TextureHandle [] dBufferHandles = resourceDt.dBuffer;
                    // for (int i = 0; i < dBufferHandles.Length; ++i)
                    //     registerReadTex (builder, dBufferHandles [i]);
                    // registerReadTex (builder, resourceDt.ssaoTexture);
                    // --
                    var desc = new RendererListDesc (_shaderTagId, rendDt.cullResults, camDt.camera) {
                        sortingCriteria = SortingCriteria.None,
                        renderQueueRange = RenderQueueRange.all,
                        layerMask = ~0,
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
                    };
                    var rendererList = context.CreateRendererList (desc);
                    cmd.DrawRendererList (rendererList);
#else
                    var drawingSettings = CreateDrawingSettings (_shaderTagId, ref renderingData, SortingCriteria.None);
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
