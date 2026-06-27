// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.Settings
{
    using Notepads.Extensions;
    using Notepads.Services;
    using System.Linq;
    using Windows.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;

    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            Unloaded += SettingsPage_Unloaded;

            if (App.IsGameBarWidget)
            {
                ThemeSettingsService.SetRequestedTheme(null, App.MainWindow.Content, null);
            }
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (App.IsGameBarWidget)
            {
                ThemeSettingsService.OnThemeChanged += ThemeSettingsService_OnThemeChanged;
                ThemeSettingsService.OnAccentColorChanged += ThemeSettingsService_OnAccentColorChanged;
            }
            var firstItem = (NavigationViewItem)SettingsNavigationView.MenuItems.First();
            firstItem.IsSelected = true;
            // WinUI3ではIsSelectedを真に設定してもItemInvokedイベントが発火しないため、初回ロード時に手動で初期画面を表示します。
            SettingsPanel.Show(firstItem.Content as string, firstItem.Tag as string);
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (App.IsGameBarWidget)
            {
                ThemeSettingsService.OnThemeChanged -= ThemeSettingsService_OnThemeChanged;
                ThemeSettingsService.OnAccentColorChanged -= ThemeSettingsService_OnAccentColorChanged;
            }
        }

        private async void ThemeSettingsService_OnAccentColorChanged(object sender, Color color)
        {
            await Dispatcher.CallOnUIThreadAsync(ThemeSettingsService.SetRequestedAccentColor);
        }

        private async void ThemeSettingsService_OnThemeChanged(object sender, ElementTheme theme)
        {
            await Dispatcher.CallOnUIThreadAsync(() =>
            {
                ThemeSettingsService.SetRequestedTheme(null, App.MainWindow.Content, null);
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            switch (e.Parameter)
            {
                case null:
                    return;
            }
        }

        private void SettingsPanel_OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            SettingsPanel.Show((args.InvokedItem as string), (args.InvokedItemContainer as NavigationViewItem)?.Tag as string);
        }
    }
}