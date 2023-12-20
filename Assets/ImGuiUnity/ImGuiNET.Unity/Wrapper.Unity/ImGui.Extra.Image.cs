using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using ImGuiNET.Unity;

namespace ImGuiNET.Unity {
    // ImGui extra functionality related with Images
    public static partial class ImGuiUnity {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Image(Texture tex) {
            ImGui.Image((IntPtr)GetTextureId(tex), CreateSysVec2(tex.width, tex.height));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Image(Texture tex, Vector2 size) {
            ImGui.Image((IntPtr)GetTextureId(tex), CreateSysVec2(size));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Image(Sprite sprite) {
            SpriteInfo info = GetSpriteInfo(sprite);
            ImGui.Image((IntPtr)GetTextureId(info.texture), CreateSysVec2(info.size), CreateSysVec2(info.uv0), CreateSysVec2(info.uv1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Image(Sprite sprite, Vector2 size) {
            SpriteInfo info = GetSpriteInfo(sprite);
            ImGui.Image((IntPtr)GetTextureId(info.texture), CreateSysVec2(size), CreateSysVec2(info.uv0), CreateSysVec2(info.uv1));
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ImageButton(string id, Texture tex) {
            ImGui.ImageButton(id, (IntPtr)GetTextureId(tex), CreateSysVec2(tex.width, tex.height));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ImageButton(string id, Texture tex, Vector2 size) {
            ImGui.ImageButton(id, (IntPtr)GetTextureId(tex), CreateSysVec2(size));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ImageButton(string id, Sprite sprite) {
            SpriteInfo info = GetSpriteInfo(sprite);
            ImGui.ImageButton(id, (IntPtr)GetTextureId(info.texture), CreateSysVec2(info.size), CreateSysVec2(info.uv0), CreateSysVec2(info.uv1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ImageButton(string id, Sprite sprite, Vector2 size) {
            SpriteInfo info = GetSpriteInfo(sprite);
            ImGui.ImageButton(id, (IntPtr)GetTextureId(info.texture), CreateSysVec2(size), CreateSysVec2(info.uv0), CreateSysVec2(info.uv1));
        }
    }
}