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

using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;

namespace IFilterShellView_X.ShellContext
{
    public partial class ShellContextContainer
    {
        public partial class CEventManager
        {
            private class WinEventProcHook
            {
                private NativeWin32.WinEventProc Fn_NativeEventCallback;
                private IntPtr HwinEventHook;

                private readonly Action Execute;
                private readonly IntPtr Hwnd;


                public WinEventProcHook(IntPtr hWnd, Action execute)
                {
                    uint dwThreadId = NativeWin32.GetWindowThreadProcessId(hWnd, out uint dwProcessId);

                    if (dwThreadId == 0) throw new Exception();

                    Fn_NativeEventCallback = new NativeWin32.WinEventProc(NativeEventCallback);

                    HwinEventHook = NativeWin32.SetWinEventHook(
                        NativeWin32.SetWinEventHookEvents.EVENT_OBJECT_DESTROY,
                        NativeWin32.SetWinEventHookEvents.EVENT_OBJECT_DESTROY,
                        IntPtr.Zero,
                        Fn_NativeEventCallback,
                        dwProcessId,
                        dwThreadId,
                        NativeWin32.SetWinEventHookFlags.WINEVENT_OUTOFCONTEXT
                        );

                    if (HwinEventHook == IntPtr.Zero) throw new Exception();

                    Hwnd = hWnd;
                    Execute = execute;

                    Debug.WriteLine("[+] Created new instance of GlobalEventHook");
                }

                public void FreeResources()
                {
                    // Free Unamnaged Ressources
                    if (HwinEventHook != IntPtr.Zero)
                    {
                        _ = NativeWin32.UnhookWinEvent(HwinEventHook);
                        HwinEventHook = IntPtr.Zero;

                        Debug.WriteLine("[-] Disposed instance of GlobalEventHook");
                    }
                }

                private void NativeEventCallback(IntPtr hWinEventHook, uint iEvent, IntPtr hWnd, long idObject, long idChild, uint dwEventThread, uint dwmsEventTime)
                {
                    const long OBJID_WINDOW = 0;
                    const long INDEXID_CONTAINER = 0;

                    if (iEvent == NativeWin32.SetWinEventHookEvents.EVENT_OBJECT_DESTROY &&
                         hWnd == Hwnd && idObject == OBJID_WINDOW
                         /*idObject == OBJID_WINDOW && idChild == INDEXID_CONTAINER*/)
                    {
                        // TODO: check if fails here
                        //if (Execute != null) Application.Current.Dispatcher.Invoke(Execute);
                        if (Execute != null) DispatcherQueue.GetForCurrentThread().TryEnqueue( () => Execute());
                    }
                }
            }
        }
    }
}
