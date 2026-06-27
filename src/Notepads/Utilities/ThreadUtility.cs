// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.UI.Dispatching;

    internal static class ThreadUtility
    {
        public static bool IsOnUIThread()
        {
            var queue = Notepads.App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            return queue != null && queue.HasThreadAccess;
        }

        public static async Task RunOnUIThreadAsync(Action action)
        {
            var queue = Notepads.App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (queue == null)
            {
                action();
                return;
            }

            if (queue.HasThreadAccess)
            {
                action();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            queue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;
        }

        public static async Task<T> RunOnUIThreadAsync<T>(Func<T> func)
        {
            var queue = Notepads.App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (queue == null)
            {
                return func();
            }

            if (queue.HasThreadAccess)
            {
                return func();
            }

            var tcs = new TaskCompletionSource<T>();
            queue.TryEnqueue(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }

        public static async Task RunOnUIThreadAsync(Func<Task> function)
        {
            var queue = Notepads.App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (queue == null)
            {
                await function();
                return;
            }

            if (queue.HasThreadAccess)
            {
                await function();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();
            queue.TryEnqueue(async () =>
            {
                try
                {
                    await function();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;
        }

        public static async Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> function)
        {
            var queue = Notepads.App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (queue == null)
            {
                return await function();
            }

            if (queue.HasThreadAccess)
            {
                return await function();
            }

            var tcs = new TaskCompletionSource<T>();
            queue.TryEnqueue(async () =>
            {
                try
                {
                    tcs.SetResult(await function());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }
    }
}