/* Copyright (C) 2021 Reznicencu Bogdan
*  This program is free software; you can redistribute it and/or modify
*  it under the terms of the GNU General Public License as published by
*  the Free Software Foundation; either version 2 of the License, or
*  (at your option) any later version.
*  
*  This program is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU General Public License for more details.
*  
*  You should have received a copy of the GNU General Public License along
*  with this program; if not, write to the Free Software Foundation, Inc.,
*  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
*/

using IFilterShellView.Exceptions;
using IFilterShellView.Extensions;
using IFilterShellView.Filter;
using IFilterShellView.HelperClasses;
using IFilterShellView.Model;
using IFilterShellView.Native;
using IFilterShellView.Parser;
using IFilterShellView.Shell.Interfaces;
using IFilterShellView.Program;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SHDocVw;
using IFilterShellView.Helpers;

namespace IFilterShellView
{
    public partial class MainWindow : Window
    {
        /**
         * Working conditions
         *
         * - I am inside an explorer shell window. I press CTRL+F hotkey and a popup window shows up.
         * - I insert text in the popup and folders/files in explorer start to be selected
         *
         * Popup closing events
         * - I click outside the popup and it dissapears
         *
         * Expected behaviour
         * - When I press CTRL+F while in the same folder I want the filtering to continue from where I left.
         */

        private readonly ObservableCollection<CPidlData> listOfPidlData;
        private readonly ObservableCollection<CCommandItem> listOfAvailableCommands;
        private readonly ObservableCollection<CHistoryItem> listOfHistoryItems;
        private readonly List<Key> listOfHotkeys = new List<Key> { Key.LeftCtrl, Key.F };

        private readonly ListViewItemData prevListViewItemPidl = new ListViewItemData();
        private readonly GlobalKeyboardHook globalHookObject;
        private readonly BackgroundWorker workerObject_SelectionProc;
        private readonly DispatcherTimer dispatcherInputFilter;

        public readonly MainWindowModelMerger mainWindowModelMerger = new MainWindowModelMerger();


        private readonly int filterAfterDelay = 120;
        private const char keyModExtendedCommandMode = '?';
        private bool flagStringReadyToProcess = false;
        private DateTime lastTimeTextChanged;




        public MainWindow()
        {
            this.DataContext = mainWindowModelMerger;

            InitializeComponent();

            // Initialize a background worker responsible for the heavy selection task
            workerObject_SelectionProc = new BackgroundWorker();
            workerObject_SelectionProc.DoWork += new DoWorkEventHandler(WorkerCallback_SelectionProc_Task);
            workerObject_SelectionProc.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCallback_SelectionProc_Completed);
            workerObject_SelectionProc.ProgressChanged += new ProgressChangedEventHandler(WorkerCallback_SelectionProc_Progress);
            workerObject_SelectionProc.WorkerReportsProgress = true;
            workerObject_SelectionProc.WorkerSupportsCancellation = true;

            // Set item source
            listOfPidlData = CompilePidlList();
            listOfAvailableCommands = CompileCommandList();
            listOfHistoryItems = CompileHistoryList();

            ItemsList.ItemsSource = listOfPidlData;
            CommandList.ItemsSource = listOfAvailableCommands;
            HistoryList.ItemsSource = listOfHistoryItems;

            SyncApplicationSettingsWithUI();

            try
            {
                // This can throw - it handles unmanaged resources and it's a critical component
                Context _unused_ = Context.Instance;
                globalHookObject = new GlobalKeyboardHook();
                globalHookObject.AddHotkeys(listOfHotkeys, Callback_GlobalKeyboardHookSafeSta);
            }
            catch (Exception)
            {
                // TODO: critical exception - stop now. Cannot recover
                throw;
            }

            dispatcherInputFilter = new DispatcherTimer();
            dispatcherInputFilter.Tick += Callback_TimerPulse;
            dispatcherInputFilter.Interval = new TimeSpan(0, 0, 0, 0, 300);
        }
        private void Window_Deactivated(object sender, EventArgs e)
        {
            Callback_OnWindowCancellOrExit(false);
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Callback_OnWindowCancellOrExit(true);
        }




