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
using System.Globalization;
using System.Windows.Data;

namespace IFilterShellView.Converter
{
    public class ItemDatesConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string OutputData = "";

            //OutputData += ((DateTime)values[0]).ToShortDateString() + " | ";
            //OutputData += ((DateTime)values[1]).ToShortDateString() + " | ";
            //OutputData += ((DateTime)values[2]).ToShortDateString();

            OutputData += ((DateTime)values[0]).ToString(Properties.Settings.Default.DateFormat) + " | ";
            OutputData += ((DateTime)values[1]).ToString(Properties.Settings.Default.DateFormat) + " | ";
            OutputData += ((DateTime)values[2]).ToString(Properties.Settings.Default.DateFormat);


            return OutputData;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
