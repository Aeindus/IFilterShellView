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

using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace IFilterShellView_WinX
{
    public class CPidlData
    {
        public BitmapImage BmpImage { get; set; }

        public DateTime ftCreationTime { get; set; }
        public DateTime ftLastAccessTime { get; set; }
        public DateTime ftLastWriteTime { get; set; }


        public uint dwFileAttributes { get; set; }
        public bool AttributesSet { get; set; }
        public string PidlName { get; set; }
        public ulong FileSize { get; set; }
    }
}
