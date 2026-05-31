using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CursorCue.Windows
{
internal sealed class MouseHook : IDisposable
    {
        private readonly NativeMethods.LowLevelMouseProc hookProc;
        private readonly Action<ClickEvent> onEvent;
        private IntPtr hookHandle;
        private bool laserPointerEnabled;
        private int lastHookError;

        public MouseHook(Action<ClickEvent> onEvent)
        {
            this.onEvent = onEvent;
            hookProc = HookCallback;
        }

        public string StatusLabel
        {
            get
            {
                if (hookHandle != IntPtr.Zero) return "Hook active";
                return lastHookError != 0 ? "Hook failed" : "Stopped";
            }
        }

        public void Start(bool laserPointerEnabled)
        {
            this.laserPointerEnabled = laserPointerEnabled;
            if (hookHandle != IntPtr.Zero) return;

            IntPtr module = NativeMethods.GetModuleHandle(null);
            hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, hookProc, module, 0);
            lastHookError = hookHandle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        }

        public void Stop()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                NativeMethods.MSLLHOOKSTRUCT data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                ClickKind? kind = KindForMessage(message);
                if (kind.HasValue)
                {
                    onEvent(new ClickEvent(kind.Value, data.pt.X, data.pt.Y, Clock.NowSeconds()));
                }
            }

            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private ClickKind? KindForMessage(int message)
        {
            if (message == NativeMethods.WM_LBUTTONDOWN)
            {
                return ClickKind.LeftDown;
            }
            if (message == NativeMethods.WM_LBUTTONUP)
            {
                return ClickKind.LeftUp;
            }
            if (message == NativeMethods.WM_RBUTTONDOWN)
            {
                return ClickKind.RightDown;
            }
            if (message == NativeMethods.WM_RBUTTONUP)
            {
                return ClickKind.RightUp;
            }
            if (message == NativeMethods.WM_MOUSEMOVE)
            {
                if (IsAnyMouseButtonDown())
                {
                    return ClickKind.Drag;
                }
                if (laserPointerEnabled)
                {
                    return ClickKind.Move;
                }
            }
            return null;
        }

        private static bool IsAnyMouseButtonDown()
        {
            return IsKeyDown(NativeMethods.VK_LBUTTON) ||
                IsKeyDown(NativeMethods.VK_RBUTTON) ||
                IsKeyDown(NativeMethods.VK_MBUTTON) ||
                IsKeyDown(NativeMethods.VK_XBUTTON1) ||
                IsKeyDown(NativeMethods.VK_XBUTTON2);
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (NativeMethods.GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
        }
    }
}
