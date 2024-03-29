﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace ImGuiNET.Unity {
    /// <summary>
    /// Renderer bindings in charge of producing instructions for rendering ImGui draw data.
    /// Uses DrawMesh.
    /// </summary>
    public sealed class ImGuiRendererMesh : IImGuiRenderer {
        private readonly Shader shader;
        private readonly int texID;

        private Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        private readonly TextureManager texManager;

        private Mesh mesh;
        // Color sent with TexCoord1 semantics because otherwise Color attribute would be reordered to come before UVs
        private static readonly VertexAttributeDescriptor[] vertexAttrs = new[] {   // ImDrawVert layout
            new VertexAttributeDescriptor(VertexAttribute.Position , VertexAttributeFormat.Float32, 2), // position
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2), // uv
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32 , 1), // color
        };
        // skip all checks and validation when updating the mesh
        private const MeshUpdateFlags NoMeshChecks = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds
                                           | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontValidateIndices;
        private int prevSubMeshCount = 1;  // number of sub meshes used previously

        private List<SubMeshDescriptor> descriptors = new List<SubMeshDescriptor>();
        private static readonly ProfilerMarker s_updateMeshPerfMarker = new ProfilerMarker("DearImGui.RendererMesh.UpdateMesh");
        private static readonly ProfilerMarker s_createDrawComandsPerfMarker = new ProfilerMarker("DearImGui.RendererMesh.CreateDrawCommands");

        public ImGuiRendererMesh(ShaderResourcesAsset resources, TextureManager texManager) {
            shader = resources.shaders.mesh;
            this.texManager = texManager;
            texID = Shader.PropertyToID(resources.propertyNames.tex);
        }

        public void Initialize(ImGuiIOPtr io) {
            // TODO:[ViE] allow to set backend renderer name
            // io.SetBackendRendererName("Unity Mesh");                            // setup renderer info and capabilities
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;          // supports ImDrawCmd::VtxOffset to output large meshes while still using 16-bits indices

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave & ~HideFlags.DontUnloadUnusedAsset };
            mesh = new Mesh() { name = "DearImGui Mesh" };
            mesh.MarkDynamic();
        }

        public void Shutdown(ImGuiIOPtr io) {
            // TODO:[ViE] allow to set backend renderer name
            // io.SetBackendRendererName(null);

            if (mesh     != null) { Object.Destroy(mesh);      mesh     = null; }
            if (material != null) { Object.Destroy(material);  material = null; }
        }

        public void RenderDrawLists(CommandBuffer cmd, ImDrawDataPtr drawData) {
            Vector2 fbSize = ImGuiUnity.CreateUnityVec2(drawData.DisplaySize * drawData.FramebufferScale);
            if (fbSize.x <= 0f || fbSize.y <= 0f || drawData.TotalVtxCount == 0)
                return; // avoid rendering when minimized

            s_updateMeshPerfMarker.Begin();
            UpdateMesh(drawData, fbSize);
            s_updateMeshPerfMarker.End();

            cmd.BeginSample("DearImGui.ExecuteDrawCommands");
            s_createDrawComandsPerfMarker.Begin();
            CreateDrawCommands(cmd, drawData, fbSize);
            s_createDrawComandsPerfMarker.End();
            cmd.EndSample("DearImGui.ExecuteDrawCommands");
        }

        private unsafe void UpdateMesh(ImDrawDataPtr drawData, Vector2 fbSize) {
            int subMeshCount = 0; // nr of submeshes is the same as the nr of ImDrawCmd
            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n)
                subMeshCount += drawData.CmdLists[n].CmdBuffer.Size;

            // set mesh structure
            if (prevSubMeshCount != subMeshCount) {
                mesh.Clear(true); // occasionally crashes when changing subMeshCount without clearing first
                mesh.subMeshCount = prevSubMeshCount = subMeshCount;
            }

            mesh.SetVertexBufferParams(drawData.TotalVtxCount, vertexAttrs);
            mesh.SetIndexBufferParams (drawData.TotalIdxCount, IndexFormat.UInt16);

            // upload data into mesh
            int vtxOf = 0;
            int idxOf = 0;
            // int subOf = 0;
            descriptors.Clear();

            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n) {
                ImDrawListPtr drawList = drawData.CmdLists[n];
                NativeArray<ImDrawVert> vtxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ImDrawVert>(
                    (void*)drawList.VtxBuffer.Data, drawList.VtxBuffer.Size, Allocator.None);
                NativeArray<ushort>     idxArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>(
                    (void*)drawList.IdxBuffer.Data, drawList.IdxBuffer.Size, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref vtxArray, AtomicSafetyHandle.GetTempMemoryHandle());
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref idxArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                // upload vertex/index data
                mesh.SetVertexBufferData(vtxArray, 0, vtxOf, vtxArray.Length, 0, NoMeshChecks);
                mesh.SetIndexBufferData (idxArray, 0, idxOf, idxArray.Length,    NoMeshChecks);

                // define subMeshes
                for (int i = 0, iMax = drawList.CmdBuffer.Size; i < iMax; ++i) {
                    ImDrawCmdPtr cmd = drawList.CmdBuffer[i];
                    var descriptor = new SubMeshDescriptor {
                        topology = MeshTopology.Triangles,
                        indexStart = idxOf + (int)cmd.IdxOffset,
                        indexCount = (int)cmd.ElemCount,
                        baseVertex = vtxOf + (int)cmd.VtxOffset,
                    };

                    descriptors.Add(descriptor);
                }

                vtxOf += vtxArray.Length;
                idxOf += idxArray.Length;
            }

            mesh.SetSubMeshes(descriptors, NoMeshChecks);
            mesh.UploadMeshData(false);
        }

        private void CreateDrawCommands(CommandBuffer cmd, ImDrawDataPtr drawData, Vector2 fbSize) {
            var prevTextureId = System.IntPtr.Zero;
            var clipOffset = new Vector4(drawData.DisplayPos.X, drawData.DisplayPos.Y, drawData.DisplayPos.X, drawData.DisplayPos.Y);
            var clipScale = new Vector4(drawData.FramebufferScale.X, drawData.FramebufferScale.Y, drawData.FramebufferScale.X, drawData.FramebufferScale.Y);

            cmd.SetViewport(new Rect(0f, 0f, fbSize.x, fbSize.y));
            cmd.SetViewProjectionMatrices(
                Matrix4x4.Translate(new Vector3(0.5f / fbSize.x, 0.5f / fbSize.y, 0f)), // small adjustment to improve text
                Matrix4x4.Ortho(0f, fbSize.x, fbSize.y, 0f, 0f, 1f));

            int subOf = 0;

            for (int n = 0, nMax = drawData.CmdListsCount; n < nMax; ++n) {
                ImDrawListPtr drawList = drawData.CmdLists[n];
                for (int i = 0, iMax = drawList.CmdBuffer.Size; i < iMax; ++i, ++subOf) {
                    ImDrawCmdPtr drawCmd = drawList.CmdBuffer[i];
                    // TODO: user callback in drawCmd.UserCallback & drawCmd.UserCallbackData

                    // project scissor rectangle into framebuffer space and skip if fully outside
                    var clip = Vector4.Scale(ImGuiUnity.CreateUnityVec4(drawCmd.ClipRect) - clipOffset, clipScale);
                    if (clip.x >= fbSize.x || clip.y >= fbSize.y || clip.z < 0f || clip.w < 0f) continue;

                    if (prevTextureId != drawCmd.TextureId)
                        properties.SetTexture(texID, texManager.GetTexture((int)(prevTextureId = drawCmd.TextureId)));

                    cmd.EnableScissorRect(new Rect(clip.x, fbSize.y - clip.w, clip.z - clip.x, clip.w - clip.y)); // invert y
                    cmd.DrawMesh(mesh, Matrix4x4.identity, material, subOf, -1, properties);
                }
            }

            cmd.DisableScissorRect();
        }
    }
}