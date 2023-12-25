#if HAS_INPUTSYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ImGuiNET.Unity {
    // Implemented features:
    // [x] Platform: Clipboard support.
    // [x] Platform: Mouse cursor shape and visibility. Disable with io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange.
    // [x] Platform: Keyboard arrays indexed using InputSystem.Key codes, e.g. ImGui.IsKeyPressed(Key.Space).
    // [~] Platform: Gamepad support. Enabled with io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad.
    // [~] Platform: IME support.
    // [~] Platform: INI settings support.

    /// <summary>
    /// Platform bindings for ImGui in Unity in charge of: mouse/keyboard/gamepad inputs, cursor shape, timing, windowing.
    /// </summary>
    public sealed class ImGuiPlatformInputSystem : IImGuiPlatform {
        private Dictionary<ImGuiKey, Func<Keyboard, bool>> mainKeys;                                                        // main keys
        private readonly List<char> textInput = new List<char>();                      // accumulate text input

        private readonly CursorShapesAsset cursorShapes;                               // cursor shape definitions
        private ImGuiMouseCursor lastCursor = ImGuiMouseCursor.COUNT;                  // last cursor requested by ImGui
        private Keyboard keyboard = null;                                              // currently setup keyboard, need to reconfigure on changes

        private readonly IniSettingsAsset iniSettings;                                 // ini settings data

        private readonly PlatformCallbacks callbacks = new PlatformCallbacks {
            GetClipboardText = (_) => GUIUtility.systemCopyBuffer,
            SetClipboardText = (_, text) => GUIUtility.systemCopyBuffer = text,
#if IMGUI_FEATURE_CUSTOM_ASSERT
            LogAssert = (condition, file, line) => Debug.LogError($"[DearImGui] Assertion failed: '{condition}', file '{file}', line: {line}."),
            DebugBreak = () => System.Diagnostics.Debugger.Break(),
#endif
        };

        public ImGuiPlatformInputSystem(CursorShapesAsset cursorShapes, IniSettingsAsset iniSettings) {
            this.cursorShapes = cursorShapes;
            this.iniSettings = iniSettings;
            callbacks.ImeSetInputScreenPos = (x, y) => keyboard.SetIMECursorPosition(new Vector2(x, y));
        }

        public bool Initialize(ImGuiIOPtr io) {
            InputSystem.onDeviceChange += OnDeviceChange;                       // listen to keyboard device and layout changes

            // TODO:[ViE] allow to set backend platform name
            // io.SetBackendPlatformName("Unity Input System");                    // setup backend info and capabilities
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;               // can honor GetMouseCursor() values
            io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;                // can honor io.WantSetMousePos requests
            // io.BackendFlags |= ImGuiBackendFlags.HasGamepad;                 // set by UpdateGamepad()

            callbacks.Assign(io);                                              // assign platform callbacks
            io.ClipboardUserData = IntPtr.Zero;

            if (iniSettings != null) {                                         // ini settings
                // TODO:[ViE] allow to set ini file name
                // io.SetIniFilename(null);                                        // handle ini saving manually
                ImGui.LoadIniSettingsFromMemory(iniSettings.Load());           // call after CreateContext(), before first call to NewFrame()
            }

            SetupKeyboard(io, Keyboard.current);                                // sets key mapping, text input, and IME

            return true;
        }

        public void Shutdown(ImGuiIOPtr io) {
            callbacks.Unset(io);
            // TODO:[ViE] allow to set backend platform name
            // io.SetBackendPlatformName(null);

            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        public void PrepareFrame(ImGuiIOPtr io, Rect displayRect) {
            Assert.IsTrue(io.Fonts.IsBuilt(), "Font atlas not built! Generally built by the renderer. Missing call to renderer NewFrame() function?");

            io.DisplaySize = ImGuiUnity.CreateSysVec2(displayRect.width, displayRect.height);// setup display size (every frame to accommodate for window resizing)
            // TODO: dpi aware, scale, etc

            io.DeltaTime = Time.unscaledDeltaTime;                              // setup timestep

            // input
            UpdateKeyboard(io, Keyboard.current);                               // update keyboard state
            UpdateMouse(io, Mouse.current);                                     // update mouse state
            UpdateCursor(io, ImGui.GetMouseCursor());                           // update Unity cursor with the cursor requested by ImGui
            UpdateGamepad(io, Gamepad.current);                                 // update game controllers (if enabled and available)

            // ini settings
            if (iniSettings != null && io.WantSaveIniSettings) {
                iniSettings.Save(ImGui.SaveIniSettingsToMemory());
                io.WantSaveIniSettings = false;
            }
        }

        private void SetupKeyboard(ImGuiIOPtr io, Keyboard kb) {
            if (keyboard != null) {
                // for (int i = 0; i < (int)ImGuiKey.COUNT; ++i)
                    // io.KeyMap[i] = -1;
                keyboard.onTextInput -= textInput.Add;
            }
            keyboard = kb;

            mainKeys = new Dictionary<ImGuiKey, Func<Keyboard, bool>>();
            mainKeys.Add(ImGuiKey.Tab, (device) => device.tabKey.isPressed);
            mainKeys.Add(ImGuiKey.LeftArrow, (device) => device.leftArrowKey.isPressed);
            mainKeys.Add(ImGuiKey.RightArrow, (device) => device.rightArrowKey.isPressed);
            mainKeys.Add(ImGuiKey.UpArrow, (device) => device.upArrowKey.isPressed);
            mainKeys.Add(ImGuiKey.DownArrow, (device) => device.downArrowKey.isPressed);
            mainKeys.Add(ImGuiKey.PageUp, (device) => device.pageUpKey.isPressed);
            mainKeys.Add(ImGuiKey.PageDown, (device) => device.pageDownKey.isPressed);
            mainKeys.Add(ImGuiKey.Home, (device) => device.homeKey.isPressed);
            mainKeys.Add(ImGuiKey.End, (device) => device.endKey.isPressed);
            mainKeys.Add(ImGuiKey.Insert, (device) => device.insertKey.isPressed);
            mainKeys.Add(ImGuiKey.Delete, (device) => device.deleteKey.isPressed);
            mainKeys.Add(ImGuiKey.Backspace, (device) => device.backspaceKey.isPressed);
            mainKeys.Add(ImGuiKey.Space, (device) => device.spaceKey.isPressed);
            mainKeys.Add(ImGuiKey.Enter, (device) => device.enterKey.isPressed);
            mainKeys.Add(ImGuiKey.KeypadEnter, (device) => device.numpadEnterKey.isPressed);
            mainKeys.Add(ImGuiKey.ModShift, (device) => device.shiftKey.isPressed);
            mainKeys.Add(ImGuiKey.ModCtrl, (device) => device.ctrlKey.isPressed);
            mainKeys.Add(ImGuiKey.ModAlt, (device) => device.altKey.isPressed);
            mainKeys.Add(ImGuiKey.ModSuper, (device) => device.leftMetaKey.isPressed || device.rightMetaKey.isPressed);
            mainKeys.Add(ImGuiKey.A, (device) => device.aKey.isPressed);
            mainKeys.Add(ImGuiKey.C, (device) => device.cKey.isPressed);
            mainKeys.Add(ImGuiKey.V, (device) => device.vKey.isPressed);
            mainKeys.Add(ImGuiKey.X, (device) => device.xKey.isPressed);
            mainKeys.Add(ImGuiKey.Y, (device) => device.yKey.isPressed);
            mainKeys.Add(ImGuiKey.Z, (device) => device.zKey.isPressed);
        }

        private void UpdateKeyboard(ImGuiIOPtr io, Keyboard keyboard) {
            if (keyboard == null) {
                return;
            }

            // main keys
            foreach (var keyMapItem in mainKeys) {
                io.AddKeyEvent(keyMapItem.Key, keyMapItem.Value.Invoke(keyboard));
            }

            // text input
            for (int i = 0, iMax = textInput.Count; i < iMax; ++i) {
                io.AddInputCharacter(textInput[i]);
            }

            textInput.Clear();
        }

        private static void UpdateMouse(ImGuiIOPtr io, Mouse mouse) {
            if (mouse == null) {
                return;
            }

            if (io.WantSetMousePos) { // set Unity mouse position if requested
                mouse.WarpCursorPosition(ImGuiUnity.ImGuiToScreen(ImGuiUnity.CreateUnityVec2(io.MousePos)));
            }

            io.MousePos = ImGuiUnity.CreateSysVec2(ImGuiUnity.ScreenToImGui(mouse.position.ReadValue()));

            Vector2 mouseScroll = mouse.scroll.ReadValue() / 120f;
            io.MouseWheel   = mouseScroll.y;
            io.MouseWheelH  = mouseScroll.x;

            io.MouseDown[0] = mouse.leftButton.isPressed;
            io.MouseDown[1] = mouse.rightButton.isPressed;
            io.MouseDown[2] = mouse.middleButton.isPressed;
        }

        private static void UpdateGamepad(ImGuiIOPtr io, Gamepad gamepad) {
            io.BackendFlags = gamepad == null
                ? io.BackendFlags & ~ImGuiBackendFlags.HasGamepad
                : io.BackendFlags |  ImGuiBackendFlags.HasGamepad;

            if (gamepad == null || (io.ConfigFlags&ImGuiConfigFlags.NavEnableGamepad) == 0) {
                return;
            }

            // TODO:[ViE] gamepad support
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

        private void OnDeviceChange(InputDevice device, InputDeviceChange change) {
            if (device is Keyboard kb) {
                if (change == InputDeviceChange.ConfigurationChanged) { // keyboard layout change, remap main keys
                    SetupKeyboard(ImGui.GetIO(), kb);
                }

                if (Keyboard.current != keyboard) { // keyboard device changed, setup again
                    SetupKeyboard(ImGui.GetIO(), Keyboard.current);
                }
            }
        }
    }
}
#endif
