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
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace IFilterShellView.Extensions
{
    public static class ResourceExtensions
    {
        public static BitmapImage LoadBitmapFromResource(string rsrc)
        {
            BitmapImage BitmapResource = new BitmapImage();

#if DEBUG
            // Just so that we don't crash the Designer
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return BitmapResource;
#endif

            BitmapResource.BeginInit();
            BitmapResource.UriSource = new Uri(string.Format("pack://application:,,/Resources/{0}", rsrc));
            BitmapResource.EndInit();
            BitmapResource.Freeze();
            return BitmapResource;
        }
    }
}
