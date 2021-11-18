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

using IFilterShellView2.Exceptions;
using IFilterShellView2.Export;
using IFilterShellView2.Extensions;
using IFilterShellView2.Filter;
using IFilterShellView2.HelperClasses;
using IFilterShellView2.Model;
using IFilterShellView2.Native;
using IFilterShellView2.Parser;
using IFilterShellView2.Shell.Interfaces;
using IFilterShellView2.ShellContext;
using Microsoft.Win32;
using SHDocVw;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace IFilterShellView2
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

        private readonly ObservableCollection<CPidlData> listOfPidlData = new ObservableCollection<CPidlData>();
        private readonly ObservableCollection<CCommandItem> listOfAvailableCommands = new ObservableCollection<CCommandItem>();
        private readonly ObservableCollection<CHistoryItem> listOfHistoryItems = new ObservableCollection<CHistoryItem>();
        private readonly List<CHistoryItem> tempListOfHistoryItems = new List<CHistoryItem>();
        private readonly List<Key> listOfHotkeys = new List<Key> { Key.LeftCtrl, Key.F };
        private readonly Dictionary<string, BitmapSource> extensionIconDictionary = new Dictionary<string, BitmapSource>();


        private readonly ListViewItemPidl prevListViewItemPidl = new ListViewItemPidl();
        private readonly GlobalKeyboardHook globalHookObject;
        private readonly ShellContextContainer shellContext;
        private readonly BackgroundWorker workerObject_SelectionProc;
        private readonly DispatcherTimer dispatcherInputFilter;

        private readonly string assemblyImageName;
        private readonly string assemblyImageLocation;
        private const char keyModExtendedCommandMode = '?';

        private readonly int filterAfterDelay = 120;
        private bool flagStringReadyToProcess = false;
        private DateTime lastTimeTextChanged;






        public VisibilityModel SearchPageVisibilityModel = new VisibilityModel();

        private void ShowSearchResultsPage(bool Visible)
        {
            SearchPageVisibilityModel.Visible = Visible;
        }


        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = SearchPageVisibilityModel;

            Assembly CurrentImageAssembly = Assembly.GetExecutingAssembly();
            assemblyImageName = CurrentImageAssembly.GetName().Name;
            assemblyImageLocation = Path.Combine(Path.GetDirectoryName(CurrentImageAssembly.Location), assemblyImageName + ".exe");

            // Initialize application settings
            LoadApplicationSettings();

            // Initialize a background worker responsible for the heavy selection task
            workerObject_SelectionProc = new BackgroundWorker();
            workerObject_SelectionProc.DoWork += new DoWorkEventHandler(WorkerCallback_SelectionProc_Task);
            workerObject_SelectionProc.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCallback_SelectionProc_Completed);
            workerObject_SelectionProc.ProgressChanged += new ProgressChangedEventHandler(WorkerCallback_SelectionProc_Progress);
            workerObject_SelectionProc.WorkerReportsProgress = true;
            workerObject_SelectionProc.WorkerSupportsCancellation = true;

            ItemsList.ItemsSource = listOfPidlData;
            CompileCommandDataList();
            CommandList.ItemsSource = listOfAvailableCommands;

            listOfHistoryItems = new ObservableCollection<CHistoryItem>(
                SerializeExtensions.MaterializeGenericClassList<CHistoryItem>(Properties.Settings.Default.HistoryListSerialized));

            HistoryList.ItemsSource = listOfHistoryItems;

            try
            {
                // This can throw - it handles unmanaged resources and it's a critical component
                shellContext = new ShellContextContainer();
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




        private bool GatherShellInterfacesLinkedToShell(IntPtr ForegroundWindow)
        {
            ShellWindows pIShellWindows = new ShellWindows();

            foreach (IWebBrowserApp pIWebBrowserApp in pIShellWindows)
            {
                if (pIWebBrowserApp.HWND != (int)ForegroundWindow /*&& pIWebBrowserApp.FullName == ShellPath*/)
                    continue;

                CIServiceProvider pIServiceProvider = pIWebBrowserApp as CIServiceProvider;

                if (pIServiceProvider == null)
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
                CIFolderView2 pIFolderView2 = pIShellView as CIFolderView2;


                if (pIFolderView2 == null || pIShellView == null)
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

                // Old way of positioning the window - removed because of bug caused by KB5007186 
                //NativeWin32.GetWindowRect(ForegroundWindow, out ShellContext.ShellViewRect);
                //System.Drawing.Point point = new System.Drawing.Point(0, 0);
                //NativeWin32.ClientToScreen(ForegroundWindow, ref point);


                // Set the rest of the ShellContext class
                shellContext.pIShellBrowser = pIShellBrowser;
                shellContext.pIFolderView2 = pIFolderView2;
                shellContext.pIShellFolder = pIShellFolder;
                shellContext.pIShellView = pIShellView;

                // This can occur if the focused shell window is presenting a virtual namespace
                if (pIWebBrowserApp.LocationURL == null || pIWebBrowserApp.LocationURL.Length == 0)
                    return false;

                shellContext.LocationUrl = new Uri(pIWebBrowserApp.LocationURL).LocalPath;

                return true;
            }

            return false;
        }
        private void ResetInterfaceData(bool GoesIntoHidingMode = false)
        {
            //ItemsList.ItemsSource = null;
            listOfPidlData.Clear();
            //ItemsList.ItemsSource = listOfPidlData;
            //GC.Collect();

            UpdateSelectionStatusBarInfo(0);

            if (!Properties.Settings.Default.KeepFilterText && GoesIntoHidingMode)
            {
                FilterTb.Text = "";
            }

            AdvancedSettingsPanel.Visibility = Visibility.Collapsed;
            //ItemsPanel.Visibility = Visibility.Collapsed;
            ShowSearchResultsPage(false);
        }




        #region Events that are called on before, after and during the filtering process
        private void Callback_UIOnBeforeFiltering(bool GoesInDeepProcessing = false)
        {
            ResetInterfaceData();

            // ItemsPanel.Visibility = Visibility.Visible;
            ShowSearchResultsPage(true);

            if (GoesInDeepProcessing)
            {
                ProgressPb.Visibility = Visibility.Visible;
                //ItemsPanel.IsEnabled = false;
                FilterTb.IsReadOnly = true;
            }
        }
        private void Callback_UIOnAfterFiltering(bool ReturnedFromDeepProcessing = false, string ErrorMessage = "")
        {
            if (ReturnedFromDeepProcessing)
            {
                ProgressPb.Visibility = Visibility.Collapsed;
                // ItemsPanel.IsEnabled = true;
                FilterTb.IsReadOnly = false;

                if (tempListOfHistoryItems.Count != 0)
                {
                    tempListOfHistoryItems.ForEach(HItem => listOfHistoryItems.Add(HItem));
                    tempListOfHistoryItems.Clear();
                }
            }

            if (shellContext.FilterCount == 0)
            {
                // ItemsPanel.Visibility = Visibility.Collapsed;
                ShowSearchResultsPage(false);
            }
        }
        private void Callback_UIReportSelectionProgress(int ReportPecentage, List<CPidlData> ListOfSelections)
        {
            ListOfSelections.ForEach(pidl_data => listOfPidlData.Add(pidl_data));
            UpdateSelectionStatusBarInfo();
        }
        #endregion



        #region General callbacks that handle the filtering process
        private void Callback_OnWindowCancellOrExit(bool ApplicationIsExiting)
        {
            if (workerObject_SelectionProc.IsBusy) workerObject_SelectionProc.CancelAsync();

            dispatcherInputFilter.Stop();
            shellContext.Reset();

            ResetInterfaceData(true);

            if (ApplicationIsExiting)
            {
                // Save settings here as a precaution
                SaveApplicationSettings();

                dispatcherInputFilter.Stop();
                globalHookObject.Dispose();
                shellContext.Dispose();
            }
            else
            {
                Hide();
            }
        }
        private void Callback_OnShellWindowCloseEvent()
        {
            Callback_OnWindowCancellOrExit(false);
        }
        private void Callback_GlobalKeyboardHookSafeSta()
        {
            shellContext.PrevShellWindowHwnd = NativeWin32.GetForegroundWindow();

            if (GatherShellInterfacesLinkedToShell(shellContext.PrevShellWindowHwnd))
            {
                // Show the window and make it visible
                this.Show();
                this.Activate(); // must be set before ?
                this.Focus();

                // Make sure that the window is active using native calls
                // WindowExtensions.ActivateWindow(ThisWindowRef);

                // Add focus to the search bar
                FilterTb.Focus();
                Keyboard.Focus(FilterTb);

                //IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(ThisWindowRef).EnsureHandle();
                //NativeWin32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, (int)ShellWidth, (int)ThisWindowRef.Height, NativeWin32.SWP_NOSIZE | NativeWin32.SWP_NOZORDER);
                UpdateWindowPositionToFixedPin();

                // Subscribe to the shell's message pump and filter close messages
                shellContext.EventManager.ResubscribeToExtCloseEvent(shellContext.PrevShellWindowHwnd, Callback_OnShellWindowCloseEvent);

                /* Note: Do not move this line. We will create a copy of the current location url and store it inside LocationUrlBefore browse
                 * because we will need a copy when we click on a filtered item. When we click we call GatherShellInterfacesLinkedToShell 
                 * which resets LocationUrl. 
                 */
                shellContext.LocationUrlBeforeBrowse = shellContext.LocationUrl;

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

            // Handle ShellContext's exposed interfaces
            if (shellContext.pIFolderView2 == null || shellContext.pIShellFolder == null || shellContext.pIShellView == null)
                return;

            if (!FilterTb.IsFocused || shellContext.FilterText.Equals(shellContext.PrevFilterText) /*|| ShellContext.FilterText.Length == 0*/)
                return;

            string ErrorString = "";
            shellContext.PrevFilterText = shellContext.FilterText;
            shellContext.FlagExtendedFilterMod = shellContext.FilterText.StartsWith(keyModExtendedCommandMode);

            try
            {
                // Query the view for the number of items that are hosted
                shellContext.pIFolderView2.ItemCount(SVGIO.SVGIO_ALLVIEW, out shellContext.PidlCount);

                // If I want to enter a command
                if (shellContext.FlagExtendedFilterMod)
                {
                    shellContext.FlagRunInBackgroundWorker = true;

                    return;
                }

                Debug.WriteLine(string.Format("[{0}] executing command: '{1}'", DateTime.Now.ToString(), shellContext.FilterText));

                Callback_UIOnBeforeFiltering();

                // Check if the number of items in folder is greate than the maximum accepted
                if (shellContext.PidlCount >= Properties.Settings.Default.MaxFolderPidlCount_Deepscan)
                {
                    shellContext.FlagRunInBackgroundWorker = true;
                    return;
                }

                StartFilteringTheNamespaceFolderInContext();
            }
            catch (Exception ExceptionMessage)
            {
                HandleFilteringNamespaceException(ExceptionMessage, out ErrorString);
            }

            Callback_UIOnAfterFiltering(false, ErrorString);
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
            string ErrorString = "";

            if (e.Error != null)
            {
                HandleFilteringNamespaceException(e.Error, out ErrorString);
            }
            else if (e.Cancelled)
            {
                // 
            }
            else
            {
            }
            Callback_UIOnAfterFiltering(true, ErrorString);

            shellContext.FlagRunInBackgroundWorker = false;
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
            int NumberOfIntervals = Math.Min(10, shellContext.PidlCount);
            int NumberOfItemsPerInterval = Math.Min(shellContext.PidlCount / NumberOfIntervals, NumberOfIntervals);
            int ReportPecentage = 0;
            int TempCounter = 0;
            int FailedAttempts = 0;
            int MaxFailedAttempts = 15;

            // Callback that handles report management - dispatches reports according to the caller's context.
            Action ReportAction = () =>
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
            };

            // Setting some variables
            shellContext.FilterReportCount = 0;
            shellContext.FilterCount = 0;

            shellContext.pIFolderView2.SetRedraw(false);
            shellContext.pIShellView.SelectItem(IntPtr.Zero, SVSI.SVSI_DESELECTOTHERS);


            // After deselecting all items check if the filter is empty
            if (shellContext.FilterText.Length == 0)
            {
                goto LabelRestoreState;
            }


            // Compile the predicate chain if needed
            if (shellContext.FlagExtendedFilterMod)
            {
                XPressParser XLanguageParser = new XPressParser(shellContext.FilterText[1..]);
                ILLanguageFunct = XLanguageParser.Compile();

                if (ILLanguageFunct == null)
                {
                    throw new UserException("Parser couldn't compile the given command");
                }

                // Save the command in the list
                tempListOfHistoryItems.Add(new CHistoryItem(shellContext.FilterText));
            }

            try
            {
                // Iterate through all the items inside the shell and start matching
                for (int PidlIndex = 0; PidlIndex < shellContext.PidlCount; PidlIndex++, TempCounter++)
                {
                    bool FilterMatched = false;

                    // If I want to return only a sepcific number of elements
                    if (Properties.Settings.Default.MaxNumberFilterUpTo > 0 &&
                        Properties.Settings.Default.MaxNumberFilterUpTo <= shellContext.FilterCount)
                    {
                        break;
                    }

                    shellContext.pIFolderView2.Item(PidlIndex, out PidlPtrObj);
                    ReportPecentage = (int)((float)PidlIndex / shellContext.PidlCount * 100.0f);

                    if (PidlPtrObj == IntPtr.Zero)
                    {
                        if (FailedAttempts > MaxFailedAttempts)
                            throw new UserException("Too many failures while interacting with the shell view");

                        FailedAttempts++;
                        continue;
                    }
                    else FailedAttempts = 0;

                    NativeWin32.HResult hr = NativeWin32.SHGetDataFromIDListW(
                        shellContext.pIShellFolder,
                        PidlPtrObj,
                        NativeWin32.SHGDFIL_FINDDATA,
                        out NativeWin32.WIN32_FIND_DATA Pidl_Win32_FindData,
                        NativeWin32.WIN32_FIND_DATA_SIZE);

                    CPidlData PidlData = new CPidlData();

                    if (hr == NativeWin32.HResult.Ok)
                    {
                        PidlData.PidlName = Pidl_Win32_FindData.cFileName;
                        PidlData.dwFileAttributes = Pidl_Win32_FindData.dwFileAttributes;
                        PidlData.FileSize = ((ulong)Pidl_Win32_FindData.nFileSizeHigh << 32) | Pidl_Win32_FindData.nFileSizeLow;
                        PidlData.ftCreationTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftCreationTime);
                        PidlData.ftLastAccessTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftLastAccessTime);
                        PidlData.ftLastWriteTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftLastWriteTime);
                        PidlData.AttributesSet = true;
                    }
                    else
                    {
                        shellContext.pIShellFolder.GetDisplayNameOf
                        (
                            PidlPtrObj,
                            SHGNO.INFOLDER | SHGNO.FORPARSING,
                            shellContext.MarshalPIDLNativeDataHolder.STRRET
                        );

                        NativeWin32.StrRetToBuf
                        (
                            shellContext.MarshalPIDLNativeDataHolder.STRRET,
                            PidlPtrObj,
                            shellContext.MarshalPIDLNativeDataHolder.BUFFER,
                            shellContext.MarshalPIDLNativeDataHolder.MAX_PATH
                        );

                        PidlData.PidlName = shellContext.MarshalPIDLNativeDataHolder.BUFFER.ToString();
                        PidlData.AttributesSet = false;
                    }

                    // Get extension icon
                    string Extension = Path.GetExtension(PidlData.PidlName);

                    if (NativeUtilities.IsAttributeOfFolder(PidlData.dwFileAttributes))
                    {
                        PidlData.IconBitmapSource = StaticImageFactory.ImageList[0];
                    }
                    else
                    {
                        if (!extensionIconDictionary.TryGetValue(Extension, out BitmapSource IconBitmapSource))
                        {
                            string FilePath = Path.Combine(shellContext.LocationUrlBeforeBrowse, PidlData.PidlName);
                            IconBitmapSource = NativeUtilities.GetIconBitmapSource(FilePath, false);
                            extensionIconDictionary[Extension] = IconBitmapSource;
                            PidlData.IconBitmapSource = IconBitmapSource;
                        }
                        else
                        {
                            PidlData.IconBitmapSource = IconBitmapSource;
                        }
                    }

                    if (shellContext.FlagExtendedFilterMod)
                    {
                        // Then all the matching will be done according to an expression tree
                        FilterMatched = ILLanguageFunct(PidlData);
                    }
                    else
                    {
                        // Then all the matching will be done according to settings enabled by the user
                        FilterMatched = FilterBasedOnSettings(PidlData, shellContext.FilterText);
                    }

                    // If there was a match then this pidl must be selected
                    if (FilterMatched)
                    {
                        LocalSelectionBuffer.Add(PidlData);
                        shellContext.pIShellView.SelectItem(PidlPtrObj, SVSI.SVSI_SELECT);
                        shellContext.FilterCount++;
                    }

                    Marshal.FreeCoTaskMem(PidlPtrObj);
                    PidlPtrObj = IntPtr.Zero;

                    if (TempCounter >= NumberOfItemsPerInterval)
                    {
                        ReportAction();
                        TempCounter = 0;
                        shellContext.FilterReportCount++;
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
                ReportAction();

            shellContext.pIFolderView2.SetRedraw(true);

            if (LastException != null) throw LastException;
        }
        private void HandleFilteringNamespaceException(Exception ExceptionParam, out string NormalizedException)
        {
            if (ExceptionParam is UserException) NormalizedException = ((UserException)ExceptionParam).Message;
            else NormalizedException = "The application failed to finalize the task due to some internal error.";
        }
        #endregion





        /*
         * Event handlers
         */

        #region Settings
        private void LoadApplicationSettings()
        {
            Action<IEnumerable<RadioButton>, uint> ApplyButtonConfiguration = (IEnumerable<RadioButton> SButtons, uint Setting) =>
            {
                foreach (RadioButton RButton in SButtons)
                {
                    int tag = Convert.ToInt32(RButton.Tag);
                    if (Setting != tag) continue;
                    RButton.IsChecked = true;
                    HandleSettingChangedUniv(RButton, false);
                    break;
                }
            };

            ApplyButtonConfiguration(SettingsPlacement.Children.OfType<RadioButton>(), Properties.Settings.Default.SettingsPlacementId);
            ApplyButtonConfiguration(SettingsCase.Children.OfType<RadioButton>(), Properties.Settings.Default.SettingsCaseId);

            // Other settings
            MaxFolderPidlCount_Deepscan.Text = Convert.ToString(Properties.Settings.Default.MaxFolderPidlCount_Deepscan);
            MaxNumberFilterUpTo.Text = Convert.ToString(Properties.Settings.Default.MaxNumberFilterUpTo);
            KeepFilterText.IsChecked = Properties.Settings.Default.KeepFilterText;
            DateFilterFormat.Text = Properties.Settings.Default.DateFormat;
            MaxHistory.Text = Properties.Settings.Default.MaxHistory.ToString();

            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                RunStartupCb.IsChecked = key.GetValue(assemblyImageName) != null;
            }
            catch { }
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
            string FilterText = FilterTb.Text.TrimStart();
            shellContext.FilterText = FilterText;

            /* NOTE: consider the following scenario: we start filtering A's items and click on a subitem B.
             * LocationUrlBeforeBrowse is set to the initial path and LocationUrl will be changed as we click any of A's subitems (i.e B,C, ...)
             * However now we decide to change the filter. As we do that we get B's subitems. LocationUrl changes as well. 
             * However LocationUrlBeforeBrowse will not reflect the changes as it is set only when the window is show.
             * 
             * Edit: LocationUrlBeforeBrowse is important because browsing is done via it's value
             */
            shellContext.LocationUrlBeforeBrowse = shellContext.LocationUrl;


            lastTimeTextChanged = DateTime.Now;
            flagStringReadyToProcess = true;
        }
        private void FilterTb_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (!shellContext.FlagRunInBackgroundWorker || workerObject_SelectionProc.IsBusy)
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
            AdvancedSettingsPanel.Visibility = AdvancedSettingsPanel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
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
            if (NativeUtilities.IsAttributeOfFolder(SelectedPidlData.dwFileAttributes))
            {
                if (!BrowseToFolderByDisplayName(FullyQuallifiedItemName))
                {
                    // TODO: log this event
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

                if (NativeUtilities.IsAttributeOfFolder(SearchResult.Pidl.dwFileAttributes))
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
        private void ItemsPanelGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            FilterItemsControlBox.Visibility = Visibility.Visible;
        }
        private void ItemsPanelGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            FilterItemsControlBox.Visibility = Visibility.Collapsed;
        }
        private void Cmd_RunFile_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

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
            if (!GetHoveredPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            DataObject ClpDataObject = new DataObject();
            string[] ClpFileArray = new string[1];
            ClpFileArray[0] = FullyQuallifiedItemName;
            ClpDataObject.SetData(DataFormats.FileDrop, ClpFileArray, true);
            Clipboard.SetDataObject(ClpDataObject, true);
        }
        private void Cmd_InvokeProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            NativeUtilities.ShowFileProperties(FullyQuallifiedItemName);
        }
        private void Cmd_DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (!GetHoveredPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            if (DeletePidlFromSystem(SelectedPidlData))
            {
                int SelectedIndex = ItemsList.SelectedIndex;
                if (SelectedIndex >= 0)
                    listOfPidlData.RemoveAt(SelectedIndex);
            }
        }
        private void BrowseBackBt_Click(object sender, RoutedEventArgs e)
        {
            if (shellContext.LocationUrlBeforeBrowse == shellContext.LocationUrl) return;

            if (!BrowseToFolderByDisplayName(shellContext.LocationUrlBeforeBrowse))
            {
                // TODO: log this event
            }
        }
        private void SaveListBt_Click(object sender, RoutedEventArgs e)
        {
            bool IncludePath = SaveListSettings_Path.IsChecked;
            bool IncludeExtension = SaveListSettings_Ext.IsChecked;

            ComboBoxItem cbi = (ComboBoxItem)SaveListSettings_Format.SelectedItem;
            int itag = Convert.ToInt32(cbi.Tag);
            ExportManger.FORMAT fmt = (ExportManger.FORMAT)itag;
            // TODO: it expects a list of strings
            string ExportData = ExportManger.ExportData(fmt, listOfPidlData.ToList(), IncludePath, IncludeExtension, shellContext.LocationUrl);

            SaveFileDialog SaveDialogWindow = new SaveFileDialog();
            SaveDialogWindow.Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json|XML files (*.xml)|*.xml|C header (*.h;*.hpp;*.c;*.cpp;*.x)|*.h;*.hpp;*.c;*.cpp;*.x|Text files (*.txt)|*.txt|All files (*.*)|*.*";
            SaveDialogWindow.FilterIndex = itag;
            SaveDialogWindow.RestoreDirectory = true;

            if (SaveDialogWindow.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(SaveDialogWindow.FileName, ExportData);
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void LikeBt_Click(object sender, RoutedEventArgs e)
        {
            Process myProcess = new Process();

            try
            {
                myProcess.StartInfo.UseShellExecute = true;
                myProcess.StartInfo.FileName = "https://github.com/ReznicencuBogdan/ExplorerFilterExtension";
                myProcess.Start();
            }
            catch (Exception)
            {
                // TODO: log this exception
            }
        }
        #endregion





        #region Advanced settings panel
        private void MaxFolderPidlCount_Deepscan_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxFolderPidlCount_Deepscan = Convert.ToInt32(MaxFolderPidlCount_Deepscan.Text);
        }
        private void MaxNumberFilterUpTo_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxNumberFilterUpTo = Convert.ToInt32(MaxNumberFilterUpTo.Text);
        }
        private void DateFilterFormat_LostFocus(object sender, RoutedEventArgs e)
        {
            string NewDateFormat = DateFilterFormat.Text.Trim();

            if (DateTimeExtensions.ValidateDateTimeFormatString(NewDateFormat))
            {
                Properties.Settings.Default.DateFormat = NewDateFormat;
            }
            else
            {
                DateFilterFormat.Text = Properties.Settings.Default.DateFormat;
            }
        }
        private void MaxHistory_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxHistory = Convert.ToInt32(MaxHistory.Text);
        }
        private void RunStartupCb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (RunStartupCb.IsChecked == true)
                    key.SetValue(assemblyImageName, assemblyImageLocation);
                else
                    key.DeleteValue(assemblyImageName);
            }
            catch { }
        }
        private void KeepFilterText_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.KeepFilterText = (bool)KeepFilterText.IsChecked;
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
            NativeWin32.HResult hr = shellContext.pIShellBrowser.BrowseObject(
                PidlBrowse,
                SBSP.SBSP_SAMEBROWSER | SBSP.SBSP_WRITENOHISTORY | SBSP.SBSP_NOTRANSFERHIST
            );

            bool FlagResult = hr == NativeWin32.HResult.Ok;

            // Delete referenced data
            Marshal.FreeCoTaskMem(PidlBrowse);

            // When we browse a new folder some of the data changes
            return FlagResult && GatherShellInterfacesLinkedToShell(shellContext.PrevShellWindowHwnd);
        }
        private void UpdateSelectionStatusBarInfo(int? FilterCount = null, int? PidlCount = null)
        {
            FolCountTb.Text = string.Format("{0}/{1}", FilterCount ?? shellContext.FilterCount, PidlCount ?? shellContext.PidlCount);
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
            FullyQuallifiedItemName = Path.Combine(shellContext.LocationUrlBeforeBrowse, SelectedPidlData.PidlName);
        }
        private bool DeletePidlFromSystem(CPidlData SelectedPidlData)
        {
            GetPidlFullPath(SelectedPidlData, out string FullyQuallifiedItemName);
            try
            {
                if (NativeUtilities.IsAttributeOfFolder(SelectedPidlData.dwFileAttributes))
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
        private void CompileCommandDataList()
        {
            CCommandItem CmdWrapperUI = null;
            int LastComIndex = -1;

            foreach (KeyValuePair<string, XPressCommands.ComIndex> CommandEntry in XPressCommands.ComStrToComIndex)
            {
                if (LastComIndex != (int)CommandEntry.Value)
                {
                    if (CmdWrapperUI != null)
                    {
                        listOfAvailableCommands.Add(CmdWrapperUI);
                    }

                    CmdWrapperUI = new CCommandItem();
                    CmdWrapperUI.Name = String.Format("{0} - {1}", CommandEntry.Key, XPressCommands.ComIndexDescription[CommandEntry.Value]);
                    CmdWrapperUI.CmdAlias = "Alias - ";
                    LastComIndex = (int)CommandEntry.Value;
                }
                else CmdWrapperUI.CmdAlias += CommandEntry.Key + " | ";
            }
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
    }
}
