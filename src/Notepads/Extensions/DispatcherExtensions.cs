// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Windows.UI.Core;

    using Microsoft.UI.Dispatching;

    public static class DispatcherExtensions
    {
        public static async Task CallOnUIThreadAsync(this CoreDispatcher dispatcher, DispatchedHandler handler)
        {
            if (dispatcher != null)
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, handler);
            }
            else
            {
                var queue = Notepads.App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
                if (queue != null)
                {
                    if (queue.HasThreadAccess)
                    {
                        handler();
                        return;
                    }
                    var tcs = new TaskCompletionSource<bool>();
                    queue.TryEnqueue(() =>
                    {
                        try
                        {
                            handler();
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    });
                    await tcs.Task;
                }
                else
                {
                    handler();
                }
            }
        }
    }
}