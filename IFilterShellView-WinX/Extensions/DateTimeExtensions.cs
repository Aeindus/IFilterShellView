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
using System.Linq;

namespace IFilterShellView_WinX.Extensions
{
    public static class DateTimeExtensions
    {
        private static string[] ExpectedDateComponents = { "dd", "MM", "yyyy" };

        public static bool ParseTimeByGlobalDateFormat(string DateString, out DateTime FormatedTime)
        {
            // TODO: settings : DateFormat
            var DateFormat = AppSettings.Get<string>("DateFormat");

            return DateTime.TryParseExact(DateString,
                DateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out FormatedTime
                );
        }

        public static bool ValidateDateTimeFormatString(string DateTimeFormat)
        {
            if (DateTimeFormat.Length != 10) return false;

            string[] DateComponents = DateTimeFormat.Split('/');

            foreach (string component in DateComponents)
            {
                if (!ExpectedDateComponents.Contains(component)) return false;
            }
            return true;
        }
    }
}
