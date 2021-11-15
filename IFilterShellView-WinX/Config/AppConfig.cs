using Windows.ApplicationModel;
using Windows.Storage;

namespace IFilterShellView_WinX.Config
{
    public static class AppConfig
    {
        private static ApplicationDataContainer LocalSettings = Windows.Storage.ApplicationData.Current.LocalSettings;


        private static T GetValueOrDefault<T>(string Key, T DefaultValue)
        {
            if (LocalSettings.Values.ContainsKey(Key)) return (T)LocalSettings.Values[Key];
            return DefaultValue;
        }

        private static void SetValue<T>(string Key, T Value)
        {
            LocalSettings.Values[Key] = Value;
        }

        public static uint FilterSettings 
        {
            get => GetValueOrDefault("FilterSettings", 33u);
            set => SetValue("FilterSettings", value);
        }
        public static int MaxFolderPidlCount_Deepscan
        {
            get => GetValueOrDefault("MaxFolderPidlCount_Deepscan", 150);
            set => SetValue("MaxFolderPidlCount_Deepscan", value);
        }

        public static int MaxNumberFilterUpTo
        {
            get => GetValueOrDefault("MaxNumberFilterUpTo", -1);
            set => SetValue("MaxNumberFilterUpTo", value);
        }
        public static int MaxHistory
        {
            get => GetValueOrDefault("MaxHistory", 15);
            set => SetValue("MaxHistory", value);
        }

        public static bool KeepFilterText
        {
            get => GetValueOrDefault("KeepFilterText", false);
            set => SetValue("KeepFilterText", value);
        }

        public static string DateFormat
        {
            get => GetValueOrDefault("DateFormat", "dd/MM/yyyy");
            set => SetValue("DateFormat", value);
        }

        public static string HistoryListSerialized
        {
            get => GetValueOrDefault("HistoryListSerialized", "");
            set => SetValue("HistoryListSerialized", value);
        }

    }
}
