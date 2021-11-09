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
using System.Collections.Generic;
using System.Text;

namespace IFilterShellView.Parser
{
    public enum IdentityType
    { 
        CMD, ARG, OP, CLOSURE
    }

    public struct SyntaxInterval
    {
        public int x;
        public int y;
        public IdentityType t;

        public SyntaxInterval(int px, int py, IdentityType pt)
        {
            x = px;
            y = py;
            t = pt;
        }

        public static SyntaxInterval Get(int px, int py, IdentityType pt)
        {
            return new SyntaxInterval(px, py, pt);
        }
    }


    public class SyntaxIdentities
    {
        public List<SyntaxInterval> Intervals = new List<SyntaxInterval>();
    }
}
