// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Views.MainPage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Notepads.Commands;
    using Notepads.Controls.Dialog;
    using Notepads.Controls.Print;
    using Notepads.Controls.TextEditor;
    using Notepads.Core;
    using Notepads.Extensions;
    using Notepads.Services;
    using Notepads.Settings;
    using Notepads.Utilities;
    using Notepads.Views.Settings;
    using Windows.ApplicationModel.Activation;
    using Windows.ApplicationModel.DataTransfer;
    using Windows.Storage;
    using Windows.System;
    using Windows.UI.ViewManagement;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media.Animation;
    using Microsoft.UI.Xaml.Navigation;
    using Windows.Graphics.Printing;

    public sealed partial class NotepadsMainPage : Page
    {
        private IReadOnlyList<IStorageItem> _appLaunchFiles;

        private string _appLaunchCmdDir;
        private string _appLaunchCmdArgs;
        private Uri _appLaunchUri;

        private readonly ResourceLoader _resourceLoader = new ResourceLoader();

        private bool _loaded = false;
        private bool _lastTabMovedToAnotherInstance = false;

        private INotepadsCore _notepadsCore;

        private INotepadsCore NotepadsCore
        {
            get
            {
                if (_notepadsCore != null) return _notepadsCore;

                _notepadsCore = new NotepadsCore(Sets, new NotepadsExtensionProvider(), Dispatcher);
                _notepadsCore.StorageItemsDropped += OnStorageItemsDropped;
                _notepadsCore.TextEditorLoaded += OnTextEditorLoaded;
                _notepadsCore.TextEditorUnloaded += OnTextEditorUnloaded;
                _notepadsCore.TextEditorKeyDown += OnTextEditorKeyDown;
                _notepadsCore.TextEditorClosing += OnTextEditorClosing;
                _notepadsCore.TextEditorSaved += OnTextEditorSaved;
                _notepadsCore.TextEditorMovedToAnotherAppInstance += OnTextEditorMovedToAnotherAppInstance;
                _notepadsCore.TextEditorRenamed += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) SetupStatusBar(editor); };
                _notepadsCore.TextEditorSelectionChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) UpdateLineColumnIndicator(editor); };
                _notepadsCore.TextEditorFontZoomFactorChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) UpdateFontZoomIndicator(editor); };
                _notepadsCore.TextEditorEncodingChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) UpdateEncodingIndicator(editor.GetEncoding()); };
                _notepadsCore.TextEditorLineEndingChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) { UpdateLineEndingIndicator(editor.GetLineEnding()); UpdateLineColumnIndicator(editor); } };
                _notepadsCore.TextEditorEditorModificationStateChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) SetupStatusBar(editor); };
                _notepadsCore.TextEditorFileModificationStateChanged += (sender, editor) => { if (NotepadsCore.GetSelectedTextEditor() == editor) OnTextEditorFileModificationStateChanged(editor); };

                return _notepadsCore;
            }
        }

        private ICommandHandler<KeyRoutedEventArgs> _keyboardCommandHandler;

        private const string XBoxGameBarSessionFilePrefix = "XBoxGameBar-";

        private ISessionManager _sessionManager;

        private ISessionManager SessionManager => _sessionManager ?? (_sessionManager = SessionUtility.GetSessionManager(NotepadsCore, App.IsGameBarWidget ? XBoxGameBarSessionFilePrefix : null));

        private readonly string _defaultNewFileName;

        public NotepadsMainPage()
        {
            InitializeComponent();

            _defaultNewFileName = _resourceLoader.GetString("TextEditor_DefaultNewFileName");

            this.Loaded += (sender, e) =>
            {
                // Set custom title bar dragging area after page is loaded and in visual tree
                App.MainWindow.SetTitleBar(AppTitleBar);
            };

            InitializeNotificationCenter();
            InitializeThemeSettings();
            InitializeStatusBar();
            InitializeControls();
            InitializeMainMenu();
            InitializeKeyboardShortcuts();

            // Session backup and restore toggle
            AppSettingsService.OnSessionBackupAndRestoreOptionChanged += OnSessionBackupAndRestoreOptionChanged;

            // Register for printing
            if (PrintManager.IsSupported())
            {
                PrintArgs.RegisterForPrinting(this);
            }

            // Register for content Sharing
            try
            {
                Windows.ApplicationModel.DataTransfer.DataTransferManager.GetForCurrentView().DataRequested += MainPage_DataRequested;
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"Failed to register for content Sharing: {ex.Message}");
            }

            if (App.MainWindow != null)
            {
                var appWindow = App.MainWindow.AppWindow;
                appWindow.Closing += AppWindow_Closing;
                appWindow.Changed += AppWindow_Changed;

                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    _lastIsMaximized = presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
                }

                _lastNormalWidth = ApplicationSettingsStore.Read(SettingsKey.WindowWidthInt) is int w ? w : appWindow.Size.Width;
                _lastNormalHeight = ApplicationSettingsStore.Read(SettingsKey.WindowHeightInt) is int h ? h : appWindow.Size.Height;
                _lastNormalX = ApplicationSettingsStore.Read(SettingsKey.WindowPositionXInt) is int x ? x : appWindow.Position.X;
                _lastNormalY = ApplicationSettingsStore.Read(SettingsKey.WindowPositionYInt) is int y ? y : appWindow.Position.Y;
            }

            if (App.IsGameBarWidget)
            {
                TitleBarReservedArea.Width = .0f;
            }
            else
            {
                if (App.MainWindow != null)
                {
                    App.MainWindow.SizeChanged += WindowSizeChanged;
                }
            }
        }

        private void InitializeControls()
        {
            ToolTipService.SetToolTip(ExitCompactOverlayButton, _resourceLoader.GetString("App_ExitCompactOverlayMode_Text"));
            RootSplitView.PaneOpening += delegate { SettingsFrame.Navigate(typeof(SettingsPage), null, new SuppressNavigationTransitionInfo()); };
            RootSplitView.PaneClosed += delegate { NotepadsCore.FocusOnSelectedTextEditor(); };
            NewSetButton.Click += delegate { NotepadsCore.OpenNewTextEditor(_defaultNewFileName); };
        }

        private void InitializeKeyboardShortcuts()
        {
            _keyboardCommandHandler = new KeyboardCommandHandler(new List<IKeyboardCommand<KeyRoutedEventArgs>>()
            {
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.W, (args) => NotepadsCore.CloseTextEditor(NotepadsCore.GetSelectedTextEditor())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Tab, (args) => NotepadsCore.SwitchTo(true)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.Tab, (args) => NotepadsCore.SwitchTo(false)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.N, (args) => NotepadsCore.OpenNewTextEditor(_defaultNewFileName)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.T, (args) => NotepadsCore.OpenNewTextEditor(_defaultNewFileName)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.O, async (args) => await OpenNewFilesAsync()),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.S, async (args) => await SaveAsync(NotepadsCore.GetSelectedTextEditor(), saveAs: false, ignoreUnmodifiedDocument: true)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.S, async (args) => await SaveAsync(NotepadsCore.GetSelectedTextEditor(), saveAs: true)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.P, async (args) => await PrintAsync(NotepadsCore.GetSelectedTextEditor())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.P, async (args) => await PrintAllAsync(NotepadsCore.GetAllTextEditors())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.R, (args) => ReloadFileFromDiskAsync(this, new RoutedEventArgs())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, true, VirtualKey.N, async (args) => await OpenNewAppInstanceAsync()),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number1, (args) => NotepadsCore.SwitchTo(0)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number2, (args) => NotepadsCore.SwitchTo(1)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number3, (args) => NotepadsCore.SwitchTo(2)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number4, (args) => NotepadsCore.SwitchTo(3)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number5, (args) => NotepadsCore.SwitchTo(4)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number6, (args) => NotepadsCore.SwitchTo(5)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number7, (args) => NotepadsCore.SwitchTo(6)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number8, (args) => NotepadsCore.SwitchTo(7)),
                new KeyboardCommand<KeyRoutedEventArgs>(true, false, false, VirtualKey.Number9, (args) => NotepadsCore.SwitchTo(8)),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F11, (args) => EnterExitFullScreenMode()),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F12, (args) => EnterExitCompactOverlayMode()),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.Escape, (args) => { if (RootSplitView.IsPaneOpen) RootSplitView.IsPaneOpen = false; }),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F1, (args) => { if (App.IsPrimaryInstance && !App.IsGameBarWidget) RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen; }),
                new KeyboardCommand<KeyRoutedEventArgs>(VirtualKey.F2, async (args) => await RenameFileAsync(NotepadsCore.GetSelectedTextEditor())),
                new KeyboardCommand<KeyRoutedEventArgs>(true, true, true, VirtualKey.L, async (args) => { await OpenFileAsync(LoggingService.GetLogFile(), rebuildOpenRecentItems: false); })
            });
        }

        private static async Task OpenNewAppInstanceAsync()
        {
            if (!await NotepadsProtocolService.LaunchProtocolAsync(NotepadsOperationProtocol.OpenNewInstance))
            {
                AnalyticsService.TrackEvent("FailedToOpenNewAppInstance");
            }
        }

        #region Application Life Cycle & Window management

        // Handles external links or cmd args activation before Sets loaded
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            switch (e.Parameter)
            {
                case null:
                    return;
                case FileActivatedEventArgs fileActivatedEventArgs:
                    _appLaunchFiles = fileActivatedEventArgs.Files;
                    break;
                case CommandLineActivatedEventArgs commandLineActivatedEventArgs:
                    _appLaunchCmdDir = commandLineActivatedEventArgs.Operation.CurrentDirectoryPath;
                    _appLaunchCmdArgs = commandLineActivatedEventArgs.Operation.Arguments;
                    break;
                case ProtocolActivatedEventArgs protocol:
                    _appLaunchUri = protocol.Uri;
                    break;
            }
        }

        // App should wait for Sets fully loaded before opening files requested by user (by click or from cmd)
        // Open files from external links or cmd args on Sets Loaded
        private async void Sets_Loaded(object sender, RoutedEventArgs e)
        {
            int loadedCount = 0;

            if (!_loaded && AppSettingsService.IsSessionSnapshotEnabled)
            {
                try
                {
                    loadedCount = await SessionManager.LoadLastSessionAsync();
                }
                catch (SessionDataCorruptedException ex)
                {
                    LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to load last session: {ex}");

                    // Last session data is corrupted, clear it first
                    await SessionManager.ClearSessionDataAsync();

                    // Recover backup files
                    int numberOfRecoveredFiles = await SessionManager.RecoverBackupFilesAsync();

                    LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] {numberOfRecoveredFiles} file(s) recovered from last session backup.");

                    AnalyticsService.TrackEvent("SessionManager_FailedToLoadLastSession_SessionDataCorruptedException",
                        new Dictionary<string, string>()
                        {
                            { "Exception", ex.Message },
                            { "NumberOfRecoveredFiles", numberOfRecoveredFiles.ToString() }
                        });

                    // Show session recovery dialog if there are any recovered files
                    if (numberOfRecoveredFiles > 0)
                    {
                        var sessionCorruptionErrorDialog = new SessionCorruptionErrorDialog(
                            recoveryAction: async () =>
                            {
                                await SessionManager.OpenSessionBackupFolderAsync();
                            });
                        await DialogManager.OpenDialogAsync(sessionCorruptionErrorDialog, awaitPreviousDialog: false);
                    }
                }
                catch (Exception ex) // Catch all other exceptions
                {
                    LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to load last session: {ex}");
                    AnalyticsService.TrackEvent("SessionManager_FailedToLoadLastSession_UnhandledException", new Dictionary<string, string>() { { "Exception", ex.Message } });
                }
            }

            // Ensure we resume on the UI thread
            await ThreadUtility.RunOnUIThreadAsync(() => {});

            if (_appLaunchFiles != null && _appLaunchFiles.Count > 0)
            {
                loadedCount += await OpenFilesAsync(_appLaunchFiles);
                _appLaunchFiles = null;
            }
            else if (_appLaunchCmdDir != null)
            {
                var file = await FileSystemUtility.OpenFileFromCommandLineAsync(_appLaunchCmdDir, _appLaunchCmdArgs);
                if (file != null && await OpenFileAsync(file))
                {
                    loadedCount++;
                }
                _appLaunchCmdDir = null;
                _appLaunchCmdArgs = null;
            }
            else if (_appLaunchUri != null)
            {
                var operation = NotepadsProtocolService.GetOperationProtocol(_appLaunchUri, out var context);
                if (operation == NotepadsOperationProtocol.OpenNewInstance || operation == NotepadsOperationProtocol.Unrecognized)
                {
                    // Do nothing
                }
                _appLaunchUri = null;
            }

            if (!_loaded)
            {
                if (loadedCount == 0)
                {
                    NotepadsCore.OpenNewTextEditor(_defaultNewFileName);
                }
                _loaded = true;
            }

            if (AppSettingsService.IsSessionSnapshotEnabled)
            {
                SessionManager.IsBackupEnabled = true;
                SessionManager.StartSessionBackup();
            }

            await BuildOpenRecentButtonSubItemsAsync();

            if (!App.IsGameBarWidget && App.MainWindow != null)
            {
                App.MainWindow.Activated -= MainWindow_Activated;
                App.MainWindow.Activated += MainWindow_Activated;

                App.MainWindow.Closed -= MainWindow_Closed;
                App.MainWindow.Closed += MainWindow_Closed;
            }
        }

        public void ExecuteProtocol(Uri uri)
        {
            LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] Executing protocol: {uri}", consoleOnly: true);
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (AppSettingsService.IsSessionSnapshotEnabled)
            {
                await SessionManager.SaveSessionAsync();
            }
        }

        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
            {
                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] MainWindow Deactivated.", consoleOnly: true);
                NotepadsCore.GetSelectedTextEditor()?.StopCheckingFileStatus();
                if (AppSettingsService.IsSessionSnapshotEnabled)
                {
                    SessionManager.StopSessionBackup();
                }
            }
            else if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated ||
                     args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.CodeActivated)
            {
                LoggingService.LogInfo($"[{nameof(NotepadsMainPage)}] MainWindow Activated.", consoleOnly: true);
                Task.Run(() => ApplicationSettingsStore.Write(SettingsKey.ActiveInstanceIdStr, App.InstanceId.ToString()));
                NotepadsCore.GetSelectedTextEditor()?.StartCheckingFileStatusPeriodically();
                if (AppSettingsService.IsSessionSnapshotEnabled)
                {
                    SessionManager.StartSessionBackup();
                }
            }
        }

        // Content sharing
        private void MainPage_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            var textEditor = NotepadsCore.GetSelectedTextEditor();
            if (textEditor == null) return;

            if (NotepadsCore.TryGetSharingContent(textEditor, out var title, out var content))
            {
                args.Request.Data.Properties.Title = title;
                args.Request.Data.SetText(content);
            }
            else
            {
                args.Request.FailWithDisplayText(_resourceLoader.GetString("ContentSharing_FailureDisplayText"));
            }
        }

        private bool _isClosingFromAppWindow = false;
        private bool _isClosingInProgress = false;

        private int _lastNormalWidth = 1000;
        private int _lastNormalHeight = 700;
        private int _lastNormalX = -1;
        private int _lastNormalY = -1;
        private bool _lastIsMaximized = false;

        private void CloseMainWindow()
        {
            SaveWindowPositionAndSize();
            if (App.MainWindow == null)
            {
                Application.Current.Exit();
                return;
            }

            try
            {
                bool enqueued = App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        App.MainWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to close App.MainWindow inside DispatcherQueue: {ex.Message}");
                        Application.Current.Exit();
                    }
                });

                if (!enqueued)
                {
                    LoggingService.LogWarning($"[{nameof(NotepadsMainPage)}] Failed to enqueue close command onto App.MainWindow.DispatcherQueue. Exiting application directly.");
                    Application.Current.Exit();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"[{nameof(NotepadsMainPage)}] Failed to enqueue close App.MainWindow: {ex.Message}");
                Application.Current.Exit();
            }
        }

        private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (sender.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                _lastIsMaximized = presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;

                if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Restored)
                {
                    _lastNormalWidth = sender.Size.Width;
                    _lastNormalHeight = sender.Size.Height;
                    _lastNormalX = sender.Position.X;
                    _lastNormalY = sender.Position.Y;
                }
            }
        }

        private void SaveWindowPositionAndSize()
        {
            try
            {
                ApplicationSettingsStore.Write(SettingsKey.WindowWidthInt, _lastNormalWidth);
                ApplicationSettingsStore.Write(SettingsKey.WindowHeightInt, _lastNormalHeight);
                ApplicationSettingsStore.Write(SettingsKey.WindowPositionXInt, _lastNormalX);
                ApplicationSettingsStore.Write(SettingsKey.WindowPositionYInt, _lastNormalY);
                ApplicationSettingsStore.Write(SettingsKey.WindowIsMaximizedBool, _lastIsMaximized);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save window position and size: {ex.Message}");
            }
        }

        private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
        {
            if (_isClosingFromAppWindow)
            {
                sender.Changed -= AppWindow_Changed;
                return;
            }

            e.Cancel = true;

            if (_isClosingInProgress)
            {
                return;
            }

            _isClosingInProgress = true;

            if (AppSettingsService.IsSessionSnapshotEnabled)
            {
                await SessionManager.SaveSessionAsync(() => { SessionManager.IsBackupEnabled = false; });
                App.InstanceHandlerMutex?.Dispose();
                _isClosingFromAppWindow = true;
                CloseMainWindow();
                return;
            }

            if (!NotepadsCore.HaveUnsavedTextEditor())
            {
                App.InstanceHandlerMutex?.Dispose();
                _isClosingFromAppWindow = true;
                CloseMainWindow();
                return;
            }

            HideAllOpenFlyouts();

            var appCloseSaveReminderDialog = new AppCloseSaveReminderDialog(
                async () =>
                {
                    var count = NotepadsCore.GetNumberOfOpenedTextEditors();

                    foreach (var textEditor in NotepadsCore.GetAllTextEditors())
                    {
                        if (await SaveAsync(textEditor, saveAs: false, ignoreUnmodifiedDocument: true, rebuildOpenRecentItems: false))
                        {
                            NotepadsCore.DeleteTextEditor(textEditor);
                            count--;
                        }
                    }

                    // Prevent app from closing if there is any tab still open
                    if (count > 0)
                    {
                        await BuildOpenRecentButtonSubItemsAsync();
                        _isClosingInProgress = false;
                    }
                    else
                    {
                        App.InstanceHandlerMutex?.Dispose();
                        _isClosingFromAppWindow = true;
                        CloseMainWindow();
                    }
                },
                discardAndExitAction: () =>
                {
                    App.InstanceHandlerMutex?.Dispose();
                    _isClosingFromAppWindow = true;
                    CloseMainWindow();
                },
                cancelAction: () =>
                {
                    _isClosingInProgress = false;
                });

            var result = await DialogManager.OpenDialogAsync(appCloseSaveReminderDialog, awaitPreviousDialog: false);

            if (result == null || appCloseSaveReminderDialog.IsAborted)
            {
                NotepadsCore.FocusOnSelectedTextEditor();
                _isClosingInProgress = false;
            }
        }

        private void HideAllOpenFlyouts()
        {
            // Hide TextEditor ContextFlyout if it is showing
            // Why we need to do this? Take a look here: https://github.com/microsoft/microsoft-ui-xaml/issues/2461
            var editorFlyout = NotepadsCore.GetSelectedTextEditor()?.GetContextFlyout();
            if (editorFlyout != null && editorFlyout.IsOpen)
            {
                editorFlyout.Hide();
            }
        }

        private async void OnSessionBackupAndRestoreOptionChanged(object sender, bool isSessionBackupAndRestoreEnabled)
        {
            await Dispatcher.CallOnUIThreadAsync(async () =>
            {
                if (isSessionBackupAndRestoreEnabled)
                {
                    SessionManager.IsBackupEnabled = true;
                    SessionManager.StartSessionBackup(startImmediately: true);
                }
                else
                {
                    SessionManager.IsBackupEnabled = false;
                    SessionManager.StopSessionBackup();
                    await SessionManager.ClearSessionDataAsync();
                }
            });
        }

        private static void UpdateApplicationTitle(ITextEditor activeTextEditor)
        {
            if (!App.IsGameBarWidget)
            {
                App.MainWindow.Title = activeTextEditor.EditingFileName ?? activeTextEditor.FileNamePlaceholder;
            }
        }

        #endregion

        #region NotepadsCore Events

        private void OnTextEditorLoaded(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetSelectedTextEditor() == textEditor)
            {
                SetupStatusBar(textEditor);
                NotepadsCore.FocusOnSelectedTextEditor();
            }
        }

        private async void OnTextEditorUnloaded(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetNumberOfOpenedTextEditors() == 0)
            {
                if (AppSettingsService.IsSessionSnapshotEnabled)
                {
                    await SessionManager.SaveSessionAsync(() => { SessionManager.IsBackupEnabled = false; });
                }

                if (_lastTabMovedToAnotherInstance || AppSettingsService.ExitWhenLastTabClosed)
                {
                    Application.Current.Exit();
                }
                else
                {
                    NotepadsCore.OpenNewTextEditor(_defaultNewFileName);
                }
            }
        }

        private void OnTextEditorFileModificationStateChanged(ITextEditor textEditor)
        {
            if (textEditor.FileModificationState == FileModificationState.Modified)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_FileModifiedOutsideIndicator_ToolTip"), 3500);
            }
            else if (textEditor.FileModificationState == FileModificationState.RenamedMovedOrDeleted)
            {
                NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_FileRenamedMovedOrDeletedIndicator_ToolTip"), 3500);
            }
            UpdateFileModificationStateIndicator(textEditor);
            UpdatePathIndicator(textEditor);
        }

        private void OnTextEditorSaved(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetSelectedTextEditor() == textEditor)
            {
                SetupStatusBar(textEditor);
            }
            NotificationCenter.Instance.PostNotification(_resourceLoader.GetString("TextEditor_NotificationMsg_FileSaved"), 1500);
        }

        private void OnTextEditorMovedToAnotherAppInstance(object sender, ITextEditor textEditor)
        {
            if (NotepadsCore.GetNumberOfOpenedTextEditors() == 1)
            {
                _lastTabMovedToAnotherInstance = true;
            }
            NotepadsCore.DeleteTextEditor(textEditor);
        }

        private async void OnTextEditorClosing(object sender, ITextEditor textEditor)
        {
            if (!AppSettingsService.ExitWhenLastTabClosed &&
                NotepadsCore.GetNumberOfOpenedTextEditors() == 1 &&
                textEditor.IsModified == false &&
                textEditor.EditingFile == null)
            {
                // Do nothing if user doesn't want closing window when last tab closed
                // And if user is trying to close the last tab and the last tab is a new empty document
            }
            else if (!textEditor.IsModified)
            {
                NotepadsCore.DeleteTextEditor(textEditor);
            }
            else // Remind user to save uncommitted changes
            {
                var file = textEditor.EditingFilePath ?? textEditor.FileNamePlaceholder;

                var setCloseSaveReminderDialog = new SetCloseSaveReminderDialog(file,
                    saveAction: async () =>
                    {
                        if (NotepadsCore.GetAllTextEditors().Contains(textEditor) && await SaveAsync(textEditor, saveAs: false))
                        {
                            NotepadsCore.DeleteTextEditor(textEditor);
                        }
                    },
                    skipSavingAction: () =>
                    {
                        if (NotepadsCore.GetAllTextEditors().Contains(textEditor))
                        {
                            NotepadsCore.DeleteTextEditor(textEditor);
                        }
                    });

                setCloseSaveReminderDialog.Opened += (s, a) =>
                {
                    if (NotepadsCore.GetAllTextEditors().Contains(textEditor))
                    {
                        NotepadsCore.SwitchTo(textEditor);
                    }
                };

                await DialogManager.OpenDialogAsync(setCloseSaveReminderDialog, awaitPreviousDialog: true);

                if (!setCloseSaveReminderDialog.IsAborted)
                {
                    NotepadsCore.FocusOnSelectedTextEditor();
                }
            }
        }

        private void OnTextEditorKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!(sender is ITextEditor textEditor)) return;
            // ignoring key events coming from inactive text editors
            if (NotepadsCore.GetSelectedTextEditor() != textEditor) return;
            var result = _keyboardCommandHandler.Handle(e);
            if (result.ShouldHandle)
            {
                e.Handled = true;
            }
        }

        private async void OnStorageItemsDropped(object sender, IReadOnlyList<IStorageItem> storageItems)
        {
            foreach (var storageItem in storageItems)
            {
                if (storageItem is StorageFile file)
                {
                    await OpenFileAsync(file);
                    AnalyticsService.TrackEvent("OnStorageFileDropped");
                }
            }
        }

        #endregion
    }
}