using UnityEngine;
using UnityEngine.Rendering;

namespace AkiDevCat.AVL.Rendering
{
    public static class AVLTools
    {
        private static readonly MaterialPropertyBlock _PropertyBlock = new ();
        
        public static void BlitTextureWithMainTex(CommandBuffer cmd, RTHandle source, Material material, int pass)
        {
            if (source != null)
                _PropertyBlock.SetTexture(ShaderProperties._MainTex, source);
            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, _PropertyBlock);
            _PropertyBlock.Clear();
        }

        private static class ShaderProperties
        {
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        }
    }
}