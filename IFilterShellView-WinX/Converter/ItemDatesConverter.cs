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
using IFilterShellView_WinX.Config;
using Microsoft.UI.Xaml.Data;


namespace IFilterShellView_WinX.Converter
{
    // TODO: use function binding

    public class ItemDatesConverter /*: IMultiValueConverter*/
    {
        public object Convert(object[] values, Type targetType, object parameter, string culture)
        {
            string OutputData = "";

            //OutputData += ((DateTime)values[0]).ToShortDateString() + " | ";
            //OutputData += ((DateTime)values[1]).ToShortDateString() + " | ";
            //OutputData += ((DateTime)values[2]).ToShortDateString();

            var DateFormat = AppConfig.DateFormat;


            OutputData += ((DateTime)values[0]).ToString(DateFormat) + " | ";
            OutputData += ((DateTime)values[1]).ToString(DateFormat) + " | ";
            OutputData += ((DateTime)values[2]).ToString(DateFormat);

            return OutputData;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, string culture)
        {
            return null;
        }
    }
}
