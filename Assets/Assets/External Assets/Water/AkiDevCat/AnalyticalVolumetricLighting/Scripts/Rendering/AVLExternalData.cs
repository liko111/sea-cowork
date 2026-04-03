using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace AkiDevCat.AVL.Rendering
{
    public class AVLExternalData : ContextItem
    {
        public TextureHandle VolumetricLightTexture;
        
        public override void Reset()
        {
            VolumetricLightTexture = TextureHandle.nullHandle;
        }
    }
}