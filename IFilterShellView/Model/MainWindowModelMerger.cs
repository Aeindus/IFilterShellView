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

using System.Windows.Media;

namespace IFilterShellView.Model
{
    public class MainWindowModelMerger
    {
        private readonly VisibilityModel _SearchPageVisibilityModel = new VisibilityModel();
        private readonly StringModel _SearchPageNoticeTitle = new StringModel();
        private readonly StringModel _SearchPageNoticeSubtitle = new StringModel();


        public VisibilityModel SearchPageVisibilityModel => _SearchPageVisibilityModel;
        public StringModel SearchPageNoticeTitle => _SearchPageNoticeTitle;
        public StringModel SearchPageNoticeMessage => _SearchPageNoticeSubtitle;
    }
}
