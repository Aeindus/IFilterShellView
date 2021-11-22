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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace IFilterShellView.Native
{
    public sealed class GlobalKeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private readonly BackgroundWorker Worker = new BackgroundWorker();

        private IntPtr HookID = IntPtr.Zero;
        private NativeWin32.LowLevelKeyboardProc Fn_NativeEventCallback;
        private Dictionary<int, KeyValuePair<KeyCombination, Action>> HookEventsDict;
        private KeyCombination PressedKeys;
        private bool Disposed;


        public GlobalKeyboardHook()
        {
            Fn_NativeEventCallback = new NativeWin32.LowLevelKeyboardProc(NativeEventCallback);
            HookEventsDict = new Dictionary<int, KeyValuePair<KeyCombination, Action>>();
            PressedKeys = new KeyCombination();
            Worker.DoWork += STA_BackgroundWorkerThread;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                HookID = NativeWin32.SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    Fn_NativeEventCallback,
                    NativeWin32.GetModuleHandle(curModule.ModuleName),
                    0
                    );
            }
        }


        /// <summary>
        /// Register a keyboard hook event
        /// </summary>
        /// <param name="keys">The short keys. minimum is two keys</param>
        /// <param name="execute">The action to run when the key ocmbination has pressed</param>
        /// <param name="dispose">An action to run when unsubscribing from keyboard hook. can be null</param>
        /// <returns>Event id to use when unregister</returns>
        public bool AddHotkeys(List<Key> keys, Action execute)
        {
            if (HookEventsDict == null ||
                keys == null ||
                execute == null ||
                keys.Count < 2)
            {
                return false;
            }

            if (!ValidateKeys(keys)) return false;

            KeyCombination kc = new KeyCombination(keys);

            int id = kc.GetHashCode();

            if (HookEventsDict.ContainsKey(id)) return false;

            HookEventsDict[id] = new KeyValuePair<KeyCombination, Action>(kc, execute);

            return true;
        }
        
        private IntPtr NativeEventCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return NativeWin32.CallNextHookEx(HookID, nCode, wParam, lParam);

            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                int k = Marshal.ReadInt32(lParam);
                Key pk = KeyInterop.KeyFromVirtualKey(k);
                PressedKeys.Add(pk);

                if (PressedKeys.Count >= 2)
                {
                    var keysToAction = HookEventsDict.Values.FirstOrDefault(val => val.Key.Equals(PressedKeys));

                    if (keysToAction.Value != null)
                    {
                        Action KeyAction = keysToAction.Value;

                        // Issue the background worker due to some nasty OLE STA Apartment Threading Policy
                        if (!Worker.IsBusy)
                        {
                            // Trigger the callback inside the worker
                            Worker.RunWorkerAsync((object)KeyAction);

                            // Cancel the key stroke
                            return (IntPtr)1; // it produced unwanted sideffects
                        }
                    }
                }
            }
            else if (wParam == (IntPtr)WM_KEYUP)
            {
                PressedKeys.Clear();
            }

            return NativeWin32.CallNextHookEx(HookID, nCode, wParam, lParam);
        }
        private void STA_BackgroundWorkerThread(object sender, DoWorkEventArgs e)
        {
            // run all background tasks here
            Application.Current.Dispatcher.Invoke((Action)e.Argument);
        }



        private bool ValidateKeys(IEnumerable<Key> keys) => keys.All(t => IsKeyValid((int)t));
        private bool IsKeyValid(int key) => (key >= 44 && key <= 69) || (key >= 116 && key <= 119);



        #region IDsiposable
        private void Dispose(bool dispose)
        {
            if (Disposed) return;

            // Unmanaged resources here
            if (HookID != IntPtr.Zero)
            {
                NativeWin32.UnhookWindowsHookEx(HookID);
            }

            if (dispose)
            {
                HookEventsDict?.Clear();
                PressedKeys?.Clear();

                HookEventsDict = null;
                PressedKeys = null;
                HookID = IntPtr.Zero;

                if (this != null) GC.SuppressFinalize(this);
            }
            Disposed = true;
        }
        public void Dispose() => Dispose(true);
        ~GlobalKeyboardHook() => Dispose(false);

        #endregion





        /// <summary>
        /// Class that handles keys
        /// </summary>
        private class KeyCombination : IEquatable<KeyCombination>
        {
            private readonly bool _canModify;
            private readonly List<Key> _keys;


            public KeyCombination(List<Key> keys) => _keys = keys ?? new List<Key>();
            public KeyCombination()
            {
                _keys = new List<Key>();
                _canModify = true;
            }


            public int Count { get { return _keys.Count; } }

            public void Add(Key key)
            {
                if (_canModify) _keys.Add(key);
            }
            public void Remove(Key key)
            {
                if (_canModify) _keys.Remove(key);
            }
            public void Clear()
            {
                if (_canModify) _keys.Clear();
            }



            public bool Equals(KeyCombination other) => other._keys != null && _keys != null && KeysEqual(other._keys);
            private bool KeysEqual(List<Key> keys)
            {
                if (keys == null || _keys == null || keys.Count != _keys.Count) return false;
                for (int i = 0; i < _keys.Count; i++)
                    if (_keys[i] != keys[i])
                        return false;

                return true;
            }
            public override bool Equals(object obj)
            {
                if (obj is KeyCombination)
                    return Equals((KeyCombination)obj);
                return false;
            }

            public override int GetHashCode()
            {
                if (_keys == null) return 0;

                unchecked
                {
                    int hash = 19;
                    for (int i = 0; i < _keys.Count; i++)
                        hash = hash * 31 + _keys[i].GetHashCode();
                    return hash;
                }
            }

            public override string ToString()
            {
                if (_keys == null)
                    return string.Empty;

                var sb = new StringBuilder((_keys.Count - 1) * 4 + 10);
                for (int i = 0; i < _keys.Count; i++)
                {
                    if (i < _keys.Count - 1)
                        sb.Append(_keys[i] + " , ");
                    else
                        sb.Append(_keys[i]);
                }
                return sb.ToString();
            }
        }


    }
}
