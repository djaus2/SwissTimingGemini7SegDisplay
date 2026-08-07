using System.ComponentModel;
using System.Windows;
using SwissTimingDisplay.Properties;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    public partial class SiriccoWindowControlled : Window
    {
        public SiriccoWindowControlled(MainViewModel vm, MainWindow? mainWindow = null)
        {
            InitializeComponent();
            gauge.SetUp(vm, mainWindow);
            gauge.CaptureStarted += (s, e) => windSpeedPanel.Visibility = Visibility.Collapsed;
            gauge.WindSpeedDetermined += (s, e) => windSpeedPanel.Visibility = Visibility.Visible;
            DataContext = vm;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Settings.Default.Save();
        }
    }
}
