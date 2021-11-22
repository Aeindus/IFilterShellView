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

namespace IFilterShellView.Helpers
{
    public class NotificationManager
    {
        public class NotificationData
        {
            public string Title;
            public string Message;
        }

        // used
        public static NotificationData Notification_EmptyResults = new NotificationData()
        { 
            Title = "No items to display", 
            Message= "The filter couldn't return any results. Try to change the case sensitivity parameter. Maybe there are no items that match your filter. Are you sure you know what you are looking for ?" 
        };

        // used
        public static NotificationData Notification_TooManyItems = new NotificationData()
        {
            Title = "Deep processing required",
            Message = "This folder exceeds the maximum number of items. A deep search is therefore required.\n • Write your query in the search box then press [ENTER] to start matching items\n • To cancel press [BACKSPACE] or [ESCAPE]"
        };

        // used
        public static NotificationData Notification_CommandGiven = new NotificationData()
        {
            Title = "Command notice",
            Message = "You are about to compile a command. Consider the following indications:\n • List of available commands can be seen on the first page\n • Write your query in the search box then press [ENTER] to start matching items\n • To cancel press [BACKSPACE] or [ESCAPE]"
        };

        public static NotificationData Get(string Title, string Message)
        {
            return new NotificationData()
            {
                Title = Title,
                Message = Message
            };
        }
    }
}
