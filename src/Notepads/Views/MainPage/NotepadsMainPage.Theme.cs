// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using Windows.UI;
    using Windows.UI.ViewManagement;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Notepads.Extensions;
    using Notepads.Services;

    public sealed partial class NotepadsMainPage
    {
        private void InitializeThemeSettings()
        {
            ThemeSettingsService.SetRequestedTheme(RootGrid, App.MainWindow.Content, App.MainWindow.AppWindow.TitleBar);
            ThemeSettingsService.OnBackgroundChanged += ThemeSettingsService_OnBackgroundChanged;
            ThemeSettingsService.OnThemeChanged += ThemeSettingsService_OnThemeChanged;
            ThemeSettingsService.OnAccentColorChanged += ThemeSettingsService_OnAccentColorChanged;
        }

        private async void ThemeSettingsService_OnAccentColorChanged(object sender, Color color)
        {
            await Dispatcher.CallOnUIThreadAsync(ThemeSettingsService.SetRequestedAccentColor);
        }

        private async void ThemeSettingsService_OnThemeChanged(object sender, ElementTheme theme)
        {
            await Dispatcher.CallOnUIThreadAsync(() =>
            {
                ThemeSettingsService.SetRequestedTheme(RootGrid, App.MainWindow.Content, App.MainWindow.AppWindow.TitleBar);
            });
        }

        private async void ThemeSettingsService_OnBackgroundChanged(object sender, Brush backgroundBrush)
        {
            await Dispatcher.CallOnUIThreadAsync(() =>
            {
                RootGrid.Background = backgroundBrush;
            });
        }
    }
}