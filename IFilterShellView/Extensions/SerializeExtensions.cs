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
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace IFilterShellView.Extensions
{
    public static class SerializeExtensions
    {
        public static string SerializeGenericClassList<T>(List<T> ListOfSerializable)
        {
            using (MemoryStream MemStream = new MemoryStream())
            {
                BinaryFormatter BinFormatter = new BinaryFormatter();
                BinFormatter.Serialize(MemStream, ListOfSerializable);
                MemStream.Position = 0;
                byte[] InBuffer = new byte[(int)MemStream.Length];
                MemStream.Read(InBuffer, 0, InBuffer.Length);
                return Convert.ToBase64String(InBuffer);
            }
        }
        public static List<T> MaterializeGenericClassList<T>(string StringOfSerialized)
        {
            if (StringOfSerialized.Length == 0) return new List<T>();

            using (MemoryStream MemStream = new MemoryStream(Convert.FromBase64String(StringOfSerialized)))
            {
                BinaryFormatter BinFormatter = new BinaryFormatter();
                return (List<T>)BinFormatter.Deserialize(MemStream);
            }
        }
    }
}
