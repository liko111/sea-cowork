using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AkiDevCat.AVL.Rendering
{
    public class AVLRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private AVLFeatureSettings _settings;

        private GraphicsBuffer _globalLightsBuffer;
        private GraphicsBuffer _globalMasksBuffer;

        private Queue<ScriptableRenderPass> _renderPasses = new();
        private Queue<IDisposable> _disposablePasses = new();

        private bool _flDisposed = true;
        
        public override void Create()
        {
            if (!_flDisposed)
            {
                FullCleanup();
            }
            
            if (_settings == null)
            {
                return;
            }

            _flDisposed = false;

            // Initialize
            name = "Analytical Volumetric Lighting";

            #if UNITY_EDITOR
            
            // // Load shaders
            // if (_settings.ClusteringShader == null)
            // {
            //     _settings.ClusteringShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(
            //         "Assets/AkiDevCat/AnalyticalVolumetricLighting/Shaders/Passes/AVLClusteringPass.compute");
            //     UnityEditor.EditorUtility.SetDirty(this);
            // }
            //
            // if (_settings.RenderingShader == null)
            // {
            //     _settings.RenderingShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
            //         "Assets/AkiDevCat/AnalyticalVolumetricLighting/Shaders/Passes/AVLRenderingPass.shader");
            //     UnityEditor.EditorUtility.SetDirty(this);
            // }
            //
            // if (_settings.DebugShader == null)
            // {
            //     _settings.DebugShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
            //         "Assets/AkiDevCat/AnalyticalVolumetricLighting/Shaders/Passes/AVLDebugPass.shader");
            //     UnityEditor.EditorUtility.SetDirty(this);
            // }
            //
            // UnityEditor.AssetDatabase.SaveAssets();

            #endif
            
            // CreateGraphicsBuffers();

            // Create Passes
            var setupPass =               new SetupPass(_settings);
            var depthDownsamplePass =     new DepthDownsamplePass(_settings);
            var clusteringPass =          new ClusteringPass(_settings);
            var renderingPass =           new RenderingPass(_settings);
            
            _renderPasses.Enqueue(setupPass);
            _renderPasses.Enqueue(depthDownsamplePass);
            _renderPasses.Enqueue(clusteringPass);
            _renderPasses.Enqueue(renderingPass);
            _disposablePasses.Enqueue(depthDownsamplePass);
            _disposablePasses.Enqueue(clusteringPass);
            _disposablePasses.Enqueue(renderingPass);

            if (_settings.debugMode != DebugMode.None)
            {
                _renderPasses.Enqueue(new DebugPass(_settings));
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_flDisposed)
            {
                Debug.LogError($"{nameof(AVLRenderFeature)} is already disposed.");
                return;
            }
            
            if (!_settings.IsValid)
            {
                return;
            }

            // Enqueue all passes
            foreach (var pass in _renderPasses)
            {
                renderer.EnqueuePass(pass);
            }
        }
        
        

        protected override void Dispose(bool disposing) 
        {
            FullCleanup();
        }

        private void FullCleanup()
        {
            foreach (var pass in _disposablePasses)
            {
                pass.Dispose();
            }
            
            _renderPasses.Clear();
            _disposablePasses.Clear();

            _flDisposed = true;
        }
    }
}