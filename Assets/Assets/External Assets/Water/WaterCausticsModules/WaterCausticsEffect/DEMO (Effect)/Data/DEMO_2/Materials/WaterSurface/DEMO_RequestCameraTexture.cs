// WaterCausticsModules
// Copyright (c) 2021 Masataka Hakozaki

#if WCE_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace MH.WaterCausticsModules {
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu ("")]
    public class DEMO_RequestCameraTexture : MonoBehaviour {
        public bool m_OpaqueTexture = true;
        public bool m_DepthTexture = true;

        private void OnEnable () {
            WaterCausticsEffectFeature.onEnqueue -= enqueuePass;
            WaterCausticsEffectFeature.onEnqueue += enqueuePass;
        }

        private void OnDisable () {
            WaterCausticsEffectFeature.onEnqueue -= enqueuePass;
        }

        private RequestTexPass _pass;
        private void enqueuePass (ScriptableRenderer renderer, Camera cam) {
            if (cam.cameraType == CameraType.Preview) return;
            if (_pass == null) _pass = new RequestTexPass ();
            _pass.Setup (m_OpaqueTexture, m_DepthTexture);
            renderer.EnqueuePass (_pass);
        }

        internal class RequestTexPass : ScriptableRenderPass {
            internal RequestTexPass () {
                base.profilingSampler = new ProfilingSampler ("WCE_DEMO_RequestTexPass");
                this.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            internal void Setup (bool opaque, bool depth) {
                ScriptableRenderPassInput request = 0;
                if (opaque) request |= ScriptableRenderPassInput.Color;
                if (depth) request |= ScriptableRenderPassInput.Depth;
                ConfigureInput (request);
            }

#if UNITY_6000_0_OR_NEWER
            public override void RecordRenderGraph (RenderGraph renderGraph, ContextContainer frameData) { }
#endif
#if UNITY_6000_0_OR_NEWER && !UNITY_6000_4_OR_NEWER
            [System.Obsolete]
#endif
#if !UNITY_6000_4_OR_NEWER
            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) { }
#endif

        }

    }
}
#endif // end of WCE_URP
