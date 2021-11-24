using IFilterShellView.Extensions;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace IFilterShellView
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly string assemblyImageName;
        private readonly string assemblyImageLocation;



        public SettingsWindow()
        {
            InitializeComponent();

            Assembly CurrentImageAssembly = Assembly.GetExecutingAssembly();
            assemblyImageName = CurrentImageAssembly.GetName().Name;
            assemblyImageLocation = Path.Combine(Path.GetDirectoryName(CurrentImageAssembly.Location), assemblyImageName + ".exe");

            LoadApplicationSettings();
        }

        private void LoadApplicationSettings()
        {
            // Other settings
            MaxFolderPidlCount_Deepscan.Text = Convert.ToString(Properties.Settings.Default.MaxFolderPidlCount_Deepscan);
            MaxNumberFilterUpTo.Text = Convert.ToString(Properties.Settings.Default.MaxNumberFilterUpTo);
            DateFilterFormat.Text = Properties.Settings.Default.DateFormat;
            MaxHistory.Text = Properties.Settings.Default.MaxHistory.ToString();

            KeepFilterText.IsChecked = Properties.Settings.Default.KeepFilterText;
            AutoSelectFiltered.IsChecked = Properties.Settings.Default.AutoSelectFiltered;
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                RunStartupCb.IsChecked = key.GetValue(assemblyImageName) != null;
            }
            catch { }
        }

        private void MaxFolderPidlCount_Deepscan_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxFolderPidlCount_Deepscan = Convert.ToInt32(MaxFolderPidlCount_Deepscan.Text);
        }
        private void MaxNumberFilterUpTo_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxNumberFilterUpTo = Convert.ToInt32(MaxNumberFilterUpTo.Text);
        }
        private void DateFilterFormat_LostFocus(object sender, RoutedEventArgs e)
        {
            string NewDateFormat = DateFilterFormat.Text.Trim();

            if (DateTimeExtensions.ValidateDateTimeFormatString(NewDateFormat))
            {
                Properties.Settings.Default.DateFormat = NewDateFormat;
            }
            else
            {
                DateFilterFormat.Text = Properties.Settings.Default.DateFormat;
            }
        }
        private void MaxHistory_LostFocus(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.MaxHistory = Convert.ToInt32(MaxHistory.Text);
        }
        private void RunStartupCb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (RunStartupCb.IsChecked == true)
                    key.SetValue(assemblyImageName, assemblyImageLocation);
                else
                    key.DeleteValue(assemblyImageName);
            }
            catch { }
        }
        private void KeepFilterText_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.KeepFilterText = (bool)KeepFilterText.IsChecked;
        }
        private void AutoSelectFiltered_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.AutoSelectFiltered = (bool)AutoSelectFiltered.IsChecked;
        }
    }
}
