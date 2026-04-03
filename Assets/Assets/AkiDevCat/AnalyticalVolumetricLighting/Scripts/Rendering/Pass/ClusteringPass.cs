using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AkiDevCat.AVL.Components;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    public class ClusteringPass : ScriptableRenderPass, IDisposable
    {
        public ClusteringPass(AVLFeatureSettings settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
            _mainShader = settings.ClusteringShader;
            
            _GlobalLightBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                settings.maxLights,
                Marshal.SizeOf<LightData>());
            
            _LightIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                settings.cullingClusterSizeX * settings.cullingClusterSizeY * (int)AVLConstants.MAX_LIGHT_PER_CLUSTER,
                Marshal.SizeOf<uint>());
            
            _LightClusterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                settings.cullingClusterSizeX * settings.cullingClusterSizeY,
                Marshal.SizeOf<LightClusterData>());
            
            _GlobalMaskBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                settings.maxMasks,
                Marshal.SizeOf<MaskData>());
            
            _MaskIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                settings.cullingClusterSizeX * settings.cullingClusterSizeY * (int)AVLConstants.MAX_MASKS_PER_CLUSTER,
                Marshal.SizeOf<uint>());
            
            _MaskInverseIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                settings.cullingClusterSizeX * settings.cullingClusterSizeY * settings.maxMasks,
                Marshal.SizeOf<int>());

            _CulledLightsCountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint)); // ToDo remove?
        }

        public void Dispose()
        {
             _GlobalLightBuffer?.Release();
             _LightIndexBuffer?.Release();
             _MaskIndexBuffer?.Release();
             _MaskInverseIndexBuffer?.Release();
             _GlobalMaskBuffer?.Release();
             _CulledLightsCountBuffer?.Release();
             _LightClusterBuffer?.Release();

             _flDisposed = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            if (!cameraData.postProcessEnabled)
                return;
            
            using var builder = renderGraph.AddComputePass("AVL - Clustering Pass", out PassData passData);
            var context = frameData.Get<AVLRenderingContext>();
            var camera = cameraData.camera;
            
            passData.settings = context.Settings;
            
            passData.mainShader = _mainShader;
            passData.clusterFrustumKernel = _mainShader.FindKernel("ClusterFrustumMain");
            passData.depthAlignmentKernel = _mainShader.FindKernel("DepthAlignmentMain");
            passData.cullLightsKernel = _mainShader.FindKernel("CullLightsMain");
            passData.cullMasksKernel = _mainShader.FindKernel("CullMasksMain");
            passData.globalLightBuffer = renderGraph.ImportBuffer(_GlobalLightBuffer);
            passData.globalMaskBuffer = renderGraph.ImportBuffer(_GlobalMaskBuffer);
            passData.lightIndexBuffer = renderGraph.ImportBuffer(_LightIndexBuffer);
            passData.maskIndexBuffer = renderGraph.ImportBuffer(_MaskIndexBuffer);
            passData.maskInverseIndexBuffer = renderGraph.ImportBuffer(_MaskInverseIndexBuffer);
            passData.lightClusterBuffer = renderGraph.ImportBuffer(_LightClusterBuffer);
            passData.culledLightsCountBuffer = renderGraph.ImportBuffer(_CulledLightsCountBuffer);

            // passData.globalLightList = passData.globalLightList;
            // passData.globalMaskList = passData.globalMaskList;
            
            passData.clusterSizeX = context.Settings.cullingClusterSizeX;
            passData.clusterSizeY = context.Settings.cullingClusterSizeY;
            passData.scaledResolutionX = (int)(camera.scaledPixelWidth * context.Settings.RenderingScale);
            passData.scaledResolutionY = (int)(camera.scaledPixelHeight * context.Settings.RenderingScale);
            passData.nearClipPlane = camera.nearClipPlane;
            passData.farClipPlane = camera.farClipPlane;
            passData.inverseProjectionMatrix = camera.projectionMatrix.inverse;
            passData.viewMatrix = cameraData.GetViewMatrix();
            passData.cameraPosition = cameraData.worldSpaceCameraPos;
            {
                // Calculate frustum corners
                camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), 
                    camera.nearClipPlane, camera.stereoActiveEye, passData.cameraFrustumCorners);

                passData.cameraNearFrustumPlane = Vector3.Normalize(Vector3.Cross(
                    passData.cameraFrustumCorners[0] - passData.cameraFrustumCorners[1], 
                    passData.cameraFrustumCorners[2] - passData.cameraFrustumCorners[1]));
                passData.cameraNearFrustumPlane = -passData.cameraNearFrustumPlane;
                passData.cameraNearFrustumPlane.w = Vector3.Dot(passData.cameraFrustumCorners[0], passData.cameraNearFrustumPlane);
             
                camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), 
                    camera.farClipPlane, camera.stereoActiveEye, passData.cameraFrustumCorners);
             
                passData.cameraFarFrustumPlane = Vector3.Normalize(Vector3.Cross(
                    passData.cameraFrustumCorners[0] - passData.cameraFrustumCorners[1], 
                    passData.cameraFrustumCorners[2] - passData.cameraFrustumCorners[1]));
                passData.cameraFarFrustumPlane.w = Vector3.Dot(passData.cameraFrustumCorners[0], passData.cameraFarFrustumPlane);
            }
            
            context.GlobalLightBuffer = passData.globalLightBuffer;
            context.GlobalMaskBuffer = passData.globalMaskBuffer;
            context.LightIndexBuffer = passData.lightIndexBuffer;
            context.MaskIndexBuffer = passData.maskIndexBuffer;
            context.MaskInverseIndexBuffer = passData.maskInverseIndexBuffer;
            context.LightClusterBuffer = passData.lightClusterBuffer;
            
            // var bufferDSDescriptor = new RenderTextureDescriptor(passData.scaledResolutionX, passData.scaledResolutionY)
            var bufferDSDescriptor = new RenderTextureDescriptor(passData.clusterSizeX, passData.clusterSizeY)
            {
                graphicsFormat = GraphicsFormat.R32_UInt,
                msaaSamples = 1,
                useMipMap = false,
                enableRandomWrite = true
            };
            
            passData.lightClusterDepthTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, bufferDSDescriptor, "Light Cluster Depth", true);
            context.LightClusterDepthTexture = passData.lightClusterDepthTexture;

            passData.cameraDepthTexture = resourceData.cameraDepthTexture;
            
            builder.UseBuffer(passData.globalLightBuffer, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.globalMaskBuffer, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.lightIndexBuffer, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.maskIndexBuffer, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.maskInverseIndexBuffer, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.lightClusterBuffer, AccessFlags.ReadWrite);
            builder.UseBuffer(passData.culledLightsCountBuffer, AccessFlags.ReadWrite);
            
            builder.UseTexture(passData.lightClusterDepthTexture, AccessFlags.ReadWrite);
            builder.UseTexture(passData.cameraDepthTexture);
            
            /*
             * Setup global light list
             */
            SetupGlobalLightList(passData);
            // Profiler.BeginSample(LIGHT_LOOP_PROFILING_NAME);
             
            // Profiler.EndSample();
            
            // Set shader keywords
            passData.mainShader.SetKeyword(new LocalKeyword(passData.mainShader, DEPTH_CULLING_KEYWORD), passData.settings.enableDepthCulling);
            
            context.GlobalLightBufferSize = passData.globalLightBufferCount;
            context.GlobalMaskBufferSize = passData.globalMaskBufferCount;
            
            builder.SetRenderFunc((PassData data, ComputeGraphContext cgContext) => ExecutePass(data, cgContext));
        }

        private static void ExecutePass(PassData passData, ComputeGraphContext cgContext)
        {
            var cmd = cgContext.cmd;
            
            /*
             * Align cluster frustums' far Z plane
             */

            if (passData.settings.enableDepthCulling)
            {
                SetupDepthAlignmentKernel(cmd, passData);
                ExecuteDepthAlignmentKernel(cmd, passData);
            }
            
            /*
              * Reconstruct cluster frustums
              * This seems to be very inexpensive, so we'll run it every frame
              * Theoretically, this can be called once the camera updates its projection matrix
              */
             
             SetupClusterFrustumKernel(cmd, passData);
             ExecuteClusterFrustumKernel(cmd, passData);
             
             /*
              * Conduct the actual light culling
              */

             SetupCullLightsKernel(cmd, passData);
             ExecuteCullLightsKernel(cmd, passData);
             
             SetupCullMasksKernel(cmd, passData);
             ExecuteCullMasksKernel(cmd, passData);
        }
        
        private static void SetupDepthAlignmentKernel(ComputeCommandBuffer cmd, PassData passData)
        {
            var far = passData.farClipPlane;
            var near = passData.nearClipPlane;
            
            var projectionParams = new Vector4(1.0f, near, far, 1.0f / far);
            
            var zBufferParams = new Vector4(-1.0f + far / near, 1.0f, 0.0f, 1.0f / far);
            zBufferParams.z = zBufferParams.x / far;

            var clusterSize = new Vector4(passData.settings.cullingClusterSizeX, passData.settings.cullingClusterSizeY);
            clusterSize.z = 1.0f / clusterSize.x;
            clusterSize.w = 1.0f / clusterSize.y;
            
            cmd.SetComputeFloatParam(passData.mainShader, ShaderConstants._RenderingQuality, 
                passData.settings.RenderingScale);
            
            cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._RenderingResolution, 
                new Vector4(passData.scaledResolutionX, passData.scaledResolutionY, 0, 0));
            
            cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._ClusterSize, 
                clusterSize);
            
            cmd.SetComputeMatrixParam(passData.mainShader, ShaderConstants._InvProjectionMatrix, 
                passData.inverseProjectionMatrix);
            
            cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._ProjectionParams, 
                projectionParams);
            
            cmd.SetComputeTextureParam(passData.mainShader, passData.depthAlignmentKernel, 
                ShaderConstants._CameraDepthTexture, passData.cameraDepthTexture);
            
            cmd.SetComputeTextureParam(passData.mainShader, passData.depthAlignmentKernel, 
                ShaderConstants._LightClusterDepthTexture, passData.lightClusterDepthTexture);
        }
        
        private static void SetupClusterFrustumKernel(ComputeCommandBuffer cmd, PassData passData)
        {
            var far = passData.farClipPlane;
            var near = passData.nearClipPlane;
             
             var projectionParams = new Vector4(1.0f, near, far, 1.0f / far);
             
             var zBufferParams = new Vector4(-1.0f + far / near, 1.0f, 0.0f, 1.0f / far);
             zBufferParams.z = zBufferParams.x / far;

             var clusterSize = new Vector4(passData.settings.cullingClusterSizeX, passData.settings.cullingClusterSizeY);
             clusterSize.z = 1.0f / clusterSize.x;
             clusterSize.w = 1.0f / clusterSize.y;
            
             cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._ClusterSize, 
                 clusterSize);
            
             cmd.SetComputeMatrixParam(passData.mainShader, ShaderConstants._InvProjectionMatrix, 
                 passData.inverseProjectionMatrix);
            
             cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._ProjectionParams, 
                 projectionParams);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.clusterFrustumKernel, ShaderConstants._LightClusterBuffer, passData.lightClusterBuffer);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.clusterFrustumKernel, ShaderConstants._LightIndexBuffer, passData.lightIndexBuffer);
             
             cmd.SetComputeTextureParam(passData.mainShader, passData.clusterFrustumKernel, ShaderConstants._LightClusterDepthTexture, passData.lightClusterDepthTexture);
        }
        
        private static void SetupCullLightsKernel(ComputeCommandBuffer cmd, PassData passData)
         {
             cmd.SetComputeIntParam(passData.mainShader, ShaderConstants._GlobalLightBufferSize, passData.globalLightBufferCount);
             cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._NearFrustumPlane, passData.cameraNearFrustumPlane);
             cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._FarFrustumPlane, passData.cameraFarFrustumPlane);
             cmd.SetComputeMatrixParam(passData.mainShader, ShaderConstants._ViewMatrix, passData.viewMatrix);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullLightsKernel, 
                 ShaderConstants._GlobalLightBuffer, passData.globalLightBuffer);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullLightsKernel, 
                 ShaderConstants._LightClusterBuffer, passData.lightClusterBuffer);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullLightsKernel, 
                 ShaderConstants._LightIndexBuffer, passData.lightIndexBuffer);
         }

         private static void SetupCullMasksKernel(ComputeCommandBuffer cmd, PassData passData)
         {
             cmd.SetComputeIntParam(passData.mainShader, ShaderConstants._GlobalMaskBufferSize, passData.globalMaskBufferCount);
             cmd.SetComputeIntParam(passData.mainShader, ShaderConstants._GlobalMaskBufferMaxSize, passData.settings.maxMasks);
             cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._NearFrustumPlane, passData.cameraNearFrustumPlane);
             cmd.SetComputeVectorParam(passData.mainShader, ShaderConstants._FarFrustumPlane, passData.cameraFarFrustumPlane);
             cmd.SetComputeMatrixParam(passData.mainShader, ShaderConstants._ViewMatrix, passData.viewMatrix);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullMasksKernel, 
                 ShaderConstants._GlobalMaskBuffer, passData.globalMaskBuffer);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullMasksKernel, 
                 ShaderConstants._LightClusterBuffer, passData.lightClusterBuffer);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullMasksKernel, 
                 ShaderConstants._MaskIndexBuffer, passData.maskIndexBuffer);
             
             cmd.SetComputeBufferParam(passData.mainShader, passData.cullMasksKernel, 
                 ShaderConstants._MaskInverseIndexBuffer, passData.maskInverseIndexBuffer);
         }
        
        private static void ExecuteDepthAlignmentKernel(ComputeCommandBuffer cmd, PassData passData)
        {
            cmd.DispatchCompute(passData.mainShader, passData.depthAlignmentKernel, 
                Mathf.CeilToInt(passData.settings.cullingClusterSizeX / 4.0f), 
                Mathf.CeilToInt(passData.settings.cullingClusterSizeY / 4.0f), 
                1);
        }
        
        private static void ExecuteClusterFrustumKernel(ComputeCommandBuffer cmd, PassData passData)
        {
             cmd.DispatchCompute(passData.mainShader, passData.clusterFrustumKernel, 
                 Mathf.CeilToInt(passData.settings.cullingClusterSizeX / 4.0f), 
                 Mathf.CeilToInt(passData.settings.cullingClusterSizeY / 4.0f), 
                 1);
        }
        
        private static void ExecuteCullLightsKernel(ComputeCommandBuffer cmd, PassData passData)
         {
             cmd.DispatchCompute(passData.mainShader, passData.cullLightsKernel, 
                 passData.settings.cullingClusterSizeX, 
                 passData.settings.cullingClusterSizeY, 
                 1);
         }
         
         private static void ExecuteCullMasksKernel(ComputeCommandBuffer cmd, PassData passData)
         {
             cmd.DispatchCompute(passData.mainShader, passData.cullMasksKernel, 
                 passData.settings.cullingClusterSizeX, 
                 passData.settings.cullingClusterSizeY, 
                 1);
         }
        
         private void SetupGlobalLightList(PassData passData)
         {
             passData.globalLightList.Clear();
             passData.globalMaskList.Clear();

             // InstanceID, Reference, LocalID
             Dictionary<int, (VolumetricLightMask, int)> masksRegistry = new();

             var maskIdMax = -1;

             var camPos = passData.cameraPosition;

             foreach (var (instanceID, light) in GlobalLightManager.AsEnumerable())
             {
                 if (!light.LightEnabled)
                     continue;
                 
                 if (passData.globalLightList.Count >= passData.settings.maxLights)
                     continue;
                 
                 // Update light cache for this frame if required
                 light.UpdateLightCache();

                 var cullingFading = 0.0f;
                 var maskId = -1;

                 if (light.distanceCullingEnabled)
                 {
                     var camDistanceSqr = (light.TransformCached.position - camPos).sqrMagnitude;
                     var lightHardCullingSqr = light.hardCullingDistance * light.hardCullingDistance;

                     if (camDistanceSqr > lightHardCullingSqr)
                     {
                         continue;
                     }

                     var lightSoftCullingSqr = light.softCullingDistance * light.softCullingDistance;
                     cullingFading = Mathf.Clamp01((camDistanceSqr - lightSoftCullingSqr) /
                                                   (lightHardCullingSqr - lightSoftCullingSqr));
                 }

                 if (light.lightMask != null && light.lightMask.isActiveAndEnabled)
                 {
                     var maskInstanceID = light.lightMask.GetInstanceID();
                     if (masksRegistry.TryGetValue(maskInstanceID, out var foundMask))
                     {
                         maskId = foundMask.Item2;
                     }
                     else
                     {
                         var md = new MaskData
                         {
                             Type = (uint) light.lightMask.shape,
                             WorldToMask = light.lightMask.worldToMaskMatrix,
                             BoundingRadius = light.lightMask.BoundingRadius,
                             Origin = light.lightMask.transform.position
                         };
                         passData.globalMaskList.Add(md);
                         masksRegistry.Add(maskInstanceID, (light.lightMask, ++maskIdMax));
                         maskId = maskIdMax;
                     }
                 }

                 var ld = new LightData
                 {
                     Type = (uint) light.lightShape,
                     Origin = light.TransformCached.position,
                     Right = light.TransformRightCached,
                     Up = light.TransformUpCached,
                     Forward = light.TransformForwardCached,
                     BoundingOrigin = light.BoundingOriginCached,
                     BoundingRadius = light.BoundingRadiusCached,
                     Color = light.LightVisibleColorCached,
                     SecondaryAngle = light.SecondaryAngleRadCached,
                     PrimaryAngle = light.PrimaryAngleRadCached,
                     Scattering = light.LightScatteringSqrCached,
                     CullingFading = cullingFading,
                     MaskID = maskId,
                     Range = light.lightRange,
                     Rect = light.lightRect
                 };

                 passData.globalLightList.Add(ld);
             }
             
             _GlobalLightBuffer.SetData(passData.globalLightList);
             passData.globalLightBufferCount = passData.globalLightList.Count;
             _GlobalMaskBuffer.SetData(passData.globalMaskList);
             passData.globalMaskBufferCount = passData.globalMaskList.Count;
         }

         private bool _flDisposed = false;

        private readonly ComputeShader _mainShader;
        
        private readonly GraphicsBuffer _GlobalLightBuffer;
        private readonly GraphicsBuffer _GlobalMaskBuffer;
        private readonly GraphicsBuffer _LightIndexBuffer;
        private readonly GraphicsBuffer _MaskIndexBuffer;
        private readonly GraphicsBuffer _LightClusterBuffer;
        private readonly GraphicsBuffer _CulledLightsCountBuffer;
        private readonly GraphicsBuffer _MaskInverseIndexBuffer;

        private class PassData
        {
            public AVLFeatureSettings settings;
            
            public ComputeShader mainShader;
            public int clusterFrustumKernel;
            public int depthAlignmentKernel;
            public int cullLightsKernel;
            public int cullMasksKernel;

            public TextureHandle cameraDepthTexture;
            public TextureHandle lightClusterDepthTexture;
            public BufferHandle globalLightBuffer;
            public BufferHandle globalMaskBuffer;
            public BufferHandle lightIndexBuffer;
            public BufferHandle maskIndexBuffer;
            public BufferHandle lightClusterBuffer;
            public BufferHandle culledLightsCountBuffer;
            public BufferHandle maskInverseIndexBuffer;
            public int globalLightBufferCount;
            public int globalMaskBufferCount;

            public List<LightData> globalLightList = new();
            public List<MaskData> globalMaskList = new();

            public int scaledResolutionX;
            public int scaledResolutionY;
            public int clusterSizeX;
            public int clusterSizeY;
            public float nearClipPlane;
            public float farClipPlane;
            public Matrix4x4 inverseProjectionMatrix;
            public Matrix4x4 viewMatrix;
            public Vector3 cameraPosition;
            public Vector3[] cameraFrustumCorners = new Vector3[4];
            public Vector4 cameraNearFrustumPlane;
            public Vector4 cameraFarFrustumPlane;
        }
        
        private const string DEPTH_CULLING_KEYWORD = "DEPTH_CULLING_ON";
        
        internal static class ShaderConstants
        {
            public static readonly int _ProjectionParams = Shader.PropertyToID("_ProjectionParams");
            public static readonly int _ClusterSize = Shader.PropertyToID("_ClusterSize");
            public static readonly int _InvProjectionMatrix = Shader.PropertyToID("_InvProjectionMatrix");
            public static readonly int _GlobalLightBuffer = Shader.PropertyToID("_GlobalLightBuffer");
            public static readonly int _GlobalLightBufferSize = Shader.PropertyToID("_GlobalLightBufferSize");
            public static readonly int _GlobalMaskBuffer = Shader.PropertyToID("_GlobalMaskBuffer");
            public static readonly int _GlobalMaskBufferSize = Shader.PropertyToID("_GlobalMaskBufferSize");
            public static readonly int _LightClusterBuffer = Shader.PropertyToID("_LightClusterBuffer");
            public static readonly int _CulledLightsCountBuffer = Shader.PropertyToID("_CulledLightsCountBuffer");
            public static readonly int _LightIndexCount = Shader.PropertyToID("_LightIndexCount");
            public static readonly int _LightIndexBuffer = Shader.PropertyToID("_LightIndexBuffer");
            public static readonly int _MaskIndexBuffer = Shader.PropertyToID("_MaskIndexBuffer");
            public static readonly int _NearFrustumPlane = Shader.PropertyToID("_NearFrustumPlane");
            public static readonly int _FarFrustumPlane = Shader.PropertyToID("_FarFrustumPlane");
            public static readonly int _ViewMatrix = Shader.PropertyToID("_ViewMatrix");
            public static readonly int _RenderingResolution = Shader.PropertyToID("_RenderingResolution");
            public static readonly int _LightClusterDepthTexture = Shader.PropertyToID("_LightClusterDepthTexture");
            public static readonly int _CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
            public static readonly int _RenderingQuality = Shader.PropertyToID("_RenderingQuality");
            public static readonly int _MaskInverseIndexBuffer = Shader.PropertyToID("_MaskInverseIndexBuffer");
            public static readonly int _GlobalMaskBufferMaxSize = Shader.PropertyToID("_GlobalMaskBufferMaxSize");
        }
    }
}