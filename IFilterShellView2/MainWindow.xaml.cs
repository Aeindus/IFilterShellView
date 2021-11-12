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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

        private static readonly List<Key> Keys = new List<Key> { Key.LeftCtrl, Key.F };
        private ObservableCollection<CPidlData> ListOfPidlData = new ObservableCollection<CPidlData>();
        private ObservableCollection<CCommandItem> ListOfAvailableCommands = new ObservableCollection<CCommandItem>();
        private ObservableCollection<CHistoryItem> ListOfHistoryItems = new ObservableCollection<CHistoryItem>();
        private List<CHistoryItem> TempListOfHistoryItems = new List<CHistoryItem>();


        private readonly GlobalKeyboardHook GlobalHookObject;
        private readonly ShellContextContainer ShellContext;
        private readonly CInfoClass InfoClass;

        private BackgroundWorker WorkerObject_SelectionProc;
        private DispatcherTimer DispatcherInputFilter;
        private MainWindow ThisWindowRef;

        private List<BitmapImage> PidlImageList = new List<BitmapImage>()
        {
            ResourceExtensions.LoadBitmapFromResource("ic_folder.ico"),
            ResourceExtensions.LoadBitmapFromResource("ic_file.ico"),
        };


        private readonly string AssemblyImageName;
        private readonly string AssemblyImageLocation;
        private const char KeyModExtendedCommandMode = '?';

        private bool FlagExtendedFilterModNoticeShown = false;
        private readonly int FilterAfterDelay = 120;
        private bool FlagStringReadyToProcess = false;
        private DateTime LastTimeTextChanged;



        #region Data related to filtering and partial settings
        private enum FilterSettingsFlags : uint
        {
            F_STARTSWITH = 1U,
            F_CONTAINS = 2U,
            F_ENDSWITH = 4U,
            F_REGEX = 8U,
            F_CASESENS = 16U,
            F_CASEINSENS = 32U
        }
        private Dictionary<FilterSettingsFlags, Func<string, string, bool>> SettingsActionMap = new Dictionary<FilterSettingsFlags, Func<string, string, bool>>
        {
            { FilterSettingsFlags.F_STARTSWITH, (pidl_name, input) => pidl_name.StartsWith(input)},
            { FilterSettingsFlags.F_CONTAINS, (pidl_name, input) => pidl_name.Contains(input)},
            { FilterSettingsFlags.F_ENDSWITH, (pidl_name, input) => pidl_name.EndsWith(input)},
            { FilterSettingsFlags.F_REGEX, (pidl_name, input) => RegexFilterCallback(pidl_name, input)}
        };
        private static (string Input, Regex CompiledRegex) FilterRegexContainer = ("", null);
        private uint FilterSettings;
        #endregion Data related to filtering and partial settings





        public MainWindow()
        {
            InitializeComponent();

            Assembly CurrentImageAssembly = Assembly.GetExecutingAssembly();
            AssemblyImageName = CurrentImageAssembly.GetName().Name;
            AssemblyImageLocation = Path.Combine(Path.GetDirectoryName(CurrentImageAssembly.Location), AssemblyImageName + ".exe");

            ThisWindowRef = this;

            // Initialize application settings
            LoadApplicationSettings();

            // Initialize regular data types
            InfoClass = new CInfoClass(this); // not an external reference so it is theoretically ok

            // Initialize a background worker responsible for the heavy selection task
            WorkerObject_SelectionProc = new BackgroundWorker();
            WorkerObject_SelectionProc.DoWork += new DoWorkEventHandler(WorkerCallback_SelectionProc_Task);
            WorkerObject_SelectionProc.RunWorkerCompleted += new RunWorkerCompletedEventHandler(WorkerCallback_SelectionProc_Completed);
            WorkerObject_SelectionProc.ProgressChanged += new ProgressChangedEventHandler(WorkerCallback_SelectionProc_Progress);
            WorkerObject_SelectionProc.WorkerReportsProgress = true;
            WorkerObject_SelectionProc.WorkerSupportsCancellation = true;

            XML_ItemsList.ItemsSource = ListOfPidlData;
            CompileCommandDataList();
            XML_CommandList.ItemsSource = ListOfAvailableCommands;

            ListOfHistoryItems = new ObservableCollection<CHistoryItem>(
                SerializeExtensions.MaterializeGenericClassList<CHistoryItem>(Properties.Settings.Default.HistoryListSerialized));

            XML_HistoryList.ItemsSource = ListOfHistoryItems;


            // Add focus to the search bar
            XML_FilterTb.Focus();
            Keyboard.Focus(XML_FilterTb);

            try
            {
                // This can throw - it handles unmanaged resources and it's a critical component
                ShellContext = new ShellContextContainer();
                GlobalHookObject = new GlobalKeyboardHook();
                GlobalHookObject.AddHotkeys(Keys, Callback_GlobalKeyboardHookSafeSta);
            }
            catch (Exception)
            {
                // TODO: critical exception - stop now. Cannot recover
                throw;
            }

            DispatcherInputFilter = new DispatcherTimer();
            DispatcherInputFilter.Tick += Callback_TimerPulse;
            DispatcherInputFilter.Interval = new TimeSpan(0, 0, 0, 0, 300);
        }
        private void Window_Deactivated(object sender, EventArgs e)
        {
            Callback_OnWindowCancellOrExit(false);
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Callback_OnWindowCancellOrExit(true);
        }









        private void LoadApplicationSettings()
        {
            // Load this setting and update UI
            FilterSettings = Properties.Settings.Default.FilterSettings;
            IEnumerable<ToggleButton> lorbtns = XML_ToolbarSettings.Children.OfType<RadioButton>();
            foreach (RadioButton rb in lorbtns)
            {
                uint tag = Convert.ToUInt32(rb.Tag);
                if ((FilterSettings & tag) == tag) rb.IsChecked = true;
            }

            // Other settings
            XML_MaxFolderPidlCount_Deepscan.Text = Convert.ToString(Properties.Settings.Default.MaxFolderPidlCount_Deepscan);
            XML_MaxNumberFilterUpTo.Text = Convert.ToString(Properties.Settings.Default.MaxNumberFilterUpTo);
            XML_KeepFilterText.IsChecked = Properties.Settings.Default.KeepFilterText;
            XML_DateFilterFormat.Text = Properties.Settings.Default.DateFormat;
            XML_MaxHistory.Text = Properties.Settings.Default.MaxHistory.ToString();


            //
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                XML_RunStartupCb.IsChecked = key.GetValue(AssemblyImageName) != null;
            }
            catch { }
        }
        private void SaveApplicationSettings()
        {
            List<CHistoryItem> HistoryListFromIObs = ListOfHistoryItems.ToList();
            if (ListOfHistoryItems.Count > Properties.Settings.Default.MaxHistory)
            {
                HistoryListFromIObs.RemoveRange(0, Properties.Settings.Default.MaxHistory / 2);
            }

            Properties.Settings.Default.HistoryListSerialized =
                SerializeExtensions.SerializeGenericClassList(HistoryListFromIObs);

            Properties.Settings.Default.Save();
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

                NativeWin32.GetWindowRect(ForegroundWindow, out ShellContext.ShellViewRect);

                // Set the rest of the ShellContext class
                ShellContext.pIShellBrowser = pIShellBrowser;
                ShellContext.pIFolderView2 = pIFolderView2;
                ShellContext.pIShellFolder = pIShellFolder;
                ShellContext.pIShellView = pIShellView;

                // This can occur if the focused shell window is presenting a virtual namespace
                if (pIWebBrowserApp.LocationURL == null || pIWebBrowserApp.LocationURL.Length == 0)
                    return false;

                ShellContext.LocationUrl = new Uri(pIWebBrowserApp.LocationURL).LocalPath;

                return true;
            }

            return false;
        }
        private void ResetInterfaceData(bool GoesIntoHidingMode = false)
        {
            ListOfPidlData.Clear();
            UpdateSelectionStatusBarInfo(0);

            if (!Properties.Settings.Default.KeepFilterText && GoesIntoHidingMode)
            {
                XML_FilterTb.Text = "";
            }

            XML_HistoryPanel.Visibility = Visibility.Collapsed;
            XML_AdvancedSettingsPanel.Visibility = Visibility.Collapsed;
            XML_InfoPanel.Visibility = Visibility.Collapsed;
            XML_ItemsPanel.Visibility = Visibility.Collapsed;
            XML_CommandPanel.Visibility = Visibility.Collapsed;
        }





        #region Events that are called on before, after and during the filtering process
        private void Callback_UIOnBeforeFiltering(bool GoesInDeepProcessing = false)
        {
            ResetInterfaceData();

            XML_ItemsPanel.Visibility = Visibility.Visible;

            if (GoesInDeepProcessing)
            {
                XML_ProgressPb.Visibility = Visibility.Visible;
                XML_ToolbarSettings.IsEnabled = false;
                XML_ItemsPanel.IsEnabled = false;
                XML_FilterTb.IsReadOnly = true;
            }
        }
        private void Callback_UIOnAfterFiltering(bool ReturnedFromDeepProcessing = false, string ErrorMessage = "")
        {
            InfoClass.Message = ErrorMessage;

            if (ReturnedFromDeepProcessing)
            {
                XML_ProgressPb.Visibility = Visibility.Collapsed;
                XML_ToolbarSettings.IsEnabled = true;
                XML_ItemsPanel.IsEnabled = true;
                XML_FilterTb.IsReadOnly = false;

                if (TempListOfHistoryItems.Count != 0)
                {
                    TempListOfHistoryItems.ForEach(HItem => ListOfHistoryItems.Add(HItem));
                    TempListOfHistoryItems.Clear();

                    if (XML_HistoryPanel.Visibility == Visibility.Collapsed)
                    {
                        XML_HistoryPanel.Visibility = Visibility.Visible;
                    }
                }
            }

            if (ShellContext.FilterCount == 0)
            {
                XML_ItemsPanel.Visibility = Visibility.Collapsed;
            }
        }
        private void Callback_UIReportSelectionProgress(int ReportPecentage, List<CPidlData> ListOfSelections)
        {
            ListOfSelections.ForEach(pidl_data => ListOfPidlData.Add(pidl_data));
            UpdateSelectionStatusBarInfo();
        }
        #endregion



        #region General callbacks that handle the filtering process
        private void Callback_OnWindowCancellOrExit(bool ApplicationIsExiting)
        {
            if (WorkerObject_SelectionProc.IsBusy) WorkerObject_SelectionProc.CancelAsync();

            DispatcherInputFilter.Stop();
            ShellContext.Reset();

            ResetInterfaceData(true);

            // Save settings here as a precaution
            SaveApplicationSettings();

            if (ApplicationIsExiting)
            {
                DispatcherInputFilter.Stop();
                GlobalHookObject.Dispose();
                ShellContext.Dispose();
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
            ShellContext.PrevShellWindowHwnd = NativeWin32.GetForegroundWindow();

            if (GatherShellInterfacesLinkedToShell(ShellContext.PrevShellWindowHwnd))
            {
                // Show the window and make it visible
                ThisWindowRef.Show();
                ThisWindowRef.Activate(); // must be set before ?
                ThisWindowRef.Focus();

                // Make sure that the window is active using native calls
                // WindowExtensions.ActivateWindow(ThisWindowRef);

                // Add focus to the search bar
                XML_FilterTb.Focus();
                Keyboard.Focus(XML_FilterTb);

                // Set new window properties
                // System.Windows.SystemParameters.PrimaryScreenWidth take current screen width into consideration
                int ShellWidth = ShellContext.ShellViewRect.Right - ShellContext.ShellViewRect.Left;
                ThisWindowRef.Left = ShellContext.ShellViewRect.Left + ShellWidth / 2 - ThisWindowRef.ActualWidth / 2;
                ThisWindowRef.Top = ShellContext.ShellViewRect.Top;
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(ThisWindowRef).EnsureHandle();
                // NativeWin32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, NativeWin32.SWP_NOSIZE | NativeWin32.SWP_NOZORDER);

                // ThisWindowRef.Width = ShrinkedWidth;

                // Subscribe to the shell's message pump and filter close messages
                ShellContext.EventManager.ResubscribeToExtCloseEvent(ShellContext.PrevShellWindowHwnd, Callback_OnShellWindowCloseEvent);

                /* Note: Do not move this line. We will create a copy of the current location url and store it inside LocationUrlBefore browse
                 * because we will need a copy when we click on a filtered item. When we click we call GatherShellInterfacesLinkedToShell 
                 * which resets LocationUrl. 
                 */
                ShellContext.LocationUrlBeforeBrowse = ShellContext.LocationUrl;

                // Enable dispatcher timer
                DispatcherInputFilter.Start();
            }
        }
        private void Callback_TimerPulse(object sender, EventArgs e)
        {
            TimeSpan DeltaTime = DateTime.Now - LastTimeTextChanged;

            if (DeltaTime.TotalMilliseconds < FilterAfterDelay || !FlagStringReadyToProcess)
                return;

            FlagStringReadyToProcess = false;

            // Handle ShellContext's exposed interfaces
            if (ShellContext.pIFolderView2 == null || ShellContext.pIShellFolder == null || ShellContext.pIShellView == null)
                return;

            if (!XML_FilterTb.IsFocused || ShellContext.FilterText.Equals(ShellContext.PrevFilterText) /*|| ShellContext.FilterText.Length == 0*/)
                return;

            string ErrorString = "";
            ShellContext.PrevFilterText = ShellContext.FilterText;
            ShellContext.FlagExtendedFilterMod = ShellContext.FilterText.StartsWith(KeyModExtendedCommandMode);

            try
            {
                // Query the view for the number of items that are hosted
                ShellContext.pIFolderView2.ItemCount(SVGIO.SVGIO_ALLVIEW, out ShellContext.PidlCount);

                // If I want to enter a command
                if (ShellContext.FlagExtendedFilterMod)
                {
                    if (!FlagExtendedFilterModNoticeShown)
                    {
                        // Show a notification
                        InfoClass.Message = "You are about to issue a command. Press [Enter] to compile and run it. If you want to abort then press [Backspace] or [Escape].";
                        FlagExtendedFilterModNoticeShown = true;

                        // Also show the command history list
                        if (ListOfHistoryItems.Count != 0) XML_HistoryPanel.Visibility = Visibility.Visible;
                    }
                    ShellContext.FlagRunInBackgroundWorker = true;

                    return;
                }
                else
                {
                    XML_HistoryPanel.Visibility = Visibility.Collapsed;
                    FlagExtendedFilterModNoticeShown = false;
                }

                Debug.WriteLine(string.Format("[{0}] executing command: '{1}'", DateTime.Now.ToString(), ShellContext.FilterText));

                Callback_UIOnBeforeFiltering();

                // Check if the number of items in folder is greate than the maximum accepted
                if (ShellContext.PidlCount >= Properties.Settings.Default.MaxFolderPidlCount_Deepscan)
                {
                    InfoClass.Message = "Too many items in this folder. Press [Enter] to start a heavy iteration. If you want to abort then press [Backspace] or [Escape].";
                    ShellContext.FlagRunInBackgroundWorker = true;
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

            ShellContext.FlagRunInBackgroundWorker = false;
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
            int NumberOfIntervals = Math.Min(10, ShellContext.PidlCount);
            int NumberOfItemsPerInterval = Math.Min(ShellContext.PidlCount / NumberOfIntervals, NumberOfIntervals);
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
            ShellContext.FilterReportCount = 0;
            ShellContext.FilterCount = 0;

            ShellContext.pIFolderView2.SetRedraw(false);
            ShellContext.pIShellView.SelectItem(IntPtr.Zero, SVSI.SVSI_DESELECTOTHERS);


            // After deselecting all items check if the filter is empty
            if (ShellContext.FilterText.Length == 0)
            {
                goto LabelRestoreState;
            }


            // Compile the predicate chain if needed
            if (ShellContext.FlagExtendedFilterMod)
            {
                XPressParser XLanguageParser = new XPressParser(ShellContext.FilterText[1..]);
                ILLanguageFunct = XLanguageParser.Compile();

                if (ILLanguageFunct == null)
                {
                    throw new UserException("Parser couldn't compile the given command");
                }

                // Save the command in the list
                TempListOfHistoryItems.Add(new CHistoryItem(ShellContext.FilterText));
            }

            try
            {
                // Iterate through all the items inside the shell and start matching
                for (int PidlIndex = 0; PidlIndex < ShellContext.PidlCount; PidlIndex++, TempCounter++)
                {
                    bool FilterMatched = false;

                    // If I want to return only a sepcific number of elements
                    if (Properties.Settings.Default.MaxNumberFilterUpTo > 0 &&
                        Properties.Settings.Default.MaxNumberFilterUpTo <= ShellContext.FilterCount)
                    {
                        break;
                    }

                    ShellContext.pIFolderView2.Item(PidlIndex, out PidlPtrObj);
                    ReportPecentage = (int)((float)PidlIndex / ShellContext.PidlCount * 100.0f);

                    if (PidlPtrObj == IntPtr.Zero)
                    {
                        if (FailedAttempts > MaxFailedAttempts)
                            throw new UserException("Too many failures while interacting with the shell view");

                        FailedAttempts++;
                        continue;
                    }
                    else FailedAttempts = 0;

                    NativeWin32.HResult hr = NativeWin32.SHGetDataFromIDListW(
                        ShellContext.pIShellFolder,
                        PidlPtrObj,
                        NativeWin32.SHGDFIL_FINDDATA,
                        out NativeWin32.WIN32_FIND_DATA Pidl_Win32_FindData,
                        NativeWin32.WIN32_FIND_DATA_SIZE);

                    CPidlData PidlData = new CPidlData();

                    if (hr == NativeWin32.HResult.Ok)
                    {
                        PidlData.PidlName = Pidl_Win32_FindData.cFileName;
                        PidlData.dwFileAttributes = Pidl_Win32_FindData.dwFileAttributes;
                        PidlData.FileSize = ((ulong)Pidl_Win32_FindData.nFileSizeHigh << 32) | Pidl_Win32_FindData.nFileSizeHigh;
                        PidlData.ftCreationTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftCreationTime);
                        PidlData.ftLastAccessTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftLastAccessTime);
                        PidlData.ftLastWriteTime = FileTimeExtension.ToDateTime(Pidl_Win32_FindData.ftLastWriteTime);
                        PidlData.AttributesSet = true;
                    }
                    else
                    {
                        ShellContext.pIShellFolder.GetDisplayNameOf
                        (
                            PidlPtrObj,
                            SHGNO.INFOLDER | SHGNO.FORPARSING,
                            ShellContext.MarshalPIDLNativeDataHolder.STRRET
                        );

                        NativeWin32.StrRetToBuf
                        (
                            ShellContext.MarshalPIDLNativeDataHolder.STRRET,
                            PidlPtrObj,
                            ShellContext.MarshalPIDLNativeDataHolder.BUFFER,
                            ShellContext.MarshalPIDLNativeDataHolder.MAX_PATH
                        );

                        PidlData.PidlName = ShellContext.MarshalPIDLNativeDataHolder.BUFFER.ToString();
                        PidlData.AttributesSet = false;
                    }


                    if (IsPidlDataFolder(PidlData))
                        PidlData.BmpImage = PidlImageList[0];
                    else
                        PidlData.BmpImage = PidlImageList[1];


                    if (ShellContext.FlagExtendedFilterMod)
                    {
                        // Then all the matching will be done according to an expression tree
                        FilterMatched = ILLanguageFunct(PidlData);
                    }
                    else
                    {
                        // Then all the matching will be done according to settings enabled by the user
                        FilterMatched = FilterBasedOnSettings(PidlData, ShellContext.FilterText);
                    }

                    // If there was a match then this pidl must be selected
                    if (FilterMatched)
                    {
                        LocalSelectionBuffer.Add(PidlData);
                        ShellContext.pIShellView.SelectItem(PidlPtrObj, SVSI.SVSI_SELECT);
                        ShellContext.FilterCount++;
                    }

                    Marshal.FreeCoTaskMem(PidlPtrObj);
                    PidlPtrObj = IntPtr.Zero;

                    if (TempCounter >= NumberOfItemsPerInterval)
                    {
                        ReportAction();
                        TempCounter = 0;
                        ShellContext.FilterReportCount++;
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

            ShellContext.pIFolderView2.SetRedraw(true);

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

        #region Toolbar Left Settings
        private void XML_ToolbarLeft_Setting_Changed(object sender, RoutedEventArgs e)
        {
            RadioButton crbtn = sender as RadioButton;
            uint tag = Convert.ToUInt32(crbtn.Tag);

            IEnumerable<ToggleButton> SameGroup = XML_ToolbarSettings.Children.OfType<RadioButton>()
                          .Where(c => crbtn.GroupName.Equals(c.GroupName));

            // Clear that bit in every filter in the same group
            foreach (RadioButton rb in SameGroup)
            {
                FilterSettings &= ~Convert.ToUInt32(rb.Tag);
            }

            // Set the bit
            FilterSettings |= tag;

            Properties.Settings.Default.FilterSettings = FilterSettings;
        }
        #endregion




        #region Filter input handlers
        private void XML_FilterTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            string FilterText = XML_FilterTb.Text.TrimStart();
            ShellContext.FilterText = FilterText;

            /* NOTE: consider the following scenario: we start filtering A's items and click on a subitem B.
             * LocationUrlBeforeBrowse is set to the initial path and LocationUrl will be changed as we click any of A's subitems (i.e B,C, ...)
             * However now we decide to change the filter. As we do that we get B's subitems. LocationUrl changes as well. 
             * However LocationUrlBeforeBrowse will not reflect the changes as it is set only when the window is show.
             * 
             * Edit: LocationUrlBeforeBrowse is important because browsing is done via it's value
             */
            ShellContext.LocationUrlBeforeBrowse = ShellContext.LocationUrl;


            LastTimeTextChanged = DateTime.Now;
            FlagStringReadyToProcess = true;
        }
        private void XML_FilterTb_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (!ShellContext.FlagRunInBackgroundWorker || WorkerObject_SelectionProc.IsBusy)
                            return;

                        // Start worker
                        Callback_UIOnBeforeFiltering(true);
                        WorkerObject_SelectionProc.RunWorkerAsync();
                        break;
                    }
                case Key.Escape:
                    {
                        if (!WorkerObject_SelectionProc.IsBusy)
                        {
                            Callback_OnWindowCancellOrExit(false);
                            return;
                        }

                        WorkerObject_SelectionProc.CancelAsync();
                        break;
                    }
                default: break;
            }
        }
        private void XML_FilterTb_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Back:
                    {
                        if (WorkerObject_SelectionProc.IsBusy)
                        {
                            WorkerObject_SelectionProc.CancelAsync();
                            e.Handled = true;
                            return;
                        }
                        break;
                    }
                default: break;
            }
        }
        #endregion




        #region Toolbar Right Settings
        private void XML_ShowCommandBt_Click(object sender, RoutedEventArgs e)
        {
            XML_CommandPanel.Visibility = XML_CommandPanel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
        }
        private void XML_LikeBt_Click(object sender, RoutedEventArgs e)
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
        private void XML_SettingsBt_Click(object sender, RoutedEventArgs e)
        {
            XML_AdvancedSettingsPanel.Visibility = XML_AdvancedSettingsPanel.IsVisible ? Visibility.Collapsed : Visibility.Visible;
        }
        private void XML_ExitBt_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        #endregion





        #region Item List Viewer - Folders and files
        private void XML_ItemsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            // Open eplorer at selected folder
            if (IsPidlDataFolder(SelectedPidlData))
            {
                if (!BrowseToFolderByDisplayName(FullyQuallifiedItemName))
                {
                    // TODO: log this event
                }
            }
            else
            {
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
        }
        private void XML_ItemsList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (e.Source != null)
                {
                    if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

                    DataObject dragObj = new DataObject();
                    dragObj.SetFileDropList(new System.Collections.Specialized.StringCollection() { FullyQuallifiedItemName });
                    DragDrop.DoDragDrop((System.Windows.Controls.ListView)e.Source, dragObj, DragDropEffects.Copy);
                }
            }
        }
        private void XML_BrowseBackBt_Click(object sender, RoutedEventArgs e)
        {
            if (ShellContext.LocationUrlBeforeBrowse == ShellContext.LocationUrl) return;

            if (!BrowseToFolderByDisplayName(ShellContext.LocationUrlBeforeBrowse))
            {
                // TODO: log this event
            }
        }
        private void XML_ClearListBt_Click(object sender, RoutedEventArgs e)
        {
            ListOfPidlData.Clear();
            XML_ItemsPanel.Visibility = Visibility.Collapsed;
        }
        private void XML_SaveListBt_Click(object sender, RoutedEventArgs e)
        {
            bool IncludePath = XML_SaveListSettings_Path.IsChecked;
            bool IncludeExtension = XML_SaveListSettings_Ext.IsChecked;

            ComboBoxItem cbi = (ComboBoxItem)XML_SaveListSettings_Format.SelectedItem;
            int itag = Convert.ToInt32(cbi.Tag);
            ExportManger.FORMAT fmt = (ExportManger.FORMAT)itag;
            // TODO: it expects a list of strings
            string ExportData = ExportManger.ExportData(fmt, ListOfPidlData.ToList(), IncludePath, IncludeExtension, ShellContext.LocationUrl);

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
                InfoClass.Message = "Failed exporting the specified data to a file. Try again";
                throw;
            }
        }
        #endregion





        #region Advanced settings panel
        private void XML_MaxFolderPidlCount_Deepscan_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxFolderPidlCount_Deepscan = Convert.ToInt32(XML_MaxFolderPidlCount_Deepscan.Text);
        }
        private void XML_MaxNumberFilterUpTo_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxNumberFilterUpTo = Convert.ToInt32(XML_MaxNumberFilterUpTo.Text);
        }
        private void XML_DateFilterFormat_LostFocus(object sender, RoutedEventArgs e)
        {
            string NewDateFormat = XML_DateFilterFormat.Text.Trim();

            if (DateTimeExtensions.ValidateDateTimeFormatString(NewDateFormat))
            {
                Properties.Settings.Default.DateFormat = NewDateFormat;
            }
            else
            {
                XML_DateFilterFormat.Text = Properties.Settings.Default.DateFormat;
            }
        }
        private void XML_MaxHistory_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxHistory = Convert.ToInt32(XML_MaxHistory.Text);
        }
        private void XML_RunStartupCb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (XML_RunStartupCb.IsChecked == true)
                    key.SetValue(AssemblyImageName, AssemblyImageLocation);
                else
                    key.DeleteValue(AssemblyImageName);
            }
            catch { }
        }
        private void XML_KeepFilterText_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.KeepFilterText = (bool)XML_KeepFilterText.IsChecked;
        }
        #endregion



        #region PidlListView Context Menu - Event Handlers
        private void XML_PidlCtx_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            if (DeletePidlFromSystem(SelectedPidlData))
            {
                int SelectedIndex = XML_ItemsList.SelectedIndex;
                if (SelectedIndex >= 0)
                    ListOfPidlData.RemoveAt(SelectedIndex);
            }
        }
        private void XML_PidlCtx_CpyItem_Click(object sender, RoutedEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            DataObject ClpDataObject = new DataObject();
            string[] ClpFileArray = new string[1];
            ClpFileArray[0] = FullyQuallifiedItemName;
            ClpDataObject.SetData(DataFormats.FileDrop, ClpFileArray, true);
            Clipboard.SetDataObject(ClpDataObject, true);
        }
        private void XML_PidlCtx_CpyPath_Click(object sender, RoutedEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            Clipboard.SetText(FullyQuallifiedItemName);
        }
        #endregion




        #region History List
        private void XML_HistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (XML_HistoryList.SelectedIndex < 0) return;

            XML_FilterTb.Text = ListOfHistoryItems[XML_HistoryList.SelectedIndex].Command;
        }
        #endregion



        #region Command List
        private void XML_CommandList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (XML_CommandList.SelectedIndex < 0) return;

            XML_FilterTb.Text = "? " + ListOfAvailableCommands[XML_CommandList.SelectedIndex].Name;
        }
        #endregion




        /*
         * Helpers and extensions
         */

        private bool FilterBasedOnSettings(CPidlData PidlData, string input)
        {
            if ((FilterSettings & (uint)FilterSettingsFlags.F_CASEINSENS) == (uint)FilterSettingsFlags.F_CASEINSENS)
            {
                PidlData.PidlName = PidlData.PidlName.ToLower();
                input = input.ToLower();
            }

            // Find applicable actions
            var actions = SettingsActionMap.Where(kvp => (FilterSettings & (uint)kvp.Key) == (uint)kvp.Key)
                                       .Select(kvp => kvp.Value);

            foreach (var action in actions)
                if (!action(PidlData.PidlName, input)) return false;

            return true;
        }
        private static bool RegexFilterCallback(string pidl_name, string input)
        {
            if (!FilterRegexContainer.Input.Equals(input))
            {
                FilterRegexContainer.CompiledRegex = new Regex(input, RegexOptions.Compiled);
                FilterRegexContainer.Input = input;
            }

            return FilterRegexContainer.Item2.Match(pidl_name).Success;
        }

        private bool BrowseToFolderByDisplayName(string FullyQuallifiedItemName)
        {
            // Parse display name to pidl
            IntPtr PidlBrowse = NativeWin32.ConvertItemNameToPidl(FullyQuallifiedItemName);

            // Is the conversion succesful
            if (PidlBrowse == IntPtr.Zero) return false;

            // Browse to selected folder
            NativeWin32.HResult hr = ShellContext.pIShellBrowser.BrowseObject(
                PidlBrowse,
                SBSP.SBSP_SAMEBROWSER | SBSP.SBSP_WRITENOHISTORY | SBSP.SBSP_NOTRANSFERHIST
            );

            bool FlagResult = hr == NativeWin32.HResult.Ok;

            // Delete referenced data
            Marshal.FreeCoTaskMem(PidlBrowse);

            // When we browse a new folder some of the data changes
            return FlagResult && GatherShellInterfacesLinkedToShell(ShellContext.PrevShellWindowHwnd);
        }
        private bool IsPidlDataFolder(CPidlData PidlData)
        {
            return PidlData.dwFileAttributes == 0x10;
        }
        private void UpdateSelectionStatusBarInfo(int? FilterCount = null, int? PidlCount = null)
        {
            XML_FolCountTb.Text = string.Format("{0}/{1}", FilterCount ?? ShellContext.FilterCount, PidlCount ?? ShellContext.PidlCount);
        }
        private bool GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            int SelectedIndex = XML_ItemsList.SelectedIndex;
            SelectedPidlData = null;
            FullyQuallifiedItemName = "";

            if (SelectedIndex < 0) return false;

            SelectedPidlData = ListOfPidlData[SelectedIndex];
            GetPidlFullPath(SelectedPidlData, out FullyQuallifiedItemName);
            return true;
        }
        private void GetPidlFullPath(CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            FullyQuallifiedItemName = Path.Combine(ShellContext.LocationUrlBeforeBrowse, SelectedPidlData.PidlName);
        }
        private bool DeletePidlFromSystem(CPidlData SelectedPidlData)
        {
            GetPidlFullPath(SelectedPidlData, out string FullyQuallifiedItemName);
            try
            {
                if (IsPidlDataFolder(SelectedPidlData))
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
                        ListOfAvailableCommands.Add(CmdWrapperUI);
                    }

                    CmdWrapperUI = new CCommandItem();
                    CmdWrapperUI.Name = CommandEntry.Key;
                    CmdWrapperUI.Description = ": " + XPressCommands.ComIndexDescription[CommandEntry.Value];
                    CmdWrapperUI.CmdAlias = "/" + CommandEntry.Key + "/";
                    LastComIndex = (int)CommandEntry.Value;
                }
                else CmdWrapperUI.CmdAlias += CommandEntry.Key + "/";
            }
        }




        private class CInfoClass
        {
            private MainWindow Window;
            public CInfoClass(MainWindow window) => Window = window;

            public string Message
            {
                get => Window.XML_InfoText.Text;
                set
                {
                    if (value == null || value.Length == 0)
                    {
                        if (Window.XML_InfoPanel.Visibility != Visibility.Collapsed)
                            Window.XML_InfoPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        Window.XML_InfoPanel.Visibility = Visibility.Visible;
                        Window.XML_InfoText.Text = value;
                    }
                }
            }
        }
    }
}
