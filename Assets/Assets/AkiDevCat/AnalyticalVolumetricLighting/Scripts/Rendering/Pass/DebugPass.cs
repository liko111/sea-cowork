using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    public class DebugPass : ScriptableRenderPass
    {
        public DebugPass(AVLFeatureSettings settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            
            _shader = settings.DebugShader;
             Debug.Assert(_shader, $"[AVL] Debug shader is missing in the feature settings. Please assign the shader.");
//             
//             if (_shader == null)
//             {
//                 return;
//             }
//             
             _material = CoreUtils.CreateEngineMaterial(_shader);
//             
//             _fogRTI = new RenderTargetIdentifier(ShaderConstants._AVLFogTexture);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            if (!cameraData.postProcessEnabled)
                return;
            
            using var builder = renderGraph.AddRasterRenderPass("AVL - Debug Pass", out PassData passData);
            var context = frameData.Get<AVLRenderingContext>();

            passData.settings = context.Settings;
            
            passData.material = _material;
            passData.cullingClusterSizeX = context.Settings.cullingClusterSizeX;
            passData.cullingClusterSizeY = context.Settings.cullingClusterSizeY;
            passData.globalLightBufferSize = context.GlobalLightBufferSize;
            passData.globalMaskBufferSize = context.GlobalMaskBufferSize;
            passData.debugMode = context.Settings.debugMode;
            passData.viewMatrix = cameraData.GetViewMatrix();
            passData.lightClusterBuffer = context.LightClusterBuffer;
            passData.globalLightBuffer = context.GlobalLightBuffer;
            passData.lightIndexBuffer = context.LightIndexBuffer;

            // passData.activeColorTexture = resourceData.activeColorTexture;
            // builder.UseTexture(passData.activeColorTexture, AccessFlags.ReadWrite);

            passData.lightClusterDepthTexture = context.LightClusterDepthTexture;
            builder.UseTexture(passData.lightClusterDepthTexture);
            // builder.UseTexture(passData.activeColorTexture);
            
            builder.UseBuffer(passData.lightClusterBuffer);
            builder.UseBuffer(passData.globalLightBuffer);
            builder.UseBuffer(passData.lightIndexBuffer);
            
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            
            builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) => ExecutePass(data, rgContext));
        }
        
        private static void ExecutePass(PassData passData, RasterGraphContext rgContext)
        {
            var cmd = rgContext.cmd;
            
            SetupMaterial(passData);
            
            cmd.DrawProcedural(Matrix4x4.identity, passData.material, 0, MeshTopology.Triangles, 3, 1);
        }
        
        private static void SetupMaterial(PassData passData)
         {
             var mat = passData.material;
             
             var clusterSize = new Vector4(passData.cullingClusterSizeX, passData.cullingClusterSizeY);
             clusterSize.z = 1.0f / clusterSize.x;
             clusterSize.w = 1.0f / clusterSize.y;
             
             mat.SetInt(ShaderConstants._DebugMode, (int)passData.debugMode);
             mat.SetInt(ShaderConstants._GlobalLightBufferSize, passData.globalLightBufferSize);
             mat.SetFloat(ShaderConstants._DebugModeOpacity, passData.settings.debugOpacity);
             mat.SetMatrix(ShaderConstants._CameraViewMatrix, passData.viewMatrix);
             mat.SetBuffer(ShaderConstants._LightClusterBuffer, passData.lightClusterBuffer);
             mat.SetBuffer(ShaderConstants._GlobalLightBuffer, passData.globalLightBuffer);
             mat.SetBuffer(ShaderConstants._LightIndexBuffer, passData.lightIndexBuffer);
             // mat.SetFloat(ShaderConstants._DebugModeOpacity, RenderingContext.Settings.debugOpacity);
             // mat.SetMatrix(ShaderConstants._CameraViewMatrix, cameraData.GetViewMatrix());
             // mat.SetBuffer(ShaderConstants._LightClusterBuffer, RenderingContext.LightClusterBuffer);
             // mat.SetBuffer(ShaderConstants._GlobalLightBuffer, RenderingContext.GlobalLightBuffer);
             // mat.SetBuffer(ShaderConstants._LightIndexBuffer, RenderingContext.LightIndexBuffer);
             mat.SetVector(ShaderConstants._ClusterSize, clusterSize);
             mat.SetTexture(ShaderConstants._LightClusterDepthTexture, passData.lightClusterDepthTexture);
         }
        
        private readonly Shader _shader;
        private readonly Material _material;

        private class PassData
        {
            public AVLFeatureSettings settings;
            public Material material;
            
            public TextureHandle lightClusterDepthTexture;

            public int cullingClusterSizeX;
            public int cullingClusterSizeY;
            public int globalLightBufferSize;
            public int globalMaskBufferSize;
            public Matrix4x4 viewMatrix;
            public BufferHandle lightClusterBuffer;
            public BufferHandle globalLightBuffer;
            public BufferHandle lightIndexBuffer;
            public DebugMode debugMode;
        }
        
        internal static class ShaderConstants
         {
             public static readonly int _AVLFogTexture          = Shader.PropertyToID("_AVLFogTexture");
             public static readonly int _LightClusterBuffer     = Shader.PropertyToID("_LightClusterBuffer");
             public static readonly int _ClusterSize            = Shader.PropertyToID("_ClusterSize");
             public static readonly int _CameraViewMatrix       = Shader.PropertyToID("_CameraViewMatrix");
             public static readonly int _GlobalLightBuffer      = Shader.PropertyToID("_GlobalLightBuffer");
             public static readonly int _LightIndexBuffer       = Shader.PropertyToID("_LightIndexBuffer");
             public static readonly int _DebugMode              = Shader.PropertyToID("_DebugMode");
             public static readonly int _GlobalLightBufferSize  = Shader.PropertyToID("_GlobalLightBufferSize");
             public static readonly int _DebugModeOpacity       = Shader.PropertyToID("_DebugModeOpacity");
             public static readonly int _LightClusterDepthTexture = Shader.PropertyToID("_LightClusterDepthTexture");
         }
    }
}