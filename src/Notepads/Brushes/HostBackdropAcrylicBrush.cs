// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Brushes
{
    using System;
    using Windows.UI;
    using Microsoft.UI.Xaml.Media;

    public sealed class HostBackdropAcrylicBrush : AcrylicBrush, IDisposable
    {
        public new double TintOpacity
        {
            get => base.TintOpacity;
            set => base.TintOpacity = value;
        }

        public Color LuminosityColor
        {
            get => TintColor;
            set => TintColor = value;
        }

        public Uri NoiseTextureUri { get; set; }

        public HostBackdropAcrylicBrush()
        {
        }

        public void Dispose()
        {
        }
    }
}