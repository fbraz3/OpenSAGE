using System;
using System.Runtime.InteropServices;
using OpenSage.Graphics.BGFX.Native;
using Veldrid.Sdl2;

namespace OpenSage.Graphics.BGFX;

/// <summary>
/// Platform-specific initialization data for BGFX
/// Handles extraction of native window handles from SDL2 windows
/// </summary>
public static class BgfxPlatformData
{
    /// <summary>
    /// Get platform data for the current OS and SDL2 window
    /// </summary>
    public static BgfxNative.PlatformData GetPlatformData(Sdl2Window window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        var platformData = new BgfxNative.PlatformData();

        // Get native window handle from SDL2
        var nativeWindow = window.Handle;

        // Extract platform-specific window handle
        var osVersion = Environment.OSVersion;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: Get HWND from SDL2 window
            platformData.Nwh = GetWindowsHandle(nativeWindow);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: Get NSWindow* from SDL2 window
            platformData.Nwh = GetMacOSHandle(nativeWindow);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: Get X11 Window or Wayland Surface from SDL2
            platformData.Nwh = GetLinuxHandle(nativeWindow);
        }

        return platformData;
    }

    /// <summary>
    /// Get Windows HWND from SDL2 window
    /// </summary>
    private static IntPtr GetWindowsHandle(IntPtr sdlWindow)
    {
        // SDL_GetWindowWMInfo returns window manager specific information
        // For Windows, this includes the HWND
        // We need to call SDL_GetWindowWMInfo and extract the hwnd field

        // For now, use a simpler approach: get the window from the SDL info structure
        var wmInfo = new SDL_SysWMInfo();
        wmInfo.version.major = SDL_MAJOR_VERSION;
        wmInfo.version.minor = SDL_MINOR_VERSION;
        wmInfo.version.patch = SDL_PATCHLEVEL;

        if (SDL_GetWindowWMInfo(sdlWindow, ref wmInfo) != SDL_bool.SDL_TRUE)
        {
            throw new InvalidOperationException("Failed to get Windows window handle from SDL2");
        }

        return wmInfo.info.win.window;
    }

    /// <summary>
    /// Get macOS NSWindow* from SDL2 window
    /// </summary>
    private static IntPtr GetMacOSHandle(IntPtr sdlWindow)
    {
        // macOS: Get NSWindow from SDL_SysWMInfo
        var wmInfo = new SDL_SysWMInfo();
        wmInfo.version.major = SDL_MAJOR_VERSION;
        wmInfo.version.minor = SDL_MINOR_VERSION;
        wmInfo.version.patch = SDL_PATCHLEVEL;

        if (SDL_GetWindowWMInfo(sdlWindow, ref wmInfo) != SDL_bool.SDL_TRUE)
        {
            throw new InvalidOperationException("Failed to get macOS window handle from SDL2");
        }

        return wmInfo.info.cocoa.window;
    }

    /// <summary>
    /// Get X11 or Wayland window handle from SDL2 window
    /// </summary>
    private static IntPtr GetLinuxHandle(IntPtr sdlWindow)
    {
        // Linux: Get X11 Window or Wayland Surface from SDL_SysWMInfo
        var wmInfo = new SDL_SysWMInfo();
        wmInfo.version.major = SDL_MAJOR_VERSION;
        wmInfo.version.minor = SDL_MINOR_VERSION;
        wmInfo.version.patch = SDL_PATCHLEVEL;

        if (SDL_GetWindowWMInfo(sdlWindow, ref wmInfo) != SDL_bool.SDL_TRUE)
        {
            throw new InvalidOperationException("Failed to get Linux window handle from SDL2");
        }

        // Check if X11 or Wayland
        if (wmInfo.subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_X11)
        {
            return wmInfo.info.x11.window;
        }
        else if (wmInfo.subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_WAYLAND)
        {
            return wmInfo.info.wl.surface;
        }

        throw new InvalidOperationException("Unsupported window system on Linux");
    }

    // ============================================
    // SDL2 NATIVE INTEROP
    // ============================================

    private const string SDL2_LibName = "SDL2";
    private const int SDL_MAJOR_VERSION = 2;
    private const int SDL_MINOR_VERSION = 28;
    private const int SDL_PATCHLEVEL = 0;

    [DllImport(SDL2_LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern SDL_bool SDL_GetWindowWMInfo(IntPtr window, ref SDL_SysWMInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_version
    {
        public byte major;
        public byte minor;
        public byte patch;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct SDL_SysWMinfo_data
    {
        [FieldOffset(0)]
        public SDL_SysWMinfo_windows win;

        [FieldOffset(0)]
        public SDL_SysWMinfo_cocoa cocoa;

        [FieldOffset(0)]
        public SDL_SysWMinfo_x11 x11;

        [FieldOffset(0)]
        public SDL_SysWMinfo_wayland wl;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_SysWMinfo_windows
    {
        public IntPtr window;  // HWND
        public IntPtr hdc;     // HDC
        public IntPtr hinstance; // HINSTANCE
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_SysWMinfo_cocoa
    {
        public IntPtr window;  // NSWindow*
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_SysWMinfo_x11
    {
        public IntPtr display; // Display*
        public IntPtr window;  // Window
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_SysWMinfo_wayland
    {
        public IntPtr display; // struct wl_display*
        public IntPtr surface; // struct wl_surface*
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SDL_SysWMInfo
    {
        public SDL_version version;
        public SDL_SYSWM_TYPE subsystem;
        public SDL_SysWMinfo_data info;
    }

    private enum SDL_SYSWM_TYPE
    {
        SDL_SYSWM_UNKNOWN = 0,
        SDL_SYSWM_WINDOWS = 1,
        SDL_SYSWM_X11 = 2,
        SDL_SYSWM_DIRECTFB = 3,
        SDL_SYSWM_COCOA = 4,
        SDL_SYSWM_UIKIT = 5,
        SDL_SYSWM_WAYLAND = 6,
        SDL_SYSWM_ANDROID = 7,
        SDL_SYSWM_VIVANTE = 8,
        SDL_SYSWM_OS2 = 9,
        SDL_SYSWM_HAIKU = 10,
        SDL_SYSWM_KMSDRM = 11,
    }

    private enum SDL_bool
    {
        SDL_FALSE = 0,
        SDL_TRUE = 1,
    }
}
