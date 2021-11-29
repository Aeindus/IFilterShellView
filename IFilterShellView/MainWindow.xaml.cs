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
using ModernWpf;
using System.Windows.Media;

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
        private readonly HotkeyFilterManager globalHookObject;
        private readonly DispatcherTimer timerInputFilter;
        private readonly FilterShell filterShell = new FilterShell();
        public readonly MainWindowModelMerger mainWindowModelMerger = new MainWindowModelMerger();

        private readonly int filterAfterDelay = 120;
        private const char keyModExtendedCommandMode = '?';
        private DateTime lastTimeTextChanged;



        public MainWindow()
        {
            this.DataContext = mainWindowModelMerger;

            InitializeComponent();

            // Only Net Framework?
            // AppContext.SetSwitch("Switch.System.Windows.DoNotUsePresentationDpiCapabilityTier2OrGreater", false);

            filterShell.ptrOnUiBefore = Callback_UIOnBeforeFiltering;
            filterShell.ptrOnUiAfter = Callback_UIOnAfterFiltering;
            filterShell.ptrOnUiProgress = Callback_UIReportSelectionProgress;


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
                globalHookObject = new HotkeyFilterManager();
                globalHookObject.AddHotkeys(listOfHotkeys, Callback_GlobalKeyboardHookSafeSta);
            }
            catch (Exception)
            {
                // TODO: critical exception - stop now. Cannot recover
                throw;
            }

            timerInputFilter = new DispatcherTimer();
            timerInputFilter.Tick += TimerInputFilter_OnTick;
            timerInputFilter.Interval = new TimeSpan(0, 0, 0, 0, 300);
        }
        private void Window_Deactivated(object sender, EventArgs e)
        {
            Window_OnCancelOrExit(false);
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Window_OnCancelOrExit(true);
        }
        private void Window_OnCancelOrExit(bool ApplicationIsExiting)
        {
            filterShell.StopAsync();

            // Re-eanble the shell's filter window.
            if (Context.Instance.PrevShellWindowModernSearchBoxHwnd != IntPtr.Zero)
            {
                NativeWin32.EnableWindow(Context.Instance.PrevShellWindowModernSearchBoxHwnd, true);
            }

            ShowSearchResultsPage(false);
            listOfPidlData.Clear();

            // Disable the timer
            timerInputFilter.Stop();
            Context.Instance.HardReset();

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

                this.Opacity = 1;

                Hide();
            }
        }




        #region Events that are called on before, after or during the filtering process
        private void Callback_UIOnBeforeFiltering()
        {
            listOfPidlData.Clear();

            ShowSearchResultsPage(true);
        }
        private void Callback_UIOnAfterFiltering(Exception RuntimeException, bool WasQueryExecuted)
        {
            if (RuntimeException != null)
            {
                string NormalizedException;
                if (RuntimeException is UserException exception)
                {
                    NormalizedException = exception.Message;
                }
                else
                {
                    NormalizedException = "The application failed to finalize the task due to some internal error.";
                }
                NotificationManager.NotificationData NotificationData = NotificationManager.Get("Compiling the command failed", string.Format("The parser encountered the following error while compiling the given command: \n'{0}'", NormalizedException));
                UpdateNotificationData(NotificationData);
            }
            else if (listOfPidlData.Count == 0)
            {
                if (Context.Instance.FilterText.Length == 0)
                {
                    ShowSearchResultsPage(false);
                }
                else if (WasQueryExecuted)
                {
                    UpdateNotificationData(NotificationManager.Notification_EmptyResults);
                }
                else if (Context.Instance.FlagExtendedFilterMod)
                {
                    UpdateNotificationData(NotificationManager.Notification_CommandGiven);
                }
                else if (Context.Instance.FlagTooManyItems)
                {
                    UpdateNotificationData(NotificationManager.Notification_TooManyItems);
                }
            }
        }
        private void Callback_UIReportSelectionProgress(List<CPidlData> ListOfSelections)
        {
            ListOfSelections.ForEach(pidl_data => listOfPidlData.Add(pidl_data));
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
                Context.Instance.EventManager.ResubscribeToExtCloseEvent(ForegroundWindow, () => Window_OnCancelOrExit(false));

                /* Note: Do not move this line. We will create a copy of the current location url and store it inside LocationUrlBefore browse
                 * because we will need a copy when we click on a filtered item. When we click we call GatherShellInterfacesLinkedToShell 
                 * which resets LocationUrl. 
                 */
                Context.Instance.LocationUrlOnStart = Context.Instance.LocationUrl;
            }
        }
        #endregion

        private bool GatherShellInterfacesLinkedToShell(IntPtr ForegroundWindow)
        {
            ShellWindows pIShellWindows = new ShellWindows();
            CIShellBrowser pIShellBrowser;
            CIPersistFolder2 pIPersistFolder2;
            CIShellFolder pIShellFolder;

            foreach (IWebBrowserApp pIWebBrowserApp in pIShellWindows)
            {
                if (pIWebBrowserApp.HWND != (int)ForegroundWindow)
                {
                    continue;
                }

                if (!(pIWebBrowserApp is CIServiceProvider pIServiceProvider))
                {
                    return false;
                }

                pIServiceProvider.QueryService(Service.SID_STopLevelBrowser, typeof(CIShellBrowser).GUID, out object sb);
                pIShellBrowser = (CIShellBrowser)sb;

                if (pIShellBrowser == null)
                {
                    return false;
                }

                pIShellBrowser.QueryActiveShellView(out CIShellView pIShellView);

                if (!(pIShellView is CIFolderView2 pIFolderView2) || pIShellView == null)
                {
                    return false;
                }

                pIFolderView2.GetFolder(typeof(CIPersistFolder2).GUID, out object ppv);
                pIPersistFolder2 = (CIPersistFolder2)ppv; ;
                pIShellFolder = (CIShellFolder)ppv;

                if (pIShellFolder == null)
                {
                    return false;
                }

                // Get the modern search box hwnd
                IntPtr ModernSearchBoxHwnd = WindowExtensions.FindChildWindowByClassName(ForegroundWindow, "ModernSearchBox");

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



        private void TimerInputFilter_OnTick(object sender, EventArgs e)
        {
            TimeSpan DeltaTime = DateTime.Now - lastTimeTextChanged;

            if (DeltaTime.TotalMilliseconds < filterAfterDelay)
            {
                return;
            }
            else if (DeltaTime.TotalMilliseconds > 2000)
            {
                timerInputFilter.Stop();
                return;
            }

            // Handle Context.Instance's exposed interfaces
            if (Context.Instance.pIFolderView2 == null ||
                Context.Instance.pIShellFolder == null ||
                Context.Instance.pIShellView == null)
            {
                return;
            }

            if (!FilterTb.IsFocused || 
                Context.Instance.FilterText.Equals(Context.Instance.PrevFilterText))
            {
                return;
            }

            Context.Instance.PrevFilterText = Context.Instance.FilterText;

            Debug.WriteLine(string.Format("[{0}] executing command: '{1}'", DateTime.Now.ToString(), Context.Instance.FilterText));

            filterShell.StartSync(null, null);
        }


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
                HistoryListFromIObs.RemoveRange(Properties.Settings.Default.MaxHistory / 2, Properties.Settings.Default.MaxHistory);
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
            Context.Instance.FlagExtendedFilterMod = Context.Instance.FilterText.StartsWith(keyModExtendedCommandMode);
            Context.Instance.LocationUrlOnStart = Context.Instance.LocationUrl;

            // Query the view for the number of items that are hosted
            Context.Instance.pIFolderView2.ItemCount(SVGIO.SVGIO_ALLVIEW, out Context.Instance.PidlCount);
            Context.Instance.FlagTooManyItems = Context.Instance.PidlCount >= Properties.Settings.Default.MaxFolderPidlCount_Deepscan;
            Context.Instance.FlagRunInBackgroundWorker =
                Context.Instance.FlagExtendedFilterMod | Context.Instance.FlagTooManyItems;

            if (filterShell.IsBusy)
            {
                filterShell.StopAsync();
            } 
            else
            {
                // Start the timer if it wasn't enabled
                if (!timerInputFilter.IsEnabled)
                {
                    timerInputFilter.Start();
                }
            }

            lastTimeTextChanged = DateTime.Now;
        }
        private void FilterTb_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (!Context.Instance.FlagRunInBackgroundWorker)
                        {
                            return;
                        }

                        // Start worker
                        filterShell.StartAsync(
                            /* on before */
                            () =>
                            {
                                // Disable timer
                                timerInputFilter.Stop();

                                // Goes in deep processing
                                ProgressPb.IsIndeterminate = true;
                                ProgressPb.Visibility = Visibility.Visible;
                                //FilterTb.IsReadOnly = true;
                            },
                            /* on after */
                            (Exception RuntimeException, bool WasQueryExecuted) =>
                            {
                                // No need to re-enable timer
                                // timerInputFilter.Start();

                                // Came out of deep processing
                                ProgressPb.IsIndeterminate = false;
                                ProgressPb.Visibility = Visibility.Collapsed;
                                //FilterTb.IsReadOnly = false;

                                if (RuntimeException == null && Context.Instance.FlagExtendedFilterMod)
                                {
                                    var CommandText = Context.Instance.FilterTextWithoutCommandModifier;
                                    var CommandFound = listOfHistoryItems.Any(o => o.Command.Equals(CommandText));

                                    if (!CommandFound)
                                    {
                                        listOfHistoryItems.Insert(0, new CHistoryItem(CommandText));
                                    }
                                }
                            });

                        break;
                    }
                case Key.Escape:
                    {
                        if (!filterShell.StopAsync())
                        {
                            Window_OnCancelOrExit(false);
                            return;
                        }
                        break;
                    }
                case Key.Up:
                    {
                        if (ItemsList.SelectedIndex > 0)
                        {
                            ItemsList.SelectedIndex--;
                        }
                        else
                        {
                            ItemsList.SelectedIndex = ItemsList.Items.Count - 1;
                        }

                        e.Handled = true;
                        break;
                    }
                case Key.Down:
                    {
                        if (ItemsList.SelectedIndex < ItemsList.Items.Count - 1)
                        {
                            ItemsList.SelectedIndex++;
                        }
                        else
                        {
                            ItemsList.SelectedIndex = 0;
                        }

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
                        if (filterShell.StopAsync())
                        {
                            e.Handled = true;
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
        private void ToggleThemeBt_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            else
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }
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
                else
                {
                    // We browsed to another folder now
                    // Lower opacity to see through
                    this.Opacity = 0.1;
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
                if (!this.Opacity.EpsEq(1))
                {
                    this.Opacity = 1;
                }

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
        private void ItemsList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.XButton1:
                    if (Context.Instance.LocationUrlOnStart == Context.Instance.LocationUrl) return;

                    if (!BrowseBackToParentItem())
                    {
                        // Failed browsing
                    }

                    this.Opacity = 1;
                    break;
                case MouseButton.XButton2:
                    break;
            }
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


        #region History List
        private void HistoryList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (HistoryList.SelectedIndex < 0) return;

            FilterTb.Text = String.Format("{0} {1}", keyModExtendedCommandMode, listOfHistoryItems[HistoryList.SelectedIndex].Command);
        }
        private void ClearHistoryBt_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.HistoryListSerialized = "";
            listOfHistoryItems.Clear();
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
        public void UpdateWindowPositionToFixedPin()
        {
            // Weird .. some unusual behaviour ever since by KB5007186. 
            // Fixed by converting points from screen space to wpf logical space
            // Maybe a rendering rule was enforced ?

            NativeWin32.GetWindowRect(Context.Instance.PrevShellWindowHwnd, out NativeWin32.RECT ShellWindowRect);

            var TopMargin = 4;
            var TransformationMatrix = PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice;
            var TransformedShellWindowRect = new Rect(ShellWindowRect.Left, ShellWindowRect.Top, ShellWindowRect.Right - ShellWindowRect.Left, ShellWindowRect.Bottom - ShellWindowRect.Top);
            TransformedShellWindowRect.Transform(TransformationMatrix);

            NativeWin32.WINDOWPLACEMENT WinPlacement = NativeWin32.WINDOWPLACEMENT.Default;
            NativeWin32.GetWindowPlacement(Context.Instance.PrevShellWindowHwnd, ref WinPlacement);

            if (WinPlacement.ShowCmd.HasFlag(NativeWin32.ShowState.SW_SHOWMAXIMIZED))
            {
                TopMargin = 10;
            }

            double WinLeft = TransformedShellWindowRect.X + TransformedShellWindowRect.Width / 2 - this.ActualWidth / 2;
            double WinTop = TransformedShellWindowRect.Y + TopMargin;
            FilterTb.MaxWidth = FilterTb.ActualWidth;


            NativeWin32.MONITORINFOEX MonitorInfo = new NativeWin32.MONITORINFOEX();
            MonitorInfo.Init();
            IntPtr CurrentMonitorHandle = NativeWin32.MonitorFromWindow(Context.Instance.PrevShellWindowHwnd, NativeWin32.MONITOR_DEFAULTTONEAREST);
            NativeWin32.GetMonitorInfo(CurrentMonitorHandle, ref MonitorInfo);
            Rect TansformedMonitorRect = new Rect()
            {
                X = MonitorInfo.Monitor.Left,
                Y = MonitorInfo.Monitor.Top,
                Width = MonitorInfo.Monitor.Right - MonitorInfo.Monitor.Left,
                Height = MonitorInfo.Monitor.Bottom - MonitorInfo.Monitor.Top
            };
            TansformedMonitorRect.Transform(TransformationMatrix);

            var f1 = WinLeft + this.ActualWidth >= TansformedMonitorRect.X + TansformedMonitorRect.Width;
            var f2 = WinLeft <= TansformedMonitorRect.X;
            var f3 = WinTop <= TansformedMonitorRect.Y;
            var f4 = WinTop + this.ActualHeight >= TansformedMonitorRect.Y + TansformedMonitorRect.Height;

            if (/* Left side */
                f1 ||
                /* Right side */
                f2 || 
                /* Top side */
                f3 || 
                /* Bottom side */
                f4)
            {
                WinLeft = TansformedMonitorRect.X + TansformedMonitorRect.Width / 2 - this.ActualWidth / 2;
                WinTop = MonitorInfo.WorkArea.Top + TopMargin; // + MonitorRect.Height / 2 - this.ActualHeight / 2;
                
                if (WinTop < 0)
                {
                    WinTop += this.ActualHeight / 2;
                }
            }

            this.Left = WinLeft;
            this.Top = WinTop;

            // NativeWin32.RECT CurrentMonitorRect = MonitorInfo.Monitor;
            // double widthDPIFactor = this.GetWindowDPIFactorClass().widthDPIFactor;
            // double ScreenWidth = CurrentMonitorRect.ToRectangle().Width;

            // this.Width = ScreenWidth * widthDPIFactor;
            // this.Left = 0;
            // this.Top = 0;
        }
        private void ShowSearchResultsPage(bool Visible)
        {
            mainWindowModelMerger.SearchPageVisibilityModel.Visible = Visible;
        }
        private void UpdateNotificationData(NotificationManager.NotificationData notification)
        {
            mainWindowModelMerger.SearchPageNoticeTitle.Text = notification.Title;
            mainWindowModelMerger.SearchPageNoticeMessage.Text = notification.Message;
        }



        private ObservableCollection<CPidlData> CompilePidlList()
        {
            return new ObservableCollection<CPidlData>();
        }
        private ObservableCollection<CCommandItem> CompileCommandList()
        {
            ObservableCollection<CCommandItem> LocalListOfCommands = new ObservableCollection<CCommandItem>();
            CCommandItem CmdWrapperUI = null;
            List<string> ListOfAliases = new List<string>();

            int LastComIndex = -1;

            foreach (KeyValuePair<string, XPressCommands.ComIndex> CommandEntry in XPressCommands.ComStrToComIndex)
            {
                if (LastComIndex != (int)CommandEntry.Value)
                {
                    if (CmdWrapperUI != null)
                    {
                        if (ListOfAliases.Count != 0)
                        {
                            CmdWrapperUI.CmdAlias = string.Join(" , ", ListOfAliases);
                        }
                        LocalListOfCommands.Add(CmdWrapperUI);
                    }

                    CmdWrapperUI = new CCommandItem
                    {
                        Name = CommandEntry.Key,
                        Description = XPressCommands.ComIndexDescription[CommandEntry.Value],
                        CmdAlias = "No alias for this command"
                    };
                    ListOfAliases.Clear();
                    LastComIndex = (int)CommandEntry.Value;
                }
                else
                {
                    ListOfAliases.Add(CommandEntry.Key);
                }
            }

            if (CmdWrapperUI != null)
            {
                LocalListOfCommands.Add(CmdWrapperUI);
            }

            return LocalListOfCommands;
        }
        public ObservableCollection<CHistoryItem> CompileHistoryList()
        {
            return new ObservableCollection<CHistoryItem>(SerializeExtensions.MaterializeGenericClassList<CHistoryItem>(Properties.Settings.Default.HistoryListSerialized));
        }
    }
}
