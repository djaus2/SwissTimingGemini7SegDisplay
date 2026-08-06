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
        private readonly MainViewModel _vm;

        public SiriccoWindowControlled() : this(MainViewModel.SharedInstance, null)
        {
        }

        public SiriccoWindowControlled(MainViewModel vm, MainWindow? mainWindow = null)
        {
            _vm = vm;
            InitializeComponent();
            _control = new SiriccoWindGaugeControl(vm, mainWindow);
            _control.ControlVisibility = Settings.Default.ShowSiriccoControls ? Visibility.Visible : Visibility.Collapsed;
            controlHost.Children.Add(_control);
            DataContext = _control;
        }

        private void ToggleControls_Click(object sender, RoutedEventArgs e)
        {
            _control.ControlVisibility = _control.ControlVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _control.ToggleStart();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.BeginShutdown();
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Settings.Default.Save();
        }
    }
}
