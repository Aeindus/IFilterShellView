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

using IFilterShellView.Exceptions;
using IFilterShellView.Extensions;
using IFilterShellView.Native;
using System;
using System.Collections.Generic;
using System.IO;

namespace IFilterShellView.Parser
{
    public static class XPressCommands
    {
        public enum ComIndex
        {
            OLDER,
            IN,
            BETWEEN,
            NEWER,
            CONTAINS,
            STARTS,
            ENDS,
            EXTENSION,
            DIRECTORY,
            FILE,
            CASESENS,
            CASEINSENS,
            SIZE
        }

        public static readonly IReadOnlyDictionary<string, ComIndex> ComStrToComIndex = new Dictionary<string, ComIndex>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "Older", ComIndex.OLDER },
            { "Before", ComIndex.OLDER },
            { "Bef", ComIndex.OLDER },
            { "Old", ComIndex.OLDER },
            { "O", ComIndex.OLDER},

            { "Year", ComIndex.IN},
            { "Y", ComIndex.IN},
            { "In", ComIndex.IN},
            { "During", ComIndex.IN},
            { "Du", ComIndex.IN},

            { "Between", ComIndex.BETWEEN},
            { "Bet", ComIndex.BETWEEN},
            { "Be", ComIndex.BETWEEN},
            { "B", ComIndex.BETWEEN},

            { "Newer", ComIndex.NEWER },
            { "New", ComIndex.NEWER },
            { "N", ComIndex.NEWER},
            { "After", ComIndex.NEWER },
            { "Aft", ComIndex.NEWER },

            { "Contains", ComIndex.CONTAINS },
            { "Ct", ComIndex.CONTAINS },
            { "C", ComIndex.CONTAINS },
            { "Has", ComIndex.CONTAINS },
            { "H", ComIndex.CONTAINS },

            { "Starts", ComIndex.STARTS },
            { "Start", ComIndex.STARTS },
            { "Startswith", ComIndex.STARTS },
            { "Sw", ComIndex.STARTS },
            { "S", ComIndex.STARTS},

            { "Ends", ComIndex.ENDS },
            { "End", ComIndex.ENDS },
            { "Endswith", ComIndex.ENDS },
            { "Ew", ComIndex.ENDS },
            { "E", ComIndex.ENDS },

            { "Extension", ComIndex.EXTENSION },
            { "Ext", ComIndex.EXTENSION },
            { "Ex", ComIndex.EXTENSION },

            { "Directory", ComIndex.DIRECTORY },
            { "Folder", ComIndex.DIRECTORY },
            { "Folders", ComIndex.DIRECTORY },
            { "Dir", ComIndex.DIRECTORY },
            { "D", ComIndex.DIRECTORY },

            { "Files", ComIndex.FILE },
            { "File", ComIndex.FILE },
            { "Item", ComIndex.FILE },
            { "Items", ComIndex.FILE },
            { "F", ComIndex.FILE },

            { "CaseSensitive" , ComIndex.CASESENS },
            { "CaseSens" , ComIndex.CASESENS },
            { "Cs" , ComIndex.CASESENS },

            { "CaseInsensitive" , ComIndex.CASEINSENS},
            { "CaseInsens" , ComIndex.CASEINSENS},
            { "Ci" , ComIndex.CASEINSENS},

