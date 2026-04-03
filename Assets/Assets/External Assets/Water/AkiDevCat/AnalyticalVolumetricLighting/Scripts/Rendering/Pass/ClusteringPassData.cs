using UnityEngine.Rendering.RenderGraphModule;

namespace AkiDevCat.AVL.Rendering
{
    public class ClusteringPassData
    {
        public float RenderingQuality = 1.0f;

        public TextureHandle LightClusterDepthTexture;
    }
}