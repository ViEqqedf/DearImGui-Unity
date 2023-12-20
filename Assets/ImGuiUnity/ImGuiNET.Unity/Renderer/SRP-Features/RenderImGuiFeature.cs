using UnityEngine.Rendering;

#if HAS_URP
using UnityEngine.Rendering.Universal;
#endif

#if HAS_URP
namespace ImGuiNET.Unity {
    public class RenderImGuiFeature : ScriptableRendererFeature {
        private class ExecuteCommandBufferPass : ScriptableRenderPass {
            public CommandBuffer cmd;

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                context.ExecuteCommandBuffer(cmd);
            }
        }

        public CommandBuffer commandBuffer;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        private ExecuteCommandBufferPass executeCommandBufferPass;

        public override void Create() {
            executeCommandBufferPass = new ExecuteCommandBufferPass() {
                cmd = commandBuffer,
                renderPassEvent = renderPassEvent,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (commandBuffer == null) return;
            executeCommandBufferPass.renderPassEvent = renderPassEvent;
            executeCommandBufferPass.cmd = commandBuffer;
            renderer.EnqueuePass(executeCommandBufferPass);
        }
    }
}
#else
namespace ImGuiNET.Unity {
    public class RenderImGuiFeature : UnityEngine.ScriptableObject {
        public CommandBuffer commandBuffer;
    }
}
#endif