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

using System.Collections.Generic;
using System.IO;

namespace IFilterShellView_WinX.Export
{
    /// <summary>
    /// A next to silly exporter
    /// </summary>
    public static class ExportManger
    {
        public enum FORMAT
        {
            CSV = 1,
            JSON = 2,
            XML = 3,
            C = 4
        }

        /*
         * JSON: 
         * {
         *   {"item0": "c:\\program\\data"},
         *   {"item1": "c:\\program\\data"},
         *   .....   
         *   {"itemn": "c:\\program\\data"},
         * }
         * 
         * XML:
         * <RootList>
         *   <item0>"c:\\program\\data"</item0>
         *   <item1>"c:\\program\\data"</item0>
         *   ...
         *   <itemn>"c:\\program\\data"</item0>
         * </RootList>
         * 
         * C:
         * const char* root_list[] = { "", "", ... , "" } ;
         */


        private static Dictionary<FORMAT, List<string> > TemplateDictionary = new Dictionary<FORMAT, List<string>> 
        {
            // FORMAT    STRING FORMATED    HEADER    BODY    ENDING
            {FORMAT.JSON, new List<string>{"{", @"{{""items{0}"":""{1}""}}," , "}"  } },
            {FORMAT.XML, new List<string>{"<RootList>", "<\"item{0}\">{1}</item{0}>", "</RootList>"  } },
            {FORMAT.C, new List<string>{ "const char* root_list[] = {", " /*{0}*/\"{1}\", ","};" } },
        };

        public static string ExportData(FORMAT fmt, List<CPidlData> list, bool IncludePath, bool IncludeExtension, string LocationUrl)
        {
            if (fmt == FORMAT.CSV)
                return string.Join(",", list);

            List<string> template = TemplateDictionary[fmt];
            string outputbuffer = template[0];

            for (int i = 0; i < list.Count; i++)
            {
                string outp = IncludePath ? LocationUrl : "";
                outp += IncludeExtension ? list[i].PidlName : Path.GetFileNameWithoutExtension(list[i].PidlName);
                outputbuffer += string.Format(template[1], i, outp);
            }

            outputbuffer += template[2]; 
            return outputbuffer;
        }
    }
}