        #region Events that are called on before, after or during the filtering process
        private void Callback_UIOnBeforeFiltering(bool GoesInDeepProcessing = false)
        {
            listOfPidlData.Clear();

            ShowSearchResultsPage(true);

            if (GoesInDeepProcessing)
            {
                ProgressPb.Visibility = Visibility.Visible;
                FilterTb.IsReadOnly = true;
            }
        }
        private void Callback_UIOnAfterFiltering(bool ReturnedFromDeepProcessing = false, bool OverrideLastNotice = true)
        {
            if (ReturnedFromDeepProcessing)
            {
                ProgressPb.Visibility = Visibility.Collapsed;
                FilterTb.IsReadOnly = false;
            }

            if (listOfPidlData.Count == 0)
            {
                if (Context.Instance.FilterText.Length == 0)
                {
                    ShowSearchResultsPage(false);
                }
                else if (!Context.Instance.FlagRunInBackgroundWorker && OverrideLastNotice)
                {
                    UpdateNotificationData(NotificationManager.Notification_EmptyResults);
                }
            }
        }
        private void Callback_UIReportSelectionProgress(int ReportPecentage, List<CPidlData> ListOfSelections)
        {
            ListOfSelections.ForEach(pidl_data => listOfPidlData.Add(pidl_data));
        }
        private void Callback_OnWindowCancellOrExit(bool ApplicationIsExiting)
        {
            if (workerObject_SelectionProc.IsBusy) workerObject_SelectionProc.CancelAsync();

            // Re-eanble the shell's filter window.
            if (Context.Instance.PrevShellWindowModernSearchBoxHwnd != IntPtr.Zero)
            {
                NativeWin32.EnableWindow(Context.Instance.PrevShellWindowModernSearchBoxHwnd, true);
            }

            ShowSearchResultsPage(false);
            listOfPidlData.Clear();

            // Disable the timer
            dispatcherInputFilter.Stop();
            Context.Instance.Reset();

            if (ApplicationIsExiting)
            {
                // Save settings here as a precaution
                SaveApplicationSettings();

                globalHookObject.Dispose();
                Context.Instance.Dispose();
            }
            else
            {
                if (!Properties.Settings.Default.KeepFilterText)
                {
                    FilterTb.Text = "";
                }

                Hide();
            }
        }
        private void Callback_OnShellWindowCloseEvent()
        {
            Callback_OnWindowCancellOrExit(false);
        }
        private bool GatherShellInterfacesLinkedToShell(IntPtr ForegroundWindow)
        {
            ShellWindows pIShellWindows = new ShellWindows();

            foreach (IWebBrowserApp pIWebBrowserApp in pIShellWindows)
            {
                if (pIWebBrowserApp.HWND != (int)ForegroundWindow /*&& pIWebBrowserApp.FullName == ShellPath*/)
                    continue;

                if (!(pIWebBrowserApp is CIServiceProvider pIServiceProvider))
                {
                    Debug.WriteLine("Failed getting IServiceProvider interface");
                    return false;
                }

                pIServiceProvider.QueryService(Service.SID_STopLevelBrowser, typeof(CIShellBrowser).GUID, out object sb);
                CIShellBrowser pIShellBrowser = (CIShellBrowser)sb;

                if (pIShellBrowser == null)
                {
                    Debug.WriteLine("Failed getting IShellBrowser interface");
                    return false;
                }

                pIShellBrowser.QueryActiveShellView(out CIShellView pIShellView);

                if (!(pIShellView is CIFolderView2 pIFolderView2) || pIShellView == null)
                {
                    Debug.WriteLine("Failed getting IFolderView2/IShellView interface");
                    return false;
                }

                pIFolderView2.GetFolder(typeof(CIPersistFolder2).GUID, out object ppv);
                CIPersistFolder2 pIPersistFolder2 = (CIPersistFolder2)ppv; ;
                CIShellFolder pIShellFolder = (CIShellFolder)ppv;

                if (pIShellFolder == null)
                {
                    Debug.WriteLine("Failed getting IShellFolder interface");
                    return false;
                }

                // 
                IntPtr ModernSearchBoxHwnd = WindowExtensions.FindChildWindowByClassName(ForegroundWindow, "ModernSearchBox");

                // Old way of positioning the window - removed because of bug caused by KB5007186 
                //NativeWin32.GetWindowRect(ForegroundWindow, out Context.Instance.ShellViewRect);
                //System.Drawing.Point point = new System.Drawing.Point(0, 0);
                //NativeWin32.ClientToScreen(ForegroundWindow, ref point);

                // Set the rest of the Context.Instance class
                Context.Instance.PrevShellWindowModernSearchBoxHwnd = ModernSearchBoxHwnd;
                Context.Instance.PrevShellWindowHwnd = ForegroundWindow;
                Context.Instance.pIShellBrowser = pIShellBrowser;
                Context.Instance.pIFolderView2 = pIFolderView2;
                Context.Instance.pIShellFolder = pIShellFolder;
                Context.Instance.pIShellView = pIShellView;

                // This can occur if the focused shell window is presenting a virtual namespace
                if (pIWebBrowserApp.LocationURL == null || pIWebBrowserApp.LocationURL.Length == 0)
                    return false;

                Context.Instance.LocationUrl = new Uri(pIWebBrowserApp.LocationURL).LocalPath;

                return true;
            }

            return false;
        }
        private void Callback_GlobalKeyboardHookSafeSta()
        {
            IntPtr ForegroundWindow = NativeWin32.GetForegroundWindow();

            if (GatherShellInterfacesLinkedToShell(ForegroundWindow))
            {
                // Disable the shell's filter window.
                if (Context.Instance.PrevShellWindowModernSearchBoxHwnd != IntPtr.Zero)
                {
                    NativeWin32.EnableWindow(Context.Instance.PrevShellWindowModernSearchBoxHwnd, false);
                }

                // Show the window and make it visible
                this.Show();
                this.Activate();

                // Add focus to the search bar
                FilterTb.Focus();
                Keyboard.Focus(FilterTb);

                // Thank you powertoys repo! - used to bypass SetForegroundWindow restrictions
                // https://github.com/microsoft/PowerToys/blob/5a9f52fb11a7d8281fd3c93610bc6ae6ac770ff1/src/modules/fancyzones/FancyZonesLib/util.cpp#L383
                NativeWin32.INPUT[] pInputs = {
                    new NativeWin32.INPUT()
                    {
                        type = 0
                    }
                };
                NativeWin32.SendInput(1, pInputs, NativeWin32.INPUT.Size);

                // Make sure the window is the foreground window
                IntPtr hwnd = this.GetHWND();
                NativeWin32.SetForegroundWindow(hwnd);
                NativeWin32.SwitchToThisWindow(hwnd, true);
                NativeWin32.ShowWindowAsync(hwnd, 5);


                //IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(ThisWindowRef).EnsureHandle();
                //NativeWin32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, (int)ShellWidth, (int)ThisWindowRef.Height, NativeWin32.SWP_NOSIZE | NativeWin32.SWP_NOZORDER);
                UpdateWindowPositionToFixedPin();

                // Subscribe to the shell's message pump and filter close messages
                Context.Instance.EventManager.ResubscribeToExtCloseEvent(ForegroundWindow, Callback_OnShellWindowCloseEvent);

                /* Note: Do not move this line. We will create a copy of the current location url and store it inside LocationUrlBefore browse
                 * because we will need a copy when we click on a filtered item. When we click we call GatherShellInterfacesLinkedToShell 
                 * which resets LocationUrl. 
                 */
                Context.Instance.LocationUrlOnStart = Context.Instance.LocationUrl;

                // Enable dispatcher timer
                dispatcherInputFilter.Start();
            }
        }
        private void Callback_TimerPulse(object sender, EventArgs e)
        {
            TimeSpan DeltaTime = DateTime.Now - lastTimeTextChanged;

            if (DeltaTime.TotalMilliseconds < filterAfterDelay || !flagStringReadyToProcess)
                return;

            flagStringReadyToProcess = false;

            // Handle Context.Instance's exposed interfaces
            if (Context.Instance.pIFolderView2 == null || Context.Instance.pIShellFolder == null || Context.Instance.pIShellView == null)
                return;

            if (!FilterTb.IsFocused || Context.Instance.FilterText.Equals(Context.Instance.PrevFilterText) /*|| Context.Instance.FilterText.Length == 0*/)
                return;

            bool OverrideLastNotice = true;

            Context.Instance.PrevFilterText = Context.Instance.FilterText;
            Context.Instance.FlagExtendedFilterMod = Context.Instance.FilterText.StartsWith(keyModExtendedCommandMode);

            try
            {
                // Don't move this line. It must be placed right before checking the FilterText.Length and returning
                Callback_UIOnBeforeFiltering();

                // If there is no command then jump into the finally block
                if (Context.Instance.FilterText.Length == 0)
                {
                    return;
                }

                // Query the view for the number of items that are hosted
                Context.Instance.pIFolderView2.ItemCount(SVGIO.SVGIO_ALLVIEW, out Context.Instance.PidlCount);
                // The query provided is in fact a command that need special parsing

                bool FlagTooManyItems = Context.Instance.PidlCount >= Properties.Settings.Default.MaxFolderPidlCount_Deepscan;

                // Check if the number of items in folder is greater than the maximum accepted
                Context.Instance.FlagRunInBackgroundWorker |= Context.Instance.FlagExtendedFilterMod | FlagTooManyItems;


                if (Context.Instance.FlagExtendedFilterMod)
                {
                    UpdateNotificationData(NotificationManager.Notification_CommandGiven);
                }
                else if (FlagTooManyItems)
                {
                    UpdateNotificationData(NotificationManager.Notification_TooManyItems);
                }

                if (Context.Instance.FlagRunInBackgroundWorker)
                {
                    return;
                }

                Debug.WriteLine(string.Format("[{0}] executing command: '{1}'", DateTime.Now.ToString(), Context.Instance.FilterText));

                StartFilteringTheNamespaceFolderInContext();
            }
            catch (Exception ExceptionMessage)
            {
                HandleFilteringNamespaceException(ExceptionMessage);
                OverrideLastNotice = false;
            }
            finally
            {
                Callback_UIOnAfterFiltering(false, OverrideLastNotice);
            }
        }
        #endregion



