# DearImGui-Unity

This repository is a Unity wrapper of the immediate mode GUI library, Dear ImGui (https://github.com/ocornut/imgui).

This repository is based on [dear-imgui-unity](https://github.com/realgamessoftware/dear-imgui-unity), the main differences are:

1. This repository follows the Dear ImGui version (should keep updating).
2. Readapts the interface and flow.
3. To avoid references to Unity in the wrapper, system vectors are used uniformly in ImGuiNET.

Current Dear ImGui version is **1.90.0**.

## Usage

1. Download repository code.
2. Take out folder `ImGuiUnity` to your project. If your project doesn't have `System.Runtime.CompilerServices.Unsafe`, you can find it in `Assets/Scripts/Plugins`.
3. When using Universal Render Pipeline, add a `Render Im Gui Feature` render feature to the renderer asset. Assign it to the `render feature` field of the DearImGui component.
4. Subscribe to the `ImGuiUnity.Layout` event and use ImGui functions.
5. Example script:
   ```cs
   using ImGuiNET;
   using ImGuiNET.Unity;
   using UnityEngine;
   
   public class ImguiTest : MonoBehaviour {
       private void OnEnable() {
           ImGuiUnity.Layout += OnLayout;
       }
    
       private void OnDisable() {
           ImGuiUnity.Layout -= OnLayout;
       }
       
       private void OnLayout() {
           ImGui.ShowDemoWindow();
       }
   }
   ```

## See Also

This package uses Dear ImGui C bindings by [cimgui](https://github.com/cimgui/cimgui) and the C# wrapper by [ImGui.NET](https://github.com/mellinoe/ImGui.NET).

## TODO

1. **[Done]** Fix and clean up the current version (1.75.0) of the code.
2. **[Done]** Upgrade Dear ImGui version to 1.90.0, readapts the interface and flow.
3. Extraction an UPM package, update README.
4. Android, iOS support.
5. More keyboard event confirm.
6. Complete HDRP test.