using System.ComponentModel;
using System.Windows;
using SwissTimingDisplay.Controls;
using SwissTimingDisplay.Properties;
using SwissTimingDisplay.ViewModels;

namespace SwissTimingDisplay
{
    public partial class SiriccoWindowControlled : Window
    {
        private SiriccoWindGaugeControl _control;

        public SiriccoWindowControlled() : this(MainViewModel.SharedInstance, null)
        {
        }

        public SiriccoWindowControlled(MainViewModel vm, MainWindow? mainWindow = null)
        {
            InitializeComponent();
            _control = new SiriccoWindGaugeControl(vm, mainWindow);
            _control.ControlVisibility = Settings.Default.ShowSiriccoControls ? Visibility.Visible : Visibility.Collapsed;
            controlHost.Children.Add(_control);
            DataContext = _control;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Settings.Default.Save();
        }
    }
}
