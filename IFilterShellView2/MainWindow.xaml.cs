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
using IFilterShellView2.Native;
using IFilterShellView2.Parser;
using IFilterShellView2.Shell.Interfaces;
using IFilterShellView2.ShellContext;
using Microsoft.Win32;
using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
using SHDocVw;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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


        private Dictionary<string, BitmapSource> ExtensionIconDictionary = new Dictionary<string, BitmapSource>();
        private List<BitmapImage> LocalBitmapImageList = new List<BitmapImage>()
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
            F_REGEX = 8U
        }
        private Dictionary<FilterSettingsFlags, Func<string, string, bool>> SettingsActionMap = new Dictionary<FilterSettingsFlags, Func<string, string, bool>>
        {
            { FilterSettingsFlags.F_STARTSWITH, (pidl_name, input) => pidl_name.StartsWith(input)},
            { FilterSettingsFlags.F_CONTAINS, (pidl_name, input) => pidl_name.Contains(input)},
            { FilterSettingsFlags.F_ENDSWITH, (pidl_name, input) => pidl_name.EndsWith(input)},
            { FilterSettingsFlags.F_REGEX, (pidl_name, input) => RegexFilterCallback(pidl_name, input)}
        };
        private static (string Input, Regex CompiledRegex) FilterRegexContainer = ("", null);
        #endregion Data related to filtering and partial settings





        public MainWindow()
        {
            InitializeComponent();

            Assembly CurrentImageAssembly = Assembly.GetExecutingAssembly();
            AssemblyImageName = CurrentImageAssembly.GetName().Name;
            AssemblyImageLocation = Path.Combine(Path.GetDirectoryName(CurrentImageAssembly.Location), AssemblyImageName + ".exe");

            ThisWindowRef = this;

            Application.Current.Resources["TextControlBorderThemeThickness"] = 0;

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

            ItemsList.ItemsSource = ListOfPidlData;
            CompileCommandDataList();
            CommandList.ItemsSource = ListOfAvailableCommands;

            ListOfHistoryItems = new ObservableCollection<CHistoryItem>(
                SerializeExtensions.MaterializeGenericClassList<CHistoryItem>(Properties.Settings.Default.HistoryListSerialized));

            HistoryList.ItemsSource = ListOfHistoryItems;


            // Add focus to the search bar
            FilterTb.Focus();
            Keyboard.Focus(FilterTb);

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

                IntPtr CurrentMonitorHandle = NativeWin32.MonitorFromWindow(ForegroundWindow, NativeWin32.MONITOR_DEFAULTTONEAREST);
                // var primaryMonitor = NativeWin32.MonitorFromWindow(IntPtr.Zero, NativeWin32.MONITOR_DEFAULTTOPRIMARY);
                // var isInPrimary = currentMonitor == primaryMonitor;

                NativeWin32.MONITORINFOEX MonitorInfo = new NativeWin32.MONITORINFOEX();
                MonitorInfo.Init();
                NativeWin32.GetMonitorInfo(CurrentMonitorHandle, ref MonitorInfo);
                ShellContext.ShellViewRect = MonitorInfo.Monitor;


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
            ItemsList.ItemsSource = null;
            ListOfPidlData.Clear();
            ItemsList.ItemsSource = ListOfPidlData;
            GC.Collect();

            UpdateSelectionStatusBarInfo(0);

            if (!Properties.Settings.Default.KeepFilterText && GoesIntoHidingMode)
            {
                FilterTb.Text = "";
            }

            AdvancedSettingsPanel.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Collapsed;
            ItemsPanel.Visibility = Visibility.Collapsed;
        }



        #region Events that are called on before, after and during the filtering process
        private void Callback_UIOnBeforeFiltering(bool GoesInDeepProcessing = false)
        {
            ResetInterfaceData();

            ItemsPanel.Visibility = Visibility.Visible;

            if (GoesInDeepProcessing)
            {
                ProgressPb.Visibility = Visibility.Visible;
                ItemsPanel.IsEnabled = false;
                FilterTb.IsReadOnly = true;
            }
        }
        private void Callback_UIOnAfterFiltering(bool ReturnedFromDeepProcessing = false, string ErrorMessage = "")
        {
            InfoClass.Message = ErrorMessage;

            if (ReturnedFromDeepProcessing)
            {
                ProgressPb.Visibility = Visibility.Collapsed;
                ItemsPanel.IsEnabled = true;
                FilterTb.IsReadOnly = false;

                if (TempListOfHistoryItems.Count != 0)
                {
                    TempListOfHistoryItems.ForEach(HItem => ListOfHistoryItems.Add(HItem));
                    TempListOfHistoryItems.Clear();
                }
            }

            if (ShellContext.FilterCount == 0)
            {
                ItemsPanel.Visibility = Visibility.Collapsed;
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
                FilterTb.Focus();
                Keyboard.Focus(FilterTb);

                // Set new window properties
                Window MainWindow = Application.Current.MainWindow;
                PresentationSource MainWindowPresentationSource = PresentationSource.FromVisual(MainWindow);
                Matrix m = MainWindowPresentationSource.CompositionTarget.TransformToDevice;
                double thisDpiWidthFactor = m.M11;
                double thisDpiHeightFactor = m.M22;


                // double ShellWidth = SystemParameters.PrimaryScreenWidth * thisDpiWidthFactor;
                //IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(ThisWindowRef).EnsureHandle();
                //NativeWin32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, (int)ShellWidth, (int)ThisWindowRef.Height, NativeWin32.SWP_NOSIZE | NativeWin32.SWP_NOZORDER);

                ThisWindowRef.Width = SystemParameters.PrimaryScreenWidth * thisDpiWidthFactor;
                ThisWindowRef.Left = 0;
                ThisWindowRef.Top = 0;

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

            if (!FilterTb.IsFocused || ShellContext.FilterText.Equals(ShellContext.PrevFilterText) /*|| ShellContext.FilterText.Length == 0*/)
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
                    }
                    ShellContext.FlagRunInBackgroundWorker = true;

                    return;
                }
                else
                {
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
                        
                    // Get extension icon
                    string Extension = Path.GetExtension(PidlData.PidlName);

                    if (NativeUtilities.IsAttributeOfFolder(PidlData.dwFileAttributes))
                    {
                        PidlData.IconBitmapSource = LocalBitmapImageList[0];
                    }
                    else
                    {
                        if (!ExtensionIconDictionary.TryGetValue(Extension, out BitmapSource IconBitmapSource))
                        {
                            string FilePath = Path.Combine(ShellContext.LocationUrlBeforeBrowse, PidlData.PidlName);
                            IconBitmapSource = NativeUtilities.GetIconBitmapSource(FilePath, false);
                            ExtensionIconDictionary[Extension] = IconBitmapSource;
                            PidlData.IconBitmapSource = IconBitmapSource;
                        }
                        else
                        {
                            PidlData.IconBitmapSource = IconBitmapSource;
                        }
                    }

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
                RunStartupCb.IsChecked = key.GetValue(AssemblyImageName) != null;
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

                    PlacementSettingsIc.Glyph = ((FontIcon)crbtn.Content).Glyph;
                    break;
                case "SettingsCase":
                    if (SaveSetting) Properties.Settings.Default.SettingsCaseId = SettingId;

                    CaseSettingsIc.Glyph = ((FontIcon)crbtn.Content).Glyph;
                    break;
            }
        }
        #endregion




        #region Filter input handlers
        private void FilterTb_TextChanged(object sender, TextChangedEventArgs e)
        {
            string FilterText = FilterTb.Text.TrimStart();
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
        private void FilterTb_KeyDown(object sender, KeyEventArgs e)
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
        private void FilterTb_KeyUp(object sender, KeyEventArgs e)
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
        private void ItemsList_PreviewMouseMove(object sender, MouseEventArgs e)
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
        private void BrowseBackBt_Click(object sender, RoutedEventArgs e)
        {
            if (ShellContext.LocationUrlBeforeBrowse == ShellContext.LocationUrl) return;

            if (!BrowseToFolderByDisplayName(ShellContext.LocationUrlBeforeBrowse))
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
                    key.SetValue(AssemblyImageName, AssemblyImageLocation);
                else
                    key.DeleteValue(AssemblyImageName);
            }
            catch { }
        }
        private void KeepFilterText_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.KeepFilterText = (bool)KeepFilterText.IsChecked;
        }
        #endregion



        #region PidlListView Context Menu - Event Handlers
        private void PidlCtx_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            if (DeletePidlFromSystem(SelectedPidlData))
            {
                int SelectedIndex = ItemsList.SelectedIndex;
                if (SelectedIndex >= 0)
                    ListOfPidlData.RemoveAt(SelectedIndex);
            }
        }
        private void PidlCtx_CpyItem_Click(object sender, RoutedEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            DataObject ClpDataObject = new DataObject();
            string[] ClpFileArray = new string[1];
            ClpFileArray[0] = FullyQuallifiedItemName;
            ClpDataObject.SetData(DataFormats.FileDrop, ClpFileArray, true);
            Clipboard.SetDataObject(ClpDataObject, true);
        }
        private void PidlCtx_CpyPath_Click(object sender, RoutedEventArgs e)
        {
            if (!GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)) return;

            Clipboard.SetText(FullyQuallifiedItemName);
        }
        #endregion




        #region History List
        private void HistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (HistoryList.SelectedIndex < 0) return;

            FilterTb.Text = ListOfHistoryItems[HistoryList.SelectedIndex].Command;
        }
        #endregion



        #region Command List
        private void CommandList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (CommandList.SelectedIndex < 0) return;

            FilterTb.Text = "? " + ListOfAvailableCommands[CommandList.SelectedIndex].Name;
        }
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
            var action = SettingsActionMap[(FilterSettingsFlags)SettingsPlacementId];

            if (!action(PidlName, input)) return false;

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
        private void UpdateSelectionStatusBarInfo(int? FilterCount = null, int? PidlCount = null)
        {
            FolCountTb.Text = string.Format("{0}/{1}", FilterCount ?? ShellContext.FilterCount, PidlCount ?? ShellContext.PidlCount);
        }
        private bool GetSelectedPidlAndFullPath(out CPidlData SelectedPidlData, out string FullyQuallifiedItemName)
        {
            int SelectedIndex = ItemsList.SelectedIndex;
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
                get => Window.InfoText.Text;
                set
                {
                    if (value == null || value.Length == 0)
                    {
                        if (Window.InfoPanel.Visibility != Visibility.Collapsed)
                            Window.InfoPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        Window.InfoPanel.Visibility = Visibility.Visible;
                        Window.InfoText.Text = value;
                    }
                }
            }
        }

        private void PlacementSettingsBt_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private void CaseSettingsBt_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }


        private void ShowCommandList(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }
        private void ShowHistoryList(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }
    }
}
