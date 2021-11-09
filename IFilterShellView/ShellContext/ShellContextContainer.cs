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

using IFilterShellView.Shell.Interfaces;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace IFilterShellView.ShellContext
{
    public partial class ShellContextContainer : IDisposable
    {
        public readonly CEventManager EventManager;
        public readonly CMarshalPDILNativeDataHolder MarshalPIDLNativeDataHolder;


        public IntPtr PrevShellWindowHwnd;
        public string LocationUrl;
        public string LocationUrlBeforeBrowse;
        public string FilterText;
        public string PrevFilterText;
        public int FilterCount;
        public int FilterReportCount;
        public int PidlCount;
        public bool FlagRunInBackgroundWorker;
        public bool FlagExtendedFilterMod;
        private bool Disposed;

        public CIShellBrowser pIShellBrowser;
        public CIShellFolder pIShellFolder;
        public CIShellView pIShellView;
        public CIFolderView2 pIFolderView2;
        public NativeWin32.RECT ShellViewRect;

        /// <summary>
        /// Can throw exception if failed initializing memory
        /// Some objects can throw during initialization
        /// </summary>
        public ShellContextContainer()
        {
            MarshalPIDLNativeDataHolder = new CMarshalPDILNativeDataHolder(); // can throw
            EventManager = new CEventManager();  // can throw
            ShellViewRect = new NativeWin32.RECT(); 
        }

        public void Reset()
        {
            FilterText = "";
            LocationUrl = "";
            FilterCount = 0;
            FilterReportCount = 0;
            PidlCount = 0;

            //pIShellBrowser = null;
            //pIFolderView2 = null;
            //pIShellFolder = null;
            //pIShellView = null;

            FlagRunInBackgroundWorker = false;
            EventManager.ResetSubscription();
        }

        public partial class CEventManager : IDisposable
        {
            private WinEventProcHook EventHook;
            private bool Disposed;

            public bool ResubscribeToExtCloseEvent(IntPtr hWnd, Action execute)
            {
                EventHook?.FreeResources();
                
                try
                {
                    EventHook = new WinEventProcHook(hWnd, execute);
                }
                catch (Exception)
                {
                    return false;
                }

                return true;
            }
            public void ResetSubscription()
            {
                EventHook?.FreeResources();
                EventHook = null;
            }


            private void Dispose(bool dispose)
            {
                if (Disposed) return;

                // Free Unamnaged Ressources
                EventHook?.FreeResources();
                EventHook = null;

                if (dispose)
                {
                    if (this != null) GC.SuppressFinalize(this);
                }

                Disposed = true;
            }
            public void Dispose() => Dispose(true);
            ~CEventManager() => Dispose(false);
        }


        /// <summary>
        /// Can throw exception if failed initializing memory
        /// </summary>
        public class CMarshalPDILNativeDataHolder : IDisposable
        {
            public int MAX_PATH = 256;

            public IntPtr STRRET;
            public StringBuilder BUFFER;

            private bool Disposed;

            public CMarshalPDILNativeDataHolder()
            {
                STRRET = Marshal.AllocCoTaskMem(MAX_PATH * 2 + 4);
                BUFFER = new StringBuilder(MAX_PATH);

                if (STRRET == null) throw new Exception();
            }

            private void Dispose(bool dispose)
            {
                if (Disposed) return;

                // Free Unamnaged Ressources
                if (STRRET != null)
                    Marshal.FreeCoTaskMem(STRRET);

                if (dispose)
                {
                    if (this != null) GC.SuppressFinalize(this);
                }

                Disposed = true;
            }

            public void Dispose() => Dispose(true);
            ~CMarshalPDILNativeDataHolder() => Dispose(false);
        }



        private void Dispose(bool dispose)
        {
            if (Disposed) return;

            // Free Unamnaged Ressources
            EventManager.Dispose();

            if (dispose)
            {
                if (this != null) GC.SuppressFinalize(this);
            }

            Disposed = true;
        }
        public void Dispose() => Dispose(true);
        ~ShellContextContainer() => Dispose(false);
    }
}
