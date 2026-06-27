// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Controls.FindAndReplace
{
    using Windows.System;
    using Windows.UI.Core;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;

    public sealed class FindAndReplaceTextBox : TextBox
    {
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            CoreVirtualKeyStates ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
            CoreVirtualKeyStates alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
            CoreVirtualKeyStates shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

            // By default, TextBox toggles case when user hit "Shift + F3"
            // This should be restricted
            if (!ctrl.HasFlag(CoreVirtualKeyStates.Down) &&
                !alt.HasFlag(CoreVirtualKeyStates.Down) &&
                shift.HasFlag(CoreVirtualKeyStates.Down)
                && e.Key == VirtualKey.F3)
            {
                return;
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }
    }
}