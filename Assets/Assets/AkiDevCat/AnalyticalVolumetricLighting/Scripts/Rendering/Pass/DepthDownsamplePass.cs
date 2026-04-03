using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    public class DepthDownsamplePass : ScriptableRenderPass, IDisposable
    {
        public DepthDownsamplePass(AVLFeatureSettings settings)
        {
            
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            
        }

        private static void ExecutePass()
        {
            
        }

        public void Dispose()
        {
            
        }

        private class PassData
        {
            public AVLFeatureSettings settings;
        }
    }
}