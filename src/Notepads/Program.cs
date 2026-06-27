// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Notepads.Services;
    using Notepads.Settings;
    using Windows.ApplicationModel.Activation;
    using Microsoft.Windows.AppLifecycle;

    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                WinRT.ComWrappersSupport.InitializeComWrappers();
#if DEBUG
                Task.Run(LoggingService.InitializeFileSystemLoggingAsync);
#endif

                var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                IActivatedEventArgs activatedArgs = appArgs.Data as IActivatedEventArgs;

                if (activatedArgs is FileActivatedEventArgs)
                {
                    RedirectOrCreateNewInstance();
                }
                else if (activatedArgs is CommandLineActivatedEventArgs)
                {
                    RedirectOrCreateNewInstance();
                }
                else if (activatedArgs is ProtocolActivatedEventArgs protocolActivatedEventArgs)
                {
                    LoggingService.LogInfo($"[{nameof(Main)}] [ProtocolActivated] Protocol: {protocolActivatedEventArgs.Uri}");
                    var protocol = NotepadsProtocolService.GetOperationProtocol(protocolActivatedEventArgs.Uri, out _);
                    if (protocol == NotepadsOperationProtocol.OpenNewInstance)
                    {
                        OpenNewInstance();
                    }
                    else
                    {
                        RedirectOrCreateNewInstance();
                    }
                }
                else if (activatedArgs is LaunchActivatedEventArgs launchActivatedEventArgs)
                {
                    bool handled = false;

                    if (!string.IsNullOrEmpty(launchActivatedEventArgs.Arguments))
                    {
                        var protocol = NotepadsProtocolService.GetOperationProtocol(new Uri(launchActivatedEventArgs.Arguments), out _);
                        if (protocol == NotepadsOperationProtocol.OpenNewInstance)
                        {
                            handled = true;
                            OpenNewInstance();
                        }
                    }

                    if (!handled)
                    {
                        RedirectOrCreateNewInstance();
                    }
                }
                else
                {
                    RedirectOrCreateNewInstance();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var crashPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Notepads",
                        "crash_program.txt");
                    System.IO.File.WriteAllText(crashPath, ex.ToString());
                }
                catch
                {
                    // Ignore
                }
            }
        }

        private static void OpenNewInstance()
        {
            AppInstance.FindOrRegisterForKey(App.InstanceId.ToString());
            Microsoft.UI.Xaml.Application.Start(p => new App());
        }

        private static void RedirectOrCreateNewInstance()
        {
            var instance = (GetLastActiveInstance() ?? AppInstance.FindOrRegisterForKey(App.InstanceId.ToString()));

            if (instance.IsCurrent)
            {
                Microsoft.UI.Xaml.Application.Start(p => new App());
            }
            else
            {
                // open new instance if user prefers to
                if (ApplicationSettingsStore.Read(SettingsKey.AlwaysOpenNewWindowBool) is bool alwaysOpenNewWindowBool && alwaysOpenNewWindowBool)
                {
                    OpenNewInstance();
                }
                else
                {
                    var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                    instance.RedirectActivationToAsync(appArgs).GetAwaiter().GetResult();
                }
            }
        }

        private static bool IsInstanceProcessRunning(AppInstance instance)
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById((int)instance.ProcessId))
                {
                    return !process.HasExited;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AppInstance GetLastActiveInstance()
        {
            var currentProcessId = AppInstance.GetCurrent().ProcessId;
            var instances = AppInstance.GetInstances()
                .Where(i => i.ProcessId != currentProcessId && IsInstanceProcessRunning(i))
                .ToList();

            if (instances.Count == 0)
            {
                return null;
            }
            else if (instances.Count == 1)
            {
                return instances.FirstOrDefault();
            }

            if (!(ApplicationSettingsStore.Read(SettingsKey.ActiveInstanceIdStr) is string activeInstance))
            {
                return instances.FirstOrDefault();
            }

            foreach (var appInstance in instances)
            {
                if (appInstance.Key == activeInstance)
                {
                    return appInstance;
                }
            }

            // activeInstance might be closed already, let's return the first instance in this case
            return instances.FirstOrDefault();
        }
    }
}