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
        [STAThread]
        private static void Main()
        {
            NativeMethods.SetProcessDPIAwareSafe();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (ClickLightApplicationContext context = new ClickLightApplicationContext())
            {
                Application.Run(context);
            }
        }
    }
}
