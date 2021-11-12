namespace IFilterShellView_X
{
    public static class AppSettings
    {

        public static T GetSettingsHinst<T>(string SettingKeyName) where T : class
        {
            return Windows.Storage.ApplicationData.Current.LocalSettings.Values[SettingKeyName] as T;
        }

    }
}
