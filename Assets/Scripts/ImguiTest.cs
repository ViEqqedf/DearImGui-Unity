using System;
using System.Collections;
using System.Collections.Generic;
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