            { "Size" , ComIndex.SIZE},
            { "Sz" , ComIndex.SIZE}
        };

        public static readonly IReadOnlyDictionary<ComIndex, string> ComIndexDescription = new Dictionary<ComIndex, string>()
        {
            {ComIndex.OLDER, "Select items older than _date_." },
            {ComIndex.IN, "Select items created in the year _year_." },
            {ComIndex.BETWEEN, "Select items created between years _date1_ _date2_." },
            {ComIndex.NEWER, "Select items newer than _date_."  },
            {ComIndex.STARTS, "Select items that start with _string_."  },
            {ComIndex.ENDS, "Select items that end with _string_."  },
            {ComIndex.CONTAINS, "Select items that contain _string_"  },
            {ComIndex.EXTENSION, "Select items with extension _string_"  },
            {ComIndex.DIRECTORY, "Select only folder items."  },
            {ComIndex.FILE, "Select only file items."},
            {ComIndex.CASESENS, "Do not ignore case sensitivity." },
            {ComIndex.CASEINSENS, "Ignore case sensitivity." },
            {ComIndex.SIZE, "Select files with the size _>_ or _<_ than _number_" },
        };


        public static readonly IReadOnlyDictionary<ComIndex, int> ComIndexOptions = new Dictionary<ComIndex, int>()
        {
            {ComIndex.OLDER, 1 },
            {ComIndex.IN, 1 },
            {ComIndex.BETWEEN, 2 },
            {ComIndex.NEWER, 1 },
            {ComIndex.STARTS, 1 },
            {ComIndex.ENDS, 1 },
            {ComIndex.CONTAINS, 1 },
            {ComIndex.EXTENSION, 1 },
            {ComIndex.DIRECTORY, 0 },
            {ComIndex.FILE, 0 },
            {ComIndex.CASESENS, 0 },
            {ComIndex.CASEINSENS, 0 },
            {ComIndex.SIZE, 2 },
            // Size
        };

        public static readonly Dictionary<ComIndex, Func<CPidlData, CComAndArgs, bool>> CommandAttributeDict = new Dictionary<ComIndex, Func<CPidlData, CComAndArgs, bool>>() 
        {
            /* older command */
            { ComIndex.OLDER, Com_Older },

            /* in command */
            { ComIndex.IN, Com_In },

            /* between command */
            { ComIndex.BETWEEN, Com_Between },

            /* newer command */
            { ComIndex.NEWER, Com_Newer },

            /* starts with command */
            { ComIndex.STARTS, Com_Starts },

            /* contains command */
            { ComIndex.CONTAINS, Com_Contains },

            /* ends with command */
            { ComIndex.ENDS, Com_Ends },

            /* extension */
            { ComIndex.EXTENSION, Com_Extension },

            /* directory */
            { ComIndex.DIRECTORY, Com_Directory },

            /* file */
            { ComIndex.FILE, Com_File },
            
            /* case sensitive */
            { ComIndex.CASESENS, Com_CaseSensitive },
            
            /* case insensitive */
            { ComIndex.CASEINSENS, Com_CaseInsensitive },

            /* size */
            { ComIndex.SIZE, Com_Size },

        };


        public static bool Com_Older(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            if (!DateTimeExtensions.ParseTimeByGlobalDateFormat(ComAndArgs.Arguments[0], -1, out DateTime FormatedDate, out _))
            {
                throw new UserException("Date failed format checks.");
            }

            return DateTime.Compare(PidlData.CreationTime, FormatedDate) < 0;
        }

        public static bool Com_In(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            if (!DateTimeExtensions.ParseTimeByGlobalDateFormat(ComAndArgs.Arguments[0], 0, out DateTime FormatedDate, out bool OnlyYearParsed))
            {
                throw new UserException("Date failed format checks.");
            }

            if (OnlyYearParsed)
            {
                return PidlData.CreationTime.Year == FormatedDate.Year;
            }
            else
            {
                return PidlData.CreationTime.SameYearMonthAndDay(FormatedDate);
            }
        }

        public static bool Com_Between(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            if (!DateTimeExtensions.ParseTimeByGlobalDateFormat(ComAndArgs.Arguments[0], 0, out DateTime FormatedDate1, out bool OnlyYearParsed1) ||
                !DateTimeExtensions.ParseTimeByGlobalDateFormat(ComAndArgs.Arguments[1], 0, out DateTime FormatedDate2, out bool OnlyYearParsed2))
            {
                throw new UserException("Date failed format checks.");
            }

            if (OnlyYearParsed1 != OnlyYearParsed2) throw new UserException("Both date arguments must have the same format.");

            if (OnlyYearParsed1)
            {
                return FormatedDate1.Year <= PidlData.CreationTime.Year &&
                       PidlData.CreationTime.Year <= FormatedDate2.Year;
            }
            else
            {
                return FormatedDate1 <= PidlData.CreationTime &&
                       PidlData.CreationTime <= FormatedDate2;
            }
        }

        public static bool Com_Newer(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            if (!DateTimeExtensions.ParseTimeByGlobalDateFormat(ComAndArgs.Arguments[0], 1, out DateTime FormatedDate, out _))
            {
                throw new UserException("Date failed format checks.");
            }

            return DateTime.Compare(PidlData.CreationTime, FormatedDate) > 0;
        }

        public static bool Com_Starts(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            return PidlData.PidlName.StartsWith(ComAndArgs.Arguments[0], XPressParser.ComContext.StringComparisonEq);
        }

        public static bool Com_Contains(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            return PidlData.PidlName.Contains(ComAndArgs.Arguments[0], XPressParser.ComContext.StringComparisonEq);
        }

        public static bool Com_Ends(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            return Path.GetFileNameWithoutExtension(PidlData.PidlName).EndsWith(ComAndArgs.Arguments[0], XPressParser.ComContext.StringComparisonEq);
        }

        public static bool Com_Extension(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            return  Path.GetExtension(PidlData.PidlName).Equals(ComAndArgs.Arguments[0]);
        }

        public static bool Com_Directory(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            return NativeUtilities.IsAttributeOfFolder(PidlData.FileAttributes);
        }

        public static bool Com_File(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            return !NativeUtilities.IsAttributeOfFolder(PidlData.FileAttributes);
        }

        public static bool Com_CaseSensitive(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            XPressParser.ComContext.SearchSensitivity = CComContext.Sensitivity.CaseSensitive;
            return true;
        }

        public static bool Com_CaseInsensitive(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            XPressParser.ComContext.SearchSensitivity = CComContext.Sensitivity.CaseInsensitive;
            return true;
        }

        public static bool Com_Size(CPidlData PidlData, CComAndArgs ComAndArgs)
        {
            if (!ulong.TryParse(ComAndArgs.Arguments[1], out ulong Size))
            {
                throw new UserException("The second parameter (i.e Size) is not a valid number expressed in MB.");
            }

            if (ComAndArgs.Arguments[0][0] == '<')
            {
                if (PidlData.FileSizeMB < Size) return true;

                return false;
            }
            else if (ComAndArgs.Arguments[0][0] == '>')
            {
                if (PidlData.FileSizeMB > Size) return true;

                return false;
            }
            else
            {
                throw new UserException("First parameter mismatch! Allowed are only the symbols '<' and '>' that provide a comparison interface.");
            }
        }
    }
}
