// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.Dialog
{
    using Microsoft.UI;
    using Windows.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Notepads.Services;
    using CommunityToolkit.WinUI.Helpers;

    public class NotepadsDialog : ContentDialog
    {
        public bool IsAborted = false;

        private readonly SolidColorBrush _darkModeBackgroundBrush = new SolidColorBrush("#101010".ToColor());
        private readonly SolidColorBrush _lightModeBackgroundBrush = new SolidColorBrush(Colors.White);

        public NotepadsDialog()
        {
            RequestedTheme = ThemeSettingsService.ThemeMode;
            Background = ThemeSettingsService.ThemeMode == ElementTheme.Dark
                ? _darkModeBackgroundBrush
                : _lightModeBackgroundBrush;

            ActualThemeChanged += NotepadsDialog_ActualThemeChanged;
        }

        private void NotepadsDialog_ActualThemeChanged(FrameworkElement sender, object args)
        {
            Background = ActualTheme == ElementTheme.Dark
                ? _darkModeBackgroundBrush
                : _lightModeBackgroundBrush;
        }

        internal readonly ResourceLoader ResourceLoader = new ResourceLoader();

        internal static Style GetButtonStyle(Color backgroundColor)
        {
            var buttonStyle = new Microsoft.UI.Xaml.Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty, backgroundColor));
            buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty, Colors.White));
            return buttonStyle;
        }
    }
}