using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Object = UnityEngine.Object;

// TODO: switch from using ComputeBuffer to GraphicsBuffer
// starting from 2020.1 API that takes ComputeBuffer can also take GraphicsBuffer
// https://docs.unity3d.com/2020.1/Documentation/ScriptReference/GraphicsBuffer.Target.html

namespace ImGuiNET.Unity {
    /// <summary>
    /// Renderer bindings in charge of producing instructions for rendering ImGui draw data.
    /// Uses DrawProceduralIndirect to build geometry from a buffer of vertex data.
    /// </summary>
    /// <remarks>Requires shader model 4.5 level hardware.</remarks>
    public sealed class ImGuiRendererProcedural : IImGuiRenderer {
        private readonly Shader shader;
        private readonly int texID;
        private readonly int verticesID;
        private readonly int baseVertexID;

        private Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        private readonly TextureManager texManager;

        private ComputeBuffer vtxBuf;                                                  // gpu buffer for vertex data
        private GraphicsBuffer idxBuf;                                                 // gpu buffer for indexes
        private ComputeBuffer argBuf;                                                  // gpu buffer for draw arguments
        private readonly int[] drawArgs = new int[] { 0, 1, 0, 0, 0 };                 // used to build argument buffer

        private static readonly ProfilerMarker s_updateBuffersPerfMarker = new ProfilerMarker("DearImGui.RendererProcedural.UpdateBuffers");
        private static readonly ProfilerMarker s_createDrawComandsPerfMarker = new ProfilerMarker("DearImGui.RendererProcedural.CreateDrawCommands");

        public ImGuiRendererProcedural(ShaderResourcesAsset resources, TextureManager texManager) {
            if (SystemInfo.graphicsShaderLevel < 45) {
                throw new System.Exception("Device not supported");
            }

            shader = resources.shaders.procedural;
            this.texManager = texManager;
            texID = Shader.PropertyToID(resources.propertyNames.tex);
            verticesID = Shader.PropertyToID(resources.propertyNames.vertices);
            baseVertexID = Shader.PropertyToID(resources.propertyNames.baseVertex);
        }

        public void Initialize(ImGuiIOPtr io)
        {
            // TODO:[ViE] allow to set backend renderer name
            // io.SetBackendRendererName("Unity Procedural");                      // setup renderer info and capabilities
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;          // supports ImDrawCmd::VtxOffset to output large meshes while still using 16-bits indices

            material = new Material(shader) {
                hideFlags = HideFlags.HideAndDontSave & ~HideFlags.DontUnloadUnusedAsset
            };
        }

        public void Shutdown(ImGuiIOPtr io) {
            // TODO:[ViE] allow to set backend renderer name
            // io.SetBackendRendererName(null);

            if (material != null) {
                Object.Destroy(material);
                material = null;
            }

            vtxBuf?.Release();
            vtxBuf = null;
            idxBuf?.Release();
            idxBuf = null;
            argBuf?.Release();
            argBuf = null;
        }

        public void RenderDrawLists(CommandBuffer cmd, ImDrawDataPtr drawData) {
            Vector2 fbSize = ImGuiUnity.CreateUnityVec2(drawData.DisplaySize * drawData.FramebufferScale);
            if (fbSize.x <= 0f || fbSize.y <= 0f || drawData.TotalVtxCount == 0) {
                return; // avoid rendering when minimized
            }

            s_updateBuffersPerfMarker.Begin();
            UpdateBuffers(drawData);
            s_updateBuffersPerfMarker.End();

            cmd.BeginSample("DearImGui.ExecuteDrawCommands");
            s_createDrawComandsPerfMarker.Begin();
            CreateDrawCommands(cmd, drawData, fbSize);
            s_createDrawComandsPerfMarker.End();
            cmd.EndSample("DearImGui.ExecuteDrawCommands");
        }

        void CreateOrResizeVtxBuffer(ref ComputeBuffer buffer, int count) {
            int num = ((count - 1) / 256 + 1) * 256;
            buffer?.Release();
            buffer = new ComputeBuffer(num, UnsafeUtility.SizeOf<ImDrawVert>());
        }
        void CreateOrResizeIdxBuffer(ref GraphicsBuffer buffer, int count) {
            int num = ((count - 1) / 256 + 1) * 256;
            buffer?.Release();
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, num, UnsafeUtility.SizeOf<ushort>());
        }
        void CreateOrResizeArgBuffer(ref ComputeBuffer buffer, int count) {
            int num = ((count - 1) / 256 + 1) * 256;
            buffer?.Release();
            buffer = new ComputeBuffer(num, UnsafeUtility.SizeOf<int>(), ComputeBufferType.IndirectArguments);
        }

        unsafe void UpdateBuffers(ImDrawDataPtr drawData) {
            int drawArgCount = 0; // nr of drawArgs is the same as the nr of ImDrawCmd
            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n) {
                drawArgCount += drawData.CmdLists[n].CmdBuffer.Size;
            }

