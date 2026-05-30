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
internal static class Program
    {
        private const string SingleInstanceMutexName = "Local\\ClickLight.Windows.SingleInstance";
        private const string ActivationEventName = "Local\\ClickLight.Windows.ActivateSettings";

        [STAThread]
        private static void Main()
        {
            bool ownsMutex;
            using (System.Threading.EventWaitHandle activationEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, ActivationEventName))
            using (System.Threading.Mutex singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    activationEvent.Set();
                    return;
                }

                NativeMethods.SetProcessDPIAwareSafe();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (ClickLightApplicationContext context = new ClickLightApplicationContext(activationEvent))
                {
                    Application.Run(context);
                }
            }
        }
    }
}
