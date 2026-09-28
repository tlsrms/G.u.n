using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Gun.RoomRhythm
{
    public sealed class AnalogScreenFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material passMaterial;
        private AnalogPass pass;

        public override void Create()
        {
            pass?.Dispose();
            pass = new AnalogPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (passMaterial == null || !passMaterial.shader.isSupported || passMaterial.passCount == 0) return;
            CameraType type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            pass.Material = passMaterial;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => pass?.Dispose();

        private sealed class AnalogPass : ScriptableRenderPass
        {
            internal Material Material;
            private RTHandle copy;
            private static readonly Vector4 ScaleBias = new Vector4(1, 1, 0, 0);

            internal AnalogPass() { requiresIntermediateTexture = true; }

            private sealed class PassData
            {
                internal TextureHandle Source;
                internal Material Material;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid()) return;
                TextureHandle source = resources.activeColorTexture;
                TextureDesc descriptor = graph.GetTextureDesc(source);
                descriptor.name = "Analog screen color";
                descriptor.clearBuffer = false;
                TextureHandle destination = graph.CreateTexture(descriptor);
                using (var builder = graph.AddRasterRenderPass<PassData>("Analog monochrome screen", out var data))
                {
                    data.Source = source;
                    data.Material = Material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData input, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, input.Source, ScaleBias, input.Material, 0));
                }
                // Downstream rendering consumes the processed image; never sample the active output attachment.
                resources.cameraColor = destination;
            }

#pragma warning disable CS0618 // Support projects with URP RenderGraph compatibility mode enabled.
            [System.Obsolete("Compatibility path; RenderGraph uses RecordRenderGraph.")]
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref copy, descriptor, name: "Analog screen copy");
            }

            [System.Obsolete("Compatibility path; RenderGraph uses RecordRenderGraph.")]
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (source == null || copy == null) return;
                CommandBuffer cmd = CommandBufferPool.Get("Analog monochrome screen");
                try
                {
                    Blitter.BlitCameraTexture(cmd, source, copy);
                    Blitter.BlitCameraTexture(cmd, copy, source, Material, 0);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
#pragma warning restore CS0618

            internal void Dispose() { copy?.Release(); copy = null; }
        }
    }
}
