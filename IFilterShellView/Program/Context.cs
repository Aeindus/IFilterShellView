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

namespace IFilterShellView.Program
{
    public partial class Context : IDisposable
    {
        private static readonly Lazy<Context> lazy = new Lazy<Context>(() => new Context(), false);
        public readonly CEventManager EventManager;
        public readonly CMarshalPDILNativeDataHolder MarshalPIDLNativeDataHolder;

        public IntPtr PrevShellWindowHwnd;
        public IntPtr PrevShellWindowModernSearchBoxHwnd;
        public string LocationUrl;
        public string LocationUrlOnStart;
        public string PrevFilterText;
        public int FilterCount;
        public int PidlCount;
        private bool Disposed;

        public string FilterText;
        public string FilterTextWithoutCommandModifier => FilterText[1..].TrimStart();


        public bool FlagRunInBackgroundWorker;
        public bool FlagExtendedFilterMod;
        public bool FlagTooManyItems;


        public CIShellBrowser pIShellBrowser;
        public CIShellFolder pIShellFolder;
        public CIShellView pIShellView;
        public CIFolderView2 pIFolderView2;

        /// <exception cref="UserException">Can throw on initialization</exception>
        public static Context Instance => lazy.Value;

        private Context()
        {
            MarshalPIDLNativeDataHolder = new CMarshalPDILNativeDataHolder(); // can throw
            EventManager = new CEventManager();  // can throw
        }

        public void HardReset()
        {
            FilterText = "";
            LocationUrl = "";
            FilterCount = 0;
            PidlCount = 0;

            FlagRunInBackgroundWorker = false;
            FlagExtendedFilterMod = false;
            FlagTooManyItems = false;

            EventManager.ResetSubscription();
        }


        private void Dispose(bool dispose)
        {
            if (Disposed) return;

            // Free Unamnaged Ressources
            EventManager.Dispose();

            if (dispose)
            {
                if (this != null)
                {
                    GC.SuppressFinalize(this);
                }
            }

            Disposed = true;
        }
        public void Dispose() => Dispose(true);
        ~Context() => Dispose(false);
    }
}
