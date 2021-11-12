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

using Microsoft.UI.Xaml.Data;
using System;


namespace IFilterShellView_X.Converter
{
    public class ItemAttributeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            uint dwFileAttributes = (uint)value;

            string AttributesString = "";

            foreach (var FileAttributeTupple in NativeWin32.FileAttributesListTupple)
            {
                if ((dwFileAttributes & FileAttributeTupple.Item1) == FileAttributeTupple.Item1)
                {
                    AttributesString += FileAttributeTupple.Item2 + " ";
                }
            }

            if (AttributesString.Length == 0) AttributesString = "No attributes specified";

            return AttributesString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
