// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using Microsoft.UI;
    using Microsoft.UI.Xaml;
    using Notepads.Services;
    using Windows.Foundation;

    public sealed partial class NotepadsMainPage
    {
        private const int TitleBarReservedAreaDefaultWidth = 180;

        private const int TitleBarReservedAreaCompactOverlayWidth = 100;

        private bool _isAlwaysOnTop = false;
        private bool _isFullScreen = false;

        // Show hide ExitCompactOverlayButton and status bar based on current ViewMode
        // Reset TitleBarReservedArea accordingly
        private void WindowSizeChanged(object sender, Microsoft.UI.Xaml.WindowSizeChangedEventArgs e)
        {
            if (_isAlwaysOnTop)
            {
                if (ExitCompactOverlayButton.Visibility == Visibility.Collapsed)
                {
                    TitleBarReservedArea.Width = TitleBarReservedAreaCompactOverlayWidth;
                    ExitCompactOverlayButton.Visibility = Visibility.Visible;
                    MainMenuButton.Visibility = Visibility.Collapsed;
                    if (AppSettingsService.ShowStatusBar) ShowHideStatusBar(false);
                }
            }
            else // Default or FullScreen
            {
                if (ExitCompactOverlayButton.Visibility == Visibility.Visible)
                {
                    TitleBarReservedArea.Width = TitleBarReservedAreaDefaultWidth;
                    ExitCompactOverlayButton.Visibility = Visibility.Collapsed;
                    MainMenuButton.Visibility = Visibility.Visible;
                    if (AppSettingsService.ShowStatusBar) ShowHideStatusBar(true);
                }
            }
        }

        private void EnterExitCompactOverlayMode()
        {
            if (App.IsGameBarWidget || App.MainWindow == null) return;

            var appWindow = App.MainWindow.AppWindow;
            var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;

            if (presenter != null)
            {
                if (!_isAlwaysOnTop)
                {
                    presenter.IsAlwaysOnTop = true;
                    _isAlwaysOnTop = true;
                    appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 360, Height = 500 });
                }
                else
                {
                    presenter.IsAlwaysOnTop = false;
                    _isAlwaysOnTop = false;
                    appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1000, Height = 700 });
                }
                WindowSizeChanged(null, null);
            }
        }

        private void EnterExitFullScreenMode()
        {
            if (App.IsGameBarWidget || App.MainWindow == null) return;

            var appWindow = App.MainWindow.AppWindow;

            if (!_isFullScreen)
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                _isFullScreen = true;
                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] Entered full screen view mode.", consoleOnly: true);
                NotificationCenter.Instance.PostNotification(
                    _resourceLoader.GetString("TextEditor_NotificationMsg_ExitFullScreenHint"), 3000);
            }
            else
            {
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                _isFullScreen = false;
                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] Exited full screen view mode.", consoleOnly: true);
            }
        }

        private void ExitCompactOverlayButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isAlwaysOnTop)
            {
                EnterExitCompactOverlayMode();
            }
        }
    }
}