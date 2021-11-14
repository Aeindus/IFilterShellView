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

using IFilterShellView_WinX.Exceptions;
using IFilterShellView_WinX.Extensions;
using System;
using System.Collections.Generic;

namespace IFilterShellView_WinX.Parser
{
    public static class XPressCommands
    {
        public enum ComIndex
        {
            OLDER,
            NEWER,
            CONTAINS,
            STARTS,
            ENDS,
            EXTENSION,
            DIRECTORY,
            FILE
        }

        public static readonly IReadOnlyDictionary<string, ComIndex> ComStrToComIndex = new Dictionary<string, ComIndex>(StringComparer.InvariantCultureIgnoreCase)
        {
            { "Older", ComIndex.OLDER },
            { "Before", ComIndex.OLDER },
            { "Bef", ComIndex.OLDER },
            { "Old", ComIndex.OLDER },
            { "O", ComIndex.OLDER},

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

            { "End", ComIndex.ENDS },
            { "Ends", ComIndex.ENDS },
            { "Endswith", ComIndex.ENDS },
            { "Ew", ComIndex.ENDS },
            { "E", ComIndex.ENDS },

            { "Extension", ComIndex.EXTENSION },
            { "Ext", ComIndex.EXTENSION },

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
        };

        public static readonly IReadOnlyDictionary<ComIndex, string> ComIndexDescription = new Dictionary<ComIndex, string>()
        {
            {ComIndex.OLDER, "Gets all the items older than a date" },
            {ComIndex.NEWER, "Gets all the items newer than a date"  },
            {ComIndex.STARTS, "Gets all the items that start with"  },
            {ComIndex.ENDS, "Gets all the items that end with"  },
            {ComIndex.CONTAINS, "Gets all the items that contain"  },
            {ComIndex.EXTENSION, "Gets all the items with extension X"  },
            {ComIndex.DIRECTORY, "Gets all the items that are directories"  },
            {ComIndex.FILE, "Gets all the items that are files"},
        };


        public static readonly IReadOnlyDictionary<ComIndex, int> ComIndexOptions = new Dictionary<ComIndex, int>()
        {
            {ComIndex.OLDER, 1 },
            {ComIndex.NEWER, 1 },
            {ComIndex.STARTS, 1 },
            {ComIndex.ENDS, 1 },
            {ComIndex.CONTAINS, 1 },
            {ComIndex.EXTENSION, 0 },
            {ComIndex.DIRECTORY, 0 },
            {ComIndex.FILE, 0 },
            // Size
        };

        public static readonly Dictionary<ComIndex, Func<CPidlData, CCommAndArgs, bool>> CommandAttributeDict = new Dictionary<ComIndex, Func<CPidlData, CCommAndArgs, bool>>()
            {
                /* older command */
                {
                    ComIndex.OLDER, (PidlData, CommAndArgs) =>
                    {
                        if (! DateTimeExtensions.ParseTimeByGlobalDateFormat(CommAndArgs.Arguments[0], out DateTime FormatedDate))
                        {
                            throw new UserException("Date failed format checks.");
                        }

                        return DateTime.Compare(PidlData.ftCreationTime, FormatedDate) < 0;
                    }
                },

                /* newer command */
                {
                    ComIndex.NEWER, (PidlData, CommAndArgs) =>
                    {
                        if (! DateTimeExtensions.ParseTimeByGlobalDateFormat(CommAndArgs.Arguments[0], out DateTime FormatedDate))
                        {
                            throw new UserException("Date failed format checks.");
                        }

                        return DateTime.Compare(PidlData.ftCreationTime, FormatedDate) > 0;
                    }
                },

                /* starts with command */
                {
                    ComIndex.STARTS, (PidlData, CommAndArgs) => PidlData.PidlName.StartsWith(CommAndArgs.Arguments[0])
                },

                /* contains with command */
                {
                    ComIndex.CONTAINS, (PidlData, CommAndArgs) => PidlData.PidlName.Contains(CommAndArgs.Arguments[0])
                },

                /* contains with command */
                {
                    ComIndex.ENDS, (PidlData, CommAndArgs) => PidlData.PidlName.EndsWith(CommAndArgs.Arguments[0])
                },

                /* extension */
                {
                    ComIndex.EXTENSION, (PidlData, CommAndArgs) => PidlData.PidlName.EndsWith(CommAndArgs.Arguments[0])
                },


                /* directory */
                {
                    ComIndex.DIRECTORY, (PidlData, CommAndArgs) => PidlData.dwFileAttributes == 0x10
                },

                /* file */
                {
                    ComIndex.FILE, (PidlData, CommAndArgs) => PidlData.dwFileAttributes != 0x10
                },
            };


    }
}
