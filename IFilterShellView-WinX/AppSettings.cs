namespace IFilterShellView_WinX
{
    public static class AppSettings
    {

        public static T Get<T>(string SettingKeyName)
        {
            return (T)Windows.Storage.ApplicationData.Current.LocalSettings.Values[SettingKeyName];
        }

        public static void Set(string SettingKeyName, object Value)
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values[SettingKeyName] = Value;
        }
    }
}
