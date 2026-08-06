using System.Windows;
using System.Windows.Controls;
using SwissTimingDisplay.Controls;

namespace SwissTimingDisplay.Controls
{
    public partial class SiriccoWindowToolbar : UserControl
    {
        public SiriccoWindowToolbar()
        {
            InitializeComponent();
        }

        private SiriccoWindGaugeControl Control => (SiriccoWindGaugeControl)DataContext;

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Control.ToggleStart();
        }

        private void ToggleControls_Click(object sender, RoutedEventArgs e)
        {
            Control.ControlVisibility = Control.ControlVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Control.ViewModel.BeginShutdown();
            Application.Current.Shutdown();
        }
    }
}
