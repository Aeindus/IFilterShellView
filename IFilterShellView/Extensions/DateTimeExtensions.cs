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

namespace IFilterShellView.Extensions
{
    public static class DateTimeExtensions
    {
        public static bool ParseTimeByGlobalDateFormat(string DateString, int Direction, out DateTime FormatedTime, out bool OnlyYearParsed)
        {
            FormatedTime = default;
            OnlyYearParsed = false;

            if (DateString.Length == 4)
            {
                // only the year is present
                if (!int.TryParse(DateString, out int Year)) return false;

                OnlyYearParsed = true;

                switch (Direction)
                {
                    case -1:
                        FormatedTime = new DateTime(Year - 1, 12, 31);
                        break;
                    case 0:
                        FormatedTime = new DateTime(Year, 1, 1);
                        break;
                    case 1:
                        FormatedTime = new DateTime(Year + 1, 1, 1);
                        break;
                }
            }
            else
            {
                if (!DateTime.TryParseExact(
                    DateString,
                    Properties.Settings.Default.DateFormat, 
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out FormatedTime
                )) return false;
            }

            return true;
        }

        public static bool ValidateDateTimeFormatString(string DateTimeFormat)
        {
            if (DateTimeFormat.Length != 8) return false;
            uint sumcheck = 0;

            foreach (byte b in DateTimeFormat) sumcheck += b;

            return sumcheck == 0x2F3;
        }


        public static bool SameYearMonthAndDay(this DateTime ThisDateTime, DateTime CompareDateTime)
        {
            return ThisDateTime.Year == CompareDateTime.Year &&
                ThisDateTime.Month == CompareDateTime.Month &&
                ThisDateTime.Day == CompareDateTime.Day;
        }

    }
}
