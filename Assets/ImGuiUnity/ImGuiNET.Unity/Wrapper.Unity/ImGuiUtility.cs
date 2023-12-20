using UnityEngine;

namespace ImGuiNET {
    public static partial class ImGuiUnity {
        public static System.Numerics.Vector2 CreateSysVec2(float x, float y) {
            return new System.Numerics.Vector2(x, y);
        }

        public static System.Numerics.Vector2 CreateSysVec2(Vector2 vec) {
            return new System.Numerics.Vector2(vec.x, vec.y);
        }

        public static Vector2 CreateUnityVec2(System.Numerics.Vector2 vec) {
            return new Vector2(vec.X, vec.Y);
        }

        public static System.Numerics.Vector4 CreateSysVec4(Vector4 vec) {
            return new System.Numerics.Vector4(vec.x, vec.y, vec.z, vec.w);
        }

        public static Vector4 CreateUnityVec4(System.Numerics.Vector4 vec) {
            return new Vector4(vec.X, vec.Y, vec.Z, vec.W);
        }
    }
}