        #region Worker registered events
        private void WorkerCallback_SelectionProc_Task(object sender, DoWorkEventArgs e)
        {
            StartFilteringTheNamespaceFolderInContext(sender as BackgroundWorker);
        }
        private void WorkerCallback_SelectionProc_Progress(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
                Callback_UIReportSelectionProgress(e.ProgressPercentage, (List<CPidlData>)e.UserState);
        }
        private void WorkerCallback_SelectionProc_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                HandleFilteringNamespaceException(e.Error);
            }
            else
            {
                if (Context.Instance.FlagExtendedFilterMod)
                {
                    listOfHistoryItems.Add(new CHistoryItem(Context.Instance.FilterText));
                }
            }
            Callback_UIOnAfterFiltering(true);

            Context.Instance.FlagRunInBackgroundWorker = false;
        }
        #endregion Worker registered events




        #region The actual processing unit - handles both filtering and exception handling
        /// <summary> Called both by the dispatcher and the background worker </summary>
        private void StartFilteringTheNamespaceFolderInContext(BackgroundWorker worker = null)
        {
            List<CPidlData> LocalSelectionBuffer = new List<CPidlData>();
            Func<CPidlData, bool> ILLanguageFunct = null;
            IntPtr PidlPtrObj = IntPtr.Zero;
            Exception LastException = null;

            bool IsOnWorkerThread = worker != null;
            bool FlagFlushBuffer = true;
            int NumberOfIntervals = Math.Min(10, Context.Instance.PidlCount);
            int NumberOfItemsPerInterval = Math.Min(Context.Instance.PidlCount / NumberOfIntervals, NumberOfIntervals);
            int ReportPecentage = 0;
            int TempCounter = 0;
            int FailedAttempts = 0;
            int MaxFailedAttempts = 15;

            // Callback that handles report management - dispatches reports according to the caller's context.
            void ReportAction()
            {
                List<CPidlData> NewList = LocalSelectionBuffer.GetRange(0, LocalSelectionBuffer.Count);
                if (IsOnWorkerThread)
                {
                    worker.ReportProgress(ReportPecentage, (object)NewList);
                }
                else
                {
                    Callback_UIReportSelectionProgress(ReportPecentage, NewList);
                }

                LocalSelectionBuffer.Clear();
            }

            // Reset filter count
            Context.Instance.FilterCount = 0;

            // Disable redrawing for faster selection
            Context.Instance.pIFolderView2.SetRedraw(false);

            // No point in unselecting all items if I don't want to select any
            if (Properties.Settings.Default.AutoSelectFiltered)
            {
                Context.Instance.pIShellView.SelectItem(IntPtr.Zero, SVSI.SVSI_DESELECTOTHERS);
            }

            // After deselecting all items check if the filter is empty
            if (Context.Instance.FilterText.Length == 0)
            {
                goto LabelRestoreState;
            }

            // Compile the predicate chain if needed
            if (Context.Instance.FlagExtendedFilterMod)
            {
                XPressParser XLanguageParser = new XPressParser(Context.Instance.FilterText[1..]);
                ILLanguageFunct = XLanguageParser.Compile();

                if (ILLanguageFunct == null)
                {
                    throw new UserException("Parser couldn't compile the given command");
                }
            }

            try
            {
                // Iterate through all the items inside the shell and start matching
                for (int PidlIndex = 0; PidlIndex < Context.Instance.PidlCount; PidlIndex++, TempCounter++)
                {
                    bool FilterMatched = false;

                    // If I want to return only a sepcific number of elements
                    if (Properties.Settings.Default.MaxNumberFilterUpTo > 0 &&
                        Properties.Settings.Default.MaxNumberFilterUpTo <= Context.Instance.FilterCount)
                    {
                        break;
                    }

                    Context.Instance.pIFolderView2.Item(PidlIndex, out PidlPtrObj);
                    ReportPecentage = (int)((float)PidlIndex / Context.Instance.PidlCount * 100.0f);

                    if (PidlPtrObj == IntPtr.Zero)
                    {
                        if (FailedAttempts > MaxFailedAttempts)
                            throw new UserException("Too many failures while interacting with the shell view");

                        FailedAttempts++;
                        continue;
                    }
                    else
                    {
                        FailedAttempts = 0;
                    }

                    NativeWin32.HResult hr = NativeWin32.SHGetDataFromIDListW(
                        Context.Instance.pIShellFolder,
                        PidlPtrObj,
                        NativeWin32.SHGDFIL_FINDDATA,
                        out NativeWin32.WIN32_FIND_DATA Pidl_Win32_FindData,
                        NativeWin32.WIN32_FIND_DATA_SIZE);

                    CPidlData PidlData = new CPidlData();

                    if (hr == NativeWin32.HResult.Ok)
                    {
                        PidlData.PidlName = Pidl_Win32_FindData.cFileName;
                        PidlData.FileAttributes = Pidl_Win32_FindData.dwFileAttributes;
                        PidlData.FileSize = ((ulong)Pidl_Win32_FindData.nFileSizeHigh << 32) | Pidl_Win32_FindData.nFileSizeLow;
                        PidlData.CreationTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftCreationTime);
                        PidlData.LastAccessTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftLastAccessTime);
                        PidlData.LastWriteTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftLastWriteTime);
                        PidlData.AttributesSet = true;
                    }
                    else
                    {
                        Context.Instance.pIShellFolder.GetDisplayNameOf
                        (
                            PidlPtrObj,
                            SHGNO.INFOLDER | SHGNO.FORPARSING,
                            Context.Instance.MarshalPIDLNativeDataHolder.STRRET
                        );

                        NativeWin32.StrRetToBuf
                        (
                            Context.Instance.MarshalPIDLNativeDataHolder.STRRET,
                            PidlPtrObj,
                            Context.Instance.MarshalPIDLNativeDataHolder.BUFFER,
                            Context.Instance.MarshalPIDLNativeDataHolder.MAX_PATH
                        );

                        PidlData.PidlName = Context.Instance.MarshalPIDLNativeDataHolder.BUFFER.ToString();
                        PidlData.AttributesSet = false;
                    }

                    if (Context.Instance.FlagExtendedFilterMod)
                    {
                        // Then all the matching will be done according to an expression tree
                        FilterMatched = ILLanguageFunct(PidlData);
                    }
                    else
                    {
                        // Then all the matching will be done according to settings enabled by the user
                        FilterMatched = FilterBasedOnSettings(PidlData, Context.Instance.FilterText);
                    }

                    // If there was a match then this pidl must be selected
                    if (FilterMatched)
                    {
                        LocalSelectionBuffer.Add(PidlData);

                        if (Properties.Settings.Default.AutoSelectFiltered)
                        {
                            Context.Instance.pIShellView.SelectItem(PidlPtrObj, SVSI.SVSI_SELECT);
                        }

                        Context.Instance.FilterCount++;
                    }

                    Marshal.FreeCoTaskMem(PidlPtrObj);
                    PidlPtrObj = IntPtr.Zero;

                    if (TempCounter >= NumberOfItemsPerInterval)
                    {
                        ReportAction();
                        TempCounter = 0;
                    }

                    if (IsOnWorkerThread && worker.CancellationPending)
                    {
                        FlagFlushBuffer = false;
                        break;
                    }
                }
            }
            catch (Exception ExceptionMessage)
            {
                if (PidlPtrObj != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(PidlPtrObj);

                LastException = ExceptionMessage;
            }


        LabelRestoreState:

            // Flush the last reports inside the buffer
            if (FlagFlushBuffer && LocalSelectionBuffer.Count != 0)
            {
                ReportAction();
            }

            Context.Instance.pIFolderView2.SetRedraw(true);

            if (LastException != null) throw LastException;
        }
        private void HandleFilteringNamespaceException(Exception ExceptionParam)
        {
            string NormalizedException;
            if (ExceptionParam is UserException exception)
            {
                NormalizedException = exception.Message;
            }
            else
            {
                NormalizedException = "The application failed to finalize the task due to some internal error.";
            }

            UpdateNotificationData(NotificationManager.Get("Compiling the command failed", string.Format("The parser encountered the following error while compiling the given command: \n'{0}'", NormalizedException)));
        }
        #endregion





        /*
         * Event handlers
         */

        #region Settings
        private void SyncApplicationSettingsWithUI()
        {
            void ApplyButtonConfiguration(IEnumerable<RadioButton> SButtons, uint Setting)
            {
                foreach (RadioButton RButton in SButtons)
                {
                    int tag = Convert.ToInt32(RButton.Tag);
                    if (Setting != tag) continue;
                    RButton.IsChecked = true;
                    HandleSettingChangedUniv(RButton, false);
                    break;
                }
            }

            ApplyButtonConfiguration(SettingsPlacement.Children.OfType<RadioButton>(), Properties.Settings.Default.SettingsPlacementId);
            ApplyButtonConfiguration(SettingsCase.Children.OfType<RadioButton>(), Properties.Settings.Default.SettingsCaseId);
        }
        private void SaveApplicationSettings()
        {
            List<CHistoryItem> HistoryListFromIObs = listOfHistoryItems.ToList();
            if (listOfHistoryItems.Count > Properties.Settings.Default.MaxHistory)
            {
                HistoryListFromIObs.RemoveRange(0, Properties.Settings.Default.MaxHistory / 2);
            }

            Properties.Settings.Default.HistoryListSerialized =
                SerializeExtensions.SerializeGenericClassList(HistoryListFromIObs);

            Properties.Settings.Default.Save();
        }
        private void FilterSettingChanged(object sender, RoutedEventArgs e)
        {
            HandleSettingChangedUniv(sender, true);
        }
        private void HandleSettingChangedUniv(object sender, bool SaveSetting = false)
        {
            RadioButton crbtn = sender as RadioButton;
            uint SettingId = Convert.ToUInt32(crbtn.Tag);

            switch (crbtn.GroupName)
            {
                case "SettingsPlacement":
                    if (SaveSetting) Properties.Settings.Default.SettingsPlacementId = SettingId;

                    PlacementSettingsIc.Glyph = ((ModernWpf.Controls.FontIcon)crbtn.Content).Glyph;
                    break;
                case "SettingsCase":
                    if (SaveSetting) Properties.Settings.Default.SettingsCaseId = SettingId;

                    CaseSettingsIc.Glyph = ((ModernWpf.Controls.FontIcon)crbtn.Content).Glyph;
                    break;
            }
        }
        #endregion




        #region Filter input handlers
        private void FilterTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            Context.Instance.FilterText = FilterTb.Text.TrimStart();
            Context.Instance.LocationUrlOnStart = Context.Instance.LocationUrl;

            lastTimeTextChanged = DateTime.Now;
            flagStringReadyToProcess = true;
        }
        private void FilterTb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (!Context.Instance.FlagRunInBackgroundWorker || workerObject_SelectionProc.IsBusy)
                            return;

                        // Start worker
                        Callback_UIOnBeforeFiltering(true);
                        workerObject_SelectionProc.RunWorkerAsync();
                        break;
                    }
                case Key.Escape:
                    {
                        if (!workerObject_SelectionProc.IsBusy)
                        {
                            Callback_OnWindowCancellOrExit(false);
                            return;
                        }

                        workerObject_SelectionProc.CancelAsync();
                        break;
                    }
                case Key.Up:
                    {
                        if (ItemsList.SelectedIndex > 0)
                            ItemsList.SelectedIndex--;
                        else
                            ItemsList.SelectedIndex = ItemsList.Items.Count - 1;

                        e.Handled = true;
                        break;
                    }
                case Key.Down:
                    {
                        if (ItemsList.SelectedIndex < ItemsList.Items.Count - 1)
                            ItemsList.SelectedIndex++;
                        else
                            ItemsList.SelectedIndex = 0;

                        e.Handled = true;
                        break;
                    }
                default: break;
            }
        }
        private void FilterTb_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Back:
                    {
                        if (workerObject_SelectionProc.IsBusy)
                        {
                            workerObject_SelectionProc.CancelAsync();
                            e.Handled = true;
                            return;
                        }
                        break;
                    }
                default: break;
            }
        }
        #endregion




        #region Toolbar Settings
        private void PlacementSettingsBt_Click(object sender, RoutedEventArgs e) =>
            ModernWpf.Controls.Primitives.FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        private void CaseSettingsBt_Click(object sender, RoutedEventArgs e) =>
            ModernWpf.Controls.Primitives.FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        private void SettingsBt_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Show();
        }
        private void ExitBt_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion






        #region Item List Viewer - Folders and files
        private void ItemsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            // Open eplorer at selected folder
            if (NativeUtilities.IsAttributeOfFolder(SelectedPidlData.FileAttributes))
            {
                if (!BrowseToFolderByDisplayName(FullyQuallifiedItemName))
                {
                    // TODO: do something ?
                }
            }
        }
        private void ItemsList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.Source != null)
                {
                    if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

                    DataObject dragObj = new DataObject();
                    dragObj.SetFileDropList(new System.Collections.Specialized.StringCollection() { FullyQuallifiedItemName });
                    DragDrop.DoDragDrop((ListView)e.Source, dragObj, DragDropEffects.Copy);
                }
            }
            else
            {
                if (ItemsList.Items.Count == 0)
                {
                    FilterItemsControlBox.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ListViewItem listViewItem = ItemsList.GetItemAt(e.GetPosition(ItemsList));

                    if (listViewItem == null || listViewItem.Content == null) return;

                    Rect listViewItemRect = ItemsList.GetListViewItemRect(listViewItem);

                    if (prevListViewItemPidl.ItemRect.Equals(listViewItemRect)) return;

                    string PidlName = (listViewItem.Content as CPidlData).PidlName;
                    var SearchResult = listOfPidlData.Select((Pidl, Index) => (Pidl, Index))
                        .FirstOrDefault(Item => Item.Pidl.PidlName == PidlName);

                    prevListViewItemPidl.Index = (SearchResult.Pidl == null) ? -1 : SearchResult.Index;
                    prevListViewItemPidl.ItemRect = listViewItemRect;

                    if (prevListViewItemPidl.Index == -1) return;

                    var newMargins = FilterItemsControlBox.Margin;
                    newMargins.Top = listViewItemRect.Top + listViewItemRect.Height / 2 - FilterItemsControlBox.ActualHeight / 2;

                    FilterItemsControlBox.Visibility = Visibility.Visible;

                    if (NativeUtilities.IsAttributeOfFolder(SearchResult.Pidl.FileAttributes))
                    {
                        Cmd_RunFile.Visibility = Visibility.Collapsed;
                        Cmd_CopyFile.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        Cmd_RunFile.Visibility = Visibility.Visible;
                        Cmd_CopyFile.Visibility = Visibility.Visible;
                    }

                    FilterItemsControlBox.Margin = newMargins;
                }
            }
        }
        private void ItemsPanelGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            FilterItemsControlBox.Visibility = Visibility.Visible;
        }
        private void ItemsPanelGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            FilterItemsControlBox.Visibility = Visibility.Collapsed;
        }
        private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            ((ListView)sender).ScrollIntoView(e.AddedItems[0]);
        }
        private void Cmd_RunFile_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out _, out string FullyQuallifiedItemName)) return;

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(FullyQuallifiedItemName)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception)
            {
                // TODO: log this exception
            }
        }
        private void Cmd_CopyFile_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out _, out string FullyQuallifiedItemName)) return;

            DataObject ClpDataObject = new DataObject();
            string[] ClpFileArray = new string[1];
            ClpFileArray[0] = FullyQuallifiedItemName;
            ClpDataObject.SetData(DataFormats.FileDrop, ClpFileArray, true);
            Clipboard.SetDataObject(ClpDataObject, true);
        }
        private void Cmd_InvokeProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out _, out string FullyQuallifiedItemName)) return;

            NativeUtilities.ShowFileProperties(FullyQuallifiedItemName);
        }
        private void Cmd_DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out CPidlData SelectedPidlData, out _)) return;

            if (DeletePidlFromSystem(SelectedPidlData))
            {
                int SelectedIndex = ItemsList.SelectedIndex;
                if (SelectedIndex >= 0)
                    listOfPidlData.RemoveAt(SelectedIndex);
            }
        }
        private void BrowseBackBt_Click(object sender, RoutedEventArgs e)
        {
            if (Context.Instance.LocationUrlOnStart == Context.Instance.LocationUrl) return;

            if (!BrowseBackToParentItem())
            {
                // TODO: report
            }
        }
        #endregion


        #region Related functions

        private void UpdateNotificationData(NotificationManager.NotificationData notification)
        {
            mainWindowModelMerger.SearchPageNoticeTitle.Text = notification.Title;
            mainWindowModelMerger.SearchPageNoticeMessage.Text = notification.Message;
        }

        #endregion



        #region History List
        private void HistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (HistoryList.SelectedIndex < 0) return;

            FilterTb.Text = listOfHistoryItems[HistoryList.SelectedIndex].Command;
        }
        private void ShowHistoryList(object sender, RoutedEventArgs e) =>
            ModernWpf.Controls.Primitives.FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        #endregion



        #region Command List
        private void CommandList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CommandList.SelectedIndex < 0) return;

            FilterTb.Text = "? " + listOfAvailableCommands[CommandList.SelectedIndex].Name;
        }
        private void ShowCommandList(object sender, RoutedEventArgs e) =>
            ModernWpf.Controls.Primitives.FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        #endregion




        /*
         * Helpers and extensions
         */

        private bool FilterBasedOnSettings(CPidlData PidlData, string input)
        {
            uint SettingsPlacementId = Properties.Settings.Default.SettingsPlacementId;
            uint SettingsCaseId = Properties.Settings.Default.SettingsCaseId;
            string PidlName = PidlData.PidlName;

            // Case insensitive
            if (SettingsCaseId == 2)
            {
                PidlName = PidlData.PidlName.ToLower();
                input = input.ToLower();
            }

            // Find applicable actions
            var action = FitlerActions.SettingsActionMap[(FilterSettingsFlags)SettingsPlacementId];
            return action(PidlName, input);
        }
        private bool BrowseToFolderByDisplayName(string FullyQuallifiedItemName)
        {
            // Parse display name to pidl
            IntPtr PidlBrowse = NativeWin32.ConvertItemNameToPidl(FullyQuallifiedItemName);

            // Is the conversion succesful
            if (PidlBrowse == IntPtr.Zero) return false;

            // Browse to selected folder
            NativeWin32.HResult hr = Context.Instance.pIShellBrowser.BrowseObject(
                PidlBrowse,
                SBSP.SBSP_SAMEBROWSER |
                SBSP.SBSP_NOAUTOSELECT
            );

            // These are ignored because of SBSP_SAMEBROWSER
            // SBSP.SBSP_WRITENOHISTORY |
            // SBSP.SBSP_CREATENOHISTORY |
            // SBSP.SBSP_NOTRANSFERHIST |

            bool FlagResult = hr == NativeWin32.HResult.Ok;

            // Delete referenced data
            Marshal.FreeCoTaskMem(PidlBrowse);

            // When we browse a new folder some of the data changes
            return FlagResult && GatherShellInterfacesLinkedToShell(Context.Instance.PrevShellWindowHwnd);
        }
        private bool BrowseBackToParentItem()
        {
            bool FlagResult = Context.Instance.pIShellBrowser.BrowseObject(
                IntPtr.Zero,
                SBSP.SBSP_SAMEBROWSER | SBSP.SBSP_PARENT
                ) == NativeWin32.HResult.Ok;

            return FlagResult && GatherShellInterfacesLinkedToShell(Context.Instance.PrevShellWindowHwnd);
        }
        private bool GetPidlAndFullPath(int Index, out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            SelectedPidlData = null;
            FullyQuallifiedItemName = "";

            if (Index < 0 || Index >= listOfPidlData.Count) return false;

            SelectedPidlData = listOfPidlData[Index];
            GetPidlFullPath(SelectedPidlData, out FullyQuallifiedItemName);
            return true;
        }
        private bool GetHoveredPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            return GetPidlAndFullPath(prevListViewItemPidl.Index, out SelectedPidlData, out FullyQuallifiedItemName);
        }
        private bool GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            return GetPidlAndFullPath(ItemsList.SelectedIndex, out SelectedPidlData, out FullyQuallifiedItemName);
        }
        private void GetPidlFullPath(CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            FullyQuallifiedItemName = Path.Combine(Context.Instance.LocationUrlOnStart, SelectedPidlData.PidlName);
        }
        private bool DeletePidlFromSystem(CPidlData SelectedPidlData)
        {
            GetPidlFullPath(SelectedPidlData, out string FullyQuallifiedItemName);
            try
            {
                if (NativeUtilities.IsAttributeOfFolder(SelectedPidlData.FileAttributes))
                {
                    Directory.Delete(FullyQuallifiedItemName);
                }
                else
                {
                    File.Delete(FullyQuallifiedItemName);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }


        private ObservableCollection<CPidlData> CompilePidlList()
        {
            return new ObservableCollection<CPidlData>();
        }
        private ObservableCollection<CCommandItem> CompileCommandList()
        {
            ObservableCollection<CCommandItem> list = new ObservableCollection<CCommandItem>();
            CCommandItem CmdWrapperUI = null;
            int LastComIndex = -1;

            foreach (KeyValuePair<string, XPressCommands.ComIndex> CommandEntry in XPressCommands.ComStrToComIndex)
            {
                if (LastComIndex != (int)CommandEntry.Value)
                {
                    if (CmdWrapperUI != null)
                    {
                        list.Add(CmdWrapperUI);
                    }

                    CmdWrapperUI = new CCommandItem
                    {
                        Name = string.Format("{0} - {1}", CommandEntry.Key, XPressCommands.ComIndexDescription[CommandEntry.Value]),
                        CmdAlias = "Alias - "
                    };
                    LastComIndex = (int)CommandEntry.Value;
                }
                else CmdWrapperUI.CmdAlias += CommandEntry.Key + " | ";
            }

            return list;
        }
        public ObservableCollection<CHistoryItem> CompileHistoryList()
        {
            return new ObservableCollection<CHistoryItem>(SerializeExtensions.MaterializeGenericClassList<CHistoryItem>(Properties.Settings.Default.HistoryListSerialized));
        }


        public void UpdateWindowPositionToFixedPin()
        {
            NativeWin32.MONITORINFOEX MonitorInfo = new NativeWin32.MONITORINFOEX();
            MonitorInfo.Init();

            IntPtr CurrentMonitorHandle = NativeWin32.MonitorFromWindow(this.GetHWND(), NativeWin32.MONITOR_DEFAULTTONEAREST);
            NativeWin32.GetMonitorInfo(CurrentMonitorHandle, ref MonitorInfo);
            NativeWin32.RECT CurrentMonitorRect = MonitorInfo.Monitor;

            double widthDPIFactor = this.GetWindowDPIFactorClass().widthDPIFactor;
            double ScreenWidth = CurrentMonitorRect.ToRectangle().Width;

            this.Width = ScreenWidth * widthDPIFactor;
            this.Left = 0;
            this.Top = 0;
        }
        private void ShowSearchResultsPage(bool Visible)
        {
            mainWindowModelMerger.SearchPageVisibilityModel.Visible = Visible;
        }
    }
}
