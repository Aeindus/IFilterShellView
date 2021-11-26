using IFilterShellView.Exceptions;
using IFilterShellView.Filter;
using IFilterShellView.Native;
using IFilterShellView.Parser;
using IFilterShellView.Shell.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace IFilterShellView.Program
{
    public class FilterShell
    {
        public delegate void DelegateOnUIBefore();
        public delegate void DelegateOnUIAfter(Exception RuntimeException, bool WasQueryExecuted);
        public delegate void DelegateOnUIProgress(List<CPidlData> x);



        private readonly BackgroundWorker workerObject;
        public DelegateOnUIBefore ptrOnUiBefore { get; set; }
        public DelegateOnUIAfter ptrOnUiAfter { get; set; }
        public DelegateOnUIProgress ptrOnUiProgress { get; set; }

        private DelegateOnUIAfter ptrUnscopedOnUiAfter;


        public FilterShell()
        {
            workerObject = new BackgroundWorker();
            workerObject.DoWork += new DoWorkEventHandler(Worker_CallbackMain);
            workerObject.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_CallbackDone);
            workerObject.ProgressChanged += new ProgressChangedEventHandler(Worker_CallbackProgress);
            workerObject.WorkerReportsProgress = true;
            workerObject.WorkerSupportsCancellation = true;
        }





        #region Worker registered events
        private void Worker_CallbackMain(object sender, DoWorkEventArgs e)
        {
            ProcessingUnit(sender as BackgroundWorker);
        }
        private void Worker_CallbackProgress(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState != null)
            {
                ptrOnUiProgress((List<CPidlData>)e.UserState);
            }
        }
        private void Worker_CallbackDone(object sender, RunWorkerCompletedEventArgs e)
        {
            Exception RuntimeException = e.Error;

            ptrOnUiAfter(RuntimeException, true);
            ptrUnscopedOnUiAfter?.Invoke(RuntimeException, true);

            Context.Instance.FlagRunInBackgroundWorker = false;
        }
        #endregion Worker registered events





        #region The actual processing unit - handles both filtering and exception handling
        private void ProcessingUnit(BackgroundWorker worker)
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
                    ptrOnUiProgress(NewList);
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
                XPressParser XLanguageParser = new XPressParser(Context.Instance.FilterTextWithoutCommandModifier);
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
                        {
                            throw new UserException("Too many failures while interacting with the shell view");
                        }

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
        #endregion




        public void StartSync(
            DelegateOnUIBefore ptrScopedOnUiBefore,
            DelegateOnUIAfter ptrScopedOnUiAfter
            )
        {
            if (!workerObject.IsBusy)
            {
                ptrOnUiBefore();
                ptrScopedOnUiBefore?.Invoke();
                bool wasQueryExecuted = false;
                Exception RuntimeException = null;

                try
                {
                    if (Context.Instance.FlagRunInBackgroundWorker)
                    {
                        return;
                    }

                    ProcessingUnit(null);
                    wasQueryExecuted = true;
                }
                catch (Exception ExceptionMessage)
                {
                    RuntimeException = ExceptionMessage;
                }
                finally
                {
                    ptrOnUiAfter(RuntimeException, wasQueryExecuted);
                    ptrScopedOnUiAfter?.Invoke(RuntimeException, wasQueryExecuted);
                }
            }
        }

        public void StartAsync(
            DelegateOnUIBefore ptrScopedOnUiBefore,
            DelegateOnUIAfter ptrScopedOnUiAfter
            )
        {
            if (!workerObject.IsBusy)
            {
                ptrOnUiBefore();
                ptrScopedOnUiBefore?.Invoke();
                this.ptrUnscopedOnUiAfter = ptrScopedOnUiAfter;

                workerObject.RunWorkerAsync();
            }
        }


        public bool StopAsync()
        {
            if (workerObject.IsBusy)
            {
                workerObject.CancelAsync();
                return true;
            }
            return false;
        }


        public bool IsBusy => workerObject.IsBusy;
    }
}
