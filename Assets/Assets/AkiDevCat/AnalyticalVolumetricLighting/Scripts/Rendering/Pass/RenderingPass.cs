using System;
using AkiDevCat.AVL.Rendering.Data;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    public class RenderingPass : ScriptableRenderPass, IDisposable
    {
        private static LocalKeyword _lightModelKeyword_Additive;
        private static LocalKeyword _lightModelKeyword_DensityOverLuminance;
        private static LocalKeyword _lightModelKeyword_DensityOverBuffer;
        private static LocalKeyword _exportKeyword_Enabled;
        
        public RenderingPass(AVLFeatureSettings settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox; // ToDo
            _passMaterial = CoreUtils.CreateEngineMaterial(settings.RenderingShader);
            
            CreateShaderKeywords(settings);
        }

        private static void CreateShaderKeywords(AVLFeatureSettings settings)
        {
            _lightModelKeyword_Additive =                new LocalKeyword(settings.RenderingShader, "LIGHT_MODEL_ADDITIVE");
            _lightModelKeyword_DensityOverLuminance =    new LocalKeyword(settings.RenderingShader, "LIGHT_MODEL_DENSITY_OVER_LUMINANCE");
            _lightModelKeyword_DensityOverBuffer =       new LocalKeyword(settings.RenderingShader, "LIGHT_MODEL_DENSITY_OVER_BUFFER");
            _exportKeyword_Enabled =                     new LocalKeyword(settings.RenderingShader, "EXPORT_VOLUMETRIC_LIGHT_TEXTURE");
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_passMaterial);
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            if (!cameraData.postProcessEnabled)
                return;
            
            var stack = VolumeManager.instance.stack;
            var component = stack.GetComponent<AVLVolumeComponent>();
            if (component == null || !component.IsEnabled)
            {
                return;
            }
            
            using var builder = renderGraph.AddUnsafePass("AVL - Render Pass", out PassData passData);
            var context = frameData.Get<AVLRenderingContext>();
            var externalData = frameData.GetOrCreate<AVLExternalData>();
            var camera = cameraData.camera;

            passData.settings = context.Settings;
            passData.scaledResolutionX = (int)(camera.scaledPixelWidth * context.Settings.RenderingScale);
            passData.scaledResolutionY = (int)(camera.scaledPixelHeight * context.Settings.RenderingScale);
            passData.airDensity = component.airDensity.value;
            passData.fogDensityGlobal = component.fogDensityGlobal.value;
            passData.fogDensityPerLightModifier = component.fogDensityPerLightModifier.value;
            passData.fogScattering = component.fogScattering.value;
            passData.fogColor = component.fogColor.value;
            passData.cameraPositionWS = cameraData.worldSpaceCameraPos;
            passData.cameraFrustumCorners = FrustumCorners(camera);
            passData.cameraToWorldMatrix = camera.cameraToWorldMatrix;
            passData.globalLightBuffer = context.GlobalLightBuffer;
            passData.globalLightBufferCount = context.GlobalLightBufferSize;
            passData.globalMaskBuffer = context.GlobalMaskBuffer;
            passData.globalMaskBufferMaxSize = context.Settings.maxMasks;
            passData.lightClusterBuffer = context.LightClusterBuffer;
            passData.lightIndexBuffer = context.LightIndexBuffer;
            passData.maskIndexBuffer = context.MaskIndexBuffer;
            passData.maskInverseIndexBuffer = context.MaskInverseIndexBuffer;

            passData.material = _passMaterial;

            var cameraGraphicsFormat = cameraData.cameraTargetDescriptor.graphicsFormat;
            var textureGraphicsFormat = context.Settings.lightModel switch
            {
                LightModel.Additive or LightModel.DensityOverLuminance => context.Settings.enableHDR
                    ? cameraGraphicsFormat
                    : GraphicsFormat.B8G8R8A8_SRGB // ToDo change 32-bit srgb to 24-bit if possible
                ,
                LightModel.DensityOverBuffer => context.Settings.enableHDR
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.B8G8R8A8_SRGB,
                _ => throw new ArgumentOutOfRangeException()
            };

            var bufferDSDescriptor = new RenderTextureDescriptor(passData.scaledResolutionX, passData.scaledResolutionY)
             {
                 graphicsFormat = textureGraphicsFormat,
                 msaaSamples = 1,
                 useMipMap = false
             };
             
             var bufferFSDescriptor = new RenderTextureDescriptor(cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight)
             {
                 graphicsFormat = textureGraphicsFormat,
                 msaaSamples = 1,
                 useMipMap = false
             };

             passData.cameraColorTexture = resourceData.activeColorTexture;
             
             passData.volumetricLightTexture = UniversalRenderer.CreateRenderGraphTexture(
                 renderGraph, bufferDSDescriptor, "Volumetric Light", true);
             builder.UseTexture(passData.volumetricLightTexture, AccessFlags.ReadWrite);

             if (passData.settings.exportVolumetricLightTexture)
             {
                 passData.volumetricLightUpscaledTexture = UniversalRenderer.CreateRenderGraphTexture(
                     renderGraph, bufferFSDescriptor, "Volumetric Light (Upscaled)", true);
                 builder.UseTexture(passData.volumetricLightUpscaledTexture, AccessFlags.WriteAll);

                 externalData.VolumetricLightTexture = passData.volumetricLightUpscaledTexture;
             }

             builder.UseBuffer(passData.globalLightBuffer);
             
             builder.UseTexture(passData.cameraColorTexture, AccessFlags.ReadWrite);
            
            builder.SetRenderFunc((PassData data, UnsafeGraphContext gContext) => ExecutePass(data, gContext));
        }

        private static void ExecutePass(PassData passData, UnsafeGraphContext gContext)
        {
            // ToDo: It would be better to replace this with the standard unsafe cmd but the current Blitter API implementation does not allow it
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(gContext.cmd);
            
            SetupMaterial(passData);

            { // Render Global Fog
                cmd.SetRenderTarget(passData.cameraColorTexture);
                
                AVLTools.BlitTextureWithMainTex(cmd, null, passData.material, SP_RENDER_GLOBAL_FOG_PASS);
            }

            { // Render Volumetric Light
                cmd.SetRenderTarget(passData.volumetricLightTexture);
                
                AVLTools.BlitTextureWithMainTex(cmd, null, passData.material, SP_RENDER_VOLUMETRIC_LIGHT_PASS);
            }

            { // Upscale Volumetric Light
                if (passData.settings.exportVolumetricLightTexture)
                {
                    cmd.SetRenderTarget(passData.volumetricLightUpscaledTexture);
                }
                else
                {
                    cmd.SetRenderTarget(passData.cameraColorTexture);
                }

                if (passData.settings.enableUpscaling)
                {
                    AVLTools.BlitTextureWithMainTex(cmd, passData.volumetricLightTexture, passData.material, SP_UPSCALE_VOLUMETRIC_LIGHT_PASS);
                }
                else
                {
                    AVLTools.BlitTextureWithMainTex(cmd, passData.volumetricLightTexture, passData.material, SP_BLIT_PASS);
                }
                
                if (passData.settings.exportVolumetricLightTexture)
                {
                    cmd.SetRenderTarget(passData.cameraColorTexture);
                    
                    AVLTools.BlitTextureWithMainTex(cmd, passData.volumetricLightUpscaledTexture, passData.material, SP_BLIT_PASS);
                }
            }
        }
        
        private static void SetupMaterial(PassData passData)
         {
             var clusterSize = new Vector4(passData.settings.cullingClusterSizeX, passData.settings.cullingClusterSizeY);
             clusterSize.z = 1.0f / clusterSize.x;
             clusterSize.w = 1.0f / clusterSize.y;
             
             passData.material.SetFloat(ShaderConstants._AirDensity, passData.airDensity);
             passData.material.SetFloat(ShaderConstants._FogDensityGlobal, passData.fogDensityGlobal);
             passData.material.SetFloat(ShaderConstants._FogDensityPerLight, passData.fogDensityPerLightModifier);
             passData.material.SetFloat(ShaderConstants._FogScattering, passData.fogScattering);
             passData.material.SetColor(ShaderConstants._FogColor, passData.fogColor);
             passData.material.SetVector(ShaderConstants._RenderingResolution, new Vector4(passData.scaledResolutionX, passData.scaledResolutionY, 0, 0));
             passData.material.SetVector(ShaderConstants._ClusterSize, clusterSize);
             passData.material.SetVector(ShaderConstants._CameraWorldSpacePosition, passData.cameraPositionWS);
             passData.material.SetMatrix(ShaderConstants._CameraNearFrustumMatrix, passData.cameraFrustumCorners);
             passData.material.SetMatrix(ShaderConstants._CameraToWorldMatrix, passData.cameraToWorldMatrix);
             passData.material.SetBuffer(ShaderConstants._GlobalLightBuffer, passData.globalLightBuffer);
             passData.material.SetInt(ShaderConstants._GlobalLightBufferCount, passData.globalLightBufferCount);
             passData.material.SetBuffer(ShaderConstants._GlobalMaskBuffer, passData.globalMaskBuffer);
             passData.material.SetBuffer(ShaderConstants._LightClusterBuffer, passData.lightClusterBuffer);
             passData.material.SetBuffer(ShaderConstants._LightIndexBuffer, passData.lightIndexBuffer);
             passData.material.SetBuffer(ShaderConstants._MaskIndexBuffer, passData.maskIndexBuffer);
             passData.material.SetBuffer(ShaderConstants._MaskInverseIndexBuffer, passData.maskInverseIndexBuffer);
             passData.material.SetInt(ShaderConstants._GlobalMaskBufferMaxSize, passData.globalMaskBufferMaxSize);
             
             #if UNITY_EDITOR
             // This is required because shaders can be recompiled during the editor-time
             CreateShaderKeywords(passData.settings);
             #endif

             passData.material.SetKeyword(_lightModelKeyword_Additive, false);
             passData.material.SetKeyword(_lightModelKeyword_DensityOverBuffer, false);
             passData.material.SetKeyword(_lightModelKeyword_DensityOverLuminance, false);
             passData.material.SetKeyword(_exportKeyword_Enabled, passData.settings.exportVolumetricLightTexture);

             switch (passData.settings.lightModel)
             {
                 case LightModel.Additive:
                     passData.material.SetKeyword(_lightModelKeyword_Additive, true);
                     passData.material.SetInt(ShaderConstants._BlitBlendSrc, (int)BlendMode.One);
                     passData.material.SetInt(ShaderConstants._BlitBlendDst, (int)BlendMode.One);
                     passData.material.SetInt(ShaderConstants._UpscaleBlendSrc, (int)BlendMode.One);
                     passData.material.SetInt(ShaderConstants._UpscaleBlendDst, (int)BlendMode.One);
                     passData.material.SetInt(ShaderConstants._GlobalFogBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._GlobalFogBlendDst, (int)BlendMode.OneMinusSrcAlpha);
                     break;
                 case LightModel.DensityOverLuminance:
                     passData.material.SetKeyword(_lightModelKeyword_DensityOverLuminance, true);
                     passData.material.SetInt(ShaderConstants._BlitBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._BlitBlendDst, (int)BlendMode.OneMinusSrcAlpha);
                     passData.material.SetInt(ShaderConstants._UpscaleBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._UpscaleBlendDst, (int)BlendMode.OneMinusSrcAlpha);
                     passData.material.SetInt(ShaderConstants._GlobalFogBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._GlobalFogBlendDst, (int)BlendMode.OneMinusSrcAlpha);

                     if (passData.settings.exportVolumetricLightTexture)
                     {
                         passData.material.SetInt(ShaderConstants._UpscaleBlendSrc, (int)BlendMode.One);
                         passData.material.SetInt(ShaderConstants._UpscaleBlendDst, (int)BlendMode.Zero);
                     }
                     break;
                 case LightModel.DensityOverBuffer:
                     passData.material.SetKeyword(_lightModelKeyword_DensityOverBuffer, true);
                     passData.material.SetInt(ShaderConstants._BlitBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._BlitBlendDst, (int)BlendMode.OneMinusSrcAlpha);
                     passData.material.SetInt(ShaderConstants._UpscaleBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._UpscaleBlendDst, (int)BlendMode.OneMinusSrcAlpha);
                     passData.material.SetInt(ShaderConstants._GlobalFogBlendSrc, (int)BlendMode.SrcAlpha);
                     passData.material.SetInt(ShaderConstants._GlobalFogBlendDst, (int)BlendMode.OneMinusSrcAlpha);
                     
                     if (passData.settings.exportVolumetricLightTexture)
                     {
                         passData.material.SetInt(ShaderConstants._UpscaleBlendSrc, (int)BlendMode.One);
                         passData.material.SetInt(ShaderConstants._UpscaleBlendDst, (int)BlendMode.Zero);
                     }
                     break;
                 default:
                     throw new ArgumentOutOfRangeException();
             }
         }
        
        private static Matrix4x4 FrustumCorners(Camera cam)
         {
             var t = cam.transform;

             var frustumCorners = new Vector3[4];
             cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1),
                 cam.nearClipPlane, cam.stereoActiveEye, frustumCorners);

             var frustumVectorsArray = Matrix4x4.identity;

             frustumVectorsArray.SetRow(0, t.TransformPoint(frustumCorners[0]));
             frustumVectorsArray.SetRow(1, t.TransformPoint(frustumCorners[3]));
             frustumVectorsArray.SetRow(2, t.TransformPoint(frustumCorners[1]));
             frustumVectorsArray.SetRow(3, t.TransformPoint(frustumCorners[2]));

             return frustumVectorsArray;
         }

        private readonly Material _passMaterial;
        
        private const int SP_RENDER_GLOBAL_FOG_PASS = 0;
        private const int SP_RENDER_VOLUMETRIC_LIGHT_PASS = 1;
        private const int SP_UPSCALE_VOLUMETRIC_LIGHT_PASS = 2;
        private const int SP_BLIT_PASS = 3;

        class PassData
        {
            public AVLFeatureSettings settings;
            public Material material;

            public int scaledResolutionX;
            public int scaledResolutionY;

            public float airDensity;
            public float fogDensityGlobal;
            public float fogDensityPerLightModifier;
            public float fogScattering;
            public Color fogColor;

            public Vector3 cameraPositionWS;
            public Matrix4x4 cameraFrustumCorners;
            public Matrix4x4 cameraToWorldMatrix;

            public int globalLightBufferCount;
            public int globalMaskBufferMaxSize;
            public TextureHandle cameraColorTexture;
            public TextureHandle volumetricLightTexture;
            public TextureHandle volumetricLightUpscaledTexture;
            public BufferHandle globalLightBuffer;
            public BufferHandle globalMaskBuffer;
            public BufferHandle lightClusterBuffer;
            public BufferHandle lightIndexBuffer;
            public BufferHandle maskIndexBuffer;
            public BufferHandle maskInverseIndexBuffer;
        }
        
        internal static class ShaderConstants
         {
             public static readonly int _AVLBufferTexture = Shader.PropertyToID("_AVLBufferTexture");
             public static readonly int _AVLFogTexture = Shader.PropertyToID("_AVLFogTexture");
             public static readonly int _RenderingResolution = Shader.PropertyToID("_RenderingResolution");
             public static readonly int _ClusterSize = Shader.PropertyToID("_ClusterSize");
             public static readonly int _CameraWorldSpacePosition = Shader.PropertyToID("_CameraWorldSpacePosition");
             public static readonly int _AirDensity = Shader.PropertyToID("_AirDensity");
             public static readonly int _FogDensityGlobal = Shader.PropertyToID("_FogDensityGlobal");
             public static readonly int _FogDensityPerLight = Shader.PropertyToID("_FogDensityPerLight");
             public static readonly int _FogScattering = Shader.PropertyToID("_FogScattering");
             public static readonly int _FogColor = Shader.PropertyToID("_FogColor");
             public static readonly int _CameraNearFrustumMatrix = Shader.PropertyToID("_CameraNearFrustumMatrix");
             public static readonly int _CameraToWorldMatrix = Shader.PropertyToID("_CameraToWorldMatrix");
             public static readonly int _GlobalLightBuffer = Shader.PropertyToID("_GlobalLightBuffer");
             public static readonly int _LightClusterBuffer = Shader.PropertyToID("_LightClusterBuffer");
             public static readonly int _LightIndexBuffer = Shader.PropertyToID("_LightIndexBuffer");
             public static readonly int _GlobalLightBufferCount = Shader.PropertyToID("_GlobalLightBufferCount");
             public static readonly int _GlobalMaskBuffer = Shader.PropertyToID("_GlobalMaskBuffer");
             public static readonly int _MaskIndexBuffer = Shader.PropertyToID("_MaskIndexBuffer");
             public static readonly int _MaskInverseIndexBuffer = Shader.PropertyToID("_MaskInverseIndexBuffer");
             public static readonly int _GlobalMaskBufferMaxSize = Shader.PropertyToID("_GlobalMaskBufferMaxSize");
             public static readonly int _BlitBlendSrc = Shader.PropertyToID("_BlitBlendSrc");
             public static readonly int _BlitBlendDst = Shader.PropertyToID("_BlitBlendDst");
             public static readonly int _UpscaleBlendSrc = Shader.PropertyToID("_UpscaleBlendSrc");
             public static readonly int _UpscaleBlendDst = Shader.PropertyToID("_UpscaleBlendDst");
             public static readonly int _GlobalFogBlendSrc = Shader.PropertyToID("_GlobalFogBlendSrc");
             public static readonly int _GlobalFogBlendDst = Shader.PropertyToID("_GlobalFogBlendDst");
         }
    }
}