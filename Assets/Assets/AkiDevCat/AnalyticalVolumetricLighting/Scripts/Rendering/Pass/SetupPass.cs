using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    public class SetupPass : ScriptableRenderPass
    {
        private readonly AVLFeatureSettings _settings;

        public SetupPass(AVLFeatureSettings settings)
        {
            _settings = settings;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();

            if (!cameraData.postProcessEnabled)
                return;
            
            using var builder = renderGraph.AddRasterRenderPass<PassData>("AVL - Setup Pass", out var passData);
            
            var resourceData = frameData.Get<UniversalResourceData>();
            var context = frameData.GetOrCreate<AVLRenderingContext>();
            
            context.Settings = _settings;
        }
        
        private class PassData { }
    }
}