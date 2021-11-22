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
            Message = "You are about to compile a command.\n • List of available commands can be seen on the first page\n • Write your query in the search box then press [ENTER] to start matching items\n • To cancel press [BACKSPACE] or [ESCAPE]"
        };


        public static NotificationData Notification_CommandError = new NotificationData()
        {
            Title = "Malformed command",
            Message = "There was an error processing the command.\n Tips:\n • Make sure the command is followed by the right arguments\n • Don't forget to close the '\",(,)' symbols\n • Make sure the DATE arguments match the date format prvided in the settings"
        };
    }
}
