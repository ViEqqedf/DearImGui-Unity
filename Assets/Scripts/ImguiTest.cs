using System;
using System.Collections;
using System.Collections.Generic;
using ImGuiNET;
using UnityEngine;

public class ImguiTest : MonoBehaviour {
    private void OnEnable() {
        ImGuiUn.Layout += OnLayout;
    }

    private void OnDisable() {
        ImGuiUn.Layout -= OnLayout;
    }

    private void OnLayout() {
        ImGui.ShowDemoWindow();
    }
}