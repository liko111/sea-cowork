using System;
using AkiDevCat.AVL.Rendering.Data;
using UnityEngine;

namespace AkiDevCat.AVL.Rendering
{
    [System.Serializable]
    public class AVLFeatureSettings
    {
        public enum RenderingQuality { Full = 0, Half = 1, Quarter = 2, Eighth = 3 }
        
        [Header("Shaders")] 
        public ComputeShader ClusteringShader;
        public Shader RenderingShader;
        public Shader DebugShader;
        
        [Header("General Rendering")] 
        public RenderingQuality renderingQuality = RenderingQuality.Half;
        public float RenderingScale => renderingQuality switch
        {
            RenderingQuality.Full => 1.0f,
            RenderingQuality.Half => 0.5f,
            RenderingQuality.Quarter => 0.25f,
            RenderingQuality.Eighth => 0.125f,
            _ => 1.0f
        };
        public bool enableHDR = true;
        public int maxLights = 4096;
        public int maxMasks = 128;

        [Header("Upscaling Pass")] 
        public bool enableUpscaling = true;

        [Header("Culling Pass")] 
        public bool enableDepthCulling = true;
        [Range(1, 256)] public int cullingClusterSizeX = 32;
        [Range(1, 256)] public int cullingClusterSizeY = 18;

        [Header("Advanced Settings")] 
        [Tooltip("Renders volumetric light to a separate texture introducing an additional copy operation. " +
                 "This can reduce performance but might be useful if the volumetric light texture is required" +
                 "by some other render feature.")]
        public bool exportVolumetricLightTexture = false;
        [Tooltip("This is the light model used by the AVL rendering engine. Additive is the standard one that was" +
                 "available from the package release. It simply adds the volumetric light color to the final" +
                 "camera rendering image. DensityOverLuminance uses volumetric light luminance to do" +
                 "a little bit more physically-correct rendering by introducing Beer-Lambert Law with the" +
                 "luminance as the density value. DensityOverBuffer does the same except it actually stores" +
                 "the density data in an alpha channel of the volumetric light texture - it increases VRAM" +
                 "usage but might produce the most appealing visual results.")]
        public LightModel lightModel = LightModel.Additive;
        
        
        // [Header("Transparent Pass")] 
        // public bool enableTransparentPass = false;

        [Header("Debug Settings")]
        public DebugMode debugMode = DebugMode.None;
        [Range(0.0f, 1.0f)]
        public float debugOpacity = 1.0f / 25.0f;

        public bool IsValid => ClusteringShader != null && RenderingShader != null & DebugShader != null;
    }
}