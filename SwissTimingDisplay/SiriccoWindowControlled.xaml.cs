using System.Windows;
using SwissTimingDisplay.Controls;
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
    }
}
