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
internal sealed class LaunchAtLoginController
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ClickLight";
        private const string StartupTaskId = "ClickLightStartup";

        public bool IsEnabled
        {
            get
            {
                if (IsPackaged())
                {
                    StartupTaskState state;
                    return TryGetPackagedStartupTaskState(out state) && IsEnabledState(state);
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    return key != null && key.GetValue(ValueName) != null;
                }
            }
        }

        public bool RequiresManualEnable
        {
            get
            {
                if (!IsPackaged())
                {
                    return false;
                }

                StartupTaskState state;
                return TryGetPackagedStartupTaskState(out state) &&
                    (state == StartupTaskState.DisabledByUser || state == StartupTaskState.DisabledByPolicy);
            }
        }

        public void RepairIfEnabled()
        {
            if (IsPackaged())
            {
                return;
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null || key.GetValue(ValueName) == null)
                {
                    return;
                }

                string current = key.GetValue(ValueName) as string;
                string expected = RegistryStartupCommand();
                if (!String.Equals(current, expected, StringComparison.Ordinal))
                {
                    key.SetValue(ValueName, expected);
                }
            }
        }

        public bool SetEnabled(bool enabled)
        {
            if (IsPackaged())
            {
                return TrySetPackagedStartupTask(enabled);
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) return false;
                if (enabled)
                {
                    key.SetValue(ValueName, RegistryStartupCommand());
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }

            return true;
        }

        internal static string BuildRegistryStartupCommand(string executablePath)
        {
            return Quote(executablePath);
        }

        private static bool IsPackaged()
        {
            return !String.IsNullOrEmpty(NativeMethods.GetCurrentApplicationUserModelIdSafe());
        }

        private static string RegistryStartupCommand()
        {
            return BuildRegistryStartupCommand(Application.ExecutablePath);
        }

        private static bool TrySetPackagedStartupTask(bool enabled)
        {
            StartupTaskState state;
            if (TryGetPackagedStartupTaskState(out state) &&
                enabled &&
                (state == StartupTaskState.DisabledByUser || state == StartupTaskState.DisabledByPolicy))
            {
                OpenStartupAppsSettings();
                return false;
            }

            object startupTask;
            if (!TryGetPackagedStartupTask(out startupTask))
            {
                return false;
            }

            try
            {
                if (enabled)
                {
                    object operation = startupTask.GetType().GetMethod("RequestEnableAsync").Invoke(startupTask, null);
                    object result = WaitForAsyncOperation(operation);
                    StartupTaskState resultState;
                    if (!TryParseStartupTaskState(result, out resultState) || !IsEnabledState(resultState))
                    {
                        if (resultState == StartupTaskState.DisabledByUser || resultState == StartupTaskState.DisabledByPolicy)
                        {
                            OpenStartupAppsSettings();
                        }
                        return false;
                    }
                }
                else
                {
                    startupTask.GetType().GetMethod("Disable").Invoke(startupTask, null);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPackagedStartupTaskState(out StartupTaskState state)
        {
            state = StartupTaskState.Unavailable;

            if (TryGetPackagedStartupTaskStateFromApi(out state))
            {
                return true;
            }

            return TryGetPackagedStartupTaskStateFromRegistry(out state);
        }

        private static bool TryGetPackagedStartupTaskStateFromApi(out StartupTaskState state)
        {
            state = StartupTaskState.Unavailable;

            object startupTask;
            if (!TryGetPackagedStartupTask(out startupTask))
            {
                return false;
            }

            try
            {
                object value = startupTask.GetType().GetProperty("State").GetValue(startupTask, null);
                return TryParseStartupTaskState(value, out state);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPackagedStartupTaskStateFromRegistry(out StartupTaskState state)
        {
            state = StartupTaskState.Unavailable;

            string appUserModelId = NativeMethods.GetCurrentApplicationUserModelIdSafe();
            string packageFamilyName = PackageFamilyName(appUserModelId);
            if (String.IsNullOrEmpty(packageFamilyName))
            {
                return false;
            }

            string keyPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData\" +
                packageFamilyName + "\\" + StartupTaskId;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object value = key.GetValue("State");
                    if (value == null)
                    {
                        return false;
                    }

                    state = (StartupTaskState)Convert.ToInt32(value);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetPackagedStartupTask(out object startupTask)
        {
            startupTask = null;

            if (String.IsNullOrEmpty(NativeMethods.GetCurrentApplicationUserModelIdSafe()))
            {
                return false;
            }

            try
            {
                Type startupTaskType = Type.GetType("Windows.ApplicationModel.StartupTask, Windows, ContentType=WindowsRuntime");
                if (startupTaskType == null)
                {
                    return false;
                }

                object operation = startupTaskType.GetMethod("GetAsync").Invoke(null, new object[] { StartupTaskId });
                startupTask = WaitForAsyncOperation(operation);
                return startupTask != null;
            }
            catch
            {
                return false;
            }
        }

        private static object WaitForAsyncOperation(object operation)
        {
            if (operation == null)
            {
                return null;
            }

            Type operationType = operation.GetType();
            DateTime deadline = DateTime.UtcNow.AddSeconds(5.0);
            while (true)
            {
                object status = operationType.GetProperty("Status").GetValue(operation, null);
                string statusName = status == null ? String.Empty : status.ToString();
                if (!String.Equals(statusName, "Started", StringComparison.Ordinal))
                {
                    break;
                }

                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("Startup task operation timed out.");
                }

                Application.DoEvents();
                System.Threading.Thread.Sleep(25);
            }

            return operationType.GetMethod("GetResults").Invoke(operation, null);
        }

        private static bool TryParseStartupTaskState(object value, out StartupTaskState state)
        {
            state = StartupTaskState.Unavailable;
            if (value == null)
            {
                return false;
            }

            try
            {
                state = (StartupTaskState)Convert.ToInt32(value);
                return true;
            }
            catch
            {
            }

            string name = value.ToString();
            if (String.Equals(name, "Disabled", StringComparison.Ordinal))
            {
                state = StartupTaskState.Disabled;
                return true;
            }
            if (String.Equals(name, "DisabledByUser", StringComparison.Ordinal))
            {
                state = StartupTaskState.DisabledByUser;
                return true;
            }
            if (String.Equals(name, "Enabled", StringComparison.Ordinal))
            {
                state = StartupTaskState.Enabled;
                return true;
            }
            if (String.Equals(name, "DisabledByPolicy", StringComparison.Ordinal))
            {
                state = StartupTaskState.DisabledByPolicy;
                return true;
            }
            if (String.Equals(name, "EnabledByPolicy", StringComparison.Ordinal))
            {
                state = StartupTaskState.EnabledByPolicy;
                return true;
            }

            return false;
        }

        private static bool IsEnabledState(StartupTaskState state)
        {
            return state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy;
        }

        private static string PackageFamilyName(string appUserModelId)
        {
            if (String.IsNullOrEmpty(appUserModelId))
            {
                return String.Empty;
            }

            int separator = appUserModelId.IndexOf('!');
            return separator > 0 ? appUserModelId.Substring(0, separator) : appUserModelId;
        }

        public static void OpenStartupAppsSettings()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("ms-settings:startupapps");
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch
            {
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private enum StartupTaskState
        {
            Unavailable = -1,
            Disabled = 0,
            DisabledByUser = 1,
            Enabled = 2,
            DisabledByPolicy = 3,
            EnabledByPolicy = 4
        }
    }
}
