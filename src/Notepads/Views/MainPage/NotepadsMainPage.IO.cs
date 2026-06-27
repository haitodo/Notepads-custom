// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Windows.Graphics.Printing;
    using Windows.Storage;
    using Notepads.Controls.Dialog;
    using Notepads.Controls.FilePicker;
    using Notepads.Controls.Print;
    using Notepads.Controls.TextEditor;
    using Notepads.Services;
    using Notepads.Utilities;

    public sealed partial class NotepadsMainPage
    {
        private async Task OpenNewFilesAsync()
        {
            IReadOnlyList<StorageFile> files;

            try
            {
                files = await FilePickerFactory.GetFileOpenPicker().PickMultipleFilesAsync();
            }
            catch (Exception ex)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    var fileOpenErrorDialog = new FileOpenErrorDialog(filePath: null, ex.Message);
                    await DialogManager.OpenDialogAsync(fileOpenErrorDialog, awaitPreviousDialog: false);
                    if (!fileOpenErrorDialog.IsAborted)
                    {
                        NotepadsCore.FocusOnSelectedTextEditor();
                    }
                });
                return;
            }

            if (files == null || files.Count == 0)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    NotepadsCore.FocusOnSelectedTextEditor();
                });
                return;
            }

            foreach (var file in files)
            {
                await OpenFileAsync(file);
            }
        }

        public async Task<bool> OpenFileAsync(StorageFile file, bool rebuildOpenRecentItems = true)
        {
            if (file == null) return false;

            var tcs = new TaskCompletionSource<bool>();
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var openedEditor = NotepadsCore.GetTextEditor(file);
                    if (openedEditor != null)
                    {
                        NotepadsCore.SwitchTo(openedEditor);
                        NotificationCenter.Instance.PostNotification(
                            _resourceLoader.GetString("TextEditor_NotificationMsg_FileAlreadyOpened"), 2500);
                        tcs.SetResult(false);
                        return;
                    }

                    var editor = await NotepadsCore.CreateTextEditorAsync(Guid.NewGuid(), file);
                    NotepadsCore.OpenTextEditor(editor);
                    NotepadsCore.FocusOnSelectedTextEditor();
                    var success = MRUService.TryAdd(file); // Remember recently used files
                    if (success && rebuildOpenRecentItems)
                    {
                        await BuildOpenRecentButtonSubItemsAsync();
                    }

                    TrackFileExtensionIfNotSupported(file);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    var fileOpenErrorDialog = new FileOpenErrorDialog(file.Path, ex.Message);
                    await DialogManager.OpenDialogAsync(fileOpenErrorDialog, awaitPreviousDialog: false);
                    if (!fileOpenErrorDialog.IsAborted)
                    {
                        NotepadsCore.FocusOnSelectedTextEditor();
                    }
                    tcs.SetResult(false);
                }
            });
            return await tcs.Task;
        }

        // Here we track the file extension opened by user but not supported by Notepads.
        // This information will be used to on-board new extension support for future release.
        // Because UWP does not allow user to associate arbitrary file extension with the app.
        // File name will not and should not be tracked.
        private void TrackFileExtensionIfNotSupported(StorageFile file)
        {
            try
            {
                var extension = FileTypeUtility.GetFileExtension(file.Name).ToLower();
                if (!FileExtensionProvider.AllSupportedFileExtensions.Contains(extension))
                {
                    if (string.IsNullOrEmpty(extension))
                    {
                        extension = "<NoExtension>";
                    }
                    AnalyticsService.TrackEvent("UnsupportedFileExtension", new Dictionary<string, string>()
                    {
                        { "Extension", extension },
                    });
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public async Task<int> OpenFilesAsync(IReadOnlyList<IStorageItem> storageItems)
        {
            if (storageItems == null || storageItems.Count == 0) return 0;
            int successCount = 0;
            foreach (var storageItem in storageItems)
            {
                if (storageItem is StorageFile file)
                {
                    if (await OpenFileAsync(file, rebuildOpenRecentItems: false))
                    {
                        successCount++;
                    }
                }
            }
            if (successCount > 0)
            {
                await BuildOpenRecentButtonSubItemsAsync();
            }
            return successCount;
        }

        private async Task<StorageFile> OpenFileUsingFileSavePickerAsync(ITextEditor textEditor)
        {
            // WinUI 3 Desktop では PickSaveFileAsync() は UI スレッドから呼ぶ必要がある
            var tcs = new TaskCompletionSource<StorageFile>();
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    NotepadsCore.SwitchTo(textEditor);
                    StorageFile file = await FilePickerFactory.GetFileSavePicker(textEditor).PickSaveFileAsync();
                    NotepadsCore.FocusOnTextEditor(textEditor);
                    tcs.SetResult(file);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }

        private async Task SaveInternalAsync(ITextEditor textEditor, StorageFile file, bool rebuildOpenRecentItems)
        {
            await NotepadsCore.SaveContentToFileAndUpdateEditorStateAsync(textEditor, file);
            
            var tcs = new TaskCompletionSource<bool>();
            App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var success = MRUService.TryAdd(file); // Remember recently used files
                    if (success && rebuildOpenRecentItems)
                    {
                        await BuildOpenRecentButtonSubItemsAsync();
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;
        }

        private async Task<bool> SaveAsync(ITextEditor textEditor, bool saveAs, bool ignoreUnmodifiedDocument = false, bool rebuildOpenRecentItems = true)
        {
            if (textEditor == null) return false;

            if (ignoreUnmodifiedDocument && !textEditor.IsModified)
            {
                return true;
            }

            StorageFile file = null;

            try
            {
                if (textEditor.EditingFile == null || saveAs)
                {
                    file = await OpenFileUsingFileSavePickerAsync(textEditor);
                    if (file == null) return false; // ユーザーがキャンセル
                }
                else
                {
                    file = textEditor.EditingFile;
                }

                bool promptSaveAs = false;
                try
                {
                    await SaveInternalAsync(textEditor, file, rebuildOpenRecentItems);
                }
                catch (UnauthorizedAccessException) // 読み取り専用ファイルを保存しようとした場合
                {
                    promptSaveAs = true;
                }
                catch (FileNotFoundException) // ファイルが見つからない、またはストレージメディアが除去された場合
                {
                    promptSaveAs = true;
                }

                if (promptSaveAs)
                {
                    file = await OpenFileUsingFileSavePickerAsync(textEditor);
                    if (file == null) return false; // ユーザーがキャンセル

                    await SaveInternalAsync(textEditor, file, rebuildOpenRecentItems);
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                // エラーダイアログを UI スレッドで表示（fire-and-forget）
                App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    var fileSaveErrorDialog = new FileSaveErrorDialog((file == null) ? string.Empty : file.Path, ex.Message);
                    await DialogManager.OpenDialogAsync(fileSaveErrorDialog, awaitPreviousDialog: false);
                    if (!fileSaveErrorDialog.IsAborted)
                    {
                        NotepadsCore.FocusOnSelectedTextEditor();
                    }
                });
                return false;
            }
        }

        private async Task<bool> SaveAllAsync(ITextEditor[] textEditors)
        {
            var success = false;

            foreach (var textEditor in textEditors)
            {
                if (await SaveAsync(textEditor, saveAs: false, ignoreUnmodifiedDocument: true, rebuildOpenRecentItems: false)) success = true;
            }

            if (success)
            {
                await BuildOpenRecentButtonSubItemsAsync();
            }

            return success;
        }

        private async Task RenameFileAsync(ITextEditor textEditor)
        {
            if (textEditor == null) return;

            if (textEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted) return;

            if (textEditor.EditingFile != null && FileSystemUtility.IsFileReadOnly(textEditor.EditingFile)) return;

            var fileRenameDialog = new FileRenameDialog(textEditor.EditingFileName ?? textEditor.FileNamePlaceholder,
                fileExists: textEditor.EditingFile != null,
                confirmedAction: async (newFilename) =>
                {
                    try
                    {
                        await textEditor.RenameAsync(newFilename);
                        NotepadsCore.FocusOnSelectedTextEditor();
                        NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileRenamed"), 1500);
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = ex.Message?.TrimEnd('\r', '\n');
                        NotificationCenter.Instance.PostNotification(errorMessage, 3500); // TODO: Use Content Dialog to display error message
                    }
                });

            await DialogManager.OpenDialogAsync(fileRenameDialog, awaitPreviousDialog: false);
        }

        public async Task PrintAsync(ITextEditor textEditor)
        {
            if (App.IsGameBarWidget) return;
            if (textEditor == null) return;
            await PrintAllAsync(new[] { textEditor });
        }

        public async Task PrintAllAsync(ITextEditor[] textEditors)
        {
            if (App.IsGameBarWidget) return;
            if (textEditors == null || textEditors.Length == 0) return;

            // Initialize print content
            PrintArgs.PreparePrintContent(textEditors);

            if (PrintManager.IsSupported() && HaveNonemptyTextEditor(textEditors))
            {
                // Show print UI
                await PrintArgs.ShowPrintUIAsync();
            }
            else if (!PrintManager.IsSupported())
            {
                // Printing is not supported on this device
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("Print_NotificationMsg_PrintNotSupported"), 1500);
            }
        }

        private static bool HaveNonemptyTextEditor(ITextEditor[] textEditors)
        {
            foreach (ITextEditor textEditor in textEditors)
            {
                if (string.IsNullOrEmpty(textEditor.GetText())) continue;
                return true;
            }
            return false;
        }
    }
}