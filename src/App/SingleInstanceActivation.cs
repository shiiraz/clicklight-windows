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

namespace ClickLight.Windows
{
    internal static class SingleInstanceActivation
    {
        public const string MutexName = "Local\\ClickLight.Windows.SingleInstance";
        private const string ActivationMessageName = "ClickLight.Windows.ActivateSettings";
        private static int activationMessage;

        public static void SignalExistingInstance()
        {
            try
            {
                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, ActivationMessage, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }
        }

        public static int ActivationMessage
        {
            get
            {
                if (activationMessage == 0)
                {
                    activationMessage = NativeMethods.RegisterWindowMessage(ActivationMessageName);
                }
                return activationMessage;
            }
        }
    }
}
