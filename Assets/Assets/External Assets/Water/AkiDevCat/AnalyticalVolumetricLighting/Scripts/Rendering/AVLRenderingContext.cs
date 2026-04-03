using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    internal class AVLRenderingContext : ContextItem
    {
        public AVLFeatureSettings Settings { get; internal set; }
        
        public int GlobalLightBufferSize { get; set; }
        
        public int GlobalMaskBufferSize { get; set; }
        
        public BufferHandle GlobalLightBuffer { get; set; }

        public BufferHandle GlobalMaskBuffer { get; set; }
        
        public BufferHandle MaskIndexBuffer { get; set; }
        
        public BufferHandle MaskInverseIndexBuffer { get; set; }

        public BufferHandle LightIndexBuffer { get; set; }
        
        public BufferHandle LightClusterBuffer { get; set; }
        
        public List<LightData> GlobalLightList { get; set; }
        
        public List<MaskData> GlobalMaskList { get; set; }
        
        public TextureHandle LightClusterDepthTexture { get; set; }
        
        public TextureHandle DownscaledDepthTexture { get; set; }

        public AVLRenderingContext()
        {
            GlobalLightList = new();
            GlobalMaskList = new();
        }
        
        public override void Reset()
        {
            Settings = null;
            GlobalLightBufferSize = 0;
            GlobalMaskBufferSize = 0;
            GlobalLightBuffer = BufferHandle.nullHandle;
            GlobalMaskBuffer = BufferHandle.nullHandle;
            MaskIndexBuffer = BufferHandle.nullHandle;
            MaskInverseIndexBuffer = BufferHandle.nullHandle;
            LightIndexBuffer = BufferHandle.nullHandle;
            LightClusterBuffer = BufferHandle.nullHandle;
            LightClusterDepthTexture = TextureHandle.nullHandle;
            GlobalLightList?.Clear();
            GlobalMaskList?.Clear();
        }
    }
}