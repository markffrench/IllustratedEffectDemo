using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace bornacvitanic.Quantum.Rendering
{
    public class ScreenSpaceOutlines : ScriptableRendererFeature {
        
        [System.Serializable]
        private class ScreenSpaceOutlineSettings {

            public Material screenSpaceOutlineMaterial;
            
            [Header("General Outline Settings")]
            public Color outlineColor = UnityEngine.Color.black;
            [Range(0.0f, 20.0f)]
            public float outlineScale = 1.0f;
        
            [Header("Depth Settings")]
            [Range(0.0f, 100.0f)]
            public float depthThreshold = 1.5f;
            [Range(0.0f, 500.0f)]
            public float robertsCrossMultiplier = 100.0f;

            [Header("Normal Settings")]
            [Range(0.0f, 1.0f)]
            public float normalThreshold = 0.4f;

            [Header("Depth Normal Relation Settings")]
            [Range(0.0f, 2.0f)]
            public float steepAngleThreshold = 0.2f;
            [Range(0.0f, 500.0f)]
            public float steepAngleMultiplier = 25.0f;

            [Header("Debug")] 
            public bool debugView;

        }
        
        [System.Serializable]
        public class BaseTextureSettings
        {
            [Header("General Texture Settings")]
            public Material material;
            public RenderTextureFormat colorFormat;
            public int depthBufferBits = 16;
            public FilterMode filterMode;
            public Color backgroundColor = UnityEngine.Color.black;
            
            [Header("Texture Object Draw Settings")]
            public bool enableDynamicBatching;
            public bool enableInstancing;
        }
        
        [System.Serializable]
        private class ViewSpaceNormalsTextureSettings : BaseTextureSettings {
            
            public Material occludersMaterial;

            [Header("View Space Normal Texture Object Draw Settings")]
            public PerObjectData perObjectData;
        }
        
        [System.Serializable]
        private class VertexColorsTextureSettings: BaseTextureSettings {
        }

        private abstract class BaseTexturePass : ScriptableRenderPass
        {
            private readonly BaseTextureSettings textureSettings;
            protected FilteringSettings FilteringSettings;
            private readonly List<ShaderTagId> shaderTagIdList;
            private readonly RenderTargetHandle textureHandle;

            protected BaseTexturePass(RenderPassEvent renderPassEvent, LayerMask layerMask, BaseTextureSettings settings, string profilingSamplerName, string textureHandleName)
            {
                this.renderPassEvent = renderPassEvent;
                profilingSampler = new ProfilingSampler(profilingSamplerName);
                textureSettings = settings;
                if (textureSettings.material == null) return;
                FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
                
                shaderTagIdList = new List<ShaderTagId>
                {
                    new("UniversalForward"),
                    new("UniversalForwardOnly"),
                    new("LightweightForward"),
                    new("SRPDefaultUnlit")
                };

                textureHandle.Init(textureHandleName);
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                RenderTextureDescriptor textureDescriptor = cameraTextureDescriptor;
                textureDescriptor.colorFormat = textureSettings.colorFormat;
                textureDescriptor.depthBufferBits = textureSettings.depthBufferBits;
                cmd.GetTemporaryRT(textureHandle.id, textureDescriptor, textureSettings.filterMode);

                ConfigureTarget(textureHandle.Identifier());
                ConfigureClear(ClearFlag.All, textureSettings.backgroundColor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!textureSettings.material)
                    return;

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                    drawSettings.overrideMaterial = textureSettings.material;
                    drawSettings.enableDynamicBatching = textureSettings.enableDynamicBatching;
                    drawSettings.enableInstancing = textureSettings.enableInstancing;

                    ExecuteTexturePass(context, ref renderingData, cmd, drawSettings);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(textureHandle.id);
            }

            protected abstract void ExecuteTexturePass(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, DrawingSettings drawSettings);
        }
        
        private class ViewSpaceNormalsTexturePass : BaseTexturePass {
            
            private readonly ViewSpaceNormalsTextureSettings normalsTextureSettings;
            private FilteringSettings occluderFilteringSettings;

            public ViewSpaceNormalsTexturePass(RenderPassEvent renderPassEvent, LayerMask layerMask, LayerMask occluderLayerMask, ViewSpaceNormalsTextureSettings settings) : base(renderPassEvent, layerMask, settings,"View Space Normals Texture Pass", "_SceneViewSpaceNormals")
            {
                normalsTextureSettings = settings;
                if (normalsTextureSettings.material == null || normalsTextureSettings.occludersMaterial == null) return;
                FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
                occluderFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, occluderLayerMask);
                
                normalsTextureSettings.occludersMaterial.SetColor(Color, normalsTextureSettings.backgroundColor);
            }

            protected override void ExecuteTexturePass(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, DrawingSettings drawSettings) {
                if (!normalsTextureSettings.material || !normalsTextureSettings.occludersMaterial)
                    return;
                
                drawSettings.perObjectData = normalsTextureSettings.perObjectData;

                DrawingSettings occluderSettings = drawSettings;
                occluderSettings.overrideMaterial = normalsTextureSettings.occludersMaterial;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref FilteringSettings);
                context.DrawRenderers(renderingData.cullResults, ref occluderSettings, ref occluderFilteringSettings);
            }
        }
        
        private class VertexColorsTexturePass : BaseTexturePass {
            private readonly VertexColorsTextureSettings vertexColorsTextureSettings;

            public VertexColorsTexturePass(RenderPassEvent renderPassEvent, LayerMask layerMask, VertexColorsTextureSettings settings) : base(renderPassEvent, layerMask, settings,"Vertex Colors Texture Pass", "_SceneVertexColors") {
                vertexColorsTextureSettings = settings;
                if (vertexColorsTextureSettings.material == null) return;

                FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            }

            protected override void ExecuteTexturePass(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, DrawingSettings drawSettings) {
                if (!vertexColorsTextureSettings.material)
                    return;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref FilteringSettings);
            }
        }

        private class ScreenSpaceOutlinePass : ScriptableRenderPass {
            
            private readonly Material screenSpaceOutlineMaterial;

            private RenderTargetIdentifier cameraColorTarget;
            private RenderTargetIdentifier temporaryBuffer;

            public ScreenSpaceOutlinePass(RenderPassEvent renderPassEvent, ScreenSpaceOutlineSettings settings) {
                this.renderPassEvent = renderPassEvent;
                profilingSampler = new ProfilingSampler("Screen Space Outline Pass");

                screenSpaceOutlineMaterial = settings.screenSpaceOutlineMaterial;
                if (screenSpaceOutlineMaterial == null) return;
                
                screenSpaceOutlineMaterial.SetColor(OutlineColor, settings.outlineColor);
                screenSpaceOutlineMaterial.SetFloat(OutlineScale, settings.outlineScale);

                screenSpaceOutlineMaterial.SetFloat(DepthThreshold, settings.depthThreshold);
                screenSpaceOutlineMaterial.SetFloat(RobertsCrossMultiplier, settings.robertsCrossMultiplier);

                screenSpaceOutlineMaterial.SetFloat(NormalThreshold, settings.normalThreshold);

                screenSpaceOutlineMaterial.SetFloat(SteepAngleThreshold, settings.steepAngleThreshold);
                screenSpaceOutlineMaterial.SetFloat(SteepAngleMultiplier, settings.steepAngleMultiplier);
                
                screenSpaceOutlineMaterial.SetFloat(DebugView,settings.debugView?1:0);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
                RenderTextureDescriptor temporaryTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                temporaryTargetDescriptor.depthBufferBits = 0;
                cmd.GetTemporaryRT(TemporaryBufferID, temporaryTargetDescriptor, FilterMode.Bilinear);
                temporaryBuffer = new RenderTargetIdentifier(TemporaryBufferID);

                cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                if (!screenSpaceOutlineMaterial)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler)) {

                    Blit(cmd, cameraColorTarget, temporaryBuffer);
                    Blit(cmd, temporaryBuffer, cameraColorTarget, screenSpaceOutlineMaterial);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd) {
                cmd.ReleaseTemporaryRT(TemporaryBufferID);
            }
        }
        
        private static readonly int Color = Shader.PropertyToID("_Color");
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineScale = Shader.PropertyToID("_OutlineScale");
        private static readonly int DepthThreshold = Shader.PropertyToID("_DepthThreshold");
        private static readonly int RobertsCrossMultiplier = Shader.PropertyToID("_RobertsCrossMultiplier");
        private static readonly int NormalThreshold = Shader.PropertyToID("_NormalThreshold");
        private static readonly int SteepAngleThreshold = Shader.PropertyToID("_SteepAngleThreshold");
        private static readonly int SteepAngleMultiplier = Shader.PropertyToID("_SteepAngleMultiplier");
        private static readonly int DebugView = Shader.PropertyToID("_DebugView");
        private static readonly int TemporaryBufferID = Shader.PropertyToID("_TemporaryBuffer");

        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        [SerializeField] private LayerMask outlinesLayerMask;
        [SerializeField] private LayerMask outlinesOccluderLayerMask;
    
        [SerializeField] private ScreenSpaceOutlineSettings outlineSettings = new();
        [SerializeField] private ViewSpaceNormalsTextureSettings viewSpaceNormalsTextureSettings = new();
        [SerializeField] private VertexColorsTextureSettings vertexColorsTextureSettings = new();

        private ViewSpaceNormalsTexturePass viewSpaceNormalsTexturePass;
        private VertexColorsTexturePass vertexColorsTexturePass;
        private ScreenSpaceOutlinePass screenSpaceOutlinePass;

        public override void Create() {
            if (renderPassEvent < RenderPassEvent.BeforeRenderingPrePasses)
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;

            viewSpaceNormalsTexturePass = new ViewSpaceNormalsTexturePass(renderPassEvent, outlinesLayerMask, outlinesOccluderLayerMask, viewSpaceNormalsTextureSettings);
            vertexColorsTexturePass = new VertexColorsTexturePass(renderPassEvent, outlinesLayerMask, vertexColorsTextureSettings);
            screenSpaceOutlinePass = new ScreenSpaceOutlinePass(renderPassEvent, outlineSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            renderer.EnqueuePass(viewSpaceNormalsTexturePass);
            renderer.EnqueuePass(vertexColorsTexturePass);
            renderer.EnqueuePass(screenSpaceOutlinePass);
        }
    }
}
