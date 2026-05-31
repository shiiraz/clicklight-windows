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
internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool ownsMutex;
            using (System.Threading.Mutex singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceActivation.MutexName, out ownsMutex))
            {
                if (!ownsMutex)
                {
                    SingleInstanceActivation.SignalExistingInstance();
                    return;
                }

                NativeMethods.SetProcessDPIAwareSafe();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (CursorCueApplicationContext context = new CursorCueApplicationContext())
                {
                    Application.Run(context);
                }
            }
        }
    }
}
