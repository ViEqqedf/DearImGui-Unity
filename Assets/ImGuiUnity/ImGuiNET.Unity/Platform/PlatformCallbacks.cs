using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ImGuiNET.Unity {
    // TODO: should return Utf8 byte*, how to deal with memory ownership?
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate string GetClipboardTextCallback(void* user_data);
    public delegate string GetClipboardTextSafeCallback(IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void SetClipboardTextCallback(void* user_data, byte* text);
    public delegate void SetClipboardTextSafeCallback(IntPtr user_data, string text);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ImeSetInputScreenPosCallback(int x, int y);

#if IMGUI_FEATURE_CUSTOM_ASSERT
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void LogAssertCallback(byte* condition, byte* file, int line);
    public delegate void LogAssertSafeCallback(string condition, string file, int line);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DebugBreakCallback();

    public unsafe struct CustomAssertData {
        public IntPtr LogAssertFn;
        public IntPtr DebugBreakFn;
    }
#endif

    public unsafe class PlatformCallbacks {
        // fields to keep delegates from being collected by the garbage collector
        // after assigning its function pointers to unmanaged code
        private GetClipboardTextCallback getClipboardText;
        private SetClipboardTextCallback setClipboardText;
        private ImeSetInputScreenPosCallback imeSetInputScreenPos;
#if IMGUI_FEATURE_CUSTOM_ASSERT
        private LogAssertCallback logAssert;
        private DebugBreakCallback debugBreak;
#endif

        public void Assign(ImGuiIOPtr io) {
            io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(setClipboardText);
            io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(getClipboardText);
            io.SetPlatformImeDataFn = Marshal.GetFunctionPointerForDelegate(imeSetInputScreenPos);
#if IMGUI_FEATURE_CUSTOM_ASSERT
            io.SetBackendPlatformUserData<CustomAssertData>(new CustomAssertData {
                LogAssertFn = Marshal.GetFunctionPointerForDelegate(logAssert),
                DebugBreakFn = Marshal.GetFunctionPointerForDelegate(debugBreak),
            });
#endif
        }

        public void Unset(ImGuiIOPtr io) {
            io.SetClipboardTextFn = IntPtr.Zero;
            io.GetClipboardTextFn = IntPtr.Zero;
            io.SetPlatformImeDataFn = IntPtr.Zero;
#if IMGUI_FEATURE_CUSTOM_ASSERT
            io.SetBackendPlatformUserData<CustomAssertData>(null);
#endif
        }

        public GetClipboardTextSafeCallback GetClipboardText {
            set => getClipboardText = (user_data) => {
                // TODO: convert return string to Utf8 byte*
                try { return value(new IntPtr(user_data)); }
                catch (Exception ex) { Debug.LogException(ex); return null; }
            };
        }

        public SetClipboardTextSafeCallback SetClipboardText {
            set => setClipboardText = (user_data, text) => {
                try { value(new IntPtr(user_data), Util.StringFromPtr(text)); }
                catch (Exception ex) { Debug.LogException(ex); }
            };
        }

        public ImeSetInputScreenPosCallback ImeSetInputScreenPos {
            set => imeSetInputScreenPos = (x, y) => {
                try { value(x, y); }
                catch (Exception ex) { Debug.LogException(ex); }
            };
        }

#if IMGUI_FEATURE_CUSTOM_ASSERT
        public LogAssertSafeCallback LogAssert{
            set => logAssert = (condition, file, line) => {
                try { value(Util.StringFromPtr(condition), Util.StringFromPtr(file), line); }
                catch (Exception ex) { Debug.LogException(ex); }
            };
        }

        public DebugBreakCallback DebugBreak {
            set => debugBreak = () => {
                try { value(); }
                catch (Exception ex) { Debug.LogException(ex); }
            };
        }
#endif
    }
}