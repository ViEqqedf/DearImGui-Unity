using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace ImGuiNET.Unity {
    // Implemented features:
    // [x] Platform: Clipboard support.
    // [x] Platform: Mouse cursor shape and visibility. Disable with io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange.
    // [x] Platform: Keyboard arrays indexed using KeyCode codes, e.g. ImGui.IsKeyPressed(KeyCode.Space).
    // [ ] Platform: Gamepad support. Enabled with io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad.
    // [~] Platform: IME support.
    // [~] Platform: INI settings support.

    /// <summary>
    /// Platform bindings for ImGui in Unity in charge of: mouse/keyboard/gamepad inputs, cursor shape, timing, windowing.
    /// </summary>
    public sealed class ImGuiPlatformInputManager : IImGuiPlatform {
        private Dictionary<ImGuiKey, KeyCode> mainKeys;                                                        // main keys
        private readonly Event e = new Event();                                        // to get text input

        private readonly CursorShapesAsset cursorShapes;                               // cursor shape definitions
        private ImGuiMouseCursor lastCursor = ImGuiMouseCursor.COUNT;                  // last cursor requested by ImGui

        private readonly IniSettingsAsset iniSettings;                                 // ini settings data

        private readonly PlatformCallbacks callbacks = new PlatformCallbacks {
            GetClipboardText = (_) => GUIUtility.systemCopyBuffer,
            SetClipboardText = (_, text) => GUIUtility.systemCopyBuffer = text,
#if IMGUI_FEATURE_CUSTOM_ASSERT
            LogAssert = (condition, file, line) => Debug.LogError($"[DearImGui] Assertion failed: '{condition}', file '{file}', line: {line}."),
            DebugBreak = () => System.Diagnostics.Debugger.Break(),
#endif
        };

        public ImGuiPlatformInputManager(CursorShapesAsset cursorShapes, IniSettingsAsset iniSettings) {
            this.cursorShapes = cursorShapes;
            this.iniSettings = iniSettings;
            callbacks.ImeSetInputScreenPos = (x, y) => Input.compositionCursorPos = new Vector2(x, y);
        }

        public bool Initialize(ImGuiIOPtr io) {
            // TODO:[ViE] allow to set backend platform name
            // io.SetBackendPlatformName("Unity Input Manager");                   // setup backend info and capabilities
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;               // can honor GetMouseCursor() values
            io.BackendFlags &= ~ImGuiBackendFlags.HasSetMousePos;               // can't honor io.WantSetMousePos requests
            // io.BackendFlags |= ImGuiBackendFlags.HasGamepad;                 // set by UpdateGamepad()

            callbacks.Assign(io);                                              // assign platform callbacks
            io.ClipboardUserData = IntPtr.Zero;

            if (iniSettings != null) {                                         // ini setting
                // TODO:[ViE] allow to set ini file name
                // io.SetIniFilename(null);                                        // handle ini saving manually
                ImGui.LoadIniSettingsFromMemory(iniSettings.Load());           // call after CreateContext(), before first call to NewFrame()
            }

            SetupKeyboard(io);                                                  // sets key mapping, text input, and IME

            return true;
        }

        public void Shutdown(ImGuiIOPtr io) {
            callbacks.Unset(io);
            // TODO:[ViE] allow to set backend platform name
            // io.SetBackendPlatformName(null);
        }

        public void PrepareFrame(ImGuiIOPtr io, Rect displayRect) {
            Assert.IsTrue(io.Fonts.IsBuilt(), "Font atlas not built! Generally built by the renderer. Missing call to renderer NewFrame() function?");

            io.DisplaySize = ImGuiUnity.CreateSysVec2(displayRect.width, displayRect.height);// setup display size (every frame to accommodate for window resizing)
            // TODO: dpi aware, scale, etc

            io.DeltaTime = Time.unscaledDeltaTime;                              // setup timestep

            // input
            UpdateKeyboard(io);                                                 // update keyboard state
            UpdateMouse(io);                                                    // update mouse state
            UpdateCursor(io, ImGui.GetMouseCursor());                           // update Unity cursor with the cursor requested by ImGui

            // ini settings
            if (iniSettings != null && io.WantSaveIniSettings) {
                iniSettings.Save(ImGui.SaveIniSettingsToMemory());
                io.WantSaveIniSettings = false;
            }
        }

        private void SetupKeyboard(ImGuiIOPtr io) {
            mainKeys = new Dictionary<ImGuiKey, KeyCode>();
            mainKeys.Add(ImGuiKey.Tab, KeyCode.Tab);
            mainKeys.Add(ImGuiKey.LeftArrow, KeyCode.LeftArrow);
            mainKeys.Add(ImGuiKey.RightArrow, KeyCode.RightArrow);
            mainKeys.Add(ImGuiKey.UpArrow, KeyCode.UpArrow);
            mainKeys.Add(ImGuiKey.DownArrow, KeyCode.DownArrow);
            mainKeys.Add(ImGuiKey.PageUp, KeyCode.PageUp);
            mainKeys.Add(ImGuiKey.PageDown, KeyCode.PageDown);
            mainKeys.Add(ImGuiKey.Home, KeyCode.Home);
            mainKeys.Add(ImGuiKey.End, KeyCode.End);
            mainKeys.Add(ImGuiKey.Insert, KeyCode.Insert);
            mainKeys.Add(ImGuiKey.Delete, KeyCode.Delete);
            mainKeys.Add(ImGuiKey.Backspace, KeyCode.Backspace);
            mainKeys.Add(ImGuiKey.Space, KeyCode.Space);
            mainKeys.Add(ImGuiKey.Enter, KeyCode.Return);
            mainKeys.Add(ImGuiKey.Escape, KeyCode.Escape);
            mainKeys.Add(ImGuiKey.KeypadEnter, KeyCode.KeypadEnter);
            mainKeys.Add(ImGuiKey.A, KeyCode.A);
            mainKeys.Add(ImGuiKey.C, KeyCode.C);
            mainKeys.Add(ImGuiKey.V, KeyCode.V);
            mainKeys.Add(ImGuiKey.X, KeyCode.X);
            mainKeys.Add(ImGuiKey.Y, KeyCode.Y);
            mainKeys.Add(ImGuiKey.Z, KeyCode.Z);
        }

        private void UpdateKeyboard(ImGuiIOPtr io) {
            foreach (var keyMapItem in mainKeys) {
                io.AddKeyEvent(keyMapItem.Key, Input.GetKey(keyMapItem.Value));
            }

            // keyboard modifiers
            io.KeyShift = Input.GetKey(KeyCode.LeftShift  ) || Input.GetKey(KeyCode.RightShift  );
            io.KeyCtrl  = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            io.KeyAlt   = Input.GetKey(KeyCode.LeftAlt    ) || Input.GetKey(KeyCode.RightAlt    );
            io.KeySuper = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                       || Input.GetKey(KeyCode.LeftWindows) || Input.GetKey(KeyCode.RightWindows);

            // text input
            while (Event.PopEvent(e)) {
                if (e.rawType == EventType.KeyDown && e.character != 0 && e.character != '\n') {
                    io.AddInputCharacter(e.character);
                }
            }
        }

        private static void UpdateMouse(ImGuiIOPtr io) {
            io.MousePos = ImGuiUnity.CreateSysVec2(ImGuiUnity.ScreenToImGui(new Vector2(Input.mousePosition.x, Input.mousePosition.y)));

            io.MouseWheel  = Input.mouseScrollDelta.y;
            io.MouseWheelH = Input.mouseScrollDelta.x;

            io.MouseDown[0] = Input.GetMouseButton(0);
            io.MouseDown[1] = Input.GetMouseButton(1);
            io.MouseDown[2] = Input.GetMouseButton(2);
        }

        private void UpdateCursor(ImGuiIOPtr io, ImGuiMouseCursor cursor) {
            if (io.MouseDrawCursor) {
                cursor = ImGuiMouseCursor.None;
            }

            if (lastCursor == cursor) {
                return;
            }

            if ((io.ConfigFlags&ImGuiConfigFlags.NoMouseCursorChange) != 0) {
                return;
            }

            lastCursor = cursor;
            Cursor.visible = cursor != ImGuiMouseCursor.None;                   // hide cursor if ImGui is drawing it or if it wants no cursor
            if (cursorShapes != null) {
                Cursor.SetCursor(cursorShapes[cursor].texture, cursorShapes[cursor].hotspot, CursorMode.Auto);
            }
        }
    }
}