            // create or resize vertex/index buffers
            if (vtxBuf == null || vtxBuf.count < drawData.TotalVtxCount) {
                CreateOrResizeVtxBuffer(ref vtxBuf, drawData.TotalVtxCount);
            }

            if (idxBuf == null || idxBuf.count < drawData.TotalIdxCount) {
                CreateOrResizeIdxBuffer(ref idxBuf, drawData.TotalIdxCount);
            }

            if (argBuf == null || argBuf.count < drawArgCount * 5) {
                CreateOrResizeArgBuffer(ref argBuf, drawArgCount * 5);
            }

            // upload vertex/index data into buffers
            int vtxOf = 0;
            int idxOf = 0;
            int argOf = 0;

            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n) {
                ImDrawListPtr drawList = drawData.CmdLists[n];
                NativeArray<ImDrawVert> vtxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ImDrawVert>(
                    (void*)drawList.VtxBuffer.Data, drawList.VtxBuffer.Size, Allocator.None);
                NativeArray<ushort> idxArray     = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>(
                    (void*)drawList.IdxBuffer.Data, drawList.IdxBuffer.Size, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref vtxArray, AtomicSafetyHandle.GetTempMemoryHandle());
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref idxArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                // upload vertex/index data
                vtxBuf.SetData(vtxArray, 0, vtxOf, vtxArray.Length);
                idxBuf.SetData(idxArray, 0, idxOf, idxArray.Length);

                // arguments for indexed draw
                drawArgs[3] = vtxOf;                                           // base vertex location
                for (int i = 0, iMax = drawList.CmdBuffer.Size; i < iMax; ++i) {
                    ImDrawCmdPtr cmd = drawList.CmdBuffer[i];
                    drawArgs[0] = (int)cmd.ElemCount;                          // index count per instance
                    drawArgs[2] = idxOf + (int)cmd.IdxOffset;                  // start index location
                    argBuf.SetData(drawArgs, 0, argOf, 5);

                    argOf += 5;                                                 // 5 int for each cmd
                }

                vtxOf += vtxArray.Length;
                idxOf += idxArray.Length;
            }
        }

        void CreateDrawCommands(CommandBuffer cmd, ImDrawDataPtr drawData, Vector2 fbSize) {
            var prevTextureId = System.IntPtr.Zero;
            var clipOffset = new Vector4(drawData.DisplayPos.X, drawData.DisplayPos.Y, drawData.DisplayPos.X, drawData.DisplayPos.Y);
            var clipScale = new Vector4(drawData.FramebufferScale.X, drawData.FramebufferScale.Y, drawData.FramebufferScale.X, drawData.FramebufferScale.Y);

            material.SetBuffer(verticesID, vtxBuf);                          // bind vertex buffer

            cmd.SetViewport(new Rect(0f, 0f, fbSize.x, fbSize.y));
            cmd.SetViewProjectionMatrices(
                Matrix4x4.Translate(new Vector3(0.5f / fbSize.x, 0.5f / fbSize.y, 0f)), // small adjustment to improve text
                Matrix4x4.Ortho(0f, fbSize.x, fbSize.y, 0f, 0f, 1f));

            int vtxOf = 0;
            int argOf = 0;
            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n) {
                ImDrawListPtr drawList = drawData.CmdLists[n];
                for (int i = 0, iMax = drawList.CmdBuffer.Size; i < iMax; ++i, argOf += 5 * 4) {
                    ImDrawCmdPtr drawCmd = drawList.CmdBuffer[i];
                    // TODO: user callback in drawCmd.UserCallback & drawCmd.UserCallbackData

                    // project scissor rectangle into framebuffer space and skip if fully outside
                    var clip = Vector4.Scale(ImGuiUnity.CreateUnityVec4(drawCmd.ClipRect) - clipOffset, clipScale);
                    if (clip.x >= fbSize.x || clip.y >= fbSize.y || clip.z < 0f || clip.w < 0f) {
                        continue;
                    }

                    if (prevTextureId != drawCmd.TextureId)
                        properties.SetTexture(texID, texManager.GetTexture((int)(prevTextureId = drawCmd.TextureId)));

                    properties.SetInt(baseVertexID, vtxOf + (int)drawCmd.VtxOffset); // base vertex location not automatically added to SV_VertexID
                    cmd.EnableScissorRect(new Rect(clip.x, fbSize.y - clip.w, clip.z - clip.x, clip.w - clip.y)); // invert y
                    cmd.DrawProceduralIndirect(idxBuf, Matrix4x4.identity, material, -1, MeshTopology.Triangles, argBuf, argOf, properties);
                }

                vtxOf += drawList.VtxBuffer.Size;
            }

            cmd.DisableScissorRect();
        }
    }
}