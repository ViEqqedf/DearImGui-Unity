using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;
using UnityEngine.Serialization;

namespace ImGuiNET.Unity {
    // This component is responsible for setting up ImGui for use in Unity.
    // It holds the necessary context and sets it up before any operation is done to ImGui.
    // (e.g. set the context, texture and font managers before calling Layout)

    /// <summary>
    /// Dear ImGui integration into Unity
    /// </summary>
    public class DearImGui : MonoBehaviour {
        private ImGuiUnityContext context;
        private IImGuiRenderer guiRenderer;
        private IImGuiPlatform platform;
        private CommandBuffer cmd;
        private bool usingURP;

        public event System.Action Layout;  // Layout event for *this* ImGui instance
        [SerializeField] private bool doGlobalLayout = true; // do global/default Layout event too

        [SerializeField] private Camera guiCamera = null;
        [SerializeField] private RenderImGuiFeature renderFeature = null;

        [SerializeField] private RenderUtils.RenderType rendererType = RenderUtils.RenderType.Mesh;
        [SerializeField] private Platform.Type platformType = Platform.Type.InputManager;

        [Header("Configuration")]
        [SerializeField] private IOConfig initialConfiguration = default;
        [SerializeField] private FontAtlasConfigAsset fontAtlasConfiguration = null;
        [SerializeField] private IniSettingsAsset iniSettings = null;  // null: uses default imgui.ini file

        [Header("Customization")]
        [SerializeField] private ShaderResourcesAsset shaders = null;
        [SerializeField] private StyleAsset style = null;
        [SerializeField] private CursorShapesAsset cursorShapes = null;

        private const string commandBufferTag = "DearImGui";
        private static readonly ProfilerMarker s_prepareFramePerfMarker = new ProfilerMarker("DearImGui.PrepareFrame");
        private static readonly ProfilerMarker s_layoutPerfMarker = new ProfilerMarker("DearImGui.Layout");
        private static readonly ProfilerMarker s_drawListPerfMarker = new ProfilerMarker("DearImGui.RenderDrawLists");

        void Awake() {
            context = ImGuiUnity.CreateUnityContext();
        }

        void OnDestroy() {
            ImGuiUnity.DestroyUnityContext(context);
        }

        void OnEnable() {
            usingURP = RenderUtils.IsUsingURP();
            if (guiCamera == null) {
                Fail(nameof(guiCamera));
            }

            if (renderFeature == null && usingURP) {
                Fail(nameof(renderFeature));
            }

            cmd = RenderUtils.GetCommandBuffer(commandBufferTag);

            if (usingURP) {
                renderFeature.commandBuffer = cmd;
            } else {
                guiCamera.AddCommandBuffer(CameraEvent.AfterEverything, cmd);
            }

            ImGuiUnity.SetUnityContext(context);
            ImGuiIOPtr io = ImGui.GetIO();

            initialConfiguration.ApplyTo(io);
            style?.ApplyTo(ImGui.GetStyle());

            context.textures.BuildFontAtlas(io, fontAtlasConfiguration);
            context.textures.Initialize(io);

            SetPlatform(Platform.Create(platformType, cursorShapes, iniSettings), io);
            SetRenderer(RenderUtils.Create(rendererType, shaders, context.textures), io);
            if (platform == null) {
                Fail(nameof(platform));
            }

            if (guiRenderer == null) {
                Fail(nameof(guiRenderer));
            }

            void Fail(string reason) {
                OnDisable();
                enabled = false;
                throw new System.Exception($"Failed to start: {reason}");
            }
        }

        void OnDisable() {
            ImGuiUnity.SetUnityContext(context);
            ImGuiIOPtr io = ImGui.GetIO();

            SetRenderer(null, io);
            SetPlatform(null, io);

            ImGuiUnity.SetUnityContext(null);

            context.textures.Shutdown();
            context.textures.DestroyFontAtlas(io);

            if (usingURP) {
                if (renderFeature != null) {
                    renderFeature.commandBuffer = null;
                }
            } else {
                if (guiCamera != null) {
                    guiCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, cmd);
                }
            }

            if (cmd != null) {
                RenderUtils.ReleaseCommandBuffer(cmd);
            }

            cmd = null;
        }

        void Reset()
        {
            guiCamera = Camera.main;
            initialConfiguration.SetDefaults();
        }

        public void Reload()
        {
            OnDisable();
            OnEnable();
        }

        void Update()
        {
            ImGuiUnity.SetUnityContext(context);
            ImGuiIOPtr io = ImGui.GetIO();

            s_prepareFramePerfMarker.Begin(this);
            context.textures.PrepareFrame(io);
            platform.PrepareFrame(io, guiCamera.pixelRect);
            ImGui.NewFrame();
            s_prepareFramePerfMarker.End();

            s_layoutPerfMarker.Begin(this);
            try
            {
                if (doGlobalLayout)
                    ImGuiUnity.DoLayout();   // ImGuiUn.Layout: global handlers
                Layout?.Invoke();     // this.Layout: handlers specific to this instance
            }
            finally
            {
                ImGui.Render();
                s_layoutPerfMarker.End();
            }

            s_drawListPerfMarker.Begin(this);
            cmd.Clear();
            guiRenderer.RenderDrawLists(cmd, ImGui.GetDrawData());
            s_drawListPerfMarker.End();
        }

        void SetRenderer(IImGuiRenderer renderer, ImGuiIOPtr io)
        {
            guiRenderer?.Shutdown(io);
            guiRenderer = renderer;
            guiRenderer?.Initialize(io);
        }

        void SetPlatform(IImGuiPlatform platform, ImGuiIOPtr io)
        {
            this.platform?.Shutdown(io);
            this.platform = platform;
            this.platform?.Initialize(io);
        }
    }
}