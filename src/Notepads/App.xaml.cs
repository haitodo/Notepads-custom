// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads
{
    using Microsoft.UI;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.WinUI.Helpers;
    using Notepads.Services;
    using Notepads.Settings;
    using Notepads.Utilities;
    using Windows.ApplicationModel;
    using Windows.ApplicationModel.Activation;
    using Windows.ApplicationModel.Core;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.ApplicationModel.Resources.Core;
    using Windows.UI;
    using Windows.UI.ViewManagement;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;

    public sealed partial class App : Application
    {
        public static string ApplicationName = "Notepads";

        public static Guid InstanceId { get; } = Guid.NewGuid();

        public static Window MainWindow { get; private set; }
        public static bool IsPrimaryInstance = false;
        public static bool IsGameBarWidget = false;

        public static Mutex InstanceHandlerMutex { get; set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedException;

            InstanceHandlerMutex = new Mutex(true, App.ApplicationName, out bool isNew);
            if (isNew)
            {
                IsPrimaryInstance = true;
                ApplicationSettingsStore.Write(SettingsKey.ActiveInstanceIdStr, null);
            }
            else
            {
                InstanceHandlerMutex.Close();
            }

            LoggingService.LogInfo($"[{nameof(App)}] Started: Instance = {InstanceId} IsPrimaryInstance: {IsPrimaryInstance} IsGameBarWidget: {IsGameBarWidget}.");

            ApplicationSettingsStore.Write(SettingsKey.ActiveInstanceIdStr, App.InstanceId.ToString());

            InitializeComponent();
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs e)
        {
            var appArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            await ActivateAsync(appArgs.Data as IActivatedEventArgs);
        }

        private async Task ActivateAsync(IActivatedEventArgs e)
        {
            bool rootFrameCreated = false;

            if (MainWindow == null)
            {
                MainWindow = new Window();
                MainWindow.Title = ApplicationName;
            }

            if (!(MainWindow.Content is Frame rootFrame))
            {
                rootFrame = CreateRootFrame(e);
                MainWindow.Content = rootFrame;
                rootFrameCreated = true;

                ThemeSettingsService.Initialize();
                AppSettingsService.Initialize();
            }

            var appLaunchSettings = new Dictionary<string, string>()
            {
                { "OSArchitecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() },
                { "OSVersion", $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}.{Environment.OSVersion.Version.Build}" },
                { "UseWindowsTheme", ThemeSettingsService.UseWindowsTheme.ToString() },
                { "ThemeMode", ThemeSettingsService.ThemeMode.ToString() },
                { "UseWindowsAccentColor", ThemeSettingsService.UseWindowsAccentColor.ToString() },
                { "AppBackgroundTintOpacity", $"{(int) (ThemeSettingsService.AppBackgroundPanelTintOpacity * 10.0) * 10}" },
                { "ShowStatusBar", AppSettingsService.ShowStatusBar.ToString() },
                { "IsSessionSnapshotEnabled", AppSettingsService.IsSessionSnapshotEnabled.ToString() },
                { "IsShadowWindow", (!IsPrimaryInstance && !IsGameBarWidget).ToString() },
                { "IsGameBarWidget", IsGameBarWidget.ToString() },
                { "AlwaysOpenNewWindow", AppSettingsService.AlwaysOpenNewWindow.ToString() },
                { "IsHighlightMisspelledWordsEnabled", AppSettingsService.IsHighlightMisspelledWordsEnabled.ToString() },
                { "IsSmartCopyEnabled", AppSettingsService.IsSmartCopyEnabled.ToString() },
                { "ExitWhenLastTabClosed", AppSettingsService.ExitWhenLastTabClosed.ToString() },
            };

            LoggingService.LogInfo($"[{nameof(App)}] Launch settings: \n{string.Join("\n", appLaunchSettings.Select(x => x.Key + "=" + x.Value).ToArray())}.");
            AnalyticsService.TrackEvent("AppLaunch_Settings", appLaunchSettings);

            var appLaunchEditorSettings = new Dictionary<string, string>()
            {
                { "EditorDefaultLineEnding", AppSettingsService.EditorDefaultLineEnding.ToString() },
                { "EditorDefaultEncoding", EncodingUtility.GetEncodingName(AppSettingsService.EditorDefaultEncoding) },
                { "EditorDefaultTabIndents", AppSettingsService.EditorDefaultTabIndents.ToString() },
                { "EditorDefaultDecoding", AppSettingsService.EditorDefaultDecoding == null ? "Auto" : EncodingUtility.GetEncodingName(AppSettingsService.EditorDefaultDecoding) },
                { "EditorFontFamily", AppSettingsService.EditorFontFamily },
                { "EditorFontSize", AppSettingsService.EditorFontSize.ToString() },
                { "EditorFontStyle", AppSettingsService.EditorFontStyle.ToString() },
                { "EditorFontWeight", AppSettingsService.EditorFontWeight.Weight.ToString() },
                { "EditorDefaultSearchEngine", AppSettingsService.EditorDefaultSearchEngine.ToString() },
                { "DisplayLineHighlighter", AppSettingsService.EditorDisplayLineHighlighter.ToString() },
                { "DisplayLineNumbers", AppSettingsService.EditorDisplayLineNumbers.ToString() },
            };

            LoggingService.LogInfo($"[{nameof(App)}] Editor settings: \n{string.Join("\n", appLaunchEditorSettings.Select(x => x.Key + "=" + x.Value).ToArray())}.");
            AnalyticsService.TrackEvent("AppLaunch_Editor_Settings", appLaunchEditorSettings);

            try
            {
                await ActivationService.ActivateAsync(rootFrame, e);
            }
            catch (Exception ex)
            {
                var diagnosticInfo = new Dictionary<string, string>()
                {
                    { "Message", ex?.Message },
                    { "Exception", ex?.ToString() },
                };
                AnalyticsService.TrackEvent("AppFailedToActivate", diagnosticInfo);
                AnalyticsService.TrackError(ex, diagnosticInfo);
                throw;
            }



            if (rootFrameCreated)
            {
                ExtendViewIntoTitleBar();
                MainWindow.Activate();
            }
        }

        private Frame CreateRootFrame(IActivatedEventArgs e)
        {
            Frame rootFrame = new Frame();

            if (System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft)
            {
                rootFrame.FlowDirection = FlowDirection.RightToLeft;
            }
            else
            {
                rootFrame.FlowDirection = FlowDirection.LeftToRight;
            }
            rootFrame.NavigationFailed += OnNavigationFailed;

            if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
            {
                // TODO: Load state from previously suspended application
            }

            return rootFrame;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            var exception = new Exception($"[{nameof(App)}] Failed to load Page: {e.SourcePageType.FullName} Exception: {e.Exception.Message}");
            LoggingService.LogException(exception);
            AnalyticsService.TrackEvent("FailedToLoadPage", new Dictionary<string, string>()
            {
                { "Page", e.SourcePageType.FullName },
                { "Exception", e.Exception.Message }
            });
            throw exception;
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="args">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs args)
        {
            var deferral = args.SuspendingOperation.GetDeferral();

            try
            {
                // Here we flush the Clipboard again to make sure content in clipboard to remain available
                // after the application shuts down.
                Clipboard.Flush();
            }
            catch (Exception)
            {
                // Best efforts
            }
            finally
            {
                deferral.Complete();
            }
        }

        // Occurs when an exception is not handled on the UI thread.
        private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                var appDataPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Notepads");
                if (!System.IO.Directory.Exists(appDataPath)) System.IO.Directory.CreateDirectory(appDataPath);
                var crashLogPath = System.IO.Path.Combine(appDataPath, "crash.txt");
                System.IO.File.WriteAllText(crashLogPath, $"[{DateTime.UtcNow}] OnUnhandledException:\r\nMessage: {e.Message}\r\nException: {e.Exception}\r\nStack Trace:\r\n{e.Exception?.StackTrace}");
            }
            catch { }

            LoggingService.LogError($"[{nameof(App)}] OnUnhandledException: {e.Exception}");

            var diagnosticInfo = new Dictionary<string, string>()
            {
                { "Message", e.Message },
                { "Exception", e.Exception?.ToString() },
                { "Culture", System.Globalization.CultureInfo.CurrentCulture.EnglishName },
                { "AvailableMemory", "0" },
                { "FirstUseTimeUTC", DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss") },
                { "OSArchitecture", System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() },
                { "OSVersion", Environment.OSVersion.Version.ToString() },
                { "IsShadowWindow", (!IsPrimaryInstance && !IsGameBarWidget).ToString() },
                { "IsGameBarWidget", IsGameBarWidget.ToString() }
            };

            AnalyticsService.TrackEvent("OnUnhandledException", diagnosticInfo);
            AnalyticsService.TrackError(e.Exception, diagnosticInfo);

            // suppress and handle it manually.
            e.Handled = false;
        }

        // Occurs when an exception is not handled on a background thread.
        // ie. A task is fired and forgotten Task.Run(() => {...})
        private static void OnUnobservedException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LoggingService.LogError($"[{nameof(App)}] OnUnobservedException: {e.Exception}");

            var diagnosticInfo = new Dictionary<string, string>()
            {
                { "Message", e.Exception?.Message },
                { "Exception", e.Exception?.ToString() },
                { "InnerException", e.Exception?.InnerException?.ToString() },
                { "InnerExceptionMessage", e.Exception?.InnerException?.Message }
            };

            AnalyticsService.TrackEvent("OnUnobservedException", diagnosticInfo);
            AnalyticsService.TrackError(e.Exception, diagnosticInfo);

            // suppress and handle it manually.
            e.SetObserved();
        }

        private static void ExtendViewIntoTitleBar()
        {
            if (!IsGameBarWidget && MainWindow != null)
            {
                MainWindow.ExtendsContentIntoTitleBar = true;
            }
        }

        //private static void UpdateAppVersion()
        //{
        //    var packageVer = Package.Current.Id.Version;
        //    string oldVer = ApplicationSettingsStore.Read(SettingsKey.AppVersionStr) as string ?? "";
        //    string currentVer = $"{packageVer.Major}.{packageVer.Minor}.{packageVer.Build}.{packageVer.Revision}";

        //    if (currentVer != oldVer)
        //    {
        //        JumpListService.IsJumpListOutOfDate = true;
        //        ApplicationSettingsStore.Write(SettingsKey.AppVersionStr, currentVer);
        //    }
        //}

        //private static async Task UpdateJumpListAsync()
        //{
        //    if (JumpListService.IsJumpListOutOfDate)
        //    {
        //        if (await JumpListService.UpdateJumpListAsync())
        //        {
        //            JumpListService.IsJumpListOutOfDate = false;
        //        }
        //    }
        //}
    